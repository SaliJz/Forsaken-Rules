using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestiona una habilidad que potencia las estadisticas del jugador mientras esta activa.
/// La habilidad se activa y desactiva, pidiendo al PlayerStatsManager que aplique los modificadores.
/// </summary>
public class ShieldSkill : MonoBehaviour, PlayerControlls.IAbilitiesActions, IPlayerSpecialAbility
{
    #region Enums & Structs

    [System.Serializable]
    public struct BuffSettings
    {
        [Range(0f, 10f)] public float MoveMultiplier;
        [Range(0f, 10f)] public float AttackDamageMultiplier;
        [Range(0f, 10f)] public float AttackSpeedMultiplier;
        [Range(0f, 10f)] public float ToughnessDamageMultiplier;
        [Range(0f, 10f)] public float HealthDrainAmount;
        public bool DisableShieldThrow;

        public BuffSettings(float move, float dmg, float speed, float drain, float toughnessMult = 1f, bool disableThrow = true)
        {
            MoveMultiplier = move;
            AttackDamageMultiplier = dmg;
            AttackSpeedMultiplier = speed;
            HealthDrainAmount = drain;
            ToughnessDamageMultiplier = toughnessMult;
            DisableShieldThrow = disableThrow;
        }
    }

    #endregion

    #region Inspector - References

    [Header("References")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private PlayerAudioController audioController;
    [SerializeField] private PlayerShaderCtrl shaderCtrl;
    [SerializeField] private GameObject visualModelObject;
    [SerializeField] private GameObject warningMessagePrefab;

    #endregion

    #region Inspector - Stamina Settings

    [Header("Stamina System")]
    [Tooltip("Cantidad maxima de estamina que la habilidad puede consumir. " +
        "Cuando se agota, la habilidad seguira activa pero consumira vida en su lugar.")]
    [SerializeField] private float maxStamina = 100f;
    [Tooltip("Cantidad de estamina que se consume por segundo mientras la habilidad esta activa.")]
    [SerializeField] private float baseStaminaDrainRate = 10f;
    [Tooltip("Cantidad de estamina que se recarga por segundo cuando la habilidad esta inactiva.")]
    [SerializeField] private float staminaRechargeRate = 20f;
    [Tooltip("Si esta activado, la habilidad solo se puede activar si la estamina esta completamente llena. " +
        "Si esta desactivado, la habilidad se puede activar con estamina parcial, pero requerira recarga total si la estamina llega a 0.")]
    [SerializeField] private bool requireFullStaminaToActivate = false;
    [Tooltip("Si requireFullStaminaToActivate esta desactivado, esta variable define cuanta estamina minima se necesita para activar la habilidad. " +
        "Si la estamina llega a 0 durante el uso, se requerira recarga total para volver a activar.")]
    [SerializeField] private float minStaminaToActivate = 10f;

    #endregion

    #region Inspector - Buff Settings

    [Header("Buffs por Etapa de Vida")]
    [SerializeField] private BuffSettings youngBuffs = new BuffSettings(1.25f, 1.125f, 1.125f, 6f, 1.25f);
    [SerializeField] private BuffSettings adultBuffs = new BuffSettings(1.25f, 1.125f, 1.125f, 4f, 1.25f);
    [SerializeField] private BuffSettings elderBuffs = new BuffSettings(1.25f, 1.125f, 1.125f, 2f, 1.25f);

    #endregion

    #region Inspector - VFX & Feedback Settings

    [Header("VFX")]
    [Tooltip("Activa o desactiva el manejo automatico del Outline (VFX) desde esta habilidad.")]
    [SerializeField] private bool manageOutlineVFX = true;
    [Tooltip("Material cuando la habilidad esta activa y tiene estamina.")]
    [SerializeField] private Material skillActiveMaterial;
    [Tooltip("Material cuando la habilidad sigue activa pero consume vida (sin estamina).")]
    [SerializeField] private Material skillExhaustedMaterial;

    [Header("Low Health Feedback")]
    [SerializeField] private string warningMessageText = "*Tos* *Tos*\n!No puedo mas!";
    [SerializeField] private Color warningMessageColor = Color.red;
    [SerializeField] private float warningMessageOffset = 2.5f;
    [SerializeField] private float warningMessageDuration = 2f;

    #endregion

    #region Internal State

    private float currentStamina;
    private float healthDrainTimer;
    private bool isStaminaConsumptionPrevented = false;
    private const string SHIELD_SKILL_MODIFIER_KEY = "ShieldSkillBuff";
    private const float LOW_HEALTH_THRESHOLD = 10f;
    private float currentStaminaDrainRate;
    private bool canDeactivate = true;
    private bool wasSteamActivateSkillPressed;

    private PlayerControlls playerControls;
    private PlayerHealth playerHealth;
    private PlayerHealth.LifeStage lastKnownLifeStage;

    private Renderer modelRenderer;
    private Material storedBaseMaterial;

    private bool inputBlocked = false;
    private bool isForcedActive = false;
    private GameObject currentWarningMessage;

    private bool wasHealthTooLow = false;
    private bool hasStaminaBeenFullyDepleted = false;

    #endregion

    #region Public Properties & Events

    public bool isSkillActive { get; private set; }
    public bool IsActive => isSkillActive;
    public float CurrentToughnessMultiplier { get; private set; } = 1.0f;

    public event System.Action OnAbilityActivated;
    public event System.Action OnAbilityDeactivated;
    public static event System.Action<float, float> OnStaminaChanged;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        statsManager = GetComponent<PlayerStatsManager>();
        audioController = GetComponent<PlayerAudioController>();
        shaderCtrl = GetComponentInChildren<PlayerShaderCtrl>();

        currentStamina = maxStamina;

        if (statsManager == null)
        {
            Debug.LogError("PlayerStatsManager no esta asignado en ShieldSkill. La habilidad no funcionara.", this);
            enabled = false;
            return;
        }

        if (visualModelObject != null)
        {
            modelRenderer = visualModelObject.GetComponent<Renderer>();
            if (modelRenderer != null)
            {
                storedBaseMaterial = modelRenderer.sharedMaterial;
            }
            else
            {
                Debug.LogWarning("El visualModelObject asignado no tiene un componente Renderer.", this);
            }
        }
        else
        {
            Debug.LogWarning("visualModelObject no asignado en ShieldSkill. No habra feedback visual de materiales.", this);
        }

        playerControls = new PlayerControlls();
        playerControls.Abilities.SetCallbacks(this);
    }

    private void Start()
    {
        // Esto obliga a imprimir en consola que es lo que realmente tiene la variable 
        // apenas empieza el juego.
        Debug.Log($"[ShieldSkill] Datos cargados en Start - Young Move Mult: {youngBuffs.MoveMultiplier}");

        if (youngBuffs.MoveMultiplier == 0)
        {
            Debug.LogError("!ERROR! El Inspector esta entregando 0 al script. Revisa los Overrides del Prefab.");
        }
    }

    private void OnEnable()
    {
        PlayerHealth.OnLifeStageChanged += HandleLifeStageChanged;
        playerControls?.Abilities.Enable();

        if (statsManager != null)
        {
            PlayerStatsManager.OnStatChanged += HandleStaminaStatChanged;
        }
    }

    private void Update()
    {
        ReadSteamSkillInput();

        if (!isForcedActive)
        {
            bool isHealthLowNow = playerHealth.CurrentHealth <= LOW_HEALTH_THRESHOLD;

            if (isHealthLowNow && !wasHealthTooLow)
            {
                if (isSkillActive)
                {
                    ShowLowHealthWarning();
                    DeactivateSkill();
                    Debug.Log("[ShieldSkill] Habilidad desactivada: salud demasiado baja.");
                }

                wasHealthTooLow = true;
            }
            else if (!isHealthLowNow && wasHealthTooLow)
            {
                wasHealthTooLow = false;
                Debug.Log("[ShieldSkill] Salud recuperada. La habilidad puede usarse nuevamente.");
            }
        }

        if (isSkillActive)
        {
            UpdateActiveSkill();
            UpdateMaterialState();
        }
        else
        {
            RechargeStamina();
        }
    }

    private void OnDisable()
    {
        PlayerHealth.OnLifeStageChanged -= HandleLifeStageChanged;
        playerControls?.Abilities.Disable();
        RestoreOriginalMaterial();

        if (isSkillActive) DeactivateSkill();

        if (statsManager != null)
        {
            PlayerStatsManager.OnStatChanged -= HandleStaminaStatChanged;
        }
    }

    private void OnDestroy()
    {
        PlayerHealth.OnLifeStageChanged -= HandleLifeStageChanged;
        playerControls?.Dispose();
        RestoreOriginalMaterial();

        if (isSkillActive) DeactivateSkill();

        if (currentWarningMessage != null)
        {
            Destroy(currentWarningMessage);
        }
    }

    private void ReadSteamSkillInput()
    {
        if (SteamInputManager.Instance == null) return;
        if (PauseController.Instance != null && PauseController.IsGamePaused) return;
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;

        bool pressed = SteamInputManager.Instance.GetActivateSkillPressed();

        if (pressed && !wasSteamActivateSkillPressed)
        {
            if (inputBlocked)
            {
                Debug.Log("[ShieldSkill] Input bloqueado durante tutorial.");
                wasSteamActivateSkillPressed = pressed;
                return;
            }

            if (LOW_HEALTH_THRESHOLD >= playerHealth.CurrentHealth)
            {
                Debug.Log("[ShieldSkill] Salud del jugador demasiado baja. La habilidad no puede activarse.");
                ShowLowHealthWarning();
                wasSteamActivateSkillPressed = pressed;
                return;
            }

            ToggleSkill();
        }

        wasSteamActivateSkillPressed = pressed;
    }

    #endregion

    #region Initialization & Data Sync

    private void UpdateStaminaConsumptionFromStats()
    {
        if (statsManager == null)
        {
            currentStaminaDrainRate = baseStaminaDrainRate;
            return;
        }

        float staminaConsumptionMod = statsManager.GetStat(StatType.StaminaConsumption);

        if (staminaConsumptionMod <= 0f) staminaConsumptionMod = 0.1f;

        currentStaminaDrainRate = baseStaminaDrainRate + staminaConsumptionMod;
    }

    private void HandleStaminaStatChanged(StatType statType, float newValue)
    {
        if (statType == StatType.StaminaConsumption)
        {
            UpdateStaminaConsumptionFromStats();
        }
    }

    /// <summary>
    /// Maneja el cambio de etapa de vida mientras la habilidad esta activa.
    /// </summary>
    private void HandleLifeStageChanged(PlayerHealth.LifeStage newStage)
    {
        if (!isSkillActive)
        {
            lastKnownLifeStage = newStage;
            return;
        }

        Debug.Log($"[ShieldSkill] Cambio de etapa detectado durante habilidad: {lastKnownLifeStage} -> {newStage}");

        lastKnownLifeStage = newStage;

        StartCoroutine(ReapplySkillModifiersNextFrame());
    }

    #endregion

    #region Core Skill Logic

    public void OnActivateSkill(InputAction.CallbackContext context)
    {
        if (PauseController.Instance != null && PauseController.IsGamePaused) return;
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;

        if (!context.started) return;

        if (inputBlocked)
        {
            Debug.Log("[ShieldSkill] Input bloqueado durante tutorial.");
            return;
        }

        if (LOW_HEALTH_THRESHOLD >= playerHealth.CurrentHealth)
        {
            Debug.Log("[ShieldSkill] Salud del jugador demasiado baja. La habilidad no puede activarse.");
            ShowLowHealthWarning();
            return;
        }

        ToggleSkill();
    }

    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
        Debug.Log($"[ShieldSkill] Input {(blocked ? "BLOQUEADO" : "DESBLOQUEADO")}");
    }

    public void SetForcedActive(bool forced)
    {
        isForcedActive = forced;
        Debug.Log($"[ShieldSkill] Habilidad {(forced ? "FORZADA ACTIVA" : "LIBERADA")}");
    }

    public void ToggleSkillDirectly()
    {
        if (LOW_HEALTH_THRESHOLD >= playerHealth.CurrentHealth)
        {
            ShowLowHealthWarning();
            Debug.Log("[ShieldSkill] Salud del jugador demasiado baja. La habilidad no puede activarse.");
            return;
        }

        if (!CanActivateSkill())
        {
            ShowNoStaminaWarning();
            return;
        }

        ToggleSkill();
    }

    public void DeactivateSkillPublic()
    {
        if (isForcedActive)
        {
            Debug.Log("[ShieldSkill] No se puede desactivar - esta forzada activa");
            return;
        }

        if (isSkillActive) DeactivateSkill();
    }

    private void ToggleSkill()
    {
        if (isForcedActive && isSkillActive)
        {
            Debug.Log("[ShieldSkill] No se puede desactivar - esta forzada activa");
            return;
        }

        if (isSkillActive && canDeactivate) DeactivateSkill();
        else
        {
            if (!CanActivateSkill())
            {
                ShowNoStaminaWarning();
                return;
            }

            ActivateSkill();
        }
    }

    public void SetDeactivationLock(bool isLocked)
    {
        canDeactivate = !isLocked;
    }

    private void ActivateSkill()
    {
        isSkillActive = true;

        if (manageOutlineVFX)
        {
            shaderCtrl?.BersekerOutline(true);
        }

        if (audioController != null)
        {
            audioController.PlayBerserkerAbility(true);
        }

        healthDrainTimer = 0f;
        lastKnownLifeStage = playerHealth.CurrentLifeStage;

        BuffSettings currentBuffs = GetCurrentBuffs();

        RemoveHealthDrainModifier();

        CurrentToughnessMultiplier = currentBuffs.ToughnessDamageMultiplier;
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "Move", StatType.MoveSpeed, currentBuffs.MoveMultiplier - 1.0f, true);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeDmg", StatType.MeleeAttackDamage, currentBuffs.AttackDamageMultiplier - 1.0f, true);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldDmg", StatType.ShieldAttackDamage, currentBuffs.AttackDamageMultiplier - 1.0f, true);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeSpeed", StatType.MeleeAttackSpeed, currentBuffs.AttackSpeedMultiplier - 1.0f, true);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldSpeed", StatType.ShieldSpeed, currentBuffs.AttackSpeedMultiplier - 1.0f, true);

        float baseDrain = statsManager.GetBaseStat(StatType.HealthDrainAmount);
        float addedDrain = currentBuffs.HealthDrainAmount - baseDrain;
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "HealthDrain", StatType.HealthDrainAmount, addedDrain);

        UpdateMaterialState();

        OnAbilityActivated?.Invoke();
    }

    private void DeactivateSkill()
    {
        isSkillActive = false;

        if (manageOutlineVFX)
        {
            shaderCtrl?.BersekerOutline(false);
        }

        if (audioController != null)
        {
            audioController.PlayBerserkerAbility(false);
        }

        float beforeMoveValue = statsManager.GetStat(StatType.MoveSpeed);
        float beforeMeleeDmgValue = statsManager.GetStat(StatType.MeleeAttackDamage);
        float beforeShieldDmgValue = statsManager.GetStat(StatType.ShieldAttackDamage);
        float beforeMeleeSpeedValue = statsManager.GetStat(StatType.MeleeAttackSpeed);
        float beforeShieldSpeedValue = statsManager.GetStat(StatType.ShieldSpeed);

        CurrentToughnessMultiplier = 1.0f;
        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "Move");
        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeDmg");
        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldDmg");
        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeSpeed");
        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldSpeed");

        RemoveHealthDrainModifier();

        RestoreOriginalMaterial();

        float afterMoveValue = statsManager.GetStat(StatType.MoveSpeed);
        float afterMeleeDmgValue = statsManager.GetStat(StatType.MeleeAttackDamage);
        float afterShieldDmgValue = statsManager.GetStat(StatType.ShieldAttackDamage);
        float afterMeleeSpeedValue = statsManager.GetStat(StatType.MeleeAttackSpeed);
        float afterShieldSpeedValue = statsManager.GetStat(StatType.ShieldSpeed);

        OnAbilityDeactivated?.Invoke();
    }

    public void DeactivateAbility()
    {
        if (isSkillActive)
        {
            DeactivateSkill();
        }
    }

    private IEnumerator ReapplySkillModifiersNextFrame()
    {
        yield return null;

        BuffSettings currentBuffs = GetCurrentBuffs();

        RemoveHealthDrainModifier();

        CurrentToughnessMultiplier = currentBuffs.ToughnessDamageMultiplier;
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "Move", StatType.MoveSpeed, currentBuffs.MoveMultiplier - 1.0f, true);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeDmg", StatType.MeleeAttackDamage, currentBuffs.AttackDamageMultiplier - 1.0f, true);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldDmg", StatType.ShieldAttackDamage, currentBuffs.AttackDamageMultiplier - 1.0f, true);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "MeleeSpeed", StatType.MeleeAttackSpeed, currentBuffs.AttackSpeedMultiplier - 1.0f, true);
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "ShieldSpeed", StatType.ShieldSpeed, currentBuffs.AttackSpeedMultiplier - 1.0f, true);

        float baseDrain = statsManager.GetBaseStat(StatType.HealthDrainAmount);
        float addedDrain = currentBuffs.HealthDrainAmount - baseDrain;
        statsManager.ApplyNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "HealthDrain", StatType.HealthDrainAmount, addedDrain);

        Debug.Log($"[ShieldSkill] Modificadores reaplicados para etapa {lastKnownLifeStage}");
    }

    private void UpdateActiveSkill()
    {
        if (DialogManager.Instance != null && DialogManager.Instance.IsActive)
        {
            return;
        }

        if (currentStamina > 0)
        {
            ConsumeStamina();
        }
        else
        {
            float currentHealthDrain = statsManager.GetStat(StatType.HealthDrainAmount);

            if (currentHealthDrain > 0)
            {
                healthDrainTimer += Time.deltaTime;
                if (healthDrainTimer >= 1f)
                {
                    playerHealth.TakeDamage(currentHealthDrain, true);
                    healthDrainTimer %= 1f;
                }
            }
        }
    }

    /// <summary>
    /// Remueve el modificador de HealthDrainAmount.
    /// </summary>
    private void RemoveHealthDrainModifier()
    {
        float beforeValue = statsManager.GetStat(StatType.HealthDrainAmount);
        statsManager.RemoveNamedModifier(SHIELD_SKILL_MODIFIER_KEY + "HealthDrain");
        float afterValue = statsManager.GetStat(StatType.HealthDrainAmount);

        Debug.Log($"[ShieldSkill] HealthDrainAmount removido: {beforeValue} -> {afterValue}");
    }

    private BuffSettings GetCurrentBuffs()
    {
        switch (playerHealth.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young: return youngBuffs;
            case PlayerHealth.LifeStage.Adult: return adultBuffs;
            case PlayerHealth.LifeStage.Elder: return elderBuffs;
            default: return new BuffSettings(1f, 1f, 1f, 1f);
        }
    }

    #endregion

    #region Stamina System

    /// <summary>
    /// Verifica si la habilidad puede activarse segun las reglas de estamina.
    /// </summary>
    private bool CanActivateSkill()
    {
        return requireFullStaminaToActivate
        ? currentStamina >= maxStamina
        : currentStamina >= minStaminaToActivate;
    }

    /// <summary>
    /// Consume estamina mientras la habilidad esta activa.
    /// </summary>
    private void ConsumeStamina()
    {
        if (isStaminaConsumptionPrevented) return;

        UpdateStaminaConsumptionFromStats();

        float previousStamina = currentStamina;

        currentStamina -= currentStaminaDrainRate * Time.deltaTime;
        currentStamina = Mathf.Max(0f, currentStamina);

        if (previousStamina > 0f && currentStamina <= 0f)
        {
            hasStaminaBeenFullyDepleted = true;

            string logMessage = requireFullStaminaToActivate
                ? "Requerirá recarga total para reactivar."
                : $"Requerirá un mínimo de {minStaminaToActivate} de estamina para reactivar.";

            Debug.Log($"[ShieldSkill] Estamina completamente agotada {logMessage}");
        }

        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    /// <summary>
    /// Recarga la estamina cuando la habilidad esta inactiva.
    /// </summary>
    private void RechargeStamina()
    {
        if (currentStamina < maxStamina)
        {
            float previousStamina = currentStamina;
            currentStamina += staminaRechargeRate * Time.deltaTime;
            currentStamina = Mathf.Min(maxStamina, currentStamina);

            if (hasStaminaBeenFullyDepleted)
            {
                // Verifica si ya alcanza el umbral necesario
                bool hasReachedRequiredThreshold = requireFullStaminaToActivate
                    ? currentStamina >= maxStamina
                    : currentStamina >= minStaminaToActivate;

                if (hasReachedRequiredThreshold)
                {
                    hasStaminaBeenFullyDepleted = false;
                    Debug.Log("[ShieldSkill] Estamina recuperada al umbral necesario. Habilidad disponible nuevamente.");
                }
            }

            // Notifica cambio de estamina
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }

    /// <summary>
    /// Obtiene el porcentaje actual de estamina (0-1).
    /// </summary>
    public float GetStaminaPercentage()
    {
        return maxStamina > 0 ? currentStamina / maxStamina : 0f;
    }

    /// <summary>
    /// Obtiene la estamina actual.
    /// </summary>
    public float GetCurrentStamina()
    {
        return currentStamina;
    }

    /// <summary>
    /// Obtiene la estamina maxima.
    /// </summary>
    public float GetMaxStamina()
    {
        return maxStamina;
    }

    /// <summary>
    /// Recarga instantaneamente la estamina (util para power-ups o eventos especiales).
    /// </summary>
    public void RechargeStaminaInstantly(float amount)
    {
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        Debug.Log($"[ShieldSkill] Estamina recargada instantaneamente: +{amount}");
    }

    public void PreventStaminaConsumption()
    {
        isStaminaConsumptionPrevented = true;
    }

    public void AllowStaminaConsumption()
    {
        isStaminaConsumptionPrevented = false;
    }

    #endregion

    #region Visual & Audio Effects

    /// <summary>
    /// Actualiza el material del modelo visual basado en el estado de la habilidad y la estamina.
    /// </summary>
    private void UpdateMaterialState()
    {
        if (modelRenderer == null) return;

        bool hasStamina = currentStamina > 0;

        if (manageOutlineVFX)
        {
            shaderCtrl?.SetHasStamina(hasStamina);
            return;
        }

        Material targetMaterial;

        if (isSkillActive)
        {
            if (hasStamina)
            {
                targetMaterial = skillActiveMaterial;
            }
            else
            {
                targetMaterial = skillExhaustedMaterial;
            }
        }
        else
        {
            targetMaterial = storedBaseMaterial;
        }

        if (modelRenderer.sharedMaterial != targetMaterial && targetMaterial != null)
        {
            modelRenderer.material = targetMaterial;
        }
    }

    /// <summary>
    /// Restaura el material base guardado al inicio.
    /// </summary>
    private void RestoreOriginalMaterial()
    {
        if (manageOutlineVFX) return;

        if (modelRenderer != null && storedBaseMaterial != null)
        {
            modelRenderer.material = storedBaseMaterial;
        }
    }

    private void ShowNoStaminaWarning()
    {
        if (currentWarningMessage == null && warningMessagePrefab != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * warningMessageOffset;
            currentWarningMessage = Instantiate(warningMessagePrefab, spawnPosition, Quaternion.identity);
            WarningMessageFloater floater = currentWarningMessage.GetComponent<WarningMessageFloater>();
            if (floater != null)
            {
                floater.SetLifetime(warningMessageDuration);
                floater.SetColor(warningMessageColor);
                floater.SetText("!No tengo suficiente energia!");
            }

            StartCoroutine(DestroyWarningMessageAfterDelay());
        }

        Debug.Log("[ShieldSkill] No hay suficiente estamina para activar la habilidad.");
    }

    /// <summary>
    /// Muestra el mensaje de advertencia cuando el jugador intenta usar la habilidad con salud baja.
    /// </summary>
    private void ShowLowHealthWarning()
    {
        // Reproducir sonido de advertencia
        if (audioController != null)
        {
            audioController.PlayBerserkerLowWarningAbility();
        }

        // Crear mensaje visual si no existe uno activo
        if (currentWarningMessage == null && warningMessagePrefab != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * warningMessageOffset;
            currentWarningMessage = Instantiate(warningMessagePrefab, spawnPosition, Quaternion.identity);

            // Configurar el mensaje
            WarningMessageFloater floater = currentWarningMessage.GetComponent<WarningMessageFloater>();
            if (floater != null)
            {
                floater.SetLifetime(warningMessageDuration);
                floater.SetColor(warningMessageColor);
                floater.SetText(warningMessageText);
            }

            // Destruir el mensaje despues de la duracion especificada
            StartCoroutine(DestroyWarningMessageAfterDelay());
        }

        Debug.Log("[ShieldSkill] Salud del jugador demasiado baja. La habilidad no puede activarse.");
    }

    /// <summary>
    /// Destruye el mensaje de advertencia despues del tiempo especificado.
    /// </summary>
    private IEnumerator DestroyWarningMessageAfterDelay()
    {
        yield return new WaitForSeconds(warningMessageDuration);

        if (currentWarningMessage != null)
        {
            Destroy(currentWarningMessage);
            currentWarningMessage = null;
        }
        else
        {
            yield return null;
        }
    }

    #endregion
}