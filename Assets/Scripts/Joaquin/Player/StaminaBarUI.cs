using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gestiona la visualizaci�n de la barra de estamina de la habilidad especial.
/// Se actualiza autom�ticamente mediante eventos del ShieldSkill.
/// </summary>
public class StaminaBarUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Image staminaBackground;
    [SerializeField] private Image staminaFillImage;
    [SerializeField] private Sprite eyeNormalSprite;
    [SerializeField] private Sprite eyeNoStaminaSprite;
    [SerializeField] private TextMeshProUGUI staminaText;
    [SerializeField] private GameObject staminaBarContainer;

    [Header("Visual Settings")]
    [SerializeField] private Color fullStaminaColor = new Color(0.2f, 0.8f, 1f); // Azul
    //[SerializeField] private Color midStaminaColor = new Color(1f, 0.8f, 0f); // Amarillo
    //[SerializeField] private Color lowStaminaColor = new Color(1f, 0.2f, 0.2f); // Rojo
    [SerializeField] private Color emptyStaminaColor = new Color(0.5f, 0.5f, 0.5f); // Gris
    [Range(0f, 1f)]
    [SerializeField] private float lowStaminaThreshold = 0.25f; // 25%
    [Range(0f, 1f)]
    //[SerializeField] private float midStaminaThreshold = 0.5f; // 50%

    [Header("Animation Settings")]
    [SerializeField] private bool useSmoothing = true; // Usar interpolaci�n suave
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private bool pulseWhenLow = true; // Pulsar cuando la estamina est� baja
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseMinScale = 0.95f;
    [SerializeField] private float pulseMaxScale = 1.05f;

    [Header("Display Options")]
    [SerializeField] private bool showText = true; // Mostrar texto de estamina
    [SerializeField] private bool showPercentage = true; // Mostrar porcentaje
    [SerializeField] private bool showActualValues = false; // Mostrar valores actuales/maximos
    [SerializeField] private bool hideWhenFull = false; // Ocultar barra cuando est� llena
    [SerializeField] private float hideDelay = 2f; 

    private float currentDisplayFill = 1f;
    private float targetFill = 1f;
    private float currentStamina;
    private float maxStamina;
    private bool isLowStamina = false;
    private float hideTimer = 0f;
    private Vector3 originalScale;

    private void Awake()
    {
        if (staminaBarContainer != null)
        {
            originalScale = staminaBarContainer.transform.localScale;
        }

        if (staminaSlider != null)
        {
            staminaSlider.value = currentDisplayFill;
        }
        if (staminaFillImage != null)
        {
            staminaFillImage.color = fullStaminaColor;
        }

        UpdateStaminaText(1f);
    }

    private void OnEnable()
    {
        ShieldSkill.OnStaminaChanged += HandleStaminaChanged;
    }

    private void OnDisable()
    {
        ShieldSkill.OnStaminaChanged -= HandleStaminaChanged;
    }

    private void Update()
    {
        // Suavizar el cambio de la barra
        if (useSmoothing && Mathf.Abs(currentDisplayFill - targetFill) > 0.001f)
        {
            currentDisplayFill = Mathf.Lerp(currentDisplayFill, targetFill, smoothSpeed * Time.deltaTime);

            if (staminaSlider != null)
            {
                staminaSlider.value = currentDisplayFill;
            }
        }

        // Efecto de pulso cuando la estamina est� baja
        if (pulseWhenLow && isLowStamina && staminaBarContainer != null)
        {
            float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI) + 1f) * 0.5f);
            staminaBarContainer.transform.localScale = originalScale * scale;
        }
        else if (staminaBarContainer != null)
        {
            staminaBarContainer.transform.localScale = originalScale;
        }

        // Ocultar barra cuando est� llena (si est� habilitado)
        if (hideWhenFull && staminaBarContainer != null)
        {
            if (targetFill >= 0.99f)
            {
                hideTimer += Time.deltaTime;
                if (hideTimer >= hideDelay)
                {
                    staminaBarContainer.SetActive(false);
                }
            }
            else
            {
                hideTimer = 0f;
                if (!staminaBarContainer.activeSelf)
                {
                    staminaBarContainer.SetActive(true);
                }
            }
        }
    }

    /// <summary>
    /// Maneja el evento de cambio de estamina del ShieldSkill.
    /// </summary>
    private void HandleStaminaChanged(float current, float max)
    {
        currentStamina = current;
        maxStamina = max;

        float fillAmount = max > 0 ? current / max : 0f;
        targetFill = Mathf.Clamp01(fillAmount);

        // Actualizar inmediatamente si no se usa suavizado
        if (!useSmoothing)
        {
            currentDisplayFill = targetFill;
            if (staminaSlider != null)
            {
                staminaSlider.value = currentDisplayFill;
            }
        }

        // Actualizar color seg�n el nivel de estamina
        UpdateStaminaColor(targetFill);

        // Actualizar texto
        UpdateStaminaText(targetFill);

        // Verificar si est� baja
        isLowStamina = targetFill <= lowStaminaThreshold && targetFill > 0f;

        // Resetear el timer de ocultar
        hideTimer = 0f;
    }

    /// <summary>
    /// Actualiza el color de la barra seg�n el nivel de estamina.
    /// </summary>
    private void UpdateStaminaColor(float fillAmount)
    {
        if (staminaSlider == null) return;

        Color targetColor;

        if (fillAmount <= 0.01f)
        {
            targetColor = emptyStaminaColor;
            staminaBackground.sprite = eyeNoStaminaSprite;
        }
        else
        {
            staminaBackground.sprite = eyeNormalSprite;
        }
        // else if (fillAmount <= lowStaminaThreshold)
        // {
        //     targetColor = lowStaminaColor;
        // }
        // else if (fillAmount <= midStaminaThreshold)
        // {
        //     // Interpolaci�n entre rojo y amarillo
        //     float t = (fillAmount - lowStaminaThreshold) / (midStaminaThreshold - lowStaminaThreshold);
        //     targetColor = Color.Lerp(lowStaminaColor, midStaminaColor, t);
        // }
        // else
        // {
        //     // Interpolaci�n entre amarillo y azul
        //     float t = (fillAmount - midStaminaThreshold) / (1f - midStaminaThreshold);
        //     targetColor = Color.Lerp(midStaminaColor, fullStaminaColor, t);
        // }

        // staminaFillImage.color = targetColor;
    }

    /// <summary>
    /// Actualiza el texto de la barra de estamina.
    /// </summary>
    private void UpdateStaminaText(float fillAmount)
    {
        if (staminaText == null || !showText)
        {
            if (staminaText != null)
            {
                staminaText.gameObject.SetActive(false);
            }
            return;
        }

        staminaText.gameObject.SetActive(true);

        string text = "";

        if (showActualValues)
        {
            text = $"{Mathf.CeilToInt(currentStamina)}/{Mathf.CeilToInt(maxStamina)}";
        }
        else if (showPercentage)
        {
            text = $"{Mathf.RoundToInt(fillAmount * 100)}%";
        }

        staminaText.text = text;
    }

    /// <summary>
    /// M�todo p�blico para forzar la actualizaci�n de la barra.
    /// </summary>
    public void ForceUpdate(float current, float max)
    {
        HandleStaminaChanged(current, max);
    }

    /// <summary>
    /// Muestra u oculta la barra de estamina.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (staminaBarContainer != null)
        {
            staminaBarContainer.SetActive(visible);
        }
    }

    /// <summary>
    /// Resetea la barra a su estado inicial (llena).
    /// </summary>
    public void ResetBar()
    {
        currentDisplayFill = 1f;
        targetFill = 1f;

        if (staminaSlider != null)
        {
            staminaSlider.value = 1f;
        }
        if (staminaFillImage != null)
        {
            staminaFillImage.color = fullStaminaColor;
        }

        UpdateStaminaText(1f);
        isLowStamina = false;
        hideTimer = 0f;
    }
}