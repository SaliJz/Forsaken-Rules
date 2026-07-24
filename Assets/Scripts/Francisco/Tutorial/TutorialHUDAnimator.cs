using System.Collections;
using UnityEngine;
using TMPro;

public class TutorialHUDAnimator : MonoBehaviour
{
    #region REFERENCES

    [Header("UI References")]
    [SerializeField] private RectTransform maskRect;
    [SerializeField] private GameObject checkIcon;
    [SerializeField] private GameObject checkmarkIcon;
    [SerializeField] private TextMeshProUGUI instructionText;

    #endregion

    #region CONFIGURATION

    [Header("Animation Configuration")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private float fullWidth = 400f;
    [SerializeField] private float checkDisplayTime = 0.5f;

    #endregion

    #region PRIVATE_FIELDS

    private float initialMaskX;
    private Coroutine currentAnimationCoroutine;
    private string currentInstructionText = "";
    private string pendingInstructionText = null;
    private bool isAnimatingOut = false;
    private bool instructionActive = false;

    #endregion

    #region LIFECYCLE

    private void Awake()
    {
        initialMaskX = maskRect.anchoredPosition.x;
        SetMaskWidth(0);
        if (checkIcon != null) checkIcon.SetActive(true);
        if (checkmarkIcon != null) checkmarkIcon.SetActive(false);
        gameObject.SetActive(false);
        if (instructionText != null) currentInstructionText = instructionText.text;
        instructionActive = false;
    }

    private void Update()
    {
        if (gameObject.activeSelf && !string.IsNullOrEmpty(currentInstructionText) && InputIconManager.Instance != null)
        {
            string updatedText = ProcessTextWithInputIcons(currentInstructionText);
            if (instructionText != null && instructionText.text != updatedText)
            {
                instructionText.text = updatedText;
            }
        }
    }

    #endregion

    #region PUBLIC_METHODS

    public void SetAndShowInstruction(string newText)
    {
        if (isAnimatingOut)
        {
            pendingInstructionText = newText;
            return;
        }

        if (newText.Equals(currentInstructionText)) return;

        if (instructionActive)
        {
            pendingInstructionText = newText;

            if (currentAnimationCoroutine != null) StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = StartCoroutine(AnimateOutForNewInstructionCoroutine());
        }
        else
        {
            SetInstructionText(newText);
            ShowInstruction();
        }
    }

    public void ShowInstruction()
    {
        if (gameObject.activeSelf && !isAnimatingOut) return;

        if (isAnimatingOut)
        {
            pendingInstructionText = currentInstructionText;
            return;
        }

        gameObject.SetActive(true);
        if (currentAnimationCoroutine != null) StopCoroutine(currentAnimationCoroutine);
        currentAnimationCoroutine = StartCoroutine(AnimateInCoroutine());
        instructionActive = true;
    }

    public void CompleteInstruction()
    {
        if (!gameObject.activeSelf) return;
        if (isAnimatingOut) return;

        instructionActive = false;
        currentInstructionText = "";

        if (currentAnimationCoroutine != null) StopCoroutine(currentAnimationCoroutine);
        currentAnimationCoroutine = StartCoroutine(AnimateCheckAndOutCoroutine());
    }

    public bool SetInstructionText(string newText)
    {
        if (newText.Equals(currentInstructionText))
        {
            Debug.Log($"Ignorando SetInstructionText: El texto '{newText}' ya está activo.");
            return false;
        }

        currentInstructionText = newText;
        if (instructionText != null)
        {
            instructionText.text = ProcessTextWithInputIcons(newText);
        }

        return true;
    }

    #endregion

    #region TEXT_PROCESSING

    private string ProcessTextWithInputIcons(string text)
    {
        if (InputIconManager.Instance == null) return text;

        string processedText = text;
        int startIndex = 0;

        while (startIndex < processedText.Length)
        {
            int exclamationIndex = processedText.IndexOf('!', startIndex);
            if (exclamationIndex == -1) break;

            int spaceIndex = processedText.IndexOf(' ', exclamationIndex);
            int endIndex = spaceIndex == -1 ? processedText.Length : spaceIndex;

            string actionName = processedText.Substring(exclamationIndex + 1, endIndex - exclamationIndex - 1);

            if (!string.IsNullOrEmpty(actionName))
            {
                string iconSprite = InputIconManager.Instance.GetPromptForAction(actionName);
                processedText = processedText.Substring(0, exclamationIndex) + iconSprite + processedText.Substring(endIndex);
                startIndex = exclamationIndex + iconSprite.Length;
            }
            else
            {
                startIndex = exclamationIndex + 1;
            }
        }

        return processedText;
    }

    #endregion

    #region ANIMATION_LOGIC

    private void SetMaskWidth(float width)
    {
        maskRect.sizeDelta = new Vector2(width, maskRect.sizeDelta.y);
    }

    private IEnumerator AnimateInCoroutine()
    {
        SetMaskWidth(0);
        maskRect.anchoredPosition = new Vector2(initialMaskX, maskRect.anchoredPosition.y);
        if (checkmarkIcon != null) checkmarkIcon.SetActive(false);
        if (checkIcon != null) checkIcon.SetActive(true);
        isAnimatingOut = false;

        float startTime = Time.time;
        float endWidth = fullWidth;

        while (Time.time < startTime + animationDuration)
        {
            float t = (Time.time - startTime) / animationDuration;
            float currentWidth = Mathf.Lerp(0, endWidth, t);
            SetMaskWidth(currentWidth);
            yield return null;
        }

        SetMaskWidth(endWidth);
        currentAnimationCoroutine = null;
    }

    private IEnumerator AnimateOutForNewInstructionCoroutine()
    {
        isAnimatingOut = true;
        if (checkmarkIcon != null) checkmarkIcon.SetActive(false);
        if (checkIcon != null) checkIcon.SetActive(true);

        float startTime = Time.time;
        float startX = maskRect.anchoredPosition.x;
        float endX = startX - fullWidth;

        while (Time.time < startTime + animationDuration)
        {
            float t = (Time.time - startTime) / animationDuration;
            float currentX = Mathf.Lerp(startX, endX, t);
            maskRect.anchoredPosition = new Vector2(currentX, maskRect.anchoredPosition.y);
            yield return null;
        }

        HandlePendingInstruction();
    }

    private IEnumerator AnimateCheckAndOutCoroutine()
    {
        isAnimatingOut = true;
        if (checkmarkIcon != null) checkmarkIcon.SetActive(true);
        if (checkIcon != null) checkIcon.SetActive(false);

        yield return new WaitForSeconds(checkDisplayTime);

        float startTime = Time.time;
        float startX = maskRect.anchoredPosition.x;
        float endX = startX - fullWidth;

        while (Time.time < startTime + animationDuration)
        {
            float t = (Time.time - startTime) / animationDuration;
            float currentX = Mathf.Lerp(startX, endX, t);
            maskRect.anchoredPosition = new Vector2(currentX, maskRect.anchoredPosition.y);
            yield return null;
        }

        HandlePendingInstruction();
    }

    private void HandlePendingInstruction()
    {
        isAnimatingOut = false;
        currentAnimationCoroutine = null;

        if (!string.IsNullOrEmpty(pendingInstructionText))
        {
            string newInstruction = pendingInstructionText;
            pendingInstructionText = null;

            gameObject.SetActive(true);
            SetInstructionText(newInstruction);
            currentAnimationCoroutine = StartCoroutine(AnimateInCoroutine());
            instructionActive = true;
        }
        else
        {
            gameObject.SetActive(false);
            maskRect.anchoredPosition = new Vector2(initialMaskX, maskRect.anchoredPosition.y);
            SetMaskWidth(0);
            if (checkmarkIcon != null) checkmarkIcon.SetActive(false);
            if (checkIcon != null) checkIcon.SetActive(true);
            instructionActive = false;
        }
    }

    #endregion
}