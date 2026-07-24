using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class MeatPillar : MonoBehaviour
{
    [Header("Configuración de Resistencia")]
    [SerializeField] private int maxHits = 5;
    private int currentHits;

    [Header("Configuración de piezas de carne")]
    [Tooltip("Prefab del trozo pequeño de carne que sale al golpear.")]
    [SerializeField] private List<GameObject> meatPiecePrefabs;
    [Tooltip("Prefab de la explosión de trozos de carne tras ser destruido.")]
    [SerializeField] private GameObject finalExplosionPrefab;

    [SerializeField] private int minPiecesPerHit = 1;
    [SerializeField] private int maxPiecesPerHit = 4;

    [SerializeField] private float pieceLifetime = 5f;

    [Header("Configuración de Spawn")]
    [SerializeField] private float finalExplosionHeight = 5f;
    [SerializeField] private float debrisSpawnHeight = 9.5f;
    [SerializeField] private float innerSpawnRadius = 1.0f;
    [SerializeField] private float impactRadius = 5f;

    [Header("Configuración de física")]
    [Tooltip("Fuerza hacia abajo adicional para que caigan rápido (sensación de peso).")]
    [SerializeField] private float initialDownwardForce = 10f;
    [Tooltip("Fuerza lateral para dispersarlos.")]
    [SerializeField] private float lateralForce = 5f;

    [Header("Configuración de Daño de Área (AOE)")]
    [SerializeField] private bool canDamaged = false;
    [Tooltip("Radio alrededor del pilar donde caen las piezas y se daña a los enemigos.")]
    [SerializeField] private float damageToEnemies = 10f;
    [Tooltip("Layers de los enemigos que recibirán daño por los escombros.")]
    [SerializeField] private LayerMask enemyLayers;

    [Header("Configuración de SFX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitMeleeClip;
    [SerializeField] private AudioClip hitRangedClip;
    [SerializeField] private AudioClip debrisSpawnClip;
    [SerializeField] private float pitchRandomness = 0.1f;

    [Header("Configuración Adicional")]
    [SerializeField] private bool debugMode = false;

    private bool isDestroyed = false;

    private void Awake()
    {
        currentHits = 0;

        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

        if (innerSpawnRadius >= impactRadius)
        {
            impactRadius = innerSpawnRadius + 0.5f;
            Debug.LogWarning($"[MeatPillar] El radio interno era mayor o igual al externo. Se ajustó impactRadius a {impactRadius}.");
        }
    }

    public void TakeDamage(AttackDamageType damageType = AttackDamageType.Melee)
    {
        PlayHitSound(damageType);
        HitPillar();
    }

    private void PlayHitSound(AttackDamageType damageType)
    {
        if (audioSource == null) return;

        AudioClip clipToPlay = (damageType == AttackDamageType.Ranged) ? hitRangedClip : hitMeleeClip;

        if (clipToPlay != null)
        {
            audioSource.pitch = 1f + Random.Range(-pitchRandomness, pitchRandomness);
            audioSource.PlayOneShot(clipToPlay);
        }
    }

    private void HitPillar()
    {
        if (isDestroyed) return;

        currentHits++;

        // Generar trozos de carne
        SpawnDebris();

        // Dañar enemigos cercanos
        if (canDamaged) DamageNearbyEnemies();

        // Comprobar destrucción
        if (currentHits >= maxHits)
        {
            BreakPillar();
        }
        else
        {
            transform.DOPunchScale(Vector3.one * 0.15f, 0.15f, 10, 1);
        }
    }

    private void SpawnDebris()
    {
        if (audioSource != null && debrisSpawnClip != null)
        {
            audioSource.PlayOneShot(debrisSpawnClip);
        }

        if (meatPiecePrefabs == null || meatPiecePrefabs.Count == 0) return;

        int piecesToSpawn = Random.Range(minPiecesPerHit, maxPiecesPerHit + 1); // +1 porque el rango es exclusivo en el máximo

        for (int i = 0; i < piecesToSpawn; i++)
        {
            // Calcular posición de spawn dentro del radio definido
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            if (randomDir == Vector2.zero) randomDir = Vector2.right; // Protección contra vector cero

            // Distancia aleatoria entre el radio interno y el externo
            float randomDist = Random.Range(innerSpawnRadius, impactRadius);

            Vector3 spawnPos = transform.position + new Vector3(randomDir.x * randomDist, debrisSpawnHeight, randomDir.y * randomDist);

            // seleccionar prefab aleatorio
            GameObject selectedPrefab = meatPiecePrefabs[Random.Range(0, meatPiecePrefabs.Count)];

            GameObject debris = Instantiate(selectedPrefab, spawnPos, Random.rotation);

            if (debris.TryGetComponent(out HeavyDebris heavyDebris))
            {
                heavyDebris.Initialized(pieceLifetime);
            }

            // Fisica de impacto
            if (debris.TryGetComponent(out Rigidbody rb))
            {
                // Reiniciar velocidades
                rb.linearVelocity = Vector3.zero;

                // Empuje lateral
                Vector3 pushDir = (debris.transform.position - transform.position).normalized;
                pushDir.y = 0; // Solo empuje horizontal

                // Aplicar fuerza hacia abajo y lateral
                Vector3 finalForce = (Vector3.down * initialDownwardForce) + (pushDir * lateralForce);

                rb.AddForce(finalForce, ForceMode.Impulse);

                // Añadir torque aleatorio para rotación
                rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }

            // Limpieza de seguridad
            Destroy(debris, pieceLifetime);
        }
    }

    private void DamageNearbyEnemies()
    {
        // Detectar colliders en el radio definido
        Collider[] hits = Physics.OverlapSphere(transform.position, impactRadius, enemyLayers);

        foreach (Collider hit in hits)
        {
            // Evitar dañarse a sí mismo si el pilar estuviera en esa layer
            if (hit.gameObject == this.gameObject) continue;

            // Buscar componente de vida en el enemigo
            if (hit.TryGetComponent(out EnemyHealth enemyHealth))
            {
                // Aplicar daño.
                enemyHealth.TakeDamage(damageToEnemies, AttackDamageType.Nothing, transform.position);
                Debug.Log($"Pilar dañó a {hit.name} por la caída de escombros.");
            }
        }
    }

    private void BreakPillar()
    {
        if (audioSource != null && debrisSpawnClip != null)
        {
            audioSource.PlayOneShot(debrisSpawnClip);
        }

        isDestroyed = true;

        if (finalExplosionPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * finalExplosionHeight;
            Instantiate(finalExplosionPrefab, spawnPos, Quaternion.identity);
        }

        // Destruir el pilar
        Destroy(gameObject);
    }

    // Dibujar el radio en el editor para ver dónde caerán las cosas
    private void OnDrawGizmos()
    {
        if (!debugMode) return;

        // Radio Externo
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawWireSphere(transform.position, impactRadius);

        // Radio Interno
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, innerSpawnRadius);

        // Altura de spawn de piezas de carne
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position + Vector3.up * debrisSpawnHeight, transform.position + Vector3.up * debrisSpawnHeight + Vector3.right);

        // Altura de spawm de explosion final
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * finalExplosionHeight);
    }
}