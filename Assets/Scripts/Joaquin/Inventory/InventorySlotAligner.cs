using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Configura el VerticalLayoutGroup de cada columna del inventario.
/// </summary>
[RequireComponent(typeof(VerticalLayoutGroup))]
public class InventorySlotAligner : MonoBehaviour
{
    [Header("Tamaño de Celda")]
    [SerializeField] private float cellSize = 80f;
    [SerializeField] private float spacing = 8f;

    private VerticalLayoutGroup layoutGroup;

    private void Awake()
    {
        layoutGroup = GetComponent<VerticalLayoutGroup>();
        Configure();
    }

    private void Configure()
    {
        if (layoutGroup == null) return;
        layoutGroup.spacing = spacing;
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = false;

        // Fuerza tamaño de cada hijo
        foreach (RectTransform child in transform)
        {
            child.sizeDelta = new Vector2(cellSize, cellSize);
        }
    }

    [ContextMenu("Recalculate")]
    public void Recalculate()
    {
        Configure();
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }
}