using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MerchantDialogHandler : MonoBehaviour
{
    #region Dependencies

    [Header("Dependencies")]
    [SerializeField] private DialogManager dialogManager;
    [SerializeField] private MerchantRoomManager merchantManager;
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button converseButton;

    #endregion

    #region Dialog Lines

    [Header("Dialog Lines")]
    [SerializeField] private DialogLine[] firstVisitLines;
    [SerializeField] private DialogLine[] routineLines;
    [SerializeField] private DialogLine[] pactOfferLines;
    [SerializeField] private DialogLine[] tooHealthyLines;
    [SerializeField] private DialogLine[] friendlyLines;
    [SerializeField] private DialogLine[] hostileLines;
    [SerializeField] private DialogLine[] blockedLine;
    [SerializeField] private DialogLine[] pactAcceptLines;
    [SerializeField] private DialogLine[] pactShowDetailsLines;
    [SerializeField] private DialogLine[] maxReputationRewardLines;

    #endregion

    #region Pact Voice Settings

    [Header("Pact Voice Settings")]
    [SerializeField] private AudioClip pactVoiceClip;
    [Tooltip("Pitch del audio del pacto")]
    [UnityEngine.Range(0.8f, 1.2f)]
    [SerializeField] private float pactVoicePitch = 1f;
    [UnityEngine.Range(1, 5)]
    [SerializeField] private int pactVoiceFrequency = 2;

    #endregion

    #region Events & State

    [Header("Events")]
    [SerializeField] private UnityEvent OnDialogCompleteEvent;

    private static int globalReputation = 0;
    private static bool hasReceivedMaxReward = false;

    private bool isPactAvailable = false;
    private const float PactHealthThreshold = 0.70f;

    private int conversationCount = 0;

    private bool isPactBlocked = false;
    private bool waitingForPactButtons = false;
    private MerchantMood currentMood = MerchantMood.Neutral;

    private enum MerchantMood { Neutral, Friendly, Hostile }

    private const int ReputationRewardThreshold = 8;

    #endregion

    #region Unity Methods

    private void Start()
    {
        if (dialogManager == null) dialogManager = DialogManager.Instance;
        if (merchantManager == null) merchantManager = FindAnyObjectByType<MerchantRoomManager>();
        if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
        if (inventoryManager == null) inventoryManager = FindAnyObjectByType<InventoryManager>();

        if (optionsPanel != null) optionsPanel.SetActive(false);

        acceptButton?.onClick.AddListener(OnAcceptPactOption);
        backButton?.onClick.AddListener(OnBackButton);
        converseButton?.onClick.AddListener(OnConverseOption);

        if (OnDialogCompleteEvent == null)
        {
            OnDialogCompleteEvent = new UnityEvent();
        }
        OnDialogCompleteEvent.AddListener(OnDialogComplete);

        UpdateMerchantMood();
        UpdateShopPrices();
    }

    #endregion

    #region Core Interaction Flow

    public void StartMerchantInteraction()
    {
        if (dialogManager == null || merchantManager == null || playerHealth == null || dialogManager.IsActive) return;

        waitingForPactButtons = false;

        isPactAvailable = (playerHealth.CurrentHealth / playerHealth.MaxHealth) < PactHealthThreshold;

        DialogLine[] entryLines;
        bool isMerchantFlow = true;

        if (isPactBlocked)
        {
            entryLines = blockedLine;
        }
        else if (merchantManager.isFirstVisit)
        {
            entryLines = firstVisitLines;
        }
        else if (!isPactAvailable)
        {
            entryLines = tooHealthyLines;
        }
        else
        {
            Pact pactOffer = merchantManager.GenerateRandomPact();
            merchantManager.SetCurrentPactOffer(pactOffer);
            entryLines = pactOfferLines;
        }

        UnityEvent showOptionsEvent = new UnityEvent();
        showOptionsEvent.AddListener(OnEntryDialogFinished);
        dialogManager.StartDialog(entryLines, showOptionsEvent, isMerchantFlow);
    }

    private void OnEntryDialogFinished()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
        }

        bool canOfferPact = isPactAvailable && !merchantManager.hasTakenPact && !isPactBlocked;

        acceptButton?.gameObject.SetActive(canOfferPact);
        backButton?.gameObject.SetActive(true);

        if (converseButton != null)
        {
            converseButton.gameObject.SetActive(conversationCount < 5);
        }
    }

    #endregion

    #region Button Handlers

    public void OnAcceptPactOption()
    {
        Debug.Log("OnAcceptPactOption INICIO");
        optionsPanel?.SetActive(false);

        Pact currentPact = merchantManager.GetCurrentPactOffer();
        if (currentPact == null)
        {
            Debug.LogError("currentPact es NULL!");
            return;
        }

        merchantManager.OnAcceptPact();

        ModifyReputation(1);

        Debug.Log($"Reputación actual: {globalReputation}");

        string pactDetails = FormatPactDetails(currentPact);
        Debug.Log("Detalles del pacto: " + pactDetails);

        string characterName = pactAcceptLines.Length > 0 ? pactAcceptLines[0].CharacterName : "Mercader";
        Sprite profileSprite = pactAcceptLines.Length > 0 ? pactAcceptLines[0].ProfileImage : null;

        waitingForPactButtons = true;

        dialogManager.UpdateCurrentDialogText(
            pactDetails,
            characterName,
            profileSprite,
            pactVoiceClip,
            pactVoicePitch,
            pactVoiceFrequency
        );

        Debug.Log("Esperando que termine de tipear para mostrar botón");
    }

    public void OnConverseOption()
    {
        if (converseButton == null) return;
        optionsPanel?.SetActive(false);
        conversationCount++;

        int repChange = 0;

        if (conversationCount <= 3)
        {
            repChange = 2;
            Debug.Log($"Conversación {conversationCount}: +2 reputación");
        }
        else if (conversationCount == 4)
        {
            repChange = -2;
            Debug.Log($"Conversación {conversationCount}: -2 reputación");
        }
        else if (conversationCount >= 5)
        {
            repChange = -4;
            isPactBlocked = true;
            Debug.Log($"Conversación {conversationCount}: -4 reputación. PACTOS BLOQUEADOS");
        }

        bool rewardJustGiven = ModifyReputation(repChange);

        DialogLine[] linesToUse;

        if (rewardJustGiven && maxReputationRewardLines != null && maxReputationRewardLines.Length > 0)
        {
            linesToUse = maxReputationRewardLines;
            Debug.Log("Mostrando diálogo de Recompensa Especial.");
        }
        else if (currentMood == MerchantMood.Hostile)
        {
            linesToUse = hostileLines;
        }
        else if (currentMood == MerchantMood.Friendly)
        {
            linesToUse = friendlyLines;
        }
        else
        {
            linesToUse = routineLines;
        }

        if (linesToUse != null && linesToUse.Length > 0)
        {
            DialogLine lineToShow = linesToUse[Random.Range(0, linesToUse.Length)];

            dialogManager.UpdateCurrentDialogText(
                lineToShow.Text,
                lineToShow.CharacterName,
                lineToShow.ProfileImage,
                lineToShow.VoiceClip,
                lineToShow.VoicePitch,
                lineToShow.VoiceFrequency
            );
        }

        waitingForPactButtons = false;

        Debug.Log("Conversación iniciada, esperando tipeo");
    }

    private void OnDialogComplete()
    {
        if (waitingForPactButtons)
        {
            waitingForPactButtons = false;
            ShowBackButtonOnly();
        }
        else
        {
            ShowConversationButtons();
        }
    }

    private void ShowConversationButtons()
    {
        Debug.Log("ShowConversationButtons - Mostrando botones de conversación");
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
            acceptButton?.gameObject.SetActive(false);

            if (converseButton != null)
            {
                converseButton.gameObject.SetActive(conversationCount < 5);
            }

            backButton?.gameObject.SetActive(true);
        }
    }

    public void OnBackButton()
    {
        optionsPanel?.SetActive(false);
        waitingForPactButtons = false;
        merchantManager?.ClearCurrentPact();
        dialogManager.ForceEndDialog();
    }

    public void ResetMerchantState()
    {
        conversationCount = 0;
        isPactBlocked = false;

        UpdateMerchantMood();
        UpdateShopPrices();

        if (merchantManager != null)
        {
            merchantManager.SetPriceModifier(CalculatePriceModifier());
        }
    }

    private void ShowBackButtonOnly()
    {
        Debug.Log("ShowBackButtonOnly - Mostrando botón Salir");
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
            acceptButton?.gameObject.SetActive(false);

            if (converseButton != null)
            {
                converseButton.gameObject.SetActive(false);
            }

            backButton?.gameObject.SetActive(true);
        }
    }

    public void ChangeRoomMerchant(MerchantRoomManager refe) => merchantManager = refe;

    private bool ModifyReputation(int amount)
    {
        globalReputation += amount;
        bool rewardTriggered = false;

        if (amount > 0 && globalReputation >= ReputationRewardThreshold && !hasReceivedMaxReward)
        {
            rewardTriggered = GiveReputationReward();
        }

        UpdateMerchantMood();
        UpdateShopPrices();
        Debug.Log($"Reputación Global actual: {globalReputation}");

        return rewardTriggered;
    }

    private bool GiveReputationReward()
    {
        if (ShopManager.Instance == null || inventoryManager == null) return false;

        ShopItem rewardItem = ShopManager.Instance.GetRandomRewardItem();

        if (rewardItem != null)
        {
            if (inventoryManager.TryAddItem(rewardItem))
            {
                hasReceivedMaxReward = true;

                Debug.Log($"<color=yellow>¡RECOMPENSA DE LEALTAD! Recibido: {rewardItem.itemName}</color>");

                inventoryManager.ShowWarningMessage($"¡Regalo de Lealtad: {rewardItem.itemName}!");

                return true;
            }
        }
        return false;
    }

    public static void ResetReputationState()
    {
        globalReputation = 0;
        hasReceivedMaxReward = false;
        Debug.Log("Estado del mercader reiniciado (Reputación 0).");
    }

    private void UpdateMerchantMood()
    {
        if (globalReputation >= 4)
        {
            currentMood = MerchantMood.Friendly;
        }
        else if (globalReputation <= -2)
        {
            currentMood = MerchantMood.Hostile;
        }
        else
        {
            currentMood = MerchantMood.Neutral;
        }

        Debug.Log($"Humor del mercader: {currentMood}");
    }

    private void UpdateShopPrices()
    {
        if (merchantManager != null)
        {
            float priceModifier = CalculatePriceModifier();
            merchantManager.SetPriceModifier(priceModifier);
            Debug.Log($"Modificador de precios: {priceModifier:F2}");
        }
    }

    private float CalculatePriceModifier()
    {
        if (globalReputation >= ReputationRewardThreshold) return 0.6f;
        else if (globalReputation >= 6) return 0.7f;
        else if (globalReputation >= 4) return 0.85f;
        else if (globalReputation >= 2) return 0.95f;
        else if (globalReputation <= -6) return 1.5f;
        else if (globalReputation <= -4) return 1.3f;
        else if (globalReputation <= -2) return 1.15f;
        else return 1.0f;
    }

    #endregion

    #region Helpers

    private string FormatPactDetails(Pact pact)
    {
        string details = $"PACTO: {pact.pactName}\n";

        if (pact.lifeRecoveryAmount > 0)
        {
            details += $"BENEFICIO: Restaura {pact.lifeRecoveryAmount} PV\n";
        }
        else
        {
            details += "BENEFICIO: Ninguno\n";
        }

        details += "MALDICIÓN REVELADA:";

        if (pact.removeRandomRelic)
        {
            details += "Pierdes una Reliquia al azar\n";
        }

        foreach (var drawback in pact.drawbacks)
        {
            float displayAmount = drawback.isPercentage ? drawback.amount : drawback.amount;
            string amountText = drawback.isPercentage ? $"{displayAmount:F0}%" : $"{displayAmount}";
            details += $" {drawback.type} -{amountText}\n";
        }

        return details;
    }

    #endregion
}