using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    #region Inspector - UI And Feedback

    [Header("UI Y Mensajes De Advertencia")]
    public GameObject inventoryFullPanel;
    public TextMeshProUGUI inventoryFullText;
    [TextArea]
    public string[] inventoryFullMessages = new string[]
    {
        "Inventario lleno.",
        "No puedes llevar mas."
    };
    public float messageDisplayTime = 2.0f;

    [Header("Visualizacion Del Inventario UI")]
    public Transform itemTextsParent;
    private TextMeshProUGUI[] itemDisplayTexts;

    #endregion

    #region Internal State

    [SerializeField] private static readonly List<ShopItem> currentRunItems = new List<ShopItem>();
    [SerializeField] private readonly List<ItemEffectBase> activeAmuletEffects = new List<ItemEffectBase>();
    [SerializeField] private readonly List<ItemEffectBase> activeEffects = new List<ItemEffectBase>();
    private PlayerStatsManager playerStatsManager;
    private PlayerHealth playerHealth;
    private int messageIndex = 0;
    private Coroutine hideMessageCoroutine;

    public static event Action OnInventoryChanged;

    #endregion

    #region Public Properties And Events

    public int MaxInventorySize { get; private set; } = 21;

    public static List<ShopItem> CurrentRunItems
    {
        get { return currentRunItems; }
    }

    public List<ItemEffectBase> ActiveBehavioralEffects
    {
        get
        {
            List<ItemEffectBase> allEffects = new List<ItemEffectBase>();

            foreach (ShopItem item in CurrentRunItems)
            {
                if (item.behavioralEffects != null)
                {
                    allEffects.AddRange(item.behavioralEffects);
                }
            }

            allEffects.AddRange(activeAmuletEffects);

            return allEffects.Distinct().ToList();
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerStatsManager = FindAnyObjectByType<PlayerStatsManager>();
        if (playerStatsManager == null)
        {
            Debug.LogError("PlayerStatsManager no encontrado.");
        }

        playerHealth = FindAnyObjectByType<PlayerHealth>();
        if (playerHealth == null)
        {
            Debug.LogWarning("PlayerHealth no encontrado. Funciones de curacion/escudo podrian fallar.");
        }
    }

    private void Start()
    {
        if (inventoryFullPanel != null) inventoryFullPanel.SetActive(false);
        else if (inventoryFullText != null) inventoryFullText.gameObject.SetActive(false);

        if (itemTextsParent != null)
        {
            itemDisplayTexts = itemTextsParent.GetComponentsInChildren<TextMeshProUGUI>(false);
            MaxInventorySize = itemDisplayTexts.Length;
        }

        UpdateInventoryUI();
    }

    #endregion

    #region Item Management Logic

    public void ResetRunItems()
    {
        PlayerStatsManager statsManager = FindAnyObjectByType<PlayerStatsManager>();
        if (statsManager == null)
        {
            Debug.LogError("PlayerStatsManager no encontrado para remover efectos.");
            CurrentRunItems.Clear();
            activeAmuletEffects.Clear();
            return;
        }

        foreach (ItemEffectBase effect in ActiveBehavioralEffects.ToList())
        {
            effect.RemoveEffect(statsManager);
        }

        CurrentRunItems.Clear();
        activeAmuletEffects.Clear();

        Debug.Log("[InventoryManager] Inventario y todos los efectos activos reiniciados.");
    }

    public bool TryAddItem(ShopItem item)
    {
        if (item.behavioralEffects != null && item.behavioralEffects.Count > 0)
        {
            HandleEffectReplacement(item);
        }

        // Determinar si es un �tem mec�nico o normal
        bool isMechanic = InventoryUIManager.Instance != null && InventoryUIManager.Instance.GetMechanicSlotIndex(item) >= 0;

        if (isMechanic)
        {
            CurrentRunItems.Add(item);
            UpdateInventoryUI();
            NotifyInventoryChanged();
            return true;
        }
        else
        {
            // Si es normal, cuenta cuantos normales se tiene, sin sumar los mec�nicos
            int normalItemsCount = CurrentRunItems.Count(i =>
                InventoryUIManager.Instance == null || InventoryUIManager.Instance.GetMechanicSlotIndex(i) < 0);

            if (normalItemsCount < MaxInventorySize)
            {
                CurrentRunItems.Add(item);
                UpdateInventoryUI();
                NotifyInventoryChanged();
                return true;
            }
        }

        ShowInventoryFullMessage();
        return false;
    }

    /// <summary>
    /// Remueve el efecto conductual anterior del mismo tipo si existe.
    /// NO aplica el nuevo efecto - eso lo hace ShopManager.CompletePurchase.
    /// </summary>
    private void HandleEffectReplacement(ShopItem newItem)
    {
        foreach (var newEffect in newItem.behavioralEffects)
        {
            ItemEffectBase existingEffect = activeEffects.Find(e => e.typeEffect == newEffect.typeEffect);
            if (existingEffect != null)
            {
                existingEffect.RemoveEffect(playerStatsManager);
                activeEffects.Remove(existingEffect);
            }

            // Registrar el nuevo en activeEffects para futuras busquedas de reemplazo
            if (!activeEffects.Contains(newEffect))
                activeEffects.Add(newEffect);
        }
    }

    public int GetCurrentItemCount()
    {
        return CurrentRunItems.Count;
    }

    public void AddActiveAmuletEffect(ItemEffectBase effect)
    {
        if (effect != null && !activeAmuletEffects.Contains(effect))
        {
            activeAmuletEffects.Add(effect);
            Debug.Log($"[InventoryManager] Efecto de Amuleto/Pacto '{effect.EffectID}' registrado para limpieza.");
        }
    }

    public void ClearInventory()
    {
        ResetRunItems();
        NotifyInventoryChanged();
        UpdateInventoryUI();
    }

    public void AddEffectToPlayer(ItemEffectBase newEffect)
    {
        ItemEffectBase existingEffect = activeEffects.Find(e => e.typeEffect == newEffect.typeEffect);

        if (existingEffect != null)
        {
            existingEffect.RemoveEffect(playerStatsManager);
            activeEffects.Remove(existingEffect);

            Debug.Log($"[Inventory] Mecanica previa de tipo {existingEffect.typeEffect} eliminada.");
        }

        activeEffects.Add(newEffect);
        newEffect.ApplyEffect(playerStatsManager);
    }

    public bool RemoveRandomRelic()
    {
        // Obtener solo reliquias normales para el sorteo del Pacto
        List<ShopItem> normalRelics = CurrentRunItems.Where(i =>
            InventoryUIManager.Instance == null || InventoryUIManager.Instance.GetMechanicSlotIndex(i) < 0
        ).ToList();

        if (normalRelics.Count == 0)
        {
            ShowWarningMessage("No tienes reliquias para quitar.");
            return false;
        }

        normalRelics.Shuffle();
        ShopItem relicToRemove = normalRelics[0]; // Elegir una reliquia normal

        if (playerStatsManager != null)
        {
            foreach (var benefit in relicToRemove.benefits)
            {
                playerStatsManager.ApplyModifier(benefit.type, -benefit.amount, isTemporary: false, isPercentage: benefit.isPercentage);
            }

            foreach (var drawback in relicToRemove.drawbacks)
            {
                playerStatsManager.ApplyModifier(drawback.type, -drawback.amount, isTemporary: false, isPercentage: drawback.isPercentage);
            }

            if (relicToRemove.benefits.Exists(b => b.type == StatType.ShieldBlockUpgrade))
            {
                playerHealth.DisableShieldBlockUpgrade();
            }
        }

        // Se remueve de la lista original
        CurrentRunItems.Remove(relicToRemove);
        UpdateInventoryUI();
        NotifyInventoryChanged();

        ShowWarningMessage($"Pacto cumplido: Se ha quitado la reliquia '{relicToRemove.itemName}'.");
        return true;
    }

    #endregion

    #region UI And Feedback Logic

    private void UpdateInventoryUI()
    {
        if (itemDisplayTexts == null) return;

        // Filtro para que a los textos de la UI solo vayan los �tems normales
        List<ShopItem> normalItems = CurrentRunItems.Where(i =>
            InventoryUIManager.Instance == null || InventoryUIManager.Instance.GetMechanicSlotIndex(i) < 0
        ).ToList();

        for (int i = 0; i < itemDisplayTexts.Length; i++)
        {
            if (i < normalItems.Count)
            {
                itemDisplayTexts[i].text = normalItems[i].itemName;
            }
            else
            {
                itemDisplayTexts[i].text = "";
            }
        }
    }

    public void ShowInventoryFullMessage()
    {
        if (inventoryFullText == null) return;

        string message = "Inventario lleno.";
        if (inventoryFullMessages.Length > 0)
        {
            message = inventoryFullMessages[messageIndex];
            messageIndex = (messageIndex + 1) % inventoryFullMessages.Length;
        }

        ShowWarningMessage(message);
    }

    public void ShowWarningMessage(string message)
    {
        if (inventoryFullText == null) return;

        inventoryFullText.text = message;
        inventoryFullText.color = Color.red;

        if (inventoryFullPanel != null)
        {
            inventoryFullPanel.SetActive(true);
        }
        else
        {
            inventoryFullText.gameObject.SetActive(true);
        }

        if (hideMessageCoroutine != null)
        {
            StopCoroutine(hideMessageCoroutine);
        }
        hideMessageCoroutine = StartCoroutine(HideMessageAfterDelay());
    }

    private IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDisplayTime);

        if (inventoryFullPanel != null)
        {
            inventoryFullPanel.SetActive(false);
        }
        else if (inventoryFullText != null)
        {
            inventoryFullText.gameObject.SetActive(false);
        }
    }

    #endregion

    #region VFX-Melee confimation

    public void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    public bool hasMeleeUpgradeItem()
    {
        return currentRunItems.Any(item => item.benefits != null && 
                item.benefits.Any(b => (b.type == StatType.MeleeAttackDamage || b.type == StatType.MeleeAttackSpeed ||
                b.type == StatType.AttackDamage || b.type == StatType.AttackSpeed) && b.amount > 0));
    }

    public bool hasDashUpgradeItem()
    {
        return currentRunItems.Any(item => item.benefits != null && 
                item.benefits.Any(b => (b.type == StatType.DashRangeMultiplier || b.type == StatType.DashCooldownPost ||
                b.type == StatType.DashRangeFlatBonus) && b.amount > 0) || item.effectCategory == TypeEffect.Dash);
    }

    public bool hasMeleeDisplacement()
    {
        return currentRunItems.Any(item => item.benefits != null &&
                item.benefits.Any(b => b.type == StatType.MeleeComboDisplacement && b.amount > 0));
    }

    public bool hasShieldUpgradeItem()
    {
        return currentRunItems.Any(item => item.benefits != null &&
                item.benefits.Any(b => (b.type == StatType.ShieldAttackDamage || b.type == StatType.ShieldMaxDistance || 
                b.type == StatType.ShieldReturnSpeed || b.type == StatType.ShieldSpeed) && b.amount > 0));
    }

    public bool hasShieldBounceItem()
    {
        return currentRunItems.Any(item => item.benefits != null &&
                item.benefits.Any(b => (b.type == StatType.ShieldMaxRebounds || b.type == StatType.ShieldReboundRadius ||
                b.type == StatType.ShieldPushForce) && b.amount > 0));
    }

    public PlayerVfxCtrl.MecanicUpgrades? getActiveMeleeMecanic()
    {
        foreach (var item in currentRunItems)
        {
            if (item.behavioralEffects == null) continue;

            foreach(var effect in item.behavioralEffects)
            {
                if (effect == null || effect.typeEffect != TypeEffect.Melee) continue;

                if (Enum.TryParse<PlayerVfxCtrl.MecanicUpgrades>(effect.EffectID, true, out var mecanic))
                {
                    return mecanic;
                }
            }
        }

        return null;
    }

    public PlayerVfxCtrl.MecanicUpgrades? getActiveShieldMecanic()
    {
        foreach (var item in currentRunItems)
        {
            if (item.behavioralEffects == null) continue;

            foreach (var effect in item.behavioralEffects)
            {
                if (effect == null || effect.typeEffect != TypeEffect.Shield) continue;

                if (Enum.TryParse<PlayerVfxCtrl.MecanicUpgrades>(effect.EffectID, true, out var mecanic))
                {
                    return mecanic;   
                }
            }
        }

        return null;
    }

    #endregion
}