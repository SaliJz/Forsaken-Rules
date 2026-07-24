using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sistema de tooltip que aparece al pasar el cursor sobre un item del inventario.
/// </summary>
public class InventoryTooltip : MonoBehaviour
{
    #region Public Properties & Events

    public static InventoryTooltip Instance { get; private set; }

    #endregion

    #region Inspector - References

    [Header("Referencias")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipTitle;
    [SerializeField] private TextMeshProUGUI tooltipDescription;
    [SerializeField] private RectTransform tooltipRectTransform;

    #endregion

    #region Inspector - Settings

    [Header("Configuracion")]
    [Tooltip("Desplazamiento del tooltip respecto al cursor (modo mouse)")]
    [SerializeField] private Vector2 mouseOffset = new Vector2(12f, -12f);
    [Tooltip("Desplazamiento del tooltip respecto al centro del slot (modo mando)")]
    [SerializeField] private Vector2 gamepadOffset = new Vector2(0f, 80f);
    [SerializeField] private float showDelay = 0.25f;

    #endregion

    #region Internal State

    private Canvas parentCanvas;
    private Camera uiCamera;
    private float showTimer;
    private bool isShowing;
    private bool isGamepadMode;
    private Vector2 gamepadAnchorLocal; // posicion del slot en espacio canvas local

    /// <summary>
    /// Cuando el panel descriptivo esta abierto el tooltip no debe mostrarse.
    /// </summary>
    private bool detailPanelOpen;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null &&
            (parentCanvas.renderMode == RenderMode.ScreenSpaceCamera ||
             parentCanvas.renderMode == RenderMode.WorldSpace))
        {
            uiCamera = parentCanvas.worldCamera;
        }

        tooltipPanel?.SetActive(false);
    }

    private void Update()
    {
        if (!isShowing) return;
        if (detailPanelOpen) return; // suprimido mientras haya panel descriptivo abierto

        showTimer += Time.unscaledDeltaTime;
        if (showTimer >= showDelay)
        {
            tooltipPanel?.SetActive(true);
        }

        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            UpdatePosition();
        }
    }

    #endregion

    #region Tooltip Control

    /// <summary>Muestra el tooltip siguiendo al cursor del raton.</summary>
    public void Show(ShopItem item)
    {
        if (item == null) return;
        isGamepadMode = false;
        SetupContent(item);
    }

    /// <summary>
    /// Muestra el tooltip centrado sobre el slot indicado (modo mando).
    /// Si el panel descriptivo esta abierto, no lo muestra.
    /// </summary>
    public void ShowForSlot(ShopItem item, RectTransform slotRect)
    {
        if (item == null) return;
        if (slotRect != null && parentCanvas != null)
        {
            Camera cam = (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : uiCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, slotRect.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                screenPoint,
                cam,
                out gamepadAnchorLocal);
        }
        isGamepadMode = true;
        SetupContent(item);
    }

    /// <summary>Oculta el tooltip inmediatamente.</summary>
    public void Hide()
    {
        isShowing = false;
        isGamepadMode = false;
        showTimer = 0f;
        tooltipPanel?.SetActive(false);
    }

    /// <summary>
    /// Llama a este metodo cuando el panel descriptivo se abre (true) o cierra (false).
    /// Mientras este abierto, el tooltip permanece oculto y no puede mostrarse.
    /// </summary>
    public void SetDetailPanelOpen(bool open)
    {
        detailPanelOpen = open;
        if (open)
        {
            tooltipPanel?.SetActive(false);
        }
    }

    #endregion

    #region Internal Logic

    private void SetupContent(ShopItem item)
    {
        isShowing = true;
        showTimer = 0f;
        tooltipPanel?.SetActive(false); // reset para respetar el delay

        if (tooltipTitle != null)
        {
            tooltipTitle.text = item.itemName;
            tooltipTitle.color = item.GetRarityColor();
        }

        if (tooltipDescription != null)
        {
            string desc = item.description;
            if (item.isTemporary) desc += "\n<color=orange>TEMPORAL</color>";
            tooltipDescription.text = desc;
        }

        Canvas.ForceUpdateCanvases();
        if (tooltipRectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);
        }
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (tooltipRectTransform == null || parentCanvas == null) return;

        if (isGamepadMode)
        {
            // Centrado sobre el slot con offset configurable
            tooltipRectTransform.localPosition = gamepadAnchorLocal + gamepadOffset;
        }
        else
        {
            // Sigue al cursor del raton
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                Input.mousePosition,
                uiCamera,
                out Vector2 local);
            tooltipRectTransform.localPosition = local + mouseOffset;
        }

        ClampToCanvas();
    }

    private void ClampToCanvas()
    {
        if (tooltipRectTransform == null) return;

        var corners = new Vector3[4];
        var canvasCorners = new Vector3[4];
        tooltipRectTransform.GetWorldCorners(corners);
        (parentCanvas.transform as RectTransform).GetWorldCorners(canvasCorners);

        Vector3 pos = tooltipRectTransform.localPosition;

        if (corners[2].x > canvasCorners[2].x) pos.x -= corners[2].x - canvasCorners[2].x;
        else if (corners[0].x < canvasCorners[0].x) pos.x += canvasCorners[0].x - corners[0].x;

        if (corners[1].y > canvasCorners[1].y) pos.y -= corners[1].y - canvasCorners[1].y;
        else if (corners[0].y < canvasCorners[0].y) pos.y += canvasCorners[0].y - corners[0].y;

        tooltipRectTransform.localPosition = pos;
    }

    #endregion
}