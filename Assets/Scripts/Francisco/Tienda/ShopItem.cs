using System.Collections.Generic;
using UnityEngine;
using System.Text;
using Unity.VisualScripting.Antlr3.Runtime;

[System.Serializable]
public struct ItemEffect
{
    public StatType type;
    public float amount;
    public bool isPercentage;
}

public enum ItemCategory
{
    AttributeModifiers,
    SkillEnhancers,
    CounterDistortions
}

public enum ItemRarity
{
    Normal,
    Raro,
    SuperRaro
}

[CreateAssetMenu(fileName = "NewShopItem", menuName = "Shop/Shop Item")]
public class ShopItem : ScriptableObject
{
    [Header("Item Info")]
    [Tooltip("Nombre del item, para mostrar en el panel de detalles del item.")]
    public string itemName;
    [Tooltip("Descripción narrativa del item, para mostrar en el panel de detalles del item (Gangas o Reliquias) " +
        "o en el panel comparativo de Reliquias.")]
    [TextArea] public string description;

    [Header("Stats")]
    public float cost;
    public List<ItemEffect> benefits;
    public List<ItemEffect> drawbacks;

    [Header("Categorización")]
    public ItemCategory category = ItemCategory.AttributeModifiers;
    public ItemRarity rarity = ItemRarity.Normal;

    [Tooltip("Probabilidad individual de ser seleccionado DENTRO de su rareza. Mayor número = más probable.")]
    public float individualRarityWeight = 1.0f;

    [Header("Tipo de Item")]
    public bool isAmulet = false;
    public bool isTemporary = false;
    public bool isByRooms = false;
    [Tooltip("Duración en segundos (si es temporal por tiempo)")]
    public float temporaryDuration = 0f;
    [Tooltip("Duración en rooms (si es temporal por rooms)")]
    public int temporaryRooms = 0;

    [Header("Categoría de Efecto")]
    public bool hasEffectCategory = false;
    public TypeEffect effectCategory = TypeEffect.Melee;

    public bool IsEffectItem => hasEffectCategory && behavioralEffects != null && behavioralEffects.Count > 0;

    [Header("Visual")]
    [Tooltip("Sprite del ítem para el inventario")]
    public Sprite itemIcon;
    [Tooltip("Modelo 3D o Sprite a instanciar en la tienda")]
    public GameObject ShopItemPrefab;

    [Header("Rarity Materials")]
    public Material outlineNormal;
    public Material outlineRaro;
    public Material outlineSuperRaro;
    
    [Header("Comportamientos/Efectos Eventuales")]
    public List<ItemEffectBase> behavioralEffects;

    public Color GetRarityColor()
    {
        switch (rarity)
        {
            case ItemRarity.SuperRaro:
                return new Color(1f, 0.84f, 0f); // Dorado
            case ItemRarity.Raro:
                return new Color(0.25f, 0.5f, 1f); // Azul
            case ItemRarity.Normal:
            default:
                return new Color(0.6f, 0.6f, 0.6f); // Gris
        }
    }

    /// <summary>
    /// Devuelve unicamente los efectos (beneficios/perjuicios) formateados del item, sin la
    /// descripcion narrativa. Pensado para el Panel Comparativo de reemplazo de reliquias,
    /// donde solo interesa la informacion funcional para poder decidir el intercambio.
    /// </summary>
    public string GetFunctionalStatsOnly()
    {
        StringBuilder sb = new StringBuilder();

        if (benefits != null && benefits.Count > 0)
        {
            foreach (var effect in benefits)
            {
                sb.AppendLine(FormatStatEffect(effect, true));
            }
        }

        if (drawbacks != null && drawbacks.Count > 0)
        {
            foreach (var effect in drawbacks)
            {
                sb.AppendLine(FormatStatEffect(effect, false));
            }
        }

        return sb.ToString();
    }

    public string GetFormattedDescriptionOnly()
    {
        StringBuilder sb = new StringBuilder();
        if (!string.IsNullOrEmpty(description))
        {
            sb.AppendLine(description);
        }
        return sb.ToString();
    }

    public string GetFormattedDescriptionAndStats()
    {
        StringBuilder sb = new StringBuilder();

        if (!string.IsNullOrEmpty(description))
        {
            sb.AppendLine(description);
        }

        sb.AppendLine();

        if (benefits != null && benefits.Count > 0)
        {
            foreach (var effect in benefits)
            {
                sb.AppendLine(FormatStatEffect(effect, true));
            }
        }

        if (drawbacks != null && drawbacks.Count > 0)
        {
            foreach (var effect in drawbacks)
            {
                sb.AppendLine(FormatStatEffect(effect, false));
            }
        }

        return sb.ToString();
    }

    private bool IsInverseStat(StatType statType)
    {
        switch (statType)
        {
            case StatType.Endurance:
            case StatType.Gravity:
                return true;
            default:
                return false;
        }
    }

    private string FormatStatEffect(ItemEffect effect, bool isOriginalBenefit)
    {
        string colorTag = isOriginalBenefit ? "<color=#00FF00>" : "<color=#FF0000>";

        if (effect.amount == 0f)
        {
            return $"<color=#A0A0A0>0.0 en {GetStatTranslation(effect.type)}</color>";
        }

        //bool isInverse = IsInverseStat(effect.type);
        string sign;

        bool shouldBePositive;

        //if (isInverse)
        //{
        //    shouldBePositive = (isOriginalBenefit && effect.amount < 0) ||
        //                      (!isOriginalBenefit && effect.amount > 0);
        //}
        //else
        //{
        //    shouldBePositive = (isOriginalBenefit && effect.amount > 0) ||
        //                      (!isOriginalBenefit && effect.amount < 0);
        //}

        if (effect.amount < 0) shouldBePositive = false;
        else shouldBePositive = true;

        sign = shouldBePositive ? "+" : "-";

        float displayAmount = Mathf.Abs(effect.amount);

        string amountString = sign 
            + displayAmount.ToString("0.##").Replace(",", ".") 
            + (effect.isPercentage ? "%" : "");

        string statName = GetStatTranslation(effect.type);

        return $"{colorTag}{amountString} {statName}</color>";
    }

    public string GetStatTranslation(StatType statType)
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
            //case StatType.DashRangeMultiplier:
            //    return "Alcance del Impulso (Multiplicador)";
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
            //case StatType.ShieldBlockUpgrade:
            //    return "Vida del Escudo";
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

            default:
                return statType.ToString();
        }
    }

    public void ApplyOutlineMaterial(GameObject instance)
    {
        Renderer renderer = instance.GetComponentInChildren<Renderer>();

        if (renderer == null) return;

        Material outlineMat = rarity switch
        {
            ItemRarity.Raro => outlineRaro,
            ItemRarity.SuperRaro => outlineSuperRaro,
            _ => outlineNormal
        };

        if (outlineMat == null) return;

        Material[] mats = renderer.materials;
        if (mats.Length < 2) return;

        mats[1] = outlineMat;
        renderer.materials = mats;
    }
}