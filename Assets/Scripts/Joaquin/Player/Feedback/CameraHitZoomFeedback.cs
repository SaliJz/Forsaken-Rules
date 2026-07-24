using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;

public class CameraHitZoomFeedback : MonoBehaviour
{
    public static CameraHitZoomFeedback Instance { get; private set; }

    [Header("Referencia")]
    [SerializeField] private CinemachineCamera vcam;

    [Header("Zoom - Impacto a Vida (Fuerte)")]
    [SerializeField] private float healthZoomAmount = 0.3f;
    [SerializeField] private float healthZoomDuration = 0.1f;

    [Header("Zoom - Impacto a Dureza (Débil)")]
    [SerializeField] private float toughnessZoomAmount = 0.15f;
    [SerializeField] private float toughnessZoomDuration = 0.1f;

    private CinemachineBrain cameraBrain;
    private CinemachineCamera lastActiveCamera;
    private float originalSize;
    private Tween zoomTween;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            cameraBrain = Camera.main.GetComponent<CinemachineBrain>();
            if (cameraBrain == null)
            {
                Debug.LogWarning("[CameraHitZoomFeedback] No se encontró CinemachineBrain en la Main Camera de esta escena.");
            }
        }
    }

    private void OnDestroy()
    {
        if (zoomTween != null && zoomTween.IsActive())
        {
            zoomTween.Kill();
        }
    }

    public void TriggerHitZoom(bool hitToughness)
    {
        if (cameraBrain == null) return;

        var activeCustomCamera = cameraBrain.ActiveVirtualCamera as CinemachineCamera;
        if (activeCustomCamera == null) return;

        if (activeCustomCamera != lastActiveCamera)
        {
            lastActiveCamera = activeCustomCamera;
            originalSize = activeCustomCamera.Lens.Orthographic ? activeCustomCamera.Lens.OrthographicSize : activeCustomCamera.Lens.FieldOfView;
        }

        float zoomAmount = hitToughness ? toughnessZoomAmount : healthZoomAmount;
        float duration = hitToughness ? toughnessZoomDuration : healthZoomDuration;

        if (zoomTween != null && zoomTween.IsActive())
        {
            zoomTween.Kill();
        }

        float targetSize = originalSize - zoomAmount;
        Sequence seq = DOTween.Sequence();

        if (activeCustomCamera.Lens.Orthographic)
        {
            seq.Append(DOTween.To(() => activeCustomCamera.Lens.OrthographicSize, x => activeCustomCamera.Lens.OrthographicSize = x, targetSize, duration * 0.3f).SetEase(Ease.OutQuad));
            seq.Append(DOTween.To(() => activeCustomCamera.Lens.OrthographicSize, x => activeCustomCamera.Lens.OrthographicSize = x, originalSize, duration * 0.7f).SetEase(Ease.InOutSine));
        }
        else
        {
            seq.Append(DOTween.To(() => activeCustomCamera.Lens.FieldOfView, x => activeCustomCamera.Lens.FieldOfView = x, targetSize, duration * 0.3f).SetEase(Ease.OutQuad));
            seq.Append(DOTween.To(() => activeCustomCamera.Lens.FieldOfView, x => activeCustomCamera.Lens.FieldOfView = x, originalSize, duration * 0.7f).SetEase(Ease.InOutSine));
        }

        zoomTween = seq;
    }
}