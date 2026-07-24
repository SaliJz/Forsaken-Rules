using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Clase de utilidad estática para encontrar posiciones válidas en el suelo y en la NavMesh.
/// </summary>
public static class GroundingUtility
{
    /// <summary>
    /// Intenta encontrar la posición más cercana en el suelo y validarla en la NavMesh.
    /// </summary>
    /// <param name="origin">Punto de partida (p. ej. transform.position al instanciar).</param>
    /// <param name="searchRadius">Radio para el SphereCast (aprox. radio del collider).</param>
    /// <param name="groundLayer">Máscara de capas consideradas suelo.</param>
    /// <param name="groundPosition">Salida con la posición válida (si retorna true).</param>
    /// <param name="sphereCastStartHeight">Altura sobre origin desde la que se inicia el SphereCast (default 2m).</param>
    /// <param name="maxCastDistance">Distancia máxima que baja el SphereCast (default 5m).</param>
    /// <param name="maxSampleDistance">Distancia máxima para NavMesh.SamplePosition (default 1m).</param>
    /// <returns>True si se encontró una posición válida en NavMesh.</returns>
    public static bool TryFindValidGroundPosition
        (
        Vector3 origin,
        float searchRadius,
        LayerMask groundLayer,
        out Vector3 groundPosition,
        float sphereCastStartHeight = 2f,
        float maxCastDistance = 5f,
        float maxSampleDistance = 1f
        )
    {
        groundPosition = origin;

        searchRadius = Mathf.Max(0.01f, searchRadius);
        sphereCastStartHeight = Mathf.Max(0f, sphereCastStartHeight);
        maxCastDistance = Mathf.Max(0.1f, maxCastDistance);
        maxSampleDistance = Mathf.Max(0.01f, maxSampleDistance);

        Vector3 sphereOrigin = origin + Vector3.up * sphereCastStartHeight;

        if (Physics.SphereCast(sphereOrigin, searchRadius, Vector3.down, out RaycastHit hit, maxCastDistance, groundLayer))
        {
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, maxSampleDistance, NavMesh.AllAreas))
            {
                groundPosition = navHit.position;
                return true;
            }

            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit2, maxSampleDistance * 2f, NavMesh.AllAreas))
            {
                groundPosition = navHit2.position;
                return true;
            }
        }

        return false;
    }
}