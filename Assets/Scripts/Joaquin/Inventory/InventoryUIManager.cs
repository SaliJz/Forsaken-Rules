using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Gestiona la interfaz principal del inventario.
/// </summary>
public class InventoryUIManager : MonoBehaviour
{
    #region Public Properties & Events

    public static InventoryUIManager Instance { get; private set; }
    public bool IsOpen => isOpen;
    public bool IsConfirmPanelOpen => isConfirmPanelOpen;

    #endregion

    #region Inspector - References

    [Header("Referencias")]
    [Tooltip("Referencia al panel general de items de la interfaz.")]
    [SerializeField] private GameObject itemUIPanel;
    [Tooltip("Referencia al panel de botones de interaccion.")]
    [SerializeField] private GameObject interactionButtonUIPanel;
    [Tooltip("Canvas padre del inventario requerido para posicionar los elementos dinamicos.")]
    [SerializeField] private Canvas inventoryCanvas;

    [Header("Raices de visibilidad")]
    [Tooltip("Contenedor de los 3 slots dorados que siempre se mantienen visibles.")]
    [SerializeField] private Transform goldenSlotsContainer;
    [Tooltip("Contenedor de las columnas expandibles que se ocultan al cerrar el inventario.")]
    [SerializeField] private GameObject inventoryExpandRoot;

    [Header("Formato de Interfaz")]
    [Tooltip("Alterna entre el formato antiguo de 21 slots (activo) y el nuevo de 9 slots (inactivo).")]
    [SerializeField] private bool useLegacyFormat = true;

    [Header("Contenedores de Columnas (Formato Clasico)")]
    [Tooltip("Referencia a los 2 slots ubicados sobre los dorados.")]
    [SerializeField] private Transform col1AboveContainer;
    [Tooltip("Referencia a los 2 slots ubicados debajo de los dorados.")]
    [SerializeField] private Transform col1BelowContainer;
    [Tooltip("Referencia a la segunda columna de slots.")]
    [SerializeField] private Transform column2Container;
    [Tooltip("Referencia a la tercera columna de slots.")]
    [SerializeField] private Transform column3Container;

    [Header("Panel de Detalle")]
    [Tooltip("Objeto principal del panel flotante de detalles del item.")]
    [SerializeField] private GameObject detailPanel;
    [Tooltip("Transform del panel flotante para controlar su posicion en pantalla.")]
    [SerializeField] private RectTransform detailPanelRect;
    [Tooltip("Texto para mostrar el nombre del item seleccionado o enfocado.")]
    [SerializeField] private TextMeshProUGUI detailName;
    [Tooltip("Texto para mostrar la descripcion detallada del item.")]
    [SerializeField] private TextMeshProUGUI detailDescription;
    [Tooltip("Imagen para renderizar el icono del item.")]
    [SerializeField] private Image detailIcon;

    [Header("Panel de Detalle - Posicionamiento (Hover)")]
    [Tooltip("Si esta activo, el panel de detalle se reposiciona dinamicamente siguiendo al cursor (raton) o al slot enfocado (mando) durante el hover. " +
        "Si esta desactivado, el panel solo se activa/desactiva y actualiza su informacion (nombre, descripcion, icono), sin modificar su posicion en pantalla.")]
    [SerializeField] private bool allowDetailPanelHoverPositioning = true;
    [Tooltip("Desplazamiento del panel respecto a la posicion del cursor del raton.")]
    [SerializeField] private Vector2 detailMouseOffset = new Vector2(12f, -12f);
    [Tooltip("Desplazamiento del panel respecto al centro del slot al usar mando.")]
    [SerializeField] private Vector2 detailGamepadOffset = new Vector2(0f, 80f);
    [Tooltip("Tiempo de retraso en segundos antes de mostrar el panel de detalle al hacer hover.")]
    [SerializeField] private float detailShowDelay = 0.25f;

    [Header("Panel de Confirmacion de Reemplazo (Panel Comparativo)")]
    [Tooltip("Panel UI para confirmar el reemplazo de items al comprar o equipar.")]
    [SerializeField] private GameObject replaceConfirmPanel;
    [Tooltip("Texto descriptivo generico (fallback) que indica que item se va a reemplazar. Se usa si no se asignan los campos individuales de nombre/descripcion.")]
    [SerializeField] private TextMeshProUGUI replaceConfirmText;
    [Tooltip("Texto que muestra la categoria de la reliquia en comparacion (A MELÉ / A DISTANCIA / DASH).")]
    [SerializeField] private TextMeshProUGUI comparativeCategoryText;
    [Tooltip("Icono/imagen de la reliquia actualmente equipada (lado izquierdo del panel comparativo).")]
    [SerializeField] private Image comparativeCurrentIcon;
    [Tooltip("Nombre de la reliquia actualmente equipada.")]
    [SerializeField] private TextMeshProUGUI comparativeCurrentName;
    [Tooltip("Descripcion funcional de la reliquia actualmente equipada.")]
    [SerializeField] private TextMeshProUGUI comparativeCurrentDescription;
    [Tooltip("Icono/imagen de la nueva reliquia propuesta (lado derecho del panel comparativo).")]
    [SerializeField] private Image comparativeNewIcon;
    [Tooltip("Nombre de la nueva reliquia propuesta.")]
    [SerializeField] private TextMeshProUGUI comparativeNewName;
    [Tooltip("Descripcion funcional de la nueva reliquia propuesta.")]
    [SerializeField] private TextMeshProUGUI comparativeNewDescription;
    [Tooltip("Boton para aceptar el reemplazo propuesto (Sí).")]
    [SerializeField] private Button confirmReplaceButton;
    [Tooltip("Boton para cancelar el reemplazo y mantener el item actual (No).")]
    [SerializeField] private Button cancelReplaceButton;

    #endregion

    #region Inspector - Settings

    [Header("Slots Dorados")]
    [Tooltip("Color aplicado para resaltar visualmente los slots dorados.")]
    [SerializeField] private Color goldenSlotColor = new Color(1f, 0.84f, 0f);

    [Header("Hover Highlight")]
    [Tooltip("Color aplicado al slot cuando el cursor o mando pasa por encima.")]
    [SerializeField] private Color highlightColor = new Color(0.6f, 0.1f, 0.1f);

    [Header("Panel de Confirmacion - Mando")]
    [Tooltip("Fondo del boton confirmar para iluminarlo al navegar con mando.")]
    [SerializeField] private Image confirmButtonImage;
    [Tooltip("Fondo del boton cancelar para iluminarlo al navegar con mando.")]
    [SerializeField] private Image cancelButtonImage;
    // [Tooltip("Color base de los botones del panel de confirmacion cuando no estan seleccionados.")]
    // [SerializeField] private Color confirmButtonDefaultColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    [Header("Lock Settings")]
    [Tooltip("Define si el jugador tiene permitido cerrar el inventario en el estado actual.")]
    [SerializeField] private bool canCloseInventory = true;

    [Header("Animacion de Reveal")]
    [Tooltip("Tiempo de duracion de la transicion de aparicion para cada slot individual.")]
    [SerializeField] private float slotRevealDuration = 0.12f;
    [Tooltip("Tiempo de espera entre la aparicion de slots consecutivos.")]
    [SerializeField] private float slotStagger = 0.04f;
    [Tooltip("Tiempo de espera antes de iniciar la animacion de la siguiente columna.")]
    [SerializeField] private float columnDelay = 0.08f;
    [Tooltip("Distancia en pixeles del desplazamiento vertical inicial durante la animacion.")]
    [SerializeField] private float slideOffsetY = 35f;
    [Tooltip("Distancia en pixeles del desplazamiento horizontal inicial durante la animacion.")]
    [SerializeField] private float slideOffsetX = 50f;

    [Header("Feedback de item Anadido")]
    [Tooltip("Multiplicador de escala maxima durante la animacion de rebote de un slot.")]
    [SerializeField] private float bouncePeak = 1.2f;
    [Tooltip("Duracion total en segundos de la animacion de rebote.")]
    [SerializeField] private float bounceDuration = 0.25f;

    [Header("Mando - Navegacion")]
    [Tooltip("Tiempo de bloqueo entre movimientos del joystick para evitar desplazamientos multiples indeseados.")]
    [SerializeField] private float joystickRepeatCooldown = 0.2f;

    private float nextToggleTime = 0f;
    private const float InventoryInputCooldown = 0.25f;

    #endregion

    #region Internal State

    private readonly List<InventorySlot> goldenSlots = new List<InventorySlot>();
    private readonly List<InventorySlot> col1AboveSlots = new List<InventorySlot>();
    private readonly List<InventorySlot> col1BelowSlots = new List<InventorySlot>();
    private readonly List<InventorySlot> col2Slots = new List<InventorySlot>();
    private readonly List<InventorySlot> col3Slots = new List<InventorySlot>();

    private readonly List<InventorySlot> topSquareSlots = new List<InventorySlot>();
    private readonly List<InventorySlot> diamondSlots = new List<InventorySlot>();

    private readonly ShopItem[] mechanicSlots = new ShopItem[3];
    private float inventoryPreviousTimeScale = 1f;
    private float confirmPreviousTimeScale = 1f;

    private readonly Dictionary<RectTransform, Vector2> restPositions = new Dictionary<RectTransform, Vector2>();

    private bool isOpen;
    private bool isConfirmPanelOpen;
    private bool wasInventoryOpenBeforeConfirm;
    private int confirmNavIndex;
    private bool confirmPanelJustOpened;
    private ShopItem pendingReplaceItem;
    private ShopManager pendingShopManager;
    private int pendingReplaceSlotIndex;
    private Coroutine revealCoroutine;
    private bool lastInputWasGamepad; // Rastrear que dispositivo abrio el inventario

    // Slot fijado comentado
    // private InventorySlot selectedSlot;

    private bool detailPanelIsGamepadMode;
    private Vector2 detailGamepadAnchorLocal;
    private bool detailShowPending;
    private float detailShowTimer;
    private Camera detailUICamera;

    private List<List<InventorySlot>> navGrid;
    private int navCol;
    private int navRow;
    private InventorySlot focusedSlot;
    private float joystickCooldownTimer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Inicializa el Singleton de forma segura y destruye duplicados.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (inventoryCanvas == null) inventoryCanvas = GetComponentInParent<Canvas>();

        if (detailPanelRect == null && detailPanel != null)
        {
            detailPanelRect = detailPanel.GetComponent<RectTransform>();
        }

        if (inventoryCanvas != null &&
            (inventoryCanvas.renderMode == RenderMode.ScreenSpaceCamera ||
             inventoryCanvas.renderMode == RenderMode.WorldSpace))
        {
            detailUICamera = inventoryCanvas.worldCamera;
        }
    }

    private void Start()
    {
        // Recolecta todos los slots de la escena y establece el estado inicial oculto.
        CollectSlots();
        inventoryExpandRoot?.SetActive(false);
        detailPanel?.SetActive(false);
        detailShowPending = false;
        replaceConfirmPanel?.SetActive(false);

        confirmReplaceButton?.onClick.AddListener(OnConfirmReplace);
        cancelReplaceButton?.onClick.AddListener(OnCancelReplace);

        RefreshDisplay();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextToggleTime) return;

        if (isConfirmPanelOpen)
        {
            UpdateGamepadConfirmNavigation();
            return;
        }

        if (ShouldToggleInventory())
        {
            nextToggleTime = Time.unscaledTime + InventoryInputCooldown;
            ToggleInventory();
            return;
        }

        if (isOpen)
        {
            UpdateGamepadNavigation();
            UpdateDetailPanelHoverPosition();
        }
    }

    /// <summary>
    /// Limpia corrutinas activas y restaura la escala de tiempo si el objeto se desactiva mientras los paneles estan abiertos, previniendo cuelgues del juego.
    /// </summary>
    private void OnDisable()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }

        if (isConfirmPanelOpen)
        {
            Time.timeScale = confirmPreviousTimeScale;
            isConfirmPanelOpen = false;
        }
        else if (isOpen && canCloseInventory)
        {
            Time.timeScale = inventoryPreviousTimeScale;
        }

        isOpen = false;

        // Desfija logica comentada
        // selectedSlot?.SetSelected(false);
        // selectedSlot = null;

        HideItemDetails(); // Limpieza forzada del panel al desactivarse abruptamente
        focusedSlot = null;
    }

    /// <summary>
    /// Evalua los inputs de teclado, mando nativo y Steam Input para determinar si se debe alternar la visibilidad del inventario.
    /// </summary>
    private bool ShouldToggleInventory()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            lastInputWasGamepad = false;
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame)
        {
            lastInputWasGamepad = true;
            return true;
        }

        if (SteamInputManager.Instance != null &&
            SteamInputManager.Instance.GetInventoryPressed())
        {
            lastInputWasGamepad = true;
            return true;
        }

        return false;
    }

    #endregion

    #region Initialization & Data Sync

    /// <summary>
    /// Almacena las referencias de los slots fisicos ubicados en los contenedores de la jerarquia.
    /// </summary>
    private void CollectSlots()
    {
        CollectFromContainer(goldenSlotsContainer, goldenSlots);
        CollectFromContainer(column2Container, col2Slots);
        CollectFromContainer(column3Container, col3Slots);

        if (useLegacyFormat)
        {
            CollectFromContainer(col1AboveContainer, col1AboveSlots);
            CollectFromContainer(col1BelowContainer, col1BelowSlots);
        }
        else
        {
            col1AboveSlots.Clear();
            col1BelowSlots.Clear();
        }

        foreach (var s in goldenSlots) s.SetGolden(true, goldenSlotColor);
    }

    /// <summary>
    /// Itera sobre un contenedor padre, inicializa los slots hijos encontrados y los ordena segun su posicion espacial en la interfaz.
    /// </summary>
    private void CollectFromContainer(Transform container, List<InventorySlot> list)
    {
        if (container == null) return;
        list.Clear();
        foreach (Transform child in container)
        {
            if (!child.gameObject.activeSelf) continue;

            var slot = child.GetComponent<InventorySlot>();
            if (slot == null) continue;

            if (child.GetComponent<CanvasGroup>() == null)
            {
                child.gameObject.AddComponent<CanvasGroup>();
            }

            var rt = child.GetComponent<RectTransform>();
            if (rt != null && !restPositions.ContainsKey(rt))
            {
                restPositions[rt] = rt.anchoredPosition;
            }

            slot.Initialize(this);
            list.Add(slot);
        }

        list.Sort((a, b) =>
        {
            RectTransform rtA = a.GetComponent<RectTransform>();
            RectTransform rtB = b.GetComponent<RectTransform>();

            if (rtA != null && rtB != null)
            {
                int yComparison = rtB.anchoredPosition.y.CompareTo(rtA.anchoredPosition.y);

                if (Mathf.Abs(rtB.anchoredPosition.y - rtA.anchoredPosition.y) > 0.1f)
                {
                    return yComparison;
                }

                return rtA.anchoredPosition.x.CompareTo(rtB.anchoredPosition.x);
            }
            return 0;
        });
    }

    #endregion

    #region Inventory Open/Close Logic

    /// <summary>
    /// Alterna el estado del inventario tras verificar que no existan cinematicas, transiciones u otros eventos que bloqueen la interaccion.
    /// </summary>
    public void ToggleInventory()
    {
        if (PauseController.Instance != null && PauseController.IsGamePaused) return;

        if (!isOpen)
        {
            if (DungeonGenerator.Instance != null && DungeonGenerator.Instance.IsTransitioning) return;
            if (SceneController.Instance != null && SceneController.Instance.IsTransitioning) return;
            if (RoomTransitionTrigger.IsTransitioning) return;
            if (BossIntroDirector.IsPlayingCutscene) return;

            var transitions = FindObjectsByType<TransitionInteractive>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var t in transitions)
            {
                if (t.IsRunning) return;
            }
        }

        if (isOpen) CloseInventory();
        else OpenInventory();
    }

    public void OpenInventory()
    {
        isOpen = true;

        // Limpieza forzada: Oculta el panel para matar cualquier estado fantasma previo antes de abrir.
        HideItemDetails();

        if (canCloseInventory)
        {
            inventoryPreviousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        RefreshDisplay();

        if (itemUIPanel != null && itemUIPanel.activeSelf) itemUIPanel.SetActive(false);
        if (interactionButtonUIPanel != null && interactionButtonUIPanel.activeSelf)
        {
            interactionButtonUIPanel.SetActive(false);
        }

        inventoryExpandRoot?.SetActive(true);

        if (pendingReplaceItem != null)
        {
            pendingShopManager?.SetInteractionPromptActive(isOpen);
            pendingShopManager?.ResetCostBar();
            pendingShopManager?.LockAndDisplayItemDetails(null);
            ReportDebug("Abriendo inventario con una compra pendiente. Mostrando panel de confirmacion.", 1);
        }

        if (revealCoroutine != null) StopCoroutine(revealCoroutine);
        revealCoroutine = StartCoroutine(RevealSequence(opening: true));

        InventoryAudioManager.Instance?.PlayOpenSound();

        BuildNavGrid();

        // Solo aplica auto-foco si se abrio especificamente utilizando un mando.
        if (Gamepad.current != null && lastInputWasGamepad)
        {
            joystickCooldownTimer = 0f;
            focusedSlot = null;
            navCol = 0;
            navRow = 0;
            StartCoroutine(FocusFirstSlotNextFrame());
        }
    }

    public void CloseInventory()
    {
        if (!canCloseInventory)
        {
            Debug.Log("[InventoryUIManager] El cierre del inventario esta bloqueado actualmente.");
            return;
        }

        if (isConfirmPanelOpen)
        {
            var shopRef = pendingShopManager;
            pendingReplaceItem = null;
            pendingShopManager = null;
            shopRef?.RegisterPendingPurchaseCallback(null);
            shopRef?.FireCancelCallback();
            isConfirmPanelOpen = false;
        }

        isOpen = false;
        Time.timeScale = inventoryPreviousTimeScale;

        if (focusedSlot != null)
        {
            focusedSlot.SimulatePointerExit();
            focusedSlot = null;
        }

        // selectedSlot?.SetSelected(false);
        // selectedSlot = null;

        HideItemDetails(); // Limpieza forzada al cerrar
        replaceConfirmPanel?.SetActive(false);

        if (revealCoroutine != null) StopCoroutine(revealCoroutine);
        revealCoroutine = StartCoroutine(RevealSequence(opening: false));

        InventoryAudioManager.Instance?.PlayCloseSound();
    }

    /// <summary>
    /// Retrasa la asignacion del foco inicial del mando por un frame para permitir 
    /// que la cuadricula y animaciones se inicialicen correctamente.
    /// </summary>
    private IEnumerator FocusFirstSlotNextFrame()
    {
        yield return null;
        if (navGrid == null || navGrid.Count == 0) yield break;

        for (int c = 0; c < navGrid.Count; c++)
        {
            int r = FindNearestOccupiedRow(navGrid[c], 0);
            if (r >= 0)
            {
                navCol = c;
                navRow = r;
                NavigateGamepadTo(c, r);
                yield break;
            }
        }
    }

    public void SetCloseLock(bool isLocked)
    {
        canCloseInventory = !isLocked;
    }

    #endregion

    #region Visual & Audio Effects

    /// <summary>
    /// Encadena y coordina las secuencias de escalado y opacidad para mostrar u ocultar las columnas de inventario de forma progresiva.
    /// </summary>
    private IEnumerator RevealSequence(bool opening)
    {
        if (opening)
        {
            InventoryAudioManager.Instance?.PlayBarsExpandSound();

            if (useLegacyFormat)
            {
                var above = StartCoroutine(StaggerSlots(col1AboveSlots, new Vector2(0, -slideOffsetY), opening));
                var below = StartCoroutine(StaggerSlots(col1BelowSlots, new Vector2(0, slideOffsetY), opening));
                yield return above;
                yield return below;
                yield return new WaitForSecondsRealtime(columnDelay);
            }

            yield return StartCoroutine(StaggerSlots(col2Slots, new Vector2(slideOffsetX, 0), opening));
            yield return new WaitForSecondsRealtime(columnDelay);
            yield return StartCoroutine(StaggerSlots(col3Slots, new Vector2(slideOffsetX, 0), opening));
        }
        else
        {
            InventoryAudioManager.Instance?.PlayBarsRetractSound();

            yield return StartCoroutine(StaggerSlots(col3Slots, new Vector2(slideOffsetX, 0), opening));
            yield return new WaitForSecondsRealtime(columnDelay);
            yield return StartCoroutine(StaggerSlots(col2Slots, new Vector2(slideOffsetX, 0), opening));

            if (useLegacyFormat)
            {
                yield return new WaitForSecondsRealtime(columnDelay);
                var above = StartCoroutine(StaggerSlots(col1AboveSlots, new Vector2(0, -slideOffsetY), opening));
                var below = StartCoroutine(StaggerSlots(col1BelowSlots, new Vector2(0, slideOffsetY), opening));
                yield return above;
                yield return below;
            }

            inventoryExpandRoot?.SetActive(false);
        }
        revealCoroutine = null;
    }

    private IEnumerator StaggerSlots(List<InventorySlot> slots, Vector2 hiddenOffset, bool opening)
    {
        var order = opening ? slots : new List<InventorySlot>(slots);
        if (!opening) order.Reverse();

        foreach (var slot in order)
        {
            StartCoroutine(TweenSlot(slot.GetComponent<RectTransform>(), hiddenOffset, opening));
            yield return new WaitForSecondsRealtime(slotStagger);
        }
        yield return new WaitForSecondsRealtime(slotRevealDuration);
    }

    /// <summary>
    /// Interpola la posicion local y la transparencia de un RectTransform especifico a lo largo del tiempo establecido por el RevealSequence.
    /// </summary>
    private IEnumerator TweenSlot(RectTransform rt, Vector2 hiddenOffset, bool opening)
    {
        if (rt == null) yield break;

        var cg = rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();

        if (!restPositions.TryGetValue(rt, out Vector2 restPos))
        {
            restPos = rt.anchoredPosition;
            restPositions[rt] = restPos;
        }

        Vector2 fromPos = opening ? restPos + hiddenOffset : restPos;
        Vector2 toPos = opening ? restPos : restPos + hiddenOffset;
        float fromAlpha = opening ? 0f : 1f;
        float toAlpha = opening ? 1f : 0f;

        rt.anchoredPosition = fromPos;
        cg.alpha = fromAlpha;

        float elapsed = 0f;
        while (elapsed < slotRevealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slotRevealDuration);
            rt.anchoredPosition = Vector2.Lerp(fromPos, toPos, t);
            cg.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            yield return null;
        }

        rt.anchoredPosition = toPos;
        cg.alpha = toAlpha;
    }

    public void NotifyItemAdded(ShopItem item)
    {
        RefreshDisplay();

        if (isOpen)
        {
            var slot = FindSlotWithItem(item);
            if (slot != null) StartCoroutine(BounceTransform(slot.transform));
        }
        else
        {
            var goldenSlot = goldenSlots.Find(gs => gs.CurrentItem == item);
            if (goldenSlot != null) StartCoroutine(BounceTransform(goldenSlot.transform));
        }
        InventoryAudioManager.Instance?.PlayItemAddedSound();
    }

    private IEnumerator BounceTransform(Transform t)
    {
        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float scale = 1f + Mathf.Sin(elapsed / bounceDuration * Mathf.PI) * (bouncePeak - 1f);
            t.localScale = Vector3.one * scale;
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    #endregion

    #region Display & Item Management

    /// <summary>
    /// Limpia todos los contenedores visuales y los vuelve a poblar iterando sobre la lista actual de items almacenada en el InventoryManager.
    /// </summary>
    public void RefreshDisplay()
    {
        goldenSlots.RemoveAll(s => s == null);
        col2Slots.RemoveAll(s => s == null);
        col3Slots.RemoveAll(s => s == null);
        col1AboveSlots.RemoveAll(s => s == null);
        col1BelowSlots.RemoveAll(s => s == null);

        foreach (var s in goldenSlots) s.ClearSlot();
        foreach (var s in col2Slots) s.ClearSlot();
        foreach (var s in col3Slots) s.ClearSlot();

        if (useLegacyFormat)
        {
            foreach (var s in col1AboveSlots) s.ClearSlot();
            foreach (var s in col1BelowSlots) s.ClearSlot();
        }

        RebuildMechanicState();

        for (int i = 0; i < goldenSlots.Count && i < mechanicSlots.Length; i++)
        {
            goldenSlots[i].SetGolden(true, goldenSlotColor);
            if (mechanicSlots[i] != null) goldenSlots[i].SetItem(mechanicSlots[i]);
        }

        var passives = CollectPassives();
        int p = 0;

        if (useLegacyFormat)
        {
            foreach (var s in col1AboveSlots) { if (p >= passives.Count) break; s.SetItem(passives[p++]); }
            foreach (var s in col1BelowSlots) { if (p >= passives.Count) break; s.SetItem(passives[p++]); }
        }

        foreach (var s in col2Slots) { if (p >= passives.Count) break; s.SetItem(passives[p++]); }
        foreach (var s in col3Slots) { if (p >= passives.Count) break; s.SetItem(passives[p++]); }
    }

    private void RebuildMechanicState()
    {
        for (int i = 0; i < mechanicSlots.Length; i++) mechanicSlots[i] = null;
        foreach (var item in InventoryManager.CurrentRunItems)
        {
            int idx = GetMechanicSlotIndex(item);
            if (idx >= 0) mechanicSlots[idx] = item;
        }
    }

    private List<ShopItem> CollectPassives()
    {
        var list = new List<ShopItem>();
        foreach (var item in InventoryManager.CurrentRunItems)
        {
            if (GetMechanicSlotIndex(item) < 0) list.Add(item);
        }
        return list;
    }

    private InventorySlot FindSlotWithItem(ShopItem item)
    {
        if (useLegacyFormat)
        {
            foreach (var list in new[] { col1AboveSlots, col1BelowSlots, col2Slots, col3Slots })
            {
                foreach (var s in list) if (s.CurrentItem == item) return s;
            }
        }
        else
        {
            foreach (var list in new[] { topSquareSlots, diamondSlots })
            {
                foreach (var s in list) if (s.CurrentItem == item) return s;
            }
        }

        foreach (var s in goldenSlots)
        {
            if (s.CurrentItem == item) return s;
        }
        return null;
    }

    #endregion

    #region Mechanic Slots & Replace Logic

    /// <summary>
    /// Traduce la enumeracion del tipo de efecto de un ShopItem a un indice entero correspondiente a su ranura especifica.
    /// </summary>
    public int GetMechanicSlotIndex(ShopItem item)
    {
        if (item == null || !item.hasEffectCategory) return -1;
        return item.effectCategory switch
        {
            TypeEffect.Melee => 0,
            TypeEffect.Shield => 1,
            TypeEffect.Dash => 2,
            _ => -1
        };
    }

    /// <summary>
    /// Verifica si un slot de equipamiento esta ocupado durante una compra y despliega la confirmacion de reemplazo si es necesario.
    /// </summary>
    public bool RequestMechanicItemPurchase(ShopItem newItem, int slotIndex, ShopManager caller)
    {
        RebuildMechanicState();

        if (mechanicSlots[slotIndex] != null)
        {
            pendingReplaceItem = newItem;
            pendingReplaceSlotIndex = slotIndex;
            pendingShopManager = caller;
            ShowReplaceConfirm(newItem, mechanicSlots[slotIndex]);
            ReportDebug($"Compra solicitada ocupa un espacio lleno. Desplegando confirmacion de reemplazo.", 1);
            return false;
        }
        return true;
    }

    private void ShowReplaceConfirm(ShopItem newItem, ShopItem current)
    {
        if (replaceConfirmPanel == null)
        {
            ReportDebug("Falta asignar el panel de confirmacion en el inspector.", 2);
            return;
        }

        wasInventoryOpenBeforeConfirm = isOpen;
        isOpen = true;

        if (itemUIPanel != null && itemUIPanel.activeSelf) itemUIPanel.SetActive(false);
        if (interactionButtonUIPanel != null && interactionButtonUIPanel.activeSelf)
        {
            interactionButtonUIPanel.SetActive(false);
        }

        replaceConfirmPanel.SetActive(true);
        confirmPreviousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        isConfirmPanelOpen = true;
        confirmNavIndex = 0;
        confirmPanelJustOpened = true;
        ResetConfirmButtonHighlights();

        // Texto generico de respaldo (por si el prefab del panel no tiene los campos individuales asignados).
        if (replaceConfirmText != null)
        {
            replaceConfirmText.text =
                $"Reemplazar <b>{current.itemName}</b> con <b>{newItem.itemName}</b>?\n" +
                "El item anterior sera descartado.";
        }

        // Panel Comparativo: categoria + datos funcionales de ambas reliquias, sin narrativa.
        if (comparativeCategoryText != null)
        {
            comparativeCategoryText.text = GetCategoryLabel(newItem.effectCategory);
        }

        if (comparativeCurrentIcon != null)
        {
            comparativeCurrentIcon.sprite = current.itemIcon;
            comparativeCurrentIcon.enabled = current.itemIcon != null;
        }
        if (comparativeCurrentName != null) comparativeCurrentName.text = current.itemName;
        if (comparativeCurrentDescription != null) comparativeCurrentDescription.text = current.GetFormattedDescriptionOnly();

        if (comparativeNewIcon != null)
        {
            comparativeNewIcon.sprite = newItem.itemIcon;
            comparativeNewIcon.enabled = newItem.itemIcon != null;
        }
        if (comparativeNewName != null) comparativeNewName.text = newItem.itemName;
        if (comparativeNewDescription != null) comparativeNewDescription.text = newItem.GetFormattedDescriptionOnly();
    }

    /// <summary>
    /// Traduce la categoria de efecto de una reliquia mecanica a la etiqueta mostrada en el
    /// Panel Comparativo (A MELÉ / A DISTANCIA / DASH).
    /// </summary>
    private string GetCategoryLabel(TypeEffect category)
    {
        return category switch
        {
            TypeEffect.Melee => "A MELÉ",
            TypeEffect.Shield => "A DISTANCIA",
            TypeEffect.Dash => "DASH",
            _ => category.ToString()
        };
    }

    private void OnConfirmReplace()
    {
        if (pendingReplaceItem != null)
        {
            ShopItem oldItem = mechanicSlots[pendingReplaceSlotIndex];
            ReportDebug($"Reemplazo ejecutado: {oldItem.itemName} por {pendingReplaceItem.itemName}.", 1);
            pendingShopManager?.ExecuteReplacement(oldItem, pendingReplaceItem);
            pendingReplaceItem = null;
            pendingShopManager = null;
            RefreshDisplay();
        }

        isConfirmPanelOpen = false;
        replaceConfirmPanel?.SetActive(false);

        if (!wasInventoryOpenBeforeConfirm)
        {
            isOpen = false;
        }

        Time.timeScale = confirmPreviousTimeScale;
    }

    private void OnCancelReplace()
    {
        ReportDebug("Reemplazo de item cancelado por el usuario.", 1);

        var shopRef = pendingShopManager;
        pendingReplaceItem = null;
        pendingShopManager = null;
        shopRef?.RegisterPendingPurchaseCallback(null);
        shopRef?.FireCancelCallback();

        isConfirmPanelOpen = false;
        replaceConfirmPanel?.SetActive(false);

        if (!wasInventoryOpenBeforeConfirm)
        {
            isOpen = false;
        }

        Time.timeScale = confirmPreviousTimeScale;
    }

    public int GetNormalSlotsCount()
    {
        int count = col2Slots.Count + col3Slots.Count;
        if (useLegacyFormat)
        {
            count += col1AboveSlots.Count + col1BelowSlots.Count;
        }
        return count;
    }

    #endregion

    #region Detail Panel Logic

    /* Logica de fijado por clic comentada a peticion
    public void OnSlotClicked(InventorySlot slot, bool isGamepad = false)
    {
        ...
    }

    private void PinItemDetails(ShopItem item)
    {
        ...
    }
    */

    /// <summary>
    /// Inicia el temporizador para desplegar el panel flotante y formatea la informacion del item al pasar el puntero o enfocar con mando.
    /// </summary>
    public void ShowItemDetails(ShopItem item, RectTransform slotRect = null)
    {
        if (detailPanel == null || item == null) return;

        if (detailPanelRect == null)
        {
            ReportDebug("detailPanelRect nulo, imposible alinear al puntero.", 2);
            detailPanel.SetActive(true);
            if (detailName != null) { detailName.text = item.itemName; detailName.color = item.GetRarityColor(); }
            if (detailDescription != null) detailDescription.text = item.GetFormattedDescriptionAndStats();
            if (detailIcon != null) { detailIcon.sprite = item.itemIcon; detailIcon.enabled = item.itemIcon != null; }
            return;
        }

        detailPanelIsGamepadMode = allowDetailPanelHoverPositioning && slotRect != null;

        if (detailPanelIsGamepadMode)
        {
            if (inventoryCanvas != null) CalculateGamepadAnchor(slotRect);
            else ReportDebug("inventoryCanvas nulo, imposible anclar con mando.", 2);
        }

        if (detailName != null) { detailName.text = item.itemName; detailName.color = item.GetRarityColor(); }
        if (detailDescription != null) detailDescription.text = item.GetFormattedDescriptionAndStats();
        if (detailIcon != null) { detailIcon.sprite = item.itemIcon; detailIcon.enabled = item.itemIcon != null; }

        if (detailPanel.activeSelf)
        {
            detailShowPending = false;
            if (allowDetailPanelHoverPositioning) UpdateDetailPanelPosition();
            return;
        }

        detailShowPending = true;
        detailShowTimer = 0f;

        if (detailShowDelay <= 0f)
        {
            detailShowPending = false;
            detailPanel.SetActive(true);

            if (allowDetailPanelHoverPositioning)
            {
                Canvas.ForceUpdateCanvases();
                if (detailPanelRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(detailPanelRect);
                UpdateDetailPanelPosition();
            }
        }
    }

    public void HideItemDetails()
    {
        detailPanel?.SetActive(false);
        // selectedSlot = null; // Comentado
        detailShowPending = false;
        detailShowTimer = 0f;
    }

    public void HideItemDetailsIfNotSelected(InventorySlot slot)
    {
        // Validaciones de seleccionado comentadas a peticion
        // if (selectedSlot != null && selectedSlot == slot) return;
        // if (selectedSlot != null) return;

        detailPanel?.SetActive(false);
        detailShowPending = false;
        detailShowTimer = 0f;
    }

    /// <summary>
    /// Controla la logica de retardo y llama a la actualizacion de posicionamiento del panel de detalles 
    /// en cada frame mientras permanezca visible.
    /// </summary>
    private void UpdateDetailPanelHoverPosition()
    {
        if (detailPanel == null) return;

        // if (selectedSlot != null) return; // Comentado a peticion

        if (detailShowPending)
        {
            detailShowTimer += Time.unscaledDeltaTime;
            if (detailShowTimer >= detailShowDelay)
            {
                detailShowPending = false;
                detailPanel.SetActive(true);

                if (allowDetailPanelHoverPositioning)
                {
                    Canvas.ForceUpdateCanvases();
                    if (detailPanelRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(detailPanelRect);
                    UpdateDetailPanelPosition();
                }
            }
            return;
        }

        if (allowDetailPanelHoverPositioning && detailPanel.activeSelf)
        {
            UpdateDetailPanelPosition();
        }
    }

    /// <summary>
    /// Aplica dinamicamente las transformaciones de posicion al panel dependiendo del periferico de entrada activo 
    /// (raton o mando) respetando los margenes.
    /// </summary>
    private void UpdateDetailPanelPosition()
    {
        if (detailPanelRect == null || inventoryCanvas == null) return;

        if (detailPanelIsGamepadMode)
        {
            detailPanelRect.localPosition = detailGamepadAnchorLocal + detailGamepadOffset;
        }
        else
        {
            Camera cam = (inventoryCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : detailUICamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                inventoryCanvas.transform as RectTransform,
                Input.mousePosition,
                cam,
                out Vector2 local);
            detailPanelRect.localPosition = local + detailMouseOffset;
        }

        ClampRectToCanvas(detailPanelRect);
    }

    private void CalculateGamepadAnchor(RectTransform slotRect)
    {
        if (slotRect == null)
        {
            ReportDebug("slotRect nulo en CalculateGamepadAnchor, manteniendo anclaje previo.", 2);
            return;
        }
        if (inventoryCanvas == null) return;

        Camera cam = (inventoryCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : detailUICamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, slotRect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            inventoryCanvas.transform as RectTransform,
            screenPoint,
            cam,
            out detailGamepadAnchorLocal);
    }

    /// <summary>
    /// Calcula las posiciones de los vertices del objeto flotante y del canvas 
    /// para corregir las coordenadas si este excede los limites de la pantalla.
    /// </summary>
    private void ClampRectToCanvas(RectTransform rect)
    {
        if (rect == null || inventoryCanvas == null) return;

        var corners = new Vector3[4];
        var canvasCorners = new Vector3[4];
        rect.GetWorldCorners(corners);
        (inventoryCanvas.transform as RectTransform).GetWorldCorners(canvasCorners);

        Vector3 pos = rect.localPosition;
        if (corners[2].x > canvasCorners[2].x) pos.x -= corners[2].x - canvasCorners[2].x;
        else if (corners[0].x < canvasCorners[0].x) pos.x += canvasCorners[0].x - corners[0].x;

        if (corners[1].y > canvasCorners[1].y) pos.y -= corners[1].y - canvasCorners[1].y;
        else if (corners[0].y < canvasCorners[0].y) pos.y += canvasCorners[0].y - corners[0].y;
        rect.localPosition = pos;
    }

    #endregion

    #region Gamepad Navigation

    /// <summary>
    /// Genera la matriz matricial de navegacion agrupando las listas visuales 
    /// en columnas logicas para que el mando pueda moverse entre ellas.
    /// </summary>
    private void BuildNavGrid()
    {
        navGrid = new List<List<InventorySlot>>();

        var col0 = new List<InventorySlot>();
        if (useLegacyFormat) col0.AddRange(col1AboveSlots);
        col0.AddRange(goldenSlots);
        if (useLegacyFormat) col0.AddRange(col1BelowSlots);

        navGrid.Add(col0);
        navGrid.Add(new List<InventorySlot>(col2Slots));
        navGrid.Add(new List<InventorySlot>(col3Slots));
    }

    /// <summary>
    /// Captura y aplica intervalos a la lectura direccional del joystick o D-pad 
    /// para emular un comportamiento paso a paso en la matriz.
    /// </summary>
    private void UpdateGamepadNavigation()
    {
        if (Gamepad.current == null || navGrid == null) return;

        joystickCooldownTimer -= Time.unscaledDeltaTime;

        int dCol = 0, dRow = 0;

        if (Gamepad.current.dpad.up.wasPressedThisFrame) dRow = -1;
        else if (Gamepad.current.dpad.down.wasPressedThisFrame) dRow = +1;
        else if (Gamepad.current.dpad.left.wasPressedThisFrame) dCol = -1;
        else if (Gamepad.current.dpad.right.wasPressedThisFrame) dCol = +1;

        if (dCol == 0 && dRow == 0 && joystickCooldownTimer <= 0f)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            if (stick.y > 0.5f) { dRow = -1; joystickCooldownTimer = joystickRepeatCooldown; }
            else if (stick.y < -0.5f) { dRow = +1; joystickCooldownTimer = joystickRepeatCooldown; }
            else if (stick.x < -0.5f) { dCol = -1; joystickCooldownTimer = joystickRepeatCooldown; }
            else if (stick.x > 0.5f) { dCol = +1; joystickCooldownTimer = joystickRepeatCooldown; }
        }

        if (dCol != 0 || dRow != 0)
        {
            NavigateGamepadTo(navCol + dCol, navRow + dRow);
        }

        if (Gamepad.current.buttonNorth.wasPressedThisFrame && focusedSlot != null)
        {
            focusedSlot.SimulateClick();
        }
    }

    /// <summary>
    /// Evalua los comandos de direccion entrantes y translada la seleccion visual actual a un nuevo indice 
    /// garantizando que caiga sobre un elemento util.
    /// </summary>
    private void NavigateGamepadTo(int col, int row)
    {
        if (navGrid == null || navGrid.Count == 0) return;

        col = Mathf.Clamp(col, 0, navGrid.Count - 1);
        List<InventorySlot> column = navGrid[col];
        if (column.Count == 0) return;

        int dRow = (row > navRow) ? 1 : (row < navRow) ? -1 : 0;
        int dCol = (col > navCol) ? 1 : (col < navCol) ? -1 : 0;

        int targetRow = Mathf.Clamp(row, 0, column.Count - 1);

        if (dCol != 0)
        {
            targetRow = FindNearestOccupiedRow(column, navRow);
            if (targetRow < 0) return;
        }
        else if (dRow != 0)
        {
            targetRow = FindNextOccupiedRow(column, navRow, dRow);
            if (targetRow < 0) return;
        }

        if (col == navCol && targetRow == navRow && focusedSlot == column[targetRow]) return;

        bool oldIsGolden = focusedSlot != null && goldenSlots.Contains(focusedSlot);
        if (focusedSlot != null) focusedSlot.SimulatePointerExit();

        navCol = col;
        navRow = targetRow;
        focusedSlot = column[navRow];

        bool newIsGolden = goldenSlots.Contains(focusedSlot);
        focusedSlot.SimulatePointerEnter(gamepadMode: true);

        if (oldIsGolden && newIsGolden)
        {
            InventoryAudioManager.Instance?.PlaySwitchGoldenSlotSound();
        }
    }

    /// <summary>
    /// Escanea bidireccionalmente el eje Y de una columna partiendo de un punto de origen para localizar el slot ocupado mas inmediato.
    /// </summary>
    private int FindNearestOccupiedRow(List<InventorySlot> column, int fromRow)
    {
        if (column == null || column.Count == 0) return -1;
        int clamped = Mathf.Clamp(fromRow, 0, column.Count - 1);
        if (column[clamped].HasItem) return clamped;

        for (int offset = 1; offset < column.Count; offset++)
        {
            int up = clamped - offset;
            int down = clamped + offset;
            if (up >= 0 && column[up].HasItem) return up;
            if (down < column.Count && column[down].HasItem) return down;
        }
        return -1;
    }

    /// <summary>
    /// Escanea unicamente en una direccion de la columna Y para buscar el siguiente slot equipado ignorando las celdas vacias.
    /// </summary>
    private int FindNextOccupiedRow(List<InventorySlot> column, int fromRow, int dir)
    {
        for (int i = fromRow + dir; i >= 0 && i < column.Count; i += dir)
        {
            if (column[i].HasItem) return i;
        }
        return -1;
    }

    /// <summary>
    /// Restringe la lectura del mando exclusivamente a opciones binarias izquierda o derecha para controlar los botones de advertencia.
    /// </summary>
    private void UpdateGamepadConfirmNavigation()
    {
        if (Gamepad.current == null) return;

        if (confirmPanelJustOpened)
        {
            confirmPanelJustOpened = false;
            return;
        }

        joystickCooldownTimer -= Time.unscaledDeltaTime;

        int delta = 0;

        if (Gamepad.current.dpad.left.wasPressedThisFrame || Gamepad.current.dpad.right.wasPressedThisFrame)
        {
            delta = 1;
        }
        else if (joystickCooldownTimer <= 0f)
        {
            float x = Gamepad.current.leftStick.ReadValue().x;
            if (Mathf.Abs(x) > 0.5f)
            {
                delta = 1;
                joystickCooldownTimer = joystickRepeatCooldown;
            }
        }

        if (delta != 0)
        {
            confirmNavIndex = 1 - confirmNavIndex;
            ApplyConfirmButtonHighlight(confirmNavIndex);
        }

        bool pressed = Gamepad.current.buttonSouth.wasPressedThisFrame
                    || Gamepad.current.buttonNorth.wasPressedThisFrame;
        if (pressed)
        {
            if (confirmNavIndex == 0) OnConfirmReplace();
            else OnCancelReplace();
        }
    }

    private void ApplyConfirmButtonHighlight(int focusIndex)
    {
        // if (confirmButtonImage != null)
        // {
        //     confirmButtonImage.color = (focusIndex == 0) ? highlightColor : confirmButtonDefaultColor;
        // }
        // if (cancelButtonImage != null)
        // {
        //     cancelButtonImage.color = (focusIndex == 1) ? highlightColor : confirmButtonDefaultColor;
        // }

        Button target = (focusIndex == 0) ? confirmReplaceButton : cancelReplaceButton;
        if (target != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }

    private void ResetConfirmButtonHighlights()
    {
        // if (confirmButtonImage != null) confirmButtonImage.color = confirmButtonDefaultColor;
        // if (cancelButtonImage != null) cancelButtonImage.color = confirmButtonDefaultColor;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    #endregion

    #region Helpers

    public Color GetHighlightColor() => highlightColor;

    // Propiedad y metodo comentados a peticion
    // public bool HasSelectedSlot => selectedSlot != null;
    // public void ClearSelectedSlotIfThis(InventorySlot slot) { ... }

    #endregion

    #region Debugging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1: Debug.Log($"[InventoryUIManager] {message}"); break;
            case 2: Debug.LogWarning($"[InventoryUIManager] {message}"); break;
            case 3: Debug.LogError($"[InventoryUIManager] {message}"); break;
            default: Debug.Log($"[InventoryUIManager] {message}"); break;
        }
    }

    #endregion
}