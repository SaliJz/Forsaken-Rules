using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(NavMeshObstacle))]
public abstract class FloorBase : MonoBehaviour
{
    #region Enums

    public enum FloorState { Default, Transitioning, Triggered }
    public enum MoveMode { Scale, Translate }

    #endregion

    #region Inspector Fields — Shared

    [Header("Movement")]
    [SerializeField] protected MoveMode moveMode = MoveMode.Scale;
    [SerializeField] protected float moveSpeed = 5f;

    [Header("Visual Child")]
    [SerializeField] protected GameObject visualChild;

    [Header("Behavior")]
    [SerializeField] private float initialDelay = 1f;
    [SerializeField][Range(0f, 1f)] private float triggerProbability = 0.5f;
    [SerializeField] private float triggerDuration = 3f;

    [Header("Translate Mode — Limits")]
    [SerializeField] protected Transform defaultPoint;
    [SerializeField] protected Transform triggeredPoint;

    [Header("NavMesh")]
    [SerializeField] private float navMeshCarveDelay = 0.4f;

    [Header("Emissive")]
    [SerializeField] private Renderer[] emissiveRenderers;
    [ColorUsage(true, true)][SerializeField] private Color colorDefault = Color.green;
    [ColorUsage(true, true)][SerializeField] private Color colorTriggered = Color.red;
    [ColorUsage(true, true)][SerializeField] private Color colorTransitioning = Color.yellow;

    #endregion

    #region State

    protected FloorState currentState = FloorState.Default;
    protected BoxCollider boxCollider;
    protected NavMeshObstacle navObstacle;

    protected Vector3 childDefaultScale;
    protected Vector3 childTriggeredScale;

    private List<Material> emissiveMaterials = new List<Material>();
    private Coroutine transitionCoroutine;
    private EnemyManager _enemyManager;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        navObstacle = GetComponent<NavMeshObstacle>();

        navObstacle.enabled = false;
        navObstacle.shape = NavMeshObstacleShape.Box;

        if (visualChild != null)
            childDefaultScale = visualChild.transform.localScale;

        SetChildTriggeredScale();
        SetupNavObstacle();

        foreach (var r in emissiveRenderers)
            if (r) emissiveMaterials.Add(r.material);
    }

    protected virtual void Start()
    {
        _enemyManager = GetComponentInParent<EnemyManager>();

        if (_enemyManager != null)
            _enemyManager.onWavesStart += HandleWaveStart;
        else
            Debug.LogWarning($"[FloorBase] {gameObject.name} — No encontró EnemyManager en el padre");

        ApplyStateInstant(FloorState.Default);
    }

    private void OnEnable()
    {
        if (_enemyManager != null)
        {
            _enemyManager.onWavesStart += HandleWaveStart;
        }
    }

    private void OnDisable()
    {
        if (_enemyManager != null)
            _enemyManager.onWavesStart -= HandleWaveStart;
    }

    #endregion

    #region Abstract Interface

    protected abstract void SetChildTriggeredScale();
    public abstract string GizmoAxisLabel { get; }
    public abstract Vector3 GetTriggeredChildScale();
    protected virtual bool ShouldEnableObstacle(FloorState target) => false;

    #endregion

    #region State Machine

    private void RollTrigger()
    {
        if (currentState == FloorState.Transitioning) return;

        bool shouldTrigger = Random.value < triggerProbability;
        FloorState target = shouldTrigger ? FloorState.Triggered : FloorState.Default;

        if (currentState != target)
            StartTransition(target);
    }

    private void StartTransition(FloorState target)
    {
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionRoutine(target));
    }

    private IEnumerator TransitionRoutine(FloorState target)
    {
        currentState = FloorState.Transitioning;
        UpdateEmissive(colorTransitioning);
        OnTransitionBegin(target);

        yield return new WaitForSeconds(navMeshCarveDelay);

        navObstacle.enabled = true;

        yield return StartCoroutine(MoveToState(target));

        currentState = target;

        navObstacle.enabled = ShouldEnableObstacle(target);

        UpdateEmissive(target == FloorState.Default ? colorDefault : colorTriggered);
        OnTransitionEnd(target);
    }

    private IEnumerator MoveToState(FloorState target)
    {
        if (visualChild == null) yield break;

        if (moveMode == MoveMode.Scale)
        {
            Vector3 targetScale = (target == FloorState.Triggered)
                ? childTriggeredScale
                : childDefaultScale;

            while (Vector3.Distance(visualChild.transform.localScale, targetScale) > 0.005f)
            {
                visualChild.transform.localScale = Vector3.MoveTowards(
                    visualChild.transform.localScale, targetScale, moveSpeed * Time.deltaTime);
                OnChildScaleUpdated();
                yield return null;
            }
            visualChild.transform.localScale = targetScale;
            OnChildScaleUpdated();
        }
        else
        {
            if (defaultPoint == null || triggeredPoint == null) yield break;

            Transform targetTransform = (target == FloorState.Triggered) ? triggeredPoint : defaultPoint;

            while (Vector3.Distance(visualChild.transform.position, targetTransform.position) > 0.01f)
            {
                visualChild.transform.position = Vector3.MoveTowards(
                    visualChild.transform.position, targetTransform.position, moveSpeed * Time.deltaTime);
                yield return null;
            }
            visualChild.transform.position = targetTransform.position;
        }
    }

    #endregion

    #region Virtual Hooks — Children Override

    protected virtual void OnTransitionBegin(FloorState target) { }
    protected virtual void OnTransitionEnd(FloorState target) { }
    protected virtual void OnChildScaleUpdated() { }

    #endregion

    #region NavMesh

    private void SetupNavObstacle()
    {
        navObstacle.size = boxCollider.size;
    }

    #endregion

    #region Emissive

    private void UpdateEmissive(Color color)
    {
        foreach (var mat in emissiveMaterials)
            if (mat) mat.SetColor("_EmissionColor", color);
    }

    private void ApplyStateInstant(FloorState state)
    {
        Color c = state switch
        {
            FloorState.Triggered => colorTriggered,
            FloorState.Transitioning => colorTransitioning,
            _ => colorDefault
        };
        UpdateEmissive(c);

        if (visualChild == null) return;

        if (moveMode == MoveMode.Scale)
        {
            visualChild.transform.localScale = (state == FloorState.Triggered)
                ? childTriggeredScale
                : childDefaultScale;
            OnChildScaleUpdated();
        }
    }

    #endregion

    #region Public API

    public void HandleWaveStart()
    {
        RollTrigger();
    }

    public void ForceTrigger()
    {
        if (currentState != FloorState.Triggered)
            StartTransition(FloorState.Triggered);
    }

    public void ForceReset()
    {
        if (currentState != FloorState.Default)
            StartTransition(FloorState.Default);
    }

    public FloorState CurrentState => currentState;

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        DrawGizmosInternal(false);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        DrawGizmosInternal(true);
    }

    private void DrawGizmosInternal(bool selected)
    {
        BoxCollider col = boxCollider != null ? boxCollider : GetComponent<BoxCollider>();
        if (col == null) return;

        float alpha = selected ? 1f : 0.4f;

        bool isVertical = this is VerticalFloor;
        Vector3 triggeredRatio = GetTriggeredChildRatioVsCollider();

        Vector3 normalSize = col.size;
        Vector3 triggeredSize = Vector3.Scale(col.size, triggeredRatio);

        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(0f, 1f, 0f, selected ? 0.20f : 0.08f);
        Gizmos.DrawCube(col.center, normalSize);
        Gizmos.color = new Color(0f, 1f, 0f, alpha);
        Gizmos.DrawWireCube(col.center, normalSize);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, selected ? 0.15f : 0.05f);
        Gizmos.DrawCube(col.center, triggeredSize);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, alpha);
        Gizmos.DrawWireCube(col.center, triggeredSize);

        Gizmos.matrix = Matrix4x4.identity;

        if (moveMode == MoveMode.Translate && defaultPoint != null && triggeredPoint != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, alpha);
            Gizmos.DrawLine(defaultPoint.position, triggeredPoint.position);
            Gizmos.DrawSphere(defaultPoint.position, 0.08f);
            Gizmos.DrawSphere(triggeredPoint.position, 0.08f);
        }

        DrawChildGizmos(col, selected, alpha);
    }

    private Vector3 GetTriggeredChildRatioVsCollider()
    {
        if (visualChild == null) return Vector3.one;

        Vector3 defScale = Application.isPlaying
            ? childDefaultScale
            : visualChild.transform.localScale;

        Vector3 trgScale = GetTriggeredChildScale();

        return new Vector3(
            defScale.x > 0 ? trgScale.x / defScale.x : 0f,
            defScale.y > 0 ? trgScale.y / defScale.y : 1f,
            defScale.z > 0 ? trgScale.z / defScale.z : 0f);
    }

    protected virtual void DrawChildGizmos(BoxCollider col, bool selected, float alpha) { }

    #endregion
}