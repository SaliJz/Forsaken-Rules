using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "InputIconData", menuName = "Input/Icon Data", order = 1)]
public class InputIconData : ScriptableObject
{
    public enum GamepadType { Default, Xbox, PlayStation, Nintendo, Generic }

    [Header("Configuración del Esquema")]
    public bool isKeyboardScheme = false;
    public GamepadType gamepadType = GamepadType.Default;

    [System.Serializable]
    public class ActionIcon
    {
        public string actionName;

        [Tooltip("El string de TextMeshPro para el ícono/sprite")]
        public string iconSpriteString;
    }

    public List<ActionIcon> actionIcons;

    public string GetIconSpriteString(string actionName)
    {
        ActionIcon match = actionIcons.Find(a => a.actionName.Equals(actionName, System.StringComparison.OrdinalIgnoreCase));

        return match != null ? match.iconSpriteString : $"[{actionName}]";
    }
}