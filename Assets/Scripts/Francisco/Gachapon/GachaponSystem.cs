using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public struct GachaponResult
{
    public GachaponEffectData effectPair;
    public EffectRarity rarity;
}

public class GachaponSystem : MonoBehaviour
{
    private PlayerStatsManager playerStatsManager;
    private ShopManager shopManager;

    private Dictionary<EffectRarity, float> baseRarityChances = new Dictionary<EffectRarity, float>()
    {
        { EffectRarity.Comun, 60f },
        { EffectRarity.Raro, 25f },
        { EffectRarity.Epico, 12f },
        { EffectRarity.Legendario, 3f }
    };

    private const float LUCK_BONUS_PER_STACK = 6f;
    private const int MAX_LUCK_STACKS = 3;

    private void Awake()
    {
        playerStatsManager = FindAnyObjectByType<PlayerStatsManager>();
        shopManager = ShopManager.Instance;

        if (shopManager == null)
        {
            shopManager = FindAnyObjectByType<ShopManager>();
        }

        if (playerStatsManager == null)
        {
            Debug.LogError("GachaponSystem no pudo encontrar el PlayerStatsManager en la escena.");
        }

        if (shopManager == null)
        {
            Debug.LogError("GachaponSystem no pudo encontrar el ShopManager en la escena.");
        }
    }

    public GachaponResult PullGachapon()
    {
        if (playerStatsManager == null || shopManager == null)
        {
            Debug.LogError("Faltan dependencias necesarias para realizar el Pull del Gachapon.");
            return new GachaponResult { effectPair = null, rarity = EffectRarity.Comun };
        }

        EffectRarity selectedRarity = CalculateRarityWithLuck();

        GachaponEffectData selectedEffectPair = shopManager.GetAvailableGachaponEffect(selectedRarity);

        if (selectedEffectPair != null)
        {
            if (!selectedEffectPair.IsAvailableForRarity(selectedRarity))
            {
                Debug.LogWarning($"El efecto '{selectedEffectPair.effectName}' no está configurado para rareza {selectedRarity}. Buscando alternativa...");

                selectedEffectPair = shopManager.GetAvailableGachaponEffect(selectedRarity);
            }

            shopManager.MarkGachaponEffectAsUsed(selectedEffectPair);

            Debug.Log($"Gachapon Pull exitoso: '{selectedEffectPair.effectName}' (Rareza obtenida: {selectedRarity})");

            return new GachaponResult
            {
                effectPair = selectedEffectPair,
                rarity = selectedRarity
            };
        }

        Debug.LogWarning("No se pudo obtener un efecto del pool de Gachapon.");
        return new GachaponResult { effectPair = null, rarity = EffectRarity.Comun };
    }

    private EffectRarity CalculateRarityWithLuck()
    {
        float luckStacks = playerStatsManager.GetCurrentStat(StatType.LuckStack);
        luckStacks = Mathf.Min(luckStacks, MAX_LUCK_STACKS);
        float luckBonus = luckStacks * LUCK_BONUS_PER_STACK;

        Dictionary<EffectRarity, float> currentRarityChances = new Dictionary<EffectRarity, float>(baseRarityChances);

        if (luckBonus > 0)
        {
            float bonus = luckBonus;

            float transfer1 = Mathf.Min(bonus, currentRarityChances[EffectRarity.Comun] - 0.01f);
            currentRarityChances[EffectRarity.Comun] -= transfer1;
            currentRarityChances[EffectRarity.Raro] += transfer1;
            bonus -= transfer1;

            if (bonus > 0)
            {
                float transfer2 = Mathf.Min(bonus, currentRarityChances[EffectRarity.Raro] - 0.01f);
                currentRarityChances[EffectRarity.Raro] -= transfer2;
                currentRarityChances[EffectRarity.Epico] += transfer2;
                bonus -= transfer2;
            }

            if (bonus > 0)
            {
                float transfer3 = Mathf.Min(bonus, currentRarityChances[EffectRarity.Epico] - 0.01f);
                currentRarityChances[EffectRarity.Epico] -= transfer3;
                currentRarityChances[EffectRarity.Legendario] += transfer3;
            }
        }

        return SelectRarity(currentRarityChances);
    }

    private EffectRarity SelectRarity(Dictionary<EffectRarity, float> rarityChances)
    {
        float totalWeight = rarityChances.Sum(kvp => kvp.Value);
        float roll = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        List<EffectRarity> orderedRarities = new List<EffectRarity>
            { EffectRarity.Comun, EffectRarity.Raro, EffectRarity.Epico, EffectRarity.Legendario };

        foreach (EffectRarity rarity in orderedRarities)
        {
            if (rarityChances.ContainsKey(rarity))
            {
                currentWeight += rarityChances[rarity];
                if (roll <= currentWeight)
                {
                    return rarity;
                }
            }
        }
        return EffectRarity.Comun;
    }

    public void ApplyEffect(GachaponResult result)
    {
        GachaponEffectData effectPair = result.effectPair;
        EffectRarity obtainedRarity = result.rarity;

        if (effectPair == null)
        {
            Debug.LogWarning("Intento de aplicar un efecto nulo.");
            return;
        }

        var advantages = effectPair.GetAdvantageModifiersForRarity(obtainedRarity);
        foreach (var (statType, value, duration, isPercentage, durationType) in advantages)
        {
            ApplySingleModifier(statType, value, duration, isPercentage, durationType, true, obtainedRarity);
        }

        var disadvantages = effectPair.GetDisadvantageModifiersForRarity(obtainedRarity);
        foreach (var (statType, value, duration, isPercentage, durationType) in disadvantages)
        {
            ApplySingleModifier(statType, value, duration, isPercentage, durationType, false, obtainedRarity);
        }

        Debug.Log($"Gachapon aplicado: {effectPair.effectName}. Rareza obtenida: {obtainedRarity}. Total Ventajas: {advantages.Count}. Total Desventajas: {disadvantages.Count}");
    }

    private void ApplySingleModifier(StatType statType, float modifierValue, float durationValue,
                                    bool isPercentage, EffectDurationType durationType,
                                    bool isAdvantage, EffectRarity rarity)
    {
        float finalModifierValue = modifierValue;

        if (isPercentage)
        {
            float currentStatValue = playerStatsManager.GetCurrentStat(statType);
            finalModifierValue = currentStatValue * (modifierValue / 100f);
        }

        string type = isAdvantage ? "Ventaja" : "Desventaja";

        if (durationType == EffectDurationType.Permanent)
        {
            playerStatsManager.ModifyPermanentStat(statType, finalModifierValue);
        }
        else if (durationType == EffectDurationType.Rounds)
        {
            playerStatsManager.ApplyTemporaryStatByRooms(statType, finalModifierValue, (int)durationValue);
        }
        else if (durationType == EffectDurationType.Time)
        {
            playerStatsManager.ApplyTemporaryStatByTime(statType, finalModifierValue, durationValue);
        }

        Debug.Log($"   -> {type} aplicado [{rarity}]: {statType}. Valor: {finalModifierValue} (Base: {modifierValue}{(isPercentage ? "%" : "")}). Duración: {durationType} por {durationValue}");
    }

    public string GetPoolStatus()
    {
        if (shopManager == null) return "ShopManager no disponible";

        int available = shopManager.GetAvailableGachaponEffectsCount();
        int used = shopManager.GetUsedGachaponEffectsCount();
        int total = available + used;

        return $"Pool de Gachapon: {available}/{total} efectos disponibles ({used} usados)";
    }
}