using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Slot individual del inventario.
/// </summary>
public class InventorySlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    #region Inspector - References

    [Header("Referencias UI")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image rarityBorder;
    [SerializeField] private GameObject temporalEffectObject;
    [SerializeField] private Image pulseGlowImage;

    [Header("Estado Vacío")]
    [Tooltip("Sprite que se muestra cuando el slot no tiene ningún ítem asignado, para indicar visualmente que está vacío.")]
    [SerializeField] private Sprite emptySlotSprite;
    [Tooltip("Color/alpha del icono cuando muestra el sprite de slot vacío.")]
    [SerializeField] private Color emptySlotIconColor = new Color(1f, 1f, 1f, 0.35f);

    #endregion

    #region Inspector - Effect Settings

    [Header("Efectos de Pulso")]
    [Tooltip("Velocidad del pulso para items temporales.")]
    [SerializeField] private float pulseSpeed = 2f;
    [Tooltip("Intensidad del pulso (0.3 = 30% de variacion en alpha)")]
    [SerializeField] private float pulseIntensity = 0.3f;
    [SerializeField] private float hoverScale = 1.2f;

    // [Header("Efecto de Seleccion")]
    // [Tooltip("Escala del icono cuando el slot esta seleccionado (ej: 1.25)")]
    // [SerializeField] private float selectedIconScale = 1.25f;
    // [Tooltip("Duracion del tween de escala en segundos")]
    // [SerializeField] private float selectedScaleDuration = 0.15f;

    #endregion

    #region Internal State

    private ShopItem itemData;
    private InventoryUIManager inventoryManager;
    private Color originalBackgroundColor;
    private Color originalBorderColor;
    private bool hasItem;
    private bool isGoldenSlot;
    private Coroutine pulseCoroutine;

    // Variables de seleccion comentadas
    // private bool isSelected;
    // private Coroutine selectScaleCoroutine;

    #endregion

    #region Public Properties & Events

    /// <summary>
    /// Item actualmente asignado (puede ser null).
    /// </summary>
    public ShopItem CurrentItem => itemData;

    /// <summary>
    /// True si el slot tiene un item.
    /// </summary>
    public bool HasItem => hasItem;

    /// <summary>
    /// RectTransform de este slot.
    /// </summary>
    public RectTransform SlotRect => GetComponent<RectTransform>();

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (hasItem && itemData != null && itemData.isTemporary) ShowTemporalEffect();
    }

    private void OnDisable()
    {
        if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }

        // if (selectScaleCoroutine != null) { StopCoroutine(selectScaleCoroutine); selectScaleCoroutine = null; }

        // Fuerza la limpieza del estado visual cuando el inventario se apaga de golpe.
        ApplyHoverExit();
    }

    #endregion

    #region Initialization & Data Sync

    public void Initialize(InventoryUIManager manager)
    {
        inventoryManager = manager;
        if (backgroundImage != null) originalBackgroundColor = backgroundImage.color;
        if (temporalEffectObject != null) temporalEffectObject.SetActive(false);
        ClearSlot();
    }

    public void SetGolden(bool golden, Color color)
    {
        isGoldenSlot = golden;
        if (backgroundImage != null && golden)
        {
            backgroundImage.color = new Color(color.r, color.g, color.b, 1f);
            originalBackgroundColor = backgroundImage.color;
        }
        if (rarityBorder != null && golden)
        {
            rarityBorder.color = color;
            rarityBorder.enabled = true;
            originalBorderColor = color;
        }
    }

    #endregion

    #region Item Management

    public void SetItem(ShopItem item)
    {
        itemData = item;
        hasItem = item != null;

        if (!hasItem)
        {
            ClearSlot();
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = item.itemIcon;
            iconImage.color = Color.white; // deshace el tinte usado para el sprite de slot vacio
            iconImage.enabled = item.itemIcon != null;
        }

        if (rarityBorder != null)
        {
            rarityBorder.color = isGoldenSlot ? originalBorderColor : item.GetRarityColor();
            rarityBorder.enabled = true;
            if (!isGoldenSlot) originalBorderColor = rarityBorder.color;
        }

        if (item.isTemporary) ShowTemporalEffect();
        else HideTemporalEffect();
    }

    public void ClearSlot()
    {
        itemData = null;
        hasItem = false;

        if (iconImage != null)
        {
            if (emptySlotSprite != null)
            {
                // Muestra un sprite indicativo de "slot vacio" en vez de dejar el icono totalmente en blanco.
                iconImage.sprite = emptySlotSprite;
                iconImage.color = emptySlotIconColor;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        if (!isGoldenSlot && rarityBorder != null)
        {
            rarityBorder.enabled = false;
        }

        HideTemporalEffect();

        // Limpieza de seleccion comentada
        // isSelected = false;
        // if (selectScaleCoroutine != null)
        // {
        //     StopCoroutine(selectScaleCoroutine);
        //     selectScaleCoroutine = null;
        // }

        // if (iconImage != null) iconImage.transform.localScale = Vector3.one;

        // Restaura el color y la escala base para evitar que el estado visual de hover quede atascado tras recargar la UI.
        if (backgroundImage != null) backgroundImage.color = originalBackgroundColor;
        transform.localScale = Vector3.one;
    }

    #endregion

    #region Visual & Audio Effects

    /* Logica de animacion de fijado comentada a peticion
    public void SetSelected(bool selected)
    {
        if (isSelected == selected) return;
        isSelected = selected;

        if (selectScaleCoroutine != null)
        {
            StopCoroutine(selectScaleCoroutine);
            selectScaleCoroutine = null;
        }

        if (iconImage == null) return;

        float targetScale = selected ? selectedIconScale : 1f;
        
        if (gameObject.activeInHierarchy)
        {
            selectScaleCoroutine = StartCoroutine(TweenIconScale(targetScale));
        }
        else
        {
            iconImage.transform.localScale = Vector3.one * targetScale;
        }
    }

    private IEnumerator TweenIconScale(float targetScale)
    {
        if (iconImage == null) yield break;

        Vector3 fromScale = iconImage.transform.localScale;
        Vector3 toScale = Vector3.one * targetScale;
        float elapsed = 0f;

        while (elapsed < selectedScaleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / selectedScaleDuration);
            iconImage.transform.localScale = Vector3.Lerp(fromScale, toScale, t);
            yield return null;
        }

        iconImage.transform.localScale = toScale;
        selectScaleCoroutine = null;
    }
    */

    private void ShowTemporalEffect()
    {
        if (temporalEffectObject != null) temporalEffectObject.SetActive(true);

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        if (gameObject.activeInHierarchy) pulseCoroutine = StartCoroutine(PulseEffect());
    }

    private void HideTemporalEffect()
    {
        if (temporalEffectObject != null) temporalEffectObject.SetActive(false);

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        if (rarityBorder != null && hasItem) rarityBorder.color = originalBorderColor;
    }

    private IEnumerator PulseEffect()
    {
        Color base_ = originalBorderColor;
        while (hasItem && itemData != null && itemData.isTemporary)
        {
            float p = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(1f - pulseIntensity, 1f, p);
            if (rarityBorder != null)
            {
                rarityBorder.color = new Color(base_.r, base_.g, base_.b, alpha);
            }

            if (pulseGlowImage != null)
            {
                pulseGlowImage.color = new Color(1f, 0.27f, 0f, Mathf.Lerp(0.2f, 0.6f, p));
            }
            yield return null;
        }
        pulseCoroutine = null;
    }

    #endregion

    #region Pointer & Hover Interactions

    /// <summary>
    /// Aplica el estado visual y sonoro de "hover enter".
    /// <paramref name="gamepadMode"/> true => el tooltip se posiciona centrado en el slot.
    /// </summary>
    private void ApplyHoverEnter(bool gamepadMode)
    {
        if (!hasItem) return;

        if (backgroundImage != null && inventoryManager != null)
        {
            backgroundImage.color = Color.Lerp(originalBackgroundColor,
                                                inventoryManager.GetHighlightColor(), 0.5f);
        }
        transform.localScale = Vector3.one * hoverScale;

        // Mostrar el panel descriptivo directamente al hacer hover
        inventoryManager?.ShowItemDetails(itemData, gamepadMode ? SlotRect : null);

        if (isGoldenSlot) InventoryAudioManager.Instance?.PlayGoldenSlotHoverSound();
        else InventoryAudioManager.Instance?.PlayCommonSlotHoverSound();
    }

    /// <summary>
    /// Aplica el estado visual de "hover exit" y oculta el panel de detalles.
    /// </summary>
    private void ApplyHoverExit()
    {
        if (backgroundImage != null) backgroundImage.color = originalBackgroundColor;
        transform.localScale = Vector3.one;

        // Ocultar el panel descriptivo al salir del hover
        inventoryManager?.HideItemDetailsIfNotSelected(this);
    }

    /// <summary>
    /// Simula que el cursor del mando entra en este slot.
    /// </summary>
    public void SimulatePointerEnter(bool gamepadMode = true) => ApplyHoverEnter(gamepadMode);

    /// <summary>
    /// Simula que el cursor del mando sale de este slot.
    /// </summary>
    public void SimulatePointerExit()
    {
        ApplyHoverExit();
    }

    /// <summary>
    /// Simula un clic del mando sobre este slot (Button North).
    /// </summary>
    public void SimulateClick()
    {
        if (!hasItem) return;

        // inventoryManager?.OnSlotClicked(this, isGamepad: true);
        InventoryAudioManager.Instance?.PlayClickSound();
        InventoryAudioManager.Instance?.PlayRaritySound(itemData.rarity);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ApplyHoverEnter(gamepadMode: false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!hasItem) return;
        ApplyHoverExit();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!hasItem) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // inventoryManager?.OnSlotClicked(this, isGamepad: false);
        InventoryAudioManager.Instance?.PlayClickSound();
        InventoryAudioManager.Instance?.PlayRaritySound(itemData.rarity);
    }

    #endregion
}