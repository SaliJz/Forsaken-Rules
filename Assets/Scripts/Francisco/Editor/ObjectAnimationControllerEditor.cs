using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ObjectAnimationController))]
public class ObjectAnimationControllerEditor : Editor
{
    private SerializedProperty isSelectedOrConnectedProp;

    private SerializedProperty rotateXProp, rotateYProp, rotateZProp;

    private SerializedProperty speedXProp, speedYProp, speedZProp;

    private SerializedProperty directionXProp, directionYProp, directionZProp;


    private void OnEnable()
    {
        isSelectedOrConnectedProp = serializedObject.FindProperty("isSelectedOrConnected");

        rotateXProp = serializedObject.FindProperty("rotateX");
        rotateYProp = serializedObject.FindProperty("rotateY");
        rotateZProp = serializedObject.FindProperty("rotateZ");

        speedXProp = serializedObject.FindProperty("speedX");
        speedYProp = serializedObject.FindProperty("speedY");
        speedZProp = serializedObject.FindProperty("speedZ");

        directionXProp = serializedObject.FindProperty("directionX");
        directionYProp = serializedObject.FindProperty("directionY");
        directionZProp = serializedObject.FindProperty("directionZ");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Visibility State", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(isSelectedOrConnectedProp);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Rotation Activation & Settings", EditorStyles.boldLabel);

        DrawRotationAxisControl("X Axis Rotation", rotateXProp, speedXProp, directionXProp);

        DrawRotationAxisControl("Y Axis Rotation", rotateYProp, speedYProp, directionYProp);

        DrawRotationAxisControl("Z Axis Rotation", rotateZProp, speedZProp, directionZProp);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Scaling Settings", EditorStyles.boldLabel);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawRotationAxisControl(
        string label,
        SerializedProperty activateProp,
        SerializedProperty speedProp,
        SerializedProperty directionProp)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(activateProp, new GUIContent(label));

        if (activateProp.boolValue)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(speedProp, new GUIContent("Speed (degrees/sec)"));

            EditorGUILayout.PropertyField(
                directionProp,
                new GUIContent("Direction (Positive / Negative)")
            );

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }
}