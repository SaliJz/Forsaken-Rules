using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InputIconManager))]
public class InputIconManagerEditor : Editor
{
    private SerializedProperty gamepadLogicMode;
    private SerializedProperty keyboardIcons;
    private SerializedProperty unifiedOrDefaultGamepadIcons;
    private SerializedProperty specificGamepadIcons;

    private void OnEnable()
    {
        gamepadLogicMode = serializedObject.FindProperty("gamepadLogicMode");
        keyboardIcons = serializedObject.FindProperty("keyboardIcons");
        unifiedOrDefaultGamepadIcons = serializedObject.FindProperty("unifiedOrDefaultGamepadIcons");
        specificGamepadIcons = serializedObject.FindProperty("specificGamepadIcons");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Sets de Íconos Base", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(keyboardIcons, new GUIContent("Teclado/Ratón"));

        EditorGUILayout.PropertyField(unifiedOrDefaultGamepadIcons, new GUIContent("Unificado / Default Gamepad"));
        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Lógica de Gamepad", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(gamepadLogicMode, new GUIContent("Modo de Íconos"));

        InputIconManager.GamepadLogicMode currentMode =
            (InputIconManager.GamepadLogicMode)gamepadLogicMode.enumValueIndex;

        if (currentMode == InputIconManager.GamepadLogicMode.Separate)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Modo Separado: Se usan íconos específicos (PS, Xbox, etc.) si se detecta el mando. El set 'Unificado' será el genérico/Default.",
                MessageType.Info
            );
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(specificGamepadIcons, new GUIContent("Sets de Gamepad Específicos"), true);
        }
        else 
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
               "Modo Unificado: Todos los gamepads usarán el set asignado en 'Unificado / Default Gamepad'. La lista de específicos está oculta.",
               MessageType.None
           );
        }

        serializedObject.ApplyModifiedProperties();
    }
}