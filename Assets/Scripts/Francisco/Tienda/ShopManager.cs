using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ShopManager : MonoBehaviour
{
    #region Enums

    public enum ShopInteractionMode
    {
        MouseHover,
        TriggerProximity
    }

    #endregion

    #region Serialized Fields

    [Header("Interaction Mode")]
    [SerializeField] private ShopInteractionMode interactionMode = ShopInteractionMode.MouseHover;

    [Header("Item Pools")]
    public List<ShopItem> safeRelics;
    public List<Pact> allPacts = new List<Pact>();
    public List<ShopItem> allAmulets;

    [Header("Gachapon Effect Pools")]
    public List<GachaponEffectData> allGachaponEffects = new List<GachaponEffectData>();

    [Header("Shop Generation Settings")]
    [Range(0f, 1f)] public float normalRarityWeight = 0.65f;
    [Range(0f, 1f)] public float rareRarityWeight = 0.25f;
    [Range(0f, 1f)] public float superRareRarityWeight = 0.1f;
    [SerializeField] private float timeOfSpawn = 1f;

    [Header("Shop Prefabs")]
    public List<GameObject> shopItemPrefabs;
    public GameObject itemAppearanceEffectPrefab;

    [Header("Mechanic Item Guarantee")]
    [SerializeField, Range(0, 3)] private int minimumMechanicItems = 1;
    [SerializeField, Range(1, 3)] private int maximumMechanicItems = 2;

    [Header("UI References")]
    [SerializeField] private GameObject shopUIPanel;
    [SerializeField] private RectTransform uiPanelTransform;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemCostText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;
    [SerializeField] private Image costBar;

    [Header("Cost Bar Positioning")]
    [SerializeField] private RectTransform costBarRectTransform;

    [Header("Cost Bar Colors")]
    [SerializeField] private Color affordableColor = Color.green;
    [SerializeField] private Color unaffordableColor = Color.red;

    [Header("Purchase Risk Settings")]
    [SerializeField] private float lowHealthThreshold = 10f;

    [Header("Raycast Settings")]
    [SerializeField] private float raycastDistance = 5f;
    [SerializeField] private LayerMask shopItemLayer;

    [Header("Scene Restrictions")]
    public string[] restrictedPurchaseScenes = { "HUB", "TutorialCompleto", "TransitionLevel01", "TransitionLevel02" };

    [Header("Reroll Settings")]
    public int baseRerollCost = 10;

    [Header("Gamepad Virtual Cursor")]
    [SerializeField] private bool useVirtualCursor = true;

    [Header("Purchase Cooldown")]
    [SerializeField] private float purchaseCooldown = 0.5f;

    [Header("Purchase Panel Mecanic Item Sprites")]
    [SerializeField] private Image MecPanelImage;
    [SerializeField] private Sprite MecMeleeSprite;
    [SerializeField] private Sprite MecShieldSprite;
    [SerializeField] private Sprite MecDashSprite;

    [Header("=== DEBUG ===")]
    [SerializeField] private List<ShopItem> debugAvailableItems = new();
    [SerializeField] private List<ShopItem> debugCurrentShopItems = new();
    [SerializeField] private List<ShopItem> debugPurchasedItems = new();

    #endregion

    #region Private Fields

    private List<GachaponEffectData> availableGachaponEffects = new List<GachaponEffectData>();
    private List<GachaponEffectData> usedGachaponEffects = new List<GachaponEffectData>();

    private readonly List<GameObject> availablePrefabs = new List<GameObject>();
    private readonly List<GameObject> spawnedPrefabs = new List<GameObject>();
    private readonly List<GameObject> _spawnedItemObjects = new List<GameObject>();
    private List<ShopItem> currentShopItems;
    private List<Transform> _shopSpawnLocations;

    private readonly Dictionary<ShopItem, bool> _pendingPurchaseWarning = new Dictionary<ShopItem, bool>();

    private PlayerHealth playerHealth;
    private PlayerStatsManager playerStatsManager;
    private InventoryManager inventoryManager;
    private PlayerControlls playerControls;

    private System.Action pendingPurchaseCallback;
    private System.Action pendingCancelCallback;
    private bool hasPendingReplacement;
    private bool lowHealthWarningActive = false;
    private float lastPurchaseTime;
    private bool forceGagans = false;
    private bool _amuletPurchasedInRun = false;
    private float merchantPriceModifier = 1.0f;

    #endregion

    #region Properties

    public static ShopManager Instance { get; private set; }
    public ShopInteractionMode InteractionMode => interactionMode;
    public bool HasPendingReplacement => hasPendingReplacement;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        playerControls = new PlayerControlls();
        InitializeShopItemPools();
        InitializeGachaponEffectPool();
    }

    private void Start()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        playerStatsManager = FindAnyObjectByType<PlayerStatsManager>();
        inventoryManager = FindAnyObjectByType<InventoryManager>();

        if (shopUIPanel != null)
            shopUIPanel.SetActive(false);

        ResetCostBar();

        UpdateDebugLists();
    }

    private void OnEnable()
    {
        playerControls.Enable();
    }

    private void OnDisable()
    {
        playerControls.Disable();
    }

    private void Update()
    {
        if (interactionMode == ShopInteractionMode.MouseHover && shopUIPanel != null && shopUIPanel.activeInHierarchy)
        {
            PositionUIPanel();
        }
    }

    #endregion

    #region Initialization

    public void InitializeShopItemPools()
    {
        availablePrefabs.Clear();
        spawnedPrefabs.Clear();

        if (shopItemPrefabs == null) return;

        foreach (GameObject prefab in shopItemPrefabs)
        {
            if (prefab == null) continue;

            ShopItemDisplay display = prefab.GetComponent<ShopItemDisplay>();

            if (display != null &&
                display.shopItemData != null &&
                !display.shopItemData.isAmulet)
            {
                availablePrefabs.Add(prefab);
            }
        }

        availablePrefabs.RemoveAll(prefab =>
        {
            ShopItemDisplay display = prefab.GetComponent<ShopItemDisplay>();
            return display != null &&
                   InventoryManager.CurrentRunItems.Contains(display.shopItemData);
        });

        UpdateDebugLists();
    }

    public void InitializeGachaponEffectPool()
    {
        availableGachaponEffects.Clear();
        usedGachaponEffects.Clear();

        if (allGachaponEffects != null && allGachaponEffects.Count > 0)
        {
            availableGachaponEffects.AddRange(allGachaponEffects);
            Debug.Log($"[ShopManager] Gachapon pool initialized with {availableGachaponEffects.Count} effects.");
        }
        else
        {
            Debug.LogWarning("[ShopManager] No Gachapon effects assigned in ShopManager.");
        }
    }

    #endregion

    #region Item Pool Management

    public void ReturnItemToPool(ShopItem item)
    {
        if (item.isAmulet)
        {
            if (!allAmulets.Contains(item))
                allAmulets.Add(item);
        }
        else
        {
            GameObject prefab = shopItemPrefabs?.FirstOrDefault(p =>
            {
                if (p == null) return false;
                ShopItemDisplay d = p.GetComponent<ShopItemDisplay>();
                return d != null && d.shopItemData == item;
            });
            if (prefab != null && !availablePrefabs.Contains(prefab))
                availablePrefabs.Add(prefab);
        }

        Debug.Log($"[ShopManager] {item.itemName} returned to the available pool.");
    }

    public void ResetMerchantRunState()
    {
        _amuletPurchasedInRun = false;
        merchantPriceModifier = 1.0f;
    }

    public void SetMerchantPriceModifier(float modifier)
    {
        merchantPriceModifier = modifier;
    }

    private void UpdateDebugLists()
    {
        debugAvailableItems.Clear();
        debugCurrentShopItems.Clear();
        debugPurchasedItems.Clear();

        foreach (GameObject prefab in availablePrefabs)
        {
            if (prefab == null) continue;

            ShopItemDisplay display = prefab.GetComponent<ShopItemDisplay>();
            if (display != null && display.shopItemData != null)
                debugAvailableItems.Add(display.shopItemData);
        }

        if (currentShopItems != null)
            debugCurrentShopItems.AddRange(currentShopItems);

        debugPurchasedItems.AddRange(InventoryManager.CurrentRunItems);
    }

    public ShopItem GetRandomRewardItem()
    {
        if (availablePrefabs != null && availablePrefabs.Count > 0)
        {
            GameObject prefab = availablePrefabs[Random.Range(0, availablePrefabs.Count)];
            return prefab.GetComponent<ShopItemDisplay>()?.shopItemData;
        }

        if (allAmulets != null && allAmulets.Count > 0)
        {
            return allAmulets[Random.Range(0, allAmulets.Count)];
        }

        return null;
    }

    #endregion

    #region Gachapon Effect Pool

    public GachaponEffectData GetAvailableGachaponEffect(EffectRarity targetRarity)
    {
        if (availableGachaponEffects.Count == 0)
        {
            ResetGachaponEffectPool();
        }

        List<GachaponEffectData> rarityPool = availableGachaponEffects
            .Where(e => e.IsAvailableForRarity(targetRarity))
            .ToList();

        if (rarityPool.Count == 0)
        {
            EffectRarity[] fallbackOrder = { EffectRarity.Legendario, EffectRarity.Epico, EffectRarity.Raro, EffectRarity.Comun };

            foreach (var fallbackRarity in fallbackOrder)
            {
                if (fallbackRarity == targetRarity) continue;

                rarityPool = availableGachaponEffects
                    .Where(e => e.IsAvailableForRarity(fallbackRarity))
                    .ToList();

                if (rarityPool.Count > 0)
                {
                    Debug.LogWarning($"[ShopManager] No effects for rarity {targetRarity}. Using {fallbackRarity} as fallback.");
                    break;
                }
            }
        }

        if (rarityPool.Count == 0)
        {
            ResetGachaponEffectPool();
            rarityPool = availableGachaponEffects
                .Where(e => e.IsAvailableForRarity(targetRarity))
                .ToList();

            if (rarityPool.Count == 0)
            {
                rarityPool = availableGachaponEffects.ToList();
            }
        }

        if (rarityPool.Count > 0)
        {
            float totalWeight = rarityPool.Sum(e => e.poolProbability);
            float roll = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (GachaponEffectData effect in rarityPool)
            {
                currentWeight += effect.poolProbability;
                if (roll <= currentWeight)
                {
                    return effect;
                }
            }

            return rarityPool.Last();
        }

        Debug.LogError("[ShopManager] Could not retrieve any Gachapon effect. Make sure allGachaponEffects has entries.");
        return null;
    }

    public void MarkGachaponEffectAsUsed(GachaponEffectData effect)
    {
        if (effect == null) return;

        if (availableGachaponEffects.Contains(effect))
        {
            availableGachaponEffects.Remove(effect);
            usedGachaponEffects.Add(effect);
            Debug.Log($"[ShopManager] Effect '{effect.effectName}' marked as used. Available: {availableGachaponEffects.Count}, Used: {usedGachaponEffects.Count}");
        }
    }

    private void ResetGachaponEffectPool()
    {
        availableGachaponEffects.Clear();
        availableGachaponEffects.AddRange(allGachaponEffects);
        usedGachaponEffects.Clear();
        Debug.Log($"[ShopManager] Gachapon pool reset. All effects available again ({availableGachaponEffects.Count} effects).");
    }

    public int GetAvailableGachaponEffectsCount()
    {
        return availableGachaponEffects.Count;
    }

    public int GetUsedGachaponEffectsCount()
    {
        return usedGachaponEffects.Count;
    }

    #endregion

    #region Shop Generation & Spawning

    public IEnumerator GenerateShopItems(List<Transform> spawnLocations, Transform parent)
    {
        _shopSpawnLocations = spawnLocations;
        currentShopItems = new List<ShopItem>();
        spawnedPrefabs.Clear();
        _spawnedItemObjects.Clear();

        if (spawnLocations == null || spawnLocations.Count == 0)
        {
            Debug.LogWarning("[ShopManager] No spawn locations.");
            yield break;
        }

        if (availablePrefabs.Count == 0)
        {
            Debug.Log("[ShopManager] Todos los items fueron comprados. Reiniciando pool.");

            InventoryManager.CurrentRunItems.Clear();
            InitializeShopItemPools();
        }

        List<GameObject> prefabsToSpawn = new List<GameObject>();
        int itemsToSelectCount = Mathf.Min(spawnLocations.Count, availablePrefabs.Count);

        List<GameObject> mechanicPool = availablePrefabs
            .Where(p => p != null && p.GetComponent<ShopItemDisplay>()?.shopItemData?.IsEffectItem == true)
            .ToList();

        List<GameObject> passivePool = availablePrefabs
            .Where(p => p != null && p.GetComponent<ShopItemDisplay>()?.shopItemData?.IsEffectItem == false)
            .ToList();

        int mechanicItemsGenerated = 0;

        for (int i = 0; i < itemsToSelectCount; i++)
        {
            bool spawnMechanic = false;

            if (mechanicItemsGenerated >= maximumMechanicItems)
            {
                spawnMechanic = false;
            }
            else
            {
                float categoryRoll = Random.Range(0f, 100f);
                if (categoryRoll <= 40f && mechanicPool.Count > 0)
                {
                    spawnMechanic = true;
                }
            }

            List<GameObject> targetPool = spawnMechanic ? mechanicPool : passivePool;

            if (targetPool.Count == 0)
            {
                targetPool = spawnMechanic ? passivePool : mechanicPool;
                spawnMechanic = !spawnMechanic;
            }

            GameObject picked = SelectWeightedPrefab(targetPool);

            if (picked != null)
            {
                prefabsToSpawn.Add(picked);  
                targetPool.Remove(picked);   

                if (spawnMechanic)
                {
                    mechanicItemsGenerated++;
                }
            }
        }

        EnsureMinimumMechanicItems(prefabsToSpawn, mechanicPool, ref mechanicItemsGenerated);

        for (int i = 0; i < prefabsToSpawn.Count; i++)
        {
            GameObject prefab = prefabsToSpawn[i];
            GameObject spawnedObj = Instantiate(prefab, spawnLocations[i].position, Quaternion.identity, parent);
            _spawnedItemObjects.Add(spawnedObj);
            spawnedPrefabs.Add(prefab);

            ShopItem data = prefab.GetComponent<ShopItemDisplay>()?.shopItemData;
            if (data != null) currentShopItems.Add(data);

            yield return new WaitForSeconds(timeOfSpawn);
        }

        UpdateDebugLists();
    }

    private void EnsureMinimumMechanicItems(List<GameObject> prefabsToSpawn, List<GameObject> mechanicPool, ref int mechanicItemsGenerated)
    {
        if (mechanicItemsGenerated >= minimumMechanicItems) return;

        for (int i = prefabsToSpawn.Count - 1; i >= 0 && mechanicItemsGenerated < minimumMechanicItems; i--)
        {
            GameObject candidate = prefabsToSpawn[i];
            bool isMechanic = candidate.GetComponent<ShopItemDisplay>()?.shopItemData?.IsEffectItem == true;
            if (isMechanic) continue;

            GameObject replacement = SelectWeightedPrefab(mechanicPool);
            if (replacement == null) break;

            prefabsToSpawn[i] = replacement;
            availablePrefabs.Remove(replacement);
            mechanicPool.Remove(replacement);
            availablePrefabs.Add(candidate);

            mechanicItemsGenerated++;
        }
    }

    private GameObject SelectWeightedPrefab(List<GameObject> sourcePool)
    {
        if (sourcePool == null || sourcePool.Count == 0) return null;

        ItemRarity selectedRarity = RollItemRarity();

        List<GameObject> rarityPool = sourcePool
            .Where(p => p != null && p.GetComponent<ShopItemDisplay>()?.shopItemData?.rarity == selectedRarity)
            .ToList();

        if (rarityPool.Count == 0)
            rarityPool = GetFallbackRarityPool(sourcePool);

        if (rarityPool.Count == 0) return null;

        return SelectByIndividualWeight(rarityPool);
    }

    private ItemRarity RollItemRarity()
    {
        float totalWeight = normalRarityWeight + rareRarityWeight + superRareRarityWeight;
        float roll = Random.Range(0f, totalWeight);

        if (roll <= normalRarityWeight)
            return ItemRarity.Normal;

        if (roll <= normalRarityWeight + rareRarityWeight)
            return ItemRarity.Raro;

        return ItemRarity.SuperRaro;
    }

    private List<GameObject> GetFallbackRarityPool(List<GameObject> sourcePool)
    {
        List<GameObject> normal = sourcePool
            .Where(p => p?.GetComponent<ShopItemDisplay>()?.shopItemData?.rarity == ItemRarity.Normal).ToList();
        if (normal.Count > 0) return normal;

        List<GameObject> rare = sourcePool
            .Where(p => p?.GetComponent<ShopItemDisplay>()?.shopItemData?.rarity == ItemRarity.Raro).ToList();
        if (rare.Count > 0) return rare;

        List<GameObject> superRare = sourcePool
            .Where(p => p?.GetComponent<ShopItemDisplay>()?.shopItemData?.rarity == ItemRarity.SuperRaro).ToList();
        if (superRare.Count > 0) return superRare;

        return sourcePool.Where(p => p != null).ToList();
    }

    private GameObject SelectByIndividualWeight(List<GameObject> pool)
    {
        if (pool == null || pool.Count == 0) return null;

        float totalWeight = pool.Sum(p =>
        {
            ShopItem data = p?.GetComponent<ShopItemDisplay>()?.shopItemData;
            return data != null ? Mathf.Max(0f, data.individualRarityWeight) : 0f;
        });

        if (totalWeight <= 0f)
            return pool[Random.Range(0, pool.Count)];

        float roll = Random.Range(0f, totalWeight);
        float current = 0f;

        foreach (GameObject prefab in pool)
        {
            ShopItem data = prefab?.GetComponent<ShopItemDisplay>()?.shopItemData;
            current += data != null ? Mathf.Max(0f, data.individualRarityWeight) : 0f;
            if (roll <= current) return prefab;
        }

        return pool.Last();
    }

    public void SpawnItemWithEffect(ShopItem item, Transform location, Transform parent)
    {
        GameObject prefab = shopItemPrefabs?.FirstOrDefault(p =>
        {
            if (p == null) return false;
            ShopItemDisplay d = p.GetComponent<ShopItemDisplay>();
            return d != null && d.shopItemData == item;
        });

        if (prefab != null)
            StartCoroutine(SpawnItemSequence(prefab, location, parent));
        else
            Debug.LogWarning($"[ShopManager] No prefab found for item '{item?.itemName}'.");
    }

    private IEnumerator SpawnItemSequence(GameObject prefab, Transform location, Transform parent)
    {
        if (itemAppearanceEffectPrefab != null)
        {
            Instantiate(itemAppearanceEffectPrefab, location.position, Quaternion.identity, parent);
            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            yield return new WaitForSeconds(0.1f);
        }

        GameObject spawnedObj = Instantiate(prefab, location.position, Quaternion.identity, parent);
        _spawnedItemObjects.Add(spawnedObj);
        spawnedPrefabs.Add(prefab);
    }

    public void DestroyCurrentItems()
    {
        foreach (GameObject obj in _spawnedItemObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedItemObjects.Clear();
        spawnedPrefabs.Clear();
        UpdateDebugLists();
    }

    public void RerollShop(List<Transform> spawnLocations, Transform parent)
    {
        if (playerHealth == null) return;

        float currentHealth = playerHealth.GetCurrentHealth();
        if (currentHealth < baseRerollCost)
        {
            inventoryManager?.ShowWarningMessage("Not enough health to Reroll.");
            return;
        }

        playerHealth.TakeDamage(baseRerollCost, true);
        DestroyCurrentItems();
        StartCoroutine(GenerateShopItems(spawnLocations, parent));
        UpdateDebugLists();
    }

    public void DisableRemainingItems()
    {
        if (_spawnedItemObjects == null) return;

        foreach (var obj in _spawnedItemObjects)
        {
            if (obj != null && obj.activeSelf)
                obj.SetActive(false);
        }
    }

    public void DisableRemainingAmulets()
    {
        if (_spawnedItemObjects == null) return;

        foreach (var obj in _spawnedItemObjects)
        {
            if (obj == null) continue;
            ShopItemDisplay display = obj.GetComponent<ShopItemDisplay>();
            if (display != null && display.shopItemData != null && display.shopItemData.isAmulet)
                obj.SetActive(false);
        }
    }

    #endregion

    #region Purchase Flow

    public bool CanAttemptPurchase()
    {
        return Time.time > lastPurchaseTime + purchaseCooldown;
    }

    public bool PurchaseItem(ShopItem item)
    {
        if (playerStatsManager == null || playerHealth == null || inventoryManager == null) return false;

        string currentSceneName = SceneManager.GetActiveScene().name;
        bool isRestrictedScene = restrictedPurchaseScenes.Contains(currentSceneName);
        float finalCost = isRestrictedScene ? 0f : CalculateFinalCost(item.cost);

        if ((isRestrictedScene || item.isAmulet) && _amuletPurchasedInRun)
        {
            string msg = isRestrictedScene
                ? "En esta zona solo puedes comprar un artículo por visita."
                : "Solo puedes comprar un amuleto por partida.";
            inventoryManager.ShowWarningMessage(msg);
            return false;
        }

        float currentHealth = playerHealth.GetCurrentHealth();
        if (finalCost > 0 && currentHealth <= finalCost)
        {
            inventoryManager.ShowWarningMessage("No tienes suficiente vida para comprar. ¡Debes sobrevivir!");
            return false;
        }

        float healthAfterPurchase = currentHealth - finalCost;
        if (healthAfterPurchase <= lowHealthThreshold && !isRestrictedScene)
        {
            if (!_pendingPurchaseWarning.ContainsKey(item))
            {
                inventoryManager.ShowWarningMessage("¡Atención! Esta compra te dejará con muy pocos puntos de salud. Pulsa de nuevo para confirmar.");
                _pendingPurchaseWarning.Add(item, true);
                return false;
            }
            _pendingPurchaseWarning.Remove(item);
        }
        else
        {
            _pendingPurchaseWarning.Remove(item);
        }

        if (item.hasEffectCategory && InventoryUIManager.Instance != null)
        {
            int slotIndex = InventoryUIManager.Instance.GetMechanicSlotIndex(item);
            if (slotIndex >= 0)
            {
                bool canProceed = InventoryUIManager.Instance.RequestMechanicItemPurchase(item, slotIndex, this);
                if (!canProceed)
                {
                    hasPendingReplacement = true;
                    return false;
                }
            }
        }

        return CompletePurchase(item);
    }

    public bool CompletePurchase(ShopItem item)
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        bool isRestrictedScene = restrictedPurchaseScenes.Contains(currentSceneName);
        float finalCost = isRestrictedScene ? 0f : CalculateFinalCost(item.cost);
        bool ignoreDrawbacks = isRestrictedScene;

        if (!inventoryManager.TryAddItem(item)) return false;

        foreach (var benefit in item.benefits)
        {
            playerStatsManager.ApplyModifier(benefit.type, benefit.amount,
                isPercentage: benefit.isPercentage,
                isTemporary: item.isTemporary, item.temporaryDuration,
                isByRooms: item.isByRooms, item.temporaryRooms);
        }

        if (!ignoreDrawbacks)
        {
            foreach (var drawback in item.drawbacks)
            {
                playerStatsManager.ApplyModifier(drawback.type, drawback.amount,
                    isPercentage: drawback.isPercentage,
                    isTemporary: item.isTemporary, item.temporaryDuration,
                    isByRooms: item.isByRooms, item.temporaryRooms);
            }
        }

        foreach (var effect in item.behavioralEffects)
        {
            effect.RemoveEffect(playerStatsManager);
            effect.ApplyEffect(playerStatsManager);
        }

        if (!item.isAmulet)
        {
            GameObject purchasedPrefab = shopItemPrefabs?.FirstOrDefault(p =>
                p?.GetComponent<ShopItemDisplay>()?.shopItemData == item);
            if (purchasedPrefab != null)
            {
                availablePrefabs.Remove(purchasedPrefab);
                spawnedPrefabs.Remove(purchasedPrefab);
            }
        }

        if (item.isAmulet || isRestrictedScene) _amuletPurchasedInRun = true;

        if (finalCost > 0) playerHealth.TakeDamage(Mathf.RoundToInt(finalCost), true);

        lastPurchaseTime = Time.time;

        if (isRestrictedScene) DisableRemainingItems();

        if (item.benefits.Any(b => b.type == StatType.ShieldBlockUpgrade))
        {
            playerHealth.EnableShieldBlockUpgrade();
        }

        InventoryUIManager.Instance?.NotifyItemAdded(item);
        UpdateDebugLists();
        return true;
    }

    public float CalculateFinalCost(float baseCost)
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        bool isRestrictedScene = restrictedPurchaseScenes.Contains(currentSceneName);
        if (isRestrictedScene) return 0f;

        float finalCost = baseCost * merchantPriceModifier;

        if (playerStatsManager != null)
        {
            float priceReduction = playerStatsManager.GetStat(StatType.ShopPriceReduction);
            if (priceReduction > 0f)
            {
                float discount = priceReduction / 100f;
                finalCost *= (1f - discount);
            }
        }

        return Mathf.Max(0f, finalCost);
    }

    #endregion

    #region Item Replacement

    public void RegisterPendingPurchaseCallback(System.Action callback)
    {
        pendingPurchaseCallback = callback;
    }

    public void RegisterPendingCancelCallback(System.Action callback)
    {
        pendingCancelCallback = callback;
    }

    public void FireCancelCallback()
    {
        pendingCancelCallback?.Invoke();
        pendingCancelCallback = null;
        pendingPurchaseCallback = null;
        hasPendingReplacement = false;
    }

    public void ExecuteReplacement(ShopItem oldItem, ShopItem newItem)
    {
        if (oldItem != null)
        {
            ReverseItemStats(oldItem);
            InventoryManager.CurrentRunItems.Remove(oldItem);
            ReturnItemToPool(oldItem);
        }
        CompletePurchase(newItem);

        pendingPurchaseCallback?.Invoke();
        pendingPurchaseCallback = null;
        pendingCancelCallback = null;
        hasPendingReplacement = false;
    }

    private void ReverseItemStats(ShopItem item)
    {
        foreach (var benefit in item.benefits)
            playerStatsManager.ApplyModifier(benefit.type, -benefit.amount,
                isPercentage: benefit.isPercentage, isTemporary: false);

        foreach (var drawback in item.drawbacks)
        {
            playerStatsManager.ApplyModifier(drawback.type, -drawback.amount,
                isPercentage: drawback.isPercentage, isTemporary: false);
        }

        foreach (var effect in item.behavioralEffects)
            effect.RemoveEffect(playerStatsManager);
    }

    #endregion

    #region UI

    public void HandleMouseInteraction(ShopItem itemData)
    {
        LockAndDisplayItemDetails(itemData);
    }

    public void LockAndDisplayItemDetails(ShopItem itemData)
    {
        if (itemData == null)
        {
            if (shopUIPanel != null) shopUIPanel.SetActive(false);
            return;
        }

        float finalCost = CalculateFinalCost(itemData.cost);
        DisplayItemUI(itemData, finalCost);
        UpdateCostBar(itemData.cost);
    }

    public void DisplayItemUI(ShopItem itemData, float finalCost)
    {
        if (PauseController.Instance != null && PauseController.IsGamePaused) return;
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;
        if (shopUIPanel == null) return;

        shopUIPanel.SetActive(true);

        string nameWithColor = $"<color=#{ColorUtility.ToHtmlStringRGB(itemData.GetRarityColor())}" +
            $">{itemData.itemName.ToUpper()}</color>";
        float baseCost = itemData.cost * merchantPriceModifier;
        string costText;

        if (playerStatsManager != null)
        {
            float priceReduction = playerStatsManager.GetStat(StatType.ShopPriceReduction);
            if (priceReduction > 0f && finalCost < baseCost)
            {
                costText = $"<s>{Mathf.RoundToInt(baseCost)} HP</s> " +
                    $"-> <color=#00FF00>{Mathf.RoundToInt(finalCost)} HP</color> (-{priceReduction:F0}%)";
            }
            else
            {
                costText = finalCost > 0 ? $"Costo: {Mathf.RoundToInt(finalCost)} HP" : "Costo:  Aquí es gratis";
            }
        }
        else
        {
            costText = finalCost > 0 ? $"Costo: {Mathf.RoundToInt(finalCost)} HP" : "Costo:  Aquí es gratis";
        }

        if(itemData.behavioralEffects != null && itemData.behavioralEffects.Count > 0)
        {
            MecPanelImage.enabled = true;

            Sprite mecItem = itemData.effectCategory switch
            {
                TypeEffect.Dash => MecDashSprite,
                TypeEffect.Melee => MecMeleeSprite,
                TypeEffect.Shield => MecShieldSprite,
                _ => null
            };

            MecPanelImage.sprite = mecItem;
        }
        else
        {
            MecPanelImage.enabled = false;
        }

        itemNameText.text = nameWithColor;
        itemCostText.text = costText;
        itemDescriptionText.text = itemData.GetFormattedDescriptionAndStats();
    }

    public void HideItemUI()
    {
        if (shopUIPanel != null) shopUIPanel.SetActive(false);
    }

    public void SetInteractionPromptActive(bool active)
    {
        HUDManager.Instance.SetInteractionPrompt(active, "Interact", "Comprar");
    }

    public void UpdateCostBar(float cost)
    {
        if (playerHealth == null || costBar == null || itemNameText == null || itemCostText == null) return;

        float currentHealth = playerHealth.GetCurrentHealth();
        float maxHealth = playerHealth.GetMaxHealth();
        float finalCost = CalculateFinalCost(cost);

        float healthPercentage = Mathf.Clamp01(currentHealth / maxHealth);
        float costPercentage = finalCost / maxHealth;

        if (costBarRectTransform != null)
        {
            Vector2 currentAnchorMin = costBarRectTransform.anchorMin;
            Vector2 currentAnchorMax = costBarRectTransform.anchorMax;

            if (finalCost <= currentHealth)
            {
                costBarRectTransform.anchorMin = new Vector2(healthPercentage - costPercentage, currentAnchorMin.y);
                costBarRectTransform.anchorMax = new Vector2(healthPercentage, currentAnchorMax.y);
                costBar.fillOrigin = (int)Image.Origin180.Right;
                costBar.fillAmount = 1f;
            }
            else
            {
                costBarRectTransform.anchorMin = new Vector2(0f, currentAnchorMin.y);
                costBarRectTransform.anchorMax = new Vector2(healthPercentage, currentAnchorMax.y);
                costBar.fillOrigin = (int)Image.Origin180.Left;
                costBar.fillAmount = 1f;
            }
        }

        Color barColor = currentHealth >= finalCost ? affordableColor : unaffordableColor;
        barColor.a = 1f;
        costBar.color = barColor;

        if (currentHealth >= finalCost)
        {
            float healthAfterPurchase = currentHealth - finalCost;
            if (healthAfterPurchase <= lowHealthThreshold && finalCost > 0)
            {
                if (!lowHealthWarningActive)
                {
                    itemNameText.color = Color.white;
                    itemCostText.color = Color.white;
                    lowHealthWarningActive = true;
                }
            }
            else if (lowHealthWarningActive)
            {
                itemNameText.color = Color.white;
                itemCostText.color = Color.white;
                lowHealthWarningActive = false;
            }
        }
        else
        {
            itemNameText.color = Color.white;
            itemCostText.color = Color.white;
            lowHealthWarningActive = false;
        }
    }

    public void ResetCostBar()
    {
        if (costBar == null) return;

        costBar.fillAmount = 0f;

        Color resetColor = affordableColor;
        resetColor.a = 0f;
        costBar.color = resetColor;

        if (costBarRectTransform != null)
        {
            Vector2 currentAnchorMin = costBarRectTransform.anchorMin;
            Vector2 currentAnchorMax = costBarRectTransform.anchorMax;
            costBarRectTransform.anchorMin = new Vector2(0f, currentAnchorMin.y);
            costBarRectTransform.anchorMax = new Vector2(0f, currentAnchorMax.y);
        }

        if (lowHealthWarningActive)
        {
            if (itemNameText != null) itemNameText.color = Color.white;
            if (itemCostText != null) itemCostText.color = Color.white;
            lowHealthWarningActive = false;
        }
    }

    private void PositionUIPanel()
    {
        if (uiPanelTransform == null || uiPanelTransform.parent == null) return;

        Vector2 mousePosition = playerControls.UI.Point.ReadValue<Vector2>();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            uiPanelTransform.parent.GetComponent<RectTransform>(),
            mousePosition,
            null,
            out Vector2 localPoint))
        {
            uiPanelTransform.anchoredPosition = localPoint;
        }
    }

    #endregion

    #region Distortion

    public void SetDistortionActive(DevilDistortionType distortion, bool isCase)
    {
        if (distortion == DevilDistortionType.SealedLuck)
        {
            forceGagans = isCase;
        }
    }

    #endregion
}