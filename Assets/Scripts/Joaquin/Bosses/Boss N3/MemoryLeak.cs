using System.Collections;
using UnityEngine;

/// <summary>
/// Fuga de Memoria – charco de daño continuo dejado por los teletransportes
/// de Desfragmentación Evasiva y Latencia Cero del jefe Baal.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class MemoryLeak : MonoBehaviour
{
    #region Private Fields

    private float duration;
    private float dps;
    private float radius;

    #endregion

    #region Inspector - Effects & Synergy

    [Header("Visual")]
    [SerializeField] private ParticleSystem poolVFX;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip loopSFX;

    [Header("Larva Synergy")]
    [Tooltip("Porcentaje de expansión del radio al recibir una explosión de larva.")]
    [SerializeField] private float synergyExpansionPercent = 0.15f;

    #endregion

    #region Internal State

    private SphereCollider col;
    private bool playerIsInside = false;
    private bool hasExpired = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        col = GetComponent<SphereCollider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsInside = true;
            StartCoroutine(DamagePlayerRoutine(other.gameObject));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerIsInside = false;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Inicializa el charco con los parámetros del boss.
    /// </summary>
    public void Initialize(float duration, float dps, float radius)
    {
        this.duration = duration;
        this.dps = dps;
        this.radius = radius;

        float initialScaleXZ = radius;
        transform.localScale = new Vector3(initialScaleXZ, transform.localScale.y, initialScaleXZ);

        if (poolVFX != null) poolVFX.Play();
        if (audioSource != null && loopSFX != null)
        {
            audioSource.clip = loopSFX;
            audioSource.loop = true;
            audioSource.Play();
        }

        StartCoroutine(LifetimeRoutine());
    }

    /// <summary>
    /// Llamado por la larva kamikaze al explotar dentro del charco.
    /// Expande el radio.
    /// </summary>
    public void ExpandFromLarvaExplosion()
    {
        if (hasExpired) return;

        radius *= (1f + synergyExpansionPercent);

        float expandedScaleXZ = radius;
        transform.localScale = new Vector3(expandedScaleXZ, transform.localScale.y, expandedScaleXZ);

        Debug.Log($"[MemoryLeak] Sinergia de larva: radio expandido a {radius:F2} uds.");
    }

    #endregion

    #region Private Routines

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(duration);

        hasExpired = true;

        if (audioSource != null) audioSource.Stop();
        if (poolVFX != null) poolVFX.Stop();

        Destroy(gameObject);
    }

    private IEnumerator DamagePlayerRoutine(GameObject playerObj)
    {
        PlayerHealth health = playerObj.GetComponent<PlayerHealth>();
        if (health == null) yield break;

        // Cachea la espera para evitar alocaciones de memoria innecesarias en cada iteración
        WaitForSeconds oneSecondTick = new WaitForSeconds(1f);

        while (playerIsInside && !hasExpired)
        {
            // Aplica el daño total directamente
            health.TakeDamage(dps, false, AttackDamageType.Melee);

            // Espera un segundo exacto antes del siguiente tick de daño
            yield return oneSecondTick;
        }
    }

    #endregion
}