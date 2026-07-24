using UnityEngine;
public class CurativeLarva : BaseLarva
{
    #region 

    [Header("Healing")]
    [SerializeField] private float baseHealAmount = 15f;
    [SerializeField] private float healingRadius = 0.75f;
    [SerializeField] private float stopDistance = 0.5f;

    [Header("Detection & Grace Time")]
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private float loseTargetGraceTime = 1.5f;

    [Header("Spawn Protection")]
    [SerializeField] private float healActivationDelay = 0.5f;

    #endregion

    #region Private

    private PlayerHealth playerHealth;
    private Transform playerTransform;
    private float calculatedHealAmount;
    private bool hasHealed = false;

    private float graceTimer = 0f;
    private float spawnTime;

    #endregion

    #region Initialization

    public void Initialize(float originalEnemyBaseHealth)
    {
        calculatedHealAmount = baseHealAmount;
    }

    #endregion

    #region Overrides

    protected override void Awake()
    {
        base.Awake();

        playerHealth = FindAnyObjectByType<PlayerHealth>();

        if (playerHealth != null) playerTransform = playerHealth.transform;

        if (agent != null) agent.stoppingDistance = stopDistance;
        if (calculatedHealAmount == 0) calculatedHealAmount = baseHealAmount;
        spawnTime = Time.time;
    }

    protected override bool HasTarget()
    {
        if (playerTransform == null) return false;

        bool isInRange = Vector3.Distance(transform.position, playerTransform.position) <= detectionRadius;

        return isInRange || graceTimer > 0f;
    }

    protected override void SearchForTarget()
    {
        if (playerTransform == null) return;

        bool isInRange = Vector3.Distance(transform.position, playerTransform.position) <= detectionRadius;

        if (isInRange)
        {
            graceTimer = loseTargetGraceTime;
            currentState = LarvaState.Chase;
        }
        else
        {
            if (currentState == LarvaState.Chase)
            {
                graceTimer -= Time.deltaTime;

                if (graceTimer <= 0f)
                {
                    currentState = LarvaState.Idle;

                    if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                    {
                        agent.ResetPath();
                        agent.speed = wanderSpeed;
                    }
                }
            }
        }
    }

    protected override void OnChaseUpdate()
    {
        if (hasHealed || playerTransform == null) return;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = moveSpeed;
            agent.SetDestination(playerTransform.position);
        }
        if (CanHeal() && Vector3.Distance(transform.position, playerTransform.position) <= healingRadius)
            HealAndDie();
    }

    protected override void OnUpdate()
    {
        if (currentState == LarvaState.Chase)
            OnChaseUpdate();
    }

    protected override void OnDieAction() { }

    #endregion

    #region Trigger

    private void OnTriggerEnter(Collider other)
    {
        if (!hasHealed && CanHeal() && other.CompareTag("Player"))
            HealAndDie();
    }

    #endregion

    #region Healing

    private bool CanHeal()
    {
        return Time.time - spawnTime >= healActivationDelay;
    }
    private void HealAndDie()
    {
        if (hasHealed) return;
        hasHealed = true;

        playerHealth?.Heal(calculatedHealAmount);

        if (agent != null) agent.enabled = false;
        Destroy(gameObject);
    }

    #endregion

    #region Gizmos

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healingRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }

    #endregion
}