// using System.Collections.Generic;
// using System.Linq;
// using UnityEditor;
// using UnityEngine;

// // [CustomEditor(typeof(JsonAnimAsset))]
// public class AnimEventEditorWindow : Editor
// {
//     //REFERENCIAS INTERNAS
//     AnimJson.AnimJsonData animData;
//     AtlasJson.AtlasData atlasData;
//     Dictionary<string, AtlasJson.AtlasFrame> atlasLookup;

//     //IMPUTS DEL USUARIO
//     Texture2D spriteSheet;
//     TextAsset atlasJsonFile;
//     TextAsset animJsonFile;

//     JsonAnimAsset asset;

//     //REFERENCIAS DE APOYO
//     TextAsset lastLoadedAnimJson;

//     //VARIABLES DE NAVEGACION
//     bool UnsavedChanges = false;
//     int selectedDirectionIndex = 0;
//     int selectedFrameIndex = -1;
//     string[] directionKeys;
//     Vector2 framesScroll;

//     //FEEDBACK VISUAL TIMELINE
//     Color selectedFrameColor = new Color(0.3f, 0.8f, 1f);
//     Color eventIndicatorColor = new Color(1f, 0.6f, 0.1f);
//     Color previewBCcolor = new Color(0.15f, 0.15f, 0.15f, 1f);
//     const string EVENT_COLOR_PREF = "AninEventEditor_EventColor";

//     //PREVIEW ANIMADO
//     bool isPlaying = false;
//     double lastEditorTime;
//     float previewTimer = 0f;

    

//     [MenuItem("Tools/Animation/Json Anim Editor")]
//     public static void Open()
//     {
//         // GetWindow<AnimEventEditorWindow>("Json Anim Editor");
//     }

//     void OnEnable()
//     {
//         // LoadEventColor();
//         // EditorApplication.update += OnEditorUpdate;

//         asset = (JsonAnimAsset)target;

//         spriteSheet = asset.spriteSheet;
//         atlasJsonFile = asset.atlasJson;
//         animJsonFile = asset.animJson;

//         loadJson();
//         loadAtlas(); 
//     }

//     void OnDisable()
//     {
//         EditorApplication.update += OnEditorUpdate;
//     }

//     public override void OnInspectorGUI()
//     {
//         HandleKeyBoard();
//         DrawTopBar();
//         EditorGUILayout.Space(10);

//         if(animData == null)
//         {
//             EditorGUILayout.HelpBox("Carga un anim.json para editar", MessageType.Info);
//             return;
//         }

//         EditorGUILayout.BeginHorizontal();

//             /////IZQUIERDA
//             // EditorGUILayout.BeginVertical(GUILayout.Width((position.width * 0.6f)));
                
//                 DrawClipSelector();
//                 DrawClipInfo();
//                 EditorGUILayout.Space(10);

//                 DrawFramesTimeline();
//                 EditorGUILayout.Space(6);

//                 DrawFrameInspector();
//             // EditorGUILayout.EndVertical();

//         /////DERECHA
//             EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

//                 drawFramePreview();
//                 // EditorGUILayout.Space(12);

//             EditorGUILayout.EndVertical();
//         EditorGUILayout.EndHorizontal();
//     }

//     void loadJson()
//     {
//         if (animJsonFile == null)
//         {
//             Debug.LogWarning("No Anim.Json selected");

//             animData = null;
//             directionKeys = null;
//             return;
//         }

//         isPlaying = false;

//         animData = JsonUtility.FromJson<AnimJson.AnimJsonData>(animJsonFile.text);

//         if(animData == null || animData.anims == null)
//         {
//             Debug.LogError("Anim.json invalido o imcompatible");
//             animData = null;
//             directionKeys = null;
//             return;
//         }

//         selectedDirectionIndex = 0;
//         selectedFrameIndex = -1;

//         directionKeys = animData.anims.Select(a => a.key).ToArray();
//         UnsavedChanges = false;
//     }
//     void loadAtlas()
//     {
//         atlasData = null;
//         atlasLookup = null;

//         if (atlasJsonFile == null) return;
        
//         isPlaying = false;

//         atlasData = JsonUtility.FromJson<AtlasJson.AtlasData>(atlasJsonFile.text);

//         if(atlasData == null || atlasData.frames == null) return;

//         atlasLookup = new Dictionary<string, AtlasJson.AtlasFrame>();
//         foreach(var f in atlasData.frames)
//         {
//             if (!atlasLookup.ContainsKey(f.filename))
//             {
//                 atlasLookup.Add(f.filename, f);
//             }
//         }
//     }

//     void saveJson()
//     {
//         if (animData == null || animJsonFile == null)
//         {
//             Debug.Log("Nothing to save");
//             return;
//         } 

//         ApplyDataToAllDirections();
//         DuplicateEventsToAllDirections();

//         string json = JsonUtility.ToJson(animData, true);
//         string path = AssetDatabase.GetAssetPath(animJsonFile);

//         System.IO.File.WriteAllText(path, json);
//         AssetDatabase.Refresh();

//         UnsavedChanges = false;

//         EditorUtility.SetDirty(animJsonFile);
//         AssetDatabase.SaveAssets();

//         Debug.Log("AnimJson Saved");
//     }

//     void ApplyDataToAllDirections()
//     {
//         var sourceClip = animData.anims[selectedDirectionIndex];

//         for (int i = 0; i < animData.anims.Length; i++)
//         {
//             if(i == selectedDirectionIndex) continue;

//             animData.anims[i].frameRate = sourceClip.frameRate;
//             animData.anims[i].repeat = sourceClip.repeat;
//         }

//         Debug.Log("FrameRate y Repeat aplicado a todas las direcciones");
//     }

//     void ReloadFromDisc()
//     {
//         if (animJsonFile == null) return;

//         string path = AssetDatabase.GetAssetPath(animJsonFile);
//         string json = System.IO.File.ReadAllText(path);

//         animData = JsonUtility.FromJson<AnimJson.AnimJsonData>(json);

//         selectedDirectionIndex = 0;
//         selectedFrameIndex = -1;
//         directionKeys = animData.anims.Select(a => a.key).ToArray();

//         UnsavedChanges = false;
//         isPlaying = false;

//         Repaint();
//     }

//     bool hasValidClip()
//     {
//         if (animData == null) return false;
//         if (animData.anims == null) return false;
//         if (animData.anims.Length == 0) return false;
//         if (selectedDirectionIndex < 0) return false;
//         if (selectedDirectionIndex >= animData.anims.Length) return false;

//         return true;
//     }

//     void OnAnimJsonChanged()
//     {
//         if(animJsonFile == lastLoadedAnimJson) return;

//         if (UnsavedChanges)
//         {
//             int options = EditorUtility.DisplayDialogComplex("Cambios sin guardar", "Hay cambios sin guardar en el anim.json actual. \n\n ¿Deseas guardar?",
//             "Guardar y continuar", "Cancelar", "No guardar");

//             switch (options)
//             {
//                 case 0: saveJson(); break;
//                 case 1: animJsonFile = lastLoadedAnimJson; return;
//                 case 2: break;
//             }
//         }

//         lastLoadedAnimJson = animJsonFile;
//         loadJson();

//     }

//     void DrawTopBar()
//     {
//         string title = UnsavedChanges ? "Json Anim Editor *" : "Json Anim Editor";
//         // titleContent = new GUIContent(title);

//         EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
//             //// ANIM JSON
//             EditorGUI.BeginChangeCheck();
//             animJsonFile = (TextAsset)EditorGUILayout.ObjectField("Anim.json",animJsonFile, typeof(TextAsset), false, GUILayout.Width(300));
//             if (EditorGUI.EndChangeCheck())
//             {
//                 OnAnimJsonChanged();
//             }

//             //// ATLAS Y SPRITESHEET
//             EditorGUI.BeginChangeCheck();
//             atlasJsonFile = (TextAsset)EditorGUILayout.ObjectField("Atlas.json",atlasJsonFile, typeof(TextAsset), false, GUILayout.Width(300));
//             spriteSheet = (Texture2D)EditorGUILayout.ObjectField("Sprite Sheet",spriteSheet, typeof(Texture2D), false, GUILayout.Width(300));

//             //// CARGAR FRAMES DEL ATLAS
//             if(EditorGUI.EndChangeCheck()) 
//             {
//                 loadAtlas();
//             }
//         EditorGUILayout.EndHorizontal();

//         //// EVENT COLOR
//         EditorGUILayout.Space(4);
//         EditorGUI.BeginChangeCheck();
//         eventIndicatorColor = EditorGUILayout.ColorField(new GUIContent("Event Color"), eventIndicatorColor, GUILayout.Width(220));
//         if (EditorGUI.EndChangeCheck())
//         {
//             SaveEventColor();
//         }

//         EditorGUILayout.BeginHorizontal();
//             //// SAVE
//             GUI.enabled = animData != null && UnsavedChanges;
//             if(GUILayout.Button(UnsavedChanges?"Save all directions":"No changes to save", GUILayout.Width(400)))
//             {
//                 saveJson();
//             }
//             GUI.enabled = true;

//             //// RESET VALUES
//             GUI.enabled = animData != null && UnsavedChanges;
//             if(GUILayout.Button(UnsavedChanges? "Reset the last saved":"No changes to reset", GUILayout.Width(400)))
//             {
//                 bool confirm = EditorUtility.DisplayDialog("Revertir cambios", "Se perderan todos los cambios no guardados \n ¿Deseas continuar?",
//                 "Si", "Cancelar");

//                 if (confirm)
//                 {
//                     ReloadFromDisc();
//                 }
//             }
//             GUI.enabled = true;
            
//         EditorGUILayout.EndHorizontal();

//     }

//     void DrawClipSelector()
//     {
//         int newIndex = EditorGUILayout.Popup("Direction", selectedDirectionIndex, directionKeys);

//         if (newIndex != selectedDirectionIndex)
//         {
//             selectedDirectionIndex =  newIndex;
//             selectedFrameIndex = -1;
//         }
//     }

//     void DrawClipInfo()
//     {
//         if (!hasValidClip()) return;

//         var clip = animData.anims[selectedDirectionIndex];

//         EditorGUI.BeginChangeCheck();

//         clip.frameRate = EditorGUILayout.IntField("FPS", clip.frameRate);
//         // clip.repeat = EditorGUILayout.IntField("Repeat", clip.repeat);
//         bool repeatEnable = clip.repeat < 0;
//         EditorGUI.BeginChangeCheck();
//         repeatEnable = EditorGUILayout.Toggle("Is loop", repeatEnable);

//         if(EditorGUI.EndChangeCheck())
//         {
//             clip.repeat = repeatEnable? -1: 0;
//             UnsavedChanges = true;
//         }

//         EditorGUILayout.Space(4);

//         EditorGUILayout.BeginHorizontal();
//         GUILayout.FlexibleSpace();

//         if(GUILayout.Button("Apply to all directions", GUILayout.Width(180)))
//         {
//             ApplyDataToAllDirections();
//             UnsavedChanges = true;
//         }

//         EditorGUILayout.EndHorizontal();

//     }

//     void DrawFramesTimeline()
//     {
//         if (!hasValidClip()) return;

//         var clip = animData.anims[selectedDirectionIndex];

//         float timelineHeight = 100f;

//         framesScroll = EditorGUILayout.BeginScrollView(framesScroll, alwaysShowHorizontal: true, alwaysShowVertical: false, 
//             GUILayout.Height(timelineHeight));

//             EditorGUILayout.BeginHorizontal();

//             for(int i = 0; i < clip.frames.Count; i++)
//             {
//                 DrawFrameButton(i, clip.frames[i]);
//             }

//             EditorGUILayout.EndHorizontal();
//         EditorGUILayout.EndScrollView();
//     }

//     bool TryGetSelectedAtlasFrame (out AtlasJson.AtlasFrame atlasFrame)
//     {
//         atlasFrame = null;

//         if (!hasValidClip()) return false;
//         if (selectedFrameIndex < 0) return false;
//         if (atlasData == null) return false;
//         if (atlasLookup == null) return false;
 
//         var clip = animData.anims[selectedDirectionIndex];
//         if (selectedDirectionIndex >= clip.frames.Count) return false;

//         string frameName = clip.frames[selectedFrameIndex].frame;

//         if (string.IsNullOrEmpty(frameName)) return false;

//         return atlasLookup.TryGetValue(frameName, out atlasFrame);
//     }

//     void drawFramePreview()
//     {
//         drawPreviewControls();
//         EditorGUILayout.Space(6);

//         GUILayout.Label("Preview", EditorStyles.boldLabel);

//         // Rect previewRect = GUILayoutUtility.GetRect(350, 350, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

//         Rect previewRect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 20, 220);
//         //// BACKGROUND
//         EditorGUI.DrawRect(previewRect, previewBCcolor);
//         //// BC BORDER
//         EditorGUI.DrawRect(new Rect(previewRect.x, previewRect.y, previewRect.width, 1), new Color(0,0,0,0.4f));

//         if (selectedFrameIndex < 0)
//         {
//             // EditorGUILayout.HelpBox("Selecciona un frame para ver el preview", MessageType.Info);
//             EditorGUI.LabelField(previewRect, "Selecciona un frame para ver el preview", EditorStyles.centeredGreyMiniLabel);
//             return;
//         }
//         if (atlasJsonFile == null || spriteSheet == null)
//         {
//             // EditorGUILayout.HelpBox("Asigna un AtlasJson/SpriteSheet para ver el preview", MessageType.Info);
//             EditorGUI.LabelField(previewRect, "Preview no disponible", EditorStyles.centeredGreyMiniLabel);
//             return;
//         }
//         if (!TryGetSelectedAtlasFrame(out var atlasFrame))
//         {
//             // EditorGUILayout.HelpBox("No hay frame valido para preview", MessageType.None);
//             EditorGUI.LabelField(previewRect, "Frame no encontrado en atlas", EditorStyles.centeredGreyMiniLabel);
//             return;
//         }

//         DrawSpriteInRect(previewRect, atlasFrame);
//     }

//     void DrawSpriteInRect(Rect previewRect, AtlasJson.AtlasFrame atlasFrame)
//     {
//         Rect texCoords = new Rect(atlasFrame.frame.x / (float)spriteSheet.width,
//             1f - (atlasFrame.frame.y + atlasFrame.frame.h) / (float)spriteSheet.height, 
//             (float)atlasFrame.frame.w / spriteSheet.width, (float)atlasFrame.frame.h / spriteSheet.height);

//         float aspect = atlasFrame.frame.w / (float)atlasFrame.frame.h;
//         Rect drawRect = previewRect;

//         if (aspect > 1f)
//         {
//             drawRect.height /= aspect;
//             drawRect.y += (previewRect.height - drawRect.height) * 0.5f;
//         }
//         else
//         {
//             drawRect.width *= aspect;
//             drawRect.x += (previewRect.width - drawRect.width) * 0.5f;
//         }

//         GUI.DrawTextureWithTexCoords(drawRect, spriteSheet, texCoords);
//     }

//     void DrawFrameButton(int index, AnimJson.AnimFrameData frame)
//     {
//         bool isSelected = index == selectedFrameIndex;
//         bool hasEvents = frame.evnt != null && frame.evnt.Length > 0;

//         float size = 60f;

//         GUIStyle style = new GUIStyle(GUI.skin.button);
//         style.fixedHeight = size;
//         style.fixedWidth = size;
//         style.fontStyle = FontStyle.Bold;
//         style.alignment = TextAnchor.MiddleCenter;
//         style.normal.textColor = Color.white;

//         Rect rect = GUILayoutUtility.GetRect(style.fixedWidth, style.fixedHeight, GUILayout.ExpandWidth(false));

//         if(GUI.Button(rect, index.ToString(), style))
//         {
//             selectedFrameIndex = index;
//             Repaint();
//         }

//         drawEvenIndicator(hasEvents, rect);

//         if (isSelected)
//         {
//             drawSelectionBorder(rect);
//         }
//     }

//     void drawEvenIndicator(bool hasEvents, Rect frameRect)
//     {
//         if (!hasEvents) return;

//         Rect indicator = new Rect(frameRect.x + frameRect.width * 0.5f - 6, frameRect.yMax - 10, 12, 6);

//         EditorGUI.DrawRect(indicator, eventIndicatorColor);
//     }

//     void LoadEventColor()
//     {
//         if (EditorPrefs.HasKey(EVENT_COLOR_PREF))
//         {
//             ColorUtility.TryParseHtmlString(EditorPrefs.GetString(EVENT_COLOR_PREF), out eventIndicatorColor);
//         }
//     }
//     void SaveEventColor()
//     {
//         EditorPrefs.SetString(EVENT_COLOR_PREF, "#"+ ColorUtility.ToHtmlStringRGBA(eventIndicatorColor));
//     }

//     void drawSelectionBorder(Rect rect)
//     {
//         float thickness = 2f;
//         Color color = selectedFrameColor;

//         EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
//         EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
//         EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
//         EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);

        
//     }

//     void DrawFrameInspector()
//     {
//         if (!hasValidClip()) return;
//         if (selectedFrameIndex < 0) return;

//         var clip = animData.anims[selectedDirectionIndex];
//         var frame = clip.frames[selectedFrameIndex];

//         EditorGUILayout.LabelField($"Frame {selectedFrameIndex}", EditorStyles.boldLabel);

//         if(frame.evnt == null) frame.evnt = new string[0];

//         if (frame.evnt.Length == 0)
//         {
//             EditorGUILayout.HelpBox("Este frame no tiene eventos de animacion", MessageType.Info);
//         }
        
//         int removeIndex = -1;

//             for (int i = 0; i < frame.evnt.Length; i++)
//             {
//                 EditorGUILayout.BeginHorizontal();

//                     frame.evnt[i] = EditorGUILayout.TextField($"Event {i}", frame.evnt[i]);

//                     if(GUILayout.Button("X", GUILayout.Width(28)))
//                     {
//                         removeIndex = i;
//                     }

//                 EditorGUILayout.EndHorizontal();
//             }

//             if(removeIndex != -1)
//             {
//                 RemoveEventAt(ref frame.evnt, removeIndex);
//                 UnsavedChanges = true;
//                 Repaint();
//             }

//         if (GUILayout.Button("+ Add Event"))
//         {
//             AddEvent(ref frame.evnt);
//             UnsavedChanges = true;
//             Repaint();
//         }

//         EditorGUILayout.Space(6);
//         if(GUILayout.Button("Duplicate events to all directions"))
//         {
//             DuplicateEventsToAllDirections();
//             UnsavedChanges = true;
//         }
//     }

//     void AddEvent(ref string[] eventArray)
//     {
//         System.Array.Resize(ref eventArray, eventArray.Length + 1);
//         eventArray[eventArray.Length - 1] = "";
//     }
//     void RemoveEventAt(ref string[] eventArray, int index)
//     {
//         var list = eventArray.ToList();
//         list.RemoveAt(index);
//         eventArray = list.ToArray();
//     }

//     void DuplicateEventsToAllDirections()
//     {
//         if (!hasValidClip()) return;
//         if (selectedFrameIndex < 0) return;

//         isPlaying = false;

//         var sourceClip = animData.anims[selectedDirectionIndex];
//         var sourceFrame = sourceClip.frames[selectedFrameIndex];

//         // if(sourceFrame.evnt == null || sourceFrame.evnt.Length == 0) return;
//         string[] sourceEvents = sourceFrame.evnt ?? new string[0];

//         for (int i = 0; i < animData.anims.Length; i++)
//         {
//             if (i == selectedDirectionIndex) continue;

//             var targetClip = animData.anims[i];

//             if(selectedFrameIndex >= targetClip.frames.Count) continue;

//             targetClip.frames[selectedFrameIndex].evnt = (string[])sourceEvents.Clone();
//         }

//         Debug.Log("Events aplicados a todas las direcciones");
//     }

//     void OnEditorUpdate()
//     {
//         if(!isPlaying) return;
//         if(!hasValidClip()) return;

//         double time = EditorApplication.timeSinceStartup;
//         float delta = (float)(time - lastEditorTime);
//         lastEditorTime = time;

//         var clip = animData.anims[selectedDirectionIndex];
//         if (clip.frames == null || clip.frames.Count == 0) return;

//         previewTimer += delta;

//         float frameDuration = 1f / Mathf.Max(1, clip.frameRate);
//         int frameCount = clip.frames.Count;

//         int newFrame = Mathf.FloorToInt (previewTimer / frameDuration);

//         if (newFrame >= frameCount)
//         {
//             previewTimer = 0f;
//             newFrame = 0;
//         }
//         if (newFrame != selectedFrameIndex)
//         {
//             selectedFrameIndex = newFrame;
//             Repaint();
//         }
//     }

//     void drawPreviewControls()
//     {
//         EditorGUILayout.BeginHorizontal();

//         GUI.enabled = !isPlaying;
//         if (GUILayout.Button("◀◀", GUILayout.Width(32)))
//         {
//             StepFrame(-1);
//         }

//         GUI.enabled = true;

//         if (GUILayout.Button(isPlaying? "⏸ Pause" : "▶ Play", GUILayout.Width(80)))

//         {
//             isPlaying = !isPlaying;
//             lastEditorTime = EditorApplication.timeSinceStartup;
//         }

//         GUI.enabled = !isPlaying;

//         if (GUILayout.Button("▶▶", GUILayout.Width(32)))
//         {
//             StepFrame(1);
//         }

//         GUI.enabled = true;

//         EditorGUILayout.EndHorizontal();
//     }

//     void StepFrame(int dir)
//     {
//         if (!hasValidClip()) return;

//         var clip = animData.anims[selectedDirectionIndex];
//         if (clip.frames.Count == 0) return;

//         selectedFrameIndex += dir;

//         if (selectedFrameIndex < 0) selectedFrameIndex = clip.frames.Count - 1;
//         else if (selectedFrameIndex >= clip.frames.Count) selectedFrameIndex = 0;

//         previewTimer = selectedFrameIndex / (float)clip.frameRate;
//         Repaint();
//     }

//     void HandleKeyBoard()
//     {
//         Event e = Event.current;

//         if(e.type == EventType.KeyDown && e.keyCode == KeyCode.Space)
//         {
//             isPlaying = !isPlaying;
//             lastEditorTime = EditorApplication.timeSinceStartup;
//             e.Use();
//         }
//     }
// }
