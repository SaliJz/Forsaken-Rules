using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class CinematicSkipController : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private GameObject buttonContainer;
    [SerializeField] private Image fillImage;
    [SerializeField] private Button skipButton;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float idleTimeout = 3f;
    [SerializeField] private float holdDuration = 1.5f;

    [Header("Events")]
    [SerializeField] private UnityEvent onSkipComplete;

    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    private float idleTimer;
    private float holdTimer;
    private bool isButtonVisible = false;
    private bool isHolding = false;
    private bool cinematicSkipped = false;

    private Vector2 lastMousePosition;
    private bool isReadyToDetect = false;

    private void Awake()
    {
        canvasGroup = buttonContainer.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = buttonContainer.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        if (fillImage != null) fillImage.fillAmount = 0f;

        if (skipButton != null) skipButton.interactable = false;
    }

    private void Start()
    {
        if (Mouse.current != null)
        {
            lastMousePosition = Mouse.current.position.ReadValue();
        }
        Invoke(nameof(EnableDetection), 0.1f);
    }

    private void EnableDetection()
    {
        if (Mouse.current != null)
        {
            lastMousePosition = Mouse.current.position.ReadValue();
        }
        isReadyToDetect = true;
    }

    private void Update()
    {
        if (cinematicSkipped || !isReadyToDetect) return;

        CheckForActivity();

        if (isButtonVisible && !isHolding)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= idleTimeout)
            {
                ShowButton(false);
            }
        }

        CheckHoldInput();
    }

    private void CheckForActivity()
    {
        bool active = false;

        if (GamepadPointer.Instance != null)
        {
            if (GamepadPointer.Instance.IsGamepadMode())
            {
                if (GamepadPointer.Instance.IsSteamActive)
                {
                    if (SteamInputManager.Instance.GetMoveAxis().magnitude > 0.2f ||
                        SteamInputManager.Instance.GetAimAxis().magnitude > 0.2f)
                    {
                        active = true;
                    }
                }
                else if (Gamepad.current != null)
                {
                    if (Gamepad.current.leftStick.ReadValue().magnitude > 0.2f ||
                        Gamepad.current.rightStick.ReadValue().magnitude > 0.2f)
                    {
                        active = true;
                    }
                }
            }
        }

        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) active = true;

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame) active = true;

            Vector2 currentMousePos = Mouse.current.position.ReadValue();

            if ((currentMousePos - lastMousePosition).sqrMagnitude > 2f) active = true;
            lastMousePosition = currentMousePos;
        }

        if (active)
        {
            idleTimer = 0f;
            if (!isButtonVisible) ShowButton(true);
        }
    }

    private bool IsPointerOverSkipButton()
    {
        if (skipButton == null || Mouse.current == null) return false;

        RectTransform buttonRect = skipButton.GetComponent<RectTransform>();
        Vector2 pointerPos = Mouse.current.position.ReadValue();

        Canvas canvas = skipButton.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        return RectTransformUtility.RectangleContainsScreenPoint(buttonRect, pointerPos, cam);
    }

    private void CheckHoldInput()
    {
        bool isPressingSkipButton = false;

        if (GamepadPointer.Instance != null && GamepadPointer.Instance.IsGamepadMode())
        {
            if (GamepadPointer.Instance.IsSteamActive)
            {
                if (SteamInputManager.Instance.GetAdvanceDialoguePressed()) isPressingSkipButton = true;
            }
            else if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonSouth.isPressed) isPressingSkipButton = true;
            }
        }
        else
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) isPressingSkipButton = true;
            if (Mouse.current != null && Mouse.current.leftButton.isPressed && IsPointerOverSkipButton()) isPressingSkipButton = true;
        }

        if (isPressingSkipButton)
        {
            if (!isHolding)
            {
                isHolding = true;
                idleTimer = 0f;
                if (!isButtonVisible) ShowButton(true);
            }

            holdTimer += Time.deltaTime;

            if (fillImage != null)
            {
                fillImage.fillAmount = Mathf.Clamp01(holdTimer / holdDuration);
            }

            if (holdTimer >= holdDuration)
            {
                ExecuteSkip();
            }
        }
        else
        {
            if (isHolding)
            {
                isHolding = false;
                holdTimer = 0f;
                if (fillImage != null) fillImage.fillAmount = 0f;
            }
        }
    }

    private void ShowButton(bool show)
    {
        isButtonVisible = show;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeCanvas(show ? 1f : 0f));
    }

    private System.Collections.IEnumerator FadeCanvas(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0;
        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
    }

    private void ExecuteSkip()
    {
        cinematicSkipped = true;
        isHolding = false;
        ShowButton(false);

        if (skipButton != null)
        {
            skipButton.onClick.Invoke();
        }

        if (onSkipComplete != null)
        {
            onSkipComplete.Invoke();
        }
    }
}