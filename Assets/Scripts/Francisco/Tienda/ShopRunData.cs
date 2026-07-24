using System.Collections.Generic;
using UnityEngine;

public static class ShopRunData
{
    private static readonly HashSet<TypeEffect> equippedCategories = new HashSet<TypeEffect>();

    public static bool IsCategoryEquipped(TypeEffect category)
        => equippedCategories.Contains(category);

    public static void EquipCategory(TypeEffect category)
        => equippedCategories.Add(category);

    public static void UnequipCategory(TypeEffect category)
        => equippedCategories.Remove(category);


    private static readonly HashSet<ShopItem> equippedEffectItems = new HashSet<ShopItem>();

    public static bool IsEffectItemEquipped(ShopItem item)
        => equippedEffectItems.Contains(item);

    public static void EquipEffectItem(ShopItem item)
        => equippedEffectItems.Add(item);

    public static void UnequipEffectItem(ShopItem item)
        => equippedEffectItems.Remove(item);

    public static void ResetRun()
    {
        equippedCategories.Clear();
        equippedEffectItems.Clear();
        Debug.Log("[ShopRunData] Run reiniciado: categorías e ítems de efecto limpiados.");
    }
}