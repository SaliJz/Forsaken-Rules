using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Jefe 2 - BloodKnight / The Ones Who Shattered.
/// </summary>
public class BloodKnightBoss : MonoBehaviour, IDamageBlocker, IAnimEventHandler
{
    #region Enums

    private enum BossState
    {
        Idle,
        Patrol,
        Chase,
        Review,
        StaticFailureWindup,
        StaticFailureRelease,
        BrokenCharge,
        DivisoryFailure,
        ScrapRam,
        DrownedHands,
        Stunned,
        Dead
    }

    #endregion

    #region Inspector - References

    [Header("Referencias")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private Transform mineDropPoint;
    [SerializeField] private CinemachineCamera vcam;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioSource ambientAudioSource;

    #endregion

    #region Inspector - Stats Base

    [Header("Stats base")]
    [SerializeField] private float maxHealth = 500f;
    [SerializeField] private float baseMoveSpeed = 7f;
    [SerializeField] private float aggroRange = 20f;
    [SerializeField] private float angurSpeed = 720f;
    [SerializeField] private float stoppingDistance = 2.5f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask obstacleLayers;

    #endregion

    #region Inspector - Falla Estatica

    [Header("Falla Estatica")]
    [SerializeField] private float staticFailureDamage = 35f;
    [Tooltip("Tiempo de canalización antes de ejecutar la animación de ataque.")]
    [SerializeField] private float staticFailureWindup = 2f;
    [SerializeField] private float staticFailureCooldown = 20f;
    [SerializeField] private float staticFailureAoERadius = 4.5f;

    #endregion

    #region Inspector - Carga de los Quebrados

    [Header("Carga de los Quebrados")]
    [SerializeField] private int brokenChargeDashCount = 10;
    [SerializeField] private float brokenChargeDashSpeed = 13.5f;
    [SerializeField] private float brokenChargeHitDamage = 7f;
    [SerializeField] private float brokenChargeCooldown = 14f;
    [SerializeField] private float zigzagSideOffset = 3.5f;
    [SerializeField] private float dashHitRadius = 1.35f;
    [SerializeField] private float brokenChargePause = 0.7f;
    [SerializeField] private float brokenChargeDurationDash = 0.7f;

    #endregion

    #region Inspector - Embestida de Chatarra

    [Header("Embestida de Chatarra")]
    [SerializeField] private float scrapRamSpeed = 14f;
    [SerializeField] private float scrapRamDuration = 2.2f;
    [SerializeField] private float scrapRamBossStun = 2f;
    [SerializeField] private float scrapRamDamage = 15f;
    [SerializeField] private float scrapRamKnockbackForce = 15f;
    [SerializeField] private int scrapRamMineBurst = 5;
    [SerializeField] private float scrapRamActiveDist = 15f;
    [SerializeField] private float scrapRamNoDamageWindow = 10f;
    [Tooltip("Prefab del indicador proyectado en el suelo para previsualizar la embestida Scrap Ram.")]
    [SerializeField] private GameObject scrapRamWarningPrefab;

    #endregion

    #region Inspector - Falla Divisoria

    [Header("Falla Divisoria")]
    [SerializeField] private float divisoryCooldown = 13f;
    [SerializeField] private float divisoryTriggerDist = 15f;
    [SerializeField] private float divisoryWallDuration = 5f;
    [SerializeField] private float divisoryContactDmg = 2.5f;
    [SerializeField] private float divisoryInstantDmg = 12.5f;
    [Tooltip("Tiempo de canalización antes de ejecutar la animación de ataque.")]
    [SerializeField] private float divisoryCastTime = 0.8f;
    [SerializeField] private float divisoryWallThickness = 1.2f;
    [SerializeField] private float divisoryWallHeight = 3.5f;
    [SerializeField] private float divisoryRayMaxLength = 40f;
    [Tooltip("Velocidad de reposicionamiento al centro de la sala antes del golpe.")]
    [SerializeField] private float divisoryRepositionSpeed = 9f;
    [Tooltip("Radio NavMesh de muestreo para el centro de sala.")]
    [SerializeField] private float divisoryNavSampleRadius = 3f;
    [Tooltip("Distancia al punto central a partir de la cual se considera 'llegado'.")]
    [SerializeField] private float divisoryArrivalThreshold = 0.6f;
    #endregion

    #region Inspector - Manos de los Ahogados

    [Header("Manos de los Ahogados")]
    [SerializeField] private float drownedDamage = 15f;
    [SerializeField] private float drownedRange = 15f;
    [SerializeField] private int drownedHitCount = 3;
    [SerializeField] private float drownedInterval = 0.9f;
    [SerializeField] private float drownedCooldown = 8f;
    [SerializeField, Range(0f, 1f)] private float predictiveChance = 0.75f;
    [SerializeField] private int predictiveDashThreshold = 15;
    [SerializeField] private float predictiveLeadDist = 2.75f;

    #endregion

    #region Inspector - Prefabs

    [Header("Prefabs")]
    [SerializeField] private BossExplosiveMine bossMinePrefab;
    [SerializeField] private CrystalTrail crystalTrailPrefab;
    [SerializeField] private SolidLightWall solidLightWallPrefab;
    [SerializeField] private SoulHand soulHandPrefab;
    [SerializeField] private GameObject staticFailureWarningPrefab;
     [SerializeField] private GameObject staticFailureVFX;
    [SerializeField] private GameObject frontBlockVFXPrefab;
    [SerializeField] private GameObject impactStunVFXPrefab;

    #endregion

    #region Inspector - Audio

    [Header("Audio")]
    [SerializeField] private AudioClip staticChargeSFX;
    [SerializeField] private AudioClip staticImpactSFX;
    [SerializeField] private AudioClip shieldBlockSFX;
    [SerializeField] private AudioClip dashSFX;
    [SerializeField] private AudioClip divisorySFX;
    [SerializeField] private AudioClip drownedHandsSFX;
    [SerializeField] private AudioClip scrapRamSFX;
    [SerializeField] private AudioClip stunSFX;
    [SerializeField] private AudioClip deathSFX;

    #endregion

    #region Inspector - Telegraphed Timings & Feedback

    [Header("Hit Stun y Anticipación")]
    [SerializeField] private KnightAnimCtrl knightAnimCtrl;
    [SerializeField] private EnemyVisualEffects enemyVisualEffects;

    [SerializeField] private float hitStunDuration = 0.3f;
    [SerializeField] private float forceIdleDuration = 0.8f;
    [SerializeField] private float anticipationPauseDuration = 0.5f;

    [Header("SFX Hit Stun")]
    [SerializeField] private AudioClip hitStunSFX;

    [Header("SFX Anticipación y Daño")]
    [Tooltip("SFX genérico de anticipación, usado como respaldo si un ataque no define el suyo.")]
    [SerializeField] private AudioClip anticipationSFX;
    [SerializeField] private float anticipationSFXPitch = 1.0f;

    [Header("SFX Anticipación por Ataque")]
    [Tooltip("Si está vacío, se usa el SFX genérico de Anticipación de arriba.")]
    [SerializeField] private AudioClip staticFailureAnticipationSFX;
    [SerializeField] private AudioClip brokenChargeAnticipationSFX;
    [SerializeField] private AudioClip scrapRamAnticipationSFX;
    [SerializeField] private AudioClip divisoryAnticipationSFX;
    [SerializeField] private AudioClip drownedHandsAnticipationSFX;

    #endregion

    #region Inspector - Debug

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = false;

    #endregion

    #region Internal State

    private static readonly int AnimAttackEnded = Animator.StringToHash("AttackEnded");

    private BossState state = BossState.Idle;
    private float currentHealth;
    private bool isDead;

    private float nextStaticTime;
    private float nextBrokenChargeTime;
    private float nextDrownedTime;
    private float nextDivisoryTime;

    private bool interruptionActive;

    private bool attackExecuteTriggered = false;

    private bool staticShieldActive =>
        state == BossState.StaticFailureWindup && !isDead;

    private float lastDamageInRangeTime;

    private int playerDashCount;

    private Vector3 lastPlayerPos;
    private Vector3 playerVelocity;

    private readonly List<GameObject> spawnedObjects = new();

    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;

    private CinemachineBasicMultiChannelPerlin camNoise;

    private int groundLayerMask;

    private Vector3 debugDashSpherePos;
    private bool debugDashSphereActive;

    private Coroutine anticipationCoroutine = null;
    private Coroutine hitStunCoroutine = null;
    private bool isInAnticipation = false;
    private bool isInHitStun = false;
    private float hitStunRecoveryCooldown = 0f;
    private AudioClip pendingAnticipationSFX;

    private int actionToken = 0;

    private Vector3 cachedRoomCenter;
    private bool roomCenterCached = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        groundLayerMask = LayerMask.GetMask("Ground");
    }

    private void Start()
    {
        currentHealth = maxHealth;
        lastDamageInRangeTime = Time.time;
        lastPlayerPos = player != null ? player.position : transform.position;

        nextStaticTime = Time.time + 2f;
        nextBrokenChargeTime = Time.time + 6f;
        nextDrownedTime = Time.time + 4f;
        nextDivisoryTime = Time.time + 5f;

        StartCoroutine(MainBrainRoutine());
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged += HandleHealthChanged;
            enemyHealth.OnDeath += HandleDeath;
            enemyHealth.OnDamaged += HandleDamageTaken;
        }

        if (playerMovement != null)
        {
            PlayerMovement.OnDashPerformed += RegisterPlayerDash;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged -= HandleHealthChanged;
            enemyHealth.OnDeath -= HandleDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
        }

        if (playerMovement != null)
        {
            PlayerMovement.OnDashPerformed -= RegisterPlayerDash;
        }
    }

    private void Update()
    {
        if (isDead || player == null) return;

        if (hitStunRecoveryCooldown > 0f)
        {
            hitStunRecoveryCooldown -= Time.deltaTime;
        }

        TrackPlayerVelocity();
        CheckDynamicInterruptions();
    }

    #endregion

    #region Initialization & Data Sync

    private void CacheComponents()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (knightAnimCtrl == null) knightAnimCtrl = GetComponentInChildren<KnightAnimCtrl>();
        if (enemyVisualEffects == null) enemyVisualEffects = GetComponent<EnemyVisualEffects>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                playerHealth = playerObj.GetComponent<PlayerHealth>();
                playerMovement = playerObj.GetComponent<PlayerMovement>();
            }
        }

        if (vcam == null) vcam = Object.FindFirstObjectByType<CinemachineCamera>();
        if (vcam != null)
        {
            camNoise = vcam.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;
        }

        if (enemyHealth != null) enemyHealth.SetMaxHealth(maxHealth);

        if (agent != null)
        {
            agent.speed = baseMoveSpeed;
            agent.angularSpeed = angurSpeed;
            agent.stoppingDistance = stoppingDistance;
        }

        if (attackOrigin == null) attackOrigin = transform;
        if (mineDropPoint == null) mineDropPoint = transform;
    }

    #endregion

    #region Core Health & Events

    private void HandleHealthChanged(float current, float max)
    {
        currentHealth = current;
        maxHealth = max;

        if (player != null && Vector3.Distance(transform.position, player.position) <= scrapRamActiveDist)
        {
            lastDamageInRangeTime = Time.time;
        }
    }

    private void HandleDeath(GameObject go)
    {
        if (go != gameObject || isDead) return;
        isDead = true;
        state = BossState.Dead;

        CancelAnticipation();

        if (knightAnimCtrl != null) knightAnimCtrl.SetAttackEnded(true);

        StopAllCoroutines();
        CleanupSpawnedObjects();

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        PlaySFX(deathSFX);
        if (knightAnimCtrl != null) knightAnimCtrl.PlayDeath();
    }

    #endregion

    #region Boss Brain & Behaviours

    private IEnumerator MainBrainRoutine()
    {
        yield return new WaitForSeconds(1f);

        while (!isDead)
        {
            while (interruptionActive) yield return null;

            if (!PlayerInAggroRange())
            {
                state = BossState.Patrol;
                StopAgent();
                roomCenterCached = false;
                yield return null;
                continue;
            }

            if (!roomCenterCached)
            {
                cachedRoomCenter = ComputeRoomCenter();
                roomCenterCached = true;
            }

            state = BossState.Review;

            if (Time.time >= nextStaticTime)
            {
                yield return ExecuteStaticFailureRoutine();
                if (isDead || interruptionActive) continue;

                if (Time.time >= nextBrokenChargeTime)
                {
                    yield return ExecuteBrokenChargeRoutine();
                }
                continue;
            }

            //nextDrownedTime = Mathf.Min(nextDrownedTime, Time.time + 3f);
            yield return ChaseAndPokeRoutine();
        }
    }

    private IEnumerator ChaseAndPokeRoutine()
    {
        state = BossState.Chase;

        while (!isDead && !interruptionActive)
        {
            if (player == null) yield break;

            float dist = Vector3.Distance(transform.position, player.position);
            MoveTowards(player.position, baseMoveSpeed * 0.7f);
            FacePlayer(10f);

            if (dist <= drownedRange && Time.time >= nextDrownedTime)
            {
                StopAgent();
                yield return ExecuteDrownedHandsRoutine(usePredictive: false);
                nextDrownedTime = Time.time + drownedCooldown;
                yield break;
            }

            if (dist <= agent.stoppingDistance)
            {
                StopAgent();
            }
            else if (dist <= drownedRange)
            {
                MoveTowards(player.position, baseMoveSpeed * 1.1f);
            }
            else
            {
                MoveTowards(player.position, baseMoveSpeed * 0.7f);
            }

            if (Time.time >= nextStaticTime)
            {
                StopAgent();
                yield break;
            }

            yield return null;
        }

        StopAgent();
    }

    private void CheckDynamicInterruptions()
    {
        if (isDead || interruptionActive || player == null) return;

        if (state == BossState.StaticFailureWindup || state == BossState.StaticFailureRelease ||
            state == BossState.BrokenCharge || state == BossState.ScrapRam ||
            state == BossState.DivisoryFailure || state == BossState.DrownedHands ||
            state == BossState.Stunned)
        {
            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > divisoryTriggerDist && Time.time >= nextDivisoryTime)
        {
            interruptionActive = true;
            StartCoroutine(ExecuteDivisoryFailureRoutine());
            return;
        }

        if (dist <= scrapRamActiveDist && (Time.time - lastDamageInRangeTime) >= scrapRamNoDamageWindow)
        {
            interruptionActive = true;
            lastDamageInRangeTime = Time.time;
            StartCoroutine(ExecuteScrapRamRoutine());
            return;
        }

        if (playerDashCount >= predictiveDashThreshold && dist <= drownedRange && Time.time >= nextDrownedTime && state == BossState.Chase)
        {
            interruptionActive = true;
            nextDrownedTime = Time.time + drownedCooldown;
            StartCoroutine(ExecuteDrownedHandsRoutine(usePredictive: true));
        }
    }

    #endregion

    #region Attack - Falla Estatica

    private IEnumerator ExecuteStaticFailureRoutine()
    {
        int myToken = ++actionToken;
        interruptionActive = true;
        state = BossState.StaticFailureWindup;
        nextStaticTime = Time.time + staticFailureCooldown;

        StopAgent();
        FacePlayerInstant();

        GameObject warning = SpawnWarningIndicator(
            attackOrigin.position,
            staticFailureAoERadius * 2f,
            staticFailureWarningPrefab
        );

        pendingAnticipationSFX = staticFailureAnticipationSFX;
        PlaySFX(staticChargeSFX);

        yield return WaitRespectingAnticipation(staticFailureWindup);

        if (isDead || state != BossState.StaticFailureWindup || myToken != actionToken)
        {
            if (warning != null) Destroy(warning);
            if (myToken == actionToken) interruptionActive = false;
            yield break;
        }

        attackExecuteTriggered = false;
        if (knightAnimCtrl != null) knightAnimCtrl.PlayStaticFailure();

        try
        {
            yield return new WaitUntil(() => attackExecuteTriggered || isDead || state != BossState.StaticFailureWindup || myToken != actionToken);

            if (warning != null) Destroy(warning);

            if (isDead || state != BossState.StaticFailureWindup || myToken != actionToken) yield break;

            state = BossState.StaticFailureRelease;

            DealAoEDamage(attackOrigin.position, staticFailureAoERadius, staticFailureDamage);
            SpawnStaticImpactVFX(attackOrigin.position, staticFailureAoERadius);
            PlaySFX(staticImpactSFX);
            ShakeCamera(2f, 0.25f);

            animator?.SetBool(AnimAttackEnded, true);
            yield return new WaitForSeconds(0.4f);
        }
        finally
        {
            if (knightAnimCtrl != null) knightAnimCtrl.SetAttackEnded(true);
            if (warning != null) Destroy(warning);

            if (myToken == actionToken)
            {
                if (state == BossState.StaticFailureRelease || state == BossState.StaticFailureWindup)
                {
                    state = BossState.Idle;
                }
                interruptionActive = false;
            }
        }
    }

    public bool ShouldBlockDamage(Vector3 attackerPosition)
    {
        if (!staticShieldActive) return false;

        if (knightAnimCtrl != null)
        {
            knightAnimCtrl.PlayInvulnerabilityVFX();
        }

        TriggerFrontShieldBlock(attackerPosition);
        return true;
    }

    private void TriggerFrontShieldBlock(Vector3 attackerPos)
    {
        PlaySFX(shieldBlockSFX);
        if (frontBlockVFXPrefab == null) return;

        Vector3 spawnPos = transform.position + (attackerPos - transform.position).normalized * 1.2f + Vector3.up;
        var vfx = Instantiate(frontBlockVFXPrefab, spawnPos, Quaternion.identity);
        spawnedObjects.Add(vfx);
        Destroy(vfx, 1.5f);
    }

     private void SpawnStaticImpactVFX(Vector3 pos, float radius)
    {
        if (staticFailureVFX == null) return;

        var vfx = Instantiate(staticFailureVFX, pos, Quaternion.identity);

        ParticleSystem ps = staticFailureVFX.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var shape = ps.shape;
            shape.radius = radius;
        }

        spawnedObjects.Add(vfx);
    }

    #endregion

    #region Attack - Carga de los Quebrados

    private IEnumerator ExecuteBrokenChargeRoutine()
    {
        int myToken = ++actionToken;
        interruptionActive = true;
        state = BossState.BrokenCharge;
        nextBrokenChargeTime = Time.time + brokenChargeCooldown;

        float timePerDash = brokenChargeDurationDash;

        for (int i = 0; i < brokenChargeDashCount; i++)
        {
            if (isDead || state != BossState.BrokenCharge || myToken != actionToken) break;

            FacePlayerInstant();

            attackExecuteTriggered = false;
            pendingAnticipationSFX = brokenChargeAnticipationSFX;

            if (knightAnimCtrl != null) knightAnimCtrl.PlayBrokenCharge();

            SpawnBossMine(mineDropPoint.position);
            Vector3 target = ComputeZigzagTarget(i);
            PlaySFX(dashSFX);

            Coroutine dashCoroutine = StartCoroutine(DashRoutine(target, timePerDash, brokenChargeDashSpeed, dealContactDamage: false, spawnCrystalOnWallHit: true));

            if (myToken != actionToken) break;

            yield return new WaitUntil(() => attackExecuteTriggered || isDead || state != BossState.BrokenCharge || myToken != actionToken);

            if (isDead || state != BossState.BrokenCharge || myToken != actionToken) break;

            if (attackExecuteTriggered)
            {
                bool hit = TryDealBrokenChargeDamage(attackOrigin.position, dashHitRadius);

                if (!hit && player != null)
                {
                    float closeRangeDist = dashHitRadius;
                    if (Vector3.Distance(transform.position, player.position) <= closeRangeDist)
                    {
                        TryDealBrokenChargeDamage(attackOrigin.position, dashHitRadius);
                    }
                }
            }

            if (dashCoroutine != null)
            {
                yield return dashCoroutine;
            }

            yield return new WaitForSeconds(brokenChargePause);
        }

        if (myToken == actionToken)
        {
            StopAgent();

            if (state == BossState.BrokenCharge)
            {
                state = BossState.Idle;
            }
            interruptionActive = false;
        }
    }

    private Vector3 ComputeZigzagTarget(int dashIndex)
    {
        if (player == null) return transform.position;

        Vector3 toPlayer = (player.position - transform.position).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, toPlayer).normalized;
        float sign = (dashIndex % 2 == 0) ? 1f : -1f;
        Vector3 target = player.position + side * zigzagSideOffset * sign;
        target.y = transform.position.y;

        return NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            ? hit.position
            : target;
    }

    #endregion

    #region Attack - Embestida de Chatarra

    private IEnumerator ExecuteScrapRamRoutine()
    {
        int myToken = ++actionToken;
        interruptionActive = true;
        if (isDead) yield break;

        state = BossState.ScrapRam;
        StopAgent();
        FacePlayerInstant();

        attackExecuteTriggered = false;
        pendingAnticipationSFX = scrapRamAnticipationSFX;
        if (knightAnimCtrl != null) knightAnimCtrl.PlayScrapRam();

        GameObject warningTrail = null;
        if (scrapRamWarningPrefab != null)
        {
            float maxDashDistance = scrapRamSpeed * scrapRamDuration;
            Vector3 dashDir = transform.forward;
            Vector3 warningPosition = GetGroundPosition(transform.position + dashDir * (maxDashDistance * 0.5f));

            warningTrail = Instantiate(scrapRamWarningPrefab, warningPosition, Quaternion.LookRotation(dashDir));
            warningTrail.transform.localScale = new Vector3(5f * 2f, 0.05f, maxDashDistance);
            spawnedObjects.Add(warningTrail);
        }

        yield return new WaitUntil(() => attackExecuteTriggered || isDead || state != BossState.ScrapRam || myToken != actionToken);

        if (warningTrail != null)
        {
            spawnedObjects.Remove(warningTrail);
            Destroy(warningTrail);
        }

        if (isDead || state != BossState.ScrapRam || myToken != actionToken)
        {
            if (myToken == actionToken) interruptionActive = false;
            yield break;
        }

        FacePlayerInstant();
        PlaySFX(scrapRamSFX);

        float elapsed = 0f;
        bool hitPlayer = false;
        bool hitWall = false;
        Vector3 wallHitPos = Vector3.zero;
        GameObject hitPlayerTarget = null;

        while (elapsed < scrapRamDuration)
        {
            if (state != BossState.ScrapRam || myToken != actionToken || isInHitStun) break;

            transform.position += transform.forward * scrapRamSpeed * Time.deltaTime;

            Vector3 ramHitCenter = attackOrigin != null 
                ? attackOrigin.position
                : transform.position + transform.forward * 0.8f;

            Collider[] playerHits = Physics.OverlapSphere(ramHitCenter, 1.2f, playerLayer);

            foreach (var c in playerHits)
            {
                if (!c.CompareTag("Player")) continue;
                hitPlayer = true;
                hitPlayerTarget = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
                break;
            }

            if (hitPlayer) break;

            if (Physics.SphereCast(transform.position + Vector3.up * 0.75f,
                0.6f, transform.forward, out RaycastHit wallHit, 0.9f, obstacleLayers))
            {
                hitWall = true;
                wallHitPos = wallHit.point;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (myToken != actionToken) yield break;

        if (hitPlayer && hitPlayerTarget != null)
        {
            PlayerHealth ph = hitPlayerTarget.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(scrapRamDamage);
            }

            ApplyKnockback(hitPlayerTarget.transform, scrapRamKnockbackForce);
        }
        else if (hitWall)
        {
            SpawnImpactStunVFX(wallHitPos);
            PlaySFX(stunSFX);

            for (int i = 0; i < scrapRamMineBurst; i++)
            {
                Vector2 rnd = Random.insideUnitCircle * 2f;
                SpawnBossMine(transform.position + new Vector3(rnd.x, 0f, rnd.y));
            }

            yield return new WaitForSeconds(scrapRamBossStun);
        }
        else
        {
            yield return new WaitForSeconds(0.25f);
        }

        if (myToken == actionToken)
        {
            if (state == BossState.ScrapRam)
            {
                state = BossState.Idle;
            }
            interruptionActive = false;
        }
    }

    #endregion

    #region Attack - Falla Divisoria

    private IEnumerator ExecuteDivisoryFailureRoutine()
    {
        int myToken = ++actionToken;
        interruptionActive = true;
        if (isDead) yield break;

        state = BossState.DivisoryFailure;
        nextDivisoryTime = Time.time + divisoryCooldown;

        Vector3 roomCenter = roomCenterCached ? cachedRoomCenter : ComputeRoomCenter();
        yield return RepositionToCenterRoutine(roomCenter, myToken);

        if (isDead || state != BossState.DivisoryFailure || myToken != actionToken)
        {
            if (myToken == actionToken) interruptionActive = false;
            yield break;
        }

        StopAgent();
        FacePlayerInstant();

        pendingAnticipationSFX = divisoryAnticipationSFX;
        PlaySFX(divisorySFX);

        yield return WaitRespectingAnticipation(divisoryCastTime);

        if (isDead || state != BossState.DivisoryFailure || myToken != actionToken)
        {
            if (myToken == actionToken) interruptionActive = false;
            yield break;
        }

        attackExecuteTriggered = false;
        if (knightAnimCtrl != null) knightAnimCtrl.PlayHandAttack();

        yield return new WaitUntil(() => attackExecuteTriggered || isDead || state != BossState.DivisoryFailure || myToken != actionToken);

        if (isDead || state != BossState.DivisoryFailure || myToken != actionToken)
        {
            if (myToken == actionToken) interruptionActive = false;
            yield break;
        }

        if (solidLightWallPrefab != null && TryComputeDivisoryWall(roomCenter, out Vector3 center, out Quaternion rot, out float length))
        {
            SolidLightWall wall = Instantiate(solidLightWallPrefab, center, rot);
            wall.transform.localScale = new Vector3(divisoryWallThickness, divisoryWallHeight, length);
            wall.ContactDamagePerSecond = divisoryContactDmg;
            wall.InstantDamageOnSpawn = divisoryInstantDmg;
            wall.WallLifetime = divisoryWallDuration;
            spawnedObjects.Add(wall.gameObject);
        }

        yield return new WaitForSeconds(0.2f);

        if (myToken == actionToken)
        {
            if (state == BossState.DivisoryFailure)
            {
                state = BossState.Idle;
            }
            interruptionActive = false;
        }
    }

    private Vector3 ComputeRoomCenter()
    {
        if (agent == null || !agent.enabled) return transform.position;

        Vector3 origin = transform.position;

        Vector3[] dirs = {
            Vector3.forward,
            (Vector3.forward + Vector3.right).normalized,
            Vector3.right,
            (Vector3.back   + Vector3.right).normalized,
            Vector3.back,
            (Vector3.back   + Vector3.left).normalized,
            Vector3.left,
            (Vector3.forward + Vector3.left).normalized,
        };

        Vector3 sum = Vector3.zero;
        int validHits = 0;
        NavMeshPath path = new NavMeshPath();

        foreach (Vector3 dir in dirs)
        {
            float lo = 0f;
            float hi = divisoryRayMaxLength;

            for (int iter = 0; iter < 7; iter++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector3 probe = origin + dir * mid;

                if (NavMesh.SamplePosition(probe, out NavMeshHit navHit, 1.5f, agent.areaMask))
                {
                    agent.CalculatePath(navHit.position, path);
                    if (path.status == NavMeshPathStatus.PathComplete)
                        lo = mid;
                    else
                        hi = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            if (lo > 0.5f)
            {
                sum += origin + dir * lo;
                validHits++;
            }
        }

        if (validHits == 0) return origin;

        Vector3 candidate = sum / validHits;
        candidate.y = origin.y;

        if (NavMesh.SamplePosition(candidate, out NavMeshHit finalHit, divisoryNavSampleRadius, agent.areaMask))
            return finalHit.position;

        return origin;
    }

    private IEnumerator RepositionToCenterRoutine(Vector3 destination, int myToken)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) yield break;

        if ((destination - transform.position).sqrMagnitude < 0.01f) yield break;

        MoveTowards(destination, divisoryRepositionSpeed);
        yield return null;

        float sqrThreshold = divisoryArrivalThreshold * divisoryArrivalThreshold;
        float safetyTimer = 0f;

        while (!isDead && myToken == actionToken && state == BossState.DivisoryFailure)
        {
            if (agent.remainingDistance == float.PositiveInfinity) break;

            safetyTimer += Time.deltaTime;
            if (safetyTimer >= 5f) break; // Sale limpiamente sin alterar la posición física mediante Warp

            Vector3 flat = transform.position; flat.y = 0f;
            Vector3 flatDest = destination; flatDest.y = 0f;

            if ((flat - flatDest).sqrMagnitude <= sqrThreshold) break;
            if (!agent.pathPending && agent.remainingDistance <= divisoryArrivalThreshold) break;

            // Si colisiona o la velocidad baja drásticamente, pasa a Idle en vez de deslizarse estático
            SetWalking(agent.velocity.sqrMagnitude > 0.05f);

            FacePlayer(6f);
            yield return null;
        }

        StopAgent();
    }

    private bool TryComputeDivisoryWall(Vector3 roomCenter, out Vector3 wallCenter, out Quaternion wallRotation, out float wallLength)
    {
        wallCenter = roomCenter;
        wallRotation = Quaternion.identity;
        wallLength = 10f;

        if (player == null) return false;

        Vector3 toPlayer = (player.position - roomCenter);
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.001f) return false;
        toPlayer.Normalize();

        Vector3 perpDir = Vector3.Cross(Vector3.up, toPlayer).normalized;
        Vector3 origin = new Vector3(roomCenter.x, roomCenter.y + 0.5f, roomCenter.z);

        float distLeft = divisoryRayMaxLength;
        float distRight = divisoryRayMaxLength;

        if (Physics.Raycast(origin, perpDir, out RaycastHit hitRight, divisoryRayMaxLength, obstacleLayers))
        {
            distRight = hitRight.distance;
        }

        if (Physics.Raycast(origin, -perpDir, out RaycastHit hitLeft, divisoryRayMaxLength, obstacleLayers))
        {
            distLeft = hitLeft.distance;
        }

        wallLength = distLeft + distRight;
        wallCenter = origin + perpDir * (distRight * 0.5f - distLeft * 0.5f);

        if (Physics.Raycast(wallCenter + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 15f, groundLayerMask))
        {
            wallCenter.y = groundHit.point.y + divisoryWallHeight * 0.5f;
        }
        wallRotation = Quaternion.LookRotation(perpDir);

        return wallLength > 0.5f;
    }

    #endregion

    #region Attack - Manos de los Ahogados

    private IEnumerator ExecuteDrownedHandsRoutine(bool usePredictive)
    {
        int myToken = ++actionToken;
        interruptionActive = true;
        state = BossState.DrownedHands;

        try
        {
            for (int i = 0; i < drownedHitCount; i++)
            {
                if (isDead || myToken != actionToken) break;

                attackExecuteTriggered = false;
                pendingAnticipationSFX = drownedHandsAnticipationSFX;
                if (knightAnimCtrl != null) knightAnimCtrl.PlayHandAttack();

                yield return new WaitUntil(() => attackExecuteTriggered || isDead || state != BossState.DrownedHands || myToken != actionToken);

                if (isDead || state != BossState.DrownedHands || myToken != actionToken) break;

                Vector3 target = ComputeHandsTarget(usePredictive);
                SpawnSoulHand(target);
                PlaySFX(drownedHandsSFX);

                yield return new WaitForSeconds(drownedInterval);
            }
        }
        finally
        {
            if (myToken == actionToken)
            {
                if (state == BossState.DrownedHands)
                {
                    state = BossState.Idle;
                }
                interruptionActive = false;
            }
        }
    }

    private Vector3 ComputeHandsTarget(bool usePredictive)
    {
        if (player == null) return transform.position;

        Vector3 target = player.position;

        if (usePredictive && playerVelocity.sqrMagnitude > 0.01f && Random.value <= predictiveChance)
        {
            target += playerVelocity.normalized * predictiveLeadDist;
        }

        return NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            ? hit.position
            : target;
    }

    public void RegisterPlayerDash()
    {
        playerDashCount++;
    }

    #endregion

    #region Movement & Tracking Utils

    private Vector3 GetGroundPosition(Vector3 rayOrigin)
    {
        if (Physics.Raycast(rayOrigin + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 30f, groundLayerMask))
        {
            return hit.point + Vector3.up * 0.05f;
        }

        return new Vector3(rayOrigin.x, transform.position.y + 0.05f, rayOrigin.z);
    }

    private IEnumerator DashRoutine(Vector3 target, float duration, float speed, bool dealContactDamage, bool spawnCrystalOnWallHit)
    {
        float elapsed = 0f;

        while (elapsed < duration && !isDead)
        {
            if (isInHitStun)
            {
                break;
            }

            if (isInAnticipation)
            {
                yield return null;
                continue;
            }

            Vector3 dir = (target - transform.position);
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.05f)
            {
                break;
            }

            dir.Normalize();
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            if (Physics.SphereCast(transform.position + Vector3.up * 0.5f,
                0.45f, dir, out RaycastHit wallHit, 0.8f, obstacleLayers))
            {
                if (spawnCrystalOnWallHit)
                {
                    SpawnCrystalTrail(wallHit.point + wallHit.normal * 0.1f, wallHit.normal);
                }
                break;
            }

            float dist = target != null
            ? Vector3.Distance(transform.position, target)
            : float.MaxValue;

            if (dist >= dashHitRadius - 0.25f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        debugDashSphereActive = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Warp(transform.position);
        }
    }

    private void MoveTowards(Vector3 destination, float speed)
    {
        if (agent == null || !agent.enabled) return;
        agent.speed = speed;
        agent.isStopped = false;
        agent.SetDestination(destination);
        SetWalking(true);
    }

    private void StopAgent()
    {
        if (agent == null || !agent.enabled) return;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        if (agent.isOnNavMesh) agent.ResetPath();
        SetWalking(false);
    }

    private void FacePlayer(float lerpSpeed = 10f)
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * lerpSpeed);
    }

    private void FacePlayerInstant()
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private bool PlayerInAggroRange()
    {
        return player != null && Vector3.Distance(transform.position, player.position) <= aggroRange;
    }

    private void TrackPlayerVelocity()
    {
        if (player == null) return;
        playerVelocity = (player.position - lastPlayerPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPlayerPos = player.position;
    }

    #endregion

    #region Damage Utils

    private void DealAoEDamage(Vector3 center, float radius, float dmg)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, playerLayer);
        foreach (var c in hits)
        {
            if (!c.CompareTag("Player")) continue;
            c.GetComponent<PlayerHealth>()?.TakeDamage(dmg);
            return;
        }
    }

    private bool TryDealBrokenChargeDamage(Vector3 center, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, playerLayer);
        foreach (var c in hits)
        {
            if (!c.CompareTag("Player")) continue;
            c.GetComponent<PlayerHealth>()?.TakeDamage(brokenChargeHitDamage);
            return true;
        }
        return false;
    }

    private void ApplyKnockback(Transform target, float force)
    {
        if (target.TryGetComponent<PlayerKnockbackReceiver>(out var knockbackReceiver))
        {
            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0f;

            knockbackReceiver.ApplyKnockback(direction, force);
        }
    }

    #endregion

    #region Hit Stun And Anticipation Logic

    public void HandleAnimEvents(string eventName)
    {
        switch (eventName)
        {
            case "AnimEvent_AnticipationPause":
                StartAnticipationPause();
                break;
            case "AnimEvent_AttackExecute":
                attackExecuteTriggered = true;
                break;
        }
    }

    public void StartAnticipationPause()
    {
        if (isDead || isInHitStun || hitStunRecoveryCooldown > 0f) return;

        if (anticipationCoroutine != null) StopCoroutine(anticipationCoroutine);
        anticipationCoroutine = StartCoroutine(AnticipationRoutine());
    }

    private IEnumerator AnticipationRoutine()
    {
        isInAnticipation = true;
        StopAgent();

        if (knightAnimCtrl != null) knightAnimCtrl.PauseAnimation();

        AudioClip sfx = pendingAnticipationSFX != null ? pendingAnticipationSFX : anticipationSFX;
        pendingAnticipationSFX = null;

        if (audioSource != null && sfx != null)
        {
            audioSource.pitch = anticipationSFXPitch;
            audioSource.PlayOneShot(sfx);
            audioSource.pitch = 1f;
        }

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.PlayAnticipationBlink(anticipationPauseDuration);
        }

        if (knightAnimCtrl != null)
        {
            knightAnimCtrl.PlayAnticipationShake(anticipationPauseDuration);
        }

        yield return new WaitForSeconds(anticipationPauseDuration);

        if (knightAnimCtrl != null) knightAnimCtrl.ResumeAnimation();

        isInAnticipation = false;
        anticipationCoroutine = null;
    }

    private IEnumerator WaitRespectingAnticipation(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!isInAnticipation)
            {
                elapsed += Time.deltaTime;
            }
            yield return null;
        }
    }

    private void CancelAnticipation()
    {
        if (anticipationCoroutine != null)
        {
            StopCoroutine(anticipationCoroutine);
            anticipationCoroutine = null;
        }

        if (knightAnimCtrl != null)
        {
            knightAnimCtrl.ResumeAnimation();
            knightAnimCtrl.StopAnticipationShake();
        }

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.CancelAnticipationBlink();
        }

        isInAnticipation = false;
    }

    private void HandleDamageTaken()
    {
        if (isDead) return;

        if (state == BossState.StaticFailureWindup
            || state == BossState.StaticFailureRelease)
            return;

        if (hitStunDuration <= 0) return;

        actionToken++;
        isInHitStun = true;

        if (hitStunCoroutine != null) StopCoroutine(hitStunCoroutine);
        hitStunCoroutine = StartCoroutine(HitStunRoutine());
    }

    private IEnumerator HitStunRoutine()
    {
        int myToken = actionToken;

        state = BossState.Stunned;
        interruptionActive = true;

        CancelAnticipation();
        StopAgent();

        if (knightAnimCtrl != null)
        {
            knightAnimCtrl.ResumeAnimation();
            knightAnimCtrl.SetAttackEnded(true);
        }

        if (audioSource != null && hitStunSFX != null)
        {
            audioSource.PlayOneShot(hitStunSFX);
        }

        yield return new WaitForSeconds(hitStunDuration);

        yield return new WaitForSeconds(forceIdleDuration);

        if (knightAnimCtrl != null) knightAnimCtrl.SetAttackEnded(false);

        isInHitStun = false;
        hitStunCoroutine = null;
        hitStunRecoveryCooldown = 0.1f;

        if (myToken == actionToken)
        {
            interruptionActive = false;
            state = BossState.Idle;
        }
    }

    #endregion

    #region Spawners & VFX

    private void SpawnBossMine(Vector3 pos)
    {
        if (bossMinePrefab == null) return;
        var mine = Instantiate(bossMinePrefab, pos, Quaternion.identity);
        spawnedObjects.Add(mine.gameObject);
    }

    private void SpawnCrystalTrail(Vector3 pos, Vector3 normal)
    {
        if (crystalTrailPrefab == null) return;
        var trail = Instantiate(crystalTrailPrefab, pos,
            Quaternion.LookRotation(Vector3.Cross(normal, Vector3.up)));
        spawnedObjects.Add(trail.gameObject);
    }

    private void SpawnSoulHand(Vector3 pos)
    {
        if (soulHandPrefab == null) return;
        var hand = Instantiate(soulHandPrefab, pos, Quaternion.identity);
        hand.Initialize(drownedDamage, 1.5f);
        spawnedObjects.Add(hand.gameObject);
    }

    private GameObject SpawnWarningIndicator(Vector3 pos, float scale, GameObject prefab)
    {
        if (prefab == null) return null;

        float verticalOffset = 0.05f;
        int groundLayer = LayerMask.GetMask("Ground");

        Vector3 rayOrigin = new Vector3(pos.x, pos.y + 50f, pos.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f, groundLayer))
        {
            pos.y = hit.point.y + verticalOffset;
        }

        var obj = Instantiate(prefab, pos, Quaternion.identity);
        obj.transform.localScale = new Vector3(scale, obj.transform.localScale.y, scale);

        spawnedObjects.Add(obj);
        return obj;
    }

    private void SpawnImpactStunVFX(Vector3 pos)
    {
        if (impactStunVFXPrefab == null) return;
        var vfx = Instantiate(impactStunVFXPrefab, pos, Quaternion.identity);
        spawnedObjects.Add(vfx);
        Destroy(vfx, 2f);
    }

    #endregion

    #region Audio & Camera Utils

    private void SetWalking(bool isWalking)
    {
        if (knightAnimCtrl != null) knightAnimCtrl.SetWalking(isWalking);
    }

    private void PlaySFX(AudioClip clip, float vol = 1f)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, vol);
    }

    private void ShakeCamera(float amplitude, float duration)
    {
        if (camNoise == null) return;
        StartCoroutine(CameraShakeRoutine(amplitude, duration));
    }

    private IEnumerator CameraShakeRoutine(float amplitude, float duration)
    {
        float original = camNoise.AmplitudeGain;
        camNoise.AmplitudeGain = amplitude;
        yield return new WaitForSeconds(duration);
        camNoise.AmplitudeGain = original;
    }

    #endregion

    #region Cleanup

    private void CleanupSpawnedObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null) Destroy(spawnedObjects[i]);
        }
        spawnedObjects.Clear();
    }

    #endregion

    #region Logging

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, divisoryNavSampleRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, divisoryTriggerDist);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, drownedRange);

        Gizmos.color = new Color(1f, 0.3f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, scrapRamActiveDist);

        Gizmos.color = new Color(1f, 0.55f, 0f, 0.55f);
        Vector3 dashGizmoPos = (attackOrigin != null && attackOrigin != transform)
            ? attackOrigin.position : transform.position + Vector3.up * 0.6f;
        Gizmos.DrawWireSphere(dashGizmoPos, dashHitRadius);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        if (debugDashSphereActive)
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.85f);
            Gizmos.DrawWireSphere(debugDashSpherePos, dashHitRadius);
        }

        if (player != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.up, player.position + Vector3.up);
        }
    }

    private void OnGUI()
    {
        if (!showDebugGUI || !Application.isPlaying) return;

        float dist = player != null
            ? Vector3.Distance(transform.position, player.position) : -1f;

        GUIStyle box = new GUIStyle(GUI.skin.box) { fontSize = 12 };
        GUIStyle lbl = new GUIStyle(GUI.skin.label) { fontSize = 12 };

        GUILayout.BeginArea(new Rect(10, 10, 270, 295), box);
        GUILayout.Label("── BloodKnight Debug ──", lbl);
        GUILayout.Label($"State            : {state}", lbl);
        GUILayout.Label($"HP               : {currentHealth:F0} / {maxHealth:F0}", lbl);
        GUILayout.Label($"Dist al jugador  : {dist:F2} uds", lbl);
        GUILayout.Space(4);
        GUILayout.Label($"Shield activo    : {staticShieldActive}", lbl);
        GUILayout.Space(4);
        GUILayout.Label($"Dashes jugador   : {playerDashCount} / {predictiveDashThreshold}", lbl);
        GUILayout.Label($"Interrupcion     : {interruptionActive}", lbl);
        GUILayout.Space(4);
        GUILayout.Label($"CD FallaEstatica : {Mathf.Max(0f, nextStaticTime - Time.time):F1}s", lbl);
        GUILayout.Label($"CD CargaQuebrados: {Mathf.Max(0f, nextBrokenChargeTime - Time.time):F1}s", lbl);
        GUILayout.Label($"CD ManosAhogados : {Mathf.Max(0f, nextDrownedTime - Time.time):F1}s", lbl);
        GUILayout.Label($"CD FallaDivisoria: {Mathf.Max(0f, nextDivisoryTime - Time.time):F1}s", lbl);
        GUILayout.Space(4);
        GUILayout.Label($"RoomCenter cached : {roomCenterCached}", lbl);
        if (roomCenterCached)
            GUILayout.Label($"RoomCenter       : {cachedRoomCenter:F1}", lbl);
        GUILayout.EndArea();
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int priority)
    {
        switch (priority)
        {
            case 1: Debug.Log($"[The One Who Shattered] {message}"); break;
            case 2: Debug.LogWarning($"[The One Who Shattered] {message}"); break;
            case 3: Debug.LogError($"[The One Who Shattered] {message}"); break;
            default: Debug.Log($"[The One Who Shattered] {message}"); break;
        }
    }

    #endregion
}