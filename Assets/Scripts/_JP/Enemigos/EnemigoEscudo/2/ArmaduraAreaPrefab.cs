// ArmaduraAreaPrefab.cs (robusto: busca componentes en children/parents y parenta visual al GameObject donde se encontró)
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

[DisallowMultipleComponent]
public class ArmaduraAreaPrefab : MonoBehaviour
{
    [Header("Opciones del efecto (ahora en el prefab)")]
    [SerializeField, Range(0f, 1f)] private float reduccionPercent = 0.25f;
    [SerializeField] private float duracion = 10f; // ahora único tiempo (duración y mínimo visible)

    [Header("Comportamiento adicional")]
    [Tooltip("Intervalo en segundos para reaplicar efecto a entidades dentro del área.")]
    [SerializeField] private float applyInterval = 1f;

    [Header("Visual por target (opcional)")]
    [Tooltip("Prefab que se instanciará como hijo del target cuando reciba la reducción. Puede ser un simple GameObject con efectos visuales.")]
    [SerializeField] private GameObject reductionVisualPrefab = null;
    [Tooltip("Offset local en Y para colocar el visual encima del target (en unidades locales).")]
    [SerializeField] private float reductionVisualYOffset = 1.5f;
    [Tooltip("Si true, el visual será parented al target. Si false, se posicionará en mundo pero no será hijo.")]
    [SerializeField] private bool attachVisualAsChild = true;

    private Collider col;
    private ArmaduraDemonicaArea owner;
    private float flattenHeightThreshold = 1.2f;
    private LayerMask capasAfectadas = ~0;

    // corrutinas internas
    private Coroutine lifecycleRoutine = null;    // activaciones completas
    private Coroutine momentaryRoutine = null;    // activaciones momentáneas

    // para reaplicar mientras están dentro (key = root GameObject used for overlap/trigger)
    private Dictionary<GameObject, float> lastApplyTime = new Dictionary<GameObject, float>();

    // TRACKING de visuales instanciados por targetComponentGameObject (key = gameObject donde se encontró el componente)
    private readonly Dictionary<GameObject, GameObject> reductionVisualInstances = new Dictionary<GameObject, GameObject>();

    public float ReductionPercent { get => reduccionPercent; set => reduccionPercent = Mathf.Clamp01(value); }
    public float Duration { get => duracion; set => duracion = Mathf.Max(0f, value); }

    private void Awake()
    {
        col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError($"[{name}] ArmaduraAreaPrefab requiere un Collider (Box/Capsule/Sphere) ya configurado en este GameObject.");
        }
        else
        {
            if (!col.isTrigger)
            {
                Debug.LogWarning($"[{name}] Collider no está en modo trigger. Forzando isTrigger = true.");
                col.isTrigger = true;
            }
        }
    }

    public void Initialize(ArmaduraDemonicaArea owner, float radius, Color color, float thickness, float flattenHeight, LayerMask capas)
    {
        this.owner = owner;
        this.flattenHeightThreshold = flattenHeight;
        this.capasAfectadas = capas;

        if (col == null)
        {
            col = GetComponent<Collider>();
            if (col == null) Debug.LogWarning($"[{name}] Initialize: Collider ausente. No se puede ajustar tamaño.");
        }

        if (col is SphereCollider sc) sc.radius = Mathf.Max(0.01f, radius);
        else if (col is BoxCollider bc)
        {
            Vector3 size = bc.size;
            float y = size.y;
            bc.size = new Vector3(radius * 2f / transform.lossyScale.x, y, radius * 2f / transform.lossyScale.z);
        }
        else if (col is CapsuleCollider cc)
        {
            cc.radius = Mathf.Max(0.01f, radius / Mathf.Max(1f, transform.lossyScale.x));
            cc.height = Mathf.Max(cc.height, (radius * 2f) / Mathf.Max(0.0001f, transform.lossyScale.y));
        }
    }

    public void SetLayerMask(LayerMask m) { capasAfectadas = m; }
    public void SetFlattenHeight(float h) { flattenHeightThreshold = h; }

    public void SetRadius(float r)
    {
        if (col == null) { Debug.LogWarning($"[{name}] SetRadius llamado pero Collider ausente."); return; }

        if (col is SphereCollider sc) sc.radius = Mathf.Max(0.01f, r);
        else if (col is BoxCollider bc)
        {
            Vector3 size = bc.size;
            bc.size = new Vector3(r * 2f / transform.lossyScale.x, size.y, r * 2f / transform.lossyScale.z);
        }
        else if (col is CapsuleCollider cc)
        {
            cc.radius = Mathf.Max(0.01f, r / Mathf.Max(0.0001f, transform.lossyScale.x));
            cc.height = Mathf.Max(cc.height, (r * 2f) / Mathf.Max(0.0001f, transform.lossyScale.y));
        }
        else
        {
            Debug.LogWarning($"[{name}] SetRadius: tipo de Collider no soportado para ajuste automático.");
        }
    }

    public void ActivateArea() { if (col != null) col.enabled = true; else Debug.LogWarning($"[{name}] ActivateArea: Collider ausente."); }
    public void DeactivateArea() { if (col != null) col.enabled = false; else Debug.LogWarning($"[{name}] DeactivateArea: Collider ausente."); }

    private void OnTriggerEnter(Collider other) => HandleTriggerEnterOrStay(other);
    private void OnTriggerStay(Collider other) => HandleTriggerEnterOrStay(other);

    private void HandleTriggerEnterOrStay(Collider other)
    {
        if (col == null) return;
        if (((1 << other.gameObject.layer) & capasAfectadas) == 0) return;

        // root (usada para tracking de entradas/salidas)
        var root = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
        if (root == null) return;
        if (owner != null && root == owner.gameObject) return;
        if (Mathf.Abs(root.transform.position.y - transform.position.y) > flattenHeightThreshold) return;

        float tNow = Time.time;
        if (!lastApplyTime.TryGetValue(root, out float last) || tNow - last >= applyInterval)
        {
            // intentar aplicar reducción tratando de localizar el componente en la jerarquía (hitObject -> parents -> children)
            ApplyReductionRobust(root, other.gameObject, reduccionPercent, duracion);
            lastApplyTime[root] = tNow;

            if (owner != null)
            {
                try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); }
                catch { }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var root = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
        if (root != null)
        {
            if (lastApplyTime.ContainsKey(root)) lastApplyTime.Remove(root);

            // Notificar al target que salió de ESTA área (para que quite reducción asociada a este prefab)
            try
            {
                root.SendMessage("RemoveDamageReductionFromArea", this.gameObject, SendMessageOptions.DontRequireReceiver);
                Debug.Log($"[ArmaduraAreaPrefab] RemoveDamageReductionFromArea enviado a {root.name} (salida).");
            }
            catch { }

            // limpiar visuales asociados cuyos attachTo sean hijos/descendientes o iguales al root
            RemoveReductionVisualsAssociatedWithRoot(root);
        }
    }

    public void ActivateFor(float duration, Vector3 center)
    {
        transform.position = center;

        if (momentaryRoutine != null) { StopCoroutine(momentaryRoutine); momentaryRoutine = null; }
        if (lifecycleRoutine != null) StopCoroutine(lifecycleRoutine);
        lifecycleRoutine = StartCoroutine(LifecycleRoutine(duration, center));
    }

    private IEnumerator LifecycleRoutine(float duration, Vector3 center)
    {
        transform.position = center;
        ActivateArea();

        // aplicarlo al owner si existe
        if (owner != null)
        {
            ApplyReductionRobust(owner.gameObject, owner.gameObject, reduccionPercent, duration);
            lastApplyTime[owner.gameObject] = Time.time;
            try { owner.gameObject.SendMessage("OnEntityEnterArea", owner.gameObject, SendMessageOptions.DontRequireReceiver); } catch { }
        }

        Collider[] hits = GetOverlaps(center);
        foreach (var c in hits)
        {
            GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
            if (owner != null && root == owner.gameObject) continue;
            if (Mathf.Abs(root.transform.position.y - (owner != null ? owner.transform.position.y : center.y)) > flattenHeightThreshold) continue;
            ApplyReductionRobust(root, c.gameObject, reduccionPercent, duration);
            lastApplyTime[root] = Time.time;

            if (owner != null)
            {
                try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); } catch { }
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, duration));
        DeactivateArea();

        // Antes de notificar al owner, limpiar notificaciones en todos los targets aún registrados
        var targets = new List<GameObject>(lastApplyTime.Keys);
        foreach (var t in targets)
        {
            try
            {
                t.SendMessage("RemoveDamageReductionFromArea", this.gameObject, SendMessageOptions.DontRequireReceiver);
            }
            catch { }

            // destruir visual asociado a cada target (buscando attachTo dentro del root)
            RemoveReductionVisualsAssociatedWithRoot(t);
        }
        lastApplyTime.Clear();

        // Notificar al owner que esta instancia ya terminó su lifecycle (para que el owner pueda destruir/limpiar si corresponde)
        NotifyOwnerAreaDeactivated();

        lifecycleRoutine = null;
    }

    public void ActivateMomentaryAndApply(Vector3 center, float momentDuration)
    {
        transform.position = center;

        if (lifecycleRoutine != null)
        {
            Collider[] hits = GetOverlaps(center);
            foreach (var c in hits)
            {
                GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
                if (owner != null && root == owner.gameObject) continue;
                if (Mathf.Abs(root.transform.position.y - (owner != null ? owner.transform.position.y : center.y)) > flattenHeightThreshold) continue;
                ApplyReductionRobust(root, c.gameObject, reduccionPercent, duracion);
                lastApplyTime[root] = Time.time;

                if (owner != null)
                {
                    try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); } catch { }
                }
            }
            return;
        }

        if (momentaryRoutine != null) StopCoroutine(momentaryRoutine);
        momentaryRoutine = StartCoroutine(MomentaryRoutine(center, Mathf.Max(0.01f, momentDuration)));
    }

    private IEnumerator MomentaryRoutine(Vector3 center, float momentDuration)
    {
        transform.position = center;
        ActivateArea();

        if (owner != null)
        {
            ApplyReductionRobust(owner.gameObject, owner.gameObject, reduccionPercent, duracion);
            lastApplyTime[owner.gameObject] = Time.time;
            try { owner.gameObject.SendMessage("OnEntityEnterArea", owner.gameObject, SendMessageOptions.DontRequireReceiver); } catch { }
        }

        Collider[] hits = GetOverlaps(center);
        foreach (var c in hits)
        {
            GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
            if (owner != null && root == owner.gameObject) continue;
            if (Mathf.Abs(root.transform.position.y - (owner != null ? owner.transform.position.y : center.y)) > flattenHeightThreshold) continue;
            ApplyReductionRobust(root, c.gameObject, reduccionPercent, duracion);
            lastApplyTime[root] = Time.time;

            if (owner != null)
            {
                try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); } catch { }
            }
        }

        float waitTime = Mathf.Max(momentDuration, this.duracion);
        yield return new WaitForSeconds(waitTime);

        DeactivateArea();

        // limpiar y notificar a targets
        var targets = new List<GameObject>(lastApplyTime.Keys);
        foreach (var t in targets)
        {
            try { t.SendMessage("RemoveDamageReductionFromArea", this.gameObject, SendMessageOptions.DontRequireReceiver); } catch { }

            // destruir visual asociado
            RemoveReductionVisualsAssociatedWithRoot(t);
        }
        lastApplyTime.Clear();

        NotifyOwnerAreaDeactivated();

        momentaryRoutine = null;
    }

    private Collider[] GetOverlaps(Vector3 worldCenter)
    {
        if (col == null)
        {
            return Physics.OverlapSphere(worldCenter, 0.01f, capasAfectadas, QueryTriggerInteraction.Ignore);
        }

        if (col is SphereCollider sc)
        {
            Vector3 center = sc.transform.TransformPoint(sc.center);
            float radiusWorld = sc.radius * MaxScale(sc.transform);
            return Physics.OverlapSphere(center, Mathf.Max(0.001f, radiusWorld), capasAfectadas, QueryTriggerInteraction.Ignore);
        }
        else if (col is BoxCollider bc)
        {
            Vector3 center = bc.transform.TransformPoint(bc.center);
            Vector3 halfExtents = Vector3.Scale(bc.size * 0.5f, bc.transform.lossyScale);
            Quaternion orientation = bc.transform.rotation;
            return Physics.OverlapBox(center, Vector3.Max(halfExtents, Vector3.one * 0.001f), orientation, capasAfectadas, QueryTriggerInteraction.Ignore);
        }
        else if (col is CapsuleCollider cc)
        {
            Transform t = cc.transform;
            Vector3 centerLocal = cc.center;
            int dir = cc.direction;
            Vector3 axis = (dir == 0) ? Vector3.right : (dir == 1) ? Vector3.up : Vector3.forward;
            float radiusWorld = cc.radius * MaxScaleOnAxes(t, dir == 1 ? new int[] { 0, 2 } : new int[] { 0, 1, 2 });
            float halfSeg = Mathf.Max(0f, (cc.height * 0.5f) - cc.radius);
            Vector3 worldA = t.TransformPoint(centerLocal + axis * halfSeg);
            Vector3 worldB = t.TransformPoint(centerLocal - axis * halfSeg);
            return Physics.OverlapCapsule(worldA, worldB, Mathf.Max(0.001f, radiusWorld), capasAfectadas, QueryTriggerInteraction.Ignore);
        }
        else
        {
            Bounds b = col.bounds;
            return Physics.OverlapBox(b.center, b.extents, Quaternion.identity, capasAfectadas, QueryTriggerInteraction.Ignore);
        }
    }

    private float MaxScale(Transform t) => Mathf.Max(t.lossyScale.x, t.lossyScale.y, t.lossyScale.z);

    private float MaxScaleOnAxes(Transform t, int[] axes)
    {
        float max = 0f;
        foreach (var a in axes)
        {
            if (a == 0) max = Mathf.Max(max, t.lossyScale.x);
            else if (a == 1) max = Mathf.Max(max, t.lossyScale.y);
            else if (a == 2) max = Mathf.Max(max, t.lossyScale.z);
        }
        return Mathf.Max(0.0001f, max);
    }

    /// <summary>
    /// Versión robusta que intenta encontrar el componente en hitObject (collider.gameObject), en sus padres y en sus children.
    /// Si se encuentra, invoca el método en la instancia concreta y crea el visual parentado a esa instancia.
    /// </summary>
    private void ApplyReductionRobust(GameObject root, GameObject hitObject, float percent, float dur)
    {
        if (root == null || hitObject == null) return;

        float effectivePercent = Mathf.Clamp01(reduccionPercent);
        float effectiveDur = Mathf.Max(0f, duracion > 0f ? duracion : dur);

        // 1) intentar encontrar MonoBehaviour con ApplyDamageReductionFromArea / ApplyDamageReduction (look in hitObject's hierarchy and parents)
        MonoBehaviour foundMb = TryFindDamageComponentInHierarchy(hitObject);
        if (foundMb == null)
        {
            // fallback: intentar en el root entero
            foundMb = TryFindDamageComponentInHierarchy(root);
        }

        bool invoked = false;
        GameObject attachTo = null;

        if (foundMb != null)
        {
            attachTo = foundMb.gameObject;

            // intentar ApplyDamageReductionFromArea
            var miNew = foundMb.GetType().GetMethod("ApplyDamageReductionFromArea", BindingFlags.Public | BindingFlags.Instance);
            if (miNew != null)
            {
                try { miNew.Invoke(foundMb, new object[] { effectivePercent, effectiveDur, this.gameObject }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducción FROM AREA ({effectivePercent}) a {attachTo.name} via {foundMb.GetType().Name}.ApplyDamageReductionFromArea()"); }
                catch { invoked = false; }
            }

            // si no, intentar ApplyDamageReduction
            if (!invoked)
            {
                var miLegacy = foundMb.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
                if (miLegacy != null)
                {
                    try { miLegacy.Invoke(foundMb, new object[] { effectivePercent, effectiveDur }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducción (legacy) a {attachTo.name} via {foundMb.GetType().Name}.ApplyDamageReduction()"); }
                    catch { invoked = false; }
                }
            }
        }

        // si todavía no se invocó nada, intentar SendMessage al hitObject y luego al root (fallbacks)
        if (!invoked)
        {
            try
            {
                hitObject.SendMessage("ApplyDamageReductionFromArea", new object[] { effectivePercent, effectiveDur, this.gameObject }, SendMessageOptions.DontRequireReceiver);
                invoked = true;
                attachTo = hitObject;
                Debug.Log($"[ArmaduraAreaPrefab] SendMessage ApplyDamageReductionFromArea enviado a {hitObject.name} (fallback).");
            }
            catch { }
        }

        if (!invoked)
        {
            try
            {
                root.SendMessage("ApplyDamageReductionFromArea", new object[] { effectivePercent, effectiveDur, this.gameObject }, SendMessageOptions.DontRequireReceiver);
                invoked = true;
                attachTo = root;
                Debug.Log($"[ArmaduraAreaPrefab] SendMessage ApplyDamageReductionFromArea enviado a {root.name} (fallback root).");
            }
            catch { }
        }

        if (!invoked)
        {
            try
            {
                hitObject.SendMessage("ApplyDamageReduction", new object[] { effectivePercent, effectiveDur }, SendMessageOptions.DontRequireReceiver);
                invoked = true;
                attachTo = hitObject;
                Debug.Log($"[ArmaduraAreaPrefab] SendMessage ApplyDamageReduction enviado a {hitObject.name} (fallback legacy).");
            }
            catch { }
        }

        if (!invoked)
        {
            try
            {
                root.SendMessage("ApplyDamageReduction", new object[] { effectivePercent, effectiveDur }, SendMessageOptions.DontRequireReceiver);
                invoked = true;
                attachTo = root;
                Debug.Log($"[ArmaduraAreaPrefab] SendMessage ApplyDamageReduction enviado a {root.name} (fallback root legacy).");
            }
            catch { }
        }

        // si se aplicó/intentó, crear el visual parentado al attachTo (si no se encontró attachTo usar root)
        if (invoked)
        {
            if (attachTo == null) attachTo = hitObject != null ? hitObject : root;
            CreateReductionVisualFor(attachTo);
        }
    }

    /// <summary>
    /// Busca en hitObject: children (incl. inactive) y hacia arriba en padres por cualquier MonoBehaviour
    /// que contenga ApplyDamageReductionFromArea o ApplyDamageReduction y devuelve la instancia.
    /// </summary>
    private MonoBehaviour TryFindDamageComponentInHierarchy(GameObject start)
    {
        if (start == null) return null;

        // 1) buscar en children (incluye el mismo start)
        var childs = start.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in childs)
        {
            if (mb == null) continue;
            // priorizar método nuevo
            if (mb.GetType().GetMethod("ApplyDamageReductionFromArea", BindingFlags.Public | BindingFlags.Instance) != null) return mb;
            if (mb.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance) != null) return mb;
        }

        // 2) buscar hacia arriba en los padres (no incluye start otra vez)
        Transform p = start.transform.parent;
        while (p != null)
        {
            var pMbs = p.GetComponents<MonoBehaviour>();
            foreach (var mb in pMbs)
            {
                if (mb == null) continue;
                if (mb.GetType().GetMethod("ApplyDamageReductionFromArea", BindingFlags.Public | BindingFlags.Instance) != null) return mb;
                if (mb.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance) != null) return mb;
            }
            p = p.parent;
        }

        return null;
    }

    private void NotifyOwnerAreaDeactivated()
    {
        if (owner != null)
        {
            try { owner.gameObject.SendMessage("OnAreaDeactivated", this.gameObject, SendMessageOptions.DontRequireReceiver); }
            catch { }
        }
    }

    #region --- Visual helpers (create/destroy per-target) ---
    // Aquí la key es el GameObject donde se parenta el visual (attachTo)
    private void CreateReductionVisualFor(GameObject attachTo)
    {
        if (reductionVisualPrefab == null || attachTo == null) return;

        // si ya existe para ese attachTo, actualizar posición/parent si es necesario
        if (reductionVisualInstances.TryGetValue(attachTo, out GameObject existing) && existing != null)
        {
            if (attachVisualAsChild)
            {
                if (existing.transform.parent == attachTo.transform)
                {
                    existing.transform.localPosition = Vector3.up * reductionVisualYOffset;
                }
                else
                {
                    existing.transform.SetParent(attachTo.transform, false);
                    existing.transform.localPosition = Vector3.up * reductionVisualYOffset;
                    existing.transform.localRotation = Quaternion.identity;
                }
            }
            else
            {
                existing.transform.SetParent(null);
                existing.transform.position = attachTo.transform.position + Vector3.up * reductionVisualYOffset;
                existing.transform.rotation = Quaternion.identity;
            }
            return;
        }

        GameObject inst;
        try
        {
            inst = Instantiate(reductionVisualPrefab);
        }
        catch
        {
            Debug.LogWarning($"[ArmaduraAreaPrefab] No se pudo instanciar reductionVisualPrefab para {attachTo.name}.");
            return;
        }

        inst.name = $"{attachTo.name}_ArmaduraVisual";

        if (attachVisualAsChild)
        {
            inst.transform.SetParent(attachTo.transform, false);
            inst.transform.localPosition = Vector3.up * reductionVisualYOffset;
            inst.transform.localRotation = Quaternion.identity;
        }
        else
        {
            inst.transform.SetParent(null);
            inst.transform.position = attachTo.transform.position + Vector3.up * reductionVisualYOffset;
            inst.transform.rotation = Quaternion.identity;
        }

        reductionVisualInstances[attachTo] = inst;
    }

    private void RemoveReductionVisualFor(GameObject keyAttach)
    {
        if (keyAttach == null) return;
        if (reductionVisualInstances.TryGetValue(keyAttach, out GameObject inst))
        {
            if (inst != null)
            {
                Destroy(inst);
            }
            reductionVisualInstances.Remove(keyAttach);
        }
    }

    // Elimina todos los visuales cuyo attachTo sea igual o descendiente del root
    private void RemoveReductionVisualsAssociatedWithRoot(GameObject root)
    {
        if (root == null) return;

        var keys = new List<GameObject>(reductionVisualInstances.Keys);
        foreach (var k in keys)
        {
            if (k == null) { reductionVisualInstances.Remove(k); continue; }
            // si k es el root o k está dentro de root -> eliminar
            if (k == root || IsDescendantOf(k.transform, root.transform))
            {
                RemoveReductionVisualFor(k);
            }
        }
    }

    private bool IsDescendantOf(Transform child, Transform potentialAncestor)
    {
        if (child == null || potentialAncestor == null) return false;
        Transform t = child;
        while (t != null)
        {
            if (t == potentialAncestor) return true;
            t = t.parent;
        }
        return false;
    }

    // Limpia todos los visuales (usado cuando el prefab se desactiva / destruye)
    private void RemoveAllReductionVisuals()
    {
        var keys = new List<GameObject>(reductionVisualInstances.Keys);
        foreach (var k in keys) RemoveReductionVisualFor(k);
    }
    #endregion

    private void OnDisable()
    {
        // Asegurar limpieza de visuales si el prefab se desactiva
        RemoveAllReductionVisuals();
    }
}



//// ArmaduraAreaPrefab.cs (actualizado)
//using UnityEngine;
//using System.Collections;
//using System.Collections.Generic;
//using System.Reflection;

//[DisallowMultipleComponent]
//public class ArmaduraAreaPrefab : MonoBehaviour
//{
//    [Header("Opciones del efecto (ahora en el prefab)")]
//    [SerializeField, Range(0f, 1f)] private float reduccionPercent = 0.25f;
//    [SerializeField] private float duracion = 10f; // ahora único tiempo (duración y mínimo visible)

//    [Header("Comportamiento adicional")]
//    [Tooltip("Intervalo en segundos para reaplicar efecto a entidades dentro del área.")]
//    [SerializeField] private float applyInterval = 1f;

//    private Collider col;
//    private ArmaduraDemonicaArea owner;
//    private float flattenHeightThreshold = 1.2f;
//    private LayerMask capasAfectadas = ~0;

//    // corrutinas internas
//    private Coroutine lifecycleRoutine = null;    // activaciones completas
//    private Coroutine momentaryRoutine = null;    // activaciones momentáneas

//    // para reaplicar mientras están dentro (y para poder notificar limpieza)
//    private Dictionary<GameObject, float> lastApplyTime = new Dictionary<GameObject, float>();

//    public float ReductionPercent { get => reduccionPercent; set => reduccionPercent = Mathf.Clamp01(value); }
//    public float Duration { get => duracion; set => duracion = Mathf.Max(0f, value); }

//    private void Awake()
//    {
//        col = GetComponent<Collider>();
//        if (col == null)
//        {
//            Debug.LogError($"[{name}] ArmaduraAreaPrefab requiere un Collider (Box/Capsule/Sphere) ya configurado en este GameObject.");
//        }
//        else
//        {
//            if (!col.isTrigger)
//            {
//                Debug.LogWarning($"[{name}] Collider no está en modo trigger. Forzando isTrigger = true.");
//                col.isTrigger = true;
//            }
//        }
//    }

//    public void Initialize(ArmaduraDemonicaArea owner, float radius, Color color, float thickness, float flattenHeight, LayerMask capas)
//    {
//        this.owner = owner;
//        this.flattenHeightThreshold = flattenHeight;
//        this.capasAfectadas = capas;

//        if (col == null)
//        {
//            col = GetComponent<Collider>();
//            if (col == null) Debug.LogWarning($"[{name}] Initialize: Collider ausente. No se puede ajustar tamaño.");
//        }

//        if (col is SphereCollider sc) sc.radius = Mathf.Max(0.01f, radius);
//        else if (col is BoxCollider bc)
//        {
//            Vector3 size = bc.size;
//            float y = size.y;
//            bc.size = new Vector3(radius * 2f / transform.lossyScale.x, y, radius * 2f / transform.lossyScale.z);
//        }
//        else if (col is CapsuleCollider cc)
//        {
//            cc.radius = Mathf.Max(0.01f, radius / Mathf.Max(1f, transform.lossyScale.x));
//            cc.height = Mathf.Max(cc.height, (radius * 2f) / Mathf.Max(0.0001f, transform.lossyScale.y));
//        }
//    }

//    public void SetLayerMask(LayerMask m) { capasAfectadas = m; }
//    public void SetFlattenHeight(float h) { flattenHeightThreshold = h; }

//    public void SetRadius(float r)
//    {
//        if (col == null) { Debug.LogWarning($"[{name}] SetRadius llamado pero Collider ausente."); return; }

//        if (col is SphereCollider sc) sc.radius = Mathf.Max(0.01f, r);
//        else if (col is BoxCollider bc)
//        {
//            Vector3 size = bc.size;
//            bc.size = new Vector3(r * 2f / transform.lossyScale.x, size.y, r * 2f / transform.lossyScale.z);
//        }
//        else if (col is CapsuleCollider cc)
//        {
//            cc.radius = Mathf.Max(0.01f, r / Mathf.Max(0.0001f, transform.lossyScale.x));
//            cc.height = Mathf.Max(cc.height, (r * 2f) / Mathf.Max(0.0001f, transform.lossyScale.y));
//        }
//        else
//        {
//            Debug.LogWarning($"[{name}] SetRadius: tipo de Collider no soportado para ajuste automático.");
//        }
//    }

//    public void ActivateArea() { if (col != null) col.enabled = true; else Debug.LogWarning($"[{name}] ActivateArea: Collider ausente."); }
//    public void DeactivateArea() { if (col != null) col.enabled = false; else Debug.LogWarning($"[{name}] DeactivateArea: Collider ausente."); }

//    private void OnTriggerEnter(Collider other) => HandleTriggerEnterOrStay(other);
//    private void OnTriggerStay(Collider other) => HandleTriggerEnterOrStay(other);

//    private void HandleTriggerEnterOrStay(Collider other)
//    {
//        if (col == null) return;
//        if (((1 << other.gameObject.layer) & capasAfectadas) == 0) return;

//        var root = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
//        if (root == null) return;
//        if (owner != null && root == owner.gameObject) return;
//        if (Mathf.Abs(root.transform.position.y - transform.position.y) > flattenHeightThreshold) return;

//        float tNow = Time.time;
//        if (!lastApplyTime.TryGetValue(root, out float last) || tNow - last >= applyInterval)
//        {
//            // aplicar reducción usando el valor del prefab (prefab-driven)
//            ApplyReductionTo(root, reduccionPercent, duracion);
//            lastApplyTime[root] = tNow;

//            if (owner != null)
//            {
//                try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); }
//                catch { }
//            }
//        }
//    }

//    private void OnTriggerExit(Collider other)
//    {
//        var root = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
//        if (root != null)
//        {
//            if (lastApplyTime.ContainsKey(root)) lastApplyTime.Remove(root);

//            // Notificar al target que salió de ESTA área (para que quite reducción asociada a este prefab)
//            try
//            {
//                root.SendMessage("RemoveDamageReductionFromArea", this.gameObject, SendMessageOptions.DontRequireReceiver);
//                Debug.Log($"[ArmaduraAreaPrefab] RemoveDamageReductionFromArea enviado a {root.name} (salida).");
//            }
//            catch { }
//        }
//    }

//    public void ActivateFor(float duration, Vector3 center)
//    {
//        transform.position = center;

//        if (momentaryRoutine != null) { StopCoroutine(momentaryRoutine); momentaryRoutine = null; }
//        if (lifecycleRoutine != null) StopCoroutine(lifecycleRoutine);
//        lifecycleRoutine = StartCoroutine(LifecycleRoutine(duration, center));
//    }

//    private IEnumerator LifecycleRoutine(float duration, Vector3 center)
//    {
//        transform.position = center;
//        ActivateArea();

//        // aplicarlo al owner si existe
//        if (owner != null)
//        {
//            ApplyReductionTo(owner.gameObject, reduccionPercent, duration);
//            lastApplyTime[owner.gameObject] = Time.time;
//            try { owner.gameObject.SendMessage("OnEntityEnterArea", owner.gameObject, SendMessageOptions.DontRequireReceiver); } catch { }
//        }

//        Collider[] hits = GetOverlaps(center);
//        foreach (var c in hits)
//        {
//            GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
//            if (owner != null && root == owner.gameObject) continue;
//            if (Mathf.Abs(root.transform.position.y - (owner != null ? owner.transform.position.y : center.y)) > flattenHeightThreshold) continue;
//            ApplyReductionTo(root, reduccionPercent, duration);
//            lastApplyTime[root] = Time.time;

//            if (owner != null)
//            {
//                try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); } catch { }
//            }
//        }

//        yield return new WaitForSeconds(Mathf.Max(0.01f, duration));
//        DeactivateArea();

//        // Antes de notificar al owner, limpiar notificaciones en todos los targets aún registrados
//        var targets = new List<GameObject>(lastApplyTime.Keys);
//        foreach (var t in targets)
//        {
//            try
//            {
//                t.SendMessage("RemoveDamageReductionFromArea", this.gameObject, SendMessageOptions.DontRequireReceiver);
//            }
//            catch { }
//        }
//        lastApplyTime.Clear();

//        // Notificar al owner que esta instancia ya terminó su lifecycle (para que el owner pueda destruir/limpiar si corresponde)
//        NotifyOwnerAreaDeactivated();

//        lifecycleRoutine = null;
//    }

//    public void ActivateMomentaryAndApply(Vector3 center, float momentDuration)
//    {
//        transform.position = center;

//        if (lifecycleRoutine != null)
//        {
//            Collider[] hits = GetOverlaps(center);
//            foreach (var c in hits)
//            {
//                GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
//                if (owner != null && root == owner.gameObject) continue;
//                if (Mathf.Abs(root.transform.position.y - (owner != null ? owner.transform.position.y : center.y)) > flattenHeightThreshold) continue;
//                ApplyReductionTo(root, reduccionPercent, duracion);
//                lastApplyTime[root] = Time.time;

//                if (owner != null)
//                {
//                    try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); } catch { }
//                }
//            }
//            return;
//        }

//        if (momentaryRoutine != null) StopCoroutine(momentaryRoutine);
//        momentaryRoutine = StartCoroutine(MomentaryRoutine(center, Mathf.Max(0.01f, momentDuration)));
//    }

//    private IEnumerator MomentaryRoutine(Vector3 center, float momentDuration)
//    {
//        transform.position = center;
//        ActivateArea();

//        if (owner != null)
//        {
//            ApplyReductionTo(owner.gameObject, reduccionPercent, duracion);
//            lastApplyTime[owner.gameObject] = Time.time;
//            try { owner.gameObject.SendMessage("OnEntityEnterArea", owner.gameObject, SendMessageOptions.DontRequireReceiver); } catch { }
//        }

//        Collider[] hits = GetOverlaps(center);
//        foreach (var c in hits)
//        {
//            GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
//            if (owner != null && root == owner.gameObject) continue;
//            if (Mathf.Abs(root.transform.position.y - (owner != null ? owner.transform.position.y : center.y)) > flattenHeightThreshold) continue;
//            ApplyReductionTo(root, reduccionPercent, duracion);
//            lastApplyTime[root] = Time.time;

//            if (owner != null)
//            {
//                try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); } catch { }
//            }
//        }

//        float waitTime = Mathf.Max(momentDuration, this.duracion);
//        yield return new WaitForSeconds(waitTime);

//        DeactivateArea();

//        // limpiar y notificar a targets
//        var targets = new List<GameObject>(lastApplyTime.Keys);
//        foreach (var t in targets)
//        {
//            try { t.SendMessage("RemoveDamageReductionFromArea", this.gameObject, SendMessageOptions.DontRequireReceiver); } catch { }
//        }
//        lastApplyTime.Clear();

//        NotifyOwnerAreaDeactivated();

//        momentaryRoutine = null;
//    }

//    private Collider[] GetOverlaps(Vector3 worldCenter)
//    {
//        if (col == null)
//        {
//            return Physics.OverlapSphere(worldCenter, 0.01f, capasAfectadas, QueryTriggerInteraction.Ignore);
//        }

//        if (col is SphereCollider sc)
//        {
//            Vector3 center = sc.transform.TransformPoint(sc.center);
//            float radiusWorld = sc.radius * MaxScale(sc.transform);
//            return Physics.OverlapSphere(center, Mathf.Max(0.001f, radiusWorld), capasAfectadas, QueryTriggerInteraction.Ignore);
//        }
//        else if (col is BoxCollider bc)
//        {
//            Vector3 center = bc.transform.TransformPoint(bc.center);
//            Vector3 halfExtents = Vector3.Scale(bc.size * 0.5f, bc.transform.lossyScale);
//            Quaternion orientation = bc.transform.rotation;
//            return Physics.OverlapBox(center, Vector3.Max(halfExtents, Vector3.one * 0.001f), orientation, capasAfectadas, QueryTriggerInteraction.Ignore);
//        }
//        else if (col is CapsuleCollider cc)
//        {
//            Transform t = cc.transform;
//            Vector3 centerLocal = cc.center;
//            int dir = cc.direction;
//            Vector3 axis = (dir == 0) ? Vector3.right : (dir == 1) ? Vector3.up : Vector3.forward;
//            float radiusWorld = cc.radius * MaxScaleOnAxes(t, dir == 1 ? new int[] { 0, 2 } : new int[] { 0, 1, 2 });
//            float halfSeg = Mathf.Max(0f, (cc.height * 0.5f) - cc.radius);
//            Vector3 worldA = t.TransformPoint(centerLocal + axis * halfSeg);
//            Vector3 worldB = t.TransformPoint(centerLocal - axis * halfSeg);
//            return Physics.OverlapCapsule(worldA, worldB, Mathf.Max(0.001f, radiusWorld), capasAfectadas, QueryTriggerInteraction.Ignore);
//        }
//        else
//        {
//            Bounds b = col.bounds;
//            return Physics.OverlapBox(b.center, b.extents, Quaternion.identity, capasAfectadas, QueryTriggerInteraction.Ignore);
//        }
//    }

//    private float MaxScale(Transform t) => Mathf.Max(t.lossyScale.x, t.lossyScale.y, t.lossyScale.z);

//    private float MaxScaleOnAxes(Transform t, int[] axes)
//    {
//        float max = 0f;
//        foreach (var a in axes)
//        {
//            if (a == 0) max = Mathf.Max(max, t.lossyScale.x);
//            else if (a == 1) max = Mathf.Max(max, t.lossyScale.y);
//            else if (a == 2) max = Mathf.Max(max, t.lossyScale.z);
//        }
//        return Mathf.Max(0.0001f, max);
//    }

//    private void ApplyReductionTo(GameObject target, float percent, float dur)
//    {
//        if (target == null) return;

//        float effectivePercent = Mathf.Clamp01(reduccionPercent);
//        float effectiveDur = Mathf.Max(0f, duracion > 0f ? duracion : dur);

//        bool invoked = false;

//        // 1) intentar método específico ApplyDamageReductionFromArea en componentes
//        foreach (var mb in target.GetComponents<MonoBehaviour>())
//        {
//            if (mb == null) continue;
//            var miNew = mb.GetType().GetMethod("ApplyDamageReductionFromArea", BindingFlags.Public | BindingFlags.Instance);
//            if (miNew != null)
//            {
//                try { miNew.Invoke(mb, new object[] { effectivePercent, effectiveDur, this.gameObject }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducción FROM AREA ({effectivePercent}) a {target.name} via {mb.GetType().Name}.ApplyDamageReductionFromArea()"); break; }
//                catch { }
//            }
//        }

//        // 2) compatibilidad: si no existía método nuevo, intentar ApplyDamageReduction tradicional
//        if (!invoked)
//        {
//            foreach (var mb in target.GetComponents<MonoBehaviour>())
//            {
//                if (mb == null) continue;
//                if (mb is IDamageable)
//                {
//                    var mi = mb.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
//                    if (mi != null)
//                    {
//                        try { mi.Invoke(mb, new object[] { effectivePercent, effectiveDur }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducción ({effectivePercent}) a {target.name} via {mb.GetType().Name}.ApplyDamageReduction()"); break; }
//                        catch { }
//                    }
//                }
//            }
//        }

//        // 3) intentos por nombre (legacy)
//        if (!invoked)
//        {
//            var vidaEscudo = target.GetComponent("VidaEnemigoEscudo");
//            if (vidaEscudo != null)
//            {
//                var miNew = vidaEscudo.GetType().GetMethod("ApplyDamageReductionFromArea", BindingFlags.Public | BindingFlags.Instance);
//                if (miNew != null) { try { miNew.Invoke(vidaEscudo, new object[] { effectivePercent, effectiveDur, this.gameObject }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducción FROM AREA a {target.name} via VidaEnemigoEscudo.ApplyDamageReductionFromArea()"); } catch { } }
//                else
//                {
//                    var mi = vidaEscudo.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
//                    if (mi != null) { try { mi.Invoke(vidaEscudo, new object[] { effectivePercent, effectiveDur }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducción (legacy) a {target.name} via VidaEnemigoEscudo.ApplyDamageReduction()"); } catch { } }
//                }
//            }
//        }

//        if (!invoked)
//        {
//            var enemyH = target.GetComponent("EnemyHealth");
//            if (enemyH != null)
//            {
//                var miNew = enemyH.GetType().GetMethod("ApplyDamageReductionFromArea", BindingFlags.Public | BindingFlags.Instance);
//                if (miNew != null) { try { miNew.Invoke(enemyH, new object[] { effectivePercent, effectiveDur, this.gameObject }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducción FROM AREA a {target.name} via EnemyHealth.ApplyDamageReductionFromArea()"); } catch { } }
//                else
//                {
//                    var mi = enemyH.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
//                    if (mi != null) { try { mi.Invoke(enemyH, new object[] { effectivePercent, effectiveDur }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducción (legacy) a {target.name} via EnemyHealth.ApplyDamageReduction()"); } catch { } }
//                }
//            }
//        }

//        // 4) último recurso: intentar SendMessage al nuevo método (si no fue invocado)
//        if (!invoked)
//        {
//            try
//            {
//                target.SendMessage("ApplyDamageReductionFromArea", new object[] { effectivePercent, effectiveDur, this.gameObject }, SendMessageOptions.DontRequireReceiver);
//                Debug.Log($"[ArmaduraAreaPrefab] SendMessage ApplyDamageReductionFromArea ({effectivePercent}) enviado a {target.name} como fallback.");
//                invoked = true;
//            }
//            catch { }
//        }

//        // 5) si aún no se invocó nada, también intentar el SendMessage legacy para compatibilidad
//        if (!invoked)
//        {
//            try
//            {
//                target.SendMessage("ApplyDamageReduction", new object[] { effectivePercent, effectiveDur }, SendMessageOptions.DontRequireReceiver);
//                Debug.Log($"[ArmaduraAreaPrefab] SendMessage ApplyDamageReduction ({effectivePercent}) enviado a {target.name} como fallback final.");
//            }
//            catch { }
//        }
//    }

//    private void NotifyOwnerAreaDeactivated()
//    {
//        if (owner != null)
//        {
//            try { owner.gameObject.SendMessage("OnAreaDeactivated", this.gameObject, SendMessageOptions.DontRequireReceiver); }
//            catch { }
//        }
//    }

//    /// <summary>
//    /// Comprueba si la entidad pasada está actualmente dentro del área de este prefab.
//    /// Método público utilizado por versiones previas (no obligado a usarse si se aplica el nuevo protocolo por SendMessage).
//    /// </summary>
//    public bool IsEntityInside(GameObject entity)
//    {
//        if (entity == null || col == null) return false;
//        GameObject root = entity.transform.root != null ? entity.transform.root.gameObject : entity;
//        if (root == null) return false;
//        if (owner != null && root == owner.gameObject) return false;
//        if (Mathf.Abs(root.transform.position.y - transform.position.y) > flattenHeightThreshold) return false;

//        try { return col.bounds.Contains(root.transform.position); }
//        catch { return false; }
//    }
//}

