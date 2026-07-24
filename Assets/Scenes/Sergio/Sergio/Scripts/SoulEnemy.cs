using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class SoulEnemy : MonoBehaviour
{
    [Header("Patrolling")]
    public int numberOfWaypoints = 4;
    public float patrolRadius = 15f;
    public float baseSpeed = 4f;
    private List<Transform> patrolPoints = new List<Transform>();
    private int currentPointIndex = 0;

    [Header("Detection & Chase")]
    public Transform target;
    public float detectionRange = 10f;
    public float chaseSpeed = 8f;

    [Header("Attack Logic (Bite)")]
    public float attackDistance = 1.8f;
    public float paralyzeDuration = 2f;
    public float attackCooldown = 2f;
    private bool isAttacking = false;

    [Header("Collision Paralysis")]
    public float collisionParalyzeDuration = 2f;
    public float collisionParalyzeCooldown = 2f;
    private bool canParalyzeByCollision = true;

    [Header("Death Effects")]
    public float explosionForce = 7f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private EnemyHealth healthSystem;
    private bool isDead = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        healthSystem = GetComponent<EnemyHealth>();

        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (healthSystem != null)
        {
            healthSystem.SubscribeToDeath(HandleDeath);
        }

        if (target == null)
            target = GameObject.FindGameObjectWithTag("Player")?.transform;

        GeneratePatrolWaypoints();
        if (patrolPoints.Count > 0) GoToNextPatrolPoint();
    }

    void Update()
    {
        if (isDead || target == null || !agent.enabled || !agent.isOnNavMesh) return;

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance <= detectionRange)
        {
            ChasePlayer();
            if (distance <= attackDistance && !isAttacking)
            {
                StartCoroutine(ExecuteBite());
            }
        }
        else
        {
            Patrol();
        }
    }

    #region Movimiento y Patrulla
    void GeneratePatrolWaypoints()
    {
        GameObject holder = new GameObject(gameObject.name + "_Waypoints");

        for (int i = 0; i < numberOfWaypoints; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
            randomDirection += transform.position;
            NavMeshHit hit;

            if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, NavMesh.AllAreas))
            {
                GameObject wp = new GameObject("WP_" + i);
                wp.transform.position = hit.position;
                wp.transform.SetParent(holder.transform);
                patrolPoints.Add(wp.transform);
            }
            else { i--; } 
        }
    }

    void Patrol()
    {
        if (patrolPoints.Count == 0 || isAttacking || !agent.isOnNavMesh) return;

        agent.isStopped = false;
        agent.speed = baseSpeed;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            GoToNextPatrolPoint();
        }
    }

    void GoToNextPatrolPoint()
    {
        if (agent.isOnNavMesh && patrolPoints.Count > 0)
        {
            agent.destination = patrolPoints[currentPointIndex].position;
            currentPointIndex = (currentPointIndex + 1) % patrolPoints.Count;
        }
    }

    void ChasePlayer()
    {
        if (isAttacking) return;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(target.position);
        }
    }
    #endregion

    #region Ataque y Parálisis
    IEnumerator ExecuteBite()
    {
        isAttacking = true;
        agent.isStopped = true;
        agent.updateRotation = false;

        Debug.Log("<color=blue>SoulEnemy:</color> ¡Mordisco! Paralizando jugador.");

        PlayerMovement playerMove = target.GetComponent<PlayerMovement>();
        if (playerMove != null)
        {
            playerMove.SetCanMove(false);
            playerMove.DisableDashForDuration(paralyzeDuration);
            yield return new WaitForSeconds(paralyzeDuration);
            playerMove.SetCanMove(true);
        }

        yield return new WaitForSeconds(attackCooldown);

        agent.updateRotation = true;
        agent.isStopped = false;
        isAttacking = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canParalyzeByCollision || isDead) return;
        if (!other.CompareTag("Player")) return;

        PlayerMovement playerMove = other.GetComponent<PlayerMovement>();
        if (playerMove != null)
        {
            StartCoroutine(ParalyzePlayerOnCollision(playerMove));
        }
    }

    private IEnumerator ParalyzePlayerOnCollision(PlayerMovement playerMove)
    {
        canParalyzeByCollision = false;

        playerMove.SetCanMove(false);
        playerMove.DisableDashForDuration(collisionParalyzeDuration);
        yield return new WaitForSeconds(collisionParalyzeDuration);
        playerMove.SetCanMove(true);

        yield return new WaitForSeconds(collisionParalyzeCooldown);
        canParalyzeByCollision = true;
    }
    #endregion

    #region Muerte y Explosión
    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        agent.enabled = false;

        GameObject holder = GameObject.Find(gameObject.name + "_Waypoints");
        if (holder != null) Destroy(holder);

        ExplodeIntoPieces();
    }

    void ExplodeIntoPieces()
    {
        Transform[] pieces = GetComponentsInChildren<Transform>();

        foreach (Transform piece in pieces)
        {
            if (piece == transform) continue;

            piece.SetParent(null);

            Rigidbody pRb = piece.gameObject.GetComponent<Rigidbody>();

            if (pRb == null)
            {
                pRb = piece.gameObject.AddComponent<Rigidbody>();
            }

            pRb.isKinematic = false;
            pRb.useGravity = true;

            if (piece.gameObject.GetComponent<Collider>() == null)
            {
                MeshCollider mCol = piece.gameObject.AddComponent<MeshCollider>();
                mCol.convex = true; 
            }

            Vector3 forceDir = new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 1.5f), Random.Range(-1f, 1f));
            pRb.AddForce(forceDir * explosionForce, ForceMode.Impulse);

            Destroy(piece.gameObject, 5f);
        }

        Destroy(gameObject, 0.05f);
    }

    void OnDestroy()
    {
        if (healthSystem != null)
        {
            healthSystem.UnsubscribeFromDeath(HandleDeath);
        }
    }
    #endregion
}