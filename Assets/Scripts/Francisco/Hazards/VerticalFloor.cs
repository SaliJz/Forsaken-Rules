using UnityEngine;

public class VerticalFloor : FloorBase
{
    #region Inspector Fields

    [Header("Vertical Settings")]
    [SerializeField] private float expandedScaleMultiplier = 3f;
    [Header("Layer Settings")]
    [SerializeField] private string defaultLayerName = "Default";
    [SerializeField] private string expandedLayerName = "Obstacle";
    [Header("Activation")]
    [SerializeField] private Bodyblocker objectToToggle;

    #endregion

    #region State

    private int defaultLayerIndex;
    private int expandedLayerIndex;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        defaultLayerIndex = LayerMask.NameToLayer(defaultLayerName);
        expandedLayerIndex = LayerMask.NameToLayer(expandedLayerName);

        if (defaultLayerIndex < 0) Debug.LogWarning($"[VerticalFloor] Layer not found: {defaultLayerName}");
        if (expandedLayerIndex < 0) Debug.LogWarning($"[VerticalFloor] Layer not found: {expandedLayerName}");

        if (objectToToggle != null)
            objectToToggle.enabled = false;
    }

    #endregion

    #region FloorBase Overrides

    protected override bool ShouldEnableObstacle(FloorState target) => target == FloorState.Triggered;

    protected override void SetChildTriggeredScale()
    {
        childTriggeredScale = new Vector3(
            childDefaultScale.x,
            childDefaultScale.y * expandedScaleMultiplier,
            childDefaultScale.z);
    }

    public override string GizmoAxisLabel => "Y (up)";

    public override Vector3 GetTriggeredChildScale()
    {
        Vector3 baseScale = Application.isPlaying
            ? childDefaultScale
            : (visualChild != null ? visualChild.transform.localScale : Vector3.one);

        return new Vector3(
            baseScale.x,
            baseScale.y * expandedScaleMultiplier,
            baseScale.z);
    }

    protected override void OnTransitionBegin(FloorState target)
    {
        if (target == FloorState.Triggered)
        {
            if (expandedLayerIndex >= 0 && visualChild != null)
                visualChild.layer = expandedLayerIndex; 

            if (objectToToggle != null)
                objectToToggle.enabled = true;
        }
    }

    protected override void OnTransitionEnd(FloorState target)
    {
        if (target == FloorState.Default)
        {
            if (defaultLayerIndex >= 0 && visualChild != null)
                visualChild.layer = defaultLayerIndex; 

            if (objectToToggle != null)
                objectToToggle.enabled = false;
        }
    }

    #endregion

    #region Layer

    private void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    #endregion

    #region Gizmos

    protected override void DrawChildGizmos(BoxCollider col, bool selected, float alpha)
    {
        Vector3 top = transform.position + transform.up * (col.size.y * 0.5f);

        Gizmos.color = new Color(1f, 0.6f, 0f, alpha);
        Gizmos.DrawLine(top, top + transform.up * 0.5f);
        DrawArrowHead(top + transform.up * 0.5f, transform.up, 0.12f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(top + transform.up * 0.65f, "Expands UP");
#endif
    }

    private void DrawArrowHead(Vector3 tip, Vector3 dir, float size)
    {
        Vector3 right = Vector3.Cross(dir, Vector3.forward).normalized * size;
        if (right == Vector3.zero) right = Vector3.Cross(dir, Vector3.up).normalized * size;
        Gizmos.DrawLine(tip, tip - dir * size + right);
        Gizmos.DrawLine(tip, tip - dir * size - right);
    }

    #endregion
}