using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// EnemyShieldBehaviour (OnTrigger-only, compatible con EnemigoEscudo y con IDamageable owners)
/// - Detecta colisiones por trigger y, si se trata del escudo del player (tag "Escudo"),
///   intenta forzarlo a volver llamando métodos por reflexión o SendMessage.
/// - Evita reprocesar el mismo objeto en un corto cooldown.
/// - Mantiene public bool HandleProjectileHit(...) para compatibilidad con EnemigoEscudo.
/// - Usa SafeCompareTag para evitar excepciones si una tag no está registrada.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyShieldBehaviour : MonoBehaviour
{
    [Tooltip("Si quieres mantener un fallback por OverlapSphere para casos raros, activa esto.")]
    [SerializeField] private bool useFallbackOverlap = false;
    [Tooltip("Radio usado por el fallback OverlapSphere para detectar proyectiles si no colisionan correctamente.")]
    [SerializeField] private float fallbackDetectRadius = 0.6f;
    [Tooltip("Cooldown (s) para no procesar el mismo proyectil varias veces seguidas.")]
    [SerializeField] private float handleCooldown = 0.25f;

    // ahora guardamos el owner como IDamageable (contrato) y su GameObject para accesos a transform, tag, etc.
    private IDamageable owner;
    private GameObject ownerGameObject;

    private Collider myCollider;
    private bool activo = true;
    private float frontalAngle = 120f;

    // evita reprocesar rápidamente el mismo objecto
    private readonly System.Collections.Generic.Dictionary<GameObject, float> lastHandledTime = new System.Collections.Generic.Dictionary<GameObject, float>();

    private void Awake()
    {
        myCollider = GetComponent<Collider>();
        if (myCollider != null)
        {
            // se recomienda trigger para esta versión
            myCollider.isTrigger = true;
        }

        // intentar auto-asignar owner buscando en padres cualquier MonoBehaviour que implemente IDamageable
        if (owner == null)
        {
            Transform t = transform.parent;
            while (t != null)
            {
                var mbs = t.GetComponents<MonoBehaviour>();
                foreach (var mb in mbs)
                {
                    if (mb is IDamageable idmg)
                    {
                        SetOwner(idmg);
                        break;
                    }
                }
                if (owner != null) break;
                t = t.parent;
            }
        }
    }

    private void OnEnable() => activo = true;
    private void OnDisable() => activo = false;

    /// <summary>
    /// Setea el owner como cualquier objeto que implemente IDamageable.
    /// También intenta almacenar el GameObject para accesos a transform/tag.
    /// </summary>
    public void SetOwner(IDamageable o)
    {
        owner = o;
        ownerGameObject = (o as MonoBehaviour)?.gameObject;
    }

    public void SetFrontalAngle(float angle) => frontalAngle = angle;
    public void SetActive(bool a) => activo = a;

    private void Update()
    {
        if (!activo) return;

        // Limpieza de entries antiguas
        float now = Time.time;
        var keys = new System.Collections.Generic.List<GameObject>(lastHandledTime.Keys);
        foreach (var k in keys)
        {
            if (now - lastHandledTime[k] > 5f) lastHandledTime.Remove(k);
        }

        // Fallback opcional (DESACTIVADO por defecto): detectar shields cercanos si no choca por trigger
        if (useFallbackOverlap)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, fallbackDetectRadius);
            foreach (var c in hits)
            {
                if (c == null) continue;
                GameObject candidate = c.gameObject;
                // preferir root (por si collider es child)
                if (candidate.transform.root != null) candidate = candidate.transform.root.gameObject;

                if (WasHandledRecently(candidate)) continue;

                if (IsLikelyPlayerShield(candidate))
                {
                    // usamos la position más cercana al collider del shield detectado
                    Vector3 hitPos = c.ClosestPoint(transform.position);
                    if (TryMakeShieldReturn(candidate))
                    {
                        MarkHandled(candidate);
                        Debug.Log("[EnemyShieldBehaviour] (Fallback) Ordenado retorno de escudo del player por OverlapSphere.");
                    }
                }
            }
        }
    }

    private bool WasHandledRecently(GameObject go)
    {
        if (go == null) return false;
        if (lastHandledTime.TryGetValue(go, out float t))
        {
            if (Time.time - t < handleCooldown) return true;
        }
        return false;
    }

    private void MarkHandled(GameObject go)
    {
        if (go == null) return;
        lastHandledTime[go] = Time.time;
    }

    /// <summary>
    /// ON TRIGGER: la lógica principal solicitada -> todo por OnTriggerEnter.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!activo || other == null) return;

        // obtener root del objeto colisionado (por si el collider es un child)
        GameObject candidateRoot = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;

        // prevenir que el enemigo se "dispare a sí mismo" usando ownerGameObject si existe
        if (ownerGameObject != null && (candidateRoot == ownerGameObject || candidateRoot == this.gameObject)) return;

        // si ya lo procesamos recientemente, ignorar
        if (WasHandledRecently(candidateRoot)) return;

        // Si parece ser el escudo del jugador, intentar forzar retorno
        if (IsLikelyPlayerShield(candidateRoot))
        {
            Vector3 hitPos = other.ClosestPoint(transform.position);
            bool ok = TryMakeShieldReturn(candidateRoot);
            if (ok)
            {
                MarkHandled(candidateRoot);
                Debug.Log($"[EnemyShieldBehaviour] OnTrigger: ordenado retorno del escudo player: {candidateRoot.name}");
                return;
            }
            else
            {
                // como último recurso si tiene Rigidbody, empujar ligeramente hacia el enemigo para "interrumpir" su trayectoria
                var rb = candidateRoot.GetComponent<Rigidbody>();
                if (rb != null && ownerGameObject != null)
                {
                    Vector3 dir = (ownerGameObject.transform.position - candidateRoot.transform.position).normalized;
                    float speed = Mathf.Max(6f, rb.linearVelocity.magnitude);
                    rb.linearVelocity = dir * speed;
                    MarkHandled(candidateRoot);
                    Debug.Log("[EnemyShieldBehaviour] OnTrigger: no pudo forzar retorno por reflexión, se aplicó fallback físico (empuje).");
                    return;
                }

                Debug.LogWarning("[EnemyShieldBehaviour] OnTrigger: no se pudo forzar retorno del escudo player y no tiene Rigidbody.");
            }
        }
    }

    /// <summary>
    /// Heurística ajustada: detecta el escudo del player usando la etiqueta "Escudo" (también hace comprobación segura).
    /// </summary>
    private bool IsLikelyPlayerShield(GameObject go)
    {
        if (go == null) return false;

        // evitar procesar shields del propio owner
        if (ownerGameObject != null && (go == ownerGameObject || go.transform.IsChildOf(ownerGameObject.transform))) return false;

        // ---- TAG seguro: "Escudo" ----
        if (SafeCompareTag(go, "Escudo")) return true;

        // si tiene un componente explícito de player-shield/manager
        if (go.GetComponent("PlayerShieldController") != null) return true;
        if (go.GetComponent("PlayerShield") != null) return true;

        // si tiene un componente 'Shield' es probable que sea el escudo
        var shieldComp = go.GetComponent("Shield");
        if (shieldComp != null)
        {
            // intentar ver si el root tiene tag Player
            if (go.transform.root != null && SafeCompareTag(go.transform.root.gameObject, "Player")) return true;

            // asumir true para compatibilidad amplia
            return true;
        }

        // heurística por nombre
        string name = go.name.ToLowerInvariant();
        if (name.Contains("shield") || name.Contains("escudo")) return true;

        return false;
    }

    /// <summary>
    /// Compara tags de forma segura, evitando lanzar excepción si la tag no está registrada en Project Settings.
    /// </summary>
    private bool SafeCompareTag(GameObject go, string tag)
    {
        if (go == null || string.IsNullOrEmpty(tag)) return false;
        try
        {
            return go.CompareTag(tag);
        }
        catch (UnityEngine.UnityException)
        {
            // la tag no existe en el proyecto → evitar crash y devolver false
            return false;
        }
    }

    /// <summary>
    /// Método público que EnemigoEscudo llama. Devuelve true si el proyectil/objeto fue manejado (p. ej. forzado a volver).
    /// Simplemente reutiliza la lógica que usa OnTrigger (intenta forzar retorno).
    /// </summary>
    public bool HandleProjectileHit(GameObject projRoot, Vector3 hitPos)
    {
        if (!activo || projRoot == null) return false;
        if (WasHandledRecently(projRoot)) return false;

        bool handled = TryMakeShieldReturn(projRoot);
        if (handled) MarkHandled(projRoot);
        return handled;
    }

    /// <summary>
    /// Intenta forzar que el Shield inicie su retorno.
    /// 1) intenta encontrar componentes y llamar métodos por reflexión (StartReturning, ReturnToPlayer, Volver...).
    /// 2) intenta SendMessage (optimista).
    /// Devuelve true si parece que se envió una orden válida.
    /// </summary>
    private bool TryMakeShieldReturn(GameObject proj)
    {
        if (proj == null) return false;

        // Si el objeto pertenece a enemigo, no intentamos (evitar confusiones)
        if (SafeCompareTag(proj, "Enemy") || (ownerGameObject != null && proj.transform.IsChildOf(ownerGameObject.transform))) return false;

        // nombres de métodos que intentaremos invocar
        string[] methodNames = { "StartReturning", "StartReturn", "ReturnToOwner", "ReturnToPlayer", "Return", "BeginReturn", "GoBack", "Regresar", "Volver" };

        // 1) buscar componentes y probar reflexión
        Component[] comps = proj.GetComponents<Component>();
        foreach (var comp in comps)
        {
            if (comp == null) continue;
            var t = comp.GetType();
            foreach (var name in methodNames)
            {
                try
                {
                    var mi = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (mi != null)
                    {
                        mi.Invoke(comp, null);
                        Debug.Log($"[EnemyShieldBehaviour] Reflection invocado: {t.Name}.{name}() en {proj.name}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[EnemyShieldBehaviour] Reflection fallo {t.Name}.{name}(): {ex.Message}");
                }
            }
        }

        // 2) fallback general: intentar SendMessage con nombres comunes
        try
        {
            foreach (var name in methodNames)
            {
                proj.SendMessage(name, SendMessageOptions.DontRequireReceiver);
            }
            Debug.Log($"[EnemyShieldBehaviour] SendMessage(s) enviados a {proj.name} como fallback.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[EnemyShieldBehaviour] SendMessage fallo: {ex.Message}");
        }

        return false;
    }
}


