using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class LaceratusController : MonoBehaviour, IAnimEventHandler
{
    #region Inspector - References

    [Header("Core References")]
    [SerializeField] private Renderer enemyRenderer;
    [SerializeField] private GameObject normalAttackHitbox;
    [SerializeField] private GameObject furyAttackHitbox;
    [SerializeField] private GameObject screamHitbox;
    [SerializeField] private JitterAnimCtrl animCtrl;
    [SerializeField] private GameObject furyAttackVFXPrefab;
    [SerializeField] private Transform furyAttackVFXSpawnPoint;

    #endregion

    #region Inspector - Detection

    [Header("Detection")]
    [SerializeField] private float visionRange = 7f;
    [SerializeField] private float visionConeAngle = 60f;
    [SerializeField] private LayerMask playerLayer;

    #endregion

    #region Inspector - Movement And Patrol

    [Header("Movement - Normal State")]
    [SerializeField] private float normalSpeed = 3f;
    [SerializeField] private float patrolDirectionChangeInterval = 4f;
    [SerializeField] private float patrolDirectionChangeAngle = 30f;

    [Header("Patrol - Stuck Check")]
    [SerializeField] private float stuckCheckInterval = 2f;
    [SerializeField] private float stuckDistanceThreshold = 0.1f;

    #endregion

    #region Inspector - Combat

    [Header("Combat - Normal Attack")]
    [SerializeField] private float normalAttackDamage = 5f;
    [SerializeField] private float normalAttackInterval = 1.5f;
    [SerializeField] private float normalAttackRange = 1f;
    [SerializeField] private float normalAttackSlowDuration = 1f;
    [SerializeField] private float normalAttackSlowPercent = 0.3f;

    [Header("Combat - Fury Attack")]
    [SerializeField] private float furyAttackDamage = 10f;
    [SerializeField] private float furyAttackInterval = 0.75f;
    [SerializeField] private float furyAttackRange = 2f;

    #endregion

    #region Inspector - Fury System

    [Header("Movement - Fury State")]
    [SerializeField] private float furySpeedMultiplier = 2f;
    [SerializeField] private float furyLevel2SpeedBonus = 0.3f;
    [SerializeField] private float furyJumpDistance = 2f;
    [SerializeField] private float furyJumpReducedDistance = 1f;
    [SerializeField] private float furyJumpDelay = 2f;
    [SerializeField] private float furyJumpCancelDistance = 0.5f;
    [SerializeField] private float furyJumpDuration = 0.5f;

    [Header("Fury Mechanics")]
    [SerializeField] private float furyDecayTime = 3f;
    [SerializeField] private float furyDecayNoDamageTime = 5f;
    [SerializeField] private float furyRegenerationPerSecond = 1f;
    [SerializeField] private float consecutiveHitPushDistance = 1f;
    [SerializeField] private float screamTransitionDuration = 1.2f;
    [SerializeField] private int maxFuryLevel = 2;

    #endregion

    #region Inspector - Telegraphed Settings

    [Header("Hit Stun")]
    [SerializeField] protected float hitStunDuration = 0.3f;
    [SerializeField] protected float forceIdleDuration = 0.8f;

    [Header("Anticipacion de Ataque")]
    [SerializeField] private float calmAnticipationDuration = 0.5f;
    [SerializeField] private float furyAnticipationDuration = 0.3f;

    #endregion

    #region Inspector - Visual Feedback

    [Header("Visual Feedback")]
    [SerializeField] private Material normalStateMaterial;
    [SerializeField] private Material furyStateMaterial;
    [SerializeField] private float hitboxDisplayDuration = 0.2f;

    #endregion

    #region Inspector - Audio

    [Header("Core Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("SFX Estados y Movimiento")]
    [SerializeField] private AudioClip presenceSFX;
    [SerializeField] private AudioClip normalMoveSFX;
    [SerializeField] private AudioClip furyMoveSFX;
    [SerializeField] private AudioClip furyTransitionSFX;
    [SerializeField] private AudioClip calmTransitionSFX;

    [Header("SFX Combate")]
    [SerializeField] private AudioClip normalAttackClip;
    [SerializeField] private AudioClip furyAttackClip;
    [SerializeField] private AudioClip furyJumpSFX;
    [SerializeField] private AudioClip pushbackSFX;
    [SerializeField] private AudioClip deathSFX;

    [Header("SFX Dano y Anticipacion")]
    [SerializeField] protected AudioClip hitStunSFX;
    [SerializeField] protected AudioClip toughnessBlockSFX;
    [SerializeField] private AudioClip calmAnticipationSFX;
    [SerializeField] private AudioClip furyAnticipationSFX;

    #endregion

    #region Inspector - Debugging

    [Header("QuickSheet Balance")]
    [SerializeField] private Enemies enemiesSheet;
    [SerializeField] private int ENEMY_ID = 13;

    #endregion

    #region Internal State

    private NavMeshAgent agent;
    private EnemyHealth enemyHealth;
    private EnemyVisualEffects enemyVisualEffects;
    private EnemyToughness enemyToughness;
    private EnemyKnockbackHandler knockbackHandler;
    private Transform playerTransform;

    private bool isInAnticipation = false;
    private bool pendingPushbackScreamAfterHitStun = false;
    private bool pendingEnterFuryAfterHitStun = false;
    private bool isInFury = false;
    private bool playerDetected = false;
    private bool isTransitioningToFury = false;
    private bool isPerformingJump = false;
    private bool isAttacking = false;
    private bool isInHitStun = false;

    private int consecutiveHitsReceived = 0;
    private int furyLevel = 0;
    private float lastAttackTime;
    private float lastDamageTime;
    private float lastDamageInflictedTime;
    private float patrolTimer;
    private float furyJumpTimer;
    private float timeSinceOutOfRange;
    private float stuckCheckTimer;

    private float hitStunRecoveryCooldown = 0f;
    private float hitStunRecoveryGrace = 0.1f;

    private Vector3 lastPatrolPosition;
    private Vector3 currentPatrolDirection;
    private Vector3 frozenAttackDirection;

    private Coroutine furyDecayCoroutine;
    private Coroutine furyRegenerationCoroutine;
    private Coroutine hitStunCoroutine;
    private Coroutine furyJumpCoroutine;
    private Coroutine anticipationCoroutine = null;
    private Material originalMaterial;

    private float audioStepTimer;
    private float idleAudioTimer;
    private float idleAudioInterval;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animCtrl == null) animCtrl = GetComponentInChildren<JitterAnimCtrl>();
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyToughness == null) enemyToughness = GetComponent<EnemyToughness>();
        if (knockbackHandler == null) knockbackHandler = GetComponent<EnemyKnockbackHandler>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();
        if (enemyVisualEffects == null) enemyVisualEffects = GetComponent<EnemyVisualEffects>();

        //LoadStatsFromSheet();

        if (enemyRenderer != null)
        {
            originalMaterial = enemyRenderer.material;
        }

        if (normalAttackHitbox != null) normalAttackHitbox.SetActive(false);
        if (furyAttackHitbox != null) furyAttackHitbox.SetActive(false);
        if (screamHitbox != null) screamHitbox.SetActive(false);

        if (knockbackHandler != null)
        {
            // knockbackHandler.knockbackResistance = knockbackResistance;
        }
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        patrolTimer = Random.Range(0f, patrolDirectionChangeInterval);

        float randomInitialAngle = Random.Range(0f, 360f);
        currentPatrolDirection = Quaternion.Euler(0, randomInitialAngle, 0) * Vector3.forward;
        currentPatrolDirection.Normalize();

        lastPatrolPosition = transform.position;
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleEnemyDeath;
            enemyHealth.OnDamaged += HandleDamageTaken;
            enemyHealth.OnToughnessHit += HandleToughnessHit;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
            enemyHealth.OnToughnessHit -= HandleToughnessHit;
        }
        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
            enemyHealth.OnToughnessHit -= HandleToughnessHit;
        }
        StopAllCoroutines();
    }

    private void Update()
    {
        if (enemyHealth != null && enemyHealth.IsDead) return;
        if (enemyHealth != null && enemyHealth.IsStunned)
        {
            CancelAttack();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            if (animCtrl != null) animCtrl.isWalking = false;
            return;
        }

        if (hitStunRecoveryCooldown > 0f) hitStunRecoveryCooldown -= Time.deltaTime;

        if (isTransitioningToFury || isPerformingJump || isAttacking || isInHitStun || isInAnticipation)
        {
            if (animCtrl != null && !isTransitioningToFury) animCtrl.isWalking = false;
            return;
        }

        HandleAudioLoop();
        UpdateAnimationState();

        if (!playerDetected)
        {
            CheckPlayerDetection();
            if (!playerDetected)
            {
                Patrol();
            }
        }

        if (playerDetected && playerTransform != null)
        {
            if (isInFury)
            {
                HandleFuryBehavior();
            }
            else
            {
                HandleNormalBehavior();
            }
        }

        if (isInFury)
        {
            CheckFuryDecay();
        }
    }

    #endregion

    #region Initialization And Data Sync

    private void LoadStatsFromSheet()
    {
        if (enemiesSheet == null) return;

        foreach (var row in enemiesSheet.dataArray)
        {
            if (row.ID != ENEMY_ID) continue;

            if (enemyHealth != null)
            {
                enemyHealth.SetMaxHealth(row.Health);
            }

            EnemyToughness toughnessComp = GetComponent<EnemyToughness>();
            if (toughnessComp != null)
            {
                if (row.Superarmor > 0)
                {
                    toughnessComp.SetUseToughness(true);
                    toughnessComp.SetMaxToughness(row.Superarmor);
                }
                else toughnessComp.SetUseToughness(false);
            }

            normalSpeed = row.Movespeed;
            if (agent != null) agent.speed = normalSpeed;

            normalAttackDamage = row.Regulardamage;

            furyAttackDamage = normalAttackDamage * 2;

            Debug.Log($"[Laceratus] ID {ENEMY_ID} cargado. Speed: {normalSpeed}, Melee Dmg: {normalAttackDamage}");
            return;
        }
    }

    #endregion

    #region Detection And Patrol Logic

    private void CheckPlayerDetection()
    {
        if (playerTransform == null) return;

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer > visionRange) return;

        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

        if (angleToPlayer <= visionConeAngle)
        {
            playerDetected = true;
            Debug.Log("Jitter: Jugador detectado");
        }
    }

    private void Patrol()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        patrolTimer += Time.deltaTime;
        stuckCheckTimer += Time.deltaTime;

        if (stuckCheckTimer >= stuckCheckInterval)
        {
            float distanceMoved = Vector3.Distance(transform.position, lastPatrolPosition);

            if (distanceMoved < stuckDistanceThreshold)
            {
                Debug.Log("Jitter: Atasco detectado! Forzando nuevo rumbo aleatorio.");

                ChangePatrolDirection();
                agent.ResetPath();
            }

            lastPatrolPosition = transform.position;
            stuckCheckTimer = 0f;
        }

        if (patrolTimer >= patrolDirectionChangeInterval)
        {
            patrolTimer = 0f;
            ChangePatrolDirection();
        }

        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            SetNewPatrolDestination();
        }
    }

    private void SetNewPatrolDestination()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        int attempts = 0;
        int maxAttempts = 10;
        float searchRadius = 6f;

        while (attempts < maxAttempts)
        {
            Vector3 randomPoint = Random.insideUnitSphere * searchRadius;
            randomPoint += transform.position;
            randomPoint.y = transform.position.y;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
            {
                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    float straightDistance = Vector3.Distance(transform.position, hit.position);
                    float pathLength = CalculatePathLength(path);

                    if (pathLength < straightDistance * 2.5f)
                    {
                        agent.SetDestination(hit.position);

                        currentPatrolDirection = (hit.position - transform.position).normalized;

                        Debug.Log($"Jitter: Nuevo destino aleatorio fijado a {straightDistance:F1}m (camino: {pathLength:F1}m).");
                        return;
                    }
                }
            }

            attempts++;
        }

        ChangePatrolDirection();
        agent.ResetPath();
        Debug.Log("Jitter: Error al encontrar destino NavMesh valido, forzando nuevo rumbo.");
    }

    private float CalculatePathLength(NavMeshPath path)
    {
        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++)
        {
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }
        return length;
    }

    private void ChangePatrolDirection()
    {
        float decision = Random.value;

        if (decision < 0.4f)
        {
            float randomAngle = Random.Range(-patrolDirectionChangeAngle, patrolDirectionChangeAngle);
            currentPatrolDirection = Quaternion.Euler(0, randomAngle, 0) * currentPatrolDirection;
            Debug.Log($"Jitter: Cambio de direccion de patrulla (Suave: {randomAngle:F1} grados) - Vector: {currentPatrolDirection}");
        }
        else if (decision < 0.8f)
        {
            float escapeAngle = Random.Range(90f, 180f) * (Random.value > 0.5f ? 1f : -1f);
            currentPatrolDirection = Quaternion.Euler(0, escapeAngle, 0) * transform.forward;
            Debug.Log($"Jitter: Cambio de direccion de patrulla (Brusco: {escapeAngle:F1} grados) - Vector: {currentPatrolDirection}");
        }
        else
        {
            float randomNewAngle = Random.Range(0f, 360f);
            currentPatrolDirection = Quaternion.Euler(0, randomNewAngle, 0) * Vector3.forward;
            Debug.Log($"Jitter: Cambio de direccion de patrulla (Totalmente Aleatorio: {randomNewAngle:F1} grados) - Vector: {currentPatrolDirection}");
        }

        currentPatrolDirection.Normalize();
    }

    #endregion

    #region Normal Behavior Logic

    private void HandleNormalBehavior()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        if (agent.speed != normalSpeed) agent.speed = normalSpeed;
        agent.SetDestination(playerTransform.position);

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= normalAttackRange && Time.time >= lastAttackTime + normalAttackInterval)
        {
            StartAttack();
        }
    }

    private void ApplyPlayerSlow(PlayerStatsManager playerStatsManager, float slowPercent, float slowDuration)
    {
        if (playerStatsManager != null)
        {
            string slowKey = "JitterSlow_" + GetInstanceID();
            playerStatsManager.ApplyTimedModifier(slowKey, StatType.MoveSpeed, -slowPercent, slowDuration, isPercentage: true);
        }
    }

    #endregion

    #region Fury Behavior Logic

    private void HandleFuryBehavior()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        agent.SetDestination(playerTransform.position);

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= furyAttackRange)
        {
            timeSinceOutOfRange = 0f;
            furyJumpTimer = 0f;

            if (Time.time >= lastAttackTime + furyAttackInterval)
            {
                StartAttack();
            }
        }
        else
        {
            timeSinceOutOfRange += Time.deltaTime;
            furyJumpTimer += Time.deltaTime;

            if (furyJumpDelay > 0 && furyJumpDuration > 0
                && timeSinceOutOfRange >= furyJumpDelay && furyJumpTimer >= furyJumpDelay)
            {
                PerformFuryJump(distanceToPlayer);
                furyJumpTimer = 0f;
            }
        }
    }

    private void PerformFuryJump(float distanceToPlayer)
    {
        if (knockbackHandler == null || playerTransform == null) return;
        if (isInHitStun || isTransitioningToFury) return;

        if (distanceToPlayer < furyJumpCancelDistance)
        {
            Debug.Log("Jitter: Salto cancelado - jugador demasiado cerca");
            return;
        }

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        directionToPlayer.y = 0;

        float jumpDistance = distanceToPlayer < furyJumpDistance
            ? furyJumpReducedDistance : furyJumpDistance;

        if (furyJumpCoroutine != null)
        {
            StopCoroutine(furyJumpCoroutine);
        }
        furyJumpCoroutine = StartCoroutine(ExecuteFuryJump(directionToPlayer, jumpDistance));
    }

    private IEnumerator ExecuteFuryJump(Vector3 direction, float distance)
    {
        if (furyJumpDuration <= 0f || distance <= 0f)
        {
            Debug.LogWarning("Jitter: ExecuteFuryJump abortado - furyJumpDuration o distance son 0 o negativos.");
            isPerformingJump = false;
            furyJumpCoroutine = null;
            yield break;
        }

        isPerformingJump = true;

        if (audioSource != null && furyJumpSFX != null) audioSource.PlayOneShot(furyJumpSFX);

        if (agent != null && agent.enabled) agent.enabled = false;

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + direction * distance;

        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, distance, NavMesh.AllAreas))
        {
            targetPosition = hit.position;
        }

        float elapsed = 0f;
        float jumpHeight = 1.5f;

        while (elapsed < furyJumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / furyJumpDuration;

            Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, t);
            float heightOffset = Mathf.Sin(t * Mathf.PI) * jumpHeight;

            currentPos.y += heightOffset;
            transform.position = currentPos;

            yield return null;
        }

        transform.position = targetPosition;

        if (agent != null)
        {
            agent.enabled = true;
        }

        isPerformingJump = false;
        furyJumpCoroutine = null;

        Debug.Log($"Jitter: Salto de furia completado - Distancia: {distance}");
    }

    private void PerformPushbackScream()
    {
        if (audioSource != null && pushbackSFX != null)
        {
            audioSource.PlayOneShot(pushbackSFX);
        }
        else if (audioSource != null && furyTransitionSFX != null)
        {
            audioSource.PlayOneShot(furyTransitionSFX);
        }

        if (animCtrl != null && !isAttacking) animCtrl.PlayAttack();

        if (screamHitbox != null)
        {
            StartCoroutine(ShowHitbox(screamHitbox));
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, furyAttackRange, playerLayer);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                Vector3 pushDirection = (hit.transform.position - transform.position).normalized;
                pushDirection.y = 0;

                if (hit.TryGetComponent<EnemyKnockbackHandler>(out var enemyKnockback))
                {
                    enemyKnockback.TriggerKnockback(pushDirection, consecutiveHitPushDistance, 0.3f);
                }
                else if (hit.TryGetComponent<PlayerKnockbackReceiver>(out var playerKnockback))
                {
                    playerKnockback.ApplyKnockback(pushDirection, consecutiveHitPushDistance * 10f, 0.3f);
                }

                Debug.Log("Jitter: Onda de empuje activada");
                break;
            }
        }
    }

    #endregion

    #region State Transition Logic

    private IEnumerator EnterFuryState()
    {
        isTransitioningToFury = true;
        furyLevel = 1;

        CancelAttack();

        if (audioSource != null && furyTransitionSFX != null)
        {
            audioSource.PlayOneShot(furyTransitionSFX);
        }

        if (animCtrl != null) animCtrl.SetFuryMode(true);

        ChangeMaterialToFury();

        if (agent != null && agent.enabled) agent.isStopped = true;

        yield return new WaitForSeconds(screamTransitionDuration);

        if (this == null || !gameObject.activeInHierarchy) yield break;
        if (enemyHealth != null && enemyHealth.IsDead) yield break;
        if (isInHitStun)
        {
            isTransitioningToFury = false;
            yield break;
        }

        if (agent != null && agent.enabled) agent.isStopped = false;

        isInFury = true;
        isTransitioningToFury = false;
        consecutiveHitsReceived = 0;

        UpdateFurySpeed();

        if (furyRegenerationCoroutine != null)
        {
            StopCoroutine(furyRegenerationCoroutine);
        }
        furyRegenerationCoroutine = StartCoroutine(FuryRegenerationRoutine());
    }

    private void UpdateFurySpeed()
    {
        if (agent == null || !agent.enabled) return;

        float speedMultiplier = furySpeedMultiplier;

        if (furyLevel >= 2)
        {
            speedMultiplier += furyLevel2SpeedBonus;
        }

        agent.speed = normalSpeed * speedMultiplier;

        Debug.Log($"Jitter: Velocidad ajustada a {agent.speed} unidades/s (Nivel {furyLevel})");
    }

    private void ExitFuryState()
    {
        isInFury = false;
        furyLevel = 0;
        consecutiveHitsReceived = 0;

        if (audioSource != null && calmTransitionSFX != null) audioSource.PlayOneShot(calmTransitionSFX);

        // SetFuryMode(false) desactiva RageMode en el Animator
        if (animCtrl != null) animCtrl.SetFuryMode(false);

        if (agent != null && agent.enabled) agent.speed = normalSpeed;

        ChangeMaterialToNormal();

        if (furyRegenerationCoroutine != null)
        {
            StopCoroutine(furyRegenerationCoroutine);
            furyRegenerationCoroutine = null;
        }

        pendingEnterFuryAfterHitStun = false;
        pendingPushbackScreamAfterHitStun = false;

        Debug.Log("Jitter: Saliendo del estado de furia");
    }

    private void CheckFuryDecay()
    {
        float decayTime = furyDecayTime;

        if (playerDetected && Time.time - lastDamageInflictedTime < furyDecayNoDamageTime)
        {
            decayTime = furyDecayNoDamageTime;
        }

        if (Time.time - lastDamageTime >= decayTime &&
            Time.time - lastDamageInflictedTime >= decayTime)
        {
            ExitFuryState();
        }
    }

    private IEnumerator FuryDecayRoutine()
    {
        float decayTime = furyDecayTime;

        if (playerDetected && Time.time - lastDamageInflictedTime < furyDecayNoDamageTime)
        {
            decayTime = furyDecayNoDamageTime;
        }

        yield return new WaitForSeconds(decayTime);

        if (isInFury && Time.time - lastDamageInflictedTime >= decayTime)
        {
            ExitFuryState();
        }
    }

    private IEnumerator FuryRegenerationRoutine()
    {
        while (isInFury)
        {
            yield return new WaitForSeconds(1f);

            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                enemyHealth.Heal(furyRegenerationPerSecond);
                Debug.Log($"Jitter: Regeneracion +{furyRegenerationPerSecond} PV");
            }
        }
    }

    #endregion

    #region Combat And Attack Execution

    private void StartAttack()
    {
        lastAttackTime = Time.time;
        isAttacking = true;

        if (playerTransform != null)
        {
            frozenAttackDirection = (playerTransform.position - transform.position).normalized;
            frozenAttackDirection.y = 0;
            if (frozenAttackDirection == Vector3.zero) frozenAttackDirection = transform.forward;
        }
        else
        {
            frozenAttackDirection = transform.forward;
        }

        transform.forward = frozenAttackDirection;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.velocity = Vector3.zero;
        }

        if (animCtrl != null) animCtrl.PlayAttack();

        AudioClip clip = isInFury ? furyAttackClip : normalAttackClip;
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private void CancelAttack()
    {
        if (!isAttacking) return;

        isAttacking = false;
        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
            agent.updatePosition = true;
            agent.updateRotation = true;
        }
    }

    private void OnAttackImpact()
    {
        lastDamageInflictedTime = Time.time;

        if (isInFury && furyAttackVFXPrefab != null)
        {
            Vector3 vfxPos = furyAttackVFXSpawnPoint != null
                ? furyAttackVFXSpawnPoint.position
                : transform.position;
            Instantiate(furyAttackVFXPrefab, vfxPos, transform.rotation);
        }

        float damage = isInFury ? furyAttackDamage : normalAttackDamage;
        float range = isInFury ? furyAttackRange : normalAttackRange;
        GameObject hitbox = isInFury ? furyAttackHitbox : normalAttackHitbox;

        if (hitbox != null) StartCoroutine(ShowHitbox(hitbox));

        Collider[] hits = Physics.OverlapSphere(transform.position, range, playerLayer);
        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;

            Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
            dirToTarget.y = 0;
            if (Vector3.Dot(dirToTarget, frozenAttackDirection) < 0f) continue;

            ExecuteAttack(hit.gameObject, damage);

            if (!isInFury)
            {
                PlayerStatsManager statsManager = hit.GetComponent<PlayerStatsManager>();
                if (statsManager != null) ApplyPlayerSlow(statsManager, normalAttackSlowPercent, normalAttackSlowDuration);
            }
            break;
        }
    }

    private void OnAttackEnd()
    {
        if (isInHitStun) return;
        if (isInAnticipation) return;

        isAttacking = false;
        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
            agent.updatePosition = true;
            agent.updateRotation = true;
        }
    }

    private void ExecuteAttack(GameObject target, float damageAmount)
    {
        if (target.TryGetComponent<PlayerHealth>(out var health))
        {
            if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) 
                && blockSystem.IsBlocking 
                && blockSystem.CanBlockAttack(transform.position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount);

                if (remainingDamage > 0f)
                {
                    health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                }
                Debug.Log($"<color=red>[Jitter] Ataque bloqueado por el jugador.</color>");
                return;
            }
            else
            {
                health.TakeDamage(Mathf.Max(0, damageAmount), false, AttackDamageType.Melee);
            }
        }
    }

    #endregion

    #region Hit Stun And Damage Handling

    private void HandleDamageTaken()
    {
        if (enemyHealth != null && enemyHealth.IsDead) return;

        bool hasToughness = enemyToughness != null && enemyToughness.HasToughness;

        lastDamageTime = Time.time;

        if (!playerDetected && playerTransform != null)
        {
            playerDetected = true;
        }

        if (!isInFury)
        {
            pendingEnterFuryAfterHitStun = true;
        }
        else
        {
            consecutiveHitsReceived++;

            if (furyLevel < maxFuryLevel)
            {
                furyLevel++;
                UpdateFurySpeed();
            }

            if (consecutiveHitsReceived >= 2)
            {
                pendingPushbackScreamAfterHitStun = true;
                consecutiveHitsReceived = 0;
            }

            if (furyRegenerationCoroutine != null)
            {
                StopCoroutine(furyRegenerationCoroutine);
            }
            furyRegenerationCoroutine = StartCoroutine(FuryRegenerationRoutine());
        }

        if (!hasToughness)
        {
            if (hitStunCoroutine != null)
            {
                StopCoroutine(hitStunCoroutine);
            }
            hitStunCoroutine = StartCoroutine(HitStunRoutine());
        }
        else
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh && !isTransitioningToFury)
            {
                if (!isAttacking)
                {
                    agent.isStopped = false;
                }
            }
        }

        if (furyDecayCoroutine != null)
        {
            StopCoroutine(furyDecayCoroutine);
        }
        furyDecayCoroutine = StartCoroutine(FuryDecayRoutine());
    }

    private void HandleToughnessHit()
    {
        if (enemyHealth != null && enemyHealth.IsDead) return;

        //CancelAttack();
        //if (animCtrl != null) animCtrl.PlayDamage();
        if (audioSource != null && toughnessBlockSFX != null)
        {
            audioSource.PlayOneShot(toughnessBlockSFX);
        }
    }

    private IEnumerator HitStunRoutine()
    {
        isInHitStun = true;

        CancelAnticipation();

        CancelAttack();

        if (furyJumpCoroutine != null)
        {
            StopCoroutine(furyJumpCoroutine);
            furyJumpCoroutine = null;
            isPerformingJump = false;

            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }
        }

        if (agent != null && agent.enabled)
        {
            if (!agent.isOnNavMesh)
            {
                NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas);
                agent.Warp(hit.position);
            }

            agent.isStopped = true;
            agent.ResetPath();
        }

        if (animCtrl != null && !isTransitioningToFury)
        {
            animCtrl.PlayDamage();
        }

        yield return new WaitForSeconds(hitStunDuration);

        yield return new WaitForSeconds(forceIdleDuration);

        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.ResetPath();
        }

        isInHitStun = false;
        hitStunCoroutine = null;
        hitStunRecoveryCooldown = hitStunRecoveryGrace;

        if (pendingEnterFuryAfterHitStun && !isInFury && !isTransitioningToFury)
        {
            pendingEnterFuryAfterHitStun = false;
            StartCoroutine(EnterFuryState());
            yield break;
        }

        if (pendingPushbackScreamAfterHitStun && isInFury && !isTransitioningToFury)
        {
            pendingPushbackScreamAfterHitStun = false;
            PerformPushbackScream();
        }
    }

    private IEnumerator ReduceKnockbackEffect()
    {
        yield return null;

        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position + Vector3.up * agent.baseOffset;
            }
        }
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        CancelAnticipation();

        isInHitStun = false;

        StopAllCoroutines();

        if (normalAttackHitbox != null) normalAttackHitbox.SetActive(false);
        if (furyAttackHitbox != null) furyAttackHitbox.SetActive(false);
        if (screamHitbox != null) screamHitbox.SetActive(false);

        pendingEnterFuryAfterHitStun = false;
        pendingPushbackScreamAfterHitStun = false;
        isAttacking = false;
        isPerformingJump = false;
        isTransitioningToFury = false;

        if (agent != null)
        {
            agent.enabled = false;
        }

        if (animCtrl != null) animCtrl.PlayDeath();

        if (audioSource != null && deathSFX != null)
        {
            audioSource.PlayOneShot(deathSFX);
        }

        this.enabled = false;
    }

    #endregion

    #region Animation Events And Anticipation

    private void UpdateAnimationState()
    {
        if (animCtrl == null || agent == null) return;

        bool isMoving = agent.velocity.sqrMagnitude > 0.1f && !agent.isStopped;
        animCtrl.isWalking = isMoving;
    }

    public void HandleAnimEvents(string eventName)
    {
        switch (eventName)
        {
            case "AttackImpact":
                OnAttackImpact();
                break;
            case "AttackEnd":
                OnAttackEnd();
                break;
            case "AnimEvent_AnticipationPause":
                StartAnticipationPause();
                break;
        }
    }

    private void StartAnticipationPause()
    {
        if (enemyHealth != null && enemyHealth.IsDead) return;
        if (isInHitStun || hitStunRecoveryCooldown > 0f) return;

        if (anticipationCoroutine != null) StopCoroutine(anticipationCoroutine);
        anticipationCoroutine = StartCoroutine(AnticipationRoutine());
    }

    private IEnumerator AnticipationRoutine()
    {
        isInAnticipation = true;

        float duration = isInFury ? furyAnticipationDuration : calmAnticipationDuration;
        AudioClip sfx = isInFury ? furyAnticipationSFX : calmAnticipationSFX;

        if (animCtrl != null) animCtrl.PauseAnimation();

        if (audioSource != null && sfx != null)
        {
            audioSource.PlayOneShot(sfx);
        }

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.PlayAnticipationBlink(duration);
        }

        if (animCtrl != null)
        {
            animCtrl.PlayAnticipationShake(duration);
        }

        yield return new WaitForSeconds(duration);

        if (animCtrl != null) animCtrl.ResumeAnimation();

        isInAnticipation = false;
        anticipationCoroutine = null;
    }

    private void CancelAnticipation()
    {
        if (anticipationCoroutine != null)
        {
            StopCoroutine(anticipationCoroutine);
            anticipationCoroutine = null;
        }

        if (animCtrl != null) animCtrl.ResumeAnimation();
        if (animCtrl != null) animCtrl.StopAnticipationShake();
        if (enemyVisualEffects != null) enemyVisualEffects.CancelAnticipationBlink();

        isInAnticipation = false;
    }

    #endregion

    #region Audio System

    private void HandleAudioLoop()
    {
        if (agent == null || !agent.enabled) return;

        bool isMoving = !agent.isStopped && agent.velocity.sqrMagnitude > 0.2f;

        if (isMoving)
        {
            audioStepTimer += Time.deltaTime;

            float stepRate = isInFury ? 0.35f : 0.6f;

            if (audioStepTimer >= stepRate)
            {
                AudioClip clipToPlay = isInFury ? furyMoveSFX : normalMoveSFX;

                if (audioSource != null && clipToPlay != null)
                {
                    if (isInFury) audioSource.pitch = Random.Range(1.1f, 1.3f);
                    else audioSource.pitch = Random.Range(0.9f, 1.1f);

                    audioSource.PlayOneShot(clipToPlay, 0.75f);
                    audioSource.pitch = 1f;
                }
                audioStepTimer = 0f;
            }
            ResetIdleAudioTimer();
        }
        else
        {
            idleAudioTimer += Time.deltaTime;
            if (idleAudioTimer >= idleAudioInterval)
            {
                if (audioSource != null && presenceSFX != null)
                {
                    audioSource.PlayOneShot(presenceSFX);
                }
                ResetIdleAudioTimer();
            }
        }
    }

    private void ResetIdleAudioTimer()
    {
        idleAudioTimer = 0f;
        idleAudioInterval = Random.Range(4f, 8f);
    }

    #endregion

    #region Visual Feedback

    private void ChangeMaterialToFury()
    {
        if (enemyRenderer == null || furyStateMaterial == null) return;

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.UpdateBaseMaterial(enemyRenderer, furyStateMaterial);
        }
        else
        {
            enemyRenderer.material = furyStateMaterial;
        }
    }

    private void ChangeMaterialToNormal()
    {
        if (enemyRenderer == null) return;

        Material matToUse = (normalStateMaterial != null) ? normalStateMaterial : originalMaterial;

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.UpdateBaseMaterial(enemyRenderer, matToUse);
        }
        else
        {
            enemyRenderer.material = matToUse;
        }
    }

    private IEnumerator ShowHitbox(GameObject hitbox)
    {
        if (hitbox != null)
        {
            hitbox.SetActive(true);
            yield return new WaitForSeconds(hitboxDisplayDuration);
            hitbox.SetActive(false);
        }
    }

    #endregion

    #region Logging And Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
        Gizmos.DrawSphere(transform.position, visionRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Vector3 forward = transform.forward;
        Vector3 leftBoundary = Quaternion.Euler(0, -visionConeAngle, 0) * forward;
        Vector3 rightBoundary = Quaternion.Euler(0, visionConeAngle, 0) * forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, leftBoundary * visionRange);
        Gizmos.DrawRay(transform.position, rightBoundary * visionRange);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, forward * visionRange);

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        int segments = 20;
        Vector3 previousPoint = transform.position + leftBoundary * visionRange;

        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-visionConeAngle, visionConeAngle, i / (float)segments);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * forward;
            Vector3 point = transform.position + direction * visionRange;

            Gizmos.DrawLine(previousPoint, point);
            Gizmos.DrawLine(transform.position, point);

            previousPoint = point;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, normalAttackRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, furyAttackRange);

        if (Application.isPlaying && isInFury)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, furyJumpDistance);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, furyJumpReducedDistance);
        }

        if (Application.isPlaying && !playerDetected)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, currentPatrolDirection * 3f);
        }

        if (Application.isPlaying && playerDetected && playerTransform != null)
        {
            Gizmos.color = isInFury ? Color.red : Color.yellow;
            Gizmos.DrawLine(transform.position, playerTransform.position);
        }
    }

    #endregion
}