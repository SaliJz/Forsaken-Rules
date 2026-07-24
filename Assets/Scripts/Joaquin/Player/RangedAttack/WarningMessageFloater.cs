using TMPro;
using UnityEngine;

public class WarningMessageFloater : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private float floatSpeed = 1f;
    [SerializeField] private float scaleAnimationSpeed = 2f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1f);
    [SerializeField] private bool rotateToCamera = true;

    [Header("Behavior")]
    [SerializeField] private bool useWorldSpaceForUI = false;

    private TextMeshProUGUI tmpUI;
    private TextMeshPro tmp3D;
    private Canvas parentCanvas;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    private Vector3 startWorldPos;
    private Vector2 startAnchoredPos;
    private float elapsedTime = 0f;
    private Color originalColor = Color.white;

    private bool isUI;
    private bool isWorldCanvas;
    private bool isWorld;
    private Camera mainCamera;

    private void Awake()
    {
        TryGetComponent(out tmpUI);
        TryGetComponent(out tmp3D);

        if (tmpUI == null) tmpUI = GetComponentInChildren<TextMeshProUGUI>();
        if (tmp3D == null) tmp3D = GetComponentInChildren<TextMeshPro>();

        parentCanvas = GetComponentInParent<Canvas>();
        rectTransform = GetComponent<RectTransform>();

        if (!TryGetComponent(out canvasGroup))
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (tmp3D != null) originalColor = tmp3D.color;
        else if (tmpUI != null) originalColor = tmpUI.color;

        startWorldPos = transform.position;
        if (rectTransform != null) startAnchoredPos = rectTransform.anchoredPosition;

        isWorldCanvas = parentCanvas != null && parentCanvas.renderMode == RenderMode.WorldSpace;
        isUI = (tmpUI != null && rectTransform != null && parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace && !useWorldSpaceForUI);
        isWorld = isWorldCanvas || tmp3D != null || (!isUI && parentCanvas == null) || useWorldSpaceForUI;

        mainCamera = Camera.main;

        transform.localScale = Vector3.one * scaleCurve.Evaluate(0f);
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        float clampedT = lifetime > 0f ? Mathf.Clamp01(elapsedTime / lifetime) : 1f;

        if (isUI && !isWorldCanvas)
        {
            rectTransform.anchoredPosition += Vector2.up * (floatSpeed * Time.deltaTime);
        }
        else
        {
            transform.position = startWorldPos + Vector3.up * (floatSpeed * elapsedTime);
        }

        float scaleMultiplier = scaleCurve.Evaluate(Mathf.Clamp01(elapsedTime * scaleAnimationSpeed));
        transform.localScale = Vector3.one * scaleMultiplier;

        float alpha = (clampedT > 0.5f) ? Mathf.Lerp(1f, 0f, (clampedT - 0.5f) * 2f) : 1f;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
        else
        {
            if (tmp3D != null)
            {
                Color c = originalColor;
                c.a = alpha * originalColor.a;
                tmp3D.color = c;
            }
            if (tmpUI != null)
            {
                Color c = originalColor;
                c.a = alpha * originalColor.a;
                tmpUI.color = c;
            }
        }

        if (rotateToCamera && isWorld)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 lookDir = transform.position - mainCamera.transform.position;
                if (lookDir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }
        }
    }

    public void SetLifetime(float duration)
    {
        lifetime = duration;
    }

    public void SetColor(Color color)
    {
        originalColor = color;
        if (tmp3D != null) tmp3D.color = color;
        if (tmpUI != null) tmpUI.color = color;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    public void SetText(string message)
    {
        if (tmp3D != null)
        {
            tmp3D.text = message;
            tmp3D.alignment = TextAlignmentOptions.Center;
        }
        else if (tmpUI != null)
        {
            tmpUI.text = message;
            tmpUI.alignment = TextAlignmentOptions.Center;

        }
    }

    public void ResetAndPlay()
    {
        elapsedTime = 0f;
        startWorldPos = transform.position;
        if (rectTransform != null) startAnchoredPos = rectTransform.anchoredPosition;
        transform.localScale = Vector3.one * scaleCurve.Evaluate(0f);

        if (tmp3D != null) tmp3D.color = originalColor;
        if (tmpUI != null) tmpUI.color = originalColor;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }
}