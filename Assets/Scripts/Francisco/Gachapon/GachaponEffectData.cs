using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public enum EffectRarity { Comun, Raro, Epico, Legendario }

[System.Serializable]
public enum EffectDurationType { Permanent, Rounds, Time }

[System.Serializable]
public class RarityModifierValue
{
    public EffectRarity rarity;
    public float modifierValue;
    public float durationValue = 0;
}

[System.Serializable]
public class GachaponModifier
{
    [Header("Configuración Base")]
    public StatType statType;
    public bool isPercentage = false;
    public EffectDurationType durationType = EffectDurationType.Permanent;

    [Header("Valores por Rareza")]
    public List<RarityModifierValue> valuesByRarity = new List<RarityModifierValue>();

    [Header("Valores por Defecto (si no hay rareza específica)")]
    public float defaultModifierValue = 0f;
    public float defaultDurationValue = 0f;

    public float GetModifierValue(EffectRarity rarity)
    {
        var rarityValue = valuesByRarity.FirstOrDefault(v => v.rarity == rarity);
        return rarityValue != null ? rarityValue.modifierValue : defaultModifierValue;
    }

    public float GetDurationValue(EffectRarity rarity)
    {
        var rarityValue = valuesByRarity.FirstOrDefault(v => v.rarity == rarity);
        return rarityValue != null ? rarityValue.durationValue : defaultDurationValue;
    }
}

[CreateAssetMenu(fileName = "NewGachaponEffect", menuName = "Gachapon/Effect Data", order = 1)]
public class GachaponEffectData : ScriptableObject
{
    [Header("Identificación")]
    public string effectName = "Nuevo Par de Efectos";

    [Header("Probabilidad en el Pool")]
    public float poolProbability = 1f;

    [Header("Efectos (Ventaja)")]
    public List<GachaponModifier> advantageModifiers = new List<GachaponModifier>();

    [Header("Efectos (Desventaja)")]
    public List<GachaponModifier> disadvantageModifiers = new List<GachaponModifier>();

    [Header("Rarezas Disponibles")]
    public List<EffectRarity> availableRarities = new List<EffectRarity>
    {
        EffectRarity.Comun,
        EffectRarity.Raro,
        EffectRarity.Epico,
        EffectRarity.Legendario
    };

    public bool HasAdvantage => advantageModifiers.Count > 0;
    public bool HasDisadvantage => disadvantageModifiers.Count > 0;

    public bool IsAvailableForRarity(EffectRarity rarity)
    {
        return availableRarities.Contains(rarity);
    }

    public List<(StatType statType, float value, float duration, bool isPercentage, EffectDurationType durationType)> GetAdvantageModifiersForRarity(EffectRarity rarity)
    {
        var result = new List<(StatType, float, float, bool, EffectDurationType)>();

        foreach (var modifier in advantageModifiers)
        {
            result.Add((
                modifier.statType,
                modifier.GetModifierValue(rarity),
                modifier.GetDurationValue(rarity),
                modifier.isPercentage,
                modifier.durationType
            ));
        }

        return result;
    }

    public List<(StatType statType, float value, float duration, bool isPercentage, EffectDurationType durationType)> GetDisadvantageModifiersForRarity(EffectRarity rarity)
    {
        var result = new List<(StatType, float, float, bool, EffectDurationType)>();

        foreach (var modifier in disadvantageModifiers)
        {
            result.Add((
                modifier.statType,
                modifier.GetModifierValue(rarity),
                modifier.GetDurationValue(rarity),
                modifier.isPercentage,
                modifier.durationType
            ));
        }

        return result;
    }

    public static string TranslateStatType(StatType statType)
    {
        switch (statType)
        {
            case StatType.MaxHealth:
                return "Salud Máxima";
            case StatType.Endurance:
                return "Resistencia";
            case StatType.HealthDrainAmount:
                return "Drenaje de Vida";

            case StatType.MoveSpeed:
                return "Velocidad de Movimiento";
            case StatType.Gravity:
                return "Gravedad";
            case StatType.DashRangeFlatBonus:
                return "Alcance del Impulso";
            case StatType.DashCooldownPost:
                return "Enfriamiento del Impulso";
            case StatType.KnockbackReceived:
                return "Empuje Recibido";
            case StatType.StaminaConsumption:
                return "Consumo de Energia";

            case StatType.AttackDamage:
                return "Daño a Melé y Distancia";
            case StatType.AttackSpeed:
                return "Velocidad de Ataque a Melé y Distancia";
            case StatType.MeleeAttackDamage:
                return "Daño a Melé";
            case StatType.MeleeAttackSpeed:
                return "Velocidad de Ataque a Melé";
            case StatType.MeleeRadius:
                return "Alcance del Ataque a Melé";
            case StatType.MeleeComboDisplacement:
                return "Desplazamiento al Golpear";
            case StatType.CriticalChance:
                return "Probabilidad de Crítico";
            case StatType.CriticalDamageMultiplier:
                return "Multiplicador de Daño Crítico";
            case StatType.LifestealOnKill:
                return "Robo de Vida por Eliminación";

            case StatType.ShieldAttackDamage:
                return "Daño de Ataque a Distancia";
            case StatType.ShieldSpeed:
                return "Velocidad de Ataque a Distancia";
            case StatType.ShieldMaxDistance:
                return "Alcance del Ataque a Distancia";
            case StatType.ShieldMaxRebounds:
                return "Rebote del Ataque a Distancia";
            case StatType.ShieldReboundRadius:
                return "Alcance del Rebote del Escudo";
            case StatType.ShieldPushForce:
                return "Empuje del Ataque a Distancia";
            case StatType.ShieldReturnSpeed:
                return "Velocidad de Retorno del Ataque a Distancia";

            case StatType.LuckStack:
                return "Suerte Acumulada";
            case StatType.EssenceCostReduction:
                return "Reducción de Costo de Esencia";
            case StatType.ShopPriceReduction:
                return "Reducción de Precio en Tienda";
            case StatType.HealthPerRoomRegen:
                return "Regeneración por Sala";
            default: return statType.ToString();
        }
    }

    public static string TranslateDurationType(EffectDurationType durationType, float durationValue)
    {
        switch (durationType)
        {
            case EffectDurationType.Permanent:
                return "Permanente";
            case EffectDurationType.Rounds:
                return $"{Mathf.CeilToInt(durationValue)} Sala(s)";
            case EffectDurationType.Time:
                return $"{durationValue:F1}s";
            default:
                return durationType.ToString();
        }
    }

    public string GetFormattedDescription(EffectRarity rarity)
    {
        string description = $"<b>{effectName}</b>\n";
        description += $"<color=#FFD700> {rarity}</color>\n\n";

        if (HasAdvantage)
        {
            description += "<color=#00FF00><b>VENTAJAS:</b></color>\n";
            foreach (var modifier in advantageModifiers)
            {
                float value = modifier.GetModifierValue(rarity);
                float duration = modifier.GetDurationValue(rarity);
                string statName = TranslateStatType(modifier.statType);
                string durationText = TranslateDurationType(modifier.durationType, duration);

                string sign = value >= 0 ? "+" : "";
                description += $"  • {statName}: <color=#00FF00>{sign}{value}{(modifier.isPercentage ? "%" : "")}</color>";

                if (modifier.durationType != EffectDurationType.Permanent)
                {
                    description += $" ({durationText})";
                }

                description += "\n";
            }
        }

        if (HasDisadvantage)
        {
            if (HasAdvantage) description += "\n";
            description += "<color=#FF4444><b>DESVENTAJAS:</b></color>\n";
            foreach (var modifier in disadvantageModifiers)
            {
                float value = modifier.GetModifierValue(rarity);
                float duration = modifier.GetDurationValue(rarity);
                string statName = TranslateStatType(modifier.statType);
                string durationText = TranslateDurationType(modifier.durationType, duration);

                string sign = value >= 0 ? "+" : "";
                description += $"  • {statName}: <color=#FF4444>{sign}{value}{(modifier.isPercentage ? "%" : "")}</color>";

                if (modifier.durationType != EffectDurationType.Permanent)
                {
                    description += $" ({durationText})";
                }

                description += "\n";
            }
        }

        return description;
    }

#if UNITY_EDITOR
    [ContextMenu("Inicializar Valores por Rareza")]
    private void InitializeRarityValues()
    {
        foreach (var modifier in advantageModifiers)
        {
            if (modifier.valuesByRarity.Count == 0)
            {
                modifier.valuesByRarity = new List<RarityModifierValue>
                {
                    new RarityModifierValue { rarity = EffectRarity.Comun, modifierValue = 5f, durationValue = 3f },
                    new RarityModifierValue { rarity = EffectRarity.Raro, modifierValue = 10f, durationValue = 5f },
                    new RarityModifierValue { rarity = EffectRarity.Epico, modifierValue = 15f, durationValue = 7f },
                    new RarityModifierValue { rarity = EffectRarity.Legendario, modifierValue = 25f, durationValue = 10f }
                };
            }
        }

        foreach (var modifier in disadvantageModifiers)
        {
            if (modifier.valuesByRarity.Count == 0)
            {
                modifier.valuesByRarity = new List<RarityModifierValue>
                {
                    new RarityModifierValue { rarity = EffectRarity.Comun, modifierValue = -2f, durationValue = 3f },
                    new RarityModifierValue { rarity = EffectRarity.Raro, modifierValue = -4f, durationValue = 5f },
                    new RarityModifierValue { rarity = EffectRarity.Epico, modifierValue = -6f, durationValue = 7f },
                    new RarityModifierValue { rarity = EffectRarity.Legendario, modifierValue = -10f, durationValue = 10f }
                };
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}