using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(TransitionInteractive), true)]
public class TransitionInteractiveEditor : Editor
{
    #region Estado del Editor

    private TransitionInteractive script;
    private int draggingIndex = -1;
    private string draggingType = "";
    private float maxTimelineWidth = 10f;

    #endregion

    #region Ciclo de Vida

    private void OnEnable()
    {
        script = (TransitionInteractive)target;
    }

    #endregion

    #region Inspector GUI

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        GUILayout.Space(15);
        EditorGUILayout.LabelField("LÍNEA DE TIEMPO INTERACTIVA", EditorStyles.boldLabel);
        GUILayout.Space(5);

        maxTimelineWidth = EditorGUILayout.FloatField("Duración Max Vista (Seg)", maxTimelineWidth);
        maxTimelineWidth = Mathf.Max(2f, maxTimelineWidth);

        Rect timelineRect = GUILayoutUtility.GetRect(10, 55, GUILayout.ExpandWidth(true));
        GUI.Box(timelineRect, "", EditorStyles.helpBox);

        Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
        for (int i = 0; i <= (int)maxTimelineWidth; i++)
        {
            float pct = i / maxTimelineWidth;
            float xPos = timelineRect.x + (timelineRect.width * pct);
            Handles.DrawLine(new Vector3(xPos, timelineRect.y), new Vector3(xPos, timelineRect.y + timelineRect.height));
            if (i % 2 == 0) GUI.Label(new Rect(xPos - 5, timelineRect.y + 40, 30, 15), $"{i}s", EditorStyles.miniLabel);
        }

        DrawKeyframeRow(timelineRect, 4, Color.green, "Jugador", script.PlayerNodes, (item) => item.timeTrigger);
        DrawKeyframeRow(timelineRect, 13, Color.yellow, "Plataforma", script.PlatformNodes, (item) => item.timeTrigger);
        DrawKeyframeRow(timelineRect, 22, Color.red, "Fade", script.FadeEvents, (item) => item.timeTrigger);
        DrawKeyframeRow(timelineRect, 31, Color.cyan, "Anim", script.AnimationEvents, (item) => item.timeTrigger);

        HandleTimelineDragging(timelineRect);

        GUILayout.Space(20);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Nodo Jugador (Verde)")) { Undo.RecordObject(script, "Add Player Node"); script.PlayerNodes.Add(new PlayerNode()); script.SortAllEvents(); }
        if (GUILayout.Button("+ Nodo Ascensor (Amarillo)")) { Undo.RecordObject(script, "Add Platform Node"); script.PlatformNodes.Add(new PlatformNode() { speed = 2f }); script.SortAllEvents(); }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Añadir Fade (Naranja)")) { Undo.RecordObject(script, "Add Fade"); script.FadeEvents.Add(new FadeEvent()); script.SortAllEvents(); }
        if (GUILayout.Button("+ Añadir Animación (Azul)")) { Undo.RecordObject(script, "Add Anim"); script.AnimationEvents.Add(new AnimationEvent()); script.SortAllEvents(); }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region Lógica Gráfica del Timeline

    private void DrawKeyframeRow<T>(Rect rect, float yOffset, Color color, string type, List<T> list, System.Func<T, float> getTime)
    {
        for (int i = 0; i < list.Count; i++)
        {
            float t = getTime(list[i]);
            float pct = t / maxTimelineWidth;
            float xPos = rect.x + (rect.width * pct);

            Rect keyRect = new Rect(xPos - 4, rect.y + yOffset, 8, 8);

            Handles.color = color;
            Handles.DrawSolidDisc(new Vector3(keyRect.x + 4, keyRect.y + 4, 0), Vector3.forward, 4f);

            if (draggingIndex == i && draggingType == type)
            {
                Handles.color = Color.white;
                Handles.DrawWireDisc(new Vector3(keyRect.x + 4, keyRect.y + 4, 0), Vector3.forward, 5.5f);
            }
        }
    }

    private void HandleTimelineDragging(Rect rect)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (CheckClickInRow(rect, 4, script.PlayerNodes, (item) => item.timeTrigger, out draggingIndex)) { draggingType = "Jugador"; e.Use(); }
            else if (CheckClickInRow(rect, 13, script.PlatformNodes, (item) => item.timeTrigger, out draggingIndex)) { draggingType = "Plataforma"; e.Use(); }
            else if (CheckClickInRow(rect, 22, script.FadeEvents, (item) => item.timeTrigger, out draggingIndex)) { draggingType = "Fade"; e.Use(); }
            else if (CheckClickInRow(rect, 31, script.AnimationEvents, (item) => item.timeTrigger, out draggingIndex)) { draggingType = "Anim"; e.Use(); }
        }
        else if (e.type == EventType.MouseDrag && draggingIndex != -1)
        {
            float mousePct = (e.mousePosition.x - rect.x) / rect.width;
            float newTime = Mathf.Clamp(mousePct * maxTimelineWidth, 0f, maxTimelineWidth);
            newTime = Mathf.Round(newTime * 10f) / 10f;

            Undo.RecordObject(script, "Drag Timeline Element");

            if (draggingType == "Jugador") { var item = script.PlayerNodes[draggingIndex]; item.timeTrigger = newTime; script.PlayerNodes[draggingIndex] = item; }
            else if (draggingType == "Plataforma") { var item = script.PlatformNodes[draggingIndex]; item.timeTrigger = newTime; script.PlatformNodes[draggingIndex] = item; }
            else if (draggingType == "Fade") { var item = script.FadeEvents[draggingIndex]; item.timeTrigger = newTime; script.FadeEvents[draggingIndex] = item; }
            else if (draggingType == "Anim") { var item = script.AnimationEvents[draggingIndex]; item.timeTrigger = newTime; script.AnimationEvents[draggingIndex] = item; }

            EditorUtility.SetDirty(script);
            e.Use();
        }
        else if (e.type == EventType.MouseUp)
        {
            if (draggingIndex != -1)
            {
                script.SortAllEvents();
                draggingIndex = -1;
                draggingType = "";
                e.Use();
            }
        }
    }

    private bool CheckClickInRow<T>(Rect rect, float yOffset, List<T> list, System.Func<T, float> getTime, out int index)
    {
        index = -1;
        Vector2 mouse = Event.current.mousePosition;
        for (int i = 0; i < list.Count; i++)
        {
            float t = getTime(list[i]);
            float pct = t / maxTimelineWidth;
            float xPos = rect.x + (rect.width * pct);
            Rect clickBox = new Rect(xPos - 6, rect.y + yOffset - 3, 12, 12);
            if (clickBox.Contains(mouse))
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    #endregion
}