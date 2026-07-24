using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class ResurrectedDevilLarva : MonoBehaviour
{
    #region Settings

    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float explosionRadius = 1.2f;
    [SerializeField] private float attackTriggerDistance = 4f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float rotationOffset = 0f;

    [Header("Dash")]
    [SerializeField] private float warningDuration = 0.6f;
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private float dashDistance = 4f;

    #endregion

    #region Private

    private Transform playerTarget;
    private NavMeshAgent agent;
    private bool isExploding = false;
    private bool isAttacking = false;
    private EnemyHealth health;
    private EnemyVisualEffects enemyVisualEffects;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        enemyVisualEffects = GetComponent<EnemyVisualEffects>();
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player) playerTarget = player.transform;

        if (agent != null) agent.speed = moveSpeed;

        StartCoroutine(LifetimeRoutine());
    }

    private void OnEnable()
    {
        if (health) health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (health) health.OnDeath -= HandleDeath;
    }

    private void Update()
    {
        if (isExploding || isAttacking || (health != null && health.IsDead) || playerTarget == null || agent == null || !agent.enabled) return;

        agent.SetDestination(playerTarget.position);

        if (Vector3.Distance(transform.position, playerTarget.position) <= attackTriggerDistance)
        {
            StartCoroutine(AttackSequence());
        }

        RotateTowardsMovement();
    }

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

    #region Attack Sequence

    private IEnumerator AttackSequence()
    {
        isAttacking = true;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.PlayAnticipationBlink(warningDuration);
        }

        float warningTimer = 0f;
        while (warningTimer < warningDuration)
        {
            if (playerTarget != null)
            {
                Vector3 dirToPlayer = (playerTarget.position - transform.position).normalized;
                dirToPlayer.y = 0f;

                if (dirToPlayer != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dirToPlayer) * Quaternion.Euler(0f, rotationOffset, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
                }
            }
            warningTimer += Time.deltaTime;
            yield return null;
        }

        if (playerTarget == null || (health != null && health.IsDead)) yield break;

        Vector3 startPos = transform.position;
        Vector3 directionToPlayer = (playerTarget.position - transform.position).normalized;
        directionToPlayer.y = 0f;

        if (directionToPlayer == Vector3.zero) directionToPlayer = transform.forward;

        Vector3 targetPos = transform.position + (directionToPlayer * dashDistance);
        targetPos.y = startPos.y;

        Quaternion finalDashRotation = Quaternion.LookRotation(directionToPlayer) * Quaternion.Euler(0f, rotationOffset, 0f);
        transform.rotation = finalDashRotation;

        float timePassed = 0f;
        while (timePassed < dashDuration)
        {
            timePassed += Time.deltaTime;
            float t = timePassed / dashDuration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        DealDamageAndDie();
    }

    #endregion

    #region Death

    private void HandleDeath(GameObject obj)
    {
        isExploding = true;
        if (enemyVisualEffects != null) enemyVisualEffects.CancelAnticipationBlink();
        StopAllCoroutines();
        if (agent) agent.enabled = false;
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        if (!isExploding && (health == null || !health.IsDead)) DealDamageAndDie();
    }

    private void DealDamageAndDie()
    {
        if (isExploding) return;
        isExploding = true;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player") && hitCollider.TryGetComponent<PlayerHealth>(out var pHealth))
            {
                pHealth.TakeDamage(baseDamage);
            }
        }

        if (agent) agent.enabled = false;
        Destroy(gameObject);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackTriggerDistance);

        if (playerTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, playerTarget.position);
        }
    }

    #endregion
}