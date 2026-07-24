using UnityEngine;

/// <summary>
/// Proyectil de "Excepción Fatal" del jefe Baal.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BaalProjectile : MonoBehaviour
{
    #region Private Fields

    private Vector3 direction;
    private float speed;
    private float damageMin;
    private float damageMax;
    private float scaleStartDist;
    private float scaleMaxDist;
    private Vector3 originPos;

    #endregion

    #region Inspector - Audio

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip impactClip;
    [SerializeField] private AudioClip explosionClip;

    #endregion

    #region Inspector - Explosión

    [Header("Impact AoE")]
    [Tooltip("Prefab instanciado al impactar (explosión de palabras).")]
    [SerializeField] private GameObject impactVFXPrefab;
    [Tooltip("Radio del área de efecto puntual al chocar.")]
    [SerializeField] private float impactAoERadius = 0.8f;
    [Tooltip("Segundos de vida máxima antes de auto-destruirse.")]
    [SerializeField] private float lifetime = 4f;

    #endregion

    #region Internal State

    private Rigidbody rb;
    private bool initialized = false;
    private bool hasImpacted = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (!initialized) return;
        rb.linearVelocity = direction * speed;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasImpacted) return;

        if (other.CompareTag("Enemy")) return;

        hasImpacted = true;

        if (other.CompareTag("Player"))
        {
            float damage = CalculateDamage();
            PlaySound(impactClip);
            DealDamageToPlayer(other.gameObject, damage);
        }

        PlaySound(explosionClip);
        ApplyImpactAoE();

        if(other.gameObject.TryGetComponent(out PlayerHealth health))
        {
            if(health.CurrentHealth > 0) SpawnImpactVFX();
        }
        else
        {
            SpawnImpactVFX();
        }
        
        Destroy(gameObject);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Inicializa el proyectil con los parámetros del boss.
    /// </summary>
    public void Initialize(
        Vector3 direction,
        float speed,
        float damageMin,
        float damageMax,
        float scaleStartDist,
        float scaleMaxDist,
        Vector3 originPosition)
    {
        this.direction = direction.normalized;
        this.speed = speed;
        this.damageMin = damageMin;
        this.damageMax = damageMax;
        this.scaleStartDist = scaleStartDist;
        this.scaleMaxDist = scaleMaxDist;
        originPos = originPosition;
        initialized = true;

        if (this.direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(this.direction);
        }
    }

    #endregion

    #region Private Helpers

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    /// <summary>
    /// Calcula el daño interpolado según la distancia recorrida.
    /// </summary>
    private float CalculateDamage()
    {
        float distance = Vector3.Distance(originPos, transform.position);

        if (distance <= scaleStartDist) return damageMin;

        if (distance >= scaleMaxDist) return damageMax;

        float t = Mathf.InverseLerp(scaleStartDist, scaleMaxDist, distance);
        return Mathf.Lerp(damageMin, damageMax, t);
    }

    private void DealDamageToPlayer(GameObject target, float damage)
    {
        if (target.TryGetComponent(out PlayerHealth health))
        {
            if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) 
                && blockSystem.IsBlocking 
                && blockSystem.CanBlockAttack(transform.position))
            {
                float remaining = blockSystem.ProcessBlockedAttack(damage);
                if (remaining > 0f)
                {
                    health.TakeDamage(remaining, false, AttackDamageType.Ranged);
                }
                return;
            }
            health.TakeDamage(damage, false, AttackDamageType.Ranged);
        }
        else if (target.TryGetComponent(out PlayerHealth healthOnly))
        {
            healthOnly.TakeDamage(damage, false, AttackDamageType.Ranged);
        }
    }

    /// <summary>
    /// Área de efecto puntual al chocar: vuelve a comprobar si el jugador
    /// está dentro del radio de explosión (cubre proyectiles que pasan cerca).
    /// </summary>
    private void ApplyImpactAoE()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, impactAoERadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                float damage = CalculateDamage();
                DealDamageToPlayer(hit.gameObject, damage);
                break;
            }
        }
    }

    private void SpawnImpactVFX()
    {
        if (impactVFXPrefab == null) return;
        GameObject vfx = Instantiate(impactVFXPrefab, transform.position, Quaternion.identity);
        Destroy(vfx, 2f);
    }

    #endregion
}