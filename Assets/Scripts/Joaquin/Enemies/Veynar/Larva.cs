using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(EnemyHealth))]
public class Larva : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float explosionDamage = 4f;
    [SerializeField] private float lifeTime = 10f;
    [SerializeField] private float knockbackForce = 0.5f;

    [Header("Movimiento / Deteccion")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float stoppingBuffer = 0.5f;
    [SerializeField] private float attackTriggerDistance = 5f;
    [SerializeField] private float damageRadius = 3f;

    [Header("Ataque / Salto")]
    [SerializeField] private float warningDuration = 0.6f;
    [SerializeField] private float jumpDuration = 0.5f;
    [SerializeField] private float jumpHeight = 2f;

    [Header("Agent tuning")]
    [SerializeField] private float destinationUpdateInterval = 0.18f;
    [SerializeField] private float accelMultiplier = 3f;
    [SerializeField] private float minAcceleration = 8f;
    [SerializeField] private float angularSpeed = 120f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;

    [Header("SFX")]
    [SerializeField] private AudioClip moveSFX;
    [SerializeField] private AudioClip attackExplosionSFX;
    [SerializeField] private AudioClip deathSFX;

    [Header("Behavior Options")]
    [SerializeField] private bool autoSearchPlayer = false;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private NavMeshAgent agent;
    private Transform player;
    private EnemyHealth enemyHealth;
    private EnemyVisualEffects enemyVisualEffects;

    private float moveSoundTimer;
    private float moveSoundRate = 0.4f;
    private bool hasExploded = false;
    private bool isAttacking = false;

    private float lifeTimer = 0f;
    private float lastDestinationTime = -999f;
    private bool initialized = false;

    private void Start()
    {
        if (player == null && autoSearchPlayer) FindPlayerFallback();
    }

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        enemyVisualEffects = GetComponent<EnemyVisualEffects>();

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        agent = GetComponent<NavMeshAgent>();
        if (agent != null) ConfigureAgentFromParams();
    }

    private void Update()
    {
        if (!initialized || player == null || enemyHealth == null || enemyHealth.IsDead || isAttacking) return;
        HandleMovementAudio();
    }

    private void OnEnable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath += HandleEnemyDeath;
    }

    private void OnDisable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        if (audioSource != null && deathSFX != null && !hasExploded)
        {
            audioSource.PlayOneShot(deathSFX);
        }

        if (enemyVisualEffects != null) enemyVisualEffects.CancelAnticipationBlink();

        StopAllCoroutines();

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
    }

    private void HandleMovementAudio()
    {
        if (agent != null && agent.enabled && !agent.isStopped && agent.velocity.sqrMagnitude > 0.5f)
        {
            moveSoundTimer += Time.deltaTime;
            if (moveSoundTimer >= moveSoundRate)
            {
                if (audioSource != null && moveSFX != null)
                {
                    audioSource.pitch = Random.Range(1.1f, 1.3f);
                    audioSource.PlayOneShot(moveSFX, 0.6f);
                    audioSource.pitch = 1f;
                }
                moveSoundTimer = 0f;
            }
        }
    }

    private void ConfigureAgentFromParams()
    {
        if (agent == null) return;
        agent.speed = speed;
        agent.acceleration = Mathf.Max(minAcceleration, speed * accelMultiplier);
        agent.angularSpeed = angularSpeed;
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.stoppingDistance = attackTriggerDistance + stoppingBuffer;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        agent.autoRepath = true;
        agent.enabled = true;
    }

    public void Initialize(Transform playerTransform)
    {
        if (!autoSearchPlayer) player = playerTransform ?? GameObject.FindGameObjectWithTag("Player")?.transform;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                agent.Warp(hit.position);
            }
            else
            {
                Die();
                return;
            }
        }

        if (agent != null) ConfigureAgentFromParams();
        StopAllCoroutines();
        lifeTimer = 0f;
        lastDestinationTime = -999f;
        hasExploded = false;
        isAttacking = false;

        StartCoroutine(LifeCycle());
        initialized = true;
    }

    private void FindPlayerFallback()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private IEnumerator LifeCycle()
    {
        while (lifeTimer < lifeTime)
        {
            if (enemyHealth == null || enemyHealth.IsDead) yield break;

            lifeTimer += Time.deltaTime;

            if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;

            if (player != null && agent != null && agent.isOnNavMesh && !isAttacking)
            {
                float distance = Vector3.Distance(transform.position, player.position);

                if (distance > agent.stoppingDistance)
                {
                    if (agent.isStopped) agent.isStopped = false;

                    if (Time.time >= lastDestinationTime + destinationUpdateInterval)
                    {
                        agent.SetDestination(player.position);
                        lastDestinationTime = Time.time;
                    }
                }
                else
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                    StartCoroutine(AttackSequence());
                }
            }
            yield return null;
        }
        Die();
    }

    private IEnumerator AttackSequence()
    {
        isAttacking = true;

        Collider collider = GetComponent<Collider>();
        if (collider != null) collider.isTrigger = true;

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.PlayAnticipationBlink(warningDuration);
        }
        yield return new WaitForSeconds(warningDuration);

        if (player == null || enemyHealth.IsDead) yield break;

        Vector3 startPos = transform.position;
        Vector3 targetPos = player.position;
        float timePassed = 0f;

        while (timePassed < jumpDuration)
        {
            timePassed += Time.deltaTime;
            float t = timePassed / jumpDuration;

            float currentHeight = Mathf.Sin(Mathf.PI * t) * jumpHeight;
            transform.position = Vector3.Lerp(startPos, targetPos, t) + new Vector3(0, currentHeight, 0);

            yield return null;
        }

        ExecuteExplosion();

        if (!hasExploded && enemyHealth != null && !enemyHealth.IsDead)
        {
            collider.isTrigger = false;
        }
    }

    private void ExecuteExplosion()
    {
        if (enemyHealth == null || enemyHealth.IsDead) return;

        hasExploded = true;

        if (audioSource != null && attackExplosionSFX != null)
        {
            audioSource.PlayOneShot(attackExplosionSFX);
        }

        Collider[] hitPlayer = Physics.OverlapSphere(transform.position, damageRadius, playerLayer);

        foreach (var hit in hitPlayer)
        {
            ExecuteAttack(hit.gameObject, explosionDamage);
            ApplyKnockback(hit.transform, knockbackForce);
        }

        Die();
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
                if (remainingDamage > 0f) health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
            }
            else
            {
                health.TakeDamage(Mathf.Max(0, damageAmount), false, AttackDamageType.Melee);
            }
        }
    }

    private void ApplyKnockback(Transform target, float force)
    {
        if (target.TryGetComponent<PlayerKnockbackReceiver>(out var knockbackReceiver))
        {
            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0f;
            direction.Normalize();

            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }

            knockbackReceiver.ApplyKnockback(direction, force);
        }
    }

    public void Die()
    {
        if (enemyHealth != null && !enemyHealth.IsDead) Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackTriggerDistance);
    }
}