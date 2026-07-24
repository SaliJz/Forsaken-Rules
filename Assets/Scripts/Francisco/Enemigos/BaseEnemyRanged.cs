using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public abstract class BaseEnemyRanged : MonoBehaviour
{
    #region Inspector Fields

    [Header("References")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private AudioSource audioSource;

    [Header("Base Stats")]
    [SerializeField] private float health = 15f;
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float attackRange = 50f;

    [Header("Projectile")]
    [SerializeField] private float projectileDamage = 1f;
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float maxDamageIncrease = 2f;
    [SerializeField] private float maxRangeForDamageIncrease = 6f;
    [SerializeField] private float maxDistanceForDamageStart = 20f;

    [Header("Fire Rate")]
    [SerializeField] private float fireRate = 1.5f;
    [SerializeField] private bool useRandomFireRate = true;
    [SerializeField] private float minFireRate = 1.5f;
    [SerializeField] private float maxFireRate = 3f;

    [Header("Intercept Prediction")]
    [SerializeField] private float interceptProbability = 0.25f;

    [Header("Patrol")]
    [SerializeField] private bool canReturnToPatrol = true;
    [SerializeField] private float detectionRadius = 50f;
    [SerializeField] private float detectionGrowthInterval = 2.5f;
    [SerializeField] private float detectionGrowthAmount = 10f;
    [SerializeField] private float teleportRange = 5f;
    [SerializeField] private Transform[] patrolWaypoints;
    [SerializeField] private bool loopWaypoints = true;
    [SerializeField] private int freePatrolIterations = 10;
    [SerializeField] private bool patrolAroundOrigin = true;
    [SerializeField] private float patrolRadius = 8f;
    [SerializeField] private float patrolIdleTime = 1.2f;
    [SerializeField] private bool canReposition = false;
    [SerializeField] private float repositionTeleportCooldown = 0.75f;

    [Header("Pursue 1")]
    [SerializeField] private float p1_teleportCooldown = 2.5f;
    [SerializeField] private float p1_pursuitAdvanceDistance = 4f;
    [SerializeField] private float p1_pursuitLateralVariationMin = 3f;
    [SerializeField] private float p1_pursuitLateralVariationMax = 5f;

    [Header("Pursue 2")]
    [SerializeField] private float p2_activationRadius = 5f;
    [SerializeField] private float p2_teleportCooldown = 2.5f;
    [SerializeField] private float p2_teleportRange = 5f;

    [Header("Spawn")]
    [SerializeField] private float spawnDelay = 1f;

    [Header("SFX")]
    [SerializeField] private AudioClip idleSFX;
    [SerializeField] private AudioClip teleportSFX;
    [SerializeField] private AudioClip deathSFX;
    [SerializeField] private AudioClip shootSFX;

    [Header("Teleport VFX")]
    [SerializeField] private float animTeleportDelay = 1f;
    [SerializeField] private GameObject teleportVFX;
    [SerializeField] private float teleportVFXHeightOffset = 1.5f;
    [SerializeField] private float teleportVFXDurationToDestroy = 1.5f;
    [SerializeField] private float teleportVFXDelay = 1.5f;

    #endregion

    #region Protected State

    protected enum EnemyState { Patrol, Pursue1, Pursue2, Repositioning }
    protected EnemyState currentState;

    protected EnemyHealth enemyHealth;
    protected NavMeshAgent agent;
    protected Transform playerTransform;
    protected CharacterController playerCharacterController;
    protected MorlockWordLibrary wordLibrary;
    protected bool isDead = false;
    protected bool isReady = false;

    #endregion

    #region Private State

    private Coroutine currentBehaviorCoroutine;
    private Coroutine shootCoroutine;
    private List<GameObject> activeTeleportVFXs = new List<GameObject>();

    private int currentWaypointIndex;
    private Vector3 originPosition;

    private float idleTimer;
    private float idleInterval;
    private float currentDetectionTimer;
    private float baseDetectionRadius;

    private int animHashX;
    private int animHashY;
    private int animHashAttack;
    private Vector3 lastLookDirection = Vector3.forward;

    #endregion

    #region Properties

    protected AudioSource AudioSource => audioSource;
    protected GameObject ProjectilePrefab => projectilePrefab;
    protected Transform FirePoint => firePoint;

    protected float Health { get => health; set => health = value; }
    protected float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }
    protected float AttackRange { get => attackRange; set => attackRange = value; }
    protected float ProjectileDamage { get => projectileDamage; set => projectileDamage = value; }
    protected float ProjectileSpeed { get => projectileSpeed; set => projectileSpeed = value; }
    protected float MaxDamageIncrease { get => maxDamageIncrease; set => maxDamageIncrease = value; }
    protected float MaxRangeForDamageIncrease { get => maxRangeForDamageIncrease; set => maxRangeForDamageIncrease = value; }
    protected float MaxDistanceForDamageStart { get => maxDistanceForDamageStart; set => maxDistanceForDamageStart = value; }
    protected float FireRate { get => fireRate; set { fireRate = value; minFireRate = value; maxFireRate = value; useRandomFireRate = false; } }
    protected float P1_TeleportCooldown => p1_teleportCooldown;
    protected float P2_ActivationRadius => p2_activationRadius;
    protected float P2_TeleportCooldown => p2_teleportCooldown;
    protected float P2_TeleportRange => p2_teleportRange;
    protected float TeleportVFXDelay => teleportVFXDelay;
    protected float AnimTeleportDelay => animTeleportDelay;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        wordLibrary = GetComponent<MorlockWordLibrary>();

        animHashX = Animator.StringToHash("Xaxis");
        animHashY = Animator.StringToHash("Yaxis");
        animHashAttack = Animator.StringToHash("Attack");

    }

    protected virtual void Start()
    {
        originPosition = transform.position;
        baseDetectionRadius = detectionRadius;

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            playerTransform = playerGO.transform;
            playerCharacterController = playerGO.GetComponent<CharacterController>();
        }

        InitializeEnemy();
        StartCoroutine(SpawnRoutine());
        ResetIdleTimer();
    }

    protected virtual void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleEnemyDeath;
            enemyHealth.OnDamaged += HandleDamageTaken;
        }
    }

    protected virtual void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
        }
        StopAllCoroutines();
    }

    protected virtual void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
        }
        StopAllCoroutines();
    }

    protected virtual void Update()
    {
        if (isDead || playerTransform == null || !isReady) return;

        UpdateAnimationAndRotation();

        if (enemyHealth != null && enemyHealth.IsStunned)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            return;
        }

        HandleIdleSound();

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float distFromOrigin = Vector3.Distance(transform.position, originPosition);

        if (distToPlayer > detectionRadius && currentState != EnemyState.Patrol && currentState != EnemyState.Repositioning)
        {
            if (canReturnToPatrol)
            {
                if (patrolAroundOrigin && distFromOrigin > patrolRadius * 1.1f)
                    ChangeState(canReposition ? EnemyState.Repositioning : EnemyState.Patrol);
                else
                    ChangeState(EnemyState.Patrol);
            }
            return;
        }

        switch (currentState)
        {
            case EnemyState.Patrol:
                HandleDetectionGrowth();
                if (distToPlayer <= detectionRadius) ChangeState(EnemyState.Pursue1);
                break;
            case EnemyState.Pursue1:
                if (distToPlayer <= p2_activationRadius) ChangeState(EnemyState.Pursue2);
                break;
            case EnemyState.Pursue2:
                OnPursue2Update(distToPlayer);
                break;
        }
    }

    #endregion

    #region Initialization

    protected virtual void InitializeEnemy()
    {
        enemyHealth.SetMaxHealth(health);

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRange;
            agent.updatePosition = true;
            agent.updateRotation = false;
            agent.isStopped = false;
        }
    }

    private IEnumerator SpawnRoutine()
    {
        isReady = false;
        yield return new WaitForSeconds(spawnDelay);
        isReady = true;
        ChangeState(EnemyState.Patrol);
    }

    #endregion

    #region Public API

    public void ApplyRoomMultiplier(float damageMult, float speedMult)
    {
        projectileDamage *= damageMult;
        maxDamageIncrease *= damageMult;
        moveSpeed *= speedMult;
        if (agent != null) agent.speed = moveSpeed;
    }

    #endregion

    #region Event Handlers

    protected virtual void HandleDamageTaken() { }

    protected virtual void HandleEnemyDeath(GameObject enemy)
    {
        if (isDead || enemy != gameObject) return;

        isDead = true;

        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        ChangeState(EnemyState.Repositioning);
        StopAllCoroutines();

        for (int i = activeTeleportVFXs.Count - 1; i >= 0; i--)
            if (activeTeleportVFXs[i] != null) Destroy(activeTeleportVFXs[i]);
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

    #region State Machine

    protected void ChangeState(EnemyState newState)
    {
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
            case EnemyState.Patrol:
                detectionRadius = baseDetectionRadius;
                currentDetectionTimer = 0f;
                currentBehaviorCoroutine = StartCoroutine(PatrolRoutine());
                break;
            case EnemyState.Pursue1:
                currentBehaviorCoroutine = StartCoroutine(Pursue1Routine());
                break;
            case EnemyState.Pursue2:
                currentBehaviorCoroutine = StartCoroutine(Pursue2Routine());
                break;
            case EnemyState.Repositioning:
                currentBehaviorCoroutine = StartCoroutine(RepositioningRoutine());
                break;
        }
    }

    protected virtual void OnPursue2Update(float distToPlayer) { }

    #endregion

    #region State Coroutines

    private IEnumerator PatrolRoutine()
    {
        int freePatrolCount = 0;
        bool shouldContinue = true;

        while (currentState == EnemyState.Patrol && shouldContinue)
        {
            Vector3 targetPosition;
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                targetPosition = patrolWaypoints[currentWaypointIndex].position;
                if (loopWaypoints)
                    currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
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
                TryGetRandomNavPoint(center, patrolRadius, out targetPosition);
            }

            yield return StartCoroutine(TeleportToPositionRoutine(targetPosition));
            yield return new WaitForSeconds(patrolIdleTime);
        }
    }

    private IEnumerator RepositioningRoutine()
    {
        if (originPosition == Vector3.zero) originPosition = transform.position;

        while (currentState == EnemyState.Repositioning)
        {
            float distFromOrigin = Vector3.Distance(transform.position, originPosition);
            if (distFromOrigin <= patrolRadius)
            {
                ChangeState(EnemyState.Patrol);
                yield break;
            }

            Vector3 stepTarget = Vector3.MoveTowards(transform.position, originPosition, teleportRange);
            if (NavMesh.SamplePosition(stepTarget, out NavMeshHit stepHit, 2.5f, NavMesh.AllAreas))
            {
                transform.position = stepHit.position;
                if (agent != null && agent.enabled) agent.Warp(stepHit.position);
            }
            else
            {
                transform.position = stepTarget;
                if (agent != null && agent.enabled) agent.Warp(stepTarget);
            }

            if (audioSource != null && teleportSFX != null) audioSource.PlayOneShot(teleportSFX);
            SpawnTeleportVFX(transform.position);

            yield return new WaitForSeconds(repositionTeleportCooldown);
        }
    }

    protected virtual IEnumerator Pursue1Routine()
    {
        while (currentState == EnemyState.Pursue1)
        {
            Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 advancePos = transform.position + dirToPlayer * p1_pursuitAdvanceDistance;
            Vector3 lateral = Vector3.Cross(dirToPlayer, Vector3.up);
            float lateralOffset = Random.Range(p1_pursuitLateralVariationMin, p1_pursuitLateralVariationMax) * (Random.value > 0.5f ? 1f : -1f);
            Vector3 targetPos = advancePos + lateral * lateralOffset;

            yield return StartCoroutine(TeleportToPositionRoutine(targetPos));
            OnAfterPursue1Teleport();
            StartShootCoroutine();

            yield return new WaitForSeconds(p1_teleportCooldown);
        }
    }

    protected virtual IEnumerator Pursue2Routine()
    {
        Vector3 lastTargetPos = Vector3.zero;

        while (currentState == EnemyState.Pursue2)
        {
            List<Vector3> validCandidates = new List<Vector3>();
            float[] angles = { 0f, 90f, 180f, 270f };

            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 offset = Quaternion.Euler(0, angles[i], 0) * Vector3.forward * p2_teleportRange;
                Vector3 potentialPos = playerTransform.position + offset;
                if (NavMesh.SamplePosition(potentialPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    validCandidates.Add(hit.position);
            }

            if (validCandidates.Count > 0)
            {
                Vector3 selectedPos;
                if (validCandidates.Count > 1 && lastTargetPos != Vector3.zero)
                {
                    var fresh = validCandidates.FindAll(p => Vector3.Distance(p, lastTargetPos) > 1f);
                    selectedPos = fresh.Count > 0
                        ? fresh[Random.Range(0, fresh.Count)]
                        : validCandidates[Random.Range(0, validCandidates.Count)];
                }
                else
                {
                    selectedPos = validCandidates[Random.Range(0, validCandidates.Count)];
                }

                lastTargetPos = selectedPos;
                yield return StartCoroutine(TeleportToPositionRoutine(selectedPos));
                OnAfterPursue2Teleport();
                StartShootCoroutine();
            }

            yield return new WaitForSeconds(p2_teleportCooldown);
        }
    }

    #endregion

    #region Teleport

    protected virtual IEnumerator TeleportToPositionRoutine(Vector3 targetPosition)
    {
        if (enemyHealth != null && enemyHealth.IsStunned) yield break;

        if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 10f, NavMesh.AllAreas)) yield break;

        Vector3 finalDestination = hit.position;

        OnBeforeTeleport(transform.position);
        SpawnTeleportVFX(transform.position);

        yield return new WaitForSeconds(teleportVFXDelay);

        if (isDead || (enemyHealth != null && enemyHealth.IsStunned)) yield break;

        if (audioSource != null && teleportSFX != null) audioSource.PlayOneShot(teleportSFX);

        transform.position = finalDestination;
        if (agent != null && agent.enabled) agent.Warp(finalDestination);

        OnAfterTeleport(finalDestination);
        SpawnTeleportVFX(finalDestination);

        if (playerTransform != null && currentState != EnemyState.Patrol) ForceFacePlayer();
    }

    protected virtual void OnBeforeTeleport(Vector3 fromPosition) { }
    protected virtual void OnAfterTeleport(Vector3 toPosition) { }
    protected virtual void OnAfterPursue1Teleport() { }
    protected virtual void OnAfterPursue2Teleport() { }

    private void SpawnTeleportVFX(Vector3 basePosition)
    {
        Vector3 spawnPos = basePosition + Vector3.up * teleportVFXHeightOffset;

        for (int i = activeTeleportVFXs.Count - 1; i >= 0; i--)
        {
            if (activeTeleportVFXs[i] != null)
            {
                if (Vector3.Distance(activeTeleportVFXs[i].transform.position, spawnPos) < 1f)
                {
                    Destroy(activeTeleportVFXs[i]);
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
            StartCoroutine(RemoveVFXAfterDelay(vfxInstance, teleportVFXDurationToDestroy));
        }
    }

    private IEnumerator RemoveVFXAfterDelay(GameObject vfx, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (vfx != null)
        {
            activeTeleportVFXs.Remove(vfx);
            Destroy(vfx);
        }
    }

    #endregion

    #region Shooting

    protected void StartShootCoroutine()
    {
        if (shootCoroutine != null) StopCoroutine(shootCoroutine);
        shootCoroutine = StartCoroutine(ShootAfterDelayRoutine());
    }

    private IEnumerator ShootAfterDelayRoutine()
    {
        float resolvedFireRate = useRandomFireRate ? Random.Range(minFireRate, maxFireRate) : fireRate;
        yield return new WaitForSeconds(resolvedFireRate);
        yield return new WaitForSeconds(animTeleportDelay);

        if (!isDead && currentState != EnemyState.Patrol && currentState != EnemyState.Repositioning)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= attackRange)
            {
                ForceFacePlayer();
                FireProjectile();
            }
        }

        shootCoroutine = null;
    }

    protected virtual void FireProjectile()
    {
        if (enemyHealth != null && enemyHealth.IsStunned) return;
        if (audioSource != null && shootSFX != null) audioSource.PlayOneShot(shootSFX);

        Vector3 aimPoint = playerTransform.position;
        if (playerCharacterController != null && Random.value < interceptProbability)
            aimPoint = CalculateInterceptPoint(playerTransform.position, playerCharacterController.velocity);

        Vector3 dir = (aimPoint - firePoint.position).normalized;
        firePoint.rotation = Quaternion.LookRotation(dir);

        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        MorlockProjectile projectile = projectileObj.GetComponent<MorlockProjectile>();

        if (projectile != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            float damage = CalculateDamageByDistance(dist);
            string word = wordLibrary != null ? wordLibrary.GetRandomWord() : string.Empty;
            projectile.Initialize(projectileSpeed, damage, word);
        }
    }

    protected virtual float CalculateDamageByDistance(float distance)
    {
        float minDamage = projectileDamage;
        float maxDamage = maxDamageIncrease;

        if (distance <= maxRangeForDamageIncrease) return maxDamage;
        if (distance >= maxDistanceForDamageStart) return minDamage;

        float t = (distance - maxRangeForDamageIncrease) / (maxDistanceForDamageStart - maxRangeForDamageIncrease);
        return Mathf.Lerp(maxDamage, minDamage, t);
    }

    private Vector3 CalculateInterceptPoint(Vector3 targetPos, Vector3 targetVel)
    {
        float dist = (targetPos - firePoint.position).magnitude;
        if (projectileSpeed <= 0) return targetPos;
        float t = dist / projectileSpeed;
        return targetPos + targetVel * t + Random.insideUnitSphere * 0.5f;
    }

    #endregion

    #region Animation & Rotation

    private void UpdateAnimationAndRotation()
    {
        Vector3 targetDir = Vector3.zero;

        if (currentState == EnemyState.Pursue1 || currentState == EnemyState.Pursue2)
        {
            if (playerTransform != null) targetDir = (playerTransform.position - transform.position).normalized;
        }
        else if (agent != null && agent.velocity.sqrMagnitude > 0.1f)
        {
            targetDir = agent.velocity.normalized;
        }

        if (targetDir == Vector3.zero) targetDir = lastLookDirection;
        else targetDir.y = 0;

        if (targetDir.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(targetDir.x, targetDir.z) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, snappedAngle, 0), 720f * Time.deltaTime);

            Vector3 dirVec = Quaternion.Euler(0, snappedAngle, 0) * Vector3.forward;
            float xInt = Mathf.Round(dirVec.x);
            float yInt = Mathf.Round(dirVec.z);

            lastLookDirection = new Vector3(xInt, 0, yInt);
        }
    }

    protected void ForceFacePlayer()
    {
        if (playerTransform == null) return;
        Vector3 dir = (playerTransform.position - transform.position).normalized;
        dir.y = 0;
        if (dir.sqrMagnitude <= 0.001f) return;

        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / 45f) * 45f;
        transform.rotation = Quaternion.Euler(0, snapped, 0);

        Vector3 dirVec = Quaternion.Euler(0, snapped, 0) * Vector3.forward;
        float xInt = Mathf.Round(dirVec.x);
        float yInt = Mathf.Round(dirVec.z);
        lastLookDirection = new Vector3(xInt, 0, yInt);
    }

    #endregion

    #region Idle Sound

    private void HandleIdleSound()
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

    private void ResetIdleTimer()
    {
        idleTimer = 0f;
        idleInterval = Random.Range(5f, 9f);
    }

    #endregion

    #region Utility

    protected bool TryGetRandomNavPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 rand = center + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(rand, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    #endregion

    #region Detection Growth

    private void HandleDetectionGrowth()
    {
        currentDetectionTimer += Time.deltaTime;
        if (currentDetectionTimer >= detectionGrowthInterval)
        {
            detectionRadius += detectionGrowthAmount;
            currentDetectionTimer = 0f;
        }
    }

    #endregion
}