using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GamepadPointer : MonoBehaviour
{
    [Header("Cursor in Pause Settings")]
    [SerializeField] private float pauseMovementRadiusFactor = 0.4f;
    [SerializeField] private float cursorSpeed = 1500f;
    [SerializeField] private RectTransform virtualCursor;

    private const float CursorFollowSpeed = 30f;
    private const float RightStickDeadZone = 0.2f;

    private float ignoreMouseInputUntilTime;
    private const float MouseWarpIgnoreDuration = 0.1f;

    private InputSystemUIInputModule uiInputModule;
    private RectTransform canvasRect;
    private Camera canvasCamera;

    private InputDevice currentActiveDevice = null;
    private Vector2 lastValidCursorPosition = Vector2.zero;

    private Gamepad currentGamepad;
    private bool isSteamGamepadActive = false;
    private bool wasGamepadMode = false;

    private GameObject lastSelectedObject = null;
    private SettingsPanel settingsPanel;

    private Vector2 lastWarpedScreenPoint = Vector2.zero;
    private bool hasWarpedOnce = false;

    public bool IsSteamActive => SteamManager.Initialized && SteamInputManager.Instance != null;
    public static GamepadPointer Instance { get; private set; }

    #region Ciclo De Vida
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        Cursor.lockState = CursorLockMode.Confined;

        if (virtualCursor != null)
        {
            Canvas canvas = virtualCursor.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvasRect = canvas.GetComponent<RectTransform>();
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
                    canvasCamera = canvas.worldCamera;
            }
        }

        uiInputModule = FindAnyObjectByType<InputSystemUIInputModule>();
        settingsPanel = FindAnyObjectByType<SettingsPanel>();

        if (uiInputModule == null)
        {
            Debug.LogError("InputSystemUIInputModule no encontrado.");
            enabled = false;
            return;
        }

        if (virtualCursor != null)
            virtualCursor.gameObject.SetActive(false);

        UpdateInitialDevice();
    }

    private void UpdateInitialDevice()
    {
        currentGamepad = Gamepad.current;

        if (SteamInputManager.Instance != null || currentGamepad != null)
        {
            isSteamGamepadActive = SteamInputManager.Instance != null;
            currentActiveDevice = currentGamepad;
            wasGamepadMode = true;
            SetUIMode(false);
        }
        else
        {
            currentActiveDevice = (InputDevice)Mouse.current ?? Keyboard.current;
            wasGamepadMode = false;
            SetUIMode(true);
        }
    }

    private void Update()
    {
        if (!SteamManager.Initialized)
        {
            if (uiInputModule != null && !uiInputModule.enabled)
                uiInputModule.enabled = true;
        }

        if (SteamManager.Initialized && SteamManager.OverlayActive)
            return;

        if (currentGamepad == null)
            currentGamepad = Gamepad.current;

        float deltaTime = Time.unscaledDeltaTime;
        bool canReadMouseInput = Time.unscaledTime >= ignoreMouseInputUntilTime;

        Vector2 stickValue = Vector2.zero;
        bool isGamepadActive = false;

        if (SteamManager.Initialized && SteamInputManager.Instance != null)
        {
            Vector2 moveAxis = SteamInputManager.Instance.GetMoveAxis();
            Vector2 aimAxis = SteamInputManager.Instance.GetAimAxis();

            stickValue = aimAxis;

            bool isStickMoving =
                moveAxis.magnitude > RightStickDeadZone ||
                aimAxis.magnitude > RightStickDeadZone;

            bool isButtonPressed =
                SteamInputManager.Instance.GetMeleeAttackPressed() ||
                SteamInputManager.Instance.GetDashPressed() ||
                SteamInputManager.Instance.GetMenuSelectPressed() ||
                SteamInputManager.Instance.GetMenuCancelPressed() ||
                SteamInputManager.Instance.GetMenuUpHeld() ||
                SteamInputManager.Instance.GetMenuDownHeld() ||
                SteamInputManager.Instance.GetMenuLeftHeld() ||
                SteamInputManager.Instance.GetMenuRightHeld() ||
                SteamInputManager.Instance.GetInteractPressed() ||        
                SteamInputManager.Instance.GetAdvanceDialoguePressed();

            isGamepadActive = isStickMoving || isButtonPressed;
        }
        else if (currentGamepad != null)
        {
            stickValue = currentGamepad.rightStick.ReadValue();

            bool isStickMoving =
                stickValue.magnitude > RightStickDeadZone ||
                currentGamepad.leftStick.ReadValue().magnitude > RightStickDeadZone;

            bool isButtonPressed =
                currentGamepad.dpad.IsActuated() ||
                currentGamepad.buttonSouth.wasPressedThisFrame ||
                currentGamepad.buttonEast.wasPressedThisFrame;

            isGamepadActive = isStickMoving || isButtonPressed;
        }


        bool isMouseMovedReal = false;

        if (Mouse.current != null && canReadMouseInput)
        {
            isMouseMovedReal =
                Mouse.current.delta.ReadValue().sqrMagnitude > 0.001f;
        }


        bool isMouseClick =
            canReadMouseInput &&
            Mouse.current != null &&
            (
                Mouse.current.leftButton.wasPressedThisFrame ||
                Mouse.current.rightButton.wasPressedThisFrame
            );


        bool isKeyboardPressed =
            Keyboard.current != null &&
            Keyboard.current.anyKey.wasPressedThisFrame;


        bool isMouseOrKeyboardActive =
            isMouseMovedReal ||
            isMouseClick ||
            isKeyboardPressed;


        if (isMouseOrKeyboardActive && !isGamepadActive)
        {
            currentActiveDevice =
                (isMouseMovedReal || isMouseClick)
                ? Mouse.current
                : Keyboard.current;

            isSteamGamepadActive = false;

            if (wasGamepadMode)
            {
                Debug.Log("[GamepadPointer] Control activo cambiado a: RATÓN/TECLADO");

                SetUIMode(true);

                EventSystem.current?.SetSelectedGameObject(null);

                if (uiInputModule != null)
                {
                    uiInputModule.DeactivateModule();
                    uiInputModule.ActivateModule();
                }

                wasGamepadMode = false;
            }
        }
        else if (isGamepadActive)
        {
            currentActiveDevice = currentGamepad;
            isSteamGamepadActive = SteamManager.Initialized;

            if (!wasGamepadMode)
            {
                Debug.Log("[GamepadPointer] Control activo cambiado a: GAMEPAD");

                SetUIMode(false);
                wasGamepadMode = true;
                RevertToLastSelected();
            }
        }

        bool isCurrentDeviceGamepad = IsGamepadMode();

        if (isCurrentDeviceGamepad && EventSystem.current?.currentSelectedGameObject != null)
        {
            lastSelectedObject = EventSystem.current.currentSelectedGameObject;
        }

        if (isCurrentDeviceGamepad && virtualCursor != null)
        {
            if (stickValue.magnitude > RightStickDeadZone)
            {
                virtualCursor.anchoredPosition = lastValidCursorPosition;
                Vector2 screenDelta = stickValue * cursorSpeed * deltaTime;
                Vector2 newPosition = virtualCursor.anchoredPosition + screenDelta;

                float cursorHalfWidth = virtualCursor.rect.width * 0.5f;
                float cursorHalfHeight = virtualCursor.rect.height * 0.5f;

                if (Time.timeScale != 0f || !PauseController.IsGamePaused)
                {
                    float canvasHalfWidth = canvasRect.rect.width * 0.5f;
                    float canvasHalfHeight = canvasRect.rect.height * 0.5f;
                    float maxRadius = Mathf.Min(canvasHalfWidth, canvasHalfHeight) * pauseMovementRadiusFactor;
                    float cursorHalfSize = Mathf.Max(cursorHalfWidth, cursorHalfHeight);
                    float effectiveRadius = Mathf.Max(0f, maxRadius - cursorHalfSize);

                    if (newPosition.magnitude > effectiveRadius)
                        newPosition = newPosition.normalized * effectiveRadius;
                }
                else
                {
                    float minX = -(canvasRect.rect.width * 0.5f) + cursorHalfWidth;
                    float maxX = (canvasRect.rect.width * 0.5f) - cursorHalfWidth;
                    float minY = -(canvasRect.rect.height * 0.5f) + cursorHalfHeight;
                    float maxY = (canvasRect.rect.height * 0.5f) - cursorHalfHeight;

                    newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
                    newPosition.y = Mathf.Clamp(newPosition.y, minY, maxY);
                }

                virtualCursor.anchoredPosition = newPosition;
                lastValidCursorPosition = newPosition;

                WarpHardwareMouseToVirtualCursor();
            }
            else
            {
                GameObject selectedObject = EventSystem.current?.currentSelectedGameObject;
                if (selectedObject != null)
                {
                    RectTransform targetRect = selectedObject.GetComponent<RectTransform>();
                    Slider slider = selectedObject.GetComponent<Slider>();

                    if (slider != null && IsMenuSlider(slider) && slider.handleRect != null)
                        targetRect = slider.handleRect;

                    if (targetRect != null)
                    {
                        virtualCursor.position = Vector3.Lerp(virtualCursor.position, targetRect.position, deltaTime * CursorFollowSpeed);
                        lastValidCursorPosition = virtualCursor.anchoredPosition;
                    }

                    WarpHardwareMouseToVirtualCursor();
                }
            }
        }
    }
    #endregion

    #region Utilidades
    private void SetUIMode(bool enableMouseMode)
    {
        if (uiInputModule == null) return;

        if (enableMouseMode)
        {
            uiInputModule.enabled = true;

            Cursor.visible = true;

            if (virtualCursor != null)
                virtualCursor.gameObject.SetActive(false);
        }
        else
        {
            uiInputModule.enabled = false;

            Cursor.visible = false;

            if (virtualCursor != null)
                virtualCursor.gameObject.SetActive(true);
        }
    }

    private void WarpHardwareMouseToVirtualCursor()
    {
        if (Mouse.current != null && virtualCursor != null)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, virtualCursor.position);

            if (!hasWarpedOnce || (screenPoint - lastWarpedScreenPoint).sqrMagnitude > 0.01f)
            {
                Mouse.current.WarpCursorPosition(screenPoint);
                ignoreMouseInputUntilTime = Time.unscaledTime + MouseWarpIgnoreDuration;
                lastWarpedScreenPoint = screenPoint;
                hasWarpedOnce = true;
            }
        }
    }

    private void RevertToLastSelected()
    {
        if (EventSystem.current == null) return;

        GameObject targetObject = lastSelectedObject;

        if (targetObject == null || !targetObject.activeInHierarchy)
        {
            ControlMenu controlMenu = FindAnyObjectByType<ControlMenu>();
            if (controlMenu != null)
            {
                controlMenu.FirstSelected();
                return;
            }
        }

        if (targetObject != null && targetObject.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(targetObject);
        }
    }

    private bool IsMenuSlider(Slider slider)
    {
        if (settingsPanel == null) return false;
        Slider[] menuSliders = settingsPanel.GetMenuSliders();
        if (menuSliders == null || menuSliders.Length == 0) return false;

        foreach (Slider menuSlider in menuSliders)
            if (menuSlider == slider) return true;

        return false;
    }

    public bool IsGamepadMode() => isSteamGamepadActive || (currentActiveDevice == currentGamepad && currentGamepad != null);
    public InputDevice GetCurrentActiveDevice() => currentActiveDevice;
    public Gamepad GetCurrentGamepad() => currentGamepad;

    public Vector2 GetAimDirectionValue()
    {
        if (IsSteamActive)
        {
            Vector2 aim = SteamInputManager.Instance.GetAimAxis();
            if (aim.magnitude > RightStickDeadZone) return aim.normalized;

            Vector2 move = SteamInputManager.Instance.GetMoveAxis();
            if (move.magnitude > RightStickDeadZone) return move.normalized;
        }
        else if (currentGamepad != null)
        {
            Vector2 rightStick = currentGamepad.rightStick.ReadValue();
            if (rightStick.magnitude > RightStickDeadZone) return rightStick.normalized;

            Vector2 leftStick = currentGamepad.leftStick.ReadValue();
            if (leftStick.magnitude > RightStickDeadZone) return leftStick.normalized;
        }
        return Vector2.zero;
    }
    #endregion
}