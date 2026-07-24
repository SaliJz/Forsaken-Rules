using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(EnemyHealth))]
public class VeynarEnemy : MonoBehaviour
{
    #region Inspector - References

    [Header("Component References")]
    [SerializeField] private VeynarAnimCtrl animCtrl;

    [Header("Spawning References")]
    [SerializeField] private GameObject hivePrefab;
    [SerializeField] private GameObject larvaPrefab;

    [Header("Sound References")]
    [SerializeField] private AudioSource audioSource;

    #endregion

    #region Inspector - Spawning Settings

    [Header("Spawning Settings")]
    [Tooltip("Maximo de colmenas activas simultaneamente.")]
    [SerializeField] private int maxActiveHives = 3;
    [Tooltip("Tiempo entre intentos de invocacion de colmena.")]
    [SerializeField] private float hiveSpawnInterval = 5f;
    [Tooltip("Distancia minima para invocar colmenas.")]
    [SerializeField] private float minHiveSpawnRadius = 5f;
    [Tooltip("Distancia maxima para invocar colmenas.")]
    [SerializeField] private float maxHiveSpawnRadius = 15f;

    #endregion

    #region Inspector - Behavior Settings

    [Header("Behavior Settings")]
    [Tooltip("Tiempo de espera al activarse antes de iniciar cualquier comportamiento.")]
    [SerializeField] private float spawnDelay = 1f;
    [Tooltip("Rango del teletransporte al re-ocultarse.")]
    [SerializeField] private float teleportRange = 20f;
    [Tooltip("Las colmenas se teletransportan con Veynar?")]
    [SerializeField] private bool teleportHivesWithVeynar = false;
    [Tooltip("Nombre de la capa a la que Veynar cambia al ser invulnerable (ej. 'Default' o 'Ignore Raycast').")]
    [SerializeField] private string invulnerableLayerName = "Default";

    #endregion

    #region Inspector - SFX Settings

    [Header("SFX Estados")]
    [SerializeField] private AudioClip idleSFX;
    [SerializeField] private AudioClip hiddenStateSFX;
    [SerializeField] private AudioClip visibleStateSFX;

    [Header("SFX Acciones")]
    [SerializeField] private AudioClip spawnHiveSFX;
    [SerializeField] private AudioClip teleportSFX;
    [SerializeField] private AudioClip deathSFX;

    #endregion

    #region Internal State

    private EnemyHealth enemyHealth;
    private Transform playerTransform;
    private NavMeshAgent navAgent;

    private List<Hive> activeHives = new List<Hive>();
    private int previousHiveCount = 0;
    private bool hasLostFirstHive = false;

    private Vector3 initialPosition;

    private float idleTimer;
    private float idleInterval;

    #endregion

    #region Public Properties

    public bool IsDead => enemyHealth != null && enemyHealth.IsDead;
    public Transform PlayerTransform => playerTransform;
    public int ActiveHiveCount => activeHives.Count;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        navAgent = GetComponent<NavMeshAgent>();
        animCtrl = GetComponentInChildren<VeynarAnimCtrl>();

        if (animCtrl == null)
        {
            Debug.LogWarning($"[VeynarEnemy] VeynarAnimCtrl no encontrado en los hijos de {gameObject.name}.");
        }
        // Asignar la capa de invulnerabilidad a EnemyHealth
        if (enemyHealth != null)
        {
            enemyHealth.invulnerableLayerIndex = LayerMask.NameToLayer(invulnerableLayerName);
            if (enemyHealth.invulnerableLayerIndex == -1)
            {
                Debug.LogWarning($"[VeynarEnemy] La capa '{invulnerableLayerName}' no existe. Veynar podria ser 'targeteable' por el escudo.");
                enemyHealth.invulnerableLayerIndex = gameObject.layer;
            }
        }
    }

    private void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        initialPosition = transform.position;

        if (hivePrefab == null || larvaPrefab == null)
        {
            Debug.LogError($"[VeynarEnemy] Prefabs no asignados en {gameObject.name}. Veynar no funcionara correctamente.", this);
            enabled = false;
            return;
        }

        StartCoroutine(InitWithDelay());
    }

    private IEnumerator InitWithDelay()
    {
        yield return new WaitForSeconds(spawnDelay);

        StartCoroutine(ApplyInitialInvulnerabilityState());
        StartCoroutine(HiveSpawnRoutine());

        ResetIdleTimer();
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleEnemyDeath;
            enemyHealth.OnDamaged += HandleEnemyDamaged;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleEnemyDamaged;
        }
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleEnemyDamaged;
        }
    }

    private void Update()
    {
        if (IsDead) return;

        idleTimer += Time.deltaTime;
        if (idleTimer >= idleInterval)
        {
            PlaySFX(idleSFX);
            ResetIdleTimer();
        }
    }

    #endregion

    #region Spawning Logic

    private IEnumerator HiveSpawnRoutine()
    {
        yield return new WaitForSeconds(1f);

        while (!IsDead)
        {
            while (enemyHealth != null && enemyHealth.IsStunned)
            {
                if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
                {
                    navAgent.isStopped = true;
                    navAgent.ResetPath();
                }
                yield return null;
            }

            if (activeHives.Count < maxActiveHives)
            {
                SpawnHive();
                yield return new WaitForSeconds(hiveSpawnInterval);
            }
            else
            {
                yield return new WaitForSeconds(1.0f);
            }
        }
    }

    private void SpawnHive()
    {
        animCtrl?.PlaySpawning();

        Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minHiveSpawnRadius, maxHiveSpawnRadius);
        Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        spawnPosition.y = -0.4f;

        if (NavMesh.SamplePosition(spawnPosition, out var hit, 5f, NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
        }

        var go = Instantiate(hivePrefab, spawnPosition, Quaternion.identity);
        var hive = go.GetComponent<Hive>();
        if (hive != null)
        {
            hive.Initialize(this, larvaPrefab);
            activeHives.Add(hive);
        }

        PlaySFX(spawnHiveSFX);
        UpdateVulnerabilityState();
    }

    internal void OnHiveDestroyed(Hive hive)
    {
        if (activeHives.Contains(hive))
        {
            activeHives.Remove(hive);
        }

        if (!hasLostFirstHive)
        {
            hasLostFirstHive = true;
        }

        PlaySFX(visibleStateSFX);
        UpdateVulnerabilityState();
    }

    #endregion

    #region Vulnerability & Visuals Logic

    /// <summary>
    /// Aplica el estado inicial: invulnerabilidad 100% y camuflaje completo (oculto).
    /// </summary>
    private IEnumerator ApplyInitialInvulnerabilityState()
    {
        // Un frame de delay para que EnemyHealth guarde su capa vulnerable.
        yield return null;

        if (enemyHealth != null)
        {
            enemyHealth.SetDynamicVulnerability(1.0f);
        }

        // Camuflaje = 1: completamente oculto.
        animCtrl?.UpdateCamou(1.0f);

        if (animCtrl != null) animCtrl.isInvulnerable = true;

        Debug.Log("[VeynarEnemy] Estado inicial aplicado: Invulnerable (100%) y oculto (camou = 1).");
    }

    /// <summary>
    /// Actualiza la vulnerabilidad y el camuflaje de Veynar según las colmenas activas.
    /// damageReduction = camouflage: 1.0 => 0.67 => 0.33 => 0.0
    /// </summary>
    private void UpdateVulnerabilityState()
    {
        if (!hasLostFirstHive) return;

        activeHives.RemoveAll(hive => hive == null);

        int currentHives = activeHives.Count;
        float statePercent = (maxActiveHives - currentHives) / (float)maxActiveHives;
        float damageReduction = 1.0f - statePercent; // 1 = max reducción/camuflaje
        float visibility = statePercent; // 1 = totalmente visible

        if (enemyHealth != null)
        {
            enemyHealth.SetDynamicVulnerability(damageReduction);
        }

        animCtrl?.UpdateCamou(damageReduction);

        if (animCtrl != null) animCtrl.isInvulnerable = (damageReduction > 0f);

        // Teletransporte al volver a tener el máximo de colmenas activas.
        if (currentHives == maxActiveHives && previousHiveCount < maxActiveHives)
        {
            PlaySFX(hiddenStateSFX);
            StartCoroutine(TeleportSequence(initialPosition, teleportRange));
        }

        previousHiveCount = currentHives;
    }

    #endregion

    #region Movement & Teleport Logic

    private IEnumerator TeleportSequence(Vector3 center, float range)
    {
        if (animCtrl != null)
        {
            animCtrl.PlayMoveOut();
            yield return StartCoroutine(animCtrl.WaitForMoveOut());
        }

        PerformTeleport(center, range);
        PlaySFX(teleportSFX);

        animCtrl?.PlayMoveIn();
    }

    private void PerformTeleport(Vector3 center, float range)
    {
        Vector3 candidate = center + Random.insideUnitSphere * range;
        candidate.y = transform.position.y;

        Vector3 oldPosition = transform.position;
        Vector3 newPosition = oldPosition;

        if (NavMesh.SamplePosition(candidate, out var hit, range, NavMesh.AllAreas))
        {
            newPosition = hit.position;
            if (navAgent != null && navAgent.isOnNavMesh)
                navAgent.Warp(newPosition);
            else
                transform.position = newPosition;
        }
        else
        {
            newPosition = candidate;
            transform.position = newPosition;
        }

        if (teleportHivesWithVeynar)
        {
            Vector3 offset = newPosition - oldPosition;
            foreach (Hive hive in activeHives)
            {
                if (hive != null) hive.Teleport(hive.transform.position + offset);
            }
        }
    }

    #endregion

    #region Core Health & Combat Logic

    private void ResetIdleTimer()
    {
        idleTimer = 0f;
        idleInterval = Random.Range(5f, 10f);
    }

    private void HandleEnemyDamaged()
    {
        if (animCtrl != null && animCtrl.isInvulnerable)
        {
            animCtrl.PlayInvulnerabilityVFX();
        }

        animCtrl?.PlayDamage();
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        StopAllCoroutines();
        PlaySFX(deathSFX);

        animCtrl?.PlayDeath();

        foreach (var hive in new List<Hive>(activeHives))
        {
            if (hive != null) Destroy(hive.gameObject);
        }
        activeHives.Clear();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxHiveSpawnRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, minHiveSpawnRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, teleportRange);
    }

    #endregion
}