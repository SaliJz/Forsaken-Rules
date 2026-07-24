#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AutoAim))]
public class AutoAimEditor : Editor
{
    #region Serialized Properties - Core

    SerializedProperty enableAutoAim;
    SerializedProperty onlyForGamepad;
    SerializedProperty autoAimRange;
    SerializedProperty autoAimAngle;
    SerializedProperty enemyLayer;
    SerializedProperty debugMode;

    #endregion

    #region Serialized Properties - Sticky

    SerializedProperty enableStickyTarget;
    SerializedProperty stickyTargetDuration;

    #endregion

    #region Serialized Properties - FX General

    SerializedProperty showTargetFX;
    SerializedProperty forceShowWithoutGamepad;
    SerializedProperty targetFXMode;

    #endregion

    #region Serialized Properties - Arrows3D

    SerializedProperty arrowColor;
    SerializedProperty arrowSize;
    SerializedProperty arrowDistance;
    SerializedProperty arrowOffset;
    SerializedProperty animateFX;
    SerializedProperty pulseSpeed;
    SerializedProperty pulseScale;
    SerializedProperty rotationSpeed;

    #endregion

    #region Serialized Properties - FIFA

    SerializedProperty fifaMarkerMode;
    SerializedProperty fifaCustomSprite;
    SerializedProperty fifaSpriteApplyColor;
    SerializedProperty fifaHeightAboveEnemy;
    SerializedProperty fifaArrowSize;
    SerializedProperty fifaBobEnabled;
    SerializedProperty fifaBobSpeed;
    SerializedProperty fifaBobAmount;
    SerializedProperty fifaBobCurve;
    SerializedProperty fifaColorCycleEnabled;
    SerializedProperty fifaColorCycleSpeed;
    SerializedProperty fifaColorGradient;
    SerializedProperty fifaStaticColor;
    SerializedProperty fifaOpacityPulseEnabled;
    SerializedProperty fifaOpacityPulseSpeed;
    SerializedProperty fifaOpacityMin;
    SerializedProperty fifaOpacityMax;
    SerializedProperty fifaSelfRotationSpeed;
    SerializedProperty fifaBillboardToCamera;

    #endregion

    #region Foldout State

    private bool foldCore = true;
    private bool foldSticky = false;
    private bool foldFXGen = true;
    private bool foldArrows = true;
    private bool foldFIFA = true;

    #endregion

    #region Styles

    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle sectionBoxStyle;
    private GUIStyle warningStyle;
    private bool stylesReady = false;

    private static readonly Color HeaderColorCore = new Color(0.25f, 0.45f, 0.75f);
    private static readonly Color HeaderColorFX = new Color(0.35f, 0.65f, 0.45f);
    private static readonly Color HeaderColorArrows = new Color(0.75f, 0.35f, 0.35f);
    private static readonly Color HeaderColorFIFA = new Color(0.75f, 0.60f, 0.20f);
    private static readonly Color HeaderColorSticky = new Color(0.55f, 0.35f, 0.70f);

    #endregion

    #region OnEnable

    private void OnEnable()
    {
        enableAutoAim = serializedObject.FindProperty("enableAutoAim");
        onlyForGamepad = serializedObject.FindProperty("onlyForGamepad");
        autoAimRange = serializedObject.FindProperty("autoAimRange");
        autoAimAngle = serializedObject.FindProperty("autoAimAngle");
        enemyLayer = serializedObject.FindProperty("enemyLayer");
        debugMode = serializedObject.FindProperty("debugMode");

        enableStickyTarget = serializedObject.FindProperty("enableStickyTarget");
        stickyTargetDuration = serializedObject.FindProperty("stickyTargetDuration");

        showTargetFX = serializedObject.FindProperty("showTargetFX");
        forceShowWithoutGamepad = serializedObject.FindProperty("forceShowWithoutGamepad");
        targetFXMode = serializedObject.FindProperty("targetFXMode");

        arrowColor = serializedObject.FindProperty("arrowColor");
        arrowSize = serializedObject.FindProperty("arrowSize");
        arrowDistance = serializedObject.FindProperty("arrowDistance");
        arrowOffset = serializedObject.FindProperty("arrowOffset");
        animateFX = serializedObject.FindProperty("animateFX");
        pulseSpeed = serializedObject.FindProperty("pulseSpeed");
        pulseScale = serializedObject.FindProperty("pulseScale");
        rotationSpeed = serializedObject.FindProperty("rotationSpeed");

        fifaMarkerMode = serializedObject.FindProperty("fifaMarkerMode");
        fifaCustomSprite = serializedObject.FindProperty("fifaCustomSprite");
        fifaSpriteApplyColor = serializedObject.FindProperty("fifaSpriteApplyColor");
        fifaHeightAboveEnemy = serializedObject.FindProperty("fifaHeightAboveEnemy");
        fifaArrowSize = serializedObject.FindProperty("fifaArrowSize");
        fifaBobEnabled = serializedObject.FindProperty("fifaBobEnabled");
        fifaBobSpeed = serializedObject.FindProperty("fifaBobSpeed");
        fifaBobAmount = serializedObject.FindProperty("fifaBobAmount");
        fifaBobCurve = serializedObject.FindProperty("fifaBobCurve");
        fifaColorCycleEnabled = serializedObject.FindProperty("fifaColorCycleEnabled");
        fifaColorCycleSpeed = serializedObject.FindProperty("fifaColorCycleSpeed");
        fifaColorGradient = serializedObject.FindProperty("fifaColorGradient");
        fifaStaticColor = serializedObject.FindProperty("fifaStaticColor");
        fifaOpacityPulseEnabled = serializedObject.FindProperty("fifaOpacityPulseEnabled");
        fifaOpacityPulseSpeed = serializedObject.FindProperty("fifaOpacityPulseSpeed");
        fifaOpacityMin = serializedObject.FindProperty("fifaOpacityMin");
        fifaOpacityMax = serializedObject.FindProperty("fifaOpacityMax");
        fifaSelfRotationSpeed = serializedObject.FindProperty("fifaSelfRotationSpeed");
        fifaBillboardToCamera = serializedObject.FindProperty("fifaBillboardToCamera");
    }

    #endregion

    #region Style Init

    private void EnsureStyles()
    {
        if (stylesReady) return;

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(6, 4, 2, 2)
        };

        subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft
        };

        sectionBoxStyle = new GUIStyle("box")
        {
            padding = new RectOffset(10, 10, 6, 8),
            margin = new RectOffset(0, 0, 2, 4)
        };

        warningStyle = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 11,
            richText = true
        };

        stylesReady = true;
    }

    #endregion

    #region OnInspectorGUI

    public override void OnInspectorGUI()
    {
        EnsureStyles();
        serializedObject.Update();

        DrawBanner();
        EditorGUILayout.Space(4);

        DrawCoreSection();
        EditorGUILayout.Space(2);

        DrawStickySection();
        EditorGUILayout.Space(2);

        DrawFXGeneralSection();
        EditorGUILayout.Space(2);

        AutoAim.FXMode currentMode = (AutoAim.FXMode)targetFXMode.enumValueIndex;

        if (currentMode == AutoAim.FXMode.Arrows3D)
            DrawArrows3DSection();
        else
            DrawFIFASection();

        EditorGUILayout.Space(4);
        DrawStatusBar();

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region Banner

    private void DrawBanner()
    {
        Rect bannerRect = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(bannerRect, new Color(0.12f, 0.12f, 0.18f, 1f));

        GUIStyle bannerLabel = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.85f, 0.90f, 1.0f) }
        };

        EditorGUI.LabelField(bannerRect, "Auto Aim System", bannerLabel);
    }

    #endregion

    #region Section: Core

    private void DrawCoreSection()
    {
        foldCore = DrawSectionHeader("Core Auto-Aim", foldCore, HeaderColorCore);
        if (!foldCore) return;

        using (new EditorGUILayout.VerticalScope(sectionBoxStyle))
        {
            EditorGUILayout.PropertyField(enableAutoAim, new GUIContent("Enable Auto-Aim"));
            EditorGUILayout.PropertyField(onlyForGamepad, new GUIContent("Only For Gamepad"));

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(autoAimRange, new GUIContent("Detection Range"));
            EditorGUILayout.PropertyField(autoAimAngle, new GUIContent("Detection Angle"));
            EditorGUILayout.PropertyField(enemyLayer, new GUIContent("Enemy Layer"));

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(debugMode, new GUIContent("Debug Gizmos"));
        }
    }

    #endregion

    #region Section: Sticky

    private void DrawStickySection()
    {
        foldSticky = DrawSectionHeader("Sticky Aim", foldSticky, HeaderColorSticky);
        if (!foldSticky) return;

        using (new EditorGUILayout.VerticalScope(sectionBoxStyle))
        {
            EditorGUILayout.PropertyField(enableStickyTarget, new GUIContent("Enable Sticky Target"));

            using (new EditorGUI.DisabledGroupScope(!enableStickyTarget.boolValue))
            {
                EditorGUILayout.PropertyField(stickyTargetDuration, new GUIContent("Sticky Duration (s)"));
            }
        }
    }

    #endregion

    #region Section: FX General

    private void DrawFXGeneralSection()
    {
        foldFXGen = DrawSectionHeader("Target FX \x97 General", foldFXGen, HeaderColorFX);
        if (!foldFXGen) return;

        using (new EditorGUILayout.VerticalScope(sectionBoxStyle))
        {
            EditorGUILayout.PropertyField(showTargetFX, new GUIContent("Show Target FX"));

            bool gamepadOnly = onlyForGamepad.boolValue;
            EditorGUILayout.PropertyField(forceShowWithoutGamepad, new GUIContent("Force Show (No Gamepad)", "Shows FX even without a connected gamepad. Useful for editor preview or KB+M."));

            if (gamepadOnly && forceShowWithoutGamepad.boolValue)
                EditorGUILayout.HelpBox("FX will be visible even without a gamepad because Force Show is enabled.", MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(targetFXMode, new GUIContent("FX Mode"));
        }
    }

    #endregion

    #region Section: Arrows 3D

    private void DrawArrows3DSection()
    {
        foldArrows = DrawSectionHeader("Mode 1 \x97 Arrows 3D", foldArrows, HeaderColorArrows);
        if (!foldArrows) return;

        using (new EditorGUILayout.VerticalScope(sectionBoxStyle))
        {
            EditorGUILayout.LabelField("Appearance", subHeaderStyle);
            EditorGUILayout.PropertyField(arrowColor, new GUIContent("Arrow Color"));
            EditorGUILayout.PropertyField(arrowSize, new GUIContent("Arrow Size"));
            EditorGUILayout.PropertyField(arrowDistance, new GUIContent("Orbit Distance"));
            EditorGUILayout.PropertyField(arrowOffset, new GUIContent("Vertical Offset"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Animation", subHeaderStyle);
            EditorGUILayout.PropertyField(animateFX, new GUIContent("Enable Animation"));

            using (new EditorGUI.DisabledGroupScope(!animateFX.boolValue))
            {
                EditorGUILayout.PropertyField(pulseSpeed, new GUIContent("Pulse Speed"));
                EditorGUILayout.PropertyField(pulseScale, new GUIContent("Pulse Scale"));
                EditorGUILayout.PropertyField(rotationSpeed, new GUIContent("Spin Speed (/s)"));
            }
        }
    }

    #endregion

    #region Section: FIFA

    private void DrawFIFASection()
    {
        foldFIFA = DrawSectionHeader("Mode 2 \x97 FIFA Style", foldFIFA, HeaderColorFIFA);
        if (!foldFIFA) return;

        AutoAim.FIFAMarkerMode markerMode = (AutoAim.FIFAMarkerMode)fifaMarkerMode.enumValueIndex;

        using (new EditorGUILayout.VerticalScope(sectionBoxStyle))
        {
            EditorGUILayout.LabelField("Marker", subHeaderStyle);
            EditorGUILayout.PropertyField(fifaMarkerMode, new GUIContent("Marker Mode"));

            if (markerMode == AutoAim.FIFAMarkerMode.Sprite)
            {
                EditorGUILayout.PropertyField(fifaCustomSprite, new GUIContent("Custom Sprite"));

                if (fifaCustomSprite.objectReferenceValue == null)
                    EditorGUILayout.HelpBox("Assign a Sprite to use as the marker.", MessageType.Warning);

                EditorGUILayout.PropertyField(fifaSpriteApplyColor, new GUIContent("Apply Color Tint", "If enabled, color cycle and opacity pulse will be applied on top of the sprite."));
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Position & Size", subHeaderStyle);
            EditorGUILayout.PropertyField(fifaHeightAboveEnemy, new GUIContent("Height Above Enemy"));
            EditorGUILayout.PropertyField(fifaArrowSize, new GUIContent("Marker Size"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Billboard & Rotation", subHeaderStyle);
            EditorGUILayout.PropertyField(fifaBillboardToCamera, new GUIContent("Billboard To Camera"));
            EditorGUILayout.PropertyField(fifaSelfRotationSpeed, new GUIContent("Self Rotation (/s)"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Bob Animation", subHeaderStyle);
            EditorGUILayout.PropertyField(fifaBobEnabled, new GUIContent("Enable Bob"));

            using (new EditorGUI.DisabledGroupScope(!fifaBobEnabled.boolValue))
            {
                EditorGUILayout.PropertyField(fifaBobSpeed, new GUIContent("Bob Speed"));
                EditorGUILayout.PropertyField(fifaBobAmount, new GUIContent("Bob Amount"));
                EditorGUILayout.PropertyField(fifaBobCurve, new GUIContent("Bob Curve"));
            }

            bool showColorOptions = markerMode == AutoAim.FIFAMarkerMode.Actual || fifaSpriteApplyColor.boolValue;

            if (showColorOptions)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Color", subHeaderStyle);
                EditorGUILayout.PropertyField(fifaColorCycleEnabled, new GUIContent("Enable Color Cycle"));

                if (fifaColorCycleEnabled.boolValue)
                {
                    EditorGUILayout.PropertyField(fifaColorCycleSpeed, new GUIContent("Cycle Speed"));
                    EditorGUILayout.PropertyField(fifaColorGradient, new GUIContent("Color Gradient"));
                }
                else
                {
                    EditorGUILayout.PropertyField(fifaStaticColor, new GUIContent("Static Color"));
                }
            }

            bool showOpacityOptions = markerMode == AutoAim.FIFAMarkerMode.Actual || fifaSpriteApplyColor.boolValue;

            if (showOpacityOptions)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Opacity Pulse", subHeaderStyle);
                EditorGUILayout.PropertyField(fifaOpacityPulseEnabled, new GUIContent("Enable Opacity Pulse"));

                using (new EditorGUI.DisabledGroupScope(!fifaOpacityPulseEnabled.boolValue))
                {
                    EditorGUILayout.PropertyField(fifaOpacityPulseSpeed, new GUIContent("Pulse Speed"));

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Opacity Range");
                    EditorGUILayout.PropertyField(fifaOpacityMin, GUIContent.none, GUILayout.Width(50));
                    EditorGUILayout.LabelField("-", GUILayout.Width(18));
                    EditorGUILayout.PropertyField(fifaOpacityMax, GUIContent.none, GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();

                    if (fifaOpacityMin.floatValue > fifaOpacityMax.floatValue)
                        EditorGUILayout.HelpBox("Opacity Min is greater than Opacity Max.", MessageType.Warning);
                }
            }
        }
    }

    #endregion

    #region Status Bar

    private void DrawStatusBar()
    {
        AutoAim comp = (AutoAim)target;
        Transform currentTarget = comp.GetCurrentTarget();

        Color barColor = currentTarget != null
            ? new Color(0.15f, 0.45f, 0.20f, 1f)
            : new Color(0.30f, 0.15f, 0.15f, 1f);

        Rect barRect = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(barRect, barColor);

        GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            fontSize = 11
        };

        string statusText = currentTarget != null
            ? $"Target locked: {currentTarget.name}"
            : "No target";

        EditorGUI.LabelField(barRect, statusText, statusStyle);

        if (Application.isPlaying)
            Repaint();
    }

    #endregion

    #region Section Header Helper

    private bool DrawSectionHeader(string label, bool expanded, Color color)
    {
        Rect rect = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, color * (EditorGUIUtility.isProSkin ? 0.6f : 0.75f));

        GUIStyle foldStyle = new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 11,
            normal = { textColor = Color.white },
            onNormal = { textColor = Color.white },
            focused = { textColor = Color.white },
            onFocused = { textColor = Color.white },
            active = { textColor = Color.white },
            onActive = { textColor = Color.white }
        };

        rect.xMin += 6;
        return EditorGUI.Foldout(rect, expanded, label, true, foldStyle);
    }

    #endregion
}
#endif