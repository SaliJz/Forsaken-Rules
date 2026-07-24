using Steamworks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

#region Configuracion
public class SteamInputManager : MonoBehaviour
{
    private static SteamInputManager instance;
    public static SteamInputManager Instance => instance;

    private InputHandle_t[] controllerHandles = new InputHandle_t[Constants.STEAM_INPUT_MAX_COUNT];
    private int activeControllerCount;
    private bool actionsInitialized;

    private InputActionSetHandle_t inGameSetHandle;
    private InputActionSetHandle_t menuSetHandle;

    private InputDigitalActionHandle_t meleeAttackHandle;
    private InputDigitalActionHandle_t shieldThrowHandle;
    private InputDigitalActionHandle_t dashHandle;
    private InputDigitalActionHandle_t pauseMenuHandle;
    private InputAnalogActionHandle_t moveHandle;
    private InputDigitalActionHandle_t interactHandle;
    private InputDigitalActionHandle_t inventoryHandle;
    private InputDigitalActionHandle_t activateSkillHandle;
    private InputDigitalActionHandle_t defenseHandle;
    private InputDigitalActionHandle_t advanceDialogueHandle;
    private InputDigitalActionHandle_t menuUpHandle;
    private InputDigitalActionHandle_t menuDownHandle;
    private InputDigitalActionHandle_t menuLeftHandle;
    private InputDigitalActionHandle_t menuRightHandle;
    private InputDigitalActionHandle_t menuSelectHandle;
    private InputDigitalActionHandle_t menuCancelHandle;
    private InputDigitalActionHandle_t menuSubmitHandle;
    private InputAnalogActionHandle_t aimHandle;

    private Dictionary<ulong, bool> previousFrameStates = new Dictionary<ulong, bool>();
    private Dictionary<ulong, bool> currentFrameStates = new Dictionary<ulong, bool>();

    private Vector2 heldMenuDirection;
    private float menuRepeatTimer;
    private float menuRepeatIntervalTimer;

    private const float MenuRepeatDelay = 0.35f;
    private const float MenuRepeatInterval = 0.12f;

    private string ultimoModoDebug = "Ninguno";
    #endregion

    #region Ciclo De Vida
    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.Log("<color=green>[SteamInput] Steam no detectado. Modo Nativo Unity activo. Todo funciona igual que siempre.</color>");
            yield break;
        }

        Debug.Log("<color=cyan>[SteamInput] Steam detectado. Activando escudo de hardware.</color>");

#if UNITY_EDITOR
        Application.OpenURL("steam://forceinputappid/4858720");
#endif

        string manifestPath = System.IO.Path.Combine(Application.dataPath, "..", "steam_input_manifest.vdf");
        string fullPath = System.IO.Path.GetFullPath(manifestPath);

        bool manifestSet = SteamInput.SetInputActionManifestFilePath(fullPath);
        bool inputInitialized = SteamInput.Init(false);

        float timeout = 8f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            SteamInput.RunFrame();

            inGameSetHandle = SteamInput.GetActionSetHandle("InGameControls");
            menuSetHandle = SteamInput.GetActionSetHandle("MenuControls");

            if (inGameSetHandle != default(InputActionSetHandle_t) && menuSetHandle != default(InputActionSetHandle_t))
            {
                Debug.Log($"<color=lime>[SteamInput] Action sets resueltos tras {elapsed:F2}s</color> | inGameSetHandle={inGameSetHandle.m_InputActionSetHandle} | menuSetHandle={menuSetHandle.m_InputActionSetHandle}");
                break;
            }

            yield return new WaitForSecondsRealtime(0.25f);
            elapsed += 0.25f;
        }

        if (inGameSetHandle == default(InputActionSetHandle_t))
        {
            Debug.LogError("[SteamInput] No se pudo cargar InGameControls tras reintentar durante 8s...");
            yield break;
        }

        InputSystemUIInputModule uiInputModule = FindAnyObjectByType<InputSystemUIInputModule>();
        if (uiInputModule != null) uiInputModule.enabled = false;

        foreach (var device in InputSystem.devices)
        {
            if (device is Gamepad)
            {
                InputSystem.DisableDevice(device);
            }
        }

        Debug.Log("<color=#00FFCC>[SteamInputManager]</color> Escudo de hardware aplicado: Control exclusivo para Steam.");
    }

    private void Update()
    {
        if (!SteamManager.Initialized) return;

        SteamInput.RunFrame();
        RefreshControllers();

        if (activeControllerCount <= 0) return;

        if (!actionsInitialized)
        {
            InitializeActions();
            actionsInitialized = true;
        }

        UpdateFrameStates();
        HandleMenuNavigation();

        if (SteamManager.OverlayActive) return;

        if (IsMenuScene())
        {
            ActivateMenuSet();
        }
    }

    private void UpdateFrameStates()
    {
        previousFrameStates.Clear();
        foreach (var kvp in currentFrameStates)
            previousFrameStates[kvp.Key] = kvp.Value;

        currentFrameStates.Clear();

        if (SteamManager.OverlayActive) return;

        InputHandle_t controller = GetFirstController();
        if (controller == default(InputHandle_t)) return;

        CacheDigitalState(meleeAttackHandle, controller);
        CacheDigitalState(shieldThrowHandle, controller);
        CacheDigitalState(dashHandle, controller);
        CacheDigitalState(pauseMenuHandle, controller);
        CacheDigitalState(interactHandle, controller);
        CacheDigitalState(inventoryHandle, controller);
        CacheDigitalState(activateSkillHandle, controller);
        CacheDigitalState(defenseHandle, controller);
        CacheDigitalState(advanceDialogueHandle, controller);
        CacheDigitalState(menuUpHandle, controller);
        CacheDigitalState(menuDownHandle, controller);
        CacheDigitalState(menuLeftHandle, controller);
        CacheDigitalState(menuRightHandle, controller);
        CacheDigitalState(menuSelectHandle, controller);
        CacheDigitalState(menuCancelHandle, controller);
        CacheDigitalState(menuSubmitHandle, controller);
    }

    private void HandleMenuNavigation()
    {
        if (!IsMenuScene() && !PauseController.IsGamePaused) return;
        if (EventSystem.current == null) return;

        bool inputDetectado = GetMenuUpHeld() || GetMenuDownHeld() || GetMenuLeftHeld() || GetMenuRightHeld() ||
                              GetMenuSelectPressed() || GetMenuCancelPressed() || GetMenuSubmitPressed();

        if (inputDetectado)
        {
            if (EventSystem.current.currentSelectedGameObject == null)
            {
                ControlMenu controlMenu = FindAnyObjectByType<ControlMenu>();
                if (controlMenu != null)
                {
                    controlMenu.FirstSelected();
                }
            }
        }

        if (EventSystem.current.currentSelectedGameObject == null) return;

        Vector2 input = Vector2.zero;

        if (GetMenuUpHeld()) input = Vector2.up;
        else if (GetMenuDownHeld()) input = Vector2.down;
        else if (GetMenuLeftHeld()) input = Vector2.left;
        else if (GetMenuRightHeld()) input = Vector2.right;

        if (input != Vector2.zero)
        {
            if (heldMenuDirection != input)
            {
                heldMenuDirection = input;
                menuRepeatTimer = 0;
                menuRepeatIntervalTimer = 0;
                NavigateUI(input);
            }
            else
            {
                menuRepeatTimer += Time.unscaledDeltaTime;
                if (menuRepeatTimer >= MenuRepeatDelay)
                {
                    menuRepeatIntervalTimer += Time.unscaledDeltaTime;
                    if (menuRepeatIntervalTimer >= MenuRepeatInterval)
                    {
                        NavigateUI(input);
                        menuRepeatIntervalTimer = 0;
                    }
                }
            }
        }
        else
        {
            heldMenuDirection = Vector2.zero;
            menuRepeatTimer = 0;
            menuRepeatIntervalTimer = 0;
        }

        if (GetMenuSelectPressed() || GetMenuSubmitPressed()) SubmitUI();
        if (GetMenuCancelPressed()) CancelUI();
    }

    private void LogCambioDeSet(string nuevoModo)
    {
        if (ultimoModoDebug != nuevoModo)
        {
            ultimoModoDebug = nuevoModo;
            Debug.Log($"<color=#00FFCC>[SteamInput Debug]</color> Cambiando a Modo: <b>{nuevoModo}</b> (Escena Actual: {SceneManager.GetActiveScene().name}, Pausa: {PauseController.IsGamePaused})");
        }
    }

    private void NavigateUI(Vector2 dir)
    {
        if (EventSystem.current == null)
            return;


        GameObject selected = EventSystem.current.currentSelectedGameObject;


        if (selected == null)
            return;


        AxisEventData data = new AxisEventData(EventSystem.current);

        data.moveVector = dir;


        data.moveDir =
            dir.y > 0 ? MoveDirection.Up :
            dir.y < 0 ? MoveDirection.Down :
            dir.x > 0 ? MoveDirection.Right :
                        MoveDirection.Left;


        ExecuteEvents.Execute(
            selected,
            data,
            ExecuteEvents.moveHandler
        );
    }

    private void SubmitUI()
    {
        if (EventSystem.current?.currentSelectedGameObject == null)
            return;


        ExecuteEvents.Execute(
            EventSystem.current.currentSelectedGameObject,
            new BaseEventData(EventSystem.current),
            ExecuteEvents.submitHandler
        );
    }



    private void CancelUI()
    {
        if (EventSystem.current?.currentSelectedGameObject == null)
            return;


        ExecuteEvents.Execute(
            EventSystem.current.currentSelectedGameObject,
            new BaseEventData(EventSystem.current),
            ExecuteEvents.cancelHandler
        );
    }

    private void CacheDigitalState(InputDigitalActionHandle_t handle, InputHandle_t controller)
    {
        if (handle == default(InputDigitalActionHandle_t)) return;
        bool state = SteamInput.GetDigitalActionData(controller, handle).bState != 0;
        currentFrameStates[handle.m_InputDigitalActionHandle] = state;
    }

    private void InitializeActions()
    {
        meleeAttackHandle = SteamInput.GetDigitalActionHandle("melee_attack");
        shieldThrowHandle = SteamInput.GetDigitalActionHandle("shield_throw");
        dashHandle = SteamInput.GetDigitalActionHandle("dash");
        pauseMenuHandle = SteamInput.GetDigitalActionHandle("pause_menu");

        moveHandle = SteamInput.GetAnalogActionHandle("Move");
        aimHandle = SteamInput.GetAnalogActionHandle("Aim");

        interactHandle = SteamInput.GetDigitalActionHandle("interact");
        inventoryHandle = SteamInput.GetDigitalActionHandle("inventory");
        activateSkillHandle = SteamInput.GetDigitalActionHandle("activate_skill");
        defenseHandle = SteamInput.GetDigitalActionHandle("defense");
        advanceDialogueHandle = SteamInput.GetDigitalActionHandle("advance_dialogue");

        menuUpHandle = SteamInput.GetDigitalActionHandle("menu_up");
        menuDownHandle = SteamInput.GetDigitalActionHandle("menu_down");
        menuLeftHandle = SteamInput.GetDigitalActionHandle("menu_left");
        menuRightHandle = SteamInput.GetDigitalActionHandle("menu_right");
        menuSelectHandle = SteamInput.GetDigitalActionHandle("menu_select");
        menuCancelHandle = SteamInput.GetDigitalActionHandle("menu_cancel");
        menuSubmitHandle = SteamInput.GetDigitalActionHandle("menu_submit");
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
    #endregion

    #region Escenas Menu

    [SerializeField] private string[] menuScenes;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (string menuScene in menuScenes)
        {
            if (scene.name == menuScene)
            {
                ActivateMenuSet();
                return;
            }
        }

        ActivateInGameSet();
    }

    private bool IsMenuScene()
    {
        foreach (string s in menuScenes)
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == s)
                return true;

        return false;
    }
    #endregion

    #region Controladores
    private void RefreshControllers()
    {
        activeControllerCount = SteamInput.GetConnectedControllers(controllerHandles);
    }

    private InputHandle_t GetFirstController()
    {
        if (!SteamManager.Initialized) return default(InputHandle_t);

        if (activeControllerCount <= 0)
            activeControllerCount = SteamInput.GetConnectedControllers(controllerHandles);

        return activeControllerCount > 0 ? controllerHandles[0] : default(InputHandle_t);
    }
    #endregion

    #region Action Sets
    public void ActivateInGameSet()
    {
        InputHandle_t controller = GetFirstController();
        if (controller == default(InputHandle_t)) return;
        if (inGameSetHandle == default(InputActionSetHandle_t)) return;

        SteamInput.ActivateActionSet(controller, inGameSetHandle);
        LogCambioDeSet("GAME CONTROLS (InGame)");
    }

    public void ActivateMenuSet()
    {
        InputHandle_t controller = GetFirstController();
        if (controller == default(InputHandle_t)) return;
        if (menuSetHandle == default(InputActionSetHandle_t)) return;

        SteamInput.ActivateActionSet(controller, menuSetHandle);
        LogCambioDeSet("MENU CONTROLS (Menus/Pausa)");
    }
    #endregion

    #region Lecturas Publicas - Estado Sostenido
    public bool GetMeleeAttackPressed() => IsHeld(meleeAttackHandle);
    public bool GetShieldThrowPressed() => IsHeld(shieldThrowHandle);
    public bool GetDashPressed() => IsHeld(dashHandle);
    public bool GetInteractPressed() => IsHeld(interactHandle);
    public bool GetInventoryPressed() => IsHeld(inventoryHandle);
    public bool GetActivateSkillPressed() => IsHeld(activateSkillHandle);
    public bool GetDefensePressed() => IsHeld(defenseHandle);
    public bool GetAdvanceDialoguePressed() => IsHeld(advanceDialogueHandle);
    public bool GetAdvanceDialogueJustPressed() => IsJustPressed(advanceDialogueHandle);

    public Vector2 GetMoveAxis()
    {
        if (!SteamManager.Initialized) return Vector2.zero;
        if (SteamManager.OverlayActive) return Vector2.zero;
        if (moveHandle == default(InputAnalogActionHandle_t)) return Vector2.zero;

        InputHandle_t controller = GetFirstController();
        if (controller == default(InputHandle_t)) return Vector2.zero;

        InputAnalogActionData_t data = SteamInput.GetAnalogActionData(controller, moveHandle);
        if (data.bActive == 0) return Vector2.zero;

        return new Vector2(data.x, data.y);
    }

    public Vector2 GetAimAxis()
    {
        if (!SteamManager.Initialized) return Vector2.zero;
        if (SteamManager.OverlayActive) return Vector2.zero;
        if (aimHandle == default(InputAnalogActionHandle_t)) return Vector2.zero;

        InputHandle_t controller = GetFirstController();
        if (controller == default(InputHandle_t)) return Vector2.zero;

        InputAnalogActionData_t data = SteamInput.GetAnalogActionData(controller, aimHandle);
        if (data.bActive == 0) return Vector2.zero;

        return new Vector2(data.x, data.y);
    }
    #endregion

    #region Lecturas Publicas - Flanco
    public bool GetMenuUpPressed() => IsJustPressed(menuUpHandle);
    public bool GetMenuDownPressed() => IsJustPressed(menuDownHandle);
    public bool GetMenuLeftPressed() => IsJustPressed(menuLeftHandle);
    public bool GetMenuRightPressed() => IsJustPressed(menuRightHandle);
    public bool GetMenuSelectPressed() => IsJustPressed(menuSelectHandle);
    public bool GetMenuCancelPressed() => IsJustPressed(menuCancelHandle);
    public bool GetMenuSubmitPressed() => IsJustPressed(menuSubmitHandle);
    public bool GetPauseMenuPressed() => IsJustPressed(pauseMenuHandle);
    public bool GetInteractJustPressed() => IsJustPressed(interactHandle);
    public bool GetMenuUpHeld() => IsHeld(menuUpHandle);
    public bool GetMenuDownHeld() => IsHeld(menuDownHandle);
    public bool GetMenuLeftHeld() => IsHeld(menuLeftHandle);
    public bool GetMenuRightHeld() => IsHeld(menuRightHandle);
    #endregion

    #region Helpers
    private bool IsHeld(InputDigitalActionHandle_t handle)
    {
        if (!SteamManager.Initialized) return false;
        if (SteamManager.OverlayActive) return false;
        if (handle == default(InputDigitalActionHandle_t)) return false;

        ulong key = handle.m_InputDigitalActionHandle;
        return currentFrameStates.TryGetValue(key, out bool state) && state;
    }

    private bool IsJustPressed(InputDigitalActionHandle_t handle)
    {
        if (!SteamManager.Initialized) return false;
        if (SteamManager.OverlayActive) return false;
        if (handle == default(InputDigitalActionHandle_t)) return false;

        ulong key = handle.m_InputDigitalActionHandle;

        bool isCurrent = currentFrameStates.TryGetValue(key, out bool curr) && curr;
        bool wasPrev = previousFrameStates.TryGetValue(key, out bool prev) && prev;

        return isCurrent && !wasPrev;
    }
    #endregion
}