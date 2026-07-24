using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine;

/// <summary>
/// Nodo Purulento – Torre de servidor de carne instanciada durante la fase
/// "Arquitectura Distribuida" (hito 50 % del jefe Baal).
/// </summary>
public class PurulentNode : MonoBehaviour
{
    #region Inspector

    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private EnemyVisualEffects enemyVisualEffects;

    [Header("Emergence Setup")]
    [Tooltip("Profundidad desde la que emerge el nodo.")]
    [SerializeField] private float emergenceDepth = 3f;
    [Tooltip("Tiempo que tarda en salir a la superficie.")]
    [SerializeField] private float emergenceDuration = 1.5f;

    [Header("NavMesh")]
    [Tooltip("El obstáculo para que el jefe y las larvas lo rodeen.")]
    [SerializeField] private NavMeshObstacle navObstacle;
    [Tooltip("Radio seguro para instanciar larvas fuera del obstáculo.")]
    [SerializeField] private float safeSpawnRadius = 2f;

    [Header("Projectile Config")]
    [Tooltip("Prefab BaalProjectile que usa el nodo como código rojo.")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("Velocidad del proyectil.")]
    [SerializeField] private float projectileSpeed = 12f;
    [Tooltip("Daño mínimo del proyectil.")]
    [SerializeField] private float projectileDmgMin = 2f;
    [Tooltip("Daño máximo del proyectil.")]
    [SerializeField] private float projectileDmgMax = 4f;
    [SerializeField] private float projScaleStart = 4f;
    [SerializeField] private float projScaleMax = 16f;
    [Tooltip("Intervalo entre disparos.")]
    [SerializeField] private float fireInterval = 2.5f;

    [Header("Larva Config")]
    [Tooltip("Prefab de larva kamikaze.")]
    [SerializeField] private GameObject larvaPrefab;
    [Tooltip("Intervalo entre spawns de larva.")]
    [SerializeField] private float larvaInterval = 5f;

    #region Inspector - Telegraphed Settings

    [Header("Anticipacion de Ataque")]
    [SerializeField] protected float anticipationPauseDuration = 0.6f;
    [SerializeField] protected float anticipationSFXPitch = 1.0f;
    [SerializeField] protected AudioClip anticipationSFX;

    #endregion

    [Header("Visual")]
    [SerializeField] private ParticleSystem fireVFX;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSFX;
    [SerializeField] private AudioClip larvaSpawnSFX;

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = false;

    #endregion

    #region Internal State

    private bool isAlive = true;
    private bool isEmerged = false;
    protected bool isInAnticipation = false;
    private Transform player;
    protected Coroutine anticipationCoroutine = null;
    private List<Larva> activeLarvas = new List<Larva>(); // Lista de larvas generadas

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyVisualEffects == null) enemyVisualEffects = GetComponent<EnemyVisualEffects>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        // Localiza al jugador
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        // Se suscribe a la muerte para detener las rutinas limpiamente
        if (enemyHealth != null) enemyHealth.OnDeath += OnNodeDeath;

        // Desactiva el obstáculo de NavMesh al inicio para que el jefe no lo considere hasta que el nodo haya emergido
        if (navObstacle != null) navObstacle.enabled = false;

        StartCoroutine(EmergenceRoutine());
    }

    private IEnumerator EmergenceRoutine()
    {
        Vector3 finalPosition = transform.position;
        Vector3 startPosition = finalPosition + Vector3.down * emergenceDepth;

        transform.position = startPosition;

        float elapsed = 0f;
        while (elapsed < emergenceDuration)
        {
            transform.position = Vector3.Lerp(startPosition, finalPosition, elapsed / emergenceDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = finalPosition;
        isEmerged = true;

        // Activa el obstáculo ahora que está en su posición final
        if (navObstacle != null) navObstacle.enabled = true;

        // Inicia el combate
        StartCoroutine(FireRoutine());
        StartCoroutine(LarvaRoutine());
    }

    private void OnDisable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= OnNodeDeath;
    }

    #endregion

    #region Combat Routines

    /// <summary>
    /// Dispara un proyectil de código rojo hacia el jugador cada <paramref name="fireInterval"/> segundos.
    /// </summary>
    private IEnumerator FireRoutine()
    {
        yield return isEmerged ? null : new WaitUntil(() => isEmerged); // Espera a que el nodo haya emergido antes de empezar a disparar
        // Pequeño offset inicial para que los nodos no disparen todos a la vez
        yield return new WaitForSeconds(Random.Range(0.2f, 1f));

        while (isAlive)
        {
            yield return new WaitForSeconds(fireInterval);

            if (!isAlive) yield break;
            if (player == null) continue;

            StartAnticipationPause();

            yield return new WaitUntil(() => !isInAnticipation);

            FireProjectile();
        }
    }

    /// <summary>
    /// Genera una larva kamikaze cada <paramref name="larvaInterval"/> segundos.
    /// </summary>
    private IEnumerator LarvaRoutine()
    {
        yield return isEmerged ? null : new WaitUntil(() => isEmerged); // Espera a que el nodo haya emergido antes de empezar a disparar

        yield return new WaitForSeconds(Random.Range(0.5f, 2f));

        while (isAlive)
        {
            yield return new WaitForSeconds(larvaInterval);

            if (!isAlive) yield break;

            SpawnLarva();
        }
    }

    #endregion

    #region Actions

    private void FireProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[PurulentNode] Falta projectilePrefab. Asignarlo en el Inspector.");
            return;
        }

        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0f;
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;

        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(dir));

        BaalProjectile ps = proj.GetComponent<BaalProjectile>();
        if (ps != null)
        {
            ps.Initialize(dir, projectileSpeed,
                          projectileDmgMin, projectileDmgMax,
                          projScaleStart, projScaleMax,
                          transform.position);
        }
        else
        {
            // Fallback
            Rigidbody rb = proj.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = dir * projectileSpeed;
            Destroy(proj, 3f);
        }

        if (audioSource != null && fireSFX != null) audioSource.PlayOneShot(fireSFX);

        if (fireVFX != null) fireVFX.Play();

        if (showDebugGUI) Debug.Log($"[PurulentNode] {name} disparó proyectil.");
    }

    private void SpawnLarva()
    {
        if (larvaPrefab == null) return;

        // Lógica circular para asegurar que aparezca fuera del NavMeshObstacle
        Vector2 randomDir = Random.insideUnitCircle.normalized * safeSpawnRadius;
        Vector3 candidatePos = transform.position + new Vector3(randomDir.x, 0.5f, randomDir.y);

        // Validación en el NavMesh para evitar que caiga al vacío
        if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, safeSpawnRadius * 2, NavMesh.AllAreas))
        {
            candidatePos = hit.position + Vector3.up * 0.2f;
        }

        GameObject larvaGO = Instantiate(larvaPrefab, candidatePos, Quaternion.identity);

        if (larvaGO.TryGetComponent(out Larva larvaScript))
        {
            if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;

            larvaScript.Initialize(player);
            activeLarvas.Add(larvaScript);
        }
        else
        {
            Debug.LogWarning("[PurulentNode] El prefab de larva no tiene el script Larva. Asignar el prefab correcto.");
            Destroy(larvaGO);
            return;
        }

        if (audioSource != null && larvaSpawnSFX != null)
        {
            audioSource.PlayOneShot(larvaSpawnSFX);
        }

        if (showDebugGUI) Debug.Log($"[PurulentNode] {name} spawneó larva.");
    }

    private void OnNodeDeath(GameObject node)
    {
        isAlive = false;

        // Mata a las larvas activas para evitar que sigan persiguiendo al jugador sin un nodo activo
        foreach (Larva larva in activeLarvas)
        {
            if (larva != null && larva.gameObject != null)
            {
                larva.Die();
            }
        }

        activeLarvas.Clear();
        CancelAnticipation();
        StopAllCoroutines();
    }

    #endregion

    #region Hit Stun & Anticipation

    public void StartAnticipationPause()
    {
        if (!isAlive) return;
        if (anticipationCoroutine != null) StopCoroutine(anticipationCoroutine);
        anticipationCoroutine = StartCoroutine(AnticipationRoutine());
    }

    protected virtual IEnumerator AnticipationRoutine()
    {
        isInAnticipation = true;

        if (audioSource != null && anticipationSFX != null)
        {
            audioSource.pitch = anticipationSFXPitch;
            audioSource.PlayOneShot(anticipationSFX);
            audioSource.pitch = 1f;
        }

        // Blink rojo de anticipacion
        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.PlayAnticipationBlink(anticipationPauseDuration);
        }

        yield return new WaitForSeconds(anticipationPauseDuration);

        isInAnticipation = false;
        anticipationCoroutine = null;
    }

    protected void CancelAnticipation()
    {
        if (anticipationCoroutine != null)
        {
            StopCoroutine(anticipationCoroutine);
            anticipationCoroutine = null;
        }

        if (enemyVisualEffects != null) enemyVisualEffects.CancelAnticipationBlink();
        isInAnticipation = false;
    }

    #endregion

    #region Debug

    private void OnDrawGizmos()
    {
        if (!showDebugGUI) return;

        Gizmos.color = new Color(1f, 0.3f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, projScaleMax);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }

    #endregion
}