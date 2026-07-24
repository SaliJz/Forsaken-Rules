using UnityEngine;

/// <summary>
/// Clase base abstracta para todas las minas de trampa del juego.
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class BaseTrapMine : MonoBehaviour
{
    #region Inspector - Estadísticas compartidas

    [Header("Daño")]
    [SerializeField] protected float damage      = 15f;
    [SerializeField] protected float slowFraction = 0.10f;
    [SerializeField] protected float slowDuration = 1f;

    [Header("Detección")]
    [SerializeField] protected float explosionRadius = 1.8f;
    [SerializeField] protected LayerMask playerLayer;

    [Header("Tiempo de vida")]
    [SerializeField] protected float duration = 5f;

    [Header("VFX")]
    [SerializeField] protected ParticleSystem explosionVFXPrefab;
    [SerializeField] protected GameObject explosionSpherePrefab;

    [Header("Audio")]
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] protected AudioClip explosionSound;

    #endregion

    #region Propiedades públicas

    public float Damage        
    { 
        get => damage;        
        set => damage = value; 
    }
    public float SlowFraction  
    { 
        get => slowFraction;  
        set => slowFraction = value; 
    }
    public float SlowDuration  
    { 
        get => slowDuration;  
        set => slowDuration = value; 
    }
    public float Duration      
    { 
        get => duration;      
        set => duration = value; 
    }

    #endregion

    #region Estado interno

    protected bool hasExploded = false;

    #endregion

    #region Unity Lifecycle

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;
        if ((playerLayer.value & (1 << other.gameObject.layer)) != 0)
            Explode();
    }

    #endregion

    #region Lógica de explosión

    /// <summary>
    /// Activa la explosión: aplica daño + ralentización en área y genera VFX.
    /// </summary>
    protected virtual void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        CancelInvoke();

        if (audioSource != null && explosionSound != null) audioSource.PlayOneShot(explosionSound);

        SpawnExplosionVFX();
        ApplyExplosionEffects();

        Destroy(gameObject);
    }

    private void ApplyExplosionEffects()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, playerLayer);
        foreach (var hit in hits)
        {
            // Daño
            hit.GetComponent<PlayerHealth>()?.TakeDamage(damage);

            // Ralentización vía PlayerStatsManager
            PlayerStatsManager statsManager = hit.GetComponent<PlayerStatsManager>();
            if (statsManager != null)
            {
                string slowKey = "MineSlow_" + GetInstanceID();
                statsManager.ApplyTimedModifier(slowKey, StatType.MoveSpeed, -slowFraction, slowDuration, isPercentage: true);
            }
        }
    }

    #endregion

    #region VFX

    protected void SpawnExplosionVFX()
    {
        if (explosionVFXPrefab != null)
        {
            ParticleSystem vfxInstance = Instantiate(explosionVFXPrefab, 
                transform.position, Quaternion.identity);

            vfxInstance.transform.SetParent(null);
            vfxInstance.Play();
        }

        if (explosionSpherePrefab != null)
        {
            Instantiate(explosionSpherePrefab, transform.position, Quaternion.identity);
        }
    }

    #endregion

    #region Debug

    protected virtual void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }

    #endregion
}