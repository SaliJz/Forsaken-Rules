using System;
using UnityEngine;

public partial class PlayerStatsManager : MonoBehaviour
{
    #region Debug Enums

    /// <summary>
    /// Anclas válidas para el panel de debug.
    /// </summary>
    public enum DebugAnchor { TopLeft, TopRight, BottomLeft, BottomRight, Custom }

    #endregion

    #region Inspector - Debug Settings

    [Header("Debug OnGUI Positioning")]
    [Tooltip("Activa el panel OnGUI (debug).")]
    [SerializeField] private bool showDebugOnGUI = false;

    [Tooltip("Rect inicial (x,y = pantalla) — si guiAnchor != Custom, se ignorará y usará snap a esquina.")]
    [SerializeField] private Vector2 guiCustomPosition = new Vector2(10f, 10f);

    [Tooltip("Anchura del panel.")]
    [SerializeField] private int guiWidth = 440;
    [Tooltip("Altura del panel.")]
    [SerializeField] private int guiHeight = 600;

    [Tooltip("Si true, el panel se puede arrastrar con el mouse.")]
    [SerializeField] private bool guiDraggable = true;

    [Tooltip("Ancla predefinida para el panel. Si se cambia, el panel se reposiciona automáticamente.")]
    [SerializeField] private DebugAnchor guiAnchor = DebugAnchor.TopLeft;

    [Tooltip("Padding de la pantalla al hacer snap a esquina.")]
    [SerializeField] private int guiCornerPadding = 8;

    [Tooltip("Si true, guarda la posición en PlayerPrefs usando 'guiPrefsKey' al cerrar/arrastrar.")]
    [SerializeField] private bool guiPersistPosition = true;

    [Tooltip("PlayerPrefs key usada para persistir la posición del GUI (opcional).")]
    [SerializeField] private string guiPrefsKey = "PlayerStatsManager_DebugGUI_Pos";

    #endregion

    #region Internal Debug State

    // Runtime
    private Rect guiWindowRect = new Rect(10, 10, 440, 600);
    private bool guiWindowRectInitialized = false;
    //private bool guiPositionLoadedFromPrefs = false;

    // Estilos y scroll
    private GUIStyle guiTitleStyle;
    private GUIStyle guiLabelStyle;
    private GUIStyle guiSmallLabelStyle;
    private GUIStyle guiBoxStyle;
    private GUIStyle guiButtonStyle;
    private Vector2 guiScrollPos = Vector2.zero;

    // Para detectar cambios y ahorrar PlayerPrefs.Save()
    private Vector2 lastSavedGuiPos = Vector2.negativeInfinity;
    private int lastScreenW = 0;
    private int lastScreenH = 0;

    // Selector y campos para aplicar modificadores
    private int guiSelectedStatIndex = 0;
    private string guiModAmount = "1";
    private bool guiModIsPercentage = false;
    private bool guiModIsTemporary = false;
    private string guiModDuration = "10"; // en segundos
    private bool guiModIsByRooms = false;
    private string guiModRooms = "1";

    #endregion

    #region Debug Unity Lifecycle

    /// <summary>
    /// OnGUI del panel debug (usa GUI.Window para permitir DragWindow).
    /// </summary>
    private void OnGUI()
    {
        if (!showDebugOnGUI) return;
#if !UNITY_EDITOR
        if (!Debug.isDebugBuild) return;
#endif
        EnsureDebugGuiStyles();
        EnsureGuiWindowRectInitialized();

        // Si cambió el tamaño de pantalla, reajustar para que no quede fuera
        if (Screen.width != lastScreenW || Screen.height != lastScreenH)
        {
            guiWindowRect.x = Mathf.Clamp(guiWindowRect.x, 0, Mathf.Max(0, Screen.width - guiWindowRect.width));
            guiWindowRect.y = Mathf.Clamp(guiWindowRect.y, 0, Mathf.Max(0, Screen.height - guiWindowRect.height));
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
            guiWindowRect.width = Mathf.Clamp(guiWidth, 200, Mathf.Max(200, Screen.width - 20));
            guiWindowRect.height = Mathf.Clamp(guiHeight, 120, Mathf.Max(120, Screen.height - 20));
        }

        // Si la anchura/altura cambiaron en inspector
        if (Mathf.Abs(guiWindowRect.width - guiWidth) > 0.01f || Mathf.Abs(guiWindowRect.height - guiHeight) > 0.01f)
        {
            guiWindowRect.width = Mathf.Clamp(guiWidth, 200, Mathf.Max(200, Screen.width - 20));
            guiWindowRect.height = Mathf.Clamp(guiHeight, 120, Mathf.Max(120, Screen.height - 20));
            guiWindowRect.x = Mathf.Clamp(guiWindowRect.x, 0, Mathf.Max(0, Screen.width - guiWindowRect.width));
            guiWindowRect.y = Mathf.Clamp(guiWindowRect.y, 0, Mathf.Max(0, Screen.height - guiWindowRect.height));
        }

        // Dibujar ventana
        guiWindowRect = GUI.Window(123451, guiWindowRect, GuiWindowFunction, "PlayerStatsManager - Debug");

        // Guardar posición si está habilitado
        if (guiPersistPosition)
        {
            SaveGuiPositionToPrefs();
        }
    }

    #endregion

    #region Debug UI Layout

    /// <summary>
    /// Contenido interno de la ventana debug con toda la funcionalidad.
    /// </summary>
    private void GuiWindowFunction(int windowID)
    {
        GUILayout.BeginVertical();

        // === BARRA DE CONTROL SUPERIOR ===
        GUILayout.BeginHorizontal();

        // Snap buttons
        if (GUILayout.Button("TL", GUILayout.Width(28))) { SnapGuiTo(DebugAnchor.TopLeft); }
        if (GUILayout.Button("TR", GUILayout.Width(28))) { SnapGuiTo(DebugAnchor.TopRight); }
        if (GUILayout.Button("BL", GUILayout.Width(28))) { SnapGuiTo(DebugAnchor.BottomLeft); }
        if (GUILayout.Button("BR", GUILayout.Width(28))) { SnapGuiTo(DebugAnchor.BottomRight); }

        GUILayout.FlexibleSpace();

        guiDraggable = GUILayout.Toggle(guiDraggable, "Drag", GUILayout.Width(60));
        guiPersistPosition = GUILayout.Toggle(guiPersistPosition, "Persist", GUILayout.Width(70));

        if (GUILayout.Button("Center", GUILayout.Width(60)))
        {
            guiAnchor = DebugAnchor.Custom;
            guiWindowRect.x = (Screen.width - guiWindowRect.width) * 0.5f;
            guiWindowRect.y = (Screen.height - guiWindowRect.height) * 0.5f;
            SaveGuiPositionToPrefs();
        }

        if (GUILayout.Button("Reset", GUILayout.Width(56)))
        {
            SnapGuiTo(DebugAnchor.TopLeft);
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // === TÍTULO ===
        GUILayout.Label("PLAYER STATS MANAGER - DEBUG", guiTitleStyle);
        GUILayout.Space(6);

        // === INFO GENERAL ===
        GUILayout.BeginHorizontal();
        GUILayout.Label("Current SO:", guiLabelStyle, GUILayout.Width(110));
        GUILayout.Label(currentStatSO != null ? currentStatSO.name : "NULL", guiLabelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Rooms completed:", guiLabelStyle, GUILayout.Width(110));
        GUILayout.Label($"{roomsCompletedSinceStart}", guiLabelStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // === ACCIONES RÁPIDAS ===
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Stats Display", guiButtonStyle, GUILayout.Height(26)))
        {
            UpdateStatsDisplay();
        }
        if (GUILayout.Button("Reset Stats from Current to Base", guiButtonStyle, GUILayout.Height(26)))
        {
            ResetCurrentStatsToBase();
            UpdateStatsDisplay();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Stats from Run to Defaults", guiButtonStyle, GUILayout.Height(26)))
        {
            ResetRunStatsToDefaults();
            UpdateStatsDisplay();
        }
        if (GUILayout.Button("Reset Stats On Death", guiButtonStyle, GUILayout.Height(26)))
        {
            ResetStatsOnDeath();
            UpdateStatsDisplay();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // === LISTADO DE STATS (SCROLLABLE) ===
        GUILayout.Label("Stats (clic para seleccionar):", guiLabelStyle);
        guiScrollPos = GUILayout.BeginScrollView(guiScrollPos, GUILayout.Height(260));

        int index = 0;
        foreach (StatType st in Enum.GetValues(typeof(StatType)))
        {
            float cur = currentStats.TryGetValue(st, out var cv) ? cv : 0f;
            float basev = baseStats.TryGetValue(st, out var bv) ? bv : 0f;
            string line = showBaseValues
                ? $"{SplitCamelCase(st.ToString())}: {cur.ToString($"F{Mathf.Max(0, decimals)}")} (base: {basev.ToString($"F{Mathf.Max(0, decimals)}")})"
                : $"{SplitCamelCase(st.ToString())}: {cur.ToString($"F{Mathf.Max(0, decimals)}")}";

            int visual = statVisualState.ContainsKey(st) ? statVisualState[st] : 0;
            Color prevCol = GUI.color;
            if (visual > 0) GUI.color = Color.green;
            else if (visual < 0) GUI.color = Color.red;

            if (GUILayout.Button(line, GUILayout.Height(20)))
            {
                guiSelectedStatIndex = index;
            }

            GUI.color = prevCol;
            index++;
        }

        GUILayout.EndScrollView();

        GUILayout.Space(6);

        // === PANEL PARA APLICAR MODIFICADOR ===
        StatType selected = (StatType)Mathf.Clamp(guiSelectedStatIndex, 0, Enum.GetValues(typeof(StatType)).Length - 1);
        GUILayout.Label($"Selected: {SplitCamelCase(selected.ToString())}", guiLabelStyle);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Amount:", guiSmallLabelStyle, GUILayout.Width(60));
        guiModAmount = GUILayout.TextField(guiModAmount, GUILayout.Width(100));
        GUILayout.Label(guiModIsPercentage ? "%" : "units", guiSmallLabelStyle, GUILayout.Width(40));
        guiModIsPercentage = GUILayout.Toggle(guiModIsPercentage, "Percentage", GUILayout.Width(100));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        guiModIsTemporary = GUILayout.Toggle(guiModIsTemporary, "Temporary", GUILayout.Width(100));
        guiModIsByRooms = GUILayout.Toggle(guiModIsByRooms, "By Rooms", GUILayout.Width(100));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Duration(s):", guiSmallLabelStyle, GUILayout.Width(80));
        guiModDuration = GUILayout.TextField(guiModDuration, GUILayout.Width(60));
        GUILayout.Label("Rooms:", guiSmallLabelStyle, GUILayout.Width(50));
        guiModRooms = GUILayout.TextField(guiModRooms, GUILayout.Width(40));
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        if (GUILayout.Button("Apply Modifier", guiButtonStyle, GUILayout.Height(28)))
        {
            if (!float.TryParse(guiModAmount, out float amount))
            {
                Debug.LogWarning("[PlayerStatsManager OnGUI] Amount inválido.");
            }
            else
            {
                int rooms = 0;
                if (!int.TryParse(guiModRooms, out rooms)) rooms = 0;

                if (guiModIsTemporary)
                {
                    if (guiModIsByRooms)
                    {
                        ApplyModifier(selected, amount, guiModIsPercentage, true, 0f, true, Mathf.Max(1, rooms));
                    }
                    else
                    {
                        if (!float.TryParse(guiModDuration, out float dur))
                        {
                            Debug.LogWarning("[PlayerStatsManager OnGUI] Duration inválida.");
                        }
                        else
                        {
                            ApplyModifier(selected, amount, guiModIsPercentage, true, Mathf.Max(0.01f, dur), false, 0);
                        }
                    }
                }
                else
                {
                    ApplyModifier(selected, amount, guiModIsPercentage, false, 0f, false, 0);
                }

                UpdateStatsDisplay();
            }
        }

        GUILayout.Space(6);

        // === BOTONES DE MUESTRA ===
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Visual States", GUILayout.Height(22)))
        {
            foreach (StatType s in Enum.GetValues(typeof(StatType))) statVisualState[s] = 0;
            UpdateStatsDisplay();
        }

        if (GUILayout.Button("Sample +5 MoveSpeed (temp 15s)", GUILayout.Height(22)))
        {
            ApplyModifier(StatType.MoveSpeed, 5f, false, true, 15f, false, 0);
            UpdateStatsDisplay();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.EndVertical();

        // === DRAG HANDLING ===
        if (guiDraggable)
        {
            GUI.DragWindow(new Rect(0, 0, guiWindowRect.width - 2, 24));
        }

        // Guardar posición cuando el usuario suelta el mouse
        if (guiPersistPosition && Event.current != null && Event.current.type == EventType.MouseUp)
        {
            SaveGuiPositionToPrefs();
        }
    }

    #endregion

    #region Debug Helpers

    /// <summary>
    /// Asegura los estilos de GUI usados por el OnGUI de debug. Cachea estilos para evitar recrearlos cada frame.
    /// </summary>
    private void EnsureDebugGuiStyles()
    {
        if (guiTitleStyle != null) return;

        guiTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        guiLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = Color.white }
        };

        guiSmallLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = Color.white }
        };

        guiBoxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 6, 6)
        };

        guiButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            padding = new RectOffset(6, 6, 4, 4)
        };
    }

    /// <summary>
    /// Inicializa la rect de la ventana (una sola vez) y carga prefs si aplica.
    /// </summary>
    private void EnsureGuiWindowRectInitialized()
    {
        if (guiWindowRectInitialized) return;

        // Tamaño inicial (clamped respecto pantalla actual)
        guiWindowRect.width = Mathf.Clamp(guiWidth, 200, Mathf.Max(200, Screen.width - 20));
        guiWindowRect.height = Mathf.Clamp(guiHeight, 120, Mathf.Max(120, Screen.height - 20));

        // Intentar cargar posición guardada si está activado y existe
        if (guiPersistPosition && PlayerPrefs.HasKey(guiPrefsKey + "_x") && PlayerPrefs.HasKey(guiPrefsKey + "_y"))
        {
            float px = PlayerPrefs.GetFloat(guiPrefsKey + "_x", guiCustomPosition.x);
            float py = PlayerPrefs.GetFloat(guiPrefsKey + "_y", guiCustomPosition.y);
            guiWindowRect.x = Mathf.Clamp(px, 0, Screen.width - guiWindowRect.width);
            guiWindowRect.y = Mathf.Clamp(py, 0, Screen.height - guiWindowRect.height);
            //guiPositionLoadedFromPrefs = true;
        }
        else
        {
            ApplyGuiAnchorImmediate();
        }

        // Cache pantalla actual
        lastScreenW = Screen.width;
        lastScreenH = Screen.height;

        // Marcar inicializado
        guiWindowRectInitialized = true;

        // Inicial lastSavedGuiPos para evitar salvados innecesarios
        lastSavedGuiPos = new Vector2(guiWindowRect.x, guiWindowRect.y);
    }

    private void ApplyGuiAnchorImmediate()
    {
        switch (guiAnchor)
        {
            case DebugAnchor.TopLeft:
                guiWindowRect.x = guiCornerPadding;
                guiWindowRect.y = guiCornerPadding;
                break;
            case DebugAnchor.TopRight:
                guiWindowRect.x = Mathf.Max(0, Screen.width - guiWindowRect.width - guiCornerPadding);
                guiWindowRect.y = guiCornerPadding;
                break;
            case DebugAnchor.BottomLeft:
                guiWindowRect.x = guiCornerPadding;
                guiWindowRect.y = Mathf.Max(0, Screen.height - guiWindowRect.height - guiCornerPadding);
                break;
            case DebugAnchor.BottomRight:
                guiWindowRect.x = Mathf.Max(0, Screen.width - guiWindowRect.width - guiCornerPadding);
                guiWindowRect.y = Mathf.Max(0, Screen.height - guiWindowRect.height - guiCornerPadding);
                break;
            case DebugAnchor.Custom:
                guiWindowRect.x = Mathf.Clamp(guiCustomPosition.x, 0, Mathf.Max(0, Screen.width - guiWindowRect.width));
                guiWindowRect.y = Mathf.Clamp(guiCustomPosition.y, 0, Mathf.Max(0, Screen.height - guiWindowRect.height));
                break;
        }
    }

    /// <summary>
    /// Persiste la posición actual de la ventana en PlayerPrefs (si guiPersistPosition==true).
    /// </summary>
    private void SaveGuiPositionToPrefs()
    {
        if (!guiPersistPosition || string.IsNullOrEmpty(guiPrefsKey)) return;

        Vector2 cur = new Vector2(guiWindowRect.x, guiWindowRect.y);
        // Solo save si cambió la posición lo suficiente (evitar saves por sub-pixel jitter)
        if (Vector2.Distance(cur, lastSavedGuiPos) > 0.5f)
        {
            PlayerPrefs.SetFloat(guiPrefsKey + "_x", guiWindowRect.x);
            PlayerPrefs.SetFloat(guiPrefsKey + "_y", guiWindowRect.y);
            PlayerPrefs.Save();
            lastSavedGuiPos = cur;
        }
    }

    /// <summary>
    /// Snap rápido a la esquina seleccionada en tiempo de ejecución.
    /// </summary>
    /// <param name="anchor">Ancla objetivo.</param>
    private void SnapGuiTo(DebugAnchor anchor)
    {
        guiAnchor = anchor;
        ApplyGuiAnchorImmediate();
        SaveGuiPositionToPrefs();
    }

    #endregion
}