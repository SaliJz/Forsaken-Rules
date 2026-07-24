#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FloorBase), editorForChildClasses: true)]
public class FloorBaseEditor : Editor
{
    #region Styles & Colors

    private static readonly Color ColorDefault = new Color(0.30f, 0.85f, 0.40f);
    private static readonly Color ColorTriggered = new Color(0.95f, 0.28f, 0.28f);
    private static readonly Color ColorTransitioning = new Color(1.00f, 0.75f, 0.10f);
    private static readonly Color ColorHeaderBg = new Color(0.13f, 0.13f, 0.17f);

    private GUIStyle bannerStyle;
    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private bool stylesInitialized;

    #endregion

    #region Foldout State

    private bool foldMovement = true;
    private bool foldBehavior = true;
    private bool foldNavMesh = true;
    private bool foldEmissive = false;
    private bool foldDebug = true;

    #endregion

    #region OnInspectorGUI

    public override void OnInspectorGUI()
    {
        InitStyles();
        serializedObject.Update();

        FloorBase floor = (FloorBase)target;

        DrawTypeBanner(floor);
        DrawStateBanner(floor);
        EditorGUILayout.Space(4);

        foldMovement = DrawSection("Movement", foldMovement, DrawMovementSection);
        foldBehavior = DrawSection("Behavior", foldBehavior, DrawBehaviorSection);
        foldNavMesh = DrawSection("NavMesh", foldNavMesh, DrawNavMeshSection);
        foldEmissive = DrawSection("Emissive", foldEmissive, DrawEmissiveSection);
        foldDebug = DrawSection("Debug", foldDebug, () => DrawDebugSection(floor));

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region Banners

    private void DrawTypeBanner(FloorBase floor)
    {
        bool isVertical = floor is VerticalFloor;
        string label = isVertical ? "VERTICAL FLOOR" : "HORIZONTAL FLOOR";
        string sub = isVertical
            ? "Child scales on Y  |  Layer changes on trigger  |  Parent collider fixed"
            : "Child scales to 0  |  Mesh hidden when triggered  |  Parent collider fixed";

        EditorGUILayout.BeginVertical(GetColorBox(ColorHeaderBg));
        GUILayout.Label(label, headerStyle);
        GUILayout.Label(sub, subHeaderStyle);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private void DrawStateBanner(FloorBase floor)
    {
        if (!Application.isPlaying) return;

        Color stateColor = floor.CurrentState switch
        {
            FloorBase.FloorState.Default => ColorDefault,
            FloorBase.FloorState.Triggered => ColorTriggered,
            FloorBase.FloorState.Transitioning => ColorTransitioning,
            _ => Color.gray
        };

        string stateLabel = floor.CurrentState switch
        {
            FloorBase.FloorState.Default => "DEFAULT",
            FloorBase.FloorState.Triggered => "TRIGGERED",
            FloorBase.FloorState.Transitioning => "TRANSITIONING",
            _ => "UNKNOWN"
        };

        var prev = GUI.color;
        GUI.color = stateColor;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label(stateLabel, bannerStyle);
        EditorGUILayout.EndVertical();
        GUI.color = prev;

        Repaint();
    }

    #endregion

    #region Section Drawers

    private void DrawMovementSection()
    {
        DrawProp("moveMode", "Move Mode");
        DrawProp("moveSpeed", "Speed");
        DrawProp("visualChild", "Visual Child");

        EditorGUILayout.HelpBox(
            "Visual Child is the only GameObject that moves or scales. " +
            "The parent stays fixed so the BoxCollider and NavMeshObstacle never change.",
            MessageType.Info);

        var modeP = serializedObject.FindProperty("moveMode");
        if (modeP != null && modeP.enumValueIndex == 1)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Translate Anchors", EditorStyles.boldLabel);
            DrawProp("defaultPoint", "Default Point");
            DrawProp("triggeredPoint", "Triggered Point");
        }

        if (target is VerticalFloor)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Vertical Settings", EditorStyles.boldLabel);
            DrawProp("expandedScaleMultiplier", "Expand Multiplier (Y)");
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Layer Settings", EditorStyles.boldLabel);
            DrawProp("defaultLayerName", "Default Layer");
            DrawProp("expandedLayerName", "Expanded Layer");
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Activation", EditorStyles.boldLabel);  
            DrawProp("objectToToggle", "Object To Toggle");                     
        }
        else if (target is HorizontalFloor)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Horizontal Settings", EditorStyles.boldLabel);
            DrawProp("contractionAxis", "Contract Axis");
            DrawProp("contractionDirection", "Contract Direction");
        }
    }

    private void DrawBehaviorSection()
    {
        DrawProp("initialDelay", "Initial Delay (s)");
        DrawProp("triggerProbability", "Trigger Probability");
        DrawProp("rerollInterval", "Re-roll Interval (s)");
        DrawProp("triggerDuration", "Triggered Duration (s)");
    }

    private void DrawNavMeshSection()
    {
        DrawProp("navMeshCarveDelay", "Carve Delay (s)");
        EditorGUILayout.HelpBox(
            "The NavMeshObstacle on the parent is sized to the BoxCollider and never changes. " +
            "It carves when triggered so AI cannot path through the space.",
            MessageType.Info);
    }

    private void DrawEmissiveSection()
    {
        DrawProp("emissiveRenderers", "Renderers");
        DrawProp("colorDefault", "Default Color");
        DrawProp("colorTriggered", "Triggered Color");
        DrawProp("colorTransitioning", "Transitioning Color");
    }

    private void DrawDebugSection(FloorBase floor)
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use debug controls.", MessageType.None);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Trigger", GUILayout.Height(26))) floor.ForceTrigger();
        if (GUILayout.Button("Force Reset", GUILayout.Height(26))) floor.ForceReset();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Axis:", floor.GizmoAxisLabel, EditorStyles.label);
    }

    #endregion

    #region Utility

    private bool DrawSection(string title, bool foldout, System.Action drawContents)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        foldout = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
        if (foldout)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Space(2);
            drawContents();
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
        return foldout;
    }

    private void DrawProp(string propName, string label)
    {
        var prop = serializedObject.FindProperty(propName);
        if (prop != null) EditorGUILayout.PropertyField(prop, new GUIContent(label));
    }

    private GUIStyle GetColorBox(Color bg)
    {
        var style = new GUIStyle(EditorStyles.helpBox);
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, bg);
        tex.Apply();
        style.normal.background = tex;
        return style;
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        bannerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter
        };

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft
        };
        headerStyle.normal.textColor = Color.white;

        subHeaderStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleLeft
        };
        subHeaderStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
    }

    #endregion
}
#endif