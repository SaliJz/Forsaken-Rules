using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SaveGamePanel : MonoBehaviour
{
    public enum PanelDisplayType { AnimatedScale, CanvasFade, Static }

    [Header("Panel Display Settings")]
    [SerializeField] private PanelDisplayType displayType = PanelDisplayType.AnimatedScale;

    [Header("Canvas Group Reference")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Main Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button tutorialButton;
    [SerializeField] private TextMeshProUGUI playButtonText;

    [Header("DOTween Settings")]
    [SerializeField] private float openCloseDuration = 0.3f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    [Header("Scale Animation Vectors")]
    [SerializeField] private Vector3 startScale = Vector3.zero;
    [SerializeField] private Vector3 endScale = Vector3.one;

    [Header("Focus Control")]
    [SerializeField] private GameObject firstSelectedButton;

    private bool isPanelOpen = false;
    private const int DEFAULT_SLOT = 1;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.localScale = startScale;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        SetupButtons();
    }

    private void LateUpdate()
    {
        if (!isPanelOpen || canvasGroup == null || !canvasGroup.interactable || EventSystem.current == null)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.transform.IsChildOf(canvasGroup.transform))
            return;

        GameObject buttonToFocus = GetButtonToFocus();
        if (buttonToFocus != null && buttonToFocus.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(buttonToFocus);
    }

    private GameObject GetButtonToFocus()
    {
        if (firstSelectedButton != null && firstSelectedButton.activeInHierarchy)
            return firstSelectedButton;

        if (playButton != null && playButton.gameObject.activeInHierarchy)
            return playButton.gameObject;

        if (tutorialButton != null && tutorialButton.gameObject.activeInHierarchy)
            return tutorialButton.gameObject;

        return null;
    }

    private void SetupButtons()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayButtonClicked);
        }

        if (tutorialButton != null)
        {
            tutorialButton.onClick.RemoveAllListeners();
            tutorialButton.onClick.AddListener(OnTutorialButtonClicked);
        }

        bool hasCompletedTutorial = false;

        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.DoesSlotExist(DEFAULT_SLOT))
        {
            SaveData data = SaveLoadManager.Instance.LoadGame(DEFAULT_SLOT);
            hasCompletedTutorial = data.hasPassedTutorial;
        }

        if (hasCompletedTutorial)
        {
            if (playButtonText != null)
            {
                playButtonText.text = "Jugar";
            }

            if (tutorialButton != null)
            {
                tutorialButton.gameObject.SetActive(true);
            }
        }
        else
        {
            if (playButtonText != null)
            {
                playButtonText.text = "Jugar";
            }

            if (tutorialButton != null)
            {
                tutorialButton.gameObject.SetActive(false);
            }
        }
    }

    private void OnPlayButtonClicked()
    {
        if (SaveLoadManager.Instance != null)
        {
            FadeOutAndLoad(DEFAULT_SLOT, false);
        }
    }

    private void OnTutorialButtonClicked()
    {
        if (SaveLoadManager.Instance != null)
        {
            FadeOutAndLoad(DEFAULT_SLOT, true);
        }
    }

    private void FadeOutAndLoad(int slotIndex, bool forceTutorial)
    {
        if (!isPanelOpen) return;
        isPanelOpen = false;

        if (EventSystem.current.currentSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.DOKill();
        }

        if (SaveLoadManager.Instance != null)
        {
            if (forceTutorial)
            {
                SaveLoadManager.Instance.StartTutorial(slotIndex);
            }
            else
            {
                SaveLoadManager.Instance.StartGameFromSlot(slotIndex);
            }
        }
    }

    public void ShowPanel()
    {
        if (isPanelOpen) return;
        isPanelOpen = true;

        SetupButtons();

        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.DOScale(endScale, openCloseDuration).SetEase(openEase).SetUpdate(true);
            canvasGroup.DOFade(1f, openCloseDuration).SetUpdate(true);
        }
        else if (displayType == PanelDisplayType.CanvasFade)
        {
            canvasGroup.DOFade(1f, openCloseDuration).SetUpdate(true);
        }
        else
        {
            canvasGroup.alpha = 1f;
        }

        GameObject buttonToFocus = GetButtonToFocus();

        if (buttonToFocus != null && buttonToFocus.activeInHierarchy && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(buttonToFocus);
            StartCoroutine(FocusButtonAfterOpening(buttonToFocus));
        }
    }

    private IEnumerator FocusButtonAfterOpening(GameObject buttonToFocus)
    {
        yield return new WaitForEndOfFrame();

        if (!isPanelOpen || buttonToFocus == null || !buttonToFocus.activeInHierarchy || EventSystem.current == null)
            yield break;

        EventSystem.current.SetSelectedGameObject(buttonToFocus);
    }

    public void HidePanel()
    {
        if (!isPanelOpen) return;
        isPanelOpen = false;

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (EventSystem.current.currentSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.DOScale(startScale, openCloseDuration).SetEase(closeEase).SetUpdate(true);
            canvasGroup.DOFade(0f, openCloseDuration).SetUpdate(true);
        }
        else if (displayType == PanelDisplayType.CanvasFade)
        {
            canvasGroup.DOFade(0f, openCloseDuration).SetUpdate(true);
        }
        else
        {
            canvasGroup.alpha = 0f;
        }
    }
}
