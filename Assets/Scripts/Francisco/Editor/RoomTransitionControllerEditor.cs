using UnityEditor;

[CustomEditor(typeof(RoomTransitionController))]
public class RoomTransitionControllerEditor : Editor
{
    private SerializedProperty transitionMode;
    private SerializedProperty playerMoveDuration;
    private SerializedProperty playerMoveDistance;
    private SerializedProperty doorActivateDelay;
    private SerializedProperty traslaciónPrefab;
    private SerializedProperty elevatorController;

    private void OnEnable()
    {
        transitionMode = serializedObject.FindProperty("transitionMode");
        playerMoveDuration = serializedObject.FindProperty("playerMoveDuration");
        playerMoveDistance = serializedObject.FindProperty("playerMoveDistance");
        doorActivateDelay = serializedObject.FindProperty("doorActivateDelay");
        traslaciónPrefab = serializedObject.FindProperty("traslaciónPrefab");
        elevatorController = serializedObject.FindProperty("elevatorController");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Modo de Transición", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(transitionMode);

        EditorGUILayout.Space(8);

        TransitionMode mode = (TransitionMode)transitionMode.enumValueIndex;

        switch (mode)
        {
            case TransitionMode.Classic:
                EditorGUILayout.LabelField("Configuración Classic", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(playerMoveDuration);
                EditorGUILayout.PropertyField(playerMoveDistance);
                EditorGUILayout.PropertyField(doorActivateDelay);
                break;

            case TransitionMode.Level1:
                EditorGUILayout.LabelField("Configuración Level 1", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Level 1 usa ascensores y traslaciones por nodos. Asigna los componentes correspondientes.", MessageType.Info);
                EditorGUILayout.PropertyField(traslaciónPrefab);
                EditorGUILayout.PropertyField(elevatorController);
                break;
        }

        EditorGUILayout.Space(4);
        serializedObject.ApplyModifiedProperties();
    }
}