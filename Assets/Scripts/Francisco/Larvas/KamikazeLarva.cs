using UnityEngine;

public class KamikazeLarva : BaseLarva
{
    #region Settings

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Explosion")]
    [SerializeField] private GameObject explosionAreaPrefab;
    [SerializeField] private float explosionTriggerRange = 1.2f;

    #endregion

    #region Private

    private Transform targetEnemy;
    private bool isDetonating = false;

    #endregion

    #region Overrides

    protected override bool HasTarget() => targetEnemy != null;

    protected override void SearchForTarget()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);
        float closestDist = Mathf.Infinity;
        targetEnemy = null;

        foreach (var col in cols)
        {
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                targetEnemy = col.transform;
            }
        }
    }

    protected override void OnChaseUpdate()
    {
        if (targetEnemy == null) return;

        if (agent != null && agent.isOnNavMesh)
            agent.SetDestination(targetEnemy.position);

        if (!isInvulnerable && Vector3.Distance(transform.position, targetEnemy.position) <= explosionTriggerRange)
            Explode();
    }

    protected override void OnUpdate()
    {
        if (isDetonating) return;

        if (currentState == LarvaState.Chase)
            OnChaseUpdate();
    }

    protected override void OnDieAction()
    {
        if (!isDetonating && explosionAreaPrefab != null)
            Instantiate(explosionAreaPrefab, transform.position, Quaternion.identity);
    }

    #endregion

    #region Trigger

    private void OnTriggerEnter(Collider other)
    {
        if (isInvulnerable || isDetonating) return;
        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
            Explode();
    }

    #endregion

    #region Explosion

    private void Explode()
    {
        if (isDetonating || isInvulnerable) return;
        isDetonating = true;

        if (explosionAreaPrefab != null)
            Instantiate(explosionAreaPrefab, transform.position, Quaternion.identity);

        if (agent != null) agent.enabled = false;
        Destroy(gameObject);
    }

    #endregion

    #region Gizmos

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = isInvulnerable ? Color.blue : Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionTriggerRange);
    }

    #endregion
}