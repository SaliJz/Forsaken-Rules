/*
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class MorlockEnemy : MonoBehaviour
{
    #region Enums & Structs

    public enum MorlockLevel { Nivel1, Nivel2, Nivel3 }
    private enum MorlockState { Patrol, Pursue1, Pursue2, Pursue3, Repositioning }

    #endregion

    #region Inspector - References

    [Header("Referencias")]
    [SerializeField] private StaticAnimCtrl visualCtrl;
    [SerializeField] private MorlockStats stats;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    #endregion

    #region Inspector - Difficulty Settings
    [Header("Dificultad de anticipación por disparo")]
    [SerializeField] private MorlockLevel currentLevel = MorlockLevel.Nivel1;
    [SerializeField] private float interceptProbabilityNivel1 = 0.25f;
    [SerializeField] private float interceptProbabilityNivel2 = 0.5f;
    [SerializeField] private float interceptProbabilityNivel3 = 0.75f;
    #endregion

    #region Inspector - Base Stats Settings

    [Header("Estadisticas (fallback si no hay MorlockStats)")]
    [Header("Vida")]
    [SerializeField] private float health = 15f;
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 4f;

    #endregion

    #region Inspector - Combat Settings

    [Header("Combate")]
    [SerializeField] private float fireRate = 1.5f;
    [SerializeField] private bool useRandomFireRate = true;
    [SerializeField] private float minFireRate = 1.5f;
    [SerializeField] private float maxFireRate = 3f;
    [SerializeField] private float projectileDamage = 1f;
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float maxDamageIncrease = 2;
    [SerializeField] private float maxRangeForDamageIncrease = 6f; // distancia a la cual el daño es el máximo
    [SerializeField] private float maxDistanceForDamageStart = 20f; // distancia a partir de la cual el daño es el base
    [SerializeField] private float attackRange = 50f;

    #endregion

    #region Inspector - Patrol Settings

    [Header("Patrullaje")]
    [Tooltip("Si está desactivado, Morlock no volverá a patrullar después de detectar al jugador por primera vez")]
    [SerializeField] private bool canReturnToPatrol = true;
    [SerializeField] private float detectionRadius = 50f;
    [Tooltip("Tiempo en segundos para aumentar el rango de detección si no encuentra al jugador")]
    [SerializeField] private float detectionGrowthInterval = 2.5f;
    [Tooltip("Cantidad en la que aumenta el radio de detección cada intervalo")]
    [SerializeField] private float detectionGrowthAmount = 10f;
    [Tooltip("Distancia máxima de teletransporte durante el patrullaje.")]
    [SerializeField] private float teleportRange = 5f;
    [Tooltip("Si se asignan waypoints, Morlock los recorrerá. Si no, hará patrullaje libre.")]
    [SerializeField] private Transform[] patrolWaypoints;
    [SerializeField] private bool loopWaypoints = true;
    [SerializeField] private int freePatrolIterations = 10;
    [SerializeField] private bool patrolAroundOrigin = true;
    [SerializeField] private float patrolRadius = 8f; // usado si no hay waypoints
    [SerializeField] private float patrolIdleTime = 1.2f; // espera entre puntos
    [SerializeField] private bool canReposition = false;
    [SerializeField] private float repositionTeleportCooldown = 0.75f;

    #endregion

    #region Inspector - Pursuit Settings

    [Header("Perseguir 1")]
    [SerializeField] private float p1_teleportCooldown = 2.5f;
    [SerializeField] private float p1_pursuitAdvanceDistance = 4f;
    [SerializeField] private float p1_pursuitLateralVariationMin = 3f;
    [SerializeField] private float p1_pursuitLateralVariationMax = 5f;

    [Header("Perseguir 2")]
    [Tooltip("Distancia para activar Perseguir2")]
    [SerializeField] private float p2_activationRadius = 5f;
    [SerializeField] private float p2_teleportCooldown = 2.5f;
    [SerializeField] private float p2_teleportRange = 5f;

    #endregion

    #region Inspector - Spawn Settings
    [Header("Configuracion de spawn")]
    [SerializeField] private float spawnDelay = 1.0f; // Tiempo que tarda en reaccionar tras aparecer
    #endregion

    #region Inspector - Audio Settings

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [Header("SFX Generales")]
    [SerializeField] private AudioClip idleSFX;
    [SerializeField] private AudioClip teleportSFX;
    [SerializeField] private AudioClip deathSFX;
    [Header("SFX Combate")]
    [SerializeField] private AudioClip shootSFX;

    #endregion

    #region Inspector - VFX Settings

    [Header("VFX Teletransporte")]
    [SerializeField] private float animTeleportDelay = 1f;
    [SerializeField] private GameObject teleportVFX;
    [SerializeField] private float teleportVFXHeightOffset = 1.5f;
    [SerializeField] private float teleportVFXDurationToDestroy = 1.5f;
    [SerializeField] private float teleportVFXDelay = 1.5f;

    #endregion

    #region Internal State

    private EnemyHealth enemyHealth;
    private NavMeshAgent agent;
    private Transform playerTransform;
    private CharacterController playerCharacterController;
    private MorlockWordLibrary wordLibrary;
    private MorlockState currentState;

    private bool isDead = false;
    private bool isReady = false;
    private Coroutine currentBehaviorCoroutine = null;
    private Coroutine shootCoroutine = null;

    private int currentWaypointIndex = 0;
    private Vector3 originPosition;
    private List<GameObject> activeTeleportVFXs = new List<GameObject>();

    private float idleTimer;
    private float idleInterval;
    private float currentDetectionTimer;
    private float baseDetectionRadius;
    private Vector3 lastLookDirection = Vector3.forward;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (visualCtrl == null) visualCtrl = GetComponentInChildren<StaticAnimCtrl>();
        if (wordLibrary == null) wordLibrary = GetComponent<MorlockWordLibrary>();

        if (enemyHealth == null) ReportDebug("Componente EnemyHealth no encontrado en el enemigo.", 3);
        if (agent == null) ReportDebug("Componente NavMeshAgent no encontrado en el enemigo.", 3);
        if (visualCtrl == null) ReportDebug("Componente StaticAnimCtrl no encontrado en el enemigo.", 2);
    }

    private void Start()
    {
        originPosition = transform.position;
        baseDetectionRadius = detectionRadius;

        var playerGameObject = GameObject.FindGameObjectWithTag("Player");
        if (playerGameObject != null)
        {
            playerTransform = playerGameObject.transform;
            playerCharacterController = playerGameObject.GetComponent<CharacterController>();
        }

        InitializedEnemy();

        StartCoroutine(SpawnRoutine());
        ResetIdleTimer();
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleEnemyDeath;
            enemyHealth.OnDamaged += HandleDamageTaken;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
        }

        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
        }

        StopAllCoroutines();
    }

    private void Update()
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

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float distanceFromOrigin = Vector3.Distance(transform.position, originPosition);

        if (distanceToPlayer > detectionRadius && (currentState != MorlockState.Patrol && currentState != MorlockState.Repositioning))
        {
            if (canReturnToPatrol)
            {
                if (patrolAroundOrigin && distanceFromOrigin > patrolRadius * 1.1f)
                {
                    if (canReposition) ChangeState(MorlockState.Repositioning);
                    else ChangeState(MorlockState.Patrol);
                }
                else ChangeState(MorlockState.Patrol);
            }
            return;
        }

        switch (currentState)
        {
            case MorlockState.Patrol:
                HandleDetectionGrowth();
                if (distanceToPlayer <= detectionRadius)
                {
                    ChangeState(MorlockState.Pursue1);
                }
                break;

            case MorlockState.Pursue1:
                if (distanceToPlayer <= p2_activationRadius)
                {
                    ChangeState(MorlockState.Pursue2);
                }
                break;

            case MorlockState.Pursue2:
                break;
        }
    }

    #endregion

    #region Initialization & Data Sync

    private void InitializedEnemy()
    {
        if (stats != null)
        {
            health = stats.health;
            moveSpeed = stats.moveSpeed;
            attackRange = stats.optimalAttackDistance;
            teleportRange = stats.teleportRange;
            fireRate = stats.fireRate;
            projectileDamage = stats.projectileDamage;
            projectileSpeed = stats.projectileSpeed;
            enemyHealth.SetMaxHealth(stats.health);
        }
        else
        {
            ReportDebug("MorlockStats no asignado. Usando valores por defecto.", 2);
        }

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
        ChangeState(MorlockState.Patrol);
    }

    #endregion

    #region Health & Damage System

    public void ApplyRoomMultiplier(float damageMult, float speedMult)
    {
        projectileDamage *= damageMult;

        moveSpeed *= speedMult;
        if (agent != null)
        {
            agent.speed = moveSpeed;
        }

        ReportDebug($"Multiplicadores de sala aplicados a Morlock. Daño x{damageMult}, Velocidad x{speedMult}. Nueva Velocidad: {moveSpeed:F2}", 1);
    }

    private void HandleDamageTaken()
    {
        // La lógica fue movida a Legacy & Commented Code
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (isDead || enemy != gameObject) return;

        isDead = true;

        if (visualCtrl != null) visualCtrl.PlayDeath();
        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        ChangeState(MorlockState.Repositioning);
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

    /// <summary>
    /// Cambia a un nuevo estado, deteniendo la lógica del estado anterior.
    /// </summary>
    /// <param name="newState"> El estado para actualiza. Si es el mismo, no pasara nada </param>
    private void ChangeState(MorlockState newState)
    {
        if (currentState == newState && currentBehaviorCoroutine != null && currentState != MorlockState.Repositioning) return;

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
            case MorlockState.Patrol:
                detectionRadius = baseDetectionRadius;
                currentDetectionTimer = 0f;
                currentBehaviorCoroutine = StartCoroutine(PatrolRoutine());
                break;
            case MorlockState.Pursue1:
                currentBehaviorCoroutine = StartCoroutine(Pursuit1Routine());
                break;
            case MorlockState.Pursue2:
                currentBehaviorCoroutine = StartCoroutine(Pursuit2Routine());
                break;
            case MorlockState.Repositioning:
                currentBehaviorCoroutine = StartCoroutine(RepositioningRoutine());
                break;
        }
    }

    private IEnumerator PatrolRoutine()
    {
        int freePatrolCount = 0;
        bool shouldContinue = true;

        while (currentState == MorlockState.Patrol && shouldContinue)
        {
            Vector3 targetPosition;
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                targetPosition = patrolWaypoints[currentWaypointIndex].position;
                if (loopWaypoints)
                {
                    currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
                }
                else
                {
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= patrolWaypoints.Length)
                    {
                        shouldContinue = false;
                    }
                }
            }
            else
            {
                if (!loopWaypoints)
                {
                    freePatrolCount++;
                    if (freePatrolCount > freePatrolIterations)
                    {
                        shouldContinue = false;
                    }
                }

                Vector3 center = patrolAroundOrigin ? originPosition : transform.position;
                TryGetRandomPoint(center, patrolRadius, out targetPosition);
            }

            yield return StartCoroutine(TeleportToPositionRoutine(targetPosition));
            yield return new WaitForSeconds(patrolIdleTime);
        }
    }

    private IEnumerator RepositioningRoutine()
    {
        if (originPosition == Vector3.zero) originPosition = transform.position;

        while (currentState == MorlockState.Repositioning)
        {
            float distFromOrigin = Vector3.Distance(transform.position, originPosition);

            if (distFromOrigin <= patrolRadius)
            {
                ChangeState(MorlockState.Patrol);
                yield break;
            }

            Vector3 stepTarget = Vector3.MoveTowards(transform.position, originPosition, teleportRange);

            NavMeshHit stepHit;
            Vector3 finalPos = stepTarget;
            if (NavMesh.SamplePosition(stepTarget, out stepHit, 2.5f, NavMesh.AllAreas))
            {
                finalPos = stepHit.position;
            }

            if (visualCtrl != null) visualCtrl.PlayTPout();
            if (audioSource != null && teleportSFX != null) audioSource.PlayOneShot(teleportSFX);
            SpawnTeleportVFX(transform.position);

            yield return new WaitForSeconds(0.4f);

            transform.position = finalPos;
            if (agent != null && agent.enabled) agent.Warp(finalPos);

            if (visualCtrl != null)
            {
                visualCtrl.restoreOriginalMaterials();
                visualCtrl.PlayTPin();
            }
            SpawnTeleportVFX(finalPos);

            yield return new WaitForSeconds(repositionTeleportCooldown);
        }
    }

    private IEnumerator Pursuit1Routine()
    {
        while (currentState == MorlockState.Pursue1)
        {
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 advancePosition = transform.position + directionToPlayer * p1_pursuitAdvanceDistance;

            Vector3 lateralDirection = Vector3.Cross(directionToPlayer, Vector3.up);
            float lateralOffset = Random.Range(p1_pursuitLateralVariationMin, p1_pursuitLateralVariationMax) * (Random.value > 0.5f ? 1f : -1f);

            Vector3 targetPosition = advancePosition + lateralDirection * lateralOffset;

            yield return StartCoroutine(TeleportToPositionRoutine(targetPosition));

            StartShootCoroutine();

            yield return new WaitForSeconds(p1_teleportCooldown);
        }
    }

    private IEnumerator Pursuit2Routine()
    {
        // Guardar la última posición para intentar no repetirla inmediatamente si hay otras opciones
        Vector3 lastTargetPos = Vector3.zero;

        while (currentState == MorlockState.Pursue2)
        {
            // Lista temporal para candidatos válidos
            System.Collections.Generic.List<Vector3> validCandidates = new System.Collections.Generic.List<Vector3>();

            float[] angles = { 0f, 90f, 180f, 270f };

            // Iterar por los 4 ángulos y validar
            for (int i = 0; i < angles.Length; i++)
            {
                // Calcular la posición ideal teórica
                Vector3 offset = Quaternion.Euler(0, angles[i], 0) * Vector3.forward * p2_teleportRange;
                Vector3 potentialPos = playerTransform.position + offset;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(potentialPos, out hit, 2.0f, NavMesh.AllAreas))
                {
                    validCandidates.Add(hit.position);
                }
            }

            // Seleccionar el punto
            if (validCandidates.Count > 0)
            {
                Vector3 selectedPos;

                // Si hay más de 1 opción y tiene una posición anterior, tratamos de no repetir la misma
                if (validCandidates.Count > 1 && lastTargetPos != Vector3.zero)
                {
                    // Buscar candidatos que estén lejos de la última posición usada
                    var freshCandidates = validCandidates.FindAll(p => Vector3.Distance(p, lastTargetPos) > 1.0f);

                    if (freshCandidates.Count > 0) selectedPos = freshCandidates[Random.Range(0, freshCandidates.Count)];
                    else selectedPos = validCandidates[Random.Range(0, validCandidates.Count)];
                }
                else
                {
                    // Solo hay una opción o es la primera vez
                    selectedPos = validCandidates[Random.Range(0, validCandidates.Count)];
                }

                lastTargetPos = selectedPos;

                yield return StartCoroutine(TeleportToPositionRoutine(selectedPos));

                StartShootCoroutine();
            }
            else
            {
                ReportDebug("Pursue2: Ningún punto cumple con la distancia/navmesh requerida. Esperandondo.", 2);
            }

            yield return new WaitForSeconds(p2_teleportCooldown);
        }
    }

    #endregion

    #region Movement & Navigation

    private void HandleDetectionGrowth()
    {
        currentDetectionTimer += Time.deltaTime;

        if (currentDetectionTimer >= detectionGrowthInterval)
        {
            detectionRadius += detectionGrowthAmount;
            currentDetectionTimer = 0f;

            ReportDebug($"Rango de detección aumentado. Nuevo radio: {detectionRadius}", 1);
        }
    }

    /// <summary>
    /// Maneja la rotación discreta (8 direcciones) y actualiza el Animator.
    /// </summary>
    private void UpdateAnimationAndRotation()
    {
        Vector3 targetDirection = Vector3.zero;

        if (currentState == MorlockState.Pursue1 || currentState == MorlockState.Pursue2)
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

    private void ForceFacePlayer()
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

    private IEnumerator TeleportToPositionRoutine(Vector3 targetPosition)
    {
        if (enemyHealth != null && enemyHealth.IsStunned) yield break;

        NavMeshHit hit;
        Vector3 finalDestination = targetPosition;
        if (NavMesh.SamplePosition(targetPosition, out hit, 10f, NavMesh.AllAreas))
        {
            finalDestination = hit.position;
        }
        else yield break;

        // 1. Iniciar desaparición
        if (visualCtrl != null) visualCtrl.PlayTPout();
        SpawnTeleportVFX(transform.position);

        // Tiempo mínimo para que el material de TP se vea antes de moverlo
        yield return new WaitForSeconds(0.35f);

        if (isDead || (enemyHealth != null && enemyHealth.IsStunned)) yield break;

        if (audioSource != null && teleportSFX != null) audioSource.PlayOneShot(teleportSFX);

        // 2. Mover instantáneamente
        transform.position = finalDestination;
        if (agent != null && agent.enabled) agent.Warp(finalDestination);

        // 3. Reaparecer de inmediato
        if (visualCtrl != null)
        {
            visualCtrl.restoreOriginalMaterials();
            visualCtrl.PlayTPin();
        }

        SpawnTeleportVFX(finalDestination);

        if (playerTransform != null && currentState != MorlockState.Patrol)
        {
            ForceFacePlayer();
        }
    }

    /// <summary>
    /// Intenta obtener un punto válido sobre el NavMesh dentro de radio.
    /// </summary>
    /// <param name="center">Centro del círculo de búsqueda.</param>
    /// <param name="radius">Radio de búsqueda.</param>
    /// <param name="result">Punto encontrado (si retorna true).</param>
    /// <returns>True si se encontró un punto válido.</returns>
    private bool TryGetRandomPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 rand = center + Random.insideUnitSphere * radius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(rand, out hit, 2.0f, NavMesh.AllAreas))
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

    private void StartShootCoroutine()
    {
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
        }
        shootCoroutine = StartCoroutine(ShootAfterDelayRoutine());
    }

    private IEnumerator ShootAfterDelayRoutine()
    {
        if (useRandomFireRate)
            fireRate = Random.Range(minFireRate, maxFireRate);

        yield return new WaitForSeconds(fireRate);

        if (!isDead && currentState != MorlockState.Patrol && currentState != MorlockState.Repositioning)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer <= attackRange)
            {
                ForceFacePlayer();

                // Ejecutar animación una sola vez
                if (visualCtrl != null) visualCtrl.PlayShoot();

                // Esperar al frame de la animación donde "suelta" el proyectil
                // Ajusta 'animTeleportDelay' en el inspector (ej. 0.5s)
                yield return new WaitForSeconds(animTeleportDelay);

                ExecuteProjectileSpawn();
            }
        }
        shootCoroutine = null;
    }

    private void ExecuteProjectileSpawn()
    {
        if (enemyHealth != null && enemyHealth.IsStunned || isDead) return;

        if (audioSource != null && shootSFX != null) audioSource.PlayOneShot(shootSFX);

        Vector3 aimPoint = playerTransform.position;
        float interceptProb = GetInterceptProbability(currentLevel);

        if (playerCharacterController != null && Random.value < interceptProb) // Disparo interceptivo
        {
            aimPoint = CalculateInterceptPoint(playerTransform.position, playerCharacterController.velocity);
        }
        else
        {
            aimPoint = playerTransform.position;
        }

        Vector3 directionToAim = (aimPoint - firePoint.position).normalized;
        firePoint.rotation = Quaternion.LookRotation(directionToAim);

        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        MorlockProjectile projectile = projectileObj.GetComponent<MorlockProjectile>();

        if (projectile != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            float calculatedDamage = CalculateDamageByDistance(distanceToPlayer);
            string selectedWord = wordLibrary != null ? wordLibrary.GetRandomWord() : "MORLOCK";

            // Inicializar con la palabra obtenida
            projectile.Initialize(projectileSpeed, calculatedDamage, selectedWord);
        }
    }

    public void Shoot()
    {
        if (enemyHealth != null && enemyHealth.IsStunned || isDead) return;

        if (audioSource != null && shootSFX != null) audioSource.PlayOneShot(shootSFX);

        Vector3 aimPoint = playerTransform.position;
        float interceptProb = GetInterceptProbability(currentLevel);

        if (playerCharacterController != null && Random.value < interceptProb) // Disparo interceptivo
        {
            aimPoint = CalculateInterceptPoint(playerTransform.position, playerCharacterController.velocity);
        }
        else
        {
            aimPoint = playerTransform.position;
        }

        Vector3 directionToAim = (aimPoint - firePoint.position).normalized;
        firePoint.rotation = Quaternion.LookRotation(directionToAim);

        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        MorlockProjectile projectile = projectileObj.GetComponent<MorlockProjectile>();

        if (projectile != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            float calculatedDamage = CalculateDamageByDistance(distanceToPlayer);
            string selectedWord = wordLibrary != null ? wordLibrary.GetRandomWord() : string.Empty;
            projectile.Initialize(projectileSpeed, calculatedDamage, selectedWord);
        }
    }

    /// <summary>
    /// Calcula el daño del proyectil basado en la distancia al jugador.
    /// </summary>
    private float CalculateDamageByDistance(float distance)
    {
        if (distance <= maxRangeForDamageIncrease) return maxDamageIncrease;
        if (distance >= maxDistanceForDamageStart) return projectileDamage;

        float t = (distance - maxRangeForDamageIncrease) / (maxDistanceForDamageStart - maxRangeForDamageIncrease);
        return Mathf.Lerp(maxDamageIncrease, projectileDamage, t);
    }

    private float GetInterceptProbability(MorlockLevel level)
    {
        switch (level)
        {
            case MorlockLevel.Nivel1:
                return interceptProbabilityNivel1;
            case MorlockLevel.Nivel2:
                return interceptProbabilityNivel2;
            case MorlockLevel.Nivel3:
                return interceptProbabilityNivel3;
            default:
                return 0.0f;
        }
    }

    /// <summary>
    /// Calcula el punto de intercepción para un disparo, prediciendo el movimiento del objetivo.
    /// </summary>
    private Vector3 CalculateInterceptPoint(Vector3 targetPosition, Vector3 targetVelocity)
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

    #region Visual & Audio Effects

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
        // Intervalo aleatorio entre 5 y 9 segundos para cuando esté quieto
        idleInterval = Random.Range(5f, 9f);
    }

    private void SpawnTeleportVFX(Vector3 basePosition)
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

    private IEnumerator RemoveVFXFromListAfterDelay(GameObject vfx, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (vfx != null)
        {
            if (activeTeleportVFXs.Contains(vfx))
            {
                activeTeleportVFXs.Remove(vfx);
            }
            Destroy(vfx);
        }
    }

    #endregion

    #region Debugging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Función de depuración para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <<param name="message">Mensaje a reportar.</param> >
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[MorlockEnemy] {message}");
                break;
            case 2:
                Debug.LogWarning($"[MorlockEnemy] {message}");
                break;
            case 3:
                Debug.LogError($"[MorlockEnemy] {message}");
                break;
            default:
                Debug.Log($"[MorlockEnemy] {message}");
                break;
        }
    }

    #endregion

    #region Legacy & Commented Code

    //[SerializeField] private bool useAnimationEvent = false;

    /* Extraído originalmente de HandleDamageTaken:
    //if (!isDead && currentState == MorlockState.Pursue2)
    //{
    //    ChangeState(MorlockState.Pursue3);
    //}
    

    #endregion
}
*/