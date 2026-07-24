using UnityEngine;
using UnityEngine.InputSystem;
using static ShopManager;

public class ShopItemDisplay : MonoBehaviour, PlayerControlls.IInteractionsActions
{
    public ShopItem shopItemData;

    private ShopManager shopManager;
    private MerchantRoomManager merchantRoomManager;
    private PlayerControlls playerControls;
    private PlayerBlockSystem cachedBlockSystem;

    [Header("SFX Configuration")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip purchaseSound;

    [Header("Grace Period Settings")]
    private float dialogueGracePeriod = 1.0f;
    private float dialogueEndTime = -1f;

    [Header("VFX de Compra")]
    [SerializeField] private GameObject purchaseVfxPrefab;

    private static GameObject defaultPurchaseVfx;

    private bool isPlayerInProximity = false;

    private void Awake()
    {
        shopManager = FindAnyObjectByType<ShopManager>();
        merchantRoomManager = GetComponentInParent<MerchantRoomManager>();

        if (merchantRoomManager != null)
        {
            audioSource = merchantRoomManager.GetComponentInChildren<AudioSource>();
        }
        else
        {
            Debug.LogWarning("AudioSource no asignado en ShopItemDisplay. Se intentara obtenerlo del MerchantRoomManager.");
        }

        if (shopManager == null)
        {
            Debug.LogError("ShopManager no encontrado. El item de la tienda no funcionara correctamente.");
        }

        if (shopItemData.ShopItemPrefab != null)
        {
            MeshRenderer m = GetComponent<MeshRenderer>();
            m.enabled = false;

            GameObject itemInstance = Instantiate(shopItemData.ShopItemPrefab, transform);
            itemInstance.transform.localPosition = Vector3.zero;
            shopItemData.ApplyOutlineMaterial(itemInstance);
        }

        playerControls = new PlayerControlls();
        playerControls.Interactions.SetCallbacks(this);
    }

    private void OnEnable()
    {
        playerControls?.Interactions.Enable();

        DialogManager.OnAnyDialogEnded += RecordDialogueEndTime;
    }

    private void OnDisable()
    {
        playerControls?.Interactions.Disable();

        DialogManager.OnAnyDialogEnded -= RecordDialogueEndTime;

        if (shopManager != null && isPlayerInProximity)
        {
            shopManager.SetInteractionPromptActive(false);
            shopManager.LockAndDisplayItemDetails(null);
            shopManager.ResetCostBar();
        }

        if (cachedBlockSystem != null)
        {
            cachedBlockSystem.SetBlockingEnabled(true);
            cachedBlockSystem = null;
        }
    }

    private void Update()
    {
        if (SteamInputManager.Instance == null) return;

        if (SteamInputManager.Instance.GetInteractPressed())
        {
            VerifityShop(); 
        }
    }

    private void OnDestroy()
    {
        playerControls?.Dispose();
    }

    private void RecordDialogueEndTime()
    {
        dialogueEndTime = Time.unscaledTime;
        Debug.Log($"[ShopItemDisplay] Fin de diálogo registrado por Evento Seguro en unscaledTime: {dialogueEndTime}");
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        VerifityShop();
    }

    private bool VerifityShop()
    {
        if (PauseController.Instance != null && PauseController.IsGamePaused) return false;
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return false;

        if (DialogManager.Instance != null && DialogManager.Instance.IsActive)
        {
            return false;
        }

        if (dialogueEndTime > 0 && (Time.unscaledTime - dialogueEndTime) < dialogueGracePeriod)
        {
            Debug.Log($"[ShopItemDisplay] Compra cancelada por seguridad. Evitando compra accidental por spam.");
            return false;
        }

        if (isPlayerInProximity && shopManager != null)
        {
            AttemptPurchase();
        }

        return true;
    }

    public void OnAdvanceDialogue(InputAction.CallbackContext context)
    {
    }

    public void DisplayFixedInfo()
    {
        if (shopManager == null) return;
        shopManager.LockAndDisplayItemDetails(shopItemData);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (shopManager != null && shopManager.InteractionMode != ShopInteractionMode.TriggerProximity)
        {
            return;
        }

        if (PauseController.Instance != null && PauseController.IsGamePaused) return;
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;

        if (other.CompareTag("Player"))
        {
            isPlayerInProximity = true;

            cachedBlockSystem = other.GetComponent<PlayerBlockSystem>();
            if (cachedBlockSystem != null)
            {
                cachedBlockSystem.SetBlockingEnabled(false);
            }

            if (shopManager != null)
            {
                shopManager.SetInteractionPromptActive(true);
                shopManager.LockAndDisplayItemDetails(shopItemData);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (shopManager != null && shopManager.InteractionMode != ShopInteractionMode.TriggerProximity)
        {
            return;
        }

        if (PauseController.Instance != null && PauseController.IsGamePaused) return;
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;

        if (!other.CompareTag("Player")) return;

        if (shopManager != null)
        {
            shopManager.SetInteractionPromptActive(true);

            float finalCost = shopManager.CalculateFinalCost(shopItemData.cost);
            shopManager.UpdateCostBar(finalCost);
            shopManager.LockAndDisplayItemDetails(shopItemData);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (shopManager != null && shopManager.InteractionMode != ShopInteractionMode.TriggerProximity)
            return;

        if (other.CompareTag("Player"))
        {
            isPlayerInProximity = false;

            if (cachedBlockSystem != null)
            {
                cachedBlockSystem.SetBlockingEnabled(true);
                cachedBlockSystem = null;
            }

            if (shopManager != null)
            {
                shopManager.SetInteractionPromptActive(false);
                shopManager.ResetCostBar();
                shopManager.LockAndDisplayItemDetails(null);
            }
        }
    }

    private void AttemptPurchase()
    {
        Debug.Log($"[ShopItemDisplay] Interacción detectada para: {shopItemData.itemName}.");
        if (!shopManager.CanAttemptPurchase()) return;

        bool purchaseSuccessful = shopManager.PurchaseItem(shopItemData);

        if (purchaseSuccessful)
        {
            OnPurchaseCompleted();
        }
        else if (shopManager.HasPendingReplacement)
        {
            isPlayerInProximity = false;
            shopManager.RegisterPendingPurchaseCallback(OnPurchaseCompleted);
            shopManager.RegisterPendingCancelCallback(OnPurchaseCancelled);
        }
    }

    private void OnPurchaseCancelled()
    {
        isPlayerInProximity = true;
    }

    private void OnPurchaseCompleted()
    {
        shopManager.ResetCostBar();

        if (merchantRoomManager != null)
        {
            merchantRoomManager.OnItemPurchased();
            if (audioSource != null && purchaseSound != null)
            {
                audioSource.PlayOneShot(purchaseSound);
            }
        }

        if (cachedBlockSystem != null)
        {
            cachedBlockSystem.SetBlockingEnabled(true);
            cachedBlockSystem = null;
        }

        GameObject vfxToSpawn = purchaseVfxPrefab != null ? purchaseVfxPrefab : GetDefaultPurchaseVfx();
        if (vfxToSpawn != null)
        {
            Instantiate(vfxToSpawn, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    private static GameObject GetDefaultPurchaseVfx()
    {
        if (defaultPurchaseVfx == null)
        {
            defaultPurchaseVfx = Resources.Load<GameObject>("VFX/VFX-Buy");

            if (defaultPurchaseVfx == null)
            {
                Debug.LogWarning("[ShopItemDisplay] No se encontró VFX-Buy en Resources/VFX/. Verifica la ruta o asigna purchaseVfxPrefab manualmente.");
            }
        }

        return defaultPurchaseVfx;
    }
}