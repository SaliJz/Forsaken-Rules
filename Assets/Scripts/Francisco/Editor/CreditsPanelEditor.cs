using UnityEditor;

[CustomEditor(typeof(CreditsPanel))]
public class CreditsPanelEditor : Editor
{
    private SerializedProperty displayTypeProp;
    private SerializedProperty canvasGroupProp;
    private SerializedProperty panelAnimatorProp;
    private SerializedProperty openTriggerProp;
    private SerializedProperty closeTriggerProp;
    private SerializedProperty openCloseDurationProp;
    private SerializedProperty openEaseProp;
    private SerializedProperty closeEaseProp;
    private SerializedProperty startScaleProp;
    private SerializedProperty endScaleProp;
    private SerializedProperty firstSelectedButtonProp;
    private SerializedProperty creditsViewportProp;
    private SerializedProperty creditsContainerProp;
    private SerializedProperty scrollSpeedProp;
    private SerializedProperty autoScrollDelayProp;
    private SerializedProperty loopCreditsProp;
    private SerializedProperty leftAlignmentOffsetProp;
    private SerializedProperty centerAlignmentOffsetProp;
    private SerializedProperty rightAlignmentOffsetProp;
    private SerializedProperty creditsEntriesProp;
    private SerializedProperty titlePrefabProp;
    private SerializedProperty subtitlePrefabProp;
    private SerializedProperty rolePrefabProp;
    private SerializedProperty namePrefabProp;
    private SerializedProperty roleWithNamePrefabProp;
    private SerializedProperty imageWithTextPrefabProp;
    private SerializedProperty spacerPrefabProp;
    private SerializedProperty defaultSpacingProp;
    private SerializedProperty titleColorProp;
    private SerializedProperty subtitleColorProp;
    private SerializedProperty roleColorProp;
    private SerializedProperty nameColorProp;
    private SerializedProperty estadoDeVidaMaterial;

    private void OnEnable()
    {
        displayTypeProp = serializedObject.FindProperty("displayType");
        canvasGroupProp = serializedObject.FindProperty("canvasGroup");
        panelAnimatorProp = serializedObject.FindProperty("panelAnimator");
        openTriggerProp = serializedObject.FindProperty("openTrigger");
        closeTriggerProp = serializedObject.FindProperty("closeTrigger");
        openCloseDurationProp = serializedObject.FindProperty("openCloseDuration");
        openEaseProp = serializedObject.FindProperty("openEase");
        closeEaseProp = serializedObject.FindProperty("closeEase");
        startScaleProp = serializedObject.FindProperty("startScale");
        endScaleProp = serializedObject.FindProperty("endScale");
        firstSelectedButtonProp = serializedObject.FindProperty("firstSelectedButton");
        creditsViewportProp = serializedObject.FindProperty("creditsViewport");
        creditsContainerProp = serializedObject.FindProperty("creditsContainer");
        scrollSpeedProp = serializedObject.FindProperty("scrollSpeed");
        autoScrollDelayProp = serializedObject.FindProperty("autoScrollDelay");
        loopCreditsProp = serializedObject.FindProperty("loopCredits");
        leftAlignmentOffsetProp = serializedObject.FindProperty("leftAlignmentOffset");
        centerAlignmentOffsetProp = serializedObject.FindProperty("centerAlignmentOffset");
        rightAlignmentOffsetProp = serializedObject.FindProperty("rightAlignmentOffset");
        creditsEntriesProp = serializedObject.FindProperty("creditsEntries");
        titlePrefabProp = serializedObject.FindProperty("titlePrefab");
        subtitlePrefabProp = serializedObject.FindProperty("subtitlePrefab");
        rolePrefabProp = serializedObject.FindProperty("rolePrefab");
        namePrefabProp = serializedObject.FindProperty("namePrefab");
        roleWithNamePrefabProp = serializedObject.FindProperty("roleWithNamePrefab");
        imageWithTextPrefabProp = serializedObject.FindProperty("imageWithTextPrefab");
        spacerPrefabProp = serializedObject.FindProperty("spacerPrefab");
        defaultSpacingProp = serializedObject.FindProperty("defaultSpacing");
        titleColorProp = serializedObject.FindProperty("titleColor");
        subtitleColorProp = serializedObject.FindProperty("subtitleColor");
        roleColorProp = serializedObject.FindProperty("roleColor");
        nameColorProp = serializedObject.FindProperty("nameColor");
        estadoDeVidaMaterial = serializedObject.FindProperty("estadoDeVidaMaterial");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Panel Display Settings", EditorStyles.boldLabel);
        if (displayTypeProp != null)
        {
            EditorGUILayout.PropertyField(displayTypeProp);
            CreditsPanel.PanelDisplayType displayType = (CreditsPanel.PanelDisplayType)displayTypeProp.enumValueIndex;

            switch (displayType)
            {
                case CreditsPanel.PanelDisplayType.CanvasFade:
                    if (canvasGroupProp != null) EditorGUILayout.PropertyField(canvasGroupProp);
                    if (openCloseDurationProp != null) EditorGUILayout.PropertyField(openCloseDurationProp);
                    break;

                case CreditsPanel.PanelDisplayType.AnimatedScale:
                    if (openCloseDurationProp != null) EditorGUILayout.PropertyField(openCloseDurationProp);
                    if (openEaseProp != null) EditorGUILayout.PropertyField(openEaseProp);
                    if (closeEaseProp != null) EditorGUILayout.PropertyField(closeEaseProp);
                    if (startScaleProp != null) EditorGUILayout.PropertyField(startScaleProp);
                    if (endScaleProp != null) EditorGUILayout.PropertyField(endScaleProp);
                    break;

                case CreditsPanel.PanelDisplayType.Animator:
                    if (panelAnimatorProp != null) EditorGUILayout.PropertyField(panelAnimatorProp);
                    if (openTriggerProp != null) EditorGUILayout.PropertyField(openTriggerProp);
                    if (closeTriggerProp != null) EditorGUILayout.PropertyField(closeTriggerProp);
                    break;
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Focus Control", EditorStyles.boldLabel);
        if (firstSelectedButtonProp != null) EditorGUILayout.PropertyField(firstSelectedButtonProp);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Credits Scroller Settings", EditorStyles.boldLabel);
        if (creditsViewportProp != null) EditorGUILayout.PropertyField(creditsViewportProp);
        if (creditsContainerProp != null) EditorGUILayout.PropertyField(creditsContainerProp);
        if (scrollSpeedProp != null) EditorGUILayout.PropertyField(scrollSpeedProp);
        if (autoScrollDelayProp != null) EditorGUILayout.PropertyField(autoScrollDelayProp);
        if (loopCreditsProp != null) EditorGUILayout.PropertyField(loopCreditsProp);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Alignment Offsets", EditorStyles.boldLabel);
        if (leftAlignmentOffsetProp != null) EditorGUILayout.PropertyField(leftAlignmentOffsetProp);
        if (centerAlignmentOffsetProp != null) EditorGUILayout.PropertyField(centerAlignmentOffsetProp);
        if (rightAlignmentOffsetProp != null) EditorGUILayout.PropertyField(rightAlignmentOffsetProp);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Prefab References", EditorStyles.boldLabel);
        if (titlePrefabProp != null) EditorGUILayout.PropertyField(titlePrefabProp);
        if (subtitlePrefabProp != null) EditorGUILayout.PropertyField(subtitlePrefabProp);
        if (rolePrefabProp != null) EditorGUILayout.PropertyField(rolePrefabProp);
        if (namePrefabProp != null) EditorGUILayout.PropertyField(namePrefabProp);
        if (roleWithNamePrefabProp != null) EditorGUILayout.PropertyField(roleWithNamePrefabProp);
        if (imageWithTextPrefabProp != null) EditorGUILayout.PropertyField(imageWithTextPrefabProp);
        if (spacerPrefabProp != null) EditorGUILayout.PropertyField(spacerPrefabProp);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Style Settings", EditorStyles.boldLabel);
        if (defaultSpacingProp != null) EditorGUILayout.PropertyField(defaultSpacingProp);
        if (titleColorProp != null) EditorGUILayout.PropertyField(titleColorProp);
        if (subtitleColorProp != null) EditorGUILayout.PropertyField(subtitleColorProp);
        if (roleColorProp != null) EditorGUILayout.PropertyField(roleColorProp);
        if (nameColorProp != null) EditorGUILayout.PropertyField(nameColorProp);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Credits Content", EditorStyles.boldLabel);
        if (creditsEntriesProp != null) EditorGUILayout.PropertyField(creditsEntriesProp, true);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("References Content", EditorStyles.boldLabel);
        if (estadoDeVidaMaterial != null) EditorGUILayout.PropertyField(estadoDeVidaMaterial);


        serializedObject.ApplyModifiedProperties();
    }
}