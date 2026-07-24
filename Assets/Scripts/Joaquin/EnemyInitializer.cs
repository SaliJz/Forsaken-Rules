using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemyInitializer : MonoBehaviour
{
    #region Inspector - Grounding Configuration

    [Header("Grounding Configuration")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [Tooltip("Radio aproximado del personaje (usado por SphereCast).")]
    [SerializeField] private float characterRadius = 0.5f;

    #endregion

    #region Inspector - Cast Settings

    [Header("Cast Settings")]
    [Tooltip("Altura sobre transform.position desde la cual inicia el SphereCast.")]
    [SerializeField] private float sphereCastStartHeight = 2f;
    [Tooltip("Distancia hacia abajo del SphereCast.")]
    [SerializeField] private float castDistance = 5f;
    [Tooltip("Distancia de muestreo para NavMesh.SamplePosition.")]
    [SerializeField] private float navSampleDistance = 1f;

    #endregion

    #region Inspector - Runtime Behavior

    [Header("Runtime behavior")]
    [Tooltip("Si true, desactiva el GameObject al fallar.")]
    [SerializeField] private bool disableOnFail = true;
    [Tooltip("Usar Awake o Start para inicializar? Awake = true, Start = false.")]
    [SerializeField] private bool useAwake = false;

    #endregion

    #region Inspector - Debug Gizmos

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawDetailedGizmos = true;
    [SerializeField] private float gizmoScale = 1f;

    #endregion

    #region Internal State

    private bool lastValid = false;
    private Vector3 lastPhysHit = Vector3.zero;
    private Vector3 lastNavHit = Vector3.zero;
    private bool initializedOnce = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (useAwake && enabled) TryGroundAndPlace();
    }

    private void Start()
    {
        if (!useAwake && enabled) TryGroundAndPlace();
    }

    #endregion

    #region Initialization And Placement

    private void TryGroundAndPlace()
    {
        if (initializedOnce) return;

        initializedOnce = true;
        Vector3 origin = transform.position;

        bool ok = GroundingUtility.TryFindValidGroundPosition(
            origin,
            characterRadius,
            groundLayer,
            out Vector3 validPosition,
            sphereCastStartHeight,
            castDistance,
            navSampleDistance);

        if (ok)
        {
            NavMeshAgent localAgent = GetComponent<NavMeshAgent>();

            if (localAgent != null)
            {
                if (localAgent.isOnNavMesh)
                {
                    localAgent.Warp(validPosition);
                }
                else
                {
                    NavMeshHit navHit;
                    if (NavMesh.SamplePosition(validPosition, out navHit, navSampleDistance, NavMesh.AllAreas))
                    {
                        localAgent.Warp(navHit.position);
                    }
                    else
                    {
                        transform.position = validPosition;
                    }
                }
            }
            else
            {
                transform.position = validPosition;
            }

            lastValid = true;
            lastNavHit = validPosition;
            lastPhysHit = validPosition;

            Debug.Log($"[EnemyInitializer] Se encontro posicion valida en NavMesh para '{gameObject.name}' en {origin}.", gameObject);
        }
        else
        {
            lastValid = false;
#if UNITY_EDITOR
            Debug.LogError($"[EnemyInitializer] No se encontro posicion valida en NavMesh para '{gameObject.name}' en {origin}.", gameObject);
#endif
            if (disableOnFail) gameObject.SetActive(false);
        }
    }

    #endregion

    #region Debug And Gizmos

    private void OnDrawGizmos()
    {
        if (!drawDetailedGizmos) return;
        DrawGizmosPreview();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawDetailedGizmos) return;
        DrawGizmosPreview(true);
    }
#endif

    private void DrawGizmosPreview(bool highlight = false)
    {
        Vector3 origin = transform.position;
        Vector3 sphereOrigin = origin + Vector3.up * sphereCastStartHeight;

        float r = Mathf.Max(0.01f, characterRadius) * gizmoScale;
        float small = 0.08f * gizmoScale;

        // esfera de inicio
        Gizmos.color = highlight ? new Color(1f, 0.9f, 0f, 0.95f) : new Color(1f, 0.9f, 0f, 0.6f);
        Gizmos.DrawWireSphere(sphereOrigin, r);
        Gizmos.DrawLine(sphereOrigin, sphereOrigin + Vector3.down * castDistance);

        if (!lastValid)
        {
            if (Physics.SphereCast(sphereOrigin, characterRadius, Vector3.down, out RaycastHit hit, castDistance, groundLayer))
            {
                lastPhysHit = hit.point;
                if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, navSampleDistance, NavMesh.AllAreas))
                {
                    lastNavHit = navHit.position;
                    lastValid = true;
                }
                else
                {
                    lastNavHit = Vector3.zero;
                }
            }
            else
            {
                lastPhysHit = Vector3.zero;
                lastNavHit = Vector3.zero;
            }
        }

        if (lastPhysHit != Vector3.zero)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            Gizmos.DrawSphere(lastPhysHit, small);
        }

        if (lastNavHit != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(lastNavHit, small * 1.15f);
            if (lastPhysHit != Vector3.zero) Gizmos.DrawLine(lastPhysHit, lastNavHit);
        }
        else if (lastPhysHit != Vector3.zero)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastPhysHit, small * 1.2f);
        }

#if UNITY_EDITOR
        // Etiquetas
        Vector3 labelPos = sphereOrigin + Vector3.up * 0.2f;
        Handles.Label(labelPos, $"SphereCast start\nr:{characterRadius:F2} cast:{castDistance:F2}");
        if (lastPhysHit != Vector3.zero) Handles.Label(lastPhysHit + Vector3.up * 0.15f, $"PhysHit\n{lastPhysHit.ToString("F2")}");
        if (lastNavHit != Vector3.zero) Handles.Label(lastNavHit + Vector3.up * 0.15f, $"NavHit\n{lastNavHit.ToString("F2")}");
#endif
    }

    #endregion
}