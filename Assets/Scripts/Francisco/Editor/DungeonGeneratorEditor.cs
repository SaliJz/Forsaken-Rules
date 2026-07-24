#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

#region Inspector

[CustomEditor(typeof(DungeonGenerator))]
public class DungeonGeneratorInspector : Editor
{
    #region Fields

    private enum InspectorTab
    {
        Generation,
        Probabilities,
        Progression,
        Settings
    }

    private static InspectorTab _activeTab = InspectorTab.Generation;
    private readonly string[] _tabLabels = { "Matriz de Reglas", "Pesos", "Hitos", "Ajustes" };

    private Vector2 _matrixScroll;
    private Vector2 _weightsScroll;
    private Vector2 _milestonesScroll;
    private int _selectedProgressionIndex = -1;

    private const float MAP_ZOOM_MIN = 0.35f;
    private const float MAP_ZOOM_MAX = 1.8f;

    #endregion

    #region Unity Inspector

    public override void OnInspectorGUI()
    {
        DungeonGenerator gen = target as DungeonGenerator;
        if (gen == null) return;

        bool isWindowOpen = DungeonGeneratorEditorWindow.IsWindowActive();

        EditorGUILayout.Space(6);
        Rect headerRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUI.DrawRect(headerRect, new Color(0.12f, 0.35f, 0.44f, 0.15f));

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Dungeon Generator Visual Suite", EditorStyles.boldLabel);

        string btnText = isWindowOpen ? "Enfocar Ventana" : "Abrir Ventana de Mapa 2D";
        if (GUILayout.Button(btnText, EditorStyles.miniButtonRight, GUILayout.Width(160)))
        {
            DungeonGeneratorEditorWindow.OpenWindowFromInspector(gen);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        if (isWindowOpen)
        {
            EditorGUILayout.HelpBox("La Suite Visual y el Mapa Direccional están activos en una ventana externa.", MessageType.Info);
            return;
        }

        serializedObject.Update();
        EditorGUI.BeginChangeCheck();

        _activeTab = (InspectorTab)GUILayout.Toolbar((int)_activeTab, _tabLabels, EditorStyles.toolbarButton, GUILayout.Height(20));
        EditorGUILayout.Space(6);

        switch (_activeTab)
        {
            case InspectorTab.Generation:
                DrawInspectorMatrix(gen);
                break;

            case InspectorTab.Probabilities:
                DrawInspectorProbabilities();
                break;

            case InspectorTab.Progression:
                DrawInspectorProgression(gen);
                break;

            case InspectorTab.Settings:
                DrawInspectorSettings();
                break;
        }

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(gen);
        }
    }

    #endregion

    #region Inspector Tabs

    private void DrawInspectorMatrix(DungeonGenerator gen)
    {
        if (gen.generationRules == null || gen.generationRules.Length == 0) return;

        var allTypes = new HashSet<RoomType>();

        foreach (var rule in gen.generationRules)
        {
            if (rule == null) continue;

            allTypes.Add(rule.currentRoomType);

            if (rule.allowedNextRoomTypes != null)
            {
                foreach (var t in rule.allowedNextRoomTypes)
                {
                    allTypes.Add(t);
                }
            }
        }

        var typeList = allTypes.OrderBy(t => t.ToString()).ToList();
        if (!typeList.Any()) return;

        _matrixScroll = EditorGUILayout.BeginScrollView(_matrixScroll, GUILayout.Height(180));

        float colW = 55f;
        float rowH = 18f;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(colW + 4);

        foreach (var dst in typeList)
        {
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            GUILayout.Label(dst.ToString().Substring(0, Mathf.Min(dst.ToString().Length, 4)), labelStyle, GUILayout.Width(colW));
        }

        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < gen.generationRules.Length; i++)
        {
            var rule = gen.generationRules[i];
            if (rule == null) continue;

            EditorGUILayout.BeginHorizontal();

            GUIStyle srcStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold
            };

            GUILayout.Label(rule.currentRoomType.ToString(), srcStyle, GUILayout.Width(colW + 4));

            var allowedList = rule.allowedNextRoomTypes?.ToList() ?? new List<RoomType>();

            foreach (var dst in typeList)
            {
                bool isAllowed = allowedList.Contains(dst);
                Rect cellRect = GUILayoutUtility.GetRect(colW, rowH, GUILayout.Width(colW));

                Color cellBg = isAllowed ? new Color(0.15f, 0.4f, 0.2f) : new Color(0.35f, 0.15f, 0.15f);
                EditorGUI.DrawRect(cellRect, cellBg);

                GUIStyle cellStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };

                if (GUI.Button(cellRect, isAllowed ? "Perm." : "Bloq.", cellStyle))
                {
                    Undo.RecordObject(gen, "Modificar Regla de Matriz");

                    if (isAllowed) allowedList.Remove(dst);
                    else allowedList.Add(dst);

                    rule.allowedNextRoomTypes = allowedList.ToArray();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawInspectorProbabilities()
    {
        SerializedProperty propList = serializedObject.FindProperty("roomTypeProbabilities");
        if (propList == null || propList.arraySize == 0) return;

        _weightsScroll = EditorGUILayout.BeginScrollView(_weightsScroll, GUILayout.Height(200));

        for (int i = 0; i < propList.arraySize; i++)
        {
            SerializedProperty propElement = propList.GetArrayElementAtIndex(i);
            SerializedProperty typeProp = propElement.FindPropertyRelative("roomType");
            SerializedProperty probProp = propElement.FindPropertyRelative("probability");
            SerializedProperty prefabsProp = propElement.FindPropertyRelative("roomPrefabs");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(((RoomType)typeProp.enumValueIndex).ToString(), EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.PropertyField(probProp, GUIContent.none, GUILayout.Width(45));
            EditorGUILayout.PropertyField(prefabsProp, new GUIContent("Pool de Prefabs Activos"), false);

            EditorGUILayout.EndHorizontal();

            if (prefabsProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(prefabsProp, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawInspectorProgression(DungeonGenerator gen)
    {
        SerializedProperty rulesProp = serializedObject.FindProperty("progressionRules");
        if (rulesProp == null || rulesProp.arraySize == 0) return;

        _milestonesScroll = EditorGUILayout.BeginScrollView(_milestonesScroll, GUILayout.Height(200));

        for (int i = 0; i < rulesProp.arraySize; i++)
        {
            SerializedProperty ruleProp = rulesProp.GetArrayElementAtIndex(i);
            RoomProgressionRule rule = gen.progressionRules[i];
            if (rule == null) continue;

            bool isSelected = _selectedProgressionIndex == i;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(rule.roomType.ToString(), EditorStyles.boldLabel, GUILayout.Width(75));

            SerializedProperty minProp = ruleProp.FindPropertyRelative("minRoomNumber");
            SerializedProperty maxProp = ruleProp.FindPropertyRelative("maxRoomNumber");

            EditorGUILayout.PropertyField(minProp, GUIContent.none, GUILayout.Width(28));
            EditorGUILayout.LabelField("al", EditorStyles.miniLabel, GUILayout.Width(15));
            EditorGUILayout.PropertyField(maxProp, GUIContent.none, GUILayout.Width(28));

            SerializedProperty mandatoryProp = ruleProp.FindPropertyRelative("isMandatory");
            EditorGUILayout.PropertyField(mandatoryProp, GUIContent.none, GUILayout.Width(20));
            EditorGUILayout.LabelField("Obligatorio", EditorStyles.miniLabel, GUILayout.Width(65));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(isSelected ? "Ocultar" : "Editar", EditorStyles.miniButton, GUILayout.Width(52)))
            {
                _selectedProgressionIndex = isSelected ? -1 : i;
            }

            EditorGUILayout.EndHorizontal();

            if (isSelected)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(2);
                DrawPropertyIfExists(ruleProp, "isProbableMandatory", "Obligatorio Probable");
                DrawPropertyIfExists(ruleProp, "probability", "Probabilidad");
                DrawPropertyIfExists(ruleProp, "generateOnce", "Generar Solo Una Vez");
                DrawPropertyIfExists(ruleProp, "allowMultipleDoorsOfSameType", "Permitir Múltiples Puertas");
                DrawPropertyIfExists(ruleProp, "useCustomPrefabs", "Usar Prefabs Personalizados");
                DrawPropertyIfExists(ruleProp, "customRoomPrefabs", "Prefabs Específicos de Sala", true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawInspectorSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Prefabs Principales", EditorStyles.boldLabel);
        DrawPropertyIfExists("startRoomPrefab", "Sala Inicial");
        DrawPropertyIfExists("endRoomPrefabs", "Salas Finales", true);
        DrawPropertyIfExists("enemyPrefabs", "Prefabs de Enemigos", true);
        DrawPropertyIfExists("spawnEffectPrefab", "Efecto de Spawn");
        DrawPropertyIfExists("defaultSpawnEffectYOffset", "Offset Y Default");
        DrawPropertyIfExists("enemySpawnEffects", "Efectos por Enemigo (Override)", true);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Reglas y Probabilidades", EditorStyles.boldLabel);
        DrawPropertyIfExists("roomGenerationRules", "Room Generation Rules", true);
        DrawPropertyIfExists("roomTypeProbabilities", "Probabilidades por Tipo", true);
        DrawPropertyIfExists("generationRules", "Matriz de Generación", true);
        DrawPropertyIfExists("progressionRules", "Reglas de Progresión", true);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Configuración del Algoritmo", EditorStyles.boldLabel);
        DrawPropertyIfExists("minRooms", "Mínimo de Salas");
        DrawPropertyIfExists("maxRooms", "Máximo de Salas");
        DrawPropertyIfExists("maxRoomAttempts", "Intentos Máximos");
        DrawPropertyIfExists("repetitionPenalty", "Penalización por Repetición");
        DrawPropertyIfExists("weightDecay", "Decaimiento de Peso");
        DrawPropertyIfExists("roomDistance", "Distancia entre Salas");
        DrawPropertyIfExists("enableDoorPreview", "Vista Previa de Puertas");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Movimiento y Puertas", EditorStyles.boldLabel);
        DrawPropertyIfExists("playerMoveDuration", "Duración Movimiento Jugador");
        DrawPropertyIfExists("playerMoveDistance", "Distancia Movimiento Jugador");
        DrawPropertyIfExists("doorActivateDelay", "Delay Activación Puerta");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Sistema Progresivo", EditorStyles.boldLabel);
        DrawPropertyIfExists("defaultEnemyConfig", "Configuración Enemigos");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Dependencias y Debug", EditorStyles.boldLabel);
        DrawPropertyIfExists("roomTransitionController", "Room Transition Controller");
        DrawPropertyIfExists("playerHealth", "Player Health");
        DrawPropertyIfExists("statsManager", "Stats Manager");
        DrawPropertyIfExists("combatActionManager", "Player Combat Action Manager");
        DrawPropertyIfExists("_debugCompleteRoomKey", "Tecla Completar Sala");
        EditorGUILayout.EndVertical();
    }

    #endregion

    #region Helpers

    private void DrawPropertyIfExists(string propertyName, string label, bool includeChildren = false)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
        }
    }

    private void DrawPropertyIfExists(SerializedProperty parent, string propertyName, string label, bool includeChildren = false)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
        }
    }

    #endregion
}

#endregion

#region Editor Window

public class DungeonGeneratorEditorWindow : EditorWindow
{
    #region Fields

    private DungeonGenerator _target;
    private SerializedObject _serialized;

    private enum Tab
    {
        Simulate,
        Rules,
        Settings
    }

    private Tab _activeTab = Tab.Simulate;
    private readonly string[] _tabLabels = { "Mapa de Rejilla Direccional", "Matriz y Ajuste de Reglas", "Ajustes Generales" };

    private List<SimRoom> _simRooms = new List<SimRoom>();
    private bool _hasResult = false;
    private int _simSeed = -1;
    private bool _useFixedSeed = false;
    private Vector2 _mapScroll;
    private SimRoom _selectedRoom;

    private const float NODE_W = 120f;
    private const float NODE_H = 65f;
    private const float GRID_CELL_SIZE = 180f;

    private Vector2 _panOffset = new Vector2(400, 300);
    private bool _isPanning = false;

    private static readonly ConnectionType[] AllowedSimulationDirections =
    {
        ConnectionType.North,
        ConnectionType.West
    };

    private static readonly Dictionary<RoomType, Color> RoomColors = new Dictionary<RoomType, Color>
    {
        { RoomType.Normal,    new Color(0.25f, 0.25f, 0.25f) },
        { RoomType.Combat,    new Color(0.65f, 0.15f, 0.15f) },
        { RoomType.Shop,      new Color(0.7f, 0.55f, 0.05f) },
        { RoomType.Treasure,  new Color(0.1f, 0.5f, 0.2f) },
        { RoomType.Boss,      new Color(0.45f, 0.1f, 0.55f) },
        { RoomType.Challenge, new Color(0.8f, 0.35f, 0.0f) },
        { RoomType.Gachapon,  new Color(0.1f, 0.45f, 0.55f) }
    };

    private Vector2 _rulesScroll;
    private Vector2 _settingsScroll;
    private bool _showGenerationRules = true;
    private bool _showProgressionRules = true;
    private bool _showProbabilities = true;
    private int _selectedProgressionRule = -1;

    private static DungeonGeneratorEditorWindow _instance = null;

    #endregion

    #region Static API

    public static bool IsWindowActive()
    {
        if (_instance != null) return true;

        DungeonGeneratorEditorWindow[] windows = Resources.FindObjectsOfTypeAll<DungeonGeneratorEditorWindow>();
        return windows != null && windows.Length > 0;
    }

    public static void OpenWindowFromInspector(DungeonGenerator target)
    {
        _instance = GetWindow<DungeonGeneratorEditorWindow>("Dungeon Directional Mapper");
        _instance.minSize = new Vector2(800, 550);
        _instance._target = target;
        _instance.OnTargetChanged();
        _instance.Show();
    }

    private static Color GetRoomColor(RoomType t)
    {
        return RoomColors.TryGetValue(t, out Color c) ? c : new Color(0.3f, 0.3f, 0.3f);
    }

    #endregion

    #region Unity Window

    private void OnEnable()
    {
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnSelectionChange()
    {
        GameObject currentGO = Selection.activeGameObject;
        if (currentGO == null) return;

        DungeonGenerator gen = currentGO.GetComponent<DungeonGenerator>();
        if (gen != null && gen != _target)
        {
            _target = gen;
            OnTargetChanged();
        }
    }

    private void OnGUI()
    {
        if (_target == null)
        {
            OnSelectionChange();
        }

        DrawEditorCore(_target, _serialized, position.width, position.height, this);
    }

    #endregion

    #region Core Draw

    public static void DrawEditorCore(DungeonGenerator target, SerializedObject serialized, float targetWidth, float targetHeight, DungeonGeneratorEditorWindow viewState = null)
    {
        DungeonGeneratorEditorWindow ctx = viewState;
        if (ctx == null) return;

        ctx._target = target;
        ctx._serialized = serialized;

        if (ctx._target == null)
        {
            EditorGUILayout.HelpBox("Selecciona un GameObject con el componente DungeonGenerator.", MessageType.Info);
            return;
        }

        if (ctx._serialized == null || ctx._serialized.targetObject != ctx._target)
        {
            ctx.OnTargetChanged();
        }

        ctx._serialized?.Update();

        ctx._activeTab = (Tab)GUILayout.Toolbar((int)ctx._activeTab, ctx._tabLabels, EditorStyles.toolbarButton, GUILayout.Height(22));
        EditorGUILayout.Space(2);

        switch (ctx._activeTab)
        {
            case Tab.Simulate:
                ctx.DrawSimulateTab(targetWidth, targetHeight);
                break;

            case Tab.Rules:
                ctx.DrawRulesTab();
                break;

            case Tab.Settings:
                ctx.DrawSettingsTab();
                break;
        }

        ctx._serialized?.ApplyModifiedProperties();
    }

    private void OnTargetChanged()
    {
        _serialized = _target != null ? new SerializedObject(_target) : null;
        _simRooms.Clear();
        _hasResult = false;
        _selectedRoom = null;
        Repaint();
    }

    #endregion

    #region Simulate Tab

    private void DrawSimulateTab(float targetWidth, float targetHeight)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        _useFixedSeed = EditorGUILayout.Toggle("Semilla Fija", _useFixedSeed, GUILayout.Width(110));

        GUI.enabled = _useFixedSeed;
        _simSeed = EditorGUILayout.IntField(_simSeed, GUILayout.Width(80));
        GUI.enabled = true;

        GUILayout.Space(10);

        if (GUILayout.Button("Simular Camino Direccional", GUILayout.Height(20), GUILayout.Width(190)))
        {
            RunDirectionalSimulation();
        }

        if (_hasResult && GUILayout.Button("Reset", GUILayout.Height(20), GUILayout.Width(50)))
        {
            _simRooms.Clear();
            _hasResult = false;
            _selectedRoom = null;
        }

        GUILayout.FlexibleSpace();

        DrawColorLegend();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        if (!_hasResult)
        {
            EditorGUILayout.HelpBox("Haz clic en 'Simular Camino Direccional'. El simulador solo usará Norte y Oeste, que en tu mapa equivalen a arriba y derecha.", MessageType.None);
            return;
        }

        DrawValidationBanner();

        EditorGUILayout.BeginHorizontal();

        float canvasHeight = targetHeight - 120f;
        float mapWidth = _selectedRoom != null ? targetWidth - 280f : targetWidth - 10f;

        HandleCanvasPan(new Rect(0, 70, mapWidth, canvasHeight));

        _mapScroll = EditorGUILayout.BeginScrollView(_mapScroll, GUILayout.Width(mapWidth), GUILayout.Height(canvasHeight));

        Rect canvasRect = GUILayoutUtility.GetRect(4000, 4000);

        DrawDirectionalGrid(canvasRect);
        DrawDirectionalConnections();
        DrawDirectionalNodes();

        EditorGUILayout.EndScrollView();

        if (_selectedRoom != null)
        {
            DrawRoomDetailPanel(canvasHeight);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void RunDirectionalSimulation()
    {
        _simRooms.Clear();
        _selectedRoom = null;

        Random.State oldState = Random.state;

        if (_useFixedSeed)
        {
            Random.InitState(_simSeed);
        }
        else
        {
            Random.InitState((int)System.DateTime.Now.Ticks);
        }

        Dictionary<RoomType, List<Room>> roomPools = new Dictionary<RoomType, List<Room>>();

        if (_target.roomTypeProbabilities != null)
        {
            foreach (RoomTypeProbability rtp in _target.roomTypeProbabilities)
            {
                if (rtp == null) continue;

                roomPools[rtp.roomType] = rtp.roomPrefabs != null
                    ? rtp.roomPrefabs.Where(p => p != null).ToList()
                    : new List<Room>();
            }
        }

        Dictionary<RoomType, RoomType[]> genRules = new Dictionary<RoomType, RoomType[]>();

        foreach (RoomGenerationRule rule in _target.generationRules ?? System.Array.Empty<RoomGenerationRule>())
        {
            if (rule != null)
            {
                genRules[rule.currentRoomType] = rule.allowedNextRoomTypes ?? System.Array.Empty<RoomType>();
            }
        }

        HashSet<Vector2Int> occupiedCoords = new HashSet<Vector2Int>();

        Vector2Int currentGridPos = Vector2Int.zero;
        occupiedCoords.Add(currentGridPos);

        SimRoom firstRoom = new SimRoom
        {
            Index = 0,
            RoomType = RoomType.Normal,
            RoomObject = _target.startRoomPrefab,
            RuleName = "Entrada Base",
            ParentIndex = -1,
            GridCoords = currentGridPos,
            CalculatedDirection = ConnectionType.North
        };

        _simRooms.Add(firstRoom);

        int totalRooms = Random.Range(_target.minRooms, _target.maxRooms + 1);
        RoomType previousType = RoomType.Normal;
        List<RoomProgressionRule> usedOnce = new List<RoomProgressionRule>();

        for (int i = 1; i <= totalRooms; i++)
        {
            var nextTypeData = DetermineNextRoomType(previousType, i, genRules, usedOnce);

            List<Room> candidatePrefabs = GetCandidatePrefabsForSimulation(
                nextTypeData.type,
                nextTypeData.rule,
                roomPools
            );

            SimRoom previousRoom = _simRooms[_simRooms.Count - 1];

            TryPickSimulationStep(
                previousRoom.RoomObject,
                candidatePrefabs,
                currentGridPos,
                occupiedCoords,
                out ConnectionType chosenDir,
                out Vector2Int nextGridPos,
                out Room selectedPrefab,
                out string prefabWarning
            );

            if (nextTypeData.rule != null && nextTypeData.rule.generateOnce)
            {
                usedOnce.Add(nextTypeData.rule);
            }

            currentGridPos = nextGridPos;
            occupiedCoords.Add(currentGridPos);

            _simRooms.Add(new SimRoom
            {
                Index = i,
                RoomType = nextTypeData.type,
                RoomObject = selectedPrefab,
                IsFromMandatoryRule = nextTypeData.isMandatory,
                RuleName = nextTypeData.rule != null ? "Hito Progresión" : "Distribución Pesos",
                ParentIndex = i - 1,
                GridCoords = currentGridPos,
                CalculatedDirection = chosenDir,
                PrefabWarning = prefabWarning,
                HasTransitionWarning = !string.IsNullOrEmpty(prefabWarning),
                TransitionWarning = prefabWarning
            });

            previousType = nextTypeData.type;
        }

        List<Room> bossPrefabs = _target.endRoomPrefabs != null
            ? _target.endRoomPrefabs.Where(p => p != null).ToList()
            : new List<Room>();

        SimRoom lastRoom = _simRooms[_simRooms.Count - 1];

        TryPickSimulationStep(
            lastRoom.RoomObject,
            bossPrefabs,
            currentGridPos,
            occupiedCoords,
            out ConnectionType finalDir,
            out Vector2Int finalCoords,
            out Room bossPrefab,
            out string bossWarning
        );

        _simRooms.Add(new SimRoom
        {
            Index = _simRooms.Count,
            RoomType = RoomType.Boss,
            RoomObject = bossPrefab,
            RuleName = "Cámara del Jefe",
            ParentIndex = _simRooms.Count - 1,
            GridCoords = finalCoords,
            CalculatedDirection = finalDir,
            PrefabWarning = bossWarning,
            HasTransitionWarning = !string.IsNullOrEmpty(bossWarning),
            TransitionWarning = bossWarning
        });

        Random.state = oldState;

        bool shouldCenterMap = !_hasResult;
        _hasResult = true;

        if (shouldCenterMap)
        {
            CenterMapOnGeneratedRooms();
        }

        UpdateRuntimeValidation();
    }

    private void CenterMapOnGeneratedRooms()
    {
        if (_simRooms == null || _simRooms.Count == 0)
        {
            return;
        }

        int minX = _simRooms.Min(room => room.GridCoords.x);
        int maxX = _simRooms.Max(room => room.GridCoords.x);
        int minY = _simRooms.Min(room => room.GridCoords.y);
        int maxY = _simRooms.Max(room => room.GridCoords.y);

        float mapCenterX = ((minX + maxX) * 0.5f) * GRID_CELL_SIZE;
        float mapCenterY = -((minY + maxY) * 0.5f) * GRID_CELL_SIZE;

        _panOffset = new Vector2(1900f - mapCenterX, 1900f - mapCenterY);
        _mapScroll = new Vector2(900f, 900f);
    }

    private Vector2Int GetDirectionOffset(ConnectionType direction)
    {
        switch (direction)
        {
            case ConnectionType.North:
                return new Vector2Int(0, 1);

            case ConnectionType.West:
                return new Vector2Int(1, 0);

            case ConnectionType.South:
                return new Vector2Int(0, -1);

            case ConnectionType.East:
                return new Vector2Int(-1, 0);

            default:
                return Vector2Int.zero;
        }
    }

    #endregion

    #region Map Drawing

    private void HandleCanvasPan(Rect area)
    {
        Event e = Event.current;

        if (!area.Contains(e.mousePosition)) return;

        if (e.type == EventType.MouseDown && (e.button == 2 || e.button == 1))
        {
            _isPanning = true;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && _isPanning)
        {
            _panOffset += e.delta;
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp)
        {
            _isPanning = false;
        }
    }

    private void DrawDirectionalGrid(Rect canvas)
    {
        EditorGUI.DrawRect(canvas, new Color(0.15f, 0.15f, 0.15f));

        Handles.BeginGUI();
        Handles.color = new Color(0.22f, 0.22f, 0.22f, 0.6f);

        float startX = _panOffset.x % GRID_CELL_SIZE;
        float startY = _panOffset.y % GRID_CELL_SIZE;

        for (float x = startX; x < canvas.width; x += GRID_CELL_SIZE)
        {
            Handles.DrawLine(new Vector3(x, 0, 0), new Vector3(x, canvas.height, 0));
        }

        for (float y = startY; y < canvas.height; y += GRID_CELL_SIZE)
        {
            Handles.DrawLine(new Vector3(0, y, 0), new Vector3(canvas.width, y, 0));
        }

        Handles.EndGUI();
    }

    private void DrawDirectionalConnections()
    {
        Handles.BeginGUI();

        foreach (SimRoom room in _simRooms)
        {
            if (room.ParentIndex < 0) continue;

            SimRoom parent = _simRooms.FirstOrDefault(r => r.Index == room.ParentIndex);
            if (parent == null) continue;

            Vector2 from = GetRoomCenterVector(parent);
            Vector2 to = GetRoomCenterVector(room);

            Handles.color = room.HasTransitionWarning ? new Color(1f, 0.2f, 0.2f) : new Color(0.2f, 0.6f, 1f, 0.8f);
            Handles.DrawAAPolyLine(4f, from, to);

            Vector2 direction = (to - from).normalized;
            Vector2 mid = Vector2.Lerp(from, to, 0.5f);
            Vector2 right = new Vector2(-direction.y, direction.x);

            Vector2 arrowA = mid - direction * 10f + right * 6f;
            Vector2 arrowB = mid - direction * 10f - right * 6f;

            Handles.DrawAAPolyLine(3f, arrowA, mid, arrowB);
        }

        Handles.EndGUI();
    }

    private void DrawDirectionalNodes()
    {
        foreach (SimRoom room in _simRooms)
        {
            Rect rect = GetRoomRect(room);

            Color nodeColor = GetRoomColor(room.RoomType);
            EditorGUI.DrawRect(rect, nodeColor);

            if (room.HasTransitionWarning)
            {
                DrawBorder(rect, Color.red, 3f);
            }
            else if (_selectedRoom == room)
            {
                DrawBorder(rect, Color.cyan, 3f);
            }
            else
            {
                DrawBorder(rect, new Color(0f, 0f, 0f, 0.65f), 2f);
            }

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUIStyle smallStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(rect.x + 4f, rect.y + 5f, rect.width - 8f, 18f), $"{room.Index}. {room.RoomType}", titleStyle);

            string prefabName = room.RoomObject != null ? room.RoomObject.name : "Sin Prefab";
            GUI.Label(new Rect(rect.x + 4f, rect.y + 26f, rect.width - 8f, 16f), prefabName, smallStyle);
            GUI.Label(new Rect(rect.x + 4f, rect.y + 44f, rect.width - 8f, 16f), $"Dir: {room.CalculatedDirection}", smallStyle);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                _selectedRoom = _selectedRoom == room ? null : room;
                Event.current.Use();
                Repaint();
            }
        }
    }

    private void DrawRoomDetailPanel(float panelHeight)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(260), GUILayout.Height(panelHeight));

        EditorGUILayout.LabelField("Inspección de Nodo Activo", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Sala Index: {_selectedRoom.Index}", EditorStyles.miniBoldLabel);

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Tipo Actual:", EditorStyles.miniLabel, GUILayout.Width(70));
        _selectedRoom.RoomType = (RoomType)EditorGUILayout.EnumPopup(_selectedRoom.RoomType);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Prefab Objeto:", EditorStyles.miniLabel, GUILayout.Width(70));
        _selectedRoom.RoomObject = (Room)EditorGUILayout.ObjectField(_selectedRoom.RoomObject, typeof(Room), false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"Origen: {_selectedRoom.RuleName}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Posición Rejilla: ({_selectedRoom.GridCoords.x}, {_selectedRoom.GridCoords.y})", EditorStyles.miniLabel);

        if (_selectedRoom.ParentIndex >= 0)
        {
            EditorGUILayout.LabelField($"Dirección Relativa: Entra por el [{_selectedRoom.CalculatedDirection}]", EditorStyles.miniBoldLabel);
        }

        if (_selectedRoom.HasTransitionWarning)
        {
            EditorGUILayout.HelpBox(_selectedRoom.TransitionWarning, MessageType.Warning);
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Cerrar Detalle"))
        {
            _selectedRoom = null;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawColorLegend()
    {
        foreach (RoomType key in RoomColors.Keys)
        {
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = RoomColors[key];

            GUILayout.Label(key.ToString(), EditorStyles.miniButton, GUILayout.Width(62));

            GUI.backgroundColor = oldBg;
        }
    }

    private void DrawValidationBanner()
    {
        EditorGUILayout.BeginHorizontal();

        IEnumerable<string> counts = _simRooms
            .GroupBy(r => r.RoomType)
            .Select(g => $"{g.Key}: {g.Count()}");

        EditorGUILayout.LabelField("Mapeado Activo -> " + string.Join(" | ", counts), EditorStyles.miniBoldLabel);

        EditorGUILayout.EndHorizontal();
    }

    private Rect GetRoomRect(SimRoom room)
    {
        float posX = _panOffset.x + room.GridCoords.x * GRID_CELL_SIZE - NODE_W / 2f;
        float posY = _panOffset.y - room.GridCoords.y * GRID_CELL_SIZE - NODE_H / 2f;

        return new Rect(posX, posY, NODE_W, NODE_H);
    }

    private Vector2 GetRoomCenterVector(SimRoom room)
    {
        return GetRoomRect(room).center;
    }

    private static void DrawBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    #endregion

    #region Rules Tab

    public void DrawRulesTab()
    {
        _rulesScroll = EditorGUILayout.BeginScrollView(_rulesScroll);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _showGenerationRules = EditorGUILayout.Foldout(_showGenerationRules, "1. Matriz de Pasos de Generación", true, EditorStyles.foldoutHeader);
        if (_showGenerationRules)
        {
            DrawGenerationRulesMatrix();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _showProbabilities = EditorGUILayout.Foldout(_showProbabilities, "2. Probabilidades Globales Mutables", true, EditorStyles.foldoutHeader);
        if (_showProbabilities)
        {
            DrawProbabilitiesTable();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _showProgressionRules = EditorGUILayout.Foldout(_showProgressionRules, "3. Reglas de Progresión de Flujo", true, EditorStyles.foldoutHeader);
        if (_showProgressionRules)
        {
            DrawProgressionRulesList();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    private void DrawGenerationRulesMatrix()
    {
        if (_target.generationRules == null) return;

        List<RoomType> allTypes = _target.generationRules
            .Where(r => r != null)
            .Select(r => r.currentRoomType)
            .OrderBy(t => t.ToString())
            .ToList();

        if (!allTypes.Any()) return;

        GUIStyle centeredMiniLabel = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };

        float colW = 65f;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(colW + 10);

        foreach (RoomType dst in allTypes)
        {
            GUILayout.Label(dst.ToString().Substring(0, Mathf.Min(dst.ToString().Length, 5)), centeredMiniLabel, GUILayout.Width(colW));
        }

        EditorGUILayout.EndHorizontal();

        foreach (RoomGenerationRule rule in _target.generationRules)
        {
            if (rule == null) continue;

            EditorGUILayout.BeginHorizontal();

            GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = GetRoomColor(rule.currentRoomType) },
                fontStyle = FontStyle.Bold
            };

            GUILayout.Label(rule.currentRoomType.ToString(), style, GUILayout.Width(colW + 8));

            List<RoomType> allowed = rule.allowedNextRoomTypes?.ToList() ?? new List<RoomType>();

            foreach (RoomType dst in allTypes)
            {
                bool isAllowed = allowed.Contains(dst);
                Rect cell = GUILayoutUtility.GetRect(colW, 20f, GUILayout.Width(colW));

                EditorGUI.DrawRect(cell, isAllowed ? new Color(0.15f, 0.4f, 0.2f) : new Color(0.35f, 0.15f, 0.15f));

                if (GUI.Button(cell, isAllowed ? "OK" : "Bloq", centeredMiniLabel))
                {
                    Undo.RecordObject(_target, "Matriz");

                    if (isAllowed) allowed.Remove(dst);
                    else allowed.Add(dst);

                    rule.allowedNextRoomTypes = allowed.ToArray();
                    UpdateRuntimeValidation();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawProbabilitiesTable()
    {
        if (_target.roomTypeProbabilities == null || _serialized == null) return;

        SerializedProperty propList = _serialized.FindProperty("roomTypeProbabilities");
        if (propList == null) return;

        for (int i = 0; i < propList.arraySize; i++)
        {
            SerializedProperty pElem = propList.GetArrayElementAtIndex(i);
            SerializedProperty typeProp = pElem.FindPropertyRelative("roomType");
            SerializedProperty probProp = pElem.FindPropertyRelative("probability");
            SerializedProperty prefabsProp = pElem.FindPropertyRelative("roomPrefabs");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            RoomType rt = (RoomType)typeProp.enumValueIndex;

            GUIStyle tStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = GetRoomColor(rt) },
                fontStyle = FontStyle.Bold
            };

            EditorGUILayout.LabelField(rt.ToString(), tStyle, GUILayout.Width(90));
            EditorGUILayout.PropertyField(probProp, GUIContent.none, GUILayout.Width(50));
            EditorGUILayout.PropertyField(prefabsProp, new GUIContent("Pool de Prefabs Activos"), false);

            EditorGUILayout.EndHorizontal();

            if (prefabsProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(prefabsProp, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawProgressionRulesList()
    {
        if (_target.progressionRules == null || _serialized == null) return;

        SerializedProperty rulesProp = _serialized.FindProperty("progressionRules");
        if (rulesProp == null) return;

        for (int i = 0; i < rulesProp.arraySize; i++)
        {
            SerializedProperty rProp = rulesProp.GetArrayElementAtIndex(i);
            RoomProgressionRule rule = _target.progressionRules[i];

            if (rule == null) continue;

            bool isSelected = _selectedProgressionRule == i;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            GUIStyle tStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = GetRoomColor(rule.roomType) },
                fontStyle = FontStyle.Bold
            };

            EditorGUILayout.LabelField(rule.roomType.ToString(), tStyle, GUILayout.Width(80));

            DrawPropertyRelativeIfExists(rProp, "minRoomNumber", GUIContent.none, false, GUILayout.Width(30));
            EditorGUILayout.LabelField("al", EditorStyles.miniLabel, GUILayout.Width(15));
            DrawPropertyRelativeIfExists(rProp, "maxRoomNumber", GUIContent.none, false, GUILayout.Width(30));
            DrawPropertyRelativeIfExists(rProp, "isMandatory", new GUIContent("Obligatorio"), false, GUILayout.Width(95));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(isSelected ? "Ocultar" : "Editar", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                _selectedProgressionRule = isSelected ? -1 : i;
            }

            EditorGUILayout.EndHorizontal();

            if (isSelected)
            {
                EditorGUI.indentLevel++;
                DrawPropertyRelativeIfExists(rProp, "isProbableMandatory", new GUIContent("Obligatorio Probable"));
                DrawPropertyRelativeIfExists(rProp, "probability", new GUIContent("Probabilidad"));
                DrawPropertyRelativeIfExists(rProp, "generateOnce", new GUIContent("Generar Una Vez"));
                DrawPropertyRelativeIfExists(rProp, "allowMultipleDoorsOfSameType", new GUIContent("Permitir Múltiples Puertas"));
                DrawPropertyRelativeIfExists(rProp, "useCustomPrefabs", new GUIContent("Usar Prefabs Específicos"));
                DrawPropertyRelativeIfExists(rProp, "customRoomPrefabs", new GUIContent("Prefabs de la Regla"), true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }
    }

    #endregion

    #region Settings Tab

    public void DrawSettingsTab()
    {
        _settingsScroll = EditorGUILayout.BeginScrollView(_settingsScroll);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Prefabs Principales", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("Room Prefabs", EditorStyles.boldLabel);
        DrawSerializedProperty("startRoomPrefab", "Sala Inicial");
        DrawSerializedProperty("endRoomPrefabs", "Salas Finales", true);

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Enemy Prefabs", EditorStyles.boldLabel);
        DrawSerializedProperty("enemyPrefabs", "Prefabs de Enemigos", true);

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Visual Effects", EditorStyles.boldLabel);
        DrawSerializedProperty("spawnEffectPrefab", "Efecto de Spawn");
        DrawSerializedProperty("defaultSpawnEffectYOffset", "Offset Y Default");
        DrawSerializedProperty("enemySpawnEffects", "Efectos por Enemigo (Override)", true);

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Reglas y Probabilidades", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("Generation Rules", EditorStyles.boldLabel);
        DrawSerializedProperty("roomGenerationRules", "Room Generation Rules", true);

        EditorGUILayout.Space(4);

        DrawSerializedProperty("roomTypeProbabilities", "Probabilidades por Tipo", true);

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Room Generation Rules", EditorStyles.boldLabel);
        DrawSerializedProperty("generationRules", "Matriz de Generación", true);

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Room Progression Rules", EditorStyles.boldLabel);
        DrawSerializedProperty("progressionRules", "Reglas de Progresión", true);

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Ajustes Algorítmicos", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
        DrawSerializedProperty("minRooms", "Mínimo de Salas");
        DrawSerializedProperty("maxRooms", "Máximo de Salas");
        DrawSerializedProperty("maxRoomAttempts", "Intentos Máximos");
        DrawSerializedProperty("repetitionPenalty", "Penalización Repetición");
        DrawSerializedProperty("weightDecay", "Decaimiento de Peso");
        DrawSerializedProperty("roomDistance", "Separación 3D");
        DrawSerializedProperty("enableDoorPreview", "Vista Previa de Puertas");

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Movimiento y Puertas", EditorStyles.boldLabel);
        DrawSerializedProperty("playerMoveDuration", "Duración Movimiento Jugador");
        DrawSerializedProperty("playerMoveDistance", "Distancia Movimiento Jugador");
        DrawSerializedProperty("doorActivateDelay", "Delay Activación Puerta");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Sistema Progresivo", EditorStyles.boldLabel);
        DrawSerializedProperty("defaultEnemyConfig", "Configuración Enemigos");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Dependencias y Debug", EditorStyles.boldLabel);
        DrawSerializedProperty("roomTransitionController", "Room Transition Controller");
        DrawSerializedProperty("playerHealth", "Player Health");
        DrawSerializedProperty("statsManager", "Stats Manager");
        DrawSerializedProperty("_debugCompleteRoomKey", "Tecla Completar Sala");
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    #endregion

    #region Simulation Logic

    private (RoomType type, RoomProgressionRule rule, bool isMandatory) DetermineNextRoomType(
        RoomType prev,
        int step,
        Dictionary<RoomType, RoomType[]> rules,
        List<RoomProgressionRule> used)
    {
        RoomProgressionRule[] prog = _target.progressionRules ?? System.Array.Empty<RoomProgressionRule>();

        RoomProgressionRule mandatory = prog.FirstOrDefault(r =>
            r != null &&
            r.isMandatory &&
            step >= r.minRoomNumber &&
            step <= r.maxRoomNumber &&
            (!r.generateOnce || !used.Contains(r)));

        if (mandatory != null)
        {
            return (mandatory.GetRoomType(), mandatory, true);
        }

        List<RoomProgressionRule> probableRules = prog
            .Where(r =>
                r != null &&
                r.isProbableMandatory &&
                step >= r.minRoomNumber &&
                step <= r.maxRoomNumber &&
                (!r.generateOnce || !used.Contains(r)))
            .ToList();

        foreach (RoomProgressionRule probable in probableRules)
        {
            float roll = Random.Range(0f, 100f);
            if (roll <= probable.probability)
            {
                RoomType probableType = probable.GetRoomType();

                if (!rules.ContainsKey(prev) || rules[prev].Contains(probableType))
                {
                    return (probableType, probable, true);
                }
            }
        }

        List<RoomType> allowed = rules.ContainsKey(prev)
            ? rules[prev].ToList()
            : RoomColors.Keys.ToList();

        List<RoomTypeProbability> validWeights = _target.roomTypeProbabilities != null
            ? _target.roomTypeProbabilities.Where(p => p != null && allowed.Contains(p.roomType)).ToList()
            : new List<RoomTypeProbability>();

        if (validWeights.Any())
        {
            float total = validWeights.Sum(p => p.probability);

            if (total > 0f)
            {
                float roll = Random.Range(0f, total);
                float sum = 0f;

                foreach (RoomTypeProbability w in validWeights)
                {
                    sum += w.probability;
                    if (roll <= sum)
                    {
                        return (w.roomType, null, false);
                    }
                }
            }
        }

        return (allowed.Count > 0 ? allowed[Random.Range(0, allowed.Count)] : RoomType.Normal, null, false);
    }

    private List<Room> GetCandidatePrefabsForSimulation(
    RoomType roomType,
    RoomProgressionRule progressionRule,
    Dictionary<RoomType, List<Room>> roomPools)
    {
        if (progressionRule != null &&
            progressionRule.customRoomPrefabs != null &&
            progressionRule.customRoomPrefabs.Length > 0)
        {
            List<Room> customPrefabs = progressionRule.customRoomPrefabs
                .Where(p => p != null)
                .ToList();

            if (customPrefabs.Count > 0)
            {
                return customPrefabs;
            }
        }

        if (roomPools != null &&
            roomPools.TryGetValue(roomType, out List<Room> prefabs) &&
            prefabs != null)
        {
            return prefabs.Where(p => p != null).ToList();
        }

        return new List<Room>();
    }

    private bool TryPickSimulationStep(
        Room currentPrefab,
        List<Room> candidatePrefabs,
        Vector2Int currentGridPos,
        HashSet<Vector2Int> occupiedCoords,
        out ConnectionType chosenDirection,
        out Vector2Int nextGridPos,
        out Room selectedPrefab,
        out string warning)
    {
        warning = "";
        selectedPrefab = null;

        List<ConnectionType> usableDirections = GetUsableSimulationDirections(currentPrefab)
            .OrderBy(_ => Random.value)
            .ToList();

        foreach (ConnectionType direction in usableDirections)
        {
            Vector2Int candidateGridPos = currentGridPos + GetDirectionOffset(direction);

            if (occupiedCoords.Contains(candidateGridPos))
            {
                continue;
            }

            List<Room> compatiblePrefabs = candidatePrefabs
                .Where(prefab => RoomHasConnection(prefab, direction))
                .ToList();

            if (compatiblePrefabs.Count > 0)
            {
                chosenDirection = direction;
                nextGridPos = candidateGridPos;
                selectedPrefab = compatiblePrefabs[Random.Range(0, compatiblePrefabs.Count)];
                return true;
            }
        }

        foreach (ConnectionType direction in usableDirections)
        {
            Vector2Int candidateGridPos = currentGridPos + GetDirectionOffset(direction);

            if (occupiedCoords.Contains(candidateGridPos))
            {
                continue;
            }

            chosenDirection = direction;
            nextGridPos = candidateGridPos;

            if (candidatePrefabs != null && candidatePrefabs.Count > 0)
            {
                selectedPrefab = candidatePrefabs[Random.Range(0, candidatePrefabs.Count)];
                warning = $"El prefab seleccionado no tiene ConnectionPoint {direction}. Revisa sus salidas.";
            }
            else
            {
                warning = "No hay prefabs disponibles para este tipo de sala.";
            }

            return true;
        }

        chosenDirection = usableDirections.Count > 0 ? usableDirections[0] : ConnectionType.North;
        nextGridPos = currentGridPos + GetDirectionOffset(chosenDirection);

        if (candidatePrefabs != null && candidatePrefabs.Count > 0)
        {
            selectedPrefab = candidatePrefabs[Random.Range(0, candidatePrefabs.Count)];
        }

        warning = "No se encontró una posición libre usando las conexiones reales del prefab actual.";
        return false;
    }

    private List<ConnectionType> GetUsableSimulationDirections(Room room)
    {
        if (room == null || room.connectionPoints == null || room.connectionPoints.Length == 0)
        {
            return AllowedSimulationDirections.ToList();
        }

        List<ConnectionType> directions = room.connectionPoints
            .Where(cp => cp != null)
            .Select(cp => cp.connectionType)
            .Where(type => AllowedSimulationDirections.Contains(type))
            .Distinct()
            .ToList();

        if (directions.Count == 0)
        {
            return AllowedSimulationDirections.ToList();
        }

        return directions;
    }

    private bool RoomHasConnection(Room room, ConnectionType connectionType)
    {
        if (room == null || room.connectionPoints == null)
        {
            return false;
        }

        return room.connectionPoints.Any(cp =>
            cp != null &&
            cp.connectionType == connectionType
        );
    }

    private void UpdateRuntimeValidation()
    {
        Dictionary<RoomType, RoomType[]> rules = new Dictionary<RoomType, RoomType[]>();

        foreach (RoomGenerationRule rule in _target.generationRules ?? System.Array.Empty<RoomGenerationRule>())
        {
            if (rule != null)
            {
                rules[rule.currentRoomType] = rule.allowedNextRoomTypes ?? System.Array.Empty<RoomType>();
            }
        }

        for (int i = 1; i < _simRooms.Count; i++)
        {
            SimRoom prev = _simRooms[i - 1];
            SimRoom curr = _simRooms[i];

            List<string> warnings = new List<string>();

            if (!string.IsNullOrEmpty(curr.PrefabWarning))
            {
                warnings.Add(curr.PrefabWarning);
            }

            if (rules.ContainsKey(prev.RoomType) && !rules[prev.RoomType].Contains(curr.RoomType))
            {
                warnings.Add($"Incompatibilidad de flujo: La matriz prohíbe pasar de {prev.RoomType} a {curr.RoomType}.");
            }

            curr.HasTransitionWarning = warnings.Count > 0;
            curr.TransitionWarning = string.Join("\n", warnings);
        }

        Repaint();
    }

    #endregion

    #region Serialized Helpers

    private void DrawSerializedProperty(string propertyName, string label, bool includeChildren = false)
    {
        if (_serialized == null) return;

        SerializedProperty property = _serialized.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
        }
    }

    private void DrawPropertyRelativeIfExists(SerializedProperty parent, string propertyName, GUIContent label, bool includeChildren = false, params GUILayoutOption[] options)
    {
        if (parent == null) return;

        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, label, includeChildren, options);
        }
    }

    #endregion

    #region Data

    private class SimRoom
    {
        public int Index;
        public RoomType RoomType;
        public Room RoomObject;
        public string RuleName = "";
        public bool IsFromMandatoryRule;
        public bool HasTransitionWarning;
        public string TransitionWarning = "";
        public string PrefabWarning = "";
        public int ParentIndex = -1;
        public Vector2Int GridCoords;
        public ConnectionType CalculatedDirection;
    }

    #endregion
}

#endregion
#endif