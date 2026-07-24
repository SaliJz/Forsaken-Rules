using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class OveruseEnemyAI : MonoBehaviour
{
    #region Enums

    private enum EnemyState
    {
        Patrolling,
        JumpingChase,
        Dead
    }

    #endregion

    #region Inspector Fields

    [Header("Detection")]
    [SerializeField] private float chaseDetectionRadius = 10f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Base Stats")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Patrol Settings")]
    private Vector3[] dynamicPatrolPoints;
    [SerializeField] private float patrolRadius = 8f;
    [SerializeField] private int dynamicWaypointCount = 4;
    [SerializeField] private float waypointReachedDistance = 1.0f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpDistance = 5f;
    [SerializeField] private float jumpDuration = 0.6f;
    [SerializeField] private float groundedDuration = 2.5f;
    [SerializeField] private float riseImpulseDuration = 1f;
    [SerializeField] private float jumpArcHeight = 2f;

    [Header("Rotation Per Jump")]
    [SerializeField] private float rotationPerJump = 90f;

    [Header("NavMesh")]
    [SerializeField] private float navMeshSampleDistance = 2f;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawGizmos = true;

    #endregion

    #region Private State

    private EnemyState currentState = EnemyState.Patrolling;

    private NavMeshAgent agent;
    private EnemyHealth enemyHealth;
    private OveruseLuminaryFall luminaryFall;
    private Animator animator;

    private Transform playerTransform;

    private int currentWaypointIndex = 0;
    private bool isJumping = false;
    private bool isGrounded = true;

    private Coroutine jumpRoutine;
    private Coroutine stateRoutine;

    private float accumulatedJumpRotation = 0f;

    #endregion

    #region Animator Hashes

    private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int AnimIsJumping = Animator.StringToHash("IsJumping");
    private static readonly int AnimIsLanding = Animator.StringToHash("IsLanding");

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemyHealth = GetComponent<EnemyHealth>();
        luminaryFall = GetComponent<OveruseLuminaryFall>();
        animator = GetComponentInChildren<Animator>();

        agent.speed = moveSpeed;
        agent.updateRotation = false;
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            Log("Player not found in scene. Detection will not work.", 2);
        }

        enemyHealth.OnDeath += HandleDeath;

        GenerateDynamicPatrolPoints();
        EnterState(EnemyState.Patrolling);
    }

    private void Update()
    {
        if (currentState == EnemyState.Dead) return;

        CheckPlayerDetection();
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
            enemyHealth.OnDeath -= HandleDeath;
    }

    #endregion

    #region State Machine

    private void EnterState(EnemyState newState)
    {
        if (currentState == EnemyState.Dead) return;

        if (stateRoutine != null)
        {
            StopCoroutine(stateRoutine);
            stateRoutine = null;
        }

        if (jumpRoutine != null)
        {
            StopCoroutine(jumpRoutine);
            jumpRoutine = null;
        }

        currentState = newState;

        Log($"State changed > {newState}", 1);

        switch (newState)
        {
            case EnemyState.Patrolling:
                stateRoutine = StartCoroutine(PatrolRoutine());
                break;

            case EnemyState.JumpingChase:
                stateRoutine = StartCoroutine(JumpingChaseRoutine());
                break;
        }
    }

    private void CheckPlayerDetection()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        bool playerInRange = distanceToPlayer <= chaseDetectionRadius;

        if (playerInRange && currentState == EnemyState.Patrolling)
        {
            EnterState(EnemyState.JumpingChase);
        }
        else if (!playerInRange && currentState == EnemyState.JumpingChase && !isJumping)
        {
            EnterState(EnemyState.Patrolling);
        }
    }

    #endregion

    #region Patrol State

    private void GenerateDynamicPatrolPoints()
    {
        float safeRadius = Mathf.Min(patrolRadius, jumpDistance * 2f);
        dynamicPatrolPoints = new Vector3[dynamicWaypointCount];
        int pointsFound = 0;

        for (int i = 0; i < dynamicWaypointCount * 5 && pointsFound < dynamicWaypointCount; i++)
        {
            Vector2 circle = Random.insideUnitCircle * safeRadius;
            Vector3 randomPoint = transform.position + new Vector3(circle.x, 0f, circle.y);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                dynamicPatrolPoints[pointsFound] = hit.position;
                pointsFound++;
            }
        }

        if (pointsFound < dynamicWaypointCount)
            System.Array.Resize(ref dynamicPatrolPoints, pointsFound);

        Log($"Generated {pointsFound} patrol points within radius {safeRadius}m.", 1);
    }

    private IEnumerator PatrolRoutine()
    {
        if (dynamicPatrolPoints == null || dynamicPatrolPoints.Length == 0)
        {
            Log("No patrol points available.", 2);
            yield break;
        }

        SetAnimatorMoving(true);

        while (currentState == EnemyState.Patrolling)
        {
            Vector3 targetPos = dynamicPatrolPoints[currentWaypointIndex];
            float distToTarget = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(targetPos.x, 0f, targetPos.z));

            if (distToTarget <= waypointReachedDistance)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % dynamicPatrolPoints.Length;
                Log($"Waypoint reached. Next index: {currentWaypointIndex}", 1);
                continue;
            }

            yield return StartCoroutine(JumpTowardsTarget(targetPos, isChasing: false));
            yield return StartCoroutine(GroundedPhase());
        }

        SetAnimatorMoving(false);
    }

    #endregion

    #region Jumping Chase State

    private IEnumerator JumpingChaseRoutine()
    {
        SetAnimatorMoving(true);

        while (currentState == EnemyState.JumpingChase)
        {
            if (playerTransform == null) break;

            yield return StartCoroutine(RiseImpulsePhase());

            Vector3 targetPosition = playerTransform.position;
            yield return StartCoroutine(JumpTowardsTarget(targetPosition, isChasing: true));

            yield return StartCoroutine(GroundedPhase());
        }

        SetAnimatorMoving(false);
    }

    #endregion

    #region Jump Logic

    private IEnumerator JumpTowardsTarget(Vector3 targetPosition, bool isChasing)
    {
        isJumping = true;
        SetAnimatorJumping(true);

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        agent.enabled = false;

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        Vector3 direction = (targetPosition - startPosition);
        direction.y = 0f;
        float distanceToTarget = direction.magnitude;
        float clampedDistance = Mathf.Min(distanceToTarget, jumpDistance);
        Vector3 jumpEndPosition = startPosition + direction.normalized * clampedDistance;

        float targetYRotation = isChasing
            ? Quaternion.LookRotation(direction.normalized).eulerAngles.y + accumulatedJumpRotation
            : transform.eulerAngles.y;
        Quaternion endRotation = Quaternion.Euler(0f, targetYRotation, 0f);

        float timer = 0f;

        while (timer < jumpDuration)
        {
            float t = timer / jumpDuration;
            float arcOffset = Mathf.Sin(t * Mathf.PI) * jumpArcHeight;

            Vector3 flatPosition = Vector3.Lerp(startPosition, jumpEndPosition, t);
            flatPosition.y += arcOffset;

            transform.position = flatPosition;
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            timer += Time.deltaTime;
            yield return null;
        }

        transform.position = jumpEndPosition;
        transform.rotation = endRotation;

        if (isChasing)
            accumulatedJumpRotation += rotationPerJump;

        SnapToNavMesh(jumpEndPosition);

        SetAnimatorJumping(false);
        SetAnimatorLanding(true);

        isJumping = false;
        Log($"Landed at {jumpEndPosition}", 1);
    }

    private void SnapToNavMesh(Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            agent.enabled = true;
            agent.Warp(hit.position);

            if (agent.isOnNavMesh)
                agent.isStopped = false;
        }
        else
        {
            transform.position = position;
            agent.enabled = true;
            Log($"Could not snap to NavMesh at {position}.", 2);
        }
    }

    #endregion

    #region Grounded & Rise Phases

    private IEnumerator GroundedPhase()
    {
        isGrounded = true;
        SetAnimatorLanding(false);

        luminaryFall?.OnLanded();

        Log($"Grounded for {groundedDuration}s.", 1);
        yield return new WaitForSeconds(groundedDuration);

        isGrounded = false;
    }

    private IEnumerator RiseImpulsePhase()
    {
        Log($"Rising for {riseImpulseDuration}s.", 1);
        yield return new WaitForSeconds(riseImpulseDuration);
    }

    #endregion

    #region Death

    private void HandleDeath(GameObject deadEnemy)
    {
        EnterState(EnemyState.Dead);

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        Log("Enemy died. AI disabled.", 1);
    }

    #endregion

    #region Animator Helpers

    private void SetAnimatorMoving(bool value)
    {
        if (animator != null) animator.SetBool(AnimIsMoving, value);
    }

    private void SetAnimatorJumping(bool value)
    {
        if (animator != null) animator.SetBool(AnimIsJumping, value);
    }

    private void SetAnimatorLanding(bool value)
    {
        if (animator != null) animator.SetBool(AnimIsLanding, value);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = new Color(1f, 0.8f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, chaseDetectionRadius);

        if (dynamicPatrolPoints == null) return;

        Gizmos.color = Color.cyan;
        foreach (Vector3 point in dynamicPatrolPoints)
        {
            Gizmos.DrawSphere(point, 0.3f);
        }
    }

    #endregion

    #region Debug

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void Log(string message, int level)
    {
        switch (level)
        {
            case 1: Debug.Log($"[OveruseEnemyAI] {message}"); break;
            case 2: Debug.LogWarning($"[OveruseEnemyAI] {message}"); break;
            case 3: Debug.LogError($"[OveruseEnemyAI] {message}"); break;
        }
    }

    #endregion
}