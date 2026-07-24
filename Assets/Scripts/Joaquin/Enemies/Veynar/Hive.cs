using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyHealth))]
public class Hive : MonoBehaviour
{
    [Header("Hive settings")]
    [SerializeField] private float larvaSpawnInterval = 2.5f;
    [SerializeField] private int maxLarvas = 3;
    [SerializeField] private float hiveLifetimeAfterProduction = 5f;
    [SerializeField] private GameObject larvaPrefab;
    [SerializeField] private NavMeshObstacle obstacle;
    [SerializeField] private NavMeshAgent navMeshAgent;

    [Header("Larva Spawn Settings")]
    [SerializeField] private float minLarvaSpawnRadius = 3f;
    [SerializeField] private float maxLarvaSpawnRadius = 5f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;

    [Header("SFX")]
    [SerializeField] private AudioClip spawnLarvaSFX;
    [SerializeField] private AudioClip deathSFX;

    [Header("VFX")]
    [SerializeField] private ParticleSystem spawnVFX;

    private int spawnedCount = 0;
    private bool isProducing = true;
    private VeynarEnemy owner;
    private EnemyHealth enemyHealth;
    private List<Larva> activeLarvas = new List<Larva>(); // Lista de larvas generadas
    
    private Coroutine delayDeathCoroutine;

    public VeynarEnemy Owner => owner;
    public int SpawnedCount => spawnedCount;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        obstacle = GetComponent<NavMeshObstacle>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (obstacle != null) obstacle.enabled = false;
        if (navMeshAgent != null) navMeshAgent.enabled = true;
    }

    public void Initialize(VeynarEnemy ownerRef, GameObject larvaPrefabRef)
    {
        owner = ownerRef;
        larvaPrefab = larvaPrefabRef;
        spawnedCount = 0;
        isProducing = true;

        StopAllCoroutines();
        StartCoroutine(ProduceRoutine());
        StartCoroutine(ActivateObstacleAfterDelay(1f));
    }

    private void OnEnable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath += HandleEnemyDeath;
    }

    private void OnDisable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
        if (obstacle != null) obstacle.enabled = false;
    }

    private void OnDestroy()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        if (audioSource != null && deathSFX != null)
        {
            audioSource.PlayOneShot(deathSFX);
        }

        obstacle.enabled = false;
        StopAllCoroutines();
        delayDeathCoroutine = null;

        // Matar a todas las larvas generadas por esta colmena
        foreach (Larva larva in activeLarvas)
        {
            if (larva != null && larva.gameObject != null)
            {
                larva.Die();
            }
        }

        activeLarvas.Clear();

        owner?.OnHiveDestroyed(this);
    }

    private IEnumerator ProduceRoutine()
    {
        while (isProducing && spawnedCount < maxLarvas)
        {
            yield return new WaitForSeconds(larvaSpawnInterval);

            if (owner == null || owner.IsDead)
            {
                StopProducing();
                yield break;
            }

            SpawnLarva();
        }

        // Si ha terminado de producir, se autodestruye
        if (spawnedCount >= maxLarvas)
        {
            StopProducing();
        }
    }

    private IEnumerator ActivateObstacleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (navMeshAgent != null) navMeshAgent.enabled = false;
        if (obstacle != null) obstacle.enabled = true;
    }

    private void SpawnLarva()
    {
        spawnedCount++;

        if (larvaPrefab == null)
        {
            Debug.LogWarning("[Hive] larvaPrefab no asignado.");
            return;
        }

        // Lógica de "dona" para larvas
        Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minLarvaSpawnRadius, maxLarvaSpawnRadius);
        Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0.5f, randomCircle.y);

        if (NavMesh.SamplePosition(spawnPosition, out var hit, 5f, NavMesh.AllAreas))
        {
            spawnPosition = hit.position + Vector3.up * 0.2f;
        }
        else
        {
            // Fallback si la dona falla
            spawnPosition = transform.position + (Random.insideUnitSphere * maxLarvaSpawnRadius);
            spawnPosition.y = 0.5f;
            if (NavMesh.SamplePosition(spawnPosition, out hit, 5f, NavMesh.AllAreas))
            {
                spawnPosition = hit.position + Vector3.up * 0.2f;
            }
            else
            {
                Debug.LogWarning("[Hive] No se encontró posición en NavMesh para larva.");
            }
        }

        var larvaGameObject = Instantiate(larvaPrefab, spawnPosition, Quaternion.identity);

        if (larvaGameObject != null && larvaGameObject.activeInHierarchy)
        {
            var larva = larvaGameObject.GetComponent<Larva>();
            if (larva != null)
            {
                larva.Initialize(owner?.PlayerTransform);
                activeLarvas.Add(larva); // Añadir a la lista
            }
            else Debug.LogWarning("[Hive] No se encontró componente Larva en el prefab.");
        }
        else
        {
            Debug.LogWarning("[Hive] La larva no se pudo activar correctamente. Destruyéndola.");
            if (larvaGameObject != null) Destroy(larvaGameObject);
            spawnedCount--;
            return;
        }

        if (spawnVFX != null) spawnVFX.Play();

        if (audioSource != null && spawnLarvaSFX != null)
        {
            audioSource.PlayOneShot(spawnLarvaSFX);
        }
    }

    public void StopProducing()
    {
        isProducing = false;
        StopAllCoroutines();
        if (enemyHealth != null && !enemyHealth.IsDead)
        {
            StartDelayAfterDeath(hiveLifetimeAfterProduction);
        }
    }

    private void StartDelayAfterDeath(float delay)
    {
        if (delayDeathCoroutine != null)
        {
            StopCoroutine(delayDeathCoroutine);
        }
        delayDeathCoroutine = StartCoroutine(DelayAfterDeath(delay));
    }

    private IEnumerator DelayAfterDeath(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (enemyHealth != null && !enemyHealth.IsDead)
        {
            enemyHealth.Die(true, AttackDamageType.Nothing);
        }
    }

    /// <summary>
    /// Mueve la colmena a una nueva posición.
    /// </summary>
    public void Teleport(Vector3 newPosition)
    {
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            // Intentar encontrar un punto válido en el NavMesh
            if (NavMesh.SamplePosition(newPosition, out var hit, 5f, NavMesh.AllAreas))
            {
                navMeshAgent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning($"[Hive] No se pudo teletransportar a {newPosition} (fuera de NavMesh).");
            }
        }
        else
        {
            // Fallback si no hay NavMeshAgent
            if (NavMesh.SamplePosition(newPosition, out var hit, 5f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
            else
            {
                transform.position = newPosition; // Mover de todos modos
            }
        }
    }

    private void OnDrawGizmos()
    {
        // Dibujar las zonas de spawn de larvas
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, minLarvaSpawnRadius);
        Gizmos.DrawWireSphere(transform.position, maxLarvaSpawnRadius);
    }
}