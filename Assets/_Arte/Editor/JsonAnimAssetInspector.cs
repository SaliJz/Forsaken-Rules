using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(JsonAnimAsset))]
public class JsonAnimmAssetInspector : Editor
{
    #region Data & References
    AnimJson.AnimJsonData animData;
    AtlasJson.AtlasData atlasData;
    Dictionary<string, AtlasJson.AtlasFrame> atlasLookup;

    JsonAnimAsset asset;
    SerializedProperty spriteSheetProp;
    SerializedProperty atlasJsonProp;
    SerializedProperty animJsonProp;
    
    JsonAnimAsset lastAsset;
    #endregion

    #region Navigation & State
    bool UnsavedChanges = false;
    int selectedDirectionIndex = 0;
    int selectedFrameIndex = -1;
    string[] directionKeys;
    Vector2 framesScroll;

    bool isPlaying = false;
    double lastEditorTime;
    float previewTimer = 0f;
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
        
        // spriteSheet = asset.spriteSheet;
        // atlasJsonFile = asset.atlasJson;
        // animJsonFile = asset.animJson;

        spriteSheetProp = serializedObject.FindProperty("spriteSheet");
        atlasJsonProp = serializedObject.FindProperty("atlasJson");
        animJsonProp = serializedObject.FindProperty("animJson");

        LoadEventColor();

        loadJson();
        loadAtlas();

        EditorApplication.update += OnEditorUpdate;
        lastAsset = asset;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;

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

        if(animData == null) 
        {
            EditorGUILayout.HelpBox("AnimJson not loaded", MessageType.Info);
            return;
        }

        DrawMainEditor();

        if (GUI.changed)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }
    }
    void DrawMainEditor()
    {
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

            animData = null;
            directionKeys = null;
            return;
        }

        isPlaying = false;

        animData = JsonUtility.FromJson<AnimJson.AnimJsonData>(asset.animJson.text);

        if(animData?.anims == null)
        {
            Debug.LogError("Anim.json invalido o imcompatible");
            animData = null;
            directionKeys = null;
            return;
        }

        selectedDirectionIndex = 0;
        selectedFrameIndex = -1;
        directionKeys = animData.anims.Select(a => a.key).ToArray();
        UnsavedChanges = false;
    }
    void loadAtlas()
    {
        atlasData = null;
        atlasLookup = null;
        if (asset.atlasJson == null) return;
        
        isPlaying = false;
        atlasData = JsonUtility.FromJson<AtlasJson.AtlasData>(asset.atlasJson.text);

        if(atlasData?.frames == null) return;

        atlasLookup = new Dictionary<string, AtlasJson.AtlasFrame>();
        foreach(var f in atlasData.frames)
        {
            if (!atlasLookup.ContainsKey(f.filename))
            {
                atlasLookup.Add(f.filename, f);
            }
        }
    }
    void saveJson()
    {
        if (animData == null || asset.animJson == null)
        {
            Debug.Log("Nothing to save");
            return;
        } 

        ApplyDataToAllDirections();
        DuplicateEventsToAllDirections();

        string json = JsonUtility.ToJson(animData, true);
        string path = AssetDatabase.GetAssetPath(asset.animJson);

        System.IO.File.WriteAllText(path, json);
        AssetDatabase.Refresh();

        UnsavedChanges = false;
        EditorUtility.SetDirty(asset.animJson);
        AssetDatabase.SaveAssets();
        Debug.Log("AnimJson Saved");
    }
    void SaveAndRebuild()
    {
        saveJson();
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

        animData = JsonUtility.FromJson<AnimJson.AnimJsonData>(json);

        selectedDirectionIndex = 0;
        selectedFrameIndex = -1;
        directionKeys = animData.anims.Select(a => a.key).ToArray();

        UnsavedChanges = false;
        isPlaying = false;

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
            GUI.enabled = animData != null && UnsavedChanges;

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
        var clip = animData.anims[selectedDirectionIndex];

        EditorGUI.BeginChangeCheck();
        clip.frameRate = EditorGUILayout.IntField("FPS", clip.frameRate);
        bool repeatEnable = clip.repeat < 0;

        EditorGUI.BeginChangeCheck();
        repeatEnable = EditorGUILayout.Toggle("Is loop", repeatEnable);

        if(EditorGUI.EndChangeCheck())
        {
            clip.repeat = repeatEnable? -1: 0;
            UnsavedChanges = true;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if(GUILayout.Button("Apply to all directions", GUILayout.Width(180)))
            {
                ApplyDataToAllDirections();
                UnsavedChanges = true;
            }

        EditorGUILayout.EndHorizontal();

    }
    void ApplyDataToAllDirections()
    {
        var sourceClip = animData.anims[selectedDirectionIndex];
        for (int i = 0; i < animData.anims.Length; i++)
        {
            if(i == selectedDirectionIndex) continue;
            animData.anims[i].frameRate = sourceClip.frameRate;
            animData.anims[i].repeat = sourceClip.repeat;
        }

        Debug.Log("FrameRate y Repeat aplicado a todas las direcciones");
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
    void DrawSpriteInRect(Rect previewRect, AtlasJson.AtlasFrame atlasFrame)
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

        var clip = animData.anims[selectedDirectionIndex];

        float timelineHeight = 100f;

        framesScroll = EditorGUILayout.BeginScrollView(framesScroll, alwaysShowHorizontal: true, alwaysShowVertical: false, 
            GUILayout.Height(timelineHeight));

            EditorGUILayout.BeginHorizontal();

                for(int i = 0; i < clip.frames.Count; i++)
                {
                    DrawFrameButton(i, clip.frames[i]);
                }

            EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }
    void DrawFrameButton(int index, AnimJson.AnimFrameData frame)
    {
        bool isSelected = index == selectedFrameIndex;
        bool hasEvents = frame.evnt != null && frame.evnt.Length > 0;

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
        if (isSelected)
        {
            drawSelectionBorder(rect);
        }
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

        var clip = animData.anims[selectedDirectionIndex];
        var frame = clip.frames[selectedFrameIndex];
        EditorGUILayout.LabelField($"Frame {selectedFrameIndex}", EditorStyles.boldLabel);

        if(frame.evnt == null) frame.evnt = new string[0];
        if (frame.evnt.Length == 0)
        {
            EditorGUILayout.HelpBox("Este frame no tiene eventos de animacion", MessageType.Info);
        }
        
        int removeIndex = -1;
        for (int i = 0; i < frame.evnt.Length; i++)
        {
            EditorGUILayout.BeginHorizontal();

                frame.evnt[i] = EditorGUILayout.TextField($"Event {i}", frame.evnt[i]);
                if(GUILayout.Button("X", GUILayout.Width(28)))
                {
                    removeIndex = i;
                }

            EditorGUILayout.EndHorizontal();
        }

        if(removeIndex != -1)
        {
            RemoveEventAt(ref frame.evnt, removeIndex);
            UnsavedChanges = true;
            Repaint();
        }

        if (GUILayout.Button("+ Add Event"))
        {
            AddEvent(ref frame.evnt);
            UnsavedChanges = true;
            Repaint();
        }

        EditorGUILayout.Space(6);
        if(GUILayout.Button("Duplicate events to all directions"))
        {
            DuplicateEventsToAllDirections();
            UnsavedChanges = true;
        }
    }
    void DuplicateEventsToAllDirections()
    {
        if (!hasValidClip() || selectedFrameIndex < 0) return;

        isPlaying = false;
        var sourceClip = animData.anims[selectedDirectionIndex];
        var sourceFrame = sourceClip.frames[selectedFrameIndex];

        string[] sourceEvents = sourceFrame.evnt ?? new string[0];

        for (int i = 0; i < animData.anims.Length; i++)
        {
            if (i == selectedDirectionIndex) continue;

            var targetClip = animData.anims[i];
            if(selectedFrameIndex < targetClip.frames.Count)
            {
                targetClip.frames[selectedFrameIndex].evnt = (string[])sourceEvents.Clone();
            }
        }
        Debug.Log("Events aplicados a todas las direcciones");
    }
#endregion

#region  Helpers & Tools
    bool hasValidClip() => animData?.anims != null && selectedDirectionIndex >= 0 &&
        selectedDirectionIndex < animData.anims.Length;

     bool TryGetSelectedAtlasFrame (out AtlasJson.AtlasFrame atlasFrame)
    {
        atlasFrame = null;

        if (!hasValidClip() || selectedFrameIndex < 0 || atlasLookup == null) return false;
        // if (atlasData == null) return false;
 
        var clip = animData.anims[selectedDirectionIndex];
        // if (selectedDirectionIndex >= clip.frames.Count) return false;
        if(selectedFrameIndex >= clip.frames.Count) return false;

        return atlasLookup.TryGetValue(clip.frames[selectedFrameIndex].frame, out atlasFrame);
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

        var clip = animData.anims[selectedDirectionIndex];
        if (clip.frames == null || clip.frames.Count == 0) return;

        previewTimer += delta;
        float frameDuration = 1f / Mathf.Max(1, clip.frameRate);

        int newFrame = Mathf.FloorToInt (previewTimer / frameDuration);
        if (newFrame >= clip.frames.Count)
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
        var clip = animData.anims[selectedDirectionIndex];

        selectedFrameIndex = (int)Mathf.Repeat(selectedFrameIndex + dir, clip.frames.Count);

        // if (selectedFrameIndex < 0) selectedFrameIndex = clip.frames.Count - 1;
        // else if (selectedFrameIndex >= clip.frames.Count) selectedFrameIndex = 0;

        previewTimer = selectedFrameIndex / (float)clip.frameRate;
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
    void AddEvent(ref string[] eventArray)
    {
        System.Array.Resize(ref eventArray, eventArray.Length + 1);
        eventArray[eventArray.Length - 1] = "";
    }
    void RemoveEventAt(ref string[] eventArray, int index)
    {
        var list = eventArray.ToList();
        list.RemoveAt(index);
        eventArray = list.ToArray();
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
    #endregion
}

