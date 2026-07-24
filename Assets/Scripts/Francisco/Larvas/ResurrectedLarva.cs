using UnityEngine;
using System.Linq;

public class ResurrectedLarva : BaseLarva
{
    #region Settings

    [Header("Damage")]
    [SerializeField] private float damagePercentOfEnemyBase = 0.5f;
    [SerializeField] private float explosionRadius = 0.5f;
    [SerializeField] private float baseDamage = 10f;

    #endregion

    #region Private

    private Transform targetEnemy;
    private float calculatedDamage;
    private bool hasExploded = false;

    #endregion

    #region Initialization

    public void Initialize(float originalEnemyBaseHealth)
    {
        calculatedDamage = originalEnemyBaseHealth * damagePercentOfEnemyBase;
        if (calculatedDamage < baseDamage) calculatedDamage = baseDamage;
    }

    #endregion

    #region Overrides

    protected override void Awake()
    {
        base.Awake();
        if (calculatedDamage == 0) calculatedDamage = baseDamage;
    }

    protected override bool HasTarget() => targetEnemy != null;

    protected override void SearchForTarget()
    {
        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (allEnemies.Length == 0) { targetEnemy = null; return; }

        targetEnemy = allEnemies
            .Select(g => g.transform)
            .OrderBy(t => Vector3.Distance(transform.position, t.position))
            .FirstOrDefault();
    }

    protected override void OnChaseUpdate()
    {
        if (targetEnemy == null) return;

        if (agent != null && agent.isOnNavMesh)
            agent.SetDestination(targetEnemy.position);

        if (!isInvulnerable && Vector3.Distance(transform.position, targetEnemy.position) <= explosionRadius)
            DealDamageAndDie();
    }

    protected override void OnUpdate()
    {
        if (hasExploded) return;

        if (currentState == LarvaState.Chase)
            OnChaseUpdate();
    }

    protected override void OnDieAction()
    {
        if (!isInvulnerable) DealDamage();
    }

    #endregion

    #region Trigger

    private void OnTriggerEnter(Collider other)
    {
        if (isInvulnerable || hasExploded) return;
        if (other.CompareTag("Enemy")) DealDamageAndDie();
    }

    #endregion

    #region Damage

    private void DealDamageAndDie()
    {
        if (hasExploded || isInvulnerable) return;
        hasExploded = true;
        DealDamage();
        if (agent != null) agent.enabled = false;
        Destroy(gameObject);
    }

    private void DealDamage()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == gameObject) continue;

            Transform root = hitCollider.transform.root;
            if (root != null && root.CompareTag("Player")) continue;

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(baseDamage, false, AttackDamageType.Nothing);
        }
    }

    #endregion

    #region Gizmos

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        if (targetEnemy != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, targetEnemy.position);
        }

        if (isInvulnerable && Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, explosionRadius * 1.5f);
        }
    }

    #endregion
}