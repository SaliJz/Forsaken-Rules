using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class SlimeExploderAI : MonoBehaviour
{
    #region Configuration

    [Header("Configuración del Movimiento")]
    [SerializeField] private float speed = 7f;
    [SerializeField] private float stoppingDistance = 1.5f;

    [Header("Configuración de la Explosión")]
    [SerializeField] private float anticipationTime = 0.5f;
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private float explosionDamage = 10f;
    [SerializeField] private LayerMask damageLayer;

    #endregion

    #region State

    private NavMeshAgent agent;
    private Transform targetPlayer;
    private EnemyHealth healthComponent;
    private bool isExploding = false;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        healthComponent = GetComponent<EnemyHealth>();

        agent.speed = speed;
        agent.stoppingDistance = stoppingDistance;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            targetPlayer = playerObj.transform;
        }
    }

    private void Update()
    {
        if (targetPlayer == null || isExploding || agent.isStopped) return;

        agent.SetDestination(targetPlayer.position);

        if (Vector3.Distance(transform.position, targetPlayer.position) <= stoppingDistance)
        {
            StartExplosionSequence();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (explosionRadius > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }

    #endregion

    #region Explosion Logic

    private void StartExplosionSequence()
    {
        if (isExploding) return;

        isExploding = true;
        agent.isStopped = true;

        StartCoroutine(ExplosionRoutine());
    }

    private IEnumerator ExplosionRoutine()
    {
        yield return new WaitForSeconds(anticipationTime);

        Explode();
    }

    private void Explode()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, damageLayer);
        foreach (var hit in hits)
        {
            PlayerHealth playerH = hit.GetComponent<PlayerHealth>();
            if (playerH != null)
            {
                playerH.TakeDamage(explosionDamage);
            }
        }

        if (healthComponent != null)
        {
            healthComponent.Die(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion
}