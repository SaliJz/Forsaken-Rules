using UnityEngine;

public class MerchantInteractionTrigger : MonoBehaviour
{
    #region Editor Fields

    [Header("UI Prompt")]
    public GameObject pactInteractionPromptPrefab;

    #endregion

    #region Private Fields

    private GameObject currentInteractionPrompt;
    private MerchantRoomManager manager;
    private Sala6MerchantFlow flowManager;

    #endregion

    #region Unity Methods

    private void Start()
    {
        manager = FindAnyObjectByType<MerchantRoomManager>();
        flowManager = FindAnyObjectByType<Sala6MerchantFlow>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (manager != null && other.CompareTag("Player"))
        {
            manager.OnPlayerEnterMerchantTrigger(this);

            if (flowManager != null && flowManager.sequenceActive)
            {
                flowManager.HandleTriggerEnter();
            }
            else if (DialogManager.Instance == null || !DialogManager.Instance.IsActive)
            {
                ShowPactPrompt();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (manager != null && other.CompareTag("Player"))
        {
            manager.OnPlayerExitMerchantTrigger();
            HidePactPrompt();
        }
    }

    #endregion

    #region Prompt Control

    public void ShowPactPrompt()
    {
        bool dialogIsNotActive = DialogManager.Instance == null || !DialogManager.Instance.IsActive;

        if (manager != null && dialogIsNotActive && pactInteractionPromptPrefab != null && currentInteractionPrompt == null)
        {
            HUDManager.Instance.SetInteractionPrompt(true, "Interact", "HABLAR");
        }
    }

    public void HidePactPrompt()
    {
        HUDManager.Instance.SetInteractionPrompt(false, "Interact", "HABLAR");

        if (currentInteractionPrompt != null)
        {
            Destroy(currentInteractionPrompt);
            currentInteractionPrompt = null;
        }
    }

    #endregion
}