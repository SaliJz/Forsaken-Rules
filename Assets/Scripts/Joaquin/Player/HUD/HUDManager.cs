using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Clase que maneja el HUD del jugador.
/// </summary> 
public class HUDManager : MonoBehaviour
{
    #region Structs & Classes

    [System.Serializable]
    public class LifeStageIcon
    {
        public PlayerHealth.LifeStage stage;
        public Sprite icon;
    }

    #endregion

    #region Inspector - Componentes del HUD

    [Header("Componentes del HUD")]
    [SerializeField] private Image healthBar;
    [SerializeField] private Image temporaryHealthBar;
    [SerializeField] private Image lifeStageIconImage;
    [SerializeField] private List<LifeStageIcon> lifeStageIcons;
    [SerializeField] private PlayerHealth.LifeStage pendingStage;

    #endregion

    #region Inspector - Prompt De Interaccion

    [Header("Prompt de Interaccion")]
    [SerializeField] private GameObject interactionPromptPanel;
    [SerializeField] private TextMeshProUGUI interactionPromptText;

    [Header("Prompt de Interaccion GACHAPON")]
    [SerializeField] private GameObject interactionPromptGACHAPONPanel;
    [SerializeField] private TextMeshProUGUI interactionPromptGACHAPONText;

    #endregion

    #region Inspector - Etapas de Vida Root Objects

    [Header("Etapas de Vida - Root Objects")]
    [SerializeField] private GameObject adultRootStage;
    [SerializeField] private GameObject elderRootStage;

    #endregion

    #region Inspector - Low Health VFX

    [Header("Low Health VFX")]
    // [SerializeField] private Image screenFlashOverlay;
    [SerializeField] private Color lowHealthScreenFlashColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] private Color lowHealthBarFlashColor = Color.white;
    [SerializeField] private float healthBarFlashInterval = 0.5f;
    [SerializeField] private float screenFlashInterval = 1f;
    [SerializeField] private float lowHealthThreshold = 0.25f;
    [SerializeField] private float lowHealthEffectDuration = 2.5f;

    #endregion

    #region Inspector - Vida Temporal Animacion

    [Header("Vida Temporal - Animacion")]
    [SerializeField] private float temporaryHealthLerpSpeed = 5f;
    [SerializeField] private RectTransform healthBarParentRect;

    #endregion

    #region Inspector - Ghosting de Barra de Vida

    [Header("Ghosting de Barra de Vida")]
    [SerializeField] private Image ghostHealthBar;
    [SerializeField] private float ghostDecayDuration = 1f;
    [SerializeField] private float ghostDecaySpeed = 0.5f;
    [SerializeField] private TMP_Text healthText;

    #endregion

    #region Inspector - VFX Cambio de Etapa

    [Header("VFX Cambio de Etapa - Secuencia Mano")]
    [SerializeField] private HUDHandAnimCtrl handAnim;

    #endregion

    #region Inspector - Estado Berserker

    [Header("Berserker Effect")]
    [SerializeField] private RectTransform gearSprite;
    [SerializeField] private ShieldSkill berserkerScript;
    [SerializeField] private float gearRotationSpeed = 90f;
    [SerializeField] private float HUDShakeIntensity = 1.5f;
    [SerializeField] private float HUDnoStamiShakeIntensity = 4f;
    #endregion

    #region  Inspector - Healing UI Effect

    [Header("Healing Effect")]
    [SerializeField] private Image heallingBorder;
    [SerializeField] private float healMaxAlpha;

    #endregion

    #region Internal State

    private float ghostFill = 1f;
    private float ghostTargetFill = 1f;
    private float ghostSnapFill = -1f;
    private float targetTempHealthPercentage = 0f;
    private float maxHealthForTempBar = 1f;
    private float lowHealthTimer;
    private bool isLowHealth = false;
    private Color originalHealthBarColor;

    private Coroutine ghostDecayCoroutine;
    private Coroutine healthBarFlashCoroutine;
    private Coroutine screenFlashCoroutine;
    private Coroutine lowHealthEffectCoroutine;

    private IPlayerSpecialAbility playerAbility;
    private bool isGearRotating = false;
    private Coroutine berserkerShakeCoroutine;
    Vector2 HUDOriginalPos;
    float currentShakeIntensity;
    bool isInitialize = false;
    Coroutine currentHealingRoutine;
    public static event Action<bool> OnLowHealthChanged;

    #endregion

    #region Public Properties & Events

    public static HUDManager Instance { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        // Guardar color original de la barra de vida
        if (healthBar != null)
        {
            originalHealthBarColor = healthBar.color;
        }

        // Inicializar overlay de pantalla
        // if (screenFlashOverlay != null)
        // {   
        //     // Asegurarse de que el overlay esta invisible al inicio
        //     Color transparent = lowHealthScreenFlashColor;
        //     transparent.a = 0f;
        //     screenFlashOverlay.color = transparent;
        //     screenFlashOverlay.raycastTarget = false; // No bloquear interacciones
        // }

        if (handAnim != null) handAnim.onSmashImpact += ApplyLifeStageIcon;
        ShieldSkill.OnStaminaChanged += HandleStaminaChange;

        GameObject player = GameObject.FindWithTag("Player");
        if(player != null) berserkerScript = player.GetComponent<ShieldSkill>();
        else Debug.LogWarning("[HUD Manager] No se encontró player para obtener ShieldSkill");

        playerAbility = berserkerScript;

        //Efecto de heal en HUD completamente transparente
        if(heallingBorder != null)
        heallingBorder.color = new Color (heallingBorder.color.r, heallingBorder.color.g, heallingBorder.color.b, 0f);
    }

    private void Start()
    {
        StartCoroutine(onStartIgnoreHandAnim());
    }

    private void Update()
    {
        if (temporaryHealthBar != null && healthBar != null)
        {
            if (healthBarParentRect != null)
            {
                float healthBarEndPosition = healthBar.fillAmount;

                float targetAnchorMinX = healthBarEndPosition;

                RectTransform tempBarRect = temporaryHealthBar.rectTransform;

                float currentWidth = tempBarRect.anchorMax.x - tempBarRect.anchorMin.x;

                Vector2 currentAnchorMin = tempBarRect.anchorMin;

                tempBarRect.anchorMin = Vector2.Lerp(
                    currentAnchorMin,
                    new Vector2(targetAnchorMinX, currentAnchorMin.y),
                    Time.deltaTime * temporaryHealthLerpSpeed
                );

                tempBarRect.anchorMax = new Vector2(
                    tempBarRect.anchorMin.x + currentWidth,
                    tempBarRect.anchorMax.y
                );

                temporaryHealthBar.fillAmount = Mathf.Lerp(temporaryHealthBar.fillAmount, targetTempHealthPercentage, Time.deltaTime * temporaryHealthLerpSpeed);
            }
        }

        if (isGearRotating)
        {
            gearSprite.Rotate(0f, 0f , gearRotationSpeed * Time.deltaTime);
        }
    }

    private void OnEnable()
    {
        PlayerHealth.OnHealthChanged += UpdateHealthBar;
        PlayerHealth.OnLifeStageChanged += UpdateLifeStageIcon;
        if (handAnim != null) handAnim.onSmashImpact += ApplyLifeStageIcon;
        if (playerAbility != null)
        {
            playerAbility.OnAbilityActivated += StartGearRotation;
            playerAbility.OnAbilityDeactivated += StopGearRotation;
        }
        PlayerHealth.onHealling += DoHeallingUIEffect;
        PlayerHealth.onDeathing += HandleDeathing;
    }

    private void OnDisable()
    {
        PlayerHealth.OnHealthChanged -= UpdateHealthBar;
        PlayerHealth.OnLifeStageChanged -= UpdateLifeStageIcon;
        if (handAnim != null) handAnim.onSmashImpact -= ApplyLifeStageIcon;
        if (playerAbility != null)
        {
            playerAbility.OnAbilityActivated -= StartGearRotation;
            playerAbility.OnAbilityDeactivated -= StopGearRotation;
        }
        ShieldSkill.OnStaminaChanged -= HandleStaminaChange;
        PlayerHealth.onHealling -= DoHeallingUIEffect;
        PlayerHealth.onDeathing -= HandleDeathing;

        if (healthBarFlashCoroutine != null) StopCoroutine(healthBarFlashCoroutine);
        if (screenFlashCoroutine != null) StopCoroutine(screenFlashCoroutine);
        if (lowHealthEffectCoroutine != null) StopCoroutine(lowHealthEffectCoroutine);
    }

    #endregion

    #region Health & Ghosting Management

    /// <summary>
    /// Funcion que actualiza la barra de salud en el HUD.
    /// </summary>
    /// <param name="currentHealth"> Vida actual del jugador </param>
    /// <param name="maxHealth"> Vida maxima del jugador </param>
    private void  UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBar == null) return;

        if (maxHealth <= 0f)
        {
            Debug.LogWarning("[HUDManager] maxHealth invalido en UpdateHealthBar: " + maxHealth);
            healthBar.fillAmount = 0f;
            return;  
        }
        if (healthText != null)
        {
            healthText.text = $"{Mathf.RoundToInt(Mathf.Max(0f, currentHealth))} / {Mathf.RoundToInt(Mathf.Max(0f, maxHealth))}";
        }

        float healthPercentage = Mathf.Clamp01(currentHealth / maxHealth);
        healthBar.fillAmount = healthPercentage;

        // Ghosting
        UpdateGhostBar(healthPercentage);

        // Verificar si esta en vida baja
        bool shouldShowLowHealthEffects = healthPercentage < lowHealthThreshold;

        if (shouldShowLowHealthEffects && !isLowHealth)
        {
            // Entrar en estado de vida baja
            isLowHealth = true;
            StartLowHealthEffects();
            OnLowHealthChanged?.Invoke(true);
        }
        else if (shouldShowLowHealthEffects && isLowHealth)
        {
            // Reiniciar efectos si aun esta en vida baja
            ResetLowHealthEffects();
        }
        else if (!shouldShowLowHealthEffects && isLowHealth)
        {
            // Salir del estado de vida baja
            isLowHealth = false;
            StopLowHealthEffects();
            OnLowHealthChanged?.Invoke(false);
        }
    }

    public void SetTemporaryHealthValues(float currentTempHealth, float maxHealth)
    {
        if (temporaryHealthBar == null) return;

        maxHealthForTempBar = maxHealth > 0 ? maxHealth : 1f;

        targetTempHealthPercentage = currentTempHealth / maxHealthForTempBar;

        ReportDebug($"Vida temporal establecida. Valor: {currentTempHealth}/{maxHealth} (Target Fill: {targetTempHealthPercentage})", 1);
    }

    public void UpdateTemporaryHealthBar(float currentTempHealth, float maxLimit)
    {
        if (temporaryHealthBar != null)
        {
            float fillAmount = maxLimit > 0 ? currentTempHealth / maxLimit : 0f;
            temporaryHealthBar.fillAmount = fillAmount;

            ReportDebug($"Vida temporal actualizada. Valor: {currentTempHealth}/{maxLimit} (Fill: {fillAmount})", 1);
        }
    }

    /// <summary>
    /// Actualiza el estado de la barra ghost al recibir dano.
    /// Solo actua si la nueva vida es menor a la anterior (dano real).
    /// </summary>
    private void UpdateGhostBar(float newHealthFill)
    {
        if (ghostHealthBar == null) return;

        // Solo aplica cuando hay dano (fill baja)
        if (newHealthFill >= ghostFill)
        {
            // Curacion o sin cambio: la ghost sigue al instante para no quedar por debajo
            ghostFill = newHealthFill;
            ghostTargetFill = newHealthFill;
            ghostSnapFill = -1f;
            ghostHealthBar.fillAmount = ghostFill;
            if (ghostDecayCoroutine != null)
            {
                StopCoroutine(ghostDecayCoroutine);
                ghostDecayCoroutine = null;
            }
            return;
        }

        // Hay un golpe nuevo:
        if (ghostDecayCoroutine != null)
        {
            // Si la barra ghost todavia estaba bajando
            // Snapea al fill de la barra principal justo antes de este golpe
            ghostSnapFill = ghostTargetFill;
        }

        // El nuevo objetivo es el fill actual de la barra principal
        ghostTargetFill = newHealthFill;

        // Reiniciar la corrutina de descenso
        if (ghostDecayCoroutine != null) StopCoroutine(ghostDecayCoroutine);
        ghostDecayCoroutine = StartCoroutine(GhostDecayRoutine());

        ReportDebug($"Ghost bar activada. Snap: {ghostSnapFill:F2} -> Actual: {ghostFill:F2} -> Target: {ghostTargetFill:F2}", 1);
    }

    /// <summary>
    /// Corrutina que maneja el descenso suave 
    /// y el snap previo si aplica
    /// de la barra de ghosting.
    /// </summary>
    private IEnumerator GhostDecayRoutine()
    {
        // Si hay un snap pendiente, aplicarlo de inmediato
        if (ghostSnapFill >= 0f)
        {
            ghostFill = ghostSnapFill;
            ghostHealthBar.fillAmount = ghostFill;
            ghostSnapFill = -1f;
        }

        // Esperar ghostDecayDuration antes de empezar a bajar
        float elapsed = 0f;
        while (elapsed < ghostDecayDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Descender suavemente desde ghostFill hasta ghostTargetFill
        float startFill = ghostFill;
        elapsed = 0f;

        float distance = Mathf.Max(startFill - ghostTargetFill, 0f);
        float totalDecayTime = Mathf.Max(distance / ghostDecaySpeed, 0.1f);

        while (elapsed < totalDecayTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / totalDecayTime);
            ghostFill = Mathf.Lerp(startFill, ghostTargetFill, t);
            ghostHealthBar.fillAmount = ghostFill;
            yield return null;
        }

        ghostFill = ghostTargetFill;
        ghostHealthBar.fillAmount = ghostFill;
        ghostDecayCoroutine = null;

        ReportDebug($"Ghost bar llego a target: {ghostFill:F2}", 1);
    }

    #endregion

    #region Low Health Effects

    /// <summary>
    /// Inicia los efectos visuales de vida baja.
    /// </summary>
    private void StartLowHealthEffects()
    {
        // Reiniciar temporizador
        lowHealthTimer = 0f;

        // Iniciar parpadeo de la barra de vida
        if (healthBarFlashCoroutine != null) StopCoroutine(healthBarFlashCoroutine);
        healthBarFlashCoroutine = StartCoroutine(HealthBarFlashRoutine());

        // Iniciar parpadeo de pantalla
        // if (screenFlashCoroutine != null) StopCoroutine(screenFlashCoroutine);
        // screenFlashCoroutine = StartCoroutine(ScreenFlashRoutine());
        

        // Iniciar corrutina de duracion del efecto
        // if (lowHealthEffectCoroutine != null) StopCoroutine(lowHealthEffectCoroutine);
        // lowHealthEffectCoroutine = StartCoroutine(LowHealthEffectDurationRoutine());

        ReportDebug("Efectos de vida baja activados.", 1);
    }

    /// <summary>
    /// Reinicia los efectos de vida baja (reinicia el temporizador)
    /// </summary>
    private void ResetLowHealthEffects()
    {
        lowHealthTimer = 0f;
        ReportDebug("Efectos de vida baja reiniciados.", 1);
    }

    /// <summary>
    /// Detiene los efectos visuales de vida baja.
    /// </summary>
    private void StopLowHealthEffects()
    {
        // Detener parpadeo de la barra de vida
        if (healthBarFlashCoroutine != null)
        {
            StopCoroutine(healthBarFlashCoroutine);
            healthBarFlashCoroutine = null;
        }

        // Restaurar color original de la barra
        if (healthBar != null)
        {
            healthBar.color = originalHealthBarColor;
        }

        // Detener parpadeo de pantalla
        if (screenFlashCoroutine != null)
        {
            StopCoroutine(screenFlashCoroutine);
            screenFlashCoroutine = null;
        }

        // Detener corrutina de duracion
        if (lowHealthEffectCoroutine != null)
        {
            StopCoroutine(lowHealthEffectCoroutine);
            lowHealthEffectCoroutine = null;
        }

        // Asegurarse de que el overlay esta invisible
        // if (screenFlashOverlay != null)
        // {
        //     Color transparent = lowHealthScreenFlashColor;
        //     transparent.a = 0f;
        //     screenFlashOverlay.color = transparent;
        // }

        ReportDebug("Efectos de vida baja desactivados.", 1);
    }

    /// <summary>
    /// Rutina que controla la duracion total del efecto de vida baja
    /// </summary>
    private IEnumerator LowHealthEffectDurationRoutine()
    {
        while (lowHealthTimer < lowHealthEffectDuration)
        {
            lowHealthTimer += Time.deltaTime;
            yield return null;
        }

        // Tiempo cumplido, detener efectos
        if (isLowHealth)
        {
            isLowHealth = false;
            StopLowHealthEffects();
        }
    }

    /// <summary>
    /// Rutina de parpadeo de la barra de vida.
    /// </summary>
    private IEnumerator HealthBarFlashRoutine()
    {
        if (healthBar == null) yield break;

        if (originalHealthBarColor == default(Color))
        {
            originalHealthBarColor = healthBar.color;
        }

        Color flashColor = lowHealthBarFlashColor;

        while (true)
        {
            if (healthBar != null)
            {
                healthBar.color = flashColor;
            }

            yield return new WaitForSeconds(healthBarFlashInterval);

            if (healthBar != null)
            {
                healthBar.color = originalHealthBarColor;
            }

            yield return new WaitForSeconds(healthBarFlashInterval);
        }
    }

    /// <summary>
    /// Rutina de parpadeo de pantalla (overlay rojo).
    /// </summary>
    // private IEnumerator ScreenFlashRoutine()
    // {
    //     // if (screenFlashOverlay == null) yield break;

    //     Color visibleColor = lowHealthScreenFlashColor;
    //     Color transparentColor = lowHealthScreenFlashColor;
    //     transparentColor.a = 0f;

    //     while (true)
    //     {
    //         // Fade in
    //         float elapsed = 0f;
    //         float fadeDuration = screenFlashInterval * 0.5f;

    //         while (elapsed < fadeDuration)
    //         {
    //             elapsed += Time.deltaTime;
    //             float t = elapsed / fadeDuration;
    //             screenFlashOverlay.color = Color.Lerp(transparentColor, visibleColor, t);
    //             yield return null;
    //         }

    //         screenFlashOverlay.color = visibleColor;

    //         // Fade out
    //         elapsed = 0f;

    //         while (elapsed < fadeDuration)
    //         {
    //             elapsed += Time.deltaTime;
    //             float t = elapsed / fadeDuration;
    //             screenFlashOverlay.color = Color.Lerp(visibleColor, transparentColor, t);
    //             yield return null;
    //         }

    //         screenFlashOverlay.color = transparentColor;

    //         // Esperar antes del siguiente flash
    //         yield return new WaitForSeconds(screenFlashInterval * 0.5f);
    //     }
    // }

    private void HandleDeathing()
    {
        gameObject.SetActive(false);
    }

    #endregion

    #region UI Healing Effect

    private IEnumerator HealingFlash(float duration = 0.3f)
    {
        float elapsed = 0;
        float alphaStart = healMaxAlpha/225f;

        while (elapsed < duration)
        {
            Debug.LogWarning("curanding");
            elapsed += Time.deltaTime;
            float t = elapsed/duration;
            Color color = new Color (heallingBorder.color.r, heallingBorder.color.g, heallingBorder.color.b, Mathf.Lerp(alphaStart, 0f, t));
            heallingBorder.color = color;
            yield return null;
        }

        heallingBorder.color = new Color (heallingBorder.color.r, heallingBorder.color.g, heallingBorder.color.b, 0f);

    }

    public void DoHeallingUIEffect()
    {
        if (currentHealingRoutine != null)
        {
            StopCoroutine(currentHealingRoutine);
        }

        currentHealingRoutine = StartCoroutine(HealingFlash());
    }

    #endregion

    #region UI Updates & Interaction

    public void SetInteractionPrompt(bool active, string actionName, string actionText)
    {
        if (interactionPromptPanel == null) return;

        interactionPromptPanel.SetActive(active);

        if (active)
        {
            string buttonPrompt = InputIconManager.Instance != null
                ? InputIconManager.Instance.GetPromptForAction(actionName)
                : "[E]";

            interactionPromptText.text = $"{buttonPrompt} {actionText}";
        }
    }

    public void SetInteractionPromptGACHAPON(bool active, string actionName, string actionText)
    {
        if (interactionPromptGACHAPONPanel == null) return;

        interactionPromptGACHAPONPanel.SetActive(active);

        if (active)
        {
            string buttonPrompt = InputIconManager.Instance != null
                ? InputIconManager.Instance.GetPromptForAction(actionName)
                : "[E]";

            interactionPromptGACHAPONText.text = $"{buttonPrompt} {actionText}";
        }
    }

    private void UpdateLifeStageIcon(PlayerHealth.LifeStage newStage)
    {
        if (!isInitialize) return;
        pendingStage = newStage;
        handAnim.PlaySmashSecuence();
    }

    private IEnumerator onStartIgnoreHandAnim()
    {
        yield return null;
        isInitialize = true;
    }

    /// <summary>
    /// Funcion que actualiza el icono de la etapa de vida en el HUD.
    /// </summary>
    /// <param name="newStage"> Nueva etapa de vida del jugador </param>
    private void ApplyLifeStageIcon()
    {
        LifeStageIcon foundIcon = lifeStageIcons.Find(icon => icon.stage == pendingStage);
        if (foundIcon != null && lifeStageIconImage != null)
        {
            lifeStageIconImage.sprite = foundIcon.icon;
            ReportDebug($"Icono del HUD actualizado a: {pendingStage}", 1);
        }

        if (pendingStage == PlayerHealth.LifeStage.Adult)
        {
            if (adultRootStage != null) adultRootStage.SetActive(true);
            if (elderRootStage != null) elderRootStage.SetActive(false);
        }
        else if (pendingStage == PlayerHealth.LifeStage.Elder)
        {
            if (adultRootStage != null) adultRootStage.SetActive(false);
            if (elderRootStage != null) elderRootStage.SetActive(true);
        }
        else
        {
            if (adultRootStage != null) adultRootStage.SetActive(false);
            if (elderRootStage != null) elderRootStage.SetActive(false);
        }

    }
    #endregion

    #region UI Berserker Effect

    private void StartGearRotation()
    {
        isGearRotating = true;
        currentShakeIntensity = HUDShakeIntensity;
        if (berserkerShakeCoroutine != null) StopCoroutine(berserkerShakeCoroutine);
        berserkerShakeCoroutine = StartCoroutine(BerserkerShakeLoop());

    }
    private void StopGearRotation()
    {
        isGearRotating = false;
        StopCoroutine(berserkerShakeCoroutine);
        berserkerShakeCoroutine = null;
        handAnim.getHUDToShake.localPosition = HUDOriginalPos;
    }

    private IEnumerator BerserkerShakeLoop()
    {
        RectTransform hud = handAnim.getHUDToShake;
        if(hud == null) yield break;

        HUDOriginalPos = hud.localPosition;

        while (true)
        {
            float randomX = UnityEngine.Random.Range(-currentShakeIntensity, currentShakeIntensity);
            float randomY = UnityEngine.Random.Range(-currentShakeIntensity, currentShakeIntensity);

            hud.localPosition = HUDOriginalPos + new Vector2(randomX, randomY);
            yield return null;
        }
    }

    private void HandleStaminaChange(float current, float max)
    {
        if (!isGearRotating) return;
        currentShakeIntensity =  current <= 0? HUDnoStamiShakeIntensity : HUDShakeIntensity;
    }

    #endregion

    #region Logging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Funcion de depuracion para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <param name="message">Mensaje a reportar.</param>
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[HUDManager] {message}");
                break;
            case 2:
                Debug.LogWarning($"[HUDManager] {message}");
                break;
            case 3:
                Debug.LogError($"[HUDManager] {message}");
                break;
            default:
                Debug.Log($"[HUDManager] {message}");
                break;
        }
    }

    #endregion
}