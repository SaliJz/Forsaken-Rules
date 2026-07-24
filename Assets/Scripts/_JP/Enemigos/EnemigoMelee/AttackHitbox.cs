
// AttackHitbox.cs
using System;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AttackHitbox : MonoBehaviour
{
    [Tooltip("Daño aplicado al entrar (puede ser float; se convertirá según la firma del receptor).")]
    public float damage = 1f;

    [Tooltip("Tiempo de vida del hitbox en segundos.")]
    public float life = 0.5f;

    [Tooltip("Si true, solo aplica daño cuando activeDamage == true (usa SetActiveDamage).")]
    public bool requireActiveDamage = false;

    [Tooltip("Si true, destruye el hitbox después de aplicar daño una vez.")]
    public bool destroyOnHit = true;

    bool activeDamage = false;

    void Start()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }

    public void SetActiveDamage(bool on)
    {
        activeDamage = on;
    }

    void OnTriggerEnter(Collider other)
    {
        if (requireActiveDamage && !activeDamage) return;
        if (other == null || other.gameObject == null) return;

        float floatDamage = damage;
        int intDamage = Mathf.RoundToInt(damage);
        bool invoked = false;

        // 1) Si tiene PlayerHealth, llamamos TakeDamage(float) directamente (tu script).
        var ph = other.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(floatDamage);
            invoked = true;
        }
        else
        {
            // 2) Buscamos en TODOS los componentes del objeto un método TakeDamage y lo invocamos
            var comps = other.GetComponents<Component>();
            foreach (var comp in comps)
            {
                if (comp == null) continue;
                var mi = comp.GetType().GetMethod("TakeDamage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi == null) continue;

                var pars = mi.GetParameters();
                try
                {
                    if (pars.Length == 0)
                    {
                        // método sin parámetros
                        mi.Invoke(comp, null);
                        invoked = true;
                        break;
                    }
                    else
                    {
                        var pType = pars[0].ParameterType;
                        if (pType == typeof(float))
                        {
                            mi.Invoke(comp, new object[] { floatDamage });
                            invoked = true;
                            break;
                        }
                        else if (pType == typeof(int))
                        {
                            mi.Invoke(comp, new object[] { intDamage });
                            invoked = true;
                            break;
                        }
                        else
                        {
                            // intenta convertir de forma segura
                            var conv = Convert.ChangeType(floatDamage, pType);
                            mi.Invoke(comp, new object[] { conv });
                            invoked = true;
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"AttackHitbox: fallo invocando TakeDamage en {comp.GetType().Name}: {e.Message}");
                }
            }
        }

        // 3) Si no encontramos ninguna sobrecarga, usamos SendMessage como fallback (float primero).
        if (!invoked)
        {
            // Intento con float; si el receptor sólo tiene int, no hará nada, así que también probamos int.
            other.SendMessage("TakeDamage", floatDamage, SendMessageOptions.DontRequireReceiver);
            other.SendMessage("TakeDamage", intDamage, SendMessageOptions.DontRequireReceiver);
        }

        if (invoked || !requireActiveDamage || activeDamage)
        {
            if (destroyOnHit) Destroy(gameObject);
        }
    }
}

//// AttackHitbox.cs
//using UnityEngine;

///// <summary>
///// Hitbox simple que aplica daño cuando está activa.
///// </summary>
//[RequireComponent(typeof(Collider))]
//public class AttackHitbox : MonoBehaviour
//{
//    public int damage = 1;
//    public float life = 0.5f; // tiempo máximo antes de autodestruirse
//    bool activeDamage = false;

//    void Start()
//    {
//        var col = GetComponent<Collider>();
//        if (col != null) col.isTrigger = true;
//    }

//    void Update()
//    {
//        life -= Time.deltaTime;
//        if (life <= 0f) Destroy(gameObject);
//    }

//    public void SetActiveDamage(bool on)
//    {
//        activeDamage = on;
//    }

//    void OnTriggerEnter(Collider other)
//    {
//        if (!activeDamage) return;
//        if (other == null || other.gameObject == null) return;

//        var dmg = other.GetComponent<IDamageable>();
//        if (dmg != null)
//        {
//            dmg.TakeDamage(damage);
//            return;
//        }

//        // fallback por nombre común
//        var ph = other.GetComponent<PlayerHealth>();
//        if (ph != null)
//        {
//            ph.TakeDamage(damage);
//            return;
//        }

//        if (other.CompareTag("Player"))
//        {
//            other.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
//        }
//    }
//}
