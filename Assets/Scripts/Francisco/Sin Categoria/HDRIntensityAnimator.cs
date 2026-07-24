using UnityEngine;
using DG.Tweening;
using TMPro;

[ExecuteInEditMode]
public class HDRIntensityAnimator : MonoBehaviour
{
    public enum AnimationMode { Once, Yoyo }

    #region Configuration
    [Header("Target & Setup")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private TextMeshProUGUI targetTMP;
    [SerializeField] private TextMeshPro targetTMP3D;

    [Header("HDR Settings")]
    [SerializeField] private AnimationMode intensityAnimationMode = AnimationMode.Yoyo;
    [ColorUsage(true, true)]
    [SerializeField] private Color emissionColor = Color.white;
    [SerializeField] private float maxIntensity = 5.0f;
    [SerializeField] private float minIntensity = 1.0f;
    [SerializeField] private float cycleDuration = 1.0f;
    #endregion

    #region Internal State
    private Material _clonedMaterial;
    private Tweener _intensityTween;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly string EmissionKeyword = "_EMISSION";
    #endregion

    #region Core Logic
    void Awake()
    {
        InitializeTarget();
        ApplyInitialColor(minIntensity);
        StartAnimation();
    }

    void OnEnable()
    {
        StartAnimation();
    }

    void OnDisable()
    {
        _intensityTween?.Kill();
    }

    private void InitializeTarget()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (targetTMP == null) targetTMP = GetComponent<TextMeshProUGUI>();
        if (targetTMP3D == null) targetTMP3D = GetComponent<TextMeshPro>();

        if (targetRenderer != null)
        {
            _clonedMaterial = targetRenderer.material;
        }
        else if (targetTMP3D != null)
        {
            _clonedMaterial = targetTMP3D.fontMaterial;
        }
    }

    private void ApplyInitialColor(float intensity)
    {
        Color finalColor = emissionColor * intensity;

        if (targetRenderer != null || targetTMP3D != null)
        {
            if (_clonedMaterial != null)
            {
                _clonedMaterial.EnableKeyword(EmissionKeyword);
                _clonedMaterial.SetColor(EmissionColorID, finalColor);
            }
        }
        else if (targetTMP != null)
        {
            targetTMP.fontSharedMaterial.EnableKeyword(ShaderUtilities.Keyword_Glow);
            targetTMP.fontSharedMaterial.SetColor(ShaderUtilities.ID_GlowColor, finalColor);
        }
    }

    private void StartAnimation()
    {
        _intensityTween?.Kill();

        if (targetRenderer == null && targetTMP == null && targetTMP3D == null) return;

        float currentIntensity = minIntensity;

        LoopType loopType = (intensityAnimationMode == AnimationMode.Yoyo) ? LoopType.Yoyo : LoopType.Restart;
        int loops = (intensityAnimationMode == AnimationMode.Yoyo) ? -1 : 1;

        _intensityTween = DOTween.To(() => currentIntensity, x => currentIntensity = x, maxIntensity, cycleDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(loops, loopType)
            .OnUpdate(() =>
            {
                ApplyColorWithCurrentIntensity(currentIntensity);
            });
    }

    private void ApplyColorWithCurrentIntensity(float intensity)
    {
        Color finalColor = emissionColor * intensity;

        if (_clonedMaterial != null)
        {
            _clonedMaterial.SetColor(EmissionColorID, finalColor);
        }
        else if (targetTMP != null)
        {
            targetTMP.fontSharedMaterial.SetColor(ShaderUtilities.ID_GlowColor, finalColor);
        }
    }

    void OnDestroy()
    {
        _intensityTween?.Kill();

        if (Application.isPlaying && _clonedMaterial != null)
        {
            Destroy(_clonedMaterial);
        }
    }
    #endregion
}