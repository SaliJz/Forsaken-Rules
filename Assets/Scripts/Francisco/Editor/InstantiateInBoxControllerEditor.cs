using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(InstantiateInBoxController))]
public class InstantiateInBoxControllerEditor : Editor
{
    private SerializedProperty spawnModeProp;
    private SerializedProperty prefabListProp;
    private SerializedProperty spawnKeyProp;
    private SerializedProperty specificEntriesProp;

    private static readonly Color AccentSimple = new Color(0.23f, 0.58f, 0.89f, 1f);
    private static readonly Color AccentSpecific = new Color(0.24f, 0.74f, 0.55f, 1f);
    private static readonly Color CardBg = new Color(0.17f, 0.17f, 0.20f, 1f);
    private static readonly Color EntryBg = new Color(0.21f, 0.21f, 0.25f, 1f);
    private static readonly Color BorderColor = new Color(1f, 1f, 1f, 0.07f);
    private static readonly Color TextMuted = new Color(0.52f, 0.52f, 0.58f, 1f);
    private static readonly Color TextPrimary = new Color(0.90f, 0.90f, 0.93f, 1f);
    private static readonly Color DestroyRed = new Color(0.82f, 0.28f, 0.26f, 1f);
    private static readonly Color AddGreen = new Color(0.22f, 0.60f, 0.42f, 1f);

    private GUIStyle _sectionLabel;
    private GUIStyle _cardStyle;
    private GUIStyle _entryStyle;
    private GUIStyle _tabStyle;
    private GUIStyle _tabActiveSimple;
    private GUIStyle _tabActiveSpecific;
    private GUIStyle _spawnBtnStyle;
    private GUIStyle _iconBtnStyle;
    private GUIStyle _mutedLabel;
    private GUIStyle _boldLabel;
    private bool _stylesReady;

    private void OnEnable()
    {
        spawnModeProp = serializedObject.FindProperty("spawnMode");
        prefabListProp = serializedObject.FindProperty("prefabList");
        spawnKeyProp = serializedObject.FindProperty("spawnKey");
        specificEntriesProp = serializedObject.FindProperty("specificEntries");
    }

    private void EnsureStyles()
    {
        if (_stylesReady) return;

        _sectionLabel = new GUIStyle(EditorStyles.label)
        {
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            normal = { textColor = TextMuted }
        };

        _cardStyle = new GUIStyle
        {
            padding = new RectOffset(12, 12, 10, 10),
            margin = new RectOffset(0, 0, 4, 4),
            normal = { background = MakeTex(CardBg) }
        };

        _entryStyle = new GUIStyle
        {
            padding = new RectOffset(10, 10, 8, 8),
            margin = new RectOffset(0, 0, 3, 3),
            normal = { background = MakeTex(EntryBg) }
        };

        _tabStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(0, 0, 8, 8),
            normal = { textColor = TextMuted, background = MakeTex(CardBg) },
            hover = { textColor = TextPrimary, background = MakeTex(new Color(0.24f, 0.24f, 0.28f)) }
        };

        _tabActiveSimple = new GUIStyle(_tabStyle)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = MakeTex(AccentSimple) },
            hover = { textColor = Color.white, background = MakeTex(AccentSimple) }
        };

        _tabActiveSpecific = new GUIStyle(_tabStyle)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = MakeTex(AccentSpecific) },
            hover = { textColor = Color.white, background = MakeTex(AccentSpecific) }
        };

        _spawnBtnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(0, 0, 5, 5),
            normal = { textColor = Color.white, background = MakeTex(AccentSpecific) },
            hover = { textColor = Color.white, background = MakeTex(new Color(0.30f, 0.85f, 0.63f)) }
        };

        _iconBtnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(0, 0, 2, 2),
            normal = { textColor = TextMuted, background = MakeTex(new Color(0.24f, 0.24f, 0.28f)) },
            hover = { textColor = TextPrimary, background = MakeTex(new Color(0.30f, 0.30f, 0.35f)) }
        };

        _mutedLabel = new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            wordWrap = true,
            normal = { textColor = TextMuted }
        };

        _boldLabel = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = TextPrimary }
        };

        _stylesReady = true;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnsureStyles();

        var controller = (InstantiateInBoxController)target;
        bool isPlaying = Application.isPlaying;

        DrawTopBar();
        GUILayout.Space(8);
        DrawModeTabs();
        GUILayout.Space(10);

        var mode = (InstantiateInBoxController.SpawnMode)spawnModeProp.enumValueIndex;

        if (mode == InstantiateInBoxController.SpawnMode.Simple)
            DrawSimplePanel(controller, isPlaying);
        else
            DrawSpecificPanel(controller, isPlaying);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTopBar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("SPAWN CONTROLLER", _sectionLabel);
            GUILayout.FlexibleSpace();
            bool hasBox = ((InstantiateInBoxController)target).GetComponent<BoxCollider>() != null;
            var dot = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                normal = { textColor = hasBox ? AccentSpecific : DestroyRed }
            };
            GUILayout.Label(hasBox ? "BoxCollider OK" : "Missing BoxCollider", dot);
        }
        DrawLine(BorderColor);
    }

    private void DrawModeTabs()
    {
        int cur = spawnModeProp.enumValueIndex;
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Simple", cur == 0 ? _tabActiveSimple : _tabStyle)) spawnModeProp.enumValueIndex = 0;
            if (GUILayout.Button("Specific", cur == 1 ? _tabActiveSpecific : _tabStyle)) spawnModeProp.enumValueIndex = 1;
        }
        GUILayout.Space(3);
        GUILayout.Label(
            cur == 0
                ? "Cycles through the prefab list each time the key is pressed."
                : "Each entry has its own prefab, key, and spawn button. Switching prefab clears previous spawns.",
            _mutedLabel);
    }

    private void DrawSimplePanel(InstantiateInBoxController ctrl, bool isPlaying)
    {
        using (new EditorGUILayout.VerticalScope(_cardStyle))
        {
            SectionLabel("PREFAB LIST");
            GUILayout.Space(3);
            EditorGUILayout.PropertyField(prefabListProp, new GUIContent("Prefabs"), true);
            GUILayout.Space(8);
            SectionLabel("SPAWN KEY");
            GUILayout.Space(3);
            EditorGUILayout.PropertyField(spawnKeyProp, new GUIContent("Key"));
        }

        if (!isPlaying) return;

        GUILayout.Space(4);
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = AccentSimple;
        if (GUILayout.Button("Spawn Next", GUILayout.Height(30)))
            ctrl.SpawnNextSimpleItem();
        GUI.backgroundColor = prev;
    }

    private void DrawSpecificPanel(InstantiateInBoxController ctrl, bool isPlaying)
    {
        SectionLabel("ENTRIES");
        GUILayout.Space(4);

        int removeIndex = -1;

        for (int i = 0; i < specificEntriesProp.arraySize; i++)
        {
            SerializedProperty entry = specificEntriesProp.GetArrayElementAtIndex(i);
            SerializedProperty prefabProp = entry.FindPropertyRelative("prefab");
            SerializedProperty keyProp = entry.FindPropertyRelative("key");

            using (new EditorGUILayout.VerticalScope(_entryStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var idxStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 10,
                        fontStyle = FontStyle.Bold,
                        fixedWidth = 18,
                        normal = { textColor = TextMuted }
                    };
                    GUILayout.Label($"{i + 1}", idxStyle);

                    EditorGUILayout.PropertyField(prefabProp, GUIContent.none);

                    GUILayout.Space(4);

                    Color prev = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.30f, 0.18f, 0.18f);
                    if (GUILayout.Button("X", _iconBtnStyle, GUILayout.Width(24), GUILayout.Height(20)))
                        removeIndex = i;
                    GUI.backgroundColor = prev;
                }

                GUILayout.Space(5);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Key", new GUIStyle(EditorStyles.label) { fontSize = 11, normal = { textColor = TextMuted }, fixedWidth = 28 });
                    EditorGUILayout.PropertyField(keyProp, GUIContent.none);

                    if (isPlaying)
                    {
                        GUILayout.Space(6);
                        string btnLabel = prefabProp.objectReferenceValue != null
                            ? $"Spawn {prefabProp.objectReferenceValue.name}"
                            : "Spawn";

                        if (GUILayout.Button(btnLabel, _spawnBtnStyle, GUILayout.Height(24)))
                            ctrl.SpawnSpecificEntry(i);
                    }
                }
            }
        }

        if (removeIndex >= 0)
        {
            specificEntriesProp.DeleteArrayElementAtIndex(removeIndex);
        }

        GUILayout.Space(6);

        Color prevColor = GUI.backgroundColor;
        GUI.backgroundColor = AddGreen;
        if (GUILayout.Button("+ Add Entry", GUILayout.Height(26)))
        {
            specificEntriesProp.arraySize++;
            var newEntry = specificEntriesProp.GetArrayElementAtIndex(specificEntriesProp.arraySize - 1);
            newEntry.FindPropertyRelative("prefab").objectReferenceValue = null;
            newEntry.FindPropertyRelative("key").enumValueIndex = 0;
        }
        GUI.backgroundColor = prevColor;
    }

    private void SectionLabel(string text) =>
        GUILayout.Label(text, _sectionLabel);

    private void DrawLine(Color color, int px = 1)
    {
        Rect r = EditorGUILayout.GetControlRect(false, px);
        EditorGUI.DrawRect(r, color);
    }

    private static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}