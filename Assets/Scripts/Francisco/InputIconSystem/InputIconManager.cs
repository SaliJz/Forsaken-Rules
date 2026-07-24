using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

public class InputIconManager : MonoBehaviour
{
    public static InputIconManager Instance { get; private set; }

    [Header("Configuración General")]
    public InputIconData.GamepadType defaultGamepadType = InputIconData.GamepadType.Xbox;

    public enum GamepadLogicMode { Unified, Separate }
    public GamepadLogicMode gamepadLogicMode = GamepadLogicMode.Separate;


    [Header("Sets de Íconos (ScriptableObjects)")]
    [SerializeField] private InputIconData keyboardIcons;
    [SerializeField] private InputIconData unifiedOrDefaultGamepadIcons;
    [SerializeField] private List<InputIconData> specificGamepadIcons;

    private InputIconData currentActiveIconSet = null;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UpdateIconSet();
    }

    private void Update()
    {
        if (GamepadPointer.Instance != null)
        {
            bool isGamepadMode = GamepadPointer.Instance.IsGamepadMode();

            if (currentActiveIconSet == null || !IsCorrectSetForMode(currentActiveIconSet, isGamepadMode))
            {
                UpdateIconSet();
            }
        }
    }

    private bool IsCorrectSetForMode(InputIconData iconSet, bool isGamepadMode)
    {
        return iconSet.isKeyboardScheme != isGamepadMode;
    }

    private InputIconData GetBestGamepadIconSet()
    {
        InputIconData.GamepadType currentType = GetCurrentGamepadType();

        if (gamepadLogicMode == GamepadLogicMode.Unified)
        {
            return unifiedOrDefaultGamepadIcons;
        }

        if (currentType != InputIconData.GamepadType.Default && currentType != InputIconData.GamepadType.Generic)
        {
            InputIconData specificSet = specificGamepadIcons.FirstOrDefault(d => d.gamepadType == currentType);

            if (specificSet != null)
            {
                return specificSet;
            }
        }

        return unifiedOrDefaultGamepadIcons;
    }


    public InputIconData.GamepadType GetCurrentGamepadType()
    {
        Gamepad currentGamepad = GamepadPointer.Instance?.GetCurrentGamepad();

        if (currentGamepad == null) return InputIconData.GamepadType.Default;

        string deviceName = currentGamepad.displayName.ToLower();

        if (deviceName.Contains("xbox") || deviceName.Contains("microsoft"))
        {
            return InputIconData.GamepadType.Xbox;
        }
        else if (deviceName.Contains("playstation") || deviceName.Contains("dualsense") || deviceName.Contains("dualshock"))
        {
            return InputIconData.GamepadType.PlayStation;
        }
        else if (deviceName.Contains("nintendo") || deviceName.Contains("switch"))
        {
            return InputIconData.GamepadType.Nintendo;
        }

        return InputIconData.GamepadType.Generic;
    }

    public void UpdateIconSet()
    {
        if (GamepadPointer.Instance == null) return;

        bool isGamepadMode = GamepadPointer.Instance.IsGamepadMode();

        if (isGamepadMode)
        {
            currentActiveIconSet = GetBestGamepadIconSet();
        }
        else
        {
            currentActiveIconSet = keyboardIcons;
        }

        if (currentActiveIconSet == null)
        {
            Debug.LogError("[InputIconManager] No se pudo encontrar un set de íconos activo.");
        }
    }

    public string GetPromptForAction(string actionName)
    {
        if (currentActiveIconSet == null)
        {
            UpdateIconSet();
            if (currentActiveIconSet == null)
            {
                return $"[{actionName}]";
            }
        }

        return currentActiveIconSet.GetIconSpriteString(actionName);
    }
}