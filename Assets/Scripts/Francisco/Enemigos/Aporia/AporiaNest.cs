using UnityEngine;
using System.Collections;

public class AporiaNest : MonoBehaviour
{
    #region Settings

    [SerializeField] private GameObject larvaPrefab;
    [Tooltip("Duracion en segundos del tiempo de vida del area de efecto.")]
    [SerializeField] private float duration = 3f;
    [Tooltip("Daño por segundo que el nido inflige al jugador mientras esté dentro del area de efecto.")]
    [SerializeField] private float dps = 1f;
    [Tooltip("Intervalo en segundos para aplicar el daño al jugador mientras esté dentro del area de efecto.")]
    [SerializeField] private float damageTickRate = 1f;
    [Tooltip("Duracion en segundos del efecto de ralentizacion aplicado al jugador mientras esté dentro del area de efecto.")]
    [SerializeField] private float slowDuration = 1f;
    [Tooltip("Fraccion de ralentización aplicada al jugador mientras esté dentro del area de efecto.")]
    [SerializeField] private float slowFraction = 0.2f;
    [Tooltip("Número de larvas que se generarán al final del ciclo de vida del nido.")]
    [SerializeField] private float currentLarvaSpawnRate = 2;

    #endregion

    // Variable para rastrear cuándo se debe aplicar el siguiente tick de daño
    private float nextDamageTime;

    #region Unity Events

    private void OnEnable()
    {
        if (larvaPrefab == null)
        {
            Debug.LogWarning($"[AporiaNest] '{name}': larvaPrefab no está asignado. El nido no generará larvas al eclosionar.");
        }

        StartCoroutine(LifeCycle());
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Solo se ejecuta si el tiempo actual ha superado el tiempo del próximo tick
            if (Time.time >= nextDamageTime)
            {
                if (other.TryGetComponent<PlayerHealth>(out var health))
                {
                    health.TakeDamage(dps); // Se aplica el daño completo
                }

                // Calcula el tiempo para el siguiente tick de daño
                nextDamageTime = Time.time + damageTickRate;
            }

            PlayerStatsManager statsManager = other.GetComponent<PlayerStatsManager>();
            if (statsManager != null)
            {
                string slowKey = "NestSlow_" + GetInstanceID();
                statsManager.ApplyTimedModifier(slowKey, StatType.MoveSpeed, -slowFraction, slowDuration, isPercentage: true);
            }
        }
    }

    #endregion

    #region Logic

    public void SetRateSpawn(float rate) => currentLarvaSpawnRate = rate;

    private IEnumerator LifeCycle()
    {
        yield return new WaitForSeconds(duration);

        for (int i = 0; i < currentLarvaSpawnRate; i++)
        {
            if (larvaPrefab != null)
            {
                Vector3 spawnPos = transform.position + (Random.insideUnitSphere * 0.5f);
                spawnPos.y = transform.position.y;
                Instantiate(larvaPrefab, spawnPos, Quaternion.identity);
            }
        }

        gameObject.SetActive(false);
    }

    #endregion
}