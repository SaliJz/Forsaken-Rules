using UnityEditor;

[CustomEditor(typeof(SettingsPanel))]
public class SettingsPanelEditor : Editor
{
    SerializedProperty displayTypeProp;

    SerializedProperty masterMixerProp;
    SerializedProperty masterSliderProp;
    SerializedProperty musicSliderProp;
    SerializedProperty sfxSliderProp;

    SerializedProperty canvasGroupProp;

    SerializedProperty durationProp;
    SerializedProperty openEaseProp;
    SerializedProperty closeEaseProp;

    SerializedProperty startScaleProp;
    SerializedProperty endScaleProp;

    SerializedProperty firstSelectedButtonProp;

    private void OnEnable()
    {
        displayTypeProp = serializedObject.FindProperty("displayType");
        masterMixerProp = serializedObject.FindProperty("masterMixer");
        masterSliderProp = serializedObject.FindProperty("masterSlider");
        musicSliderProp = serializedObject.FindProperty("musicSlider");
        sfxSliderProp = serializedObject.FindProperty("sfxSlider");
        canvasGroupProp = serializedObject.FindProperty("canvasGroup");
        durationProp = serializedObject.FindProperty("openCloseDuration");
        openEaseProp = serializedObject.FindProperty("openEase");
        closeEaseProp = serializedObject.FindProperty("closeEase");
        startScaleProp = serializedObject.FindProperty("startScale");
        endScaleProp = serializedObject.FindProperty("endScale");
        firstSelectedButtonProp = serializedObject.FindProperty("firstSelectedButton");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SettingsPanel.PanelDisplayType currentDisplayType =
            (SettingsPanel.PanelDisplayType)displayTypeProp.enumValueIndex;

        EditorGUILayout.LabelField("Panel Display Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(displayTypeProp);

        if (currentDisplayType == SettingsPanel.PanelDisplayType.CanvasFade)
        {
            EditorGUILayout.PropertyField(canvasGroupProp);
            EditorGUILayout.Space();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Audio Mixer References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(masterMixerProp);
        EditorGUILayout.PropertyField(masterSliderProp);
        EditorGUILayout.PropertyField(musicSliderProp);
        EditorGUILayout.PropertyField(sfxSliderProp);

        EditorGUILayout.Space(10);

        if (currentDisplayType != SettingsPanel.PanelDisplayType.Static)
        {
            EditorGUILayout.LabelField("DOTween Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(durationProp);
            EditorGUILayout.PropertyField(openEaseProp);
            EditorGUILayout.PropertyField(closeEaseProp);

            EditorGUILayout.Space();

            if (currentDisplayType == SettingsPanel.PanelDisplayType.AnimatedScale)
            {
                EditorGUILayout.LabelField("Scale Animation Vectors", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(startScaleProp);
                EditorGUILayout.PropertyField(endScaleProp);
            }
            else if (currentDisplayType == SettingsPanel.PanelDisplayType.CanvasFade)
            {
                EditorGUILayout.HelpBox("Modo Canvas Fade activo. La animación usa 'Alpha' del Canvas Group. Los vectores de escala están ocultos.", MessageType.Info);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Modo Estático seleccionado. Las configuraciones de DOTween están ocultas y el panel solo se activa/desactiva.", MessageType.Info);
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Focus Control", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(firstSelectedButtonProp);

        serializedObject.ApplyModifiedProperties();
    }
}