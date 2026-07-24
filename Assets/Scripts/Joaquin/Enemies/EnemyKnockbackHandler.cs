using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyKnockbackHandler : MonoBehaviour
{
    private NavMeshAgent agent;
    private Coroutine knockbackCoroutine;
    private bool isMovementPausedByStun = false;

    [Header("Knockback Validation")]
    [SerializeField] private float navMeshSampleDistance = 2f; // Distancia para buscar NavMesh válido
    [SerializeField] private float raycastStepDistance = 0.25f; // Distancia entre raycasts para validar camino
    [SerializeField] private LayerMask collisionLayers; // Capas para detectar muros

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    /// <summary>
    /// Método público que inicia el proceso de empuje.
    /// </summary>
    public void TriggerKnockback(Vector3 direction, float force, float duration)
    {
        if (knockbackCoroutine != null)
        {
            StopCoroutine(knockbackCoroutine);
        }
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(direction, force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float force, float duration)
    {
        float originalYPosition = transform.position.y;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (agent != null && agent.enabled)
        {
            agent.enabled = false;
        }

        Vector3 start = transform.position;
        Vector3 rawTarget = start + direction.normalized * force;

        Vector3 validatedTarget = ValidateKnockbackTarget(start, rawTarget, direction.normalized);

        float timer = 0;
        while (timer < duration)
        {
            transform.position = Vector3.Lerp(start, validatedTarget, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }

        transform.position = validatedTarget;

        if (agent != null && !agent.enabled)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(validatedTarget, out hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                Vector3 finalWarpPosition = hit.position;
                finalWarpPosition.y = originalYPosition;

                agent.Warp(finalWarpPosition);
                agent.enabled = true;
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = isMovementPausedByStun;
                }
            }
            else
            {
                Debug.LogWarning($"[EnemyKnockbackHandler] {gameObject.name} no pudo reactivarse - no hay NavMesh cercano");
            }
        }

        knockbackCoroutine = null;
    }

    /// <summary>
    /// Valida que el knockback no atraviese muros ni salga del NavMesh.
    /// Retorna el punto más lejano alcanzable en la dirección del knockback.
    /// </summary>
    private Vector3 ValidateKnockbackTarget(Vector3 start, Vector3 rawTarget, Vector3 direction)
    {
        float maxDistance = Vector3.Distance(start, rawTarget);
        Vector3 currentPos = start;
        Vector3 validTarget = start;

        // Raycast por pasos para detectar colisiones
        int steps = Mathf.CeilToInt(maxDistance / raycastStepDistance);
        for (int i = 1; i <= steps; i++)
        {
            Vector3 nextPos = start + direction * (raycastStepDistance * i);

            // Si excedemos la distancia objetivo, usar eso como límite
            if (Vector3.Distance(start, nextPos) > maxDistance)
            {
                nextPos = rawTarget;
            }

            // Raycast para verificar que no hay obstáculos sólidos
            if (Physics.Raycast(currentPos, (nextPos - currentPos).normalized,
                out RaycastHit hit, Vector3.Distance(currentPos, nextPos), collisionLayers))
            {
                // Hay un muro en el camino, parar un poco antes
                validTarget = currentPos;
                break;
            }

            // Verificar que la posición está en el NavMesh
            NavMeshHit navHit;
            if (!NavMesh.SamplePosition(nextPos, out navHit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                // Fuera del NavMesh, parar en la posición anterior
                break;
            }

            // Posición válida, continuar
            validTarget = nextPos;
            currentPos = nextPos;
        }

        return validTarget;
    }

    #region Debugging

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Visualizar el área de validación del NavMesh
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, navMeshSampleDistance);
    }

    #endregion
}