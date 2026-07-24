#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BoardManager))]
public class BoardManagerEditor : Editor
{
    private BoardManager board;
    private bool paintValue = true;
    private bool isDragging = false;

    private const float CellSize = 20f;
    private const float CellPadding = 2f;

    private void OnEnable()
    {
        board = (BoardManager)target;
        EnsureGridReady();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawVisualizationMode();
        EditorGUILayout.Space(8);
        DrawGridSettings();
        EditorGUILayout.Space(8);
        DrawGridPainter();
        EditorGUILayout.Space(8);
        DrawGridActions();
        EditorGUILayout.Space(8);
        DrawGameplaySettings();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(board);
            SceneView.RepaintAll();
        }
    }

    private void DrawVisualizationMode()
    {
        EditorGUILayout.LabelField("Visualization", EditorStyles.boldLabel);

        var modeProp = serializedObject.FindProperty("visualizationMode");
        EditorGUILayout.PropertyField(modeProp);

        BoardManager.VisualizationMode mode = (BoardManager.VisualizationMode)modeProp.enumValueIndex;

        EditorGUI.indentLevel++;

        if (mode == BoardManager.VisualizationMode.Gizmos)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoColorActive"), new GUIContent("Active"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoColorBlocked"), new GUIContent("Blocked"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoColorReserved"), new GUIContent("Reserved"));
        }
        else if (mode == BoardManager.VisualizationMode.TranslucentMesh)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("meshColorPrimary"), new GUIContent("Primary Color (Even)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("meshColorSecondary"), new GUIContent("Secondary Color (Odd)"));
        }

        EditorGUI.indentLevel--;
    }

    private void DrawGridSettings()
    {
        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        int newWidth = EditorGUILayout.IntSlider("Width", board.gridWidth, 1, 50);
        int newHeight = EditorGUILayout.IntSlider("Height", board.gridHeight, 1, 50);
        float newTileW = EditorGUILayout.Slider("Tile Width", board.tileWidth, 0.1f, 20f);
        float newTileH = EditorGUILayout.Slider("Tile Height", board.tileHeight, 0.1f, 20f);
        float newPadding = EditorGUILayout.Slider("Padding", board.tilePadding, 0f, 5f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(board, "Resize Board Grid");
            board.gridWidth = newWidth;
            board.gridHeight = newHeight;
            board.tileWidth = newTileW;
            board.tileHeight = newTileH;
            board.tilePadding = newPadding;
            board.ResizeGrid();
            EditorUtility.SetDirty(board);
            SceneView.RepaintAll();
        }

        EditorGUILayout.LabelField($"Active Tiles: {CountActiveCells()} / {board.gridWidth * board.gridHeight}",
            EditorStyles.miniLabel);
    }

    private void DrawGridPainter()
    {
        EditorGUILayout.LabelField("Grid Painter  (Click: toggle · Drag: paint)", EditorStyles.boldLabel);

        EnsureGridReady();

        float totalWidth = board.gridWidth * (CellSize + CellPadding) + CellPadding;
        float totalHeight = board.gridHeight * (CellSize + CellPadding) + CellPadding;

        Rect gridRect = GUILayoutUtility.GetRect(totalWidth, totalHeight);

        float indent = Mathf.Max(0f, (EditorGUIUtility.currentViewWidth - totalWidth) * 0.5f);
        gridRect.x += indent;

        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(new Rect(gridRect.x - 3, gridRect.y - 3, totalWidth + 6, totalHeight + 6),
                new Color(0.12f, 0.12f, 0.12f));

        for (int x = 0; x < board.gridWidth; x++)
        {
            for (int z = 0; z < board.gridHeight; z++)
            {
                int drawZ = board.gridHeight - 1 - z;

                Rect cellRect = new Rect(
                    gridRect.x + CellPadding + x * (CellSize + CellPadding),
                    gridRect.y + CellPadding + drawZ * (CellSize + CellPadding),
                    CellSize,
                    CellSize
                );

                bool active = board.GetCell(x, z);

                EditorGUI.DrawRect(cellRect, active
                    ? new Color(0.2f, 0.75f, 0.55f)
                    : new Color(0.22f, 0.22f, 0.22f));

                if (active && CellSize >= 20f)
                {
                    GUIStyle tiny = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 8,
                        alignment = TextAnchor.LowerRight,
                        normal = { textColor = new Color(0f, 0f, 0f, 0.45f) }
                    };
                    GUI.Label(cellRect, $"{x},{z}", tiny);
                }

                HandleCellInput(cellRect, x, z, active);
            }
        }

        if (Event.current.type == EventType.MouseUp)
            isDragging = false;
    }

    private void HandleCellInput(Rect cellRect, int x, int z, bool currentValue)
    {
        Event e = Event.current;

        if (!cellRect.Contains(e.mousePosition)) return;

        if (e.type == EventType.MouseDown)
        {
            isDragging = true;
            paintValue = !currentValue;

            Undo.RecordObject(board, "Paint Board Cell");
            board.SetCell(x, z, paintValue);
            EditorUtility.SetDirty(board);
            SceneView.RepaintAll();
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && isDragging)
        {
            if (board.GetCell(x, z) != paintValue)
            {
                Undo.RecordObject(board, "Paint Board Cell");
                board.SetCell(x, z, paintValue);
                EditorUtility.SetDirty(board);
                SceneView.RepaintAll();
            }
            e.Use();
        }

        if (e.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(cellRect, new Color(1f, 1f, 1f, 0.06f));
            Repaint();
        }
    }

    private void DrawGridActions()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Fill All"))
        {
            Undo.RecordObject(board, "Fill Board Grid");
            for (int x = 0; x < board.gridWidth; x++)
                for (int z = 0; z < board.gridHeight; z++)
                    board.SetCell(x, z, true);
            EditorUtility.SetDirty(board);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Clear All"))
        {
            Undo.RecordObject(board, "Clear Board Grid");
            for (int x = 0; x < board.gridWidth; x++)
                for (int z = 0; z < board.gridHeight; z++)
                    board.SetCell(x, z, false);
            EditorUtility.SetDirty(board);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Invert"))
        {
            Undo.RecordObject(board, "Invert Board Grid");
            for (int x = 0; x < board.gridWidth; x++)
                for (int z = 0; z < board.gridHeight; z++)
                    board.SetCell(x, z, !board.GetCell(x, z));
            EditorUtility.SetDirty(board);
            SceneView.RepaintAll();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawGameplaySettings()
    {
        EditorGUILayout.LabelField("Gameplay", EditorStyles.boldLabel);
    }

    private void OnSceneGUI()
    {
        if (board == null) return;
        if (board.visualizationMode != BoardManager.VisualizationMode.Gizmos) return;

        for (int x = 0; x < board.gridWidth; x++)
        {
            for (int z = 0; z < board.gridHeight; z++)
            {
                Vector3 worldPos = board.GetWorldPosition(x, z);
                bool active = board.GetCell(x, z);

                if (!active)
                {
                    Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.12f);
                    Handles.DrawWireCube(worldPos,
                        new Vector3(board.tileWidth, 0.02f, board.tileHeight));
                    continue;
                }

                Handles.DrawSolidRectangleWithOutline(
                    TileVerts(worldPos, board.tileWidth, board.tileHeight),
                    board.gizmoColorActive,
                    new Color(board.gizmoColorActive.r, board.gizmoColorActive.g, board.gizmoColorActive.b, 1f)
                );

                if (board.tileWidth >= 0.5f)
                {
                    Handles.Label(worldPos + Vector3.up * 0.15f, $"{x},{z}",
                        new GUIStyle { fontSize = 9, normal = { textColor = Color.white } });
                }
            }
        }
    }

    private static Vector3[] TileVerts(Vector3 center, float w, float h)
    {
        float hw = w * 0.5f;
        float hh = h * 0.5f;
        return new Vector3[]
        {
            center + new Vector3(-hw, 0f,  hh),
            center + new Vector3( hw, 0f,  hh),
            center + new Vector3( hw, 0f, -hh),
            center + new Vector3(-hw, 0f, -hh)
        };
    }

    private void EnsureGridReady()
    {
        if (board.gridWidth <= 0) board.gridWidth = 5;
        if (board.gridHeight <= 0) board.gridHeight = 5;
        board.ResizeGrid();
    }

    private int CountActiveCells()
    {
        int count = 0;
        for (int x = 0; x < board.gridWidth; x++)
            for (int z = 0; z < board.gridHeight; z++)
                if (board.GetCell(x, z)) count++;
        return count;
    }
}
#endif