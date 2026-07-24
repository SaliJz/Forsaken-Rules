using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

public partial class AstarothController : MonoBehaviour, IAnimEventHandler
{
    #region Enums

    public enum BossState
    {
        Moving,
        Attacking,
        SpecialAbility,
        MudWave
    }

    private enum CombatPatternStep
    {
        Whip,
        ShortMove,
        Smash,
        LongMove
    }

    #endregion

    #region State & General Settings

    [Header("Boss State")]
    [SerializeField] private BossState _currentState = BossState.Moving;

    private Coroutine _combatLoopCoroutine;
    private bool _isCombatLoopActive = false;
    private CombatPatternStep _combatPatternStep = CombatPatternStep.Whip;
    private CombatPatternStep _resumeCombatStep = CombatPatternStep.Whip;

    [Header("Player Settings")]
    [SerializeField] private Transform _player;

    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;

    private int _specialAbilityUsedCount = 0;
    private EnemyHealth _enemyHealth;

    #endregion

    #region Movement Settings

    [Header("Movement Settings")]
    [SerializeField] private float _stoppingDistance = 15f;
    [SerializeField] private float _safeDistance = 2.5f;

    private NavMeshAgent _navMeshAgent;
    private float _originalSpeed;

    #endregion

    #region Ability: Defensive Stomp

    [Header("Defense Mechanism: Apisonador")]
    [SerializeField] private float _stompTriggerDistance = 5f;
    [SerializeField] private float _stompCooldown = 8f;

    [Header("Apisonador - Proximity Timer")]
    [SerializeField] private float _stompProximityRequiredTime = 1.5f;

    [Header("Apisonador - Pull")]
    [SerializeField] private float _stompPullRadius = 8f;
    [SerializeField] private float _stompPullDuration = 0.5f;
    [SerializeField] private float _stompPullSpeed = 12f;
    [SerializeField] private GameObject _stompPullIndicatorObject;
    [SerializeField] private ParticleSystem _stompPullVFX;

    [Header("Apisonador - Impact")]
    [SerializeField] private bool _enableStompDamage = true;
    [SerializeField] private float _stompDamage = 2.5f;
    [SerializeField] private float _stompRadius = 5f;
    [SerializeField] private float _stompKnockbackForce = 5f;
    [SerializeField] private GameObject _stompImpactIndicatorObject;
    [SerializeField] private GameObject _stompVFXPrefab;

    private float _stompTimer = 0f;
    private float _stompProximityTimer = 0f;
    private bool _isStomping = false;
    private bool _isInAnticipation = false;
    private Coroutine _anticipationCoroutine = null;

    // private GameObject _activeStompPullVFX;

    #endregion

    #region Ability: Mud Wave

    [Header("Ability: Ola de Lodo")]
    [SerializeField] private bool _enableMudWave = true;
    [SerializeField] private float _mudWaveTriggerDistance = 12f;
    [SerializeField] private float _mudWaveFleeDuration = 1.2f;
    [SerializeField] private float _mudWaveChargeSpeed = 34f;
    [SerializeField] private float _mudWaveMinChargeDistance = 24f;
    [SerializeField] private float _mudWaveOvershootDistance = 10f;
    [SerializeField] private float _mudWaveCooldown = 7f;
    [SerializeField] private float _mudWaveWarningTime = 0.25f;
    [SerializeField] private float _mudWaveDamage = 15f;
    [SerializeField] private float _mudWaveHitRadius = 3f;
    [SerializeField] private float _mudWaveKnockbackForce = 10f;
    [SerializeField] private float _mudWaveAnimatorSpeedMultiplier = 1.8f;
    [SerializeField] private GameObject _mudWaveWarningPrefab;

    [Header("Ola de Lodo - Recovery")]
    [SerializeField] private float _mudWaveRecoveryTime = 1f;

    [Header("Ola de Lodo - Wind VFX")]
    [SerializeField] private GameObject _mudWaveWindVFXRoot;

    private bool _isMudWaving;
    private float _farDistanceTimer;
    private float _mudWaveCooldownTimer;

    #endregion

    #region Ability: Attack 1 (Whip)

    [Header("Tiempos del Ciclo de Combate")]
    [SerializeField] private float _shortMoveDuration = 3f;
    [SerializeField] private float _longMoveDuration = 5f;

    [Header("Attack 1: Latigazo Desgarrador")]
    [SerializeField] private Transform _whipDamageOrigin;
    [SerializeField] private float _whipHitRadius = 3.5f;
    [SerializeField] private float _Attack1Damage = 9f;

    [Header("Attack 1 Timings")]
    [SerializeField] private float _whipPreAttackDelay = 0.4f;
    [SerializeField] private float _whipDelay1 = 1.05f;
    [SerializeField] private float _whipDelay2 = 0.2f;
    [SerializeField] private float _whipDelay3 = 0.2f;

    private bool _isAttackingWithWhip;
    private GameObject _activeWhipIndicator;
    private ParticleSystem _whipImpactRocks_VFX;

    #endregion

    #region Ability: Attack 2 (Smash)

    [Header("Attack 2: Latigazo Demoledor")]
    [SerializeField] private GameObject _smashRockObject;
    [SerializeField] private Transform _smashRockHeldFollowTarget;
    [SerializeField] private float _smashRockScale = 1f;
    [SerializeField] private float _smashRockHitRadius = 1.5f;
    [SerializeField] private float _smashRockTravelDuration = 0.35f;
    [SerializeField] private AnimationCurve _smashRockTravelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField] private float _Attack2Damage = 25f;
    [SerializeField] private float _smashRadius = 5f;
    [SerializeField] private float _smashDetectionRadius = 10f;
    [SerializeField] private GameObject _smashRadiusPrefab;
    [SerializeField] private float _smashDelay = 1.5f;

    [Header("Attack 2 Ground Indicator")]
    [SerializeField] private Transform _smashGroundIndicator;
    [SerializeField] private GameObject _smashGroundIndicatorPrefab;
    //[SerializeField] private float _smashTargetLockBeforeImpact = 0.5f;
    [SerializeField] private float _smashIndicatorGroundOffset = 0.08f;

    private bool _isSmashing;
    private bool _smashRockInFlight;
    private bool _smashImpactCompleted;
    private bool _smashRockIsHeld;
    private Transform _smashRockOriginalParent;
    private Vector3 _smashRockOriginalLocalPosition;
    private Quaternion _smashRockOriginalLocalRotation;
    private Vector3 _smashRockOriginalLocalScale;
    private Vector3 _smashTargetPoint;
    private Vector3 _lastSmashOverlapCenter;
    private float _lastSmashOverlapRadius;
    private bool _showSmashOverlapGizmo;
    private bool _lastSmashHitPlayer = false;

    #endregion

    #region Ability: Special (Pulso Carnal)

    [Header("Special Ability: Pulso Carnal")]
    [SerializeField] private float _pulseExpansionDuration = 3f;
    [SerializeField] private float _pulseWaitDuration = 1f;
    [SerializeField] private float _pulseSlowPercentage = 0.5f;
    [SerializeField] private float _pulseSlowDuration = 2f;
    [SerializeField] private int _pulseDamage = 5;
    [SerializeField] private GameObject _nervesVisualizationPrefab;
    [SerializeField] private GameObject _crackEffectPrefab;
    [SerializeField] private Transform _headsTransform;
    [SerializeField] private float _headDownRotationAngle = -45f;
    [SerializeField] private float _headAnimationDuration = 0.5f;
    [SerializeField] private float _roomMaxRadius = 45f;
    [SerializeField] private bool _calculateRoomRadiusOnStart = true;
    [SerializeField] private float _movementSpeedForPulse = 10f;
    [SerializeField] private float _pulseDelay = 1.05f;

    private bool _isUsingSpecialAbility;
    private readonly float[] _healthThresholdsForPulse = { 0.7f, 0.4f };
    private bool _isSpecialAbilityPending = false;
    private List<GameObject> _instantiatedEffects = new List<GameObject>();
    private Vector3 _roomCenter = Vector3.zero;

    #endregion

    #region Visuals (Telegraphs & VFX)

    [Header("Attack Telegraphs")]
    [SerializeField] private GameObject _stompWarningPrefab;
    [SerializeField] private GameObject _whipTelegraphPrefab;
    [SerializeField] private GameObject _smashWarningPrefab;

    [Header("VFX")]
    [SerializeField] private TrailRenderer _trailRenderer;

    [Header("Dodge Feedback")]
    [SerializeField] private GameObject _dodgeIndicatorPrefab;
    [SerializeField] private float _dodgeIndicatorDuration = 1.5f;

    #endregion

    #region Combat Analytics

    [Header("Combat Analytics")]
    [SerializeField] private bool _enableAdaptiveDifficulty = true;
    [SerializeField] private float _difficultyAdjustmentInterval = 15f;

    private bool _isDead = false;
    private int _totalAttemptsLanded = 0;
    private int _totalAttemptsExecuted = 0;
    private int _hitsReceivedFromPlayer = 0;
    private float _difficultyTimer = 0f;

    #endregion

    #region Enraged Phase

    [Header("Enraged Phase (25% HP)")]
    [SerializeField] private float _enragedHealthThreshold = 0.25f;

    private bool _isEnraged = false;
    private bool _skipNextCombatLoopDelay = false;

    #endregion

    #region Audio & Camera

    [Header("SFX - Astaroth")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip presenceSFX;
    [SerializeField] private AudioClip walkSFX;
    [SerializeField] private AudioClip whipAttackSFX;
    [SerializeField] private AudioClip smashAttackSFX;
    [SerializeField] private AudioClip pulseAttackSFX;
    [SerializeField] private AudioClip stompSFX;
    [SerializeField] private AudioClip phase2RageSFX;
    [SerializeField] private AudioClip damageReceivedSFX;
    [SerializeField] private AudioClip deathSFX;

    private float _audioIdleTimer;
    private float _audioIdleInterval;
    private float _audioStepTimer;

    [Header("Camera Shake")]
    [SerializeField] private CinemachineCamera _vcam;
    [SerializeField] private float _shakeDuration = 0.2f;
    [SerializeField] private float _amplitude = 2f;
    [SerializeField] private float _frequency = 2f;

    private CinemachineBasicMultiChannelPerlin _noise;

    #endregion

    #region Attack Anticipation

    [Header("Anticipación - Ataque Caos (Cañón)")]
    [SerializeField] private AudioClip canonChargeSFX;
    [SerializeField] private float canonAnticipationDuration = 0.65f;

    bool smashAnticipationEnded;

    [Header("Anticipación - Apisonador (Stomp)")]
    [SerializeField] private AudioClip apisonadorLooseScrewsSFX;
    [SerializeField] private float apisonadorAnticipationDuration = 0.35f;

    [Header("Anticipación - Pulso Carnal (Pulpo)")]
    [SerializeField] private AudioClip pulsoCarnalViscousSFX;
    [SerializeField] private float pulsoCarnalAnticipationDuration = 0.65f;

    #endregion

    #region Debug

    [Header("Debug")]
    [SerializeField] private bool _showRoomGizmos = true;

    #endregion

    #region Animation

    private TheWeightAnimCtrl _animCtrl;
    private string _pendingAnimEvent;

    private const float ANIM_EVENT_TIMEOUT = 3f;

    #endregion

    #region Internal State Variables

    private EnemyVisualEffects _enemyVisualEffects;
    private Renderer[] _bossRenderers;
    private Color[] _bossOriginalColors;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (_enemyHealth == null) _enemyHealth = GetComponent<EnemyHealth>();
        if (_navMeshAgent == null) _navMeshAgent = GetComponent<NavMeshAgent>();
        if (_animCtrl == null) _animCtrl = GetComponentInChildren<TheWeightAnimCtrl>();
        if (_enemyVisualEffects == null) _enemyVisualEffects = GetComponent<EnemyVisualEffects>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

        if (_vcam == null)
        {
            CinemachineCamera vcam = Object.FindFirstObjectByType<CinemachineCamera>();
            if (vcam != null) _vcam = vcam;
        }

        CacheBossRenderers();
    }

    private void Start()
    {
        if (_navMeshAgent != null)
        {
            _navMeshAgent.updateRotation = false;
            _navMeshAgent.stoppingDistance = Mathf.Max(_stoppingDistance, _safeDistance);
            _originalSpeed = _navMeshAgent.speed;
        }

        if (_calculateRoomRadiusOnStart) CalculateRoomRadius();
        else _roomCenter = new Vector3(transform.position.x, 0f, transform.position.z);

        if (_trailRenderer != null) _trailRenderer.enabled = false;
        if (_mudWaveWindVFXRoot != null) _mudWaveWindVFXRoot.SetActive(false);
        CacheSmashRockTransform();
        SetSmashRockActive(false);
        SetStompIndicatorsActive(false);

        if (_player == null)
        {
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null) _player = playerGO.transform;
        }

        if (_enemyHealth != null) _enemyHealth.SetMaxHealth(_maxHealth);
        _currentHealth = _maxHealth;

        if (_vcam != null)
        {
            _noise = _vcam.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;
        }

        StartCombatLoop();
    }

    private void OnEnable()
    {
        if (_enemyHealth != null)
        {
            _enemyHealth.OnDeath += HandleEnemyDeath;
            _enemyHealth.OnHealthChanged += HandleEnemyHealthChange;
            _enemyHealth.OnDamaged += HandleDamageReceived;
        }
    }

    private void OnDisable()
    {
        if (_enemyHealth != null)
        {
            _enemyHealth.OnDeath -= HandleEnemyDeath;
            _enemyHealth.OnHealthChanged -= HandleEnemyHealthChange;
            _enemyHealth.OnDamaged -= HandleDamageReceived;
        }
    }

    private void Update()
    {
        if (_isDead) return;
        if (_player == null) return;

        if (_enemyHealth != null && _enemyHealth.IsStunned)
        {
            if (_navMeshAgent != null && _navMeshAgent.enabled)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.velocity = Vector3.zero;
            }
            return;
        }

        HandleAudioLoop();
        CheckHealthThresholds();

        if (_enableAdaptiveDifficulty)
        {
            _difficultyTimer += Time.deltaTime;
            if (_difficultyTimer >= _difficultyAdjustmentInterval)
            {
                AdjustDifficultyBasedOnPerformance();
                _difficultyTimer = 0f;
            }
        }

        if (_currentState == BossState.SpecialAbility)
        {
            if (_isCombatLoopActive) StopCombatLoop();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

        UpdateMudWaveTrigger(distanceToPlayer);

        if (_isMudWaving) return;

        bool playerIsClose = distanceToPlayer < _stompTriggerDistance &&
            _stompTimer <= 0f &&
            !_isStomping &&
            !_isAttackingWithWhip &&
            !_isSmashing &&
            !_isMudWaving;

        if (playerIsClose)
        {
            _stompProximityTimer += Time.deltaTime;

            if (_stompProximityTimer >= _stompProximityRequiredTime)
            {
                _stompProximityTimer = 0f;
                InterruptAndPerformStomp();
                return;
            }
        }
        else
        {
            _stompProximityTimer = 0f;
        }

        if (!_isCombatLoopActive &&
            !_isStomping &&
            !_isMudWaving &&
            !_isAttackingWithWhip &&
            !_isSmashing &&
            !_isUsingSpecialAbility &&
            _currentState == BossState.Moving)
        {
            StartCombatLoop();
        }

        _stompTimer -= Time.deltaTime;
    }

    #endregion

    #region Animation Events

    private const string ANIM_EVENT_ANTICIPATION_PAUSE = "AnimEvent_AnticipationPause";
    private const string ANIM_EVENT_WHIP_WINDUP_DONE = "WhipWindupDone";
    private const string ANIM_EVENT_WHIP_IMPACT = "WhipImpact";
    private const string ANIM_EVENT_APISONADOR_IMPACT = "ApisonadorImpact";
    private const string ANIM_EVENT_PULSO_CARNAL_IMPACT = "PulsoCarnalImpact";
    private const string ANIM_EVENT_CANON_RELEASE = "CanonRelease";

    public void HandleAnimEvents(string eventName)
    {
        if (eventName == ANIM_EVENT_ANTICIPATION_PAUSE)
        {
            StartAnticipationPause();
            return;
        }

        if (_pendingAnimEvent == eventName)
        {
            _pendingAnimEvent = null;
        }
    }

    private void StartAnticipationPause()
    {
        if (_isDead) return;

        if (_anticipationCoroutine != null) StopCoroutine(_anticipationCoroutine);
        _anticipationCoroutine = StartCoroutine(AnticipationRoutine());
    }

    private IEnumerator AnticipationRoutine()
    {
        _isInAnticipation = true;

        float duration;
        AudioClip sfx;

        if (_isSmashing)
        {
            duration = canonAnticipationDuration;
            sfx = canonChargeSFX;
        }
        else if (_isStomping)
        {
            duration = apisonadorAnticipationDuration;
            sfx = apisonadorLooseScrewsSFX;
        }
        else
        {
            duration = pulsoCarnalAnticipationDuration;
            sfx = pulsoCarnalViscousSFX;
        }

        if (_animCtrl != null) _animCtrl.PauseAnimation();

        if (audioSource != null && sfx != null)
        {
            audioSource.PlayOneShot(sfx);
        }

        if (_enemyVisualEffects != null)
        {
            _enemyVisualEffects.PlayAnticipationBlink(duration);
        }

        if (_animCtrl != null)
        {
            _animCtrl.PlayAnticipationShake(duration);
        }

        yield return new WaitForSeconds(duration);

        if (_animCtrl != null) _animCtrl.ResumeAnimation();

        _isInAnticipation = false;
        _anticipationCoroutine = null;
    }

    private void CancelAnticipation()
    {
        if (_anticipationCoroutine != null)
        {
            StopCoroutine(_anticipationCoroutine);
            _anticipationCoroutine = null;
        }

        if (_animCtrl != null) _animCtrl.ResumeAnimation();
        if (_animCtrl != null) _animCtrl.StopAnticipationShake();
        if (_enemyVisualEffects != null) _enemyVisualEffects.CancelAnticipationBlink();

        _isInAnticipation = false;
    }

    /// <summary>
    /// Espera a que llegue un Anim Event con el nombre indicado, o hasta que
    /// transcurra el timeout (fallback de seguridad si el clip no tiene el evento).
    /// </summary>
    private IEnumerator WaitForAnimEvent(string eventName, float timeout = ANIM_EVENT_TIMEOUT)
    {
        _pendingAnimEvent = eventName;
        float elapsed = 0f;

        while (_pendingAnimEvent == eventName && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        _pendingAnimEvent = null;
    }

    private void CacheSmashRockTransform()
    {
        if (_smashRockObject == null) return;

        Transform rockTransform = _smashRockObject.transform;

        _smashRockOriginalParent = rockTransform.parent;
        _smashRockOriginalLocalPosition = rockTransform.localPosition;
        _smashRockOriginalLocalRotation = rockTransform.localRotation;
        _smashRockOriginalLocalScale = rockTransform.localScale;
    }

    private void SetSmashRockActive(bool active)
    {
        if (_smashRockObject == null) return;

        _smashRockObject.SetActive(active);

        if (active)
        {
            _smashRockObject.transform.localScale = _smashRockOriginalLocalScale * _smashRockScale;
        }
    }

    private void RestoreSmashRockTransform()
    {
        if (_smashRockObject == null) return;

        Transform rockTransform = _smashRockObject.transform;

        rockTransform.SetParent(_smashRockOriginalParent, false);
        rockTransform.localPosition = _smashRockOriginalLocalPosition;
        rockTransform.localRotation = _smashRockOriginalLocalRotation;
        rockTransform.localScale = _smashRockOriginalLocalScale * _smashRockScale;
    }

    private void BeginHeldSmashRock()
    {
        if (_smashRockObject == null) return;

        _smashRockIsHeld = true;

        SetSmashRockActive(true);

        if (_smashRockHeldFollowTarget != null)
        {
            Transform rockTransform = _smashRockObject.transform;

            rockTransform.SetParent(_smashRockHeldFollowTarget, false);
            rockTransform.localPosition = Vector3.zero;
            rockTransform.localRotation = Quaternion.identity;
            rockTransform.localScale = _smashRockOriginalLocalScale * _smashRockScale;
        }
    }

    private void EndHeldSmashRock()
    {
        _smashRockIsHeld = false;
    }

    #endregion

    #region Deterministic Combat Loop

    private void StartCombatLoop()
    {
        if (_isDead) return;
        if (_isCombatLoopActive) return;

        ResetAttackState();

        _combatLoopCoroutine = StartCoroutine(CombatPatternCycle());
        _isCombatLoopActive = true;
    }

    private void StopCombatLoop()
    {
        if (_combatLoopCoroutine != null) StopCoroutine(_combatLoopCoroutine);
        _isCombatLoopActive = false;

        if (_navMeshAgent != null) _navMeshAgent.speed = _originalSpeed;
    }

    private IEnumerator CombatPatternCycle()
    {
        if (!_skipNextCombatLoopDelay)
        {
            yield return new WaitForSeconds(_mudWaveRecoveryTime);
        }

        _skipNextCombatLoopDelay = false;

        while (true)
        {
            if (_player != null && _currentState == BossState.Moving)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

                if (distanceToPlayer > _mudWaveTriggerDistance)
                {
                    _combatPatternStep = CombatPatternStep.Smash;
                }
                else if (distanceToPlayer >= _stompTriggerDistance && distanceToPlayer <= _mudWaveTriggerDistance)
                {
                    _combatPatternStep = CombatPatternStep.Whip;
                }

                _resumeCombatStep = _combatPatternStep;
            }

            switch (_combatPatternStep)
            {
                case CombatPatternStep.Whip:
                    _currentState = BossState.Attacking;
                    yield return WhipAttackSequence();
                    ResetAttackState();
                    _combatPatternStep = CombatPatternStep.ShortMove;
                    break;

                case CombatPatternStep.ShortMove:
                    yield return MoveForDuration(_shortMoveDuration, _originalSpeed);
                    _combatPatternStep = CombatPatternStep.Smash;
                    break;

                case CombatPatternStep.Smash:
                    _currentState = BossState.Attacking;
                    yield return SmashAttackSequence();
                    ResetAttackState();
                    _combatPatternStep = CombatPatternStep.LongMove;
                    break;

                case CombatPatternStep.LongMove:
                    yield return AggressivePursuitMove(_longMoveDuration);
                    _combatPatternStep = CombatPatternStep.Whip;
                    break;
            }
        }
    }

    private IEnumerator MoveForDuration(float duration, float speed)
    {
        _currentState = BossState.Moving;

        if (_navMeshAgent != null)
        {
            _navMeshAgent.isStopped = false;
            _navMeshAgent.speed = speed;
        }

        float timer = 0f;

        while (timer < duration)
        {
            SimpleMoveBehavior();
            timer += Time.deltaTime;
            yield return null;
        }

        if (_navMeshAgent != null) _navMeshAgent.speed = _originalSpeed;
    }

    private IEnumerator AggressivePursuitMove(float maxDuration)
    {
        _currentState = BossState.Moving;

        if (_navMeshAgent != null)
        {
            _navMeshAgent.isStopped = false;
            _navMeshAgent.speed = _originalSpeed;
        }

        float timer = 0f;
        float farTimer = 0f;

        while (timer < maxDuration)
        {
            if (_player == null) break;

            float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

            if (distanceToPlayer <= _stompTriggerDistance * 1.5f) break;

            if (distanceToPlayer > _mudWaveTriggerDistance &&
                _enableMudWave &&
                _mudWaveCooldownTimer <= 0f &&
                !_isMudWaving)
            {
                farTimer += Time.deltaTime;

                if (farTimer >= _mudWaveFleeDuration)
                {
                    InterruptAndPerformMudWave();
                    yield break;
                }
            }
            else
            {
                farTimer = 0f;
            }

            SimpleMoveBehavior();
            timer += Time.deltaTime;
            yield return null;
        }

        if (_navMeshAgent != null) _navMeshAgent.speed = _originalSpeed;
    }

    private void ResetAttackState()
    {
        if (_isDead) return;

        _animCtrl?.StopAnticipationShake();
        _animCtrl?.ResumeAnimation();

        _animCtrl?.ReturnToIdle();

        DestroyWhipIndicator(_activeWhipIndicator);
        SetStompIndicatorsActive(false);
        _isAttackingWithWhip = false;
        _isSmashing = false;

        ResetSmashVisuals();
    }

    private void SimpleMoveBehavior()
    {
        if (_player == null || _navMeshAgent == null) return;

        _navMeshAgent.updateRotation = true;
        float distance = Vector3.Distance(transform.position, _player.position);

        bool isMoving = _navMeshAgent.velocity.magnitude > 0.1f && !_navMeshAgent.isStopped;
        if (_animCtrl != null) _animCtrl.isWalking = isMoving;

        if (distance > _stoppingDistance)
        {
            _navMeshAgent.isStopped = false;
            _navMeshAgent.SetDestination(_player.position);
        }
        else
        {
            _navMeshAgent.isStopped = true;
            LookAtPlayer();
        }
    }

    private void PrepareCombatInterrupt()
    {
        _resumeCombatStep = _combatPatternStep;

        StopCombatLoop();
        ResetAttackState();

        if (_trailRenderer != null) _trailRenderer.enabled = false;
        StopStompPullVFX();

        if (_navMeshAgent != null && _navMeshAgent.enabled)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
            _navMeshAgent.ResetPath();
        }
    }

    #endregion

    #region Health & Phases

    private void HandleDamageReceived()
    {
        if (audioSource != null && damageReceivedSFX != null && _currentHealth > 0)
        {
            audioSource.PlayOneShot(damageReceivedSFX);
        }

        _hitsReceivedFromPlayer++;
    }

    private void HandleEnemyHealthChange(float newCurrentHealth, float newMaxHealth)
    {
        _currentHealth = newCurrentHealth;
        _maxHealth = newMaxHealth;
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;
        if (_isDead) return;

        _isDead = true;

        StopCombatLoop();
        StopAllCoroutines();

        _isAttackingWithWhip = false;
        _isSmashing = false;
        _isUsingSpecialAbility = false;
        _isEnraged = false;
        _isMudWaving = false;

        if (_enemyHealth != null) _enemyHealth.SetDynamicVulnerability(0f);

        StopMudWaveWindVFX();
        DestroyAllInstantiatedEffects();

        _smashRockInFlight = false;
        _smashImpactCompleted = false;
        SetStompIndicatorsActive(false);
        StopStompPullVFX();
        ResetSmashVisuals();

        if (_navMeshAgent != null)
        {
            _navMeshAgent.enabled = false;
        }

        if (_animCtrl != null)
        {
            _animCtrl.SetAnimatorSpeed(1f);
            _animCtrl.ReturnToIdle();
            _animCtrl.isWalking = false;
            _animCtrl.PlayDeath();
        }

        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        this.enabled = false;
    }

    private void CheckHealthThresholds()
    {
        if (_isDead) return;
        if (_isUsingSpecialAbility || _isSpecialAbilityPending) return;

        float healthPercentage = _currentHealth / _maxHealth;

        if (_specialAbilityUsedCount < _healthThresholdsForPulse.Length)
        {
            if (healthPercentage <= _healthThresholdsForPulse[_specialAbilityUsedCount])
            {
                _specialAbilityUsedCount++;
                _isSpecialAbilityPending = true;

                bool attackInProgress =
                    _isAttackingWithWhip ||
                    _isSmashing ||
                    _isMudWaving ||
                    _isStomping;

                if (attackInProgress)
                {
                    StartCoroutine(WaitForAttackThenPulso());
                }
                else
                {
                    TriggerPulsoCarnal();
                }
            }
        }

        if (!_isEnraged && healthPercentage <= _enragedHealthThreshold)
        {
            EnterEnragedPhase();
        }
    }

    private IEnumerator WaitForAttackThenPulso()
    {
        float waitLimit = 6f;
        float waited = 0f;

        while (waited < waitLimit)
        {
            if (!_isAttackingWithWhip &&
                !_isSmashing &&
                !_isMudWaving &&
                !_isStomping)
            {
                break;
            }

            waited += Time.deltaTime;
            yield return null;
        }

        TriggerPulsoCarnal();
    }

    private void TriggerPulsoCarnal()
    {
        if (_isDead) return;

        StopCombatLoop();
        DestroyAllInstantiatedEffects();
        ResetSmashVisuals();

        _isAttackingWithWhip = false;
        _isSmashing = false;
        _isMudWaving = false;
        _smashRockInFlight = false;
        _smashImpactCompleted = false;

        StopMudWaveWindVFX();

        if (_enemyHealth != null) _enemyHealth.SetDynamicVulnerability(0f);

        if (_navMeshAgent != null && _navMeshAgent.enabled)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
            _navMeshAgent.ResetPath();
        }

        if (_trailRenderer != null) _trailRenderer.enabled = false;

        _animCtrl?.ReturnToIdle();

        _currentState = BossState.SpecialAbility;
        _isSpecialAbilityPending = false;

        StartCoroutine(PulsoCarnal());
    }

    private void EnterEnragedPhase()
    {
        _isEnraged = true;

        if (phase2RageSFX != null)
        {
            AudioSource.PlayClipAtPoint(phase2RageSFX, transform.position);
        }
    }

    private void AdjustDifficultyBasedOnPerformance()
    {
        if (_totalAttemptsExecuted == 0) return;

        _totalAttemptsLanded = 0;
        _totalAttemptsExecuted = 0;
    }

    #endregion
}