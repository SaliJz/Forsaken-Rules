using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using static PlayerHealth;

#region Enums


public enum StatType
{
    MaxHealth,
    MoveSpeed,
    Gravity,
    MeleeAttackDamage,
    MeleeAttackSpeed,
    MeleeRadius,
    ShieldAttackDamage,
    ShieldSpeed,
    ShieldMaxDistance,
    ShieldMaxRebounds,
    ShieldReboundRadius,
    AttackDamage,
    AttackSpeed,
    ShieldBlockUpgrade,
    Endurance,
    HealthDrainAmount,

    LuckStack,
    EssenceCostReduction,
    ShopPriceReduction,
    HealthPerRoomRegen,
    CriticalChance,
    LifestealOnKill,

    CriticalDamageMultiplier,
    DashRangeMultiplier,
    DashRangeFlatBonus,

    KnockbackReceived,
    DashCooldownPost,
    MeleeComboDisplacement,
    ShieldPushForce,
    ShieldReturnSpeed,
    StaminaConsumption
}

#endregion

/// <summary>
/// Clase que maneja las estadsticas del jugador, incluyendo buffs y debuffs temporales (por tiempo o salas/habitaciones/enfrentamientos) o permanentes.
/// Adems muestra todas las stats en un TextMeshProUGUI ordenado y permite abrir/cerrar el panel con la tecla P.
/// </summary>
public partial class PlayerStatsManager : MonoBehaviour
{
    #region Structs

    private struct NamedModifierData
    {
        public bool IsPercentage; // true si value es porcentaje (0.20 = 20%), false si es absoluto
        public float Value;       // porcentaje (0.20) o absoluto (1.25)
        public float AppliedAmount;  // cantidad absoluta que se aplic a currentStats al momento de aplicar (para remover de forma determinista)
    }

    #endregion

    #region Inspector - ScriptableObjects

    [Header("ScriptableObjects")]
    [SerializeField] private PlayerStats baseStatsSO;
    [SerializeField] private PlayerStats currentStatSO;

    #endregion

    #region Inspector - UI & Settings

    [Header("UI: Mostrar estadsticas")]
    [SerializeField] private TextMeshProUGUI statsText;

    [Tooltip("GameObject que contiene el panel con el TextMeshProUGUI. Si no se asigna, se intentar usar el parent del statsText.")]
    [SerializeField] private GameObject statsPanel;

    [Tooltip("Si est activo, se mostrar tambin el valor base entre parntesis.")]
    [SerializeField] private bool showBaseValues = true;

    [Tooltip("Cantidad de decimales a mostrar para los valores.")]
    [SerializeField] private int decimals = 2;

    #endregion

    #region Inspector - Life Stage Modifiers Settings

    [Header("Modificadores de Etapa: Adulto")]
    [Tooltip("Modificador de velocidad de movimiento (Ej: -0.05 para reducir un 5%)")]
    [SerializeField] private float adultMoveSpeedMod = -0.05f;

    [Tooltip("Modificador de velocidad de ataque a melé (Ej: -0.177 para reducir 17.7%)")]
    [SerializeField] private float adultMeleeAttackSpeedMod = -0.177f;

    [Tooltip("Modificador de daño a melé (Ej: 0.5 para aumentar un 50%)")]
    [SerializeField] private float adultMeleeDamageMod = 0.5f;

    [Tooltip("Modificador de velocidad del escudo (Ej: -0.375)")]
    [SerializeField] private float adultShieldSpeedMod = -0.375f;

    [Tooltip("Modificador de daño del escudo (Ej: 0.5)")]
    [SerializeField] private float adultShieldDamageMod = 0.5f;

    [Header("Modificadores de Etapa: Anciano (Elder)")]
    [Tooltip("Modificador de velocidad de movimiento (Ej: -0.1 para reducir un 10%)")]
    [SerializeField] private float elderMoveSpeedMod = -0.1f;

    [Tooltip("Modificador de velocidad de ataque a melé (Ej: -0.316)")]
    [SerializeField] private float elderMeleeAttackSpeedMod = -0.316f;

    [Tooltip("Modificador de daño a melé (Ej: 1.25 para aumentar un 125%)")]
    [SerializeField] private float elderMeleeDamageMod = 1.25f;

    [Tooltip("Modificador de velocidad del escudo (Ej: -0.479)")]
    [SerializeField] private float elderShieldSpeedMod = -0.479f;

    [Tooltip("Modificador de daño del escudo (Ej: 1.25)")]
    [SerializeField] private float elderShieldDamageMod = 1.25f;

    #endregion

    #region Internal State

    [Header("Stats (internas)")]
    [SerializeField] private Dictionary<StatType, float> baseStats = new();
    [SerializeField] private Dictionary<StatType, float> currentStats = new();
    private Dictionary<string, Dictionary<StatType, NamedModifierData>> namedModifiers = new();

    [Header("Life Stage Modifiers")]
    private Dictionary<StatType, float> lifeStageModifiers = new Dictionary<StatType, float>();

    private Dictionary<StatType, int> statVisualState = new();

    private PlayerHealth playerHealth;
    private int roomsCompletedSinceStart = 0;

    #endregion

    #region Public Properties & Events

    public PlayerStats _currentStatSO => currentStatSO;

    public static event Action<StatType, float> OnStatChanged;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Inicializa los estados visuales en 0 para todos los StatType
        foreach (StatType statType in Enum.GetValues(typeof(StatType)))
        {
            statVisualState[statType] = 0;
        }

        InitializeStats();
        LoadCurrentStatsFromSO();
        //ResetCurrentStatsToBase();
    }

    private void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();

        // Si no se asign explicitamente el panel, intentamos usar el parent del TextMeshProUGUI
        if (statsPanel == null && statsText != null)
        {
            if (statsText.transform.parent != null)
            {
                statsPanel = statsText.transform.parent.gameObject;
            }
            else
            {
                statsPanel = statsText.gameObject;
            }
        }

        // Asegurarnos de que TMP permita rich text (necesario para colorear valores).
        if (statsText != null)
        {
            statsText.richText = true;
        }

        UpdateStatsDisplay(); // asegurar display inicial
    }

    private void OnEnable()
    {
        OnStatChanged += HandleStatChanged;
        PlayerHealth.OnLifeStageChanged += ApplyLifeStageModifiers;
    }

    private void OnDisable()
    {
        OnStatChanged -= HandleStatChanged;
        PlayerHealth.OnLifeStageChanged -= ApplyLifeStageModifiers;
    }

    private void Update()
    {
       
    }

    #endregion

    #region Initialization & Reset

    /// <summary>
    /// Inicializa las estadsticas base del jugador a partir de un ScriptableObject.
    /// Se llama en Awake para asegurar que siempre haya valores.
    /// </summary>
    private void InitializeStats()
    {
        if (baseStatsSO == null)
        {
            Debug.LogError("[PlayerStatsManager] Faltó asignar 'playerStats' en el Inspector.");
            return;
        }

        foreach (StatType s in Enum.GetValues(typeof(StatType)))
        {
            float baseValue = GetStatFromSO(baseStatsSO, s);

            baseStats[s] = baseValue;

            if (!statVisualState.ContainsKey(s))
            {
                statVisualState[s] = 0;
            }
        }
    }

    private float GetStatFromSO(PlayerStats statsSO, StatType type)
    {
        switch (type)
        {
            case StatType.MaxHealth: return statsSO.maxHealth;
            case StatType.MoveSpeed: return statsSO.moveSpeed;
            case StatType.Gravity: return statsSO.gravity;
            case StatType.AttackDamage: return statsSO.attackDamage;
            case StatType.AttackSpeed: return statsSO.attackSpeed;
            case StatType.HealthDrainAmount: return statsSO.HealthDrainAmount;
            case StatType.LifestealOnKill: return statsSO.lifestealOnKillAmount;

            case StatType.MeleeAttackDamage: return statsSO.meleeAttackDamage;
            case StatType.MeleeAttackSpeed: return statsSO.meleeSpeed;
            case StatType.MeleeRadius: return statsSO.meleeRadius;

            case StatType.ShieldAttackDamage: return statsSO.shieldAttackDamage;
            case StatType.ShieldSpeed: return statsSO.shieldSpeed;
            case StatType.ShieldMaxDistance: return statsSO.shieldMaxDistance;
            case StatType.ShieldMaxRebounds: return statsSO.shieldMaxRebounds;
            case StatType.ShieldReboundRadius: return statsSO.shieldReboundRadius;
            case StatType.ShieldBlockUpgrade: return statsSO.isShieldBlockUpgradeActive ? 1f : 0f;

            case StatType.LuckStack: return statsSO.luckStackBase;
            case StatType.ShopPriceReduction: return statsSO.shopPriceReductionBase;
            case StatType.HealthPerRoomRegen: return statsSO.healthPerRoomRegenBase;
            case StatType.CriticalChance: return statsSO.criticalChanceBase;

            case StatType.CriticalDamageMultiplier:
                return statsSO.criticalDamageMultiplierBase > 0f ? statsSO.criticalDamageMultiplierBase : 2.0f;

            case StatType.DashRangeMultiplier:
                return statsSO.dashRangeMultiplierBase > 0f ? statsSO.dashRangeMultiplierBase : 1.0f;

            case StatType.DashRangeFlatBonus:
                return statsSO.dashRangeFlatBonusBase;

            case StatType.Endurance:
                return statsSO.damageTakenBase;

            case StatType.KnockbackReceived:
                return statsSO.knockbackReceivedBase;

            case StatType.DashCooldownPost:
                return statsSO.dashCooldownPostBase;

            case StatType.MeleeComboDisplacement:
                return statsSO.meleeComboDisplacementBase > 0f ? statsSO.meleeComboDisplacementBase : 1.0f;

            case StatType.ShieldPushForce:
                return statsSO.shieldPushForceBase;

            case StatType.ShieldReturnSpeed:
                return statsSO.shieldReturnSpeedBase > 0f ? statsSO.shieldReturnSpeedBase : 1.0f;

            case StatType.StaminaConsumption:
                return statsSO.staminaConsumptionBase > 0f ? statsSO.staminaConsumptionBase : 1.0f;

            default:
                Debug.LogWarning($"El StatType {type} no esta mapeado en GetStatFromSO. Retornando 0.");
                return 0f;
        }
    }

    /// <summary>
    /// Restaura los valores afectados por los ítems al cambiar de escena, para no perderlos.
    /// </summary>
    private void LoadCurrentStatsFromSO()
    {
        if (currentStatSO == null) return;

        foreach (StatType s in Enum.GetValues(typeof(StatType)))
        {
            float runValue = GetStatFromSO(currentStatSO, s);
            currentStats[s] = runValue;

            float delta = currentStats[s] - baseStats[s];
            MarkVisualStateFromDelta(s, delta);
        }
    }

    /// <summary>
    /// Funcin que reinicia las estadsticas actuales a sus valores base.
    /// Esto ocurre en cada carga de escena para la nueva instancia.
    /// </summary>
    private void ResetCurrentStatsToBase()
    {
        foreach (var kvp in baseStats)
        {
            currentStats[kvp.Key] = kvp.Value;
            statVisualState[kvp.Key] = 0;
            OnStatChanged?.Invoke(kvp.Key, kvp.Value);
        }

        lifeStageModifiers.Clear();

        UpdateStatsDisplay();
    }

    /// <summary>
    /// Resetea las estadsticas al morir el jugador.
    /// </summary> 
    public void ResetStatsOnDeath()
    {
        if (baseStatsSO != null && currentStatSO != null)
        {
            CopyStatsToSO(baseStatsSO, currentStatSO);
        }

        InitializeStats();
        ResetCurrentStatsToBase();
    }

    /// <summary>
    /// Resetea las estadsticas al iniciar una nueva Run.
    /// </summary>
    public void ResetRunStatsToDefaults()
    {
        if (baseStatsSO != null && _currentStatSO != null)
        {
            CopyStatsToSO(baseStatsSO, _currentStatSO);

            _currentStatSO.currentHealth = _currentStatSO.maxHealth;

            _currentStatSO.isShieldBlockUpgradeActive = false;

            Debug.Log("[PlayerStatsManager] Reset completo de stats para nueva Run ejecutado. Vida Maxima forzada.");

            InitializeStats();
            ResetCurrentStatsToBase();
        }
    }

    private void CopyStatsToSO(PlayerStats sourceStats, PlayerStats target)
    {
        target.maxHealth = sourceStats.maxHealth;
        target.moveSpeed = sourceStats.moveSpeed;
        target.gravity = sourceStats.gravity;
        target.attackDamage = sourceStats.attackDamage;
        target.attackSpeed = sourceStats.attackSpeed;
        target.HealthDrainAmount = sourceStats.HealthDrainAmount;
        target.healthPerRoomRegenBase = sourceStats.healthPerRoomRegenBase;
        target.shopPriceReductionBase = sourceStats.shopPriceReductionBase;
        target.lifestealOnKillAmount = sourceStats.lifestealOnKillAmount;

        target.meleeAttackDamage = sourceStats.meleeAttackDamage;
        target.meleeSpeed = sourceStats.meleeSpeed;
        target.meleeRadius = sourceStats.meleeRadius;

        target.shieldAttackDamage = sourceStats.shieldAttackDamage;
        target.shieldSpeed = sourceStats.shieldSpeed;
        target.shieldMaxDistance = sourceStats.shieldMaxDistance;
        target.shieldMaxRebounds = sourceStats.shieldMaxRebounds;
        target.shieldReboundRadius = sourceStats.shieldReboundRadius;
        target.isShieldBlockUpgradeActive = sourceStats.isShieldBlockUpgradeActive;

        target.luckStackBase = sourceStats.luckStackBase;
        target.criticalChanceBase = sourceStats.criticalChanceBase;
        target.criticalDamageMultiplierBase = sourceStats.criticalDamageMultiplierBase;
        target.dashRangeMultiplierBase = sourceStats.dashRangeMultiplierBase;
        target.dashRangeFlatBonusBase = sourceStats.dashRangeFlatBonusBase;

        target.damageTakenBase = sourceStats.damageTakenBase;
        target.knockbackReceivedBase = sourceStats.knockbackReceivedBase;
        target.dashCooldownPostBase = sourceStats.dashCooldownPostBase;
        target.meleeComboDisplacementBase = sourceStats.meleeComboDisplacementBase;
        target.shieldPushForceBase = sourceStats.shieldPushForceBase;
        target.shieldReturnSpeedBase = sourceStats.shieldReturnSpeedBase;
        target.staminaConsumptionBase = sourceStats.staminaConsumptionBase;
    }

    #endregion

    #region Getters & Queries

    public float GetCurrentStatValue(StatType stat)
    {
        if (currentStats.ContainsKey(stat))
        {
            return currentStats[stat];
        }

        Debug.LogWarning($"[PlayerStatsManager] Intento de obtener stat '{stat}' que no está inicializada. Devolviendo 0.");
        return 0f;
    }

    /// <summary>
    /// Lee el valor actual de la estadstica especificada.
    /// </summary> 
    /// <param name="type"> Stat a consultar.</param>
    public float GetStat(StatType type) => currentStats.TryGetValue(type, out var value) ? value : 0;

    /// <summary>
    /// Lee el valor actual de la estadstica especificada.
    /// </summary> 
    /// <param name="type"> Stat a consultar.</param>
    public float GetCurrentStat(StatType type)
    {
        if (currentStats.ContainsKey(type))
        {
            return currentStats[type];
        }

        return 0f;
    }

    /// <summary>
    /// Lee el valor base de la estadística especificada.
    /// </summary> 
    /// <param name="type"> Stat a consultar.</param>
    public float GetBaseStat(StatType type) => baseStats.TryGetValue(type, out var value) ? value : 0;

    #endregion

    #region Core Stat Modifiers

    private void HandleStatChanged(StatType type, float newValue)
    {
        // Simplemente refresca todo el display cuando cambie cualquier stat.
        UpdateStatsDisplay();
    }

    private void NotifyStatChanged(StatType stat, float newValue)
    {
        OnStatChanged?.Invoke(stat, newValue);
    }

    /// <summary>
    /// Marca el estado visual basado en el delta aplicado.
    /// delta > 0 => subi (verde)
    /// delta < 0 => baj (rojo)
    /// delta == 0 => no cambia el estado (se mantiene)
    /// </summary>
    private void MarkVisualStateFromDelta(StatType type, float delta)
    {
        if (delta > 0f)
            statVisualState[type] = 1;
        else if (delta < 0f)
            statVisualState[type] = -1;
        // si delta == 0 no tocamos el estado: se mantiene la coloracin anterior
    }

    /// <summary>
    /// Setea el valor de la estadstica en el ScriptableObject actual.
    /// </summary>
    /// <param name="so"> ScriptableObject a modificar.</param>
    /// <param name="type"> Stat a modificar.</param>
    /// <param name="value"> Nuevo valor.</param>
    private void SetStatOnSO(PlayerStats so, StatType type, float value)
    {
        switch (type)
        {
            case StatType.MaxHealth: so.maxHealth = value; break;
            case StatType.MoveSpeed: so.moveSpeed = value; break;
            case StatType.Gravity: so.gravity = value; break;
            case StatType.AttackDamage: so.attackDamage = value; break;
            case StatType.AttackSpeed: so.attackSpeed = value; break;
            case StatType.HealthDrainAmount: so.HealthDrainAmount = value; break;
            case StatType.HealthPerRoomRegen: so.healthPerRoomRegenBase = value; break;
            case StatType.ShopPriceReduction: so.shopPriceReductionBase = value; break;
            case StatType.LifestealOnKill: so.lifestealOnKillAmount = value; break;
            case StatType.LuckStack: so.luckStackBase = value; break;

            case StatType.MeleeAttackDamage: so.meleeAttackDamage = value; break;
            case StatType.MeleeAttackSpeed: so.meleeSpeed = value; break;
            case StatType.MeleeRadius: so.meleeRadius = value; break;

            case StatType.ShieldAttackDamage: so.shieldAttackDamage = value; break;
            case StatType.ShieldSpeed: so.shieldSpeed = value; break;
            case StatType.ShieldMaxDistance: so.shieldMaxDistance = value; break;
            case StatType.ShieldMaxRebounds: so.shieldMaxRebounds = Mathf.RoundToInt(value); break;
            case StatType.ShieldReboundRadius: so.shieldReboundRadius = value; break;
            case StatType.ShieldBlockUpgrade: so.isShieldBlockUpgradeActive = value > 0.5f; break;

            case StatType.CriticalChance: so.criticalChanceBase = value; break;
            case StatType.CriticalDamageMultiplier: so.criticalDamageMultiplierBase = value; break;
            case StatType.DashRangeMultiplier: so.dashRangeMultiplierBase = value; break;
            case StatType.DashRangeFlatBonus: so.dashRangeFlatBonusBase = value; break;

            case StatType.Endurance: so.damageTakenBase = value; break;
            case StatType.KnockbackReceived: so.knockbackReceivedBase = value; break;
            case StatType.DashCooldownPost: so.dashCooldownPostBase = value; break;
            case StatType.MeleeComboDisplacement: so.meleeComboDisplacementBase = value; break;
            case StatType.ShieldPushForce: so.shieldPushForceBase = value; break;
            case StatType.ShieldReturnSpeed: so.shieldReturnSpeedBase = value; break;
            case StatType.StaminaConsumption: so.staminaConsumptionBase = value; break;

            default:
                Debug.LogWarning($"El StatType {type} no esta mapeado para la modificacion directa del SO.");
                break;
        }
    }

    private float GetStatMinimum(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHealth:
            case StatType.MeleeAttackDamage:
            case StatType.ShieldAttackDamage:
            case StatType.AttackDamage:
            case StatType.ShieldMaxRebounds:
            case StatType.ShieldMaxDistance:
                return 1f;
            case StatType.MoveSpeed:
            case StatType.MeleeAttackSpeed:
            case StatType.AttackSpeed:
            case StatType.ShieldSpeed:
            case StatType.ShieldReturnSpeed:
            case StatType.DashRangeMultiplier:
            case StatType.MeleeComboDisplacement:
            case StatType.StaminaConsumption:
            case StatType.DashCooldownPost:
                return -5f;
            case StatType.Endurance:
                return 0.1f;
            case StatType.MeleeRadius:
            case StatType.ShieldReboundRadius:
            case StatType.ShieldPushForce:
                return -10f;
            case StatType.LuckStack:
            case StatType.EssenceCostReduction:
            case StatType.ShopPriceReduction:
            case StatType.HealthPerRoomRegen:
            case StatType.CriticalChance:
            case StatType.CriticalDamageMultiplier:
            case StatType.LifestealOnKill:
            case StatType.HealthDrainAmount:
            case StatType.Gravity:
                return 0f;
            case StatType.KnockbackReceived:
                return -0.9f;
            case StatType.DashRangeFlatBonus:
                return -9999f;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Aplica un buff/debuff al stat especificado.
    /// </summary>
    /// <param name="type">Stat a modificar.</param>
    /// <param name="amount">Cantidad (positiva o negativa).</param>
    /// <param name="isPercentage">Si es true, el buff es proporcional al valor base.</param>
    /// <param name="isTemporary">Si es false, el buff es permanente hasta morir.</param>
    /// <param name="duration">Duracin en segundos (solo si es temporal por tiempo).</param>
    /// <param name="isByRooms">Si es true, la duracin se mide por salas/habitaciones/enfrentamientos.</param>
    /// <param name="roomsDuration">Cantidad de salas/habitaciones/enfrentamientos que debe durar.</param>
    public void ApplyModifier(StatType type, float amount, bool isPercentage = false, bool isTemporary = false, float duration = 0f, bool isByRooms = false, int roomsDuration = 0)
    {
        Debug.Log($"[PlayerStatsManager] Aplicando modificador: Stat={type}, Amount={amount}, IsPercentage={isPercentage}, IsTemporary={isTemporary}, Duration={duration}, IsByRooms={isByRooms}, RoomsDuration={roomsDuration}");

        if (!baseStats.ContainsKey(type))
        {
            return;
        }

        float modifierValue = amount;

        if (isPercentage && type != StatType.ShieldBlockUpgrade)
        {
            float baseVal = baseStats[type];
            if (lifeStageModifiers.TryGetValue(type, out var stageMod))
            {
                baseVal += stageMod;
            }

            if (baseVal == 0f)
            {
                modifierValue = amount;
            }
            else
            {
                float percentageFactor = amount / 100f;
                modifierValue = baseVal * percentageFactor;
            }
        }

        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] = Mathf.Max(prev + modifierValue, GetStatMinimum(type));

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

        if (float.IsNaN(currentStats[type]) || float.IsInfinity(currentStats[type]))
        {
            Debug.LogError($"[PlayerStatsManager] Stat '{type}' resultó en un valor inválido ({currentStats[type]}). Se ha reseteado al valor base.");
            currentStats[type] = baseStats.ContainsKey(type) ? baseStats[type] : 0f;
            statVisualState[type] = 0;
        }

        OnStatChanged?.Invoke(type, currentStats[type]);

        if (!isTemporary)
        {
            //baseStats[type] = currentStats[type];
            SetStatOnSO(currentStatSO, type, currentStats[type]);

            return;
        }

        if (isByRooms)
        {
            StartCoroutine(RemoveModifierAfterRooms(type, modifierValue, roomsDuration));
        }
        else if (duration > 0f)
        {
            StartCoroutine(RemoveModifierAfterTime(type, modifierValue, duration));
        }
    }

    public void ModifyPermanentStat(StatType type, float modifierValue)
    {
        if (!baseStats.ContainsKey(type)) return;

        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] = Mathf.Max(prev + modifierValue, GetStatMinimum(type));

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

        if (float.IsNaN(currentStats[type]) || float.IsInfinity(currentStats[type]))
        {
            Debug.LogError($"[PlayerStatsManager] Stat permanente '{type}' resultó en un valor inválido ({currentStats[type]}). Se ha reseteado al valor base.");
            currentStats[type] = baseStats.ContainsKey(type) ? baseStats[type] : 0f;
            statVisualState[type] = 0;
        }

        //baseStats[type] = currentStats[type];
        SetStatOnSO(currentStatSO, type, currentStats[type]);

        OnStatChanged?.Invoke(type, currentStats[type]);
    }

    #endregion

    #region Named & Temporary Modifiers

    /// <summary>
    /// Aplica un modificador temporal identificado por una clave nica. Si ya existe un modificador con esa clave, se sobrescribe.
    /// </summary>
    /// <param name="key">Una clave nica para este modificador (ej. "ShieldSkillBuff").</param>
    /// <param name="type">El tipo de stat a modificar.</param>
    /// <param name="amount">La cantidad a aadir (puede ser negativa).</param>
    public void ApplyNamedModifier(string key, StatType type, float amount, bool isPercentage = false)
    {
        RemoveNamedModifier(key);

        if (!baseStats.ContainsKey(type))
        {
            if (!currentStats.ContainsKey(type)) currentStats[type] = 0f;
        }

        float referenceBase = currentStats.TryGetValue(type, out var curVal) ? curVal : (baseStats.TryGetValue(type, out var b) ? b : 0f);

        float appliedAmount = isPercentage ? referenceBase * amount : amount;

        if (!namedModifiers.ContainsKey(key)) namedModifiers[key] = new Dictionary<StatType, NamedModifierData>();

        namedModifiers[key][type] = new NamedModifierData
        {
            IsPercentage = isPercentage,
            Value = amount,
            AppliedAmount = appliedAmount
        };

        currentStats[type] = referenceBase + appliedAmount;
        OnStatChanged?.Invoke(type, currentStats[type]);
    }

    /// <summary>
    /// Remueve un modificador previamente aplicado con una clave nica.
    /// </summary>
    /// <param name="key">La clave nica del modificador a remover.</param>
    public void RemoveNamedModifier(string key)
    {
        if (namedModifiers.TryGetValue(key, out var modifiers))
        {
            foreach (var modifier in modifiers)
            {
                var stat = modifier.Key;
                var modifiersData = modifier.Value;

                float amountToRemove = modifiersData.AppliedAmount;
                currentStats[stat] = currentStats.TryGetValue(stat, out var cur) ? cur - amountToRemove : -amountToRemove;
                OnStatChanged?.Invoke(stat, currentStats[stat]);
            }
            namedModifiers.Remove(key);
            Debug.Log($"[PlayerStatsManager] Modificador '{key}' removido.");
        }
    }

    public void ClearAllNamedModifiers()
    {
        List<string> keysToRemove = new List<string>(namedModifiers.Keys);
        foreach (string key in keysToRemove)
        {
            RemoveNamedModifier(key);
        }
        Debug.Log("Todos los modificadores de estadísticas nombrados han sido limpiados.");
    }

    public void ApplyMultiplierModifier(string key, StatType type, float multiplier)
    {
        ApplyNamedModifier(key, type, multiplier - 1.0f, true);
        Debug.Log($"[PlayerStatsManager] Multiplicador aplicado: {key} a {type} = x{multiplier}");
    }

    public void ApplyAdditiveModifier(string key, StatType type, float amount)
    {
        ApplyNamedModifier(key, type, amount, false);
        Debug.Log($"[PlayerStatsManager] Modificador aditivo aplicado: {key} a {type} = +{amount}");
    }

    /// <summary>
    /// Aplica un modificador a un stat durante un tiempo determinado y luego lo revierte.
    /// sIRVE para efectos como venenos, ralentizaciones, o buffs.
    /// </summary>
    /// <param name="key">Una clave única para este efecto (ej. "EnemySlow_123").</param>
    /// <param name="type">El stat a modificar.</param>
    /// <param name="amount">La cantidad a añadir (negativa para un debuff).</param>
    /// <param name="duration">La duración del efecto en segundos.</param>
    /// <param name="isPercentage">La cantidad es un porcentaje del valor base (true) o un valor absoluto (false).</param>
    public void ApplyTimedModifier(string key, StatType type, float amount, float duration, bool isPercentage = false)
    {
        StartCoroutine(TimedModifierCoroutine(key, type, amount, duration, isPercentage));
    }

    private IEnumerator TimedModifierCoroutine(string key, StatType type, float amount, float duration, bool isPercentage)
    {
        Debug.Log($"[PlayerStatsManager] Aplicando efecto temporal '{key}' a {type} ({amount}) por {duration}s.");
        ApplyNamedModifier(key, type, amount, isPercentage);

        yield return new WaitForSeconds(duration);

        Debug.Log($"[PlayerStatsManager] Efecto temporal '{key}' ha expirado. Revirtiendo.");
        RemoveNamedModifier(key);
    }

    public void ApplyTemporaryStatByRooms(StatType type, float modifierValue, int rooms)
    {
        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] += modifierValue;

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

        OnStatChanged?.Invoke(type, currentStats[type]);

        StartCoroutine(RemoveModifierAfterRooms(type, modifierValue, rooms));
    }

    /// <summary>
    /// Remueve un modificador temporal despus de que se completen la cantidad especificada de habitaciones.
    /// </summary>
    /// <param name="type"> Stat a modificar.</param>
    /// <param name="modifierValue"> Valor del modificador a remover.</param>
    /// <param name="duration"> Duracin en segundos.</param>
    /// <returns> IEnumerator para la corrutina.</returns>
    private IEnumerator RemoveModifierAfterRooms(StatType type, float modifierValue, int rooms)
    {
        int startRoomCount = roomsCompletedSinceStart;
        int targetRoomCount = startRoomCount + rooms;

        while (roomsCompletedSinceStart < targetRoomCount)
        {
            yield return null;
        }

        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] -= modifierValue;

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

        OnStatChanged?.Invoke(type, currentStats[type]);
        Debug.Log($"Efecto temporal '{type}' removido despus de {rooms} habitaciones.");
    }

    /// <summary>
    /// Aplica una modificacin de estadstica temporal que dura por un tiempo especfico.
    /// </summary>
    public void ApplyTemporaryStatByTime(StatType type, float modifierValue, float duration)
    {
        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] += modifierValue;

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

        OnStatChanged?.Invoke(type, currentStats[type]);

        StartCoroutine(RemoveModifierAfterTime(type, modifierValue, duration));
    }

    /// <summary>
    /// Remueve un modificador temporal despus de que pase el tiempo especificado.
    /// </summary>
    /// <param name="type"> Stat a modificar.</param>
    /// <param name="modifierValue"> Valor del modificador a remover.</param>
    /// <param name="duration"> Duracin en segundos.</param>
    /// <returns> IEnumerator para la corrutina.</returns>
    private IEnumerator RemoveModifierAfterTime(StatType type, float modifierValue, float duration)
    {
        Debug.Log($"Iniciando corrutina para remover modificador '{type}' despus de {duration} segundos.");

        yield return new WaitForSeconds(duration);

        float prev = currentStats.TryGetValue(type, out var p) ? p : 0f;

        currentStats[type] -= modifierValue;

        float delta = currentStats[type] - prev;
        MarkVisualStateFromDelta(type, delta);

        OnStatChanged?.Invoke(type, currentStats[type]);

        Debug.Log($"Efecto temporal '{type}' removido despus de {duration} segundos.");
    }

    public void RemoveAllBehavioralEffects(List<ItemEffectBase> effectsToClean)
    {
        if (effectsToClean == null) return;

        foreach (ItemEffectBase effect in effectsToClean)
        {
            effect.RemoveEffect(this);
            Debug.Log($"Efecto de amuleto revertido y limpiado: {effect.name}");
        }
    }

    #endregion

    #region Life Stage System

    /// <summary>
    /// Aplica modificadores de estadsticas permanentes basados en la etapa de vida del jugador.
    /// Este mtodo es llamado por el evento OnLifeStageChanged de PlayerHealth.
    /// CORRECCIN: ahora aplica solo incrementos positivos segn la etapa (no resta).
    /// </summary>
    /// <param name="newStage">La nueva etapa de vida del jugador.</param>
    private void ApplyLifeStageModifiers(LifeStage newStage)
    {
        var affectedStats = new StatType[]
        {
            StatType.MoveSpeed,
            StatType.MeleeAttackSpeed,
            StatType.MeleeAttackDamage,
            StatType.ShieldSpeed,
            StatType.ShieldAttackDamage
        };

        // Guarda todos los namedModifiers temporales
        var tempNamedModifiers = new Dictionary<string, Dictionary<StatType, NamedModifierData>>(StringComparer.Ordinal);
        foreach (var kvp in namedModifiers)
        {
            var inner = new Dictionary<StatType, NamedModifierData>();
            foreach (var innerKvp in kvp.Value) inner[innerKvp.Key] = innerKvp.Value;
            tempNamedModifiers[kvp.Key] = inner;
        }

        // Retira limpiamente estos modificadores de las estadísticas actuales
        ClearAllNamedModifiers();

        // Reverte solo las matemáticas de la etapa de vida anterior
        foreach (var stat in affectedStats)
        {
            if (lifeStageModifiers.ContainsKey(stat))
            {
                currentStats[stat] -= lifeStageModifiers[stat];
                lifeStageModifiers[stat] = 0f;
            }
        }

        switch (newStage)
        {
            case LifeStage.Young:
                break;
            case LifeStage.Adult:
                ApplyLifeStageMod(StatType.MoveSpeed, adultMoveSpeedMod);
                ApplyLifeStageMod(StatType.MeleeAttackSpeed, adultMeleeAttackSpeedMod);
                ApplyLifeStageMod(StatType.MeleeAttackDamage, adultMeleeDamageMod);
                ApplyLifeStageMod(StatType.ShieldSpeed, adultShieldSpeedMod);
                ApplyLifeStageMod(StatType.ShieldAttackDamage, adultShieldDamageMod);
                break;
            case LifeStage.Elder:
                ApplyLifeStageMod(StatType.MoveSpeed, elderMoveSpeedMod);
                ApplyLifeStageMod(StatType.MeleeAttackSpeed, elderMeleeAttackSpeedMod);
                ApplyLifeStageMod(StatType.MeleeAttackDamage, elderMeleeDamageMod);
                ApplyLifeStageMod(StatType.ShieldSpeed, elderShieldSpeedMod);
                ApplyLifeStageMod(StatType.ShieldAttackDamage, elderShieldDamageMod);
                break;
        }

        namedModifiers.Clear();
        foreach (var modifierKey in tempNamedModifiers.Keys)
        {
            foreach (var statPair in tempNamedModifiers[modifierKey])
            {
                var statType = statPair.Key;
                var modifierData = statPair.Value;

                float applied = modifierData.IsPercentage
                    ? (currentStats.TryGetValue(statType, out var curBase) ? curBase * modifierData.Value : 0f)
                    : modifierData.Value;

                if (!currentStats.ContainsKey(statType)) currentStats[statType] = 0f;

                currentStats[statType] += applied;

                if (!namedModifiers.ContainsKey(modifierKey))
                {
                    namedModifiers[modifierKey] = new Dictionary<StatType, NamedModifierData>();
                }

                namedModifiers[modifierKey][statType] = new NamedModifierData
                {
                    IsPercentage = modifierData.IsPercentage,
                    Value = modifierData.Value,
                    AppliedAmount = applied
                };
            }
        }

        // Notifica de golpe todos los cambios
        foreach (StatType s in Enum.GetValues(typeof(StatType)))
        {
            if (currentStats.ContainsKey(s))
            {
                OnStatChanged?.Invoke(s, currentStats[s]);
            }
        }
    }

    private void ApplyLifeStageMod(StatType stat, float percentage)
    {
        if (!baseStats.ContainsKey(stat)) return;

        float modifier = baseStats[stat] * percentage;
        currentStats[stat] += modifier;
        lifeStageModifiers[stat] = modifier;

        Debug.Log($"[PlayerStatsManager] Modificador de etapa aplicado: {stat} +{percentage * 100}% = +{modifier}");
    }

    #endregion

    #region UI & Display Helpers

    /// <summary>
    /// Alterna el estado (activo/inactivo) del panel de estadsticas.
    /// </summary>
    private void ToggleStatsPanel()
    {
        if (statsPanel == null)
        {
            // Si no hay panel, intentamos alternar directamente el objeto de statsText
            if (statsText != null)
            {
                statsText.gameObject.SetActive(!statsText.gameObject.activeSelf);
                if (statsText.gameObject.activeSelf) UpdateStatsDisplay();
            }
            return;
        }

        bool newState = !statsPanel.activeSelf;
        statsPanel.SetActive(newState);

        // Si se abre el panel, actualizamos el texto para mostrar valores recientes.
        if (newState)
            UpdateStatsDisplay();
    }

    /// <summary>
    /// Construye el texto que se mostrar en el TextMeshProUGUI con todas las stats ordenadas.
    /// </summary>
    private void UpdateStatsDisplay()
    {
        if (statsText == null)
        {
            return;
        }

        var sb = new StringBuilder();

        foreach (StatType stat in Enum.GetValues(typeof(StatType)))
        {
            float current = currentStats.TryGetValue(stat, out var cur) ? cur : 0f;
            float baseVal = baseStats.TryGetValue(stat, out var b) ? b : 0f;

            string formattedCurrent = current.ToString($"F{Mathf.Max(0, decimals)}");
            string coloredCurrent = formattedCurrent;

            // Usamos el estado visual persistente en lugar de comparar con base directamente.
            int visualState = statVisualState.ContainsKey(stat) ? statVisualState[stat] : 0;
            if (visualState == 1)
            {
                // verde para aumento persistente
                coloredCurrent = $"<color=#00FF00>{formattedCurrent}</color>";
            }
            else if (visualState == -1)
            {
                // rojo para disminucin persistente
                coloredCurrent = $"<color=#FF0000>{formattedCurrent}</color>";
            }
            else
            {
                // neutro: mostramos sin color
                coloredCurrent = formattedCurrent;
            }

            if (showBaseValues)
            {
                string formattedBase = baseVal.ToString($"F{Mathf.Max(0, decimals)}");
                sb.AppendLine($"{SplitCamelCase(stat.ToString())}: {coloredCurrent} (base: {formattedBase})");
            }
            else
            {
                sb.AppendLine($"{SplitCamelCase(stat.ToString())}: {coloredCurrent}");
            }
        }

        statsText.text = sb.ToString();
    }

    /// <summary>
    /// Convierte un nombre en CamelCase a una cadena con espacios para mejor lectura.
    /// Ej: ShieldMaxDistance -> Shield Max Distance
    /// </summary>
    private string SplitCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new StringBuilder();
        sb.Append(input[0]);
        for (int i = 1; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsUpper(c) && !char.IsUpper(input[i - 1]))
            {
                sb.Append(' ');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    #endregion

    #region Tracking

    // Incrementa el contador de habitaciones completadas.
    private void IncrementRoomCount()
    {
        roomsCompletedSinceStart++;
        Debug.Log($"[PlayerStatsManager] Sala completada. Contador: {roomsCompletedSinceStart}");
    }

    #endregion
}