using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class BaalController : MonoBehaviour
{
    #region Public Enums and State
    public enum BossState
    {
        Moving,
        Attacking,
        Recovering,
        Stunned
    }

    [Header("Boss State")]
    [SerializeField] private BossState _currentState = BossState.Moving;
    private BossState _previousState = BossState.Moving;
    #endregion

    #region General Settings
    [Header("Player Settings")]
    [SerializeField] private Transform _player;

    [Header("Movement Settings")]
    [SerializeField] private float _stoppingDistance = 5f;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _accelerationDistance = 10f;
    [SerializeField] private float _acceleratedSpeed = 10f;
    [SerializeField] private float _maxPursuitTime = 5f;
    private float _distanceTimer;
    private NavMeshAgent _navMeshAgent;
    private Animator _animator;
    private HealthController _healthController;

    [Header("Damage Settings")]
    [SerializeField] private float _attack1Damage = 10f;
    [SerializeField] private float _attack2Damage = 15f;
    [SerializeField] private float _attack3Damage = 25f;
    [SerializeField] private float _attack4Damage = 30f;
    private PlayerHealth _playerHealth;

    [Header("Attack Probability Settings")]
    [SerializeField] private float _closeRangeThreshold = 6f;
    [SerializeField] private float _midRangeThreshold = 15f;
    [SerializeField] private int _baseAttack1Weight = 10;
    [SerializeField] private int _baseAttack2Weight = 10;
    [SerializeField] private int _baseAttack3Weight = 10;
    [SerializeField] private int _distanceWeightBonus = 50;
    #endregion

    #region Color Settings
    [Header("Color Settings")]
    [SerializeField] private Renderer _bossRenderer;
    [SerializeField] private Color _movingColor = Color.white;
    [SerializeField] private Color _attackingColor = Color.red;
    [SerializeField] private Color _recoveringColor = Color.cyan;
    [SerializeField] private Color _stunnedColor = Color.green;
    private BossState _lastColorState;
    #endregion

    #region Attack 1: Side Slash Combo
    [Header("Attack 1: Combo de Tajo Lateral")]
    [SerializeField] private Transform _slashEffectTransform;
    [SerializeField] private float _slashDistance = 5f;
    [SerializeField] private float _attack1Cooldown = 5f;
    [SerializeField] private float _recoveryTime = 2f;
    [SerializeField] private int _comboCount = 3;
    [SerializeField] private float _timeBetweenSlashes = 0.5f;
    private float _attack1Timer;
    private Vector3 _hitEffectPosition;
    private float _hitEffectDuration = 0.2f;
    private float _hitEffectTimer;
    private Vector3 _lastRaycastOrigin;
    private Vector3 _lastRaycastDirection;
    #endregion

    #region Attack 2: Summon Staff & Swarm
    [Header("Attack 2: Invocación de Báculo y Enjambre")]
    [SerializeField] private Transform _staffTransform;
    [SerializeField] private StaffKeyframe[] _staffAnimationKeyframes;
    [SerializeField] private float _attack2Cooldown = 10f;
    [SerializeField] private float _swarmCastRadius = 1f;
    [SerializeField] private float _swarmCastDistance = 20f;
    [SerializeField] private float _swarmHitboxMoveSpeed = 10f;
    [SerializeField] private Transform _swarmVisualReference;
    [SerializeField] private int _swarmAttackCount = 1;
    [SerializeField] private float _timeBetweenSwarmAttacks = 1.5f;
    [SerializeField] private float _swarmReturnSpeed = 20f;
    [SerializeField] private int _maxRetargetAttempts = 1;
    private float _attack2Timer;
    private List<Vector3> _swarmPath = new List<Vector3>();

    [Header("Attack 2: VFXs")]
    [Header("VFX - Staff")]
    [SerializeField] private ParticleSystem _staffGlowVFX;
    [Header("VFX - Swarm")]
    [SerializeField] private ParticleSystem _swarmVFX;
    #endregion

    #region Attack 3: Perfect Hit - Teleport
    [Header("Attack 3: Golpe Perfecto")]
    [SerializeField] private float _attack3Cooldown = 8f;
    [SerializeField] private float _teleportHitDelayMin = 0.5f;
    [SerializeField] private float _teleportHitDelayMax = 1.5f;
    [SerializeField] private float _teleportDashSpeed = 20f;
    [SerializeField] private float _teleportDashDistance = 3f;
    [SerializeField] private GameObject _teleportVFXInstance;
    [SerializeField] private GameObject _teleportHitboxVisual;
    [SerializeField] private Vector3 _teleportHitboxSize = new Vector3(1.5f, 2f, 3f);
    [SerializeField] private float _teleportCastRadius = 1.0f;
    private float _attack3Timer;

    #endregion

    #region Attack 4: Lord of Flies 
    [Header("Attack 4: Señor de las Moscas")]
    [SerializeField] private GameObject _insectColumnWarningPrefab;
    [SerializeField] private GameObject _insectColumnPrefab;
    [SerializeField] private int _randomColumnsCount = 5;
    [SerializeField] private float _areaRandomRadius = 10f;
    [SerializeField] private float _warningDuration = 1.0f;
    [SerializeField] private bool _simultaneousWarning = false;
    [SerializeField] private float _columnDuration = 1.0f;
    [SerializeField] private float _columnCastRadius = 1.0f;
    [SerializeField] private float _columnCastHeight = 5.0f;
    [SerializeField] private bool _lordOfFliesIsUninterruptible = false;

    private bool _phase1Used = false;
    private bool _phase2Used = false;
    private Coroutine _areaAttackCoroutine;
    private List<GameObject> _activeInstantiatedEffects = new List<GameObject>();
    private List<Vector3> _columnGizmoPositions = new List<Vector3>();
    #endregion

    #region VFX and Animation Keyframes
    [Header("VFX")]
    [SerializeField] private TrailRenderer _trailRenderer;

    [System.Serializable]
    public struct StaffKeyframe
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;
        public float Time;
    }
    #endregion

    #region Unity Lifecycle Methods
    void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _healthController = GetComponent<HealthController>();

        if (_player == null)
        {
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
            {
                _player = playerGO.transform;
                _playerHealth = playerGO.GetComponent<PlayerHealth>();
            }
        }
        else
        {
            _playerHealth = _player.GetComponent<PlayerHealth>();
        }

        if (_healthController == null)
        {
            Debug.LogError("HealthController no encontrado en el objeto de Baal.");
        }
        else
        {
            _healthController.OnOverwhelmed += StunBoss;
            _healthController.OnRecovered += UnStunBoss;
        }

        if (_bossRenderer == null)
        {
            _bossRenderer = GetComponentInChildren<Renderer>();
            if (_bossRenderer == null) Debug.LogError("Renderer no encontrado en Baal.");
        }

        _attack1Timer = _attack1Cooldown;
        _attack2Timer = _attack2Cooldown;
        _attack3Timer = _attack3Cooldown;
        _navMeshAgent.updateRotation = false;
        _distanceTimer = 0f;

        if (_staffTransform != null && _staffAnimationKeyframes.Length > 0)
        {
            _staffTransform.localPosition = _staffAnimationKeyframes[0].Position;
            _staffTransform.localRotation = Quaternion.Euler(_staffAnimationKeyframes[0].Rotation);
            _staffTransform.localScale = Vector3.Lerp(_staffAnimationKeyframes[0].Scale, _staffAnimationKeyframes[0].Scale, 0);
            _staffTransform.gameObject.SetActive(false);
        }

        if (_teleportVFXInstance != null)
        {
            _teleportVFXInstance.SetActive(false);
        }

        if (_teleportHitboxVisual != null)
        {
            _teleportHitboxVisual.SetActive(false);
        }

        UpdateBossColor();
    }

    void Update()
    {
        if (_player == null || (_healthController != null && _healthController.GetTotalLife() <= 0))
        {
            return;
        }

        switch (_currentState)
        {
            case BossState.Moving:
                HandleMovement();
                break;
            case BossState.Attacking:
                break;
            case BossState.Recovering:
                HandleRecovery();
                break;
            case BossState.Stunned:
                _navMeshAgent.isStopped = true;
                break;
        }

        _hitEffectTimer -= Time.deltaTime;
        UpdateCooldowns();
        CheckPhaseAttacks();
        UpdateBossColor();
    }

    void OnDrawGizmos()
    {

        if (_player != null)
        {
            if (_currentState == BossState.Moving && Vector3.Distance(transform.position, _player.position) <= _stoppingDistance)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, _stoppingDistance);
            }
        }

        if (_hitEffectTimer > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_hitEffectPosition, 0.5f);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(_lastRaycastOrigin, _hitEffectPosition - _lastRaycastOrigin);
        }

        if (_swarmPath.Count > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _swarmPath.Count - 1; i++)
            {
                Gizmos.DrawLine(_swarmPath[i], _swarmPath[i + 1]);
            }

            Gizmos.color = Color.magenta;
            if (_swarmVisualReference != null) Gizmos.DrawWireSphere(_swarmVisualReference.position, _swarmCastRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_swarmPath[0], 0.3f);
        }

        if (_swarmVisualReference != null && _swarmVisualReference.gameObject.activeInHierarchy && _swarmPath.Count == 0)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_swarmVisualReference.position, _swarmCastRadius);
        }

        if (Application.isPlaying && _currentState == BossState.Attacking)
        {
            if (_teleportHitboxVisual != null && _teleportHitboxVisual.activeInHierarchy)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.75f);
                Gizmos.DrawWireSphere(_teleportHitboxVisual.transform.position, _teleportCastRadius);
            }
        }

        if (_columnGizmoPositions.Count > 0)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
            Vector3 boxSize = new Vector3(_columnCastRadius * 2f, _columnCastHeight * 2f, _columnCastRadius * 2f);
            foreach (Vector3 pos in _columnGizmoPositions)
            {
                Gizmos.DrawWireCube(pos, boxSize);
            }
        }

    }

    private void OnDisable()
    {
        if (_healthController != null)
        {
            _healthController.OnOverwhelmed -= StunBoss;
            _healthController.OnRecovered -= UnStunBoss;
        }

        if (_swarmVFX != null)
        {
            _swarmVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (_staffGlowVFX != null)
        {
            _staffGlowVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (_teleportVFXInstance != null)
        {
            ParticleSystem ps = _teleportVFXInstance.GetComponent<ParticleSystem>();
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _teleportVFXInstance.SetActive(false);
        }

        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        if (_healthController != null)
        {
            _healthController.OnOverwhelmed -= StunBoss;
            _healthController.OnRecovered -= UnStunBoss;
        }

        if (_swarmVFX != null)
        {
            _swarmVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (_staffGlowVFX != null)
        {
            _staffGlowVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (_teleportVFXInstance != null)
        {
            ParticleSystem ps = _teleportVFXInstance.GetComponent<ParticleSystem>();
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(_teleportVFXInstance);
        }

        StopAllCoroutines();
    }
    #endregion

    #region State and Movement Logic
    private void UpdateCooldowns()
    {
        _attack1Timer -= Time.deltaTime;
        _attack2Timer -= Time.deltaTime;
        _attack3Timer -= Time.deltaTime;
    }

    private void UpdateBossColor()
    {
        if (_bossRenderer == null || _currentState == _lastColorState) return;

        Color targetColor;

        switch (_currentState)
        {
            case BossState.Moving:
                targetColor = _movingColor;
                break;
            case BossState.Attacking:
                targetColor = _attackingColor;
                break;
            case BossState.Recovering:
                targetColor = _recoveringColor;
                break;
            case BossState.Stunned:
                targetColor = _stunnedColor;
                break;
            default:
                targetColor = _movingColor;
                break;
        }

        _bossRenderer.material.color = targetColor;
        _lastColorState = _currentState;
    }

    private void CheckPhaseAttacks()
    {
        if (_healthController == null) return;

        float currentLifeRatio = (float)_healthController.CurrentHealth / _healthController.MaxHealth;

        if (!_phase1Used && currentLifeRatio <= 0.66f)
        {
            _phase1Used = true;
            _currentState = BossState.Attacking;
            _areaAttackCoroutine = StartCoroutine(LordOfFliesSequence());
        }
        else if (!_phase2Used && currentLifeRatio <= 0.33f)
        {
            _phase2Used = true;
            _currentState = BossState.Attacking;
            _areaAttackCoroutine = StartCoroutine(LordOfFliesSequence());
        }
    }

    private void HandleMovement()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        LookAtPlayer();

        bool canAttack = _attack1Timer <= 0 || _attack2Timer <= 0 || _attack3Timer <= 0;

        if (distanceToPlayer <= _stoppingDistance && canAttack)
        {
            DecideNextAttack(distanceToPlayer);
        }

        if (_currentState != BossState.Moving) return;

        if (_attack2Timer <= 0)
        {
            _distanceTimer += Time.deltaTime;
            if (_distanceTimer >= _maxPursuitTime)
            {
                _navMeshAgent.isStopped = true;
                _currentState = BossState.Attacking;
                StartCoroutine(SummonAndSwarmAttack());
                _attack2Timer = _attack2Cooldown;
                _distanceTimer = 0f;
                return;
            }
            float currentSpeed = _acceleratedSpeed;
            _navMeshAgent.speed = currentSpeed;
            _navMeshAgent.isStopped = false;
            _navMeshAgent.SetDestination(_player.position);
        }
        else
        {
            float currentSpeed = (distanceToPlayer <= _accelerationDistance) ? _acceleratedSpeed : _moveSpeed;
            _navMeshAgent.speed = currentSpeed;
            _navMeshAgent.isStopped = false;
            _navMeshAgent.SetDestination(_player.position);
        }
    }

    private void DecideNextAttack(float distanceToPlayer)
    {
        List<(int weight, int attackId)> weightedAttacks = new List<(int, int)>();

        int preferredBonus = _distanceWeightBonus;

        if (_attack1Timer <= 0)
        {
            int weight = _baseAttack1Weight;
            if (distanceToPlayer <= _closeRangeThreshold) weight += preferredBonus;
            weightedAttacks.Add((weight, 1));
        }

        if (_attack2Timer <= 0)
        {
            int weight = _baseAttack2Weight;
            if (distanceToPlayer > _closeRangeThreshold && distanceToPlayer <= _midRangeThreshold) weight += preferredBonus;
            weightedAttacks.Add((weight, 2));
        }

        if (_attack3Timer <= 0)
        {
            int weight = _baseAttack3Weight;
            if (distanceToPlayer > _midRangeThreshold) weight += preferredBonus;
            weightedAttacks.Add((weight, 3));
        }

        if (weightedAttacks.Count == 0) return;

        int totalWeight = 0;
        foreach (var attack in weightedAttacks) totalWeight += attack.weight;

        int randomIndex = UnityEngine.Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var attack in weightedAttacks)
        {
            currentWeight += attack.weight;
            if (randomIndex < currentWeight)
            {
                _navMeshAgent.isStopped = true;
                _distanceTimer = 0f;
                _currentState = BossState.Attacking;

                switch (attack.attackId)
                {
                    case 1:
                        StartCoroutine(SlashComboSequence());
                        _attack1Timer = _attack1Cooldown;
                        break;
                    case 2:
                        StartCoroutine(SummonAndSwarmAttack());
                        _attack2Timer = _attack2Cooldown;
                        break;
                    case 3:
                        StartCoroutine(PerfectHitSequence());
                        _attack3Timer = _attack3Cooldown;
                        break;
                }
                return;
            }
        }
    }

    private void HandleRecovery()
    {
    }

    private void StunBoss()
    {
        _previousState = _currentState;
        _currentState = BossState.Stunned;
        _navMeshAgent.isStopped = true;

        if (_teleportVFXInstance != null) _teleportVFXInstance.SetActive(false);
        if (_teleportHitboxVisual != null) _teleportHitboxVisual.SetActive(false);
        if (_staffTransform != null) _staffTransform.gameObject.SetActive(false);
        if (_swarmVisualReference != null) _swarmVisualReference.gameObject.SetActive(false);

        Debug.Log("Baal ha sido aturdido (Overwhelmed).");
    }

    private void UnStunBoss()
    {
        if (_currentState == BossState.Stunned)
        {
            _currentState = BossState.Recovering;
            StartCoroutine(PostStunRecovery());
            Debug.Log("Baal se ha recuperado del aturdimiento (Recovered).");
        }
    }

    private IEnumerator PostStunRecovery()
    {
        yield return new WaitForSeconds(_recoveryTime);
        _currentState = BossState.Moving;
        _navMeshAgent.isStopped = false;
    }


    private void LookAtPlayer()
    {
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * _rotationSpeed);
        }
    }
    #endregion

    #region Attack 1 Logic
    private IEnumerator SlashComboSequence()
    {
        if (_currentState == BossState.Stunned) yield break;

        _navMeshAgent.isStopped = true;
        _navMeshAgent.ResetPath();

        if (_trailRenderer != null)
        {
            _trailRenderer.Clear();
            _trailRenderer.enabled = true;
        }

        Vector3 initialLocalPosition = _slashEffectTransform.localPosition;

        for (int i = 0; i < _comboCount; i++)
        {
            if (_currentState == BossState.Stunned) yield break;

            yield return StartCoroutine(RotateToFacePlayer(_rotationSpeed));

            Vector3 direction = (_player.position - transform.position).normalized;
            if (i == 2)
            {
                direction.y = -0.5f;
                direction.Normalize();
            }

            yield return StartCoroutine(PerformSlashVisual(direction));
            _slashEffectTransform.localPosition = initialLocalPosition;
            PerformRaycastHitDetection(direction);

            if (i < _comboCount - 1)
            {
                yield return new WaitForSeconds(_timeBetweenSlashes);
            }
        }

        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = false;
        }

        if (_currentState == BossState.Stunned) yield break;

        _currentState = BossState.Recovering;
        yield return new WaitForSeconds(_recoveryTime);
        _currentState = BossState.Moving;
    }

    private IEnumerator RotateToFacePlayer(float speed)
    {
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;
        if (direction == Vector3.zero) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        while (Quaternion.Angle(transform.rotation, targetRotation) > 0.1f)
        {
            if (_currentState == BossState.Stunned) yield break;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * speed);
            yield return null;
        }
        transform.rotation = targetRotation;
    }

    private IEnumerator PerformSlashVisual(Vector3 direction)
    {
        Vector3 startLocalPosition = _slashEffectTransform.localPosition;
        Vector3 endWorldPosition = _slashEffectTransform.position + direction * _slashDistance;
        Vector3 endLocalPosition = _slashEffectTransform.parent.InverseTransformPoint(endWorldPosition);

        float duration = 0.2f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (_currentState == BossState.Stunned) yield break;

            transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            _slashEffectTransform.localPosition = Vector3.Lerp(startLocalPosition, endLocalPosition, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        _slashEffectTransform.localPosition = endLocalPosition;
    }

    private void PerformRaycastHitDetection(Vector3 direction)
    {
        _lastRaycastOrigin = transform.position;
        _lastRaycastDirection = direction;
        _hitEffectTimer = _hitEffectDuration;

        RaycastHit hit;
        if (Physics.Raycast(transform.position, direction, out hit, _slashDistance))
        {
            _hitEffectPosition = hit.point;
            if (hit.collider.CompareTag("Player"))
            {
                _playerHealth?.TakeDamage(_attack1Damage);
                Debug.Log("Jugador golpeado por el tajo!");
            }
        }
        else
        {
            _hitEffectPosition = transform.position + direction * _slashDistance;
        }
    }
    #endregion

    #region Attack 2 Logic: Summon Staff & Swarm
    private IEnumerator SummonAndSwarmAttack()
    {
        if (_currentState == BossState.Stunned) yield break;

        _navMeshAgent.isStopped = true;

        if (_staffTransform != null)
        {
            _staffTransform.gameObject.SetActive(true);
        }

        yield return StartCoroutine(RotateToFacePlayer(_rotationSpeed));
        if (_currentState == BossState.Stunned) yield break;

        if (_staffGlowVFX != null)
        {
            _staffGlowVFX.Play();
        }

        yield return StartCoroutine(PerformStaffAnimation());

        if (_staffGlowVFX != null)
        {
            _staffGlowVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (_currentState == BossState.Stunned) yield break;

        if (_staffTransform != null)
        {
            _staffTransform.gameObject.SetActive(false);
        }

        for (int i = 0; i < _swarmAttackCount; i++)
        {
            if (_currentState == BossState.Stunned) break;

            yield return StartCoroutine(RotateToFacePlayer(_rotationSpeed));
            yield return StartCoroutine(MoveSwarmHitbox());
            yield return new WaitForSeconds(_timeBetweenSwarmAttacks);
        }

        if (_currentState == BossState.Stunned) yield break;

        _currentState = BossState.Recovering;
        yield return new WaitForSeconds(_recoveryTime);
        _currentState = BossState.Moving;
    }

    private IEnumerator PerformStaffAnimation()
    {
        if (_staffTransform == null) yield break;

        for (int k = 0; k < _staffAnimationKeyframes.Length - 1; k++)
        {
            if (_currentState == BossState.Stunned) yield break;

            StaffKeyframe startKeyframe = _staffAnimationKeyframes[k];
            StaffKeyframe endKeyframe = _staffAnimationKeyframes[k + 1];

            float segmentDuration = endKeyframe.Time - startKeyframe.Time;
            float startTime = Time.time;
            while (Time.time < startTime + segmentDuration)
            {
                if (_currentState == BossState.Stunned) yield break;

                float t = (Time.time - startTime) / segmentDuration;
                _staffTransform.localPosition = Vector3.Lerp(startKeyframe.Position, endKeyframe.Position, t);
                _staffTransform.localRotation = Quaternion.Slerp(Quaternion.Euler(startKeyframe.Rotation), Quaternion.Euler(endKeyframe.Rotation), t);
                _staffTransform.localScale = Vector3.Lerp(startKeyframe.Scale, endKeyframe.Scale, t);
                yield return null;
            }
            _staffTransform.localPosition = endKeyframe.Position;
            _staffTransform.localRotation = Quaternion.Euler(endKeyframe.Rotation);
            _staffTransform.localScale = endKeyframe.Scale;
        }
    }

    private IEnumerator MoveSwarmHitbox()
    {
        if (_swarmVisualReference == null) yield break;

        _swarmPath.Clear();
        _swarmVisualReference.gameObject.SetActive(true);

        if (_swarmVFX != null)
        {
            _swarmVFX.Play();
        }

        _swarmVisualReference.position = transform.position;

        int retargetsLeft = _maxRetargetAttempts;
        bool hitPlayer = false;

        while (!hitPlayer && retargetsLeft >= 0)
        {
            Vector3 startPosition = _swarmVisualReference.position;
            Vector3 direction = (_player.position - startPosition).normalized;

            float totalDistanceToTravel = _swarmCastDistance;
            float distanceTraveled = 0f;

            while (distanceTraveled < totalDistanceToTravel)
            {
                if (_currentState == BossState.Stunned)
                {
                    yield break;
                }

                float moveDistance = _swarmHitboxMoveSpeed * Time.deltaTime;
                float remainingDistance = totalDistanceToTravel - distanceTraveled;
                moveDistance = Mathf.Min(moveDistance, remainingDistance);

                Vector3 nextPosition = _swarmVisualReference.position + direction * moveDistance;

                RaycastHit hit;

                if (Physics.SphereCast(_swarmVisualReference.position, _swarmCastRadius, direction, out hit, moveDistance))
                {
                    if (hit.collider.CompareTag("Player"))
                    {
                        _playerHealth?.TakeDamage(_attack2Damage);
                        _swarmVisualReference.position = hit.point - direction * _swarmCastRadius;
                        _hitEffectPosition = hit.point;
                        _hitEffectTimer = _hitEffectDuration;
                        Debug.Log("Jugador golpeado por el enjambre de moscas!");
                        hitPlayer = true;
                        break;
                    }
                }

                _swarmVisualReference.position = nextPosition;
                distanceTraveled += moveDistance;
                _swarmPath.Add(_swarmVisualReference.position);

                yield return null;
            }

            if (!hitPlayer && retargetsLeft > 0)
            {
                retargetsLeft--;
                yield return new WaitForSeconds(0.1f);
            }
            else
            {
                break;
            }
        }

        while (Vector3.Distance(_swarmVisualReference.position, transform.position) > 0.5f)
        {
            Vector3 returnDirection = (transform.position - _swarmVisualReference.position).normalized;
            _swarmVisualReference.position += returnDirection * _swarmReturnSpeed * Time.deltaTime;
            _swarmPath.Add(_swarmVisualReference.position);
            yield return null;
        }

        if (_swarmVFX != null)
        {
            _swarmVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        _swarmVisualReference.gameObject.SetActive(false);
    }
    #endregion

    #region Attack 3 Logic: Perfect Hit
    private IEnumerator PerfectHitSequence()
    {
        if (_currentState == BossState.Stunned) yield break;

        _navMeshAgent.isStopped = true;

        if (_teleportVFXInstance != null) _teleportVFXInstance.SetActive(true);
        ParticleSystem particleSystem = _teleportVFXInstance.GetComponent<ParticleSystem>();
        particleSystem.Play();

        Debug.Log("Golpe Perfecto: Baal golpea el suelo y se ríe.");

        float preHitDuration = 0.4f;
        yield return new WaitForSeconds(preHitDuration);

        if (_currentState == BossState.Stunned)
        {
            if (particleSystem != null) particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_teleportVFXInstance != null) _teleportVFXInstance.SetActive(false);
            yield break;
        }

        Debug.Log("Golpe Perfecto: Baal se desvanece.");

        float teleportDelay = UnityEngine.Random.Range(_teleportHitDelayMin, _teleportHitDelayMax);
        float startTime = Time.time;

        while (Time.time < startTime + teleportDelay)
        {
            if (_currentState == BossState.Stunned)
            {
                if (particleSystem != null) particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (_teleportVFXInstance != null) _teleportVFXInstance.SetActive(false);
                yield break;
            }
            yield return null;
        }

        Vector3 playerForward = _player.forward;
        Vector3 targetPosition = _player.position - playerForward * 1.5f;
        targetPosition.y = transform.position.y;

        transform.position = targetPosition;

        if (particleSystem != null) particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (_teleportVFXInstance != null) _teleportVFXInstance.SetActive(false);

        Debug.Log("Golpe Perfecto: Baal reaparece y prepara el golpe.");

        yield return StartCoroutine(RotateToFacePlayer(_rotationSpeed * 5f));
        if (_currentState == BossState.Stunned) yield break;

        if (_teleportHitboxVisual != null)
        {
            _teleportHitboxVisual.transform.position = transform.position + transform.forward * _teleportHitboxSize.z / 2f;
            _teleportHitboxVisual.transform.rotation = transform.rotation;
            _teleportHitboxVisual.transform.localScale = _teleportHitboxSize;
            _teleportHitboxVisual.SetActive(true);
        }

        Vector3 dashDirection = transform.forward;
        Vector3 dashStart = transform.position;
        Vector3 dashEnd = dashStart + dashDirection * _teleportDashDistance;

        float dashDuration = _teleportDashDistance / _teleportDashSpeed;
        float dashTime = 0f;
        bool playerHit = false;

        while (dashTime < dashDuration)
        {
            if (_currentState == BossState.Stunned)
            {
                if (_teleportHitboxVisual != null) _teleportHitboxVisual.SetActive(false);
                yield break;
            }

            Vector3 currentPosition = transform.position;
            Vector3 nextPosition = Vector3.Lerp(dashStart, dashEnd, dashTime / dashDuration);
            Vector3 dashMove = nextPosition - currentPosition;
            float dashDistance = dashMove.magnitude;

            if (dashDistance > 0 && !playerHit)
            {
                RaycastHit hit;
                if (Physics.SphereCast(currentPosition, _teleportCastRadius, dashDirection, out hit, dashDistance))
                {
                    if (hit.collider.CompareTag("Player"))
                    {
                        _playerHealth?.TakeDamage(_attack3Damage);
                        _hitEffectPosition = hit.point;
                        _hitEffectTimer = _hitEffectDuration;
                        Debug.Log("Jugador golpeado por el Golpe Perfecto (SphereCast)!");
                        playerHit = true;
                    }
                }
            }

            transform.position = nextPosition;
            dashTime += Time.deltaTime;

            if (_teleportHitboxVisual != null)
            {
                _teleportHitboxVisual.transform.position = transform.position + transform.forward * _teleportHitboxSize.z / 2f;
            }

            yield return null;
        }

        if (_teleportHitboxVisual != null)
        {
            _teleportHitboxVisual.SetActive(false);
        }

        _currentState = BossState.Recovering;
        yield return new WaitForSeconds(_recoveryTime);
        _currentState = BossState.Moving;
    }
    #endregion

    #region Attack 4 Logic: Lord of Flies
    private IEnumerator LordOfFliesSequence()
    {
        _navMeshAgent.isStopped = true;

        if (_insectColumnWarningPrefab == null || _insectColumnPrefab == null) yield break;

        _columnGizmoPositions.Clear();

        foreach (var effect in _activeInstantiatedEffects)
        {
            if (effect != null) Destroy(effect);
        }
        _activeInstantiatedEffects.Clear();

        List<Vector3> attackPositions = new List<Vector3>();
        Vector3 playerPos = _player.position;
        playerPos.y = transform.position.y;
        attackPositions.Add(playerPos);

        for (int i = 0; i < _randomColumnsCount; i++)
        {
            Vector3 randomPoint = transform.position + UnityEngine.Random.insideUnitSphere * _areaRandomRadius;
            randomPoint.y = transform.position.y;
            attackPositions.Add(randomPoint);
        }
        _columnGizmoPositions.AddRange(attackPositions);

        if (_simultaneousWarning)
        {
            List<GameObject> activeWarnings = new List<GameObject>();
            foreach (Vector3 pos in attackPositions)
            {
                GameObject warningN = Instantiate(_insectColumnWarningPrefab, pos, Quaternion.identity);
                _activeInstantiatedEffects.Add(warningN);
                activeWarnings.Add(warningN);
            }

            yield return new WaitForSeconds(_warningDuration);

            List<Coroutine> columnCoroutines = new List<Coroutine>();
            foreach (var warning in activeWarnings)
            {
                if (warning != null)
                {
                    _activeInstantiatedEffects.Remove(warning);
                    Destroy(warning);
                }
            }
            activeWarnings.Clear();

            foreach (Vector3 pos in attackPositions)
            {
                columnCoroutines.Add(StartCoroutine(SpawnAndManageColumn(pos, _columnDuration)));
            }

            foreach (var coroutine in columnCoroutines)
            {
                yield return coroutine;
            }
        }
        else
        {
            foreach (Vector3 pos in attackPositions)
            {
                if (_currentState == BossState.Stunned && !_lordOfFliesIsUninterruptible)
                {
                    break;
                }

                GameObject warningN = Instantiate(_insectColumnWarningPrefab, pos, Quaternion.identity);
                _activeInstantiatedEffects.Add(warningN);

                float warningStartTime = Time.time;
                while (Time.time < warningStartTime + _warningDuration)
                {
                    if (_currentState == BossState.Stunned && !_lordOfFliesIsUninterruptible)
                    {
                        if (warningN != null) { _activeInstantiatedEffects.Remove(warningN); Destroy(warningN); }
                        goto FinishCurrentColumnAndBreak;
                    }
                    yield return null;
                }

                if (warningN != null)
                {
                    _activeInstantiatedEffects.Remove(warningN);
                    Destroy(warningN);
                }

                yield return StartCoroutine(SpawnAndManageColumn(pos, _columnDuration));
                continue;

            FinishCurrentColumnAndBreak:
                yield return StartCoroutine(SpawnAndManageColumn(pos, _columnDuration));

                _activeInstantiatedEffects.RemoveAll(item => item == null);

                break;
            }
        }

        _columnGizmoPositions.Clear();

        if (_currentState == BossState.Stunned && !_lordOfFliesIsUninterruptible)
        {
            foreach (var effect in _activeInstantiatedEffects) { if (effect != null) Destroy(effect); }
            _activeInstantiatedEffects.Clear();
            _areaAttackCoroutine = null;
        }
        else if (_currentState != BossState.Stunned)
        {
            _currentState = BossState.Recovering;
            yield return new WaitForSeconds(_recoveryTime);
            _currentState = BossState.Moving;
        }

        _activeInstantiatedEffects.RemoveAll(item => item == null);
        _areaAttackCoroutine = null;
    }

    private IEnumerator SpawnAndManageColumn(Vector3 position, float duration)
    {
        GameObject columnN = Instantiate(_insectColumnPrefab, position, Quaternion.identity);
        _activeInstantiatedEffects.Add(columnN);

        float startTime = Time.time;
        Vector3 halfExtents = new Vector3(_columnCastRadius, _columnCastHeight, _columnCastRadius);
        Quaternion orientation = Quaternion.identity;

        while (Time.time < startTime + duration)
        {

            Collider[] hitColliders = Physics.OverlapBox(position, halfExtents, orientation);

            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("Player"))
                {
                    _playerHealth?.TakeDamage(_attack4Damage);
                    _hitEffectPosition = position;
                    _hitEffectTimer = _hitEffectDuration;
                    Debug.Log("Jugador golpeado por Columna de Moscas!");
                }
            }

            yield return null;
        }

        if (columnN != null)
        {
            _activeInstantiatedEffects.Remove(columnN);
            Destroy(columnN);
        }
    }
    #endregion
}