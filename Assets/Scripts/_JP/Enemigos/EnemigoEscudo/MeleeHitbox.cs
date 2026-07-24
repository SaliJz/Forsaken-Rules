// MeleeHitbox.cs
using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Hitbox que aplica daño (OnTrigger). 
/// Si followTarget no es null, copia su posición (y opcionalmente rotación) cada frame.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MeleeHitbox : MonoBehaviour
{
    [Tooltip("Dueño del hitbox (no le hará daño al owner)")]
    public GameObject owner;

    [Tooltip("Daño aplicado")]
    public float damage = 10f;

    [Tooltip("Tiempo en segundos antes de auto-destruir el hitbox")]
    public float lifetime = 0.45f;

    [Tooltip("Si se asigna, el hitbox copiará la posición de este Transform cada frame")]
    public Transform followTarget;

    [Tooltip("LayerMask para filtrar qué objetos puede golpear")]
    public LayerMask hitLayers = ~0;

    [Tooltip("Destruir al primer impacto con un objetivo válido")]
    public bool destroyOnHit = true;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        if (lifetime > 0f) Destroy(gameObject, lifetime);
    }

    public void SetOwner(GameObject go) => owner = go;

    private void Update()
    {
        if (followTarget != null)
        {
            // copiar posición; si quieres también rotación, descomenta la línea siguiente
            transform.position = followTarget.position;
            // transform.rotation = followTarget.rotation;
        }
    }

    private bool IsLayerAllowed(GameObject go)
    {
        int goLayer = go.layer;
        return (hitLayers & (1 << goLayer)) != 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        var root = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;

        if (root == owner) return;
        if (!IsLayerAllowed(root)) return;

        try
        {
            if (root.CompareTag("Enemy")) return;
        }
        catch (UnityException) { }

        bool applied = TryApplyDamage(root, damage);

        if (applied && destroyOnHit)
        {
            Destroy(gameObject);
        }
    }

    private bool TryApplyDamage(GameObject target, float amount)
    {
        if (target == null) return false;

        var ph = target.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            try
            {
                ph.TakeDamage(amount);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"MeleeHitbox: fallo PlayerHealth.TakeDamage: {e.Message}");
            }
        }

        Component[] comps = target.GetComponents<Component>();
        foreach (var comp in comps)
        {
            if (comp == null) continue;
            var t = comp.GetType();
            string[] methodNames = { "TakeDamage", "ApplyDamage", "ReceiveDamage", "Damage", "RecibirDanio" };
            foreach (var name in methodNames)
            {
                var mi = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    var ps = mi.GetParameters();
                    if (ps.Length == 1 && (ps[0].ParameterType == typeof(float) || ps[0].ParameterType == typeof(int)))
                    {
                        try
                        {
                            mi.Invoke(comp, new object[] { Convert.ChangeType(amount, ps[0].ParameterType) });
                            return true;
                        }
                        catch { }
                    }
                }
            }
        }

        try
        {
            target.SendMessage("TakeDamage", amount, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("ApplyDamage", amount, SendMessageOptions.DontRequireReceiver);
            return true;
        }
        catch { }

        return false;
    }
}
