using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Clase base abstracta para todas las variantes del enemigo Static.
/// </summary>
public abstract class StaticEnemyBase : MonoBehaviour
{
    #region Enums & Structs

    public enum MorlockLevel { Nivel1, Nivel2, Nivel3 }
    protected enum StaticState { Patrol, Pursue1, Pursue2, Pursue3, Repositioning }

    #endregion

    #region Inspector - References

    [Header("Referencias")]
    [SerializeField] protected StaticAnimCtrl animCtrl;
    [SerializeField] protected MorlockStats stats;
    [SerializeField] protected GameObject projectilePrefab;
    [SerializeField] protected Transform firePoint;

    #endregion

    #region Inspector - Difficulty Settings

    [Header("Dificultad de anticipacion por disparo")]
    [SerializeField] protected MorlockLevel currentLevel = MorlockLevel.Nivel1;
    [SerializeField] protected float interceptProbabilityNivel1 = 0.25f;
    [SerializeField] protected float interceptProbabilityNivel2 = 0.5f;
    [SerializeField] protected float interceptProbabilityNivel3 = 0.75f;

    #endregion

    #region Inspector - Base Stats Settings

    [Header("Estadisticas (fallback si no hay MorlockStats)")]
    [Header("Vida")]
    [SerializeField] protected float health = 15f;
    [Header("Movimiento")]
    [SerializeField] protected float moveSpeed = 4f;

    #endregion

    #region Inspector - Combat Settings

    [Header("Combate")]
    [SerializeField] protected float fireRate = 1.5f;
    [SerializeField] protected bool useRandomFireRate = true;
    [SerializeField] protected float minFireRate = 1.5f;
    [SerializeField] protected float maxFireRate = 3f;
    [SerializeField] protected float minDamageIncrease = 1f;
    [SerializeField] protected float projectileSpeed = 12f;
    [SerializeField] protected float maxDamageIncrease = 2;
    [SerializeField] protected float maxDistanceForDamageIncrease = 6f;
    [SerializeField] protected float maxDistanceForDamageStart = 20f;
    [SerializeField] protected float attackRange = 50f;

    #endregion

    #region Inspector - Patrol Settings

    [Header("Patrullaje")]
    [SerializeField] protected bool canReturnToPatrol = true;
    [SerializeField] protected float detectionRadius = 50f;
    [SerializeField] protected float detectionGrowthInterval = 2.5f;
    [SerializeField] protected float detectionGrowthAmount = 10f;
    [SerializeField] protected float teleportRange = 5f;
    [SerializeField] protected Transform[] patrolWaypoints;
    [SerializeField] protected bool loopWaypoints = true;
    [SerializeField] protected int freePatrolIterations = 10;
    [SerializeField] protected bool patrolAroundOrigin = true;
    [SerializeField] protected float patrolRadius = 8f;
    [SerializeField] protected float patrolIdleTime = 1.2f;
    [SerializeField] protected bool canReposition = false;
    [SerializeField] protected float repositionTeleportCooldown = 0.75f;

    #endregion

    #region Inspector - Pursuit Settings

    [Header("Perseguir 1")]
    [SerializeField] protected float p1_teleportCooldown = 2.5f;
    [SerializeField] protected float p1_pursuitAdvanceDistance = 4f;
    [SerializeField] protected float p1_pursuitLateralVariationMin = 3f;
    [SerializeField] protected float p1_pursuitLateralVariationMax = 5f;

    [Header("Perseguir 2")]
    [SerializeField] protected float p2_activationRadius = 5f;
    [SerializeField] protected float p2_teleportCooldown = 2.5f;
    [SerializeField] protected float p2_teleportRange = 5f;

    #endregion

    #region Inspector - Spawn Settings

    [Header("Configuracion de spawn")]
    [SerializeField] protected float spawnDelay = 1.0f;

    #endregion

    #region Inspector - Enemy Separation Settings

    [Header("Separacion entre enemigos")]
    [SerializeField] private float minEnemySeparation = 2.5f;
    [SerializeField] private int separationAttempts = 8;

    #endregion

    #region Inspector - Audio Settings

    [Header("Sound")]
    [SerializeField] protected AudioSource audioSource;
    [Header("SFX Generales")]
    [SerializeField] protected AudioClip idleSFX;
    [SerializeField] protected AudioClip teleportSFX;
    [SerializeField] protected AudioClip deathSFX;
    [Header("SFX Combate")]
    [SerializeField] protected AudioClip shootSFX;

    #endregion

    #region Inspector - VFX Settings

    [Header("VFX Teletransporte")]
    //[SerializeField] protected float animTeleportDelay = 1f;
    [SerializeField] protected GameObject teleportVFX;
    [SerializeField] protected float teleportVFXHeightOffset = 1.5f;
    [SerializeField] protected float teleportVFXDurationToDestroy = 1.5f;
    [SerializeField] protected float teleportVFXDelay = 1.5f;

    #endregion

    #region Inspector - Telegraphed Settings

    [Header("Hit Stun")]
    [SerializeField] protected float hitStunDuration = 0.3f;
    [SerializeField] protected float forceIdleDuration = 0.8f;

    [Header("VFX Ataque")]
    [SerializeField] protected GameObject attackVFXPrefab;
    [SerializeField] protected Transform attackVFXSpawnPoint;

    [Header("SFX Dano")]
    [SerializeField] protected AudioClip hitStunSFX;
    [SerializeField] protected AudioClip toughnessBlockSFX;

    [Header("Anticipacion de Ataque")]
    [SerializeField] protected float anticipationPauseDuration = 0.6f;
    [SerializeField] protected float anticipationSFXPitch = 1.0f;
    [SerializeField] protected AudioClip anticipationSFX;

    #endregion

    #region Internal State

    protected EnemyHealth enemyHealth;
    protected EnemyToughness enemyToughness;
    private EnemyVisualEffects enemyVisualEffects;
    protected NavMeshAgent agent;
    protected Transform playerTransform;
    protected CharacterController playerCharacterController;
    protected MorlockWordLibrary wordLibrary;
    protected StaticState currentState;
    protected StaticState stateBeforeHitStun;

    protected bool isInHitStun = false;
    protected bool isDead = false;
    protected bool isReady = false;
    protected bool isInAnticipation = false;
    protected bool isAttacking = false;

    protected Coroutine hitStunCoroutine;
    protected Coroutine currentBehaviorCoroutine = null;
    protected Coroutine shootCoroutine = null;
    protected Coroutine anticipationCoroutine = null;
    protected Coroutine spawnCoroutine = null;

    protected int currentWaypointIndex = 0;
    protected Vector3 originPosition;
    protected List<GameObject> activeTeleportVFXs = new List<GameObject>();

    protected float idleTimer;
    protected float idleInterval;
    protected float currentDetectionTimer;
    protected float baseDetectionRadius;
    protected Vector3 lastLookDirection = Vector3.forward;

    protected static readonly List<Vector3> reservedPositions = new List<Vector3>();
    protected Vector3 ownReservedPosition = new Vector3(float.MinValue, float.MinValue, float.MinValue);
    protected static readonly Vector3 unsetPosition = new Vector3(float.MinValue, float.MinValue, float.MinValue);

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyToughness == null) enemyToughness = GetComponent<EnemyToughness>();
        if (enemyVisualEffects == null) enemyVisualEffects = GetComponent<EnemyVisualEffects>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animCtrl == null) animCtrl = GetComponentInChildren<StaticAnimCtrl>();
        if (wordLibrary == null) wordLibrary = GetComponent<MorlockWordLibrary>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();
    }

    protected virtual void Start()
    {
        originPosition = transform.position;
        baseDetectionRadius = detectionRadius;

        var playerGameObject = GameObject.FindGameObjectWithTag("Player");
        if (playerGameObject != null)
        {
            playerTransform = playerGameObject.transform;
            playerCharacterController = playerGameObject.GetComponent<CharacterController>();
        }
        else
        {
            ReportDebug("Jugador no encontrado en Start. Reintentando busqueda.", 2);
            StartCoroutine(RetryFindPlayerRoutine());
        }

        InitializedEnemy();
        ResetIdleTimer();
    }

    protected virtual IEnumerator RetryFindPlayerRoutine()
    {
        float elapsed = 0f;
        const float retryInterval = 0.5f;
        const float retryTimeout = 5f;

        while (playerTransform == null && elapsed < retryTimeout)
        {
            yield return new WaitForSeconds(retryInterval);
            elapsed += retryInterval;

            var playerGameObject = GameObject.FindGameObjectWithTag("Player");
            if (playerGameObject != null)
            {
                playerTransform = playerGameObject.transform;
                playerCharacterController = playerGameObject.GetComponent<CharacterController>();
                ReportDebug("Jugador encontrado tras reintento.", 1);
                yield break;
            }
        }

        if (playerTransform == null)
        {
            ReportDebug("Jugador no encontrado tras agotar reintentos. El enemigo quedara inactivo.", 3);
        }
    }

    protected virtual void OnEnable()
    {
        isReady = false;
        isInHitStun = false;
        isInAnticipation = false;
        isDead = false;

        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleEnemyDeath;
            enemyHealth.OnDamaged += HandleDamageTaken;
            enemyHealth.OnToughnessHit += HandleToughnessHit;
        }

        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    protected virtual void OnDisable()
    {
        isReady = false;
        isInHitStun = false;

        CancelAnticipation();
        ReleasePosition();

        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
            enemyHealth.OnToughnessHit -= HandleToughnessHit;
        }

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        StopAllCoroutines();
    }

    protected virtual void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
            enemyHealth.OnToughnessHit -= HandleToughnessHit;
        }
        StopAllCoroutines();
    }

    protected virtual void Update()
    {
        if (isDead || playerTransform == null || !isReady) return;

        UpdateAnimationAndRotation();

        if (enemyHealth != null && enemyHealth.IsStunned) return;

        HandleIdleSound();

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float distanceFromOrigin = Vector3.Distance(transform.position, originPosition);

        if (distanceToPlayer > detectionRadius && (currentState != StaticState.Patrol && currentState != StaticState.Repositioning))
        {
            if (canReturnToPatrol)
            {
                if (patrolAroundOrigin && distanceFromOrigin > patrolRadius * 1.1f)
                {
                    if (canReposition) ChangeState(StaticState.Repositioning);
                    else ChangeState(StaticState.Patrol);
                }
                else ChangeState(StaticState.Patrol);
            }
            return;
        }

        switch (currentState)
        {
            case StaticState.Patrol:
                HandleDetectionGrowth();
                if (distanceToPlayer <= detectionRadius)
                {
                    ChangeState(StaticState.Pursue1);
                }
                break;
            case StaticState.Pursue1:
                if (distanceToPlayer <= p2_activationRadius)
                {
                    ChangeState(StaticState.Pursue2);
                }
                break;
            case StaticState.Pursue2:
                break;
        }
    }

    #endregion

    #region Initialization & Data Sync

    protected virtual void InitializedEnemy()
    {
        if (enemyHealth == null)
        {
            ReportDebug("EnemyHealth no encontrado en el GameObject. El enemigo no puede inicializar su vida.", 3);
        }

        if (stats != null)
        {
            health = stats.health;
            moveSpeed = stats.moveSpeed;
            attackRange = stats.optimalAttackDistance;
            teleportRange = stats.teleportRange;
            fireRate = stats.fireRate;
            minDamageIncrease = stats.projectileDamage;
            projectileSpeed = stats.projectileSpeed;
        }
        else
        {
            ReportDebug("MorlockStats no asignado. Usando valores por defecto.", 2);
        }

        if (enemyHealth != null) enemyHealth.SetMaxHealth(health);

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRange;
            agent.updatePosition = true;
            agent.updateRotation = false;
            agent.isStopped = false;
        }
    }

    protected virtual IEnumerator SpawnRoutine()
    {
        isReady = false;
        yield return new WaitForSeconds(spawnDelay);
        isReady = true;
        spawnCoroutine = null;
        ChangeState(StaticState.Patrol);
    }

    #endregion

    #region Health & Damage System

    protected virtual void ApplyRoomMultiplier(float damageMult, float speedMult)
    {
        minDamageIncrease *= damageMult;
        moveSpeed *= speedMult;
        if (agent != null) agent.speed = moveSpeed;
        ReportDebug($"Multiplicadores de sala aplicados a Morlock. Dano x{damageMult}, Velocidad x{speedMult}. Nueva Velocidad: {moveSpeed:F2}", 1);
    }

    protected virtual void HandleDamageTaken()
    {
        if (enemyHealth != null && enemyHealth.IsDead) return;

        bool hasToughness = enemyToughness != null && enemyToughness.HasToughness;

        if (hasToughness)
        {
            return;
        }

        if (hitStunCoroutine != null) StopCoroutine(hitStunCoroutine);
        hitStunCoroutine = StartCoroutine(HitStunRoutine());
    }

    protected void HandleToughnessHit()
    {
        if (enemyToughness == null || !enemyToughness.HasToughness) return;
        if (enemyHealth != null && enemyHealth.IsDead) return;

        //if (animCtrl != null) animCtrl.PlayDamage();
        if (audioSource != null && toughnessBlockSFX != null)
        {
            audioSource.PlayOneShot(toughnessBlockSFX);
        }
    }

    protected virtual IEnumerator HitStunRoutine()
    {
        isInHitStun = true;
        isAttacking = false;
        stateBeforeHitStun = currentState;

        CancelAnticipation();

        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }

        if (currentBehaviorCoroutine != null)
        {
            StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }

        if (animCtrl != null) animCtrl.restoreOriginalMaterials();

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true; agent.ResetPath();
        }

        if (animCtrl != null) animCtrl.PlayDamage();
        if (audioSource != null && hitStunSFX != null) audioSource.PlayOneShot(hitStunSFX);

        yield return new WaitForSeconds(hitStunDuration);

        if (agent != null && agent.enabled) agent.isStopped = false;

        isInHitStun = false;
        hitStunCoroutine = null;

        if (!isDead && isReady) yield return StartCoroutine(ForceIdleBrieflyRoutine(forceIdleDuration));
    }

    private IEnumerator ForceIdleBrieflyRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        ChangeState(stateBeforeHitStun);
    }

    protected virtual void HandleEnemyDeath(GameObject enemy)
    {
        if (isDead || enemy != gameObject) return;

        ReleasePosition();
        isDead = true;

        if (animCtrl != null) animCtrl.PlayDeath();
        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        ChangeState(StaticState.Repositioning);
        StopAllCoroutines();

        for (int i = activeTeleportVFXs.Count - 1; i >= 0; i--)
        {
            if (activeTeleportVFXs[i] != null) Destroy(activeTeleportVFXs[i]);
        }
        activeTeleportVFXs.Clear();

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
        }

        this.enabled = false;
    }

    #endregion

    #region AI State Machine

    protected virtual void ChangeState(StaticState newState)
    {
        if (currentState == newState && currentBehaviorCoroutine != null && currentState != StaticState.Repositioning) return;

        if (currentBehaviorCoroutine != null)
        {
            StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }

        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }

        currentState = newState;

        switch (currentState)
        {
            case StaticState.Patrol:
                detectionRadius = baseDetectionRadius;
                currentDetectionTimer = 0f;
                currentBehaviorCoroutine = StartCoroutine(PatrolRoutine());
                break;
            case StaticState.Pursue1:
                currentBehaviorCoroutine = StartCoroutine(Pursuit1Routine());
                break;
            case StaticState.Pursue2:
                currentBehaviorCoroutine = StartCoroutine(Pursuit2Routine());
                break;
            case StaticState.Repositioning:
                currentBehaviorCoroutine = StartCoroutine(RepositioningRoutine());
                break;
        }
    }

    protected virtual IEnumerator PatrolRoutine()
    {
        int freePatrolCount = 0;
        bool shouldContinue = true;

        while (currentState == StaticState.Patrol && shouldContinue)
        {
            Vector3 targetPosition;
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                targetPosition = patrolWaypoints[currentWaypointIndex].position;
                if (loopWaypoints) currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
                else
                {
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= patrolWaypoints.Length) shouldContinue = false;
                }
            }
            else
            {
                if (!loopWaypoints)
                {
                    freePatrolCount++;
                    if (freePatrolCount > freePatrolIterations) shouldContinue = false;
                }

                Vector3 center = patrolAroundOrigin ? originPosition : transform.position;
                TryGetRandomPoint(center, patrolRadius, out targetPosition);
            }

            yield return StartCoroutine(TeleportToPositionRoutine(targetPosition));
            yield return new WaitForSeconds(patrolIdleTime);
        }
    }

    protected virtual IEnumerator RepositioningRoutine()
    {
        if (originPosition == Vector3.zero) originPosition = transform.position;

        while (currentState == StaticState.Repositioning)
        {
            float distFromOrigin = Vector3.Distance(transform.position, originPosition);

            if (distFromOrigin <= patrolRadius)
            {
                ChangeState(StaticState.Patrol);
                yield break;
            }

            Vector3 stepTarget = Vector3.MoveTowards(transform.position, originPosition, teleportRange);

            NavMeshHit stepHit;
            Vector3 finalPos = stepTarget;
            if (NavMesh.SamplePosition(stepTarget, out stepHit, 2.5f, NavMesh.AllAreas))
            {
                finalPos = stepHit.position;
            }

            OnBeforeTeleport(transform.position);

            if (animCtrl != null) animCtrl.PlayTPout();
            if (audioSource != null && teleportSFX != null) audioSource.PlayOneShot(teleportSFX);
            SpawnTeleportVFX(transform.position);

            yield return new WaitForSeconds(0.4f);

            transform.position = finalPos;
            if (agent != null && agent.enabled) agent.Warp(finalPos);

            if (animCtrl != null)
            {
                animCtrl.restoreOriginalMaterials();
                animCtrl.PlayTPin();
            }
            SpawnTeleportVFX(finalPos);

            yield return new WaitForSeconds(repositionTeleportCooldown);
        }
    }

    protected virtual IEnumerator Pursuit1Routine()
    {
        while (currentState == StaticState.Pursue1)
        {
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 advancePosition = transform.position + directionToPlayer * p1_pursuitAdvanceDistance;

            Vector3 lateralDirection = Vector3.Cross(directionToPlayer, Vector3.up);
            float lateralOffset = Random.Range(p1_pursuitLateralVariationMin, p1_pursuitLateralVariationMax) * (Random.value > 0.5f ? 1f : -1f);

            Vector3 targetPosition = advancePosition + lateralDirection * lateralOffset;

            yield return StartCoroutine(TeleportToPositionRoutine(targetPosition));
            StartShootCoroutine();
            yield return new WaitForSeconds(p1_teleportCooldown);

            while (shootCoroutine != null || isInAnticipation) yield return null;
        }
    }

    protected virtual IEnumerator Pursuit2Routine()
    {
        Vector3 lastTargetPos = Vector3.zero;
        int mask = GetWalkableMask();

        while (currentState == StaticState.Pursue2)
        {
            List<Vector3> validCandidates = new List<Vector3>();
            float[] angles = { 0f, 90f, 180f, 270f };

            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 offset = Quaternion.Euler(0, angles[i], 0) * Vector3.forward * p2_teleportRange;
                Vector3 potentialPos = playerTransform.position + offset;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(potentialPos, out hit, 2.0f, mask))
                {
                    validCandidates.Add(hit.position);
                }
            }

            if (validCandidates.Count > 0)
            {
                Vector3 selectedPos;
                if (validCandidates.Count > 1 && lastTargetPos != Vector3.zero)
                {
                    var freshCandidates = validCandidates.FindAll(p => Vector3.Distance(p, lastTargetPos) > 1.0f);
                    if (freshCandidates.Count > 0) selectedPos = freshCandidates[Random.Range(0, freshCandidates.Count)];
                    else selectedPos = validCandidates[Random.Range(0, validCandidates.Count)];
                }
                else
                {
                    selectedPos = validCandidates[Random.Range(0, validCandidates.Count)];
                }

                lastTargetPos = selectedPos;
                yield return StartCoroutine(TeleportToPositionRoutine(selectedPos));
                StartShootCoroutine();
            }
            else
            {
                ReportDebug("Pursue2: Ningun punto cumple con la distancia/navmesh requerida. Esperando.", 2);
            }

            yield return new WaitForSeconds(p2_teleportCooldown);

            while (shootCoroutine != null || isInAnticipation) yield return null;
        }
    }

    #endregion

    #region Movement & Navigation

    protected virtual int GetWalkableMask()
    {
        int trapsAreaIndex = NavMesh.GetAreaFromName("Traps");
        if (trapsAreaIndex != -1)
        {
            return ~(1 << trapsAreaIndex);
        }
        return NavMesh.AllAreas;
    }

    protected virtual void HandleDetectionGrowth()
    {
        currentDetectionTimer += Time.deltaTime;
        if (currentDetectionTimer >= detectionGrowthInterval)
        {
            detectionRadius += detectionGrowthAmount;
            currentDetectionTimer = 0f;
            ReportDebug($"Rango de deteccion aumentado. Nuevo radio: {detectionRadius}", 1);
        }
    }

    protected virtual void UpdateAnimationAndRotation()
    {
        Vector3 targetDirection = Vector3.zero;

        if (currentState == StaticState.Pursue1 || currentState == StaticState.Pursue2)
        {
            if (playerTransform != null) targetDirection = (playerTransform.position - transform.position).normalized;
        }
        else if (agent != null && agent.velocity.sqrMagnitude > 0.1f)
        {
            targetDirection = agent.velocity.normalized;
        }

        if (targetDirection == Vector3.zero) targetDirection = lastLookDirection;
        else targetDirection.y = 0;

        if (targetDirection.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;

            Quaternion targetRotation = Quaternion.Euler(0, snappedAngle, 0);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.deltaTime);

            Vector3 dirVector = Quaternion.Euler(0, snappedAngle, 0) * Vector3.forward;
            float xInt = Mathf.Round(dirVector.x);
            float yInt = Mathf.Round(dirVector.z);
            lastLookDirection = new Vector3(xInt, 0, yInt);
        }
    }

    protected virtual void ForceFacePlayer()
    {
        if (playerTransform == null) return;

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        directionToPlayer.y = 0;

        if (directionToPlayer.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(directionToPlayer.x, directionToPlayer.z) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;
            transform.rotation = Quaternion.Euler(0, snappedAngle, 0);

            Vector3 dirVector = Quaternion.Euler(0, snappedAngle, 0) * Vector3.forward;
            float xInt = Mathf.Round(dirVector.x);
            float yInt = Mathf.Round(dirVector.z);
            lastLookDirection = new Vector3(xInt, 0, yInt);
        }
    }

    protected virtual IEnumerator TeleportToPositionRoutine(Vector3 targetPosition)
    {
        if (enemyHealth != null && enemyHealth.IsStunned) yield break;

        NavMeshHit hit;
        Vector3 finalDestination = targetPosition;
        int mask = GetWalkableMask();

        if (NavMesh.SamplePosition(targetPosition, out hit, 10f, mask))
        {
            finalDestination = hit.position;
        }
        else yield break;

        // Buscar posicion separada si hay otro enemigo cerca
        finalDestination = FindSeparatedPosition(finalDestination);

        // Liberar posicion actual antes de iniciar el TP
        ReleasePosition();

        OnBeforeTeleport(transform.position);

        if (animCtrl != null) animCtrl.PlayTPout();
        SpawnTeleportVFX(transform.position);

        yield return new WaitForSeconds(0.35f);

        if (isDead || (enemyHealth != null && enemyHealth.IsStunned)) yield break;

        if (audioSource != null && teleportSFX != null) audioSource.PlayOneShot(teleportSFX);

        transform.position = finalDestination;
        if (agent != null && agent.enabled) agent.Warp(finalDestination);

        // Reclamar posicion al llegar
        ClaimPosition(finalDestination);

        if (animCtrl != null)
        {
            animCtrl.restoreOriginalMaterials();
            animCtrl.PlayTPin();
        }

        SpawnTeleportVFX(finalDestination);

        if (playerTransform != null && currentState != StaticState.Patrol)
        {
            ForceFacePlayer();
        }
    }

    protected virtual void OnBeforeTeleport(Vector3 fromPosition) { }

    protected virtual bool TryGetRandomPoint(Vector3 center, float radius, out Vector3 result)
    {
        int mask = GetWalkableMask();

        for (int i = 0; i < 8; i++)
        {
            Vector3 rand = center + Random.insideUnitSphere * radius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(rand, out hit, 2.0f, mask))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    #endregion

    #region Combat System

    protected virtual void StartShootCoroutine()
    {
        if (shootCoroutine != null) StopCoroutine(shootCoroutine);
        shootCoroutine = StartCoroutine(ShootAfterDelayRoutine());
    }

    protected virtual IEnumerator ShootAfterDelayRoutine()
    {
        if (useRandomFireRate) fireRate = Random.Range(minFireRate, maxFireRate);

        yield return new WaitForSeconds(fireRate);
        //yield return new WaitForSeconds(animTeleportDelay);

        if (!isDead && currentState != StaticState.Patrol && currentState != StaticState.Repositioning)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer <= attackRange)
            {
                ForceFacePlayer();
                if (animCtrl != null) animCtrl.PlayShoot();
                ExecuteProjectileSpawn();
            }
        }
        shootCoroutine = null;
    }

    protected virtual void ExecuteProjectileSpawn()
    {
        if (enemyHealth != null && enemyHealth.IsStunned || isDead) return;

        if (projectilePrefab == null || firePoint == null)
        {
            ReportDebug("projectilePrefab o firePoint no asignado. No se puede disparar.", 3);
            isAttacking = false;
            return;
        }

        SpawnAttackVFX();

        if (audioSource != null && shootSFX != null) audioSource.PlayOneShot(shootSFX);

        Vector3 aimPoint = playerTransform.position;
        float interceptProb = GetInterceptProbability(currentLevel);

        if (playerCharacterController != null && Random.value < interceptProb)
        {
            aimPoint = CalculateInterceptPoint(playerTransform.position, playerCharacterController.velocity);
        }

        Vector3 directionToAim = (aimPoint - firePoint.position).normalized;
        directionToAim.y = 0f;
        directionToAim.Normalize();
        firePoint.rotation = Quaternion.LookRotation(directionToAim);

        InstantiateAndInitializeProjectile();

        isAttacking = false;
    }

    protected virtual void InstantiateAndInitializeProjectile()
    {
        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        StaticProjectileBase projectile = projectileObj.GetComponent<StaticProjectileBase>();

        if (projectile != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            float calculatedDamage = CalculateDamageByDistance(distanceToPlayer);
            string selectedWord = wordLibrary != null ? wordLibrary.GetRandomWord() : "STATIC";

            projectile.Initialize(projectileSpeed, calculatedDamage, selectedWord);
        }
    }

    public virtual void Shoot()
    {
        ExecuteProjectileSpawn();
    }

    protected virtual float CalculateDamageByDistance(float distance)
    {
        if (distance <= maxDistanceForDamageIncrease) return maxDamageIncrease;
        if (distance >= maxDistanceForDamageStart) return minDamageIncrease;

        float t = (distance - maxDistanceForDamageIncrease) / (maxDistanceForDamageStart - maxDistanceForDamageIncrease);
        return Mathf.Lerp(maxDamageIncrease, minDamageIncrease, t);
    }

    protected virtual float GetInterceptProbability(MorlockLevel level)
    {
        switch (level)
        {
            case MorlockLevel.Nivel1: return interceptProbabilityNivel1;
            case MorlockLevel.Nivel2: return interceptProbabilityNivel2;
            case MorlockLevel.Nivel3: return interceptProbabilityNivel3;
            default: return 0.0f;
        }
    }

    protected virtual Vector3 CalculateInterceptPoint(Vector3 targetPosition, Vector3 targetVelocity)
    {
        Vector3 directionToTarget = targetPosition - firePoint.position;
        float distance = directionToTarget.magnitude;

        if (projectileSpeed <= 0) return targetPosition;

        float timeToIntercept = distance / projectileSpeed;
        Vector3 futurePosition = targetPosition + targetVelocity * timeToIntercept;
        futurePosition += Random.insideUnitSphere * 0.5f;

        return futurePosition;
    }

    #endregion

    #region Hit Stun & Anticipation

    public void StartAnticipationPause()
    {
        if (isDead || isInHitStun) return;
        if (anticipationCoroutine != null) StopCoroutine(anticipationCoroutine);
        anticipationCoroutine = StartCoroutine(AnticipationRoutine());
    }

    protected virtual IEnumerator AnticipationRoutine()
    {
        isInAnticipation = true;

        if (animCtrl != null) animCtrl.PauseAnimation();

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

        // Shake de anticipacion
        if (animCtrl != null) animCtrl.PlayAnticipationShake(anticipationPauseDuration);

        yield return new WaitForSeconds(anticipationPauseDuration);

        if (animCtrl != null) animCtrl.ResumeAnimation();

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

        if (animCtrl != null) animCtrl.ResumeAnimation();
        if (animCtrl != null) animCtrl.StopAnticipationShake();
        if (enemyVisualEffects != null) enemyVisualEffects.CancelAnticipationBlink();
        isInAnticipation = false;
    }

    #endregion

    #region Enemy Separation System

    private void ClaimPosition(Vector3 pos)
    {
        ReleasePosition();
        ownReservedPosition = pos;
        reservedPositions.Add(pos);
    }

    private void ReleasePosition()
    {
        if (ownReservedPosition != unsetPosition)
        {
            reservedPositions.Remove(ownReservedPosition);
            ownReservedPosition = unsetPosition;
        }
    }

    private bool IsOccupiedByOther(Vector3 pos)
    {
        foreach (var reserved in reservedPositions)
        {
            if (reserved == ownReservedPosition) continue; // ignorar la propia
            if (Vector3.Distance(reserved, pos) < minEnemySeparation) return true;
        }
        return false;
    }

    private Vector3 FindSeparatedPosition(Vector3 desired)
    {
        if (!IsOccupiedByOther(desired)) return desired;

        int mask = GetWalkableMask();

        for (int i = 0; i < separationAttempts; i++)
        {
            // Radio creciente para no buscar siempre en el mismo anillo
            float radius = minEnemySeparation * (1f + i * 0.4f);
            Vector3 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = desired + new Vector3(offset.x, 0f, offset.y);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(candidate, out hit, 2f, mask) && !IsOccupiedByOther(hit.position))
            {
                return hit.position;
            }
        }

        // Fallback determinista: offset basado en InstanceID para evitar solapamiento exacto
        float spread = (GetInstanceID() % 10) * 0.4f - 2f;
        Vector3 fallback = desired + new Vector3(spread, 0f, spread * 0.5f);
        NavMeshHit fallbackHit;
        if (NavMesh.SamplePosition(fallback, out fallbackHit, 3f, mask))
        {
            return fallbackHit.position;
        }

        return desired;
    }

    #endregion

    #region Visual & Audio Effects

    protected virtual void HandleIdleSound()
    {
        idleTimer += Time.deltaTime;
        if (idleTimer >= idleInterval)
        {
            if (audioSource != null && idleSFX != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(idleSFX);
                audioSource.pitch = 1f;
            }
            ResetIdleTimer();
        }
    }

    protected virtual void ResetIdleTimer()
    {
        idleTimer = 0f;
        idleInterval = Random.Range(5f, 9f);
    }

    protected virtual void SpawnAttackVFX()
    {
        if (attackVFXPrefab == null) return;

        Vector3 pos = attackVFXSpawnPoint != null
            ? attackVFXSpawnPoint.position
            : (firePoint != null ? firePoint.position : transform.position);

        Instantiate(attackVFXPrefab, pos, Quaternion.identity);
    }

    protected virtual void SpawnTeleportVFX(Vector3 basePosition)
    {
        Vector3 spawnPos = basePosition;
        spawnPos.y += teleportVFXHeightOffset;

        for (int i = activeTeleportVFXs.Count - 1; i >= 0; i--)
        {
            GameObject existingVFX = activeTeleportVFXs[i];

            if (existingVFX != null)
            {
                if (Vector3.Distance(existingVFX.transform.position, spawnPos) < 1.0f)
                {
                    Destroy(existingVFX);
                    activeTeleportVFXs.RemoveAt(i);
                }
            }
            else
            {
                activeTeleportVFXs.RemoveAt(i);
            }
        }

        if (teleportVFX != null)
        {
            GameObject vfxInstance = Instantiate(teleportVFX, spawnPos, Quaternion.identity);
            activeTeleportVFXs.Add(vfxInstance);
            StartCoroutine(RemoveVFXFromListAfterDelay(vfxInstance, teleportVFXDurationToDestroy));
        }
    }

    protected virtual IEnumerator RemoveVFXFromListAfterDelay(GameObject vfx, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (vfx != null)
        {
            if (activeTeleportVFXs.Contains(vfx)) activeTeleportVFXs.Remove(vfx);
            Destroy(vfx);
        }
    }

    #endregion

    #region Logging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    protected static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1: Debug.Log($"[StaticEnemyBase] {message}"); break;
            case 2: Debug.LogWarning($"[StaticEnemyBase] {message}"); break;
            case 3: Debug.LogError($"[StaticEnemyBase] {message}"); break;
            default: Debug.Log($"[StaticEnemyBase] {message}"); break;
        }
    }

    #endregion
}