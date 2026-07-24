// ArmaduraDemonicaArea.cs
using System.Collections;
using System.Reflection;
using UnityEngine;

public class ArmaduraDemonicaArea : MonoBehaviour
{
    [Header("Armadura")]
    [SerializeField, Range(0f, 1f)] private float reduccionPercent = 0.25f;
    [SerializeField] private float duracion = 10f;
    [SerializeField] private float cooldown = 4.5f;
    [SerializeField] private float radio = 8f;
    [SerializeField] private LayerMask capasAfectadas = ~0;

    [Header("Prefab del área (preferido)")]
    [Tooltip("Prefab que contiene ArmaduraAreaPrefab. Si está vacío, se creará runtime como fallback.")]
    [SerializeField] private GameObject areaPrefab;

    [Header("Spawn")]
    [Tooltip("Transform donde se CREARÁ/POSICIONAR el prefab del área (NO será hijo). Si es null usa la posición del propio enemigo.")]
    [SerializeField] private Transform areaSpawnPoint;

    [Header("Visual / Opciones")]
    [SerializeField] private Color visualColor = new Color(0f, 1f, 0f, 0.28f);
    [SerializeField] private float visualThickness = 0.08f;
    [SerializeField] private float flattenHeightThreshold = 1.2f;

    [Header("Debug / Test")]
    [SerializeField] private float checkInterval = 0.25f;

    [Header("Momentáneo (tecla L)")]
    [SerializeField] private float momentaryDuration = 0.15f;

    [Header("Instancia — comportamiento")]
    [Tooltip("Si true, la instancia del prefab se destruirá automáticamente tras desactivarse. Si false, se conservará para reusar.")]
    [SerializeField] private bool destroyAreaInstanceAfterUse = true;
    [Tooltip("Retraso en segundos antes de destruir la instancia tras desactivarse (0 = inmediato).")]
    [SerializeField] private float destroyDelayAfterDeactivate = 0f;

    // runtime
    private GameObject areaInstance;                 // instancia DEL PREFAB (se crea solo cuando hace falta)
    private ArmaduraAreaPrefab areaPrefabComp;       // componente manejador del prefab
    private bool activa = false;
    private bool enCooldown = false;
    private Coroutine rutina = null;

    // referencia para leer vida (solo para monitorizar porcentajes)
    private IDamageable damageableComponent = null;

    private void Awake()
    {
        // buscar componente IDamageable (p. ej. VidaEnemigoEscudo) para monitorizar vida
        foreach (var mb in GetComponents<MonoBehaviour>())
        {
            if (mb is IDamageable idmg) { damageableComponent = idmg; break; }
        }

        // Nota: NO creamos ni instanciamos el prefab aquí — creación 'lazy' cuando se necesite.
    }

    private void Start()
    {
        StartCoroutine(HealthMonitor());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            Vector3 spawnPos = GetSpawnPosition();
            CreateAreaInstanceIfNeeded(spawnPos);
            if (areaPrefabComp != null)
            {
                areaPrefabComp.ActivateMomentaryAndApply(spawnPos, momentaryDuration);
            }
        }
    }

    private Vector3 GetSpawnPosition()
    {
        return (areaSpawnPoint != null) ? areaSpawnPoint.position : transform.position;
    }

    private void CreateAreaInstanceIfNeeded(Vector3 spawnPos)
    {
        if (areaInstance != null && areaPrefabComp != null) return; // ya existe

        if (areaPrefab != null)
        {
            areaInstance = Instantiate(areaPrefab); // NO parent
            areaInstance.name = "ArmaduraArea_Instance";
            areaInstance.transform.position = spawnPos;
            areaInstance.transform.rotation = Quaternion.identity;

            areaPrefabComp = areaInstance.GetComponent<ArmaduraAreaPrefab>();
            if (areaPrefabComp == null)
            {
                Debug.LogWarning($"[{name}] areaPrefab no contiene ArmaduraAreaPrefab. Añadiendo componente automáticamente.");
                areaPrefabComp = areaInstance.AddComponent<ArmaduraAreaPrefab>();
            }
        }
        else
        {
            areaInstance = new GameObject("ArmaduraArea_Runtime");
            areaInstance.transform.position = spawnPos;
            areaPrefabComp = areaInstance.AddComponent<ArmaduraAreaPrefab>();
        }

        // configurar si tiene Initialize (varias firmas)
        if (areaPrefabComp != null)
        {
            MethodInfo mi = areaPrefabComp.GetType().GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
            if (mi != null)
            {
                var parms = mi.GetParameters();
                try
                {
                    if (parms.Length == 6 && parms[0].ParameterType == typeof(ArmaduraDemonicaArea))
                    {
                        mi.Invoke(areaPrefabComp, new object[] { this, radio, visualColor, visualThickness, flattenHeightThreshold, capasAfectadas });
                    }
                    else if (parms.Length == 3)
                    {
                        mi.Invoke(areaPrefabComp, new object[] { radio, flattenHeightThreshold, capasAfectadas });
                    }
                    else if (parms.Length == 5)
                    {
                        mi.Invoke(areaPrefabComp, new object[] { radio, visualColor, visualThickness, flattenHeightThreshold, capasAfectadas });
                    }
                }
                catch { /* no bloquee si falla la invocación */ }
            }

            // --- CAMBIO: siempre asignar explícitamente al prefab los valores del enemigo ---
            areaPrefabComp.ReductionPercent = reduccionPercent;
            areaPrefabComp.Duration = duracion;
            // -------------------------------------------------------------------------

            // mantener el collider desactivado hasta la activación explícita
            areaPrefabComp.DeactivateArea();
        }

        Debug.Log($"[{name}] Area instance creada (lazy) en {spawnPos}.");
    }

    private IEnumerator HealthMonitor()
    {
        if (damageableComponent == null) yield break;

        while (true)
        {
            if (!activa && !enCooldown && damageableComponent != null)
            {
                float percent = damageableComponent.CurrentHealth / Mathf.Max(1f, damageableComponent.MaxHealth);
                if (percent <= 0.25f)
                {
                    ActivateArmadura();
                }
            }
            yield return new WaitForSeconds(checkInterval);
        }
    }

    public void ForceActivate()
    {
        if (rutina != null) StopCoroutine(rutina);
        enCooldown = false;
        ActivateArmadura();
    }

    private void ActivateArmadura()
    {
        if (activa || enCooldown) return;
        activa = true;

        Vector3 spawnPos = GetSpawnPosition();

        // crear lazy si hace falta
        CreateAreaInstanceIfNeeded(spawnPos);

        if (areaPrefabComp != null)
        {
            areaPrefabComp.SetLayerMask(capasAfectadas);
            areaPrefabComp.SetFlattenHeight(flattenHeightThreshold);
            areaPrefabComp.SetRadius(radio);

            if (areaInstance != null) areaInstance.transform.position = spawnPos;

            areaPrefabComp.ActivateFor(duracion, spawnPos);
        }
        else
        {
            Collider[] hits = Physics.OverlapSphere(spawnPos, radio, capasAfectadas, QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
                if (root == this.gameObject) continue;
                if (Mathf.Abs(root.transform.position.y - spawnPos.y) > flattenHeightThreshold) continue;
                TryApplyReductionTo(root, reduccionPercent, duracion);
            }

            TryApplyReductionTo(this.gameObject, reduccionPercent, duracion);
        }

        rutina = StartCoroutine(ArmaduraDurationAndCooldown());
        Debug.Log($"[{name}] Armadura ACTIVADA en spawnPos {spawnPos}. Radio={radio}");
    }

    private IEnumerator ArmaduraDurationAndCooldown()
    {
        yield return new WaitForSeconds(duracion);
        DeactivateArmadura();

        enCooldown = true;
        yield return new WaitForSeconds(cooldown);
        enCooldown = false;
        rutina = null;
        Debug.Log($"[{name}] Armadura COOLDOWN finalizado.");
    }

    private void DeactivateArmadura()
    {
        activa = false;
        if (areaPrefabComp != null) areaPrefabComp.DeactivateArea();

        // Si el usuario quiere destruir la instancia después de su uso, hacerlo aquí
        if (destroyAreaInstanceAfterUse && areaInstance != null)
        {
            // programar destrucción con delay configurable
            if (destroyDelayAfterDeactivate <= 0f)
            {
                Destroy(areaInstance);
            }
            else
            {
                Destroy(areaInstance, destroyDelayAfterDeactivate);
            }

            // limpiar referencias inmediatamente para evitar usos posteriores accidentales
            areaInstance = null;
            areaPrefabComp = null;

            Debug.Log($"[{name}] Area instance programada para destrucción (delay={destroyDelayAfterDeactivate}).");
        }

        Debug.Log($"[{name}] Armadura DESACTIVADA.");
    }

    private void OnDestroy()
    {
        if (areaInstance != null)
        {
            Destroy(areaInstance);
            areaInstance = null;
            areaPrefabComp = null;
        }
    }

    private void TryApplyReductionTo(GameObject target, float percent, float dur)
    {
        if (target == null) return;

        foreach (var mb in target.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;
            var mi = mb.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
            if (mi != null)
            {
                try
                {
                    mi.Invoke(mb, new object[] { percent, dur });
                    Debug.Log($"[ArmaduraDemonicaArea] Aplicada reducción a {target.name} vía {mb.GetType().Name}.ApplyDamageReduction()");
                    return;
                }
                catch { /* seguir intentando */ }
            }
        }

        var vidaEscudo = target.GetComponent("VidaEnemigoEscudo");
        if (vidaEscudo != null)
        {
            var mi = vidaEscudo.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
            if (mi != null)
            {
                try { mi.Invoke(vidaEscudo, new object[] { percent, dur }); Debug.Log($"[ArmaduraDemonicaArea] Aplicada reducción a {target.name} vía VidaEnemigoEscudo."); return; }
                catch { }
            }
        }

        var enemyH = target.GetComponent("EnemyHealth");
        if (enemyH != null)
        {
            var mi = enemyH.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
            if (mi != null)
            {
                try { mi.Invoke(enemyH, new object[] { percent, dur }); Debug.Log($"[ArmaduraDemonicaArea] Aplicada reducción a {target.name} vía EnemyHealth."); return; }
                catch { }
            }
        }

        try
        {
            target.SendMessage("ApplyDamageReduction", new object[] { percent, dur }, SendMessageOptions.DontRequireReceiver);
            Debug.Log($"[ArmaduraDemonicaArea] SendMessage ApplyDamageReduction enviado a {target.name} como fallback.");
        }
        catch { }
    }

    /// <summary>
    /// Método invocado por SendMessage desde el prefab cuando éste termina su momentary/lifecycle
    /// Se espera recibir la referencia al GameObject del prefab (areaInstance).
    /// </summary>
    /// <param name="areaGo">GameObject del prefab que se desactivó</param>
    private void OnAreaDeactivated(GameObject areaGo)
    {
        if (areaGo == null) return;
        if (areaInstance == null) return;
        if (areaInstance != areaGo) return; // sólo gestionar si es nuestra instancia

        if (destroyAreaInstanceAfterUse && areaInstance != null)
        {
            if (destroyDelayAfterDeactivate <= 0f)
                Destroy(areaInstance);
            else
                Destroy(areaInstance, destroyDelayAfterDeactivate);
        }

        areaInstance = null;
        areaPrefabComp = null;

        Debug.Log($"[{name}] Area instance destruida por OnAreaDeactivated (delay={destroyDelayAfterDeactivate}).");
    }
}



