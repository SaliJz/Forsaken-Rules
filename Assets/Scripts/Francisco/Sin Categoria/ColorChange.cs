using UnityEngine;

public class ColorChange : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Color _startColor = Color.white;
    [SerializeField] private Color _endColor = Color.red;
    [SerializeField] private float _lerpDuration = 2.0f;
    [SerializeField] private AnimationCurve _colorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool _useHDR = false;
    [SerializeField] private float _emissionIntensity = 1.0f; 

    private Renderer _renderer;
    private Material _material;
    private float _lerpTime = 0.0f;
    private bool _lerpingForward = true;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
        {
            Debug.LogError("No se encontró un componente Renderer en este GameObject.");
            return;
        }

        _material = _renderer.material;
    }

    private void Update()
    {
        if (_material == null) return;

        if (_lerpingForward)
        {
            _lerpTime += Time.deltaTime / _lerpDuration;
        }
        else
        {
            _lerpTime -= Time.deltaTime / _lerpDuration;
        }

        if (_lerpTime >= 1.0f)
        {
            _lerpTime = 1.0f;
            _lerpingForward = false;
        }
        else if (_lerpTime <= 0.0f)
        {
            _lerpTime = 0.0f;
            _lerpingForward = true;
        }

        float curveValue = _colorCurve.Evaluate(_lerpTime);
        Color newColor = Color.Lerp(_startColor, _endColor, curveValue);

        if (_useHDR)
        {
            Color emissionColor = newColor * _emissionIntensity;
            _material.SetColor("_EmissionColor", emissionColor);

            _material.EnableKeyword("_EMISSION");
        }
        else
        {
            _material.color = newColor;
        }
    }

    private void OnDestroy()
    {
        if (_material != null)
        {
            DestroyImmediate(_material);
        }
    }
}