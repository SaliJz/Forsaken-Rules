using UnityEngine;
using System.Collections;
using UnityEngine.Events;

public class Sala6MerchantFlow : MonoBehaviour
{
    #region REFERENCES

    [Header("Referencias de Componentes")]
    private MerchantRoomManager manager;
    private PlayerHealth playerHealth;
    private ShopManager shopManager;
    private MerchantRoomInitializer roomInitializer;

    [Header("Eventos")]
    public UnityEvent onPortalDialogueComplete;

    #endregion

    #region DIALOGUES

    [Header("Diálogos de la Secuencia (Mälgor)")]
    [SerializeField] public DialogLine[] malgorApproachLines;
    [SerializeField] public DialogLine[] malgorPactAcceptLines;
    [SerializeField] public DialogLine[] malgorShopUnlockLines;
    [SerializeField] public DialogLine[] malgorNoPurchaseLines;
    [SerializeField] public DialogLine[] malgorPurchaseLines;

    [Header("Diálogos de la Secuencia (Diablo)")]
    [SerializeField] public DialogLine[] devilPurchaseLines;
    [SerializeField] public DialogLine[] devilPortalLines;

    #endregion

    #region STATE_AND_CONSTANTS

    public bool sequenceActive { get; private set; } = false;
    private bool shopUnlocked = false;
    private bool hasCompletedSequence = false;
    private bool hasPurchasedItem = false;
    private Coroutine shopWatchdogCoroutine;

    #endregion

    #region UNITY_METHODS

    private void Awake()
    {
        manager = FindAnyObjectByType<MerchantRoomManager>();
        playerHealth = FindAnyObjectByType<PlayerHealth>();

        shopManager = FindAnyObjectByType<ShopManager>();
        roomInitializer = FindAnyObjectByType<MerchantRoomInitializer>();

        hasCompletedSequence = false;
        shopUnlocked = false;
        hasPurchasedItem = false;
    }

    #endregion

    #region PUBLIC_HANDLERS

    public void HandleTriggerEnter()
    {
        StartCoroutine(SequenceFlow());
    }

    public void HandleItemPurchased()
    {
        if (shopWatchdogCoroutine != null)
        {
            StopCoroutine(shopWatchdogCoroutine);
            shopWatchdogCoroutine = null;
        }
        hasPurchasedItem = true;
        StartCoroutine(PurchaseDialogueFlow());
    }

    #endregion

    #region FLOW_LOGIC

    public void StartSequenceFlow()
    {
        StartCoroutine(SequenceFlow());
    }

    private IEnumerator SequenceFlow()
    {
        if (hasCompletedSequence || sequenceActive)
        {
            yield break;
        }

        sequenceActive = true;

        yield return StartCoroutine(PlayDialog(malgorApproachLines));

        yield return StartCoroutine(PlayDialog(malgorPactAcceptLines));

        if (playerHealth != null)
        {
            playerHealth.Heal(playerHealth.MaxHealth);
        }

        if (manager != null)
        {
            manager.hasTakenPact = true;
        }

        shopUnlocked = true;

        yield return StartCoroutine(PlayDialog(malgorShopUnlockLines));

        if (shopManager != null && roomInitializer != null && manager != null)
        {
            roomInitializer.InitializeMerchantRoom();
        }

        shopWatchdogCoroutine = StartCoroutine(WaitForShopPurchase());

        hasCompletedSequence = true;
        sequenceActive = false;
    }


    private IEnumerator PurchaseDialogueFlow()
    {
        yield return StartCoroutine(PlayDialog(malgorPurchaseLines));

        yield return StartCoroutine(PlayDialog(devilPurchaseLines));

        yield return StartCoroutine(PortalActivationFlow());
    }

    private IEnumerator WaitForShopPurchase()
    {
        yield return new WaitForSeconds(10f);

        if (shopUnlocked && !hasPurchasedItem)
        {
            yield return StartCoroutine(PlayDialog(malgorNoPurchaseLines));

            if (!hasPurchasedItem)
            {
                shopWatchdogCoroutine = StartCoroutine(WaitForShopPurchase());
            }
            else
            {
                shopWatchdogCoroutine = null;
            }
        }
        else
        {
            shopWatchdogCoroutine = null;
        }
    }

    private IEnumerator PortalActivationFlow()
    {
        yield return StartCoroutine(PlayDialog(devilPortalLines));

        onPortalDialogueComplete.Invoke();
    }

    #endregion

    #region HELPERS

    private IEnumerator PlayDialog(DialogLine[] lines, bool waitForAdvance = true)
    {
        if (DialogManager.Instance == null || lines == null || lines.Length == 0)
        {
            yield break;
        }

        DialogManager.Instance.StartDialog(lines);

        yield return null;

        if (waitForAdvance)
        {
            while (DialogManager.Instance.IsActive)
            {
                yield return null;
            }
        }
    }

    #endregion
}