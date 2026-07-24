using UnityEngine;

public class GlassShardDamage : MonoBehaviour
{
    [Tooltip("Cantidad de daño que el fragmento de vidrio inflige por segundo.")]
    [HideInInspector] public float damagePerSecond = 4f;
    [Tooltip("Frecuencia con la que se aplica el daño (en segundos).")]
    [HideInInspector] public float damageTickRate = 1f;
    [Tooltip("Capa del jugador para detectar colisiones.")]
    [HideInInspector] public LayerMask playerLayer;
    [Tooltip("Duración antes de que el fragmento de vidrio se destruya.")]
    [HideInInspector] public float shardDeathDuration = 4f;
    [Tooltip("Indica si el fragmento de vidrio debe causar un efecto de muerte al jugador.")]
    [SerializeField] private bool effectDeath = false;

    private float nextDamageTime;

    private void Start()
    {
        Destroy(gameObject, shardDeathDuration);
    }

    private void OnTriggerStay(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

        if (Time.time >= nextDamageTime)
        {
            if (other.TryGetComponent<PlayerHealth>(out var pHealth))
            {
                pHealth.TakeDamage(damagePerSecond);

                if (effectDeath)
                {
                    Vector3 dir = (other.transform.position - transform.position).normalized;
                    dir.y = 0;
                    pHealth.ApplyKnockback(dir, 4, 1f);

                    Shatter();
                }

                nextDamageTime = Time.time + damageTickRate;
            }
        }
    }

    public void Shatter()
    {
        Destroy(gameObject);
    }
}