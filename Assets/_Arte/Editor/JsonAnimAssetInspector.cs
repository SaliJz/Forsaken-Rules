using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(JsonAnimAsset))]
public class JsonAnimmAssetInspector : Editor
{
    #region Data & References
    // AnimParser.NormAnimFile animData;
    AtlasParser.AtlasFile atlasData;
    Dictionary<string, AtlasParser.AtlasFrame> atlasLookup;

    JsonAnimAsset asset;
    SerializedProperty spriteSheetProp;
    SerializedProperty atlasJsonProp;
    SerializedProperty animJsonProp;
    
    JsonAnimAsset lastAsset;
    #endregion

    #region Navigation & State
    bool UnsavedChanges => asset != null && EditorUtility.IsDirty(asset);
    int selectedDirectionIndex = 0;
    int selectedFrameIndex = -1;
    string[] directionKeys;
    List<string> mismatchedDirections = new List<string>();
    Vector2 framesScroll;

    bool isPlaying = false;
    double lastEditorTime;
    float previewTimer = 0f;

    System.DateTime lastJsonWriteTime;
    #endregion

    #region Preferences
    Color selectedFrameColor = new Color(0.3f, 0.8f, 1f);
    Color eventIndicatorColor = new Color(1f, 0.6f, 0.1f);
    Color previewBCcolor = new Color(0.15f, 0.15f, 0.15f, 1f);
    const string EVENT_COLOR_PREF = "AnimEventEditor_EventColor";
    #endregion

    #region Unity Lifecycle
    void OnEnable()
    {
        asset = (JsonAnimAsset)target;
        spriteSheetProp = serializedObject.FindProperty("spriteSheet");
        atlasJsonProp = serializedObject.FindProperty("atlasJson");
        animJsonProp = serializedObject.FindProperty("animJson");

        LoadEventColor();

        loadJson();
        loadAtlas();

        Undo.undoRedoPerformed += undoRedoPerfomed;
        EditorApplication.update += OnEditorUpdate;
        lastAsset = asset;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        Undo.undoRedoPerformed -= undoRedoPerfomed;

        if (UnsavedChanges)
        {
            int option = EditorUtility.DisplayDialogComplex("Unsaved Animation Changes",
            "There are unsaved changes in this animation. \n\nDo you want to save them?",
            "Save", "Discard","Cancel");

            switch (option)
            {
                case 0: //Save
                    SaveAndRebuild(); break;
                case 1: //Discard
                    ReloadFromDisc(); break;
                case 2: //Cancel
                    Selection.activeObject = lastAsset; break;
            }
        }
    }
    #endregion
    
    #region Core GUI
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        HandleKeyBoard();

        DrawTopBar();

        if (!asset.isBuilt)
        {
            DrawBuildOverlay();
            return;
        }

        EditorGUILayout.Space(10);

        if(asset.animData == null) 
        {
            EditorGUILayout.HelpBox("AnimJson not loaded", MessageType.Info);
            return;
        }

        CheckExternalFileChanges();

        DrawMainEditor();

        // if (GUI.changed)
        // {
        //     serializedObject.ApplyModifiedProperties();
        //     EditorUtility.SetDirty(asset);
        // }
    }
    void DrawMainEditor()
    {
        if (mismatchedDirections.Count > 0)
        {
            EditorGUILayout.HelpBox("Direcciones con cantidad de frames distinta al resto:\n" + string.Join("\n", mismatchedDirections) +
                "\n\nla logica puede ser inconsistente en estas direcciones.", MessageType.Warning);
            EditorGUILayout.Space(6);
        }

        Section("Direction Selection");
            DrawClipSelector();
            DrawClipInfo();
        EndSection();

        EditorGUILayout.Space(10);

        Section("Preview");
            DrawFramePreview();
        EndSection();
        EditorGUILayout.Space(10);

        Section("Timeline");
            DrawFramesTimeline();
        EndSection();

        EditorGUILayout.Space(10);
        Section("Event Editor");
            DrawFrameInspector();
        EndSection();
    }

#endregion 

#region Data Persistence
    void loadJson()
    {
        if (asset.animJson == null)
        {
            Debug.LogWarning("No Anim.Json selected");

            asset.animData = null;
            directionKeys = null;
            return;
        }

        isPlaying = false;

        bool needsLoad = asset.animData == null || asset.animData.directions == null || asset.animData.directions.Length == 0;

      if (needsLoad)
        {
            asset.animData = AnimParser.IsNormalize(asset.animJson) ?
                JsonUtility.FromJson<AnimParser.NormAnimFile>(asset.animJson.text) : 
                AnimParser.NormalizeAndWriteBack(asset.animJson);
        }

        if(asset.animData?.directions == null)
        {
            Debug.LogError("Anim.json invalido o imcompatible");
            asset.animData = null;
            directionKeys = null;
            return;
        }

        selectedDirectionIndex = 0;
        selectedFrameIndex = -1;
        directionKeys = asset.animData.directions.Select(d => d.direction).ToArray();

        ValidateFrameCounts();

        if(asset.animJson != null)
        {
            string path = AssetDatabase.GetAssetPath(asset.animJson);
            if (!string.IsNullOrEmpty(path))
            {
                lastJsonWriteTime = System.IO.File.GetLastWriteTime(path);
            }
        }
    }
    void loadAtlas()
    {
        atlasData = null;
        atlasLookup = null;
        if (asset.atlasJson == null) return;
        
        isPlaying = false;
        atlasData = JsonUtility.FromJson<AtlasParser.AtlasFile>(asset.atlasJson.text);

        if(atlasData?.frames == null) return;

        atlasLookup = new Dictionary<string, AtlasParser.AtlasFrame>();
        foreach(var f in atlasData.frames)
        {
            if (!atlasLookup.ContainsKey(f.filename))
            {
                atlasLookup.Add(f.filename, f);
            }
        }
    }
    // void saveJson()
    // {
    //     if (asset.animData == null || asset.animJson == null)
    //     {
    //         Debug.Log("Nothing to save");
    //         return;
    //     }

    //     string json = JsonUtility.ToJson(asset.animData, true);
    //     string path = AssetDatabase.GetAssetPath(asset.animJson);

    //     System.IO.File.WriteAllText(path, json);
    //     AssetDatabase.Refresh();

    //     UnsavedChanges = false;
    //     EditorUtility.SetDirty(asset.animJson);
    //     AssetDatabase.SaveAssets();
    //     Debug.Log("AnimJson Saved");
    // }
    void SaveAndRebuild()
    {
        // saveJson();
        JsonAnimAssetBuilder.SaveAnimJson(asset);
        JsonAnimAssetBuilder.Build(asset);
        serializedObject.Update();
        loadAtlas();
        loadJson();
        GUIUtility.ExitGUI();
    }
    void ReloadFromDisc()
    {
        if (asset.animJson == null) return;

        string path = AssetDatabase.GetAssetPath(asset.animJson);
        string json = System.IO.File.ReadAllText(path);

        asset.animData = JsonUtility.FromJson<AnimParser.NormAnimFile>(json);

        selectedDirectionIndex = 0;
        selectedFrameIndex = -1;
        directionKeys = asset.animData.directions.Select(d => d.direction).ToArray();

        EditorUtility.ClearDirty(asset);
        isPlaying = false;

        Repaint();
    }

    void CheckExternalFileChanges()
    {
        if (asset.animData == null || asset.animJson == null) return;

        string path = AssetDatabase.GetAssetPath(asset.animJson);
        if (string.IsNullOrEmpty(path)) return;

        var writeTime = System.IO.File.GetLastWriteTime(path);
        if (writeTime == lastJsonWriteTime) return;

        lastJsonWriteTime = writeTime;
        SyncFramesFromDisk(path);
    }

    void SyncFramesFromDisk(string path)
    {
        string json = System.IO.File.ReadAllText(path);
        var diskData = JsonUtility.FromJson<AnimParser.NormAnimFile>(json);

        if (diskData?.directions == null) return;

        foreach(var diskDir in diskData.directions)
        {
            var memDir = asset.animData.directions.FirstOrDefault(d => d.direction == diskDir.direction);
            if (memDir != null)
            {
                memDir.frames = diskDir.frames;
            }
        }

        ValidateFrameCounts();
        Repaint();
    }
 #endregion


#region Draw TopBar & Buttons
    void DrawTopBar()
    {
        EditorGUILayout.LabelField("Json Animation Editor", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        asset.id = EditorGUILayout.TextField("Anim ID", asset.id);
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(asset);
        }

        EditorGUILayout.Space(4);

        serializedObject.Update();
        EditorGUILayout.ObjectField(spriteSheetProp);
        EditorGUILayout.PropertyField(animJsonProp);
        EditorGUILayout.PropertyField(atlasJsonProp);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(6);

        if(asset.isBuilt) DrawSaveButtons();
    }
    void DrawSaveButtons()
    {
        EditorGUILayout.BeginHorizontal();
            GUI.enabled = asset.animData != null && UnsavedChanges;

            if(GUILayout.Button("Save Json + Rebuild Asset"))
            {
                SaveAndRebuild();
            }
            GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }
    void DrawBuildOverlay()
    {
        EditorGUILayout.Space();
        if(GUILayout.Button("Build Asset"))
        {
            JsonAnimAssetBuilder.Build(asset);
            loadAtlas();
            loadJson();
        }
    }
#endregion

#region Draw Anim Info
    void DrawClipSelector()
    {
        if (directionKeys == null || directionKeys.Length == 0)
        {
            EditorGUILayout.HelpBox("No hay direcciones cargadas", MessageType.Warning);
            return;
        }

        int newIndex = EditorGUILayout.Popup("Direction", selectedDirectionIndex, directionKeys);

        if (directionKeys != null && !isValidDirection(directionKeys[newIndex]))
        {
            EditorGUILayout.HelpBox($"Ojo: {directionKeys[newIndex]} no parece una dirección estandar (up, down, downleft, upright, etc)", MessageType.Warning);
        }

        if (newIndex != selectedDirectionIndex)
        {
            selectedDirectionIndex =  newIndex;
            selectedFrameIndex = -1;
        }
    }
    void DrawClipInfo()
    {
        if (!hasValidClip()) return;

        EditorGUI.BeginChangeCheck();
        asset.animData.frameRate = EditorGUILayout.IntField("FPS", asset.animData.frameRate);
        bool repeatEnable = asset.animData.repeat < 0;

        repeatEnable = EditorGUILayout.Toggle("Is loop", repeatEnable);

        if(EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(asset, "Change Animation Settings");
            asset.animData.repeat = repeatEnable? -1: 0;
            // UnsavedChanges = true;
            EditorUtility.SetDirty(asset);
        }
    }

#endregion
    
#region DrawFramePrev
    void DrawFramePreview()
    {
        drawPreviewControls();
        EditorGUILayout.Space(6);

        float width = Mathf.Min(EditorGUIUtility.currentViewWidth - 20, 350);
        EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            Rect previewRect = GUILayoutUtility.GetRect(width, width);
            previewRect.height = previewRect.width;
            
            GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
        
        //// BACKGROUND
        EditorGUI.DrawRect(previewRect, previewBCcolor);
        EditorGUI.DrawRect(new Rect(previewRect.x, previewRect.y, previewRect.width, 1), new Color(0,0,0,0.4f));

        if (!TryGetSelectedAtlasFrame(out var atlasFrame))
        {
            EditorGUI.LabelField(previewRect, "Frame no encontrado en atlas", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        DrawSpriteInRect(previewRect, atlasFrame);
    }
    void DrawSpriteInRect(Rect previewRect, AtlasParser.AtlasFrame atlasFrame)
    {
        Rect texCoords = new Rect(atlasFrame.frame.x / (float)asset.spriteSheet.width,
            1f - (atlasFrame.frame.y + atlasFrame.frame.h) / (float)asset.spriteSheet.height, 
            (float)atlasFrame.frame.w / asset.spriteSheet.width, (float)atlasFrame.frame.h / asset.spriteSheet.height);

        float aspect = atlasFrame.frame.w / (float)atlasFrame.frame.h;
        Rect drawRect = previewRect;

        if (aspect > 1f)
        {
            drawRect.height /= aspect;
            drawRect.y += (previewRect.height - drawRect.height) * 0.5f;
        }
        else
        {
            drawRect.width *= aspect;
            drawRect.x += (previewRect.width - drawRect.width) * 0.5f;
        }

        GUI.DrawTextureWithTexCoords(drawRect, asset.spriteSheet, texCoords);
    }
     void drawPreviewControls()
    {
        EditorGUILayout.BeginHorizontal();
            GUI.enabled = !isPlaying;
            if (GUILayout.Button("◀◀", GUILayout.Width(32)))
            {
                StepFrame(-1);
            }

            GUI.enabled = true;

            if (GUILayout.Button(isPlaying? "⏸ Pause" : "▶ Play", GUILayout.Width(80)))

            {
                isPlaying = !isPlaying;
                lastEditorTime = EditorApplication.timeSinceStartup;
            }

            GUI.enabled = !isPlaying;

            if (GUILayout.Button("▶▶", GUILayout.Width(32)))
            {
                StepFrame(1);
            }

            GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }
#endregion

#region Draw Timeline
    void DrawFramesTimeline()
    {
        if (!hasValidClip()) return;

        var frames = asset.animData.directions[selectedDirectionIndex].frames;
        float timelineHeight = 100f;

        framesScroll = EditorGUILayout.BeginScrollView(framesScroll, alwaysShowHorizontal: true, alwaysShowVertical: false, 
            GUILayout.Height(timelineHeight));

            EditorGUILayout.BeginHorizontal();

                for(int i = 0; i < frames.Length; i++)
                {
                    DrawFrameButton(i);
                }

            EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }
    void DrawFrameButton(int index)
    {
        bool isSelected = index == selectedFrameIndex;
        bool hasEvents = GetFrameEvents(index).Length > 0;

        float size = 60f;
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fixedHeight = size,
            fixedWidth = size,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        style.normal.textColor = Color.white;

        Rect rect = GUILayoutUtility.GetRect(style.fixedWidth, style.fixedHeight, GUILayout.ExpandWidth(false));
        if(GUI.Button(rect, index.ToString(), style))
        {
            selectedFrameIndex = index;
            Repaint();
        }

        drawEvenIndicator(hasEvents, rect);
        if (isSelected) drawSelectionBorder(rect);
    }
    void drawEvenIndicator(bool hasEvents, Rect frameRect)
    {
        if (!hasEvents) return;

        Rect indicator = new Rect(frameRect.x + frameRect.width * 0.5f - 6, frameRect.yMax - 10, 12, 6);
        EditorGUI.DrawRect(indicator, eventIndicatorColor);
    }
    void drawSelectionBorder(Rect rect)
    {
        float thickness = 2f;
        Color color = selectedFrameColor;

        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);

        
    }
#endregion

#region Event Editor
void DrawFrameInspector()
    {
        if (!hasValidClip()) return;
        if (selectedFrameIndex < 0) 
        {
            EditorGUILayout.HelpBox("Seleccione un frame para activar editor", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"Frame {selectedFrameIndex}", EditorStyles.boldLabel);

        string[] events = GetFrameEvents(selectedFrameIndex);
        if(events.Length == 0)
        {
            EditorGUILayout.HelpBox("Este frame no tiene eventos de animacion", MessageType.Info);
        }
        
        int removeIndex = -1;
        for (int i = 0; i < events.Length; i++)
        {
            EditorGUILayout.BeginHorizontal();

                string newValue = EditorGUILayout.TextField($"Event {i}", events[i]);
                if (newValue != events[i])
                {
                    Undo.RecordObject(asset, "New Animation Event");
                    events[i] = newValue;
                    SetFrameEvents(selectedFrameIndex, events);
                    // UnsavedChanges = true;
                    EditorUtility.SetDirty(asset);
                }

                if(GUILayout.Button("X", GUILayout.Width(28)))
                {
                    removeIndex = i;
                }

            EditorGUILayout.EndHorizontal();
        }

        if(removeIndex != -1)
        {
            Undo.RecordObject(asset, "Animation Event Deleted");
            var list = events.ToList();
            list.RemoveAt(removeIndex);
            SetFrameEvents(selectedFrameIndex, list.ToArray());
            // UnsavedChanges = true;
            EditorUtility.SetDirty(asset);
            Repaint();
        }

        if (GUILayout.Button("+ Add Event"))
        {
            Undo.RecordObject(asset, "Animation Event Added");
            var list = events.ToList();
            list.Add("");
            SetFrameEvents(selectedFrameIndex, list.ToArray());
            // UnsavedChanges = true;
            EditorUtility.SetDirty(asset);
            Repaint();
        }
    }
#endregion

#region  Helpers & Tools
    bool hasValidClip() => asset.animData?.directions != null && selectedDirectionIndex >= 0 &&
        selectedDirectionIndex < asset.animData.directions.Length;

     bool TryGetSelectedAtlasFrame (out AtlasParser.AtlasFrame atlasFrame)
    {
        atlasFrame = null;

        if (!hasValidClip() || selectedFrameIndex < 0 || atlasLookup == null) return false;

        var frames = asset.animData.directions[selectedDirectionIndex].frames;
        if (selectedFrameIndex >= frames.Length) return false;

        return atlasLookup.TryGetValue(frames[selectedFrameIndex], out atlasFrame);
    }
    bool isValidDirection(string dir)
    {
        string[] validDirections = {"up", "upleft", "upright", "left", "right", "down", "downleft", "downright"};
        return validDirections.Contains(dir);
    }
    void OnEditorUpdate()
    {
        if(!hasValidClip() || !isPlaying) return;

        double time = EditorApplication.timeSinceStartup;
        float delta = (float)(time - lastEditorTime);
        lastEditorTime = time;

        var frames = asset.animData.directions[selectedDirectionIndex].frames;
        if (frames == null || frames.Length == 0) return;

        previewTimer += delta;
        float frameDuration = 1f / Mathf.Max(1, asset.animData.frameRate);

        int newFrame = Mathf.FloorToInt (previewTimer / frameDuration);
        if (newFrame >= frames.Length)
        {
            previewTimer = 0f;
            newFrame = 0;
        }
        if (newFrame != selectedFrameIndex)
        {
            selectedFrameIndex = newFrame;
            Repaint();
        }
    }
    void StepFrame(int dir)
    {
        if (!hasValidClip()) return;
        var frames = asset.animData.directions[selectedDirectionIndex].frames;

        selectedFrameIndex = (int)Mathf.Repeat(selectedFrameIndex + dir, frames.Length);
        previewTimer = selectedFrameIndex / (float)asset.animData.frameRate;
        Repaint();
    }
    void HandleKeyBoard()
    {
        Event e = Event.current;

        if(e.type == EventType.KeyDown && e.keyCode == KeyCode.Space)
        {
            isPlaying = !isPlaying;
            lastEditorTime = EditorApplication.timeSinceStartup;
            e.Use();
        }
    }
    void LoadEventColor()
    {
        if (EditorPrefs.HasKey(EVENT_COLOR_PREF))
        {
            ColorUtility.TryParseHtmlString(EditorPrefs.GetString(EVENT_COLOR_PREF), out eventIndicatorColor);
        }
    }
    void SaveEventColor()
    {
        EditorPrefs.SetString(EVENT_COLOR_PREF, "#"+ ColorUtility.ToHtmlStringRGBA(eventIndicatorColor));
    }
    void Section(string Title)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(Title, EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
    }

    void EndSection()
    {
        EditorGUILayout.EndVertical();
    }

    string[] GetFrameEvents (int frameIndex)
    {
        if (asset.animData?.events == null) return new string[0];
        foreach (var e in asset.animData.events)
        {
            if (e.frameIndex == frameIndex) return e.keys?? new string[0];
        }
        return new string [0];
    }

    void SetFrameEvents (int frameIndex, string[] keys)
    {
        var list = asset.animData.events.ToList();
        int idx = list.FindIndex(e => e.frameIndex == frameIndex);

        if (keys.Length == 0)
        {
            if (idx != -1) list.RemoveAt(idx);
        }
        else if (idx != -1)
        {
            list[idx].keys = keys;
        }
        else
        {
            list.Add(new AnimParser.NormEvent { frameIndex = frameIndex, keys = keys});
        }

        asset.animData.events = list.ToArray();
    }

    void ValidateFrameCounts()
    {
        mismatchedDirections.Clear();
        if (asset.animData?.directions == null || asset.animData?.directions.Length == 0) return;

        int expectedCount = asset.animData.directions.GroupBy(d => d.frames.Length).
            OrderByDescending(g => g.Count()).
            First().Key;

        asset.animData.framecount = expectedCount;

        foreach (var dir in asset.animData.directions)
        {
            if (dir.frames.Length != expectedCount)
            {
                mismatchedDirections.Add($"{dir.direction} ({dir.frames.Length} frames, se esperaban {expectedCount}.)");
            }
        }
    }

    #endregion


    #region save tools

    void undoRedoPerfomed()
    {
        if (asset?.animData?.directions == null) 
        {
            directionKeys = null;
            Repaint();
            return;
        }
        directionKeys = asset.animData.directions.Select(d => d.direction).ToArray();
        if (selectedDirectionIndex >= directionKeys.Length) selectedDirectionIndex = 0;
        Repaint();
    }

    #endregion
}

