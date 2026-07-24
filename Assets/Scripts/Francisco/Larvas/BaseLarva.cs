using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public abstract class BaseLarva : MonoBehaviour
{
    #region Settings

    [Header("Movement")]
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected float lifetime = 5f;

    [Header("Idle Wander")]
    [SerializeField] protected float wanderRadius = 3f;
    [SerializeField] protected float wanderInterval = 2f;
    [SerializeField] protected float wanderSpeed = 2f;

    [Header("Invulnerability")]
    [SerializeField] protected float invulnerabilityTime = 1f;

    [Header("Rotation")]
    [SerializeField] protected float rotationSpeed = 8f;
    [SerializeField] protected float rotationOffset = 0f;

    #endregion

    #region State

    protected enum LarvaState { Idle, Chase }
    protected LarvaState currentState = LarvaState.Idle;

    protected NavMeshAgent agent;
    protected bool isDead = false;
    protected bool isInvulnerable = true;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = wanderSpeed;
    }

    protected virtual void Start()
    {
        StartCoroutine(InvulnerabilityRoutine());
        StartCoroutine(LifetimeRoutine());
        StartCoroutine(WanderRoutine());
        StartCoroutine(TargetSearchRoutine());
    }

    protected virtual void Update()
    {
        if (isDead) return;
        OnUpdate();
        RotateTowardsMovement();
    }

    protected abstract void OnUpdate();

    #endregion

    #region State Management

    protected void SetState(LarvaState newState)
    {
        if (currentState == newState) return;
        currentState = newState;

        if (agent != null)
            agent.speed = currentState == LarvaState.Chase ? moveSpeed : wanderSpeed;
    }

    protected abstract bool HasTarget();
    protected abstract void OnChaseUpdate();
    protected abstract void SearchForTarget();
    protected abstract void OnDieAction();

    #endregion

    #region Wander

    private IEnumerator WanderRoutine()
    {
        while (!isDead)
        {
            if (currentState == LarvaState.Idle)
            {
                Vector3 wanderPoint = GetRandomNavMeshPoint();
                if (wanderPoint != Vector3.zero && agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(wanderPoint);
                }
            }
            yield return new WaitForSeconds(wanderInterval);
        }
    }

    private Vector3 GetRandomNavMeshPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * wanderRadius + transform.position;
        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            return hit.position;
        return Vector3.zero;
    }

    #endregion

    #region Search Loop

    private IEnumerator TargetSearchRoutine()
    {
        while (!isDead)
        {
            if (!HasTarget())
            {
                SearchForTarget();
                if (!HasTarget())
                    SetState(LarvaState.Idle);
            }
            else
            {
                SetState(LarvaState.Chase);
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    #endregion

    #region Invulnerability

    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityTime);
        isInvulnerable = false;
    }

    #endregion

    #region Lifetime

    private IEnumerator LifetimeRoutine()
    {
        float timer = 0f;

        while (!isDead)
        {
            if (HasTarget())
            {
                timer = 0f;
            }
            else
            {
                timer += Time.deltaTime;
                if (timer >= lifetime) { Die(); yield break; }
            }

            yield return null;
        }
    }

    #endregion

    #region Death

    protected void Die()
    {
        if (isDead) return;
        isDead = true;

        if (agent != null) agent.enabled = false;
        OnDieAction();
        Destroy(gameObject);
    }

    #endregion

    #region Rotation

    private void RotateTowardsMovement()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        Vector3 velocity = agent.velocity;
        if (velocity.sqrMagnitude < 0.01f) return;

        Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
        Quaternion targetRotation = Quaternion.LookRotation(flatVelocity) * Quaternion.Euler(0f, rotationOffset, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }

    #endregion

    #region Gizmos

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, wanderRadius);
    }

    #endregion
}