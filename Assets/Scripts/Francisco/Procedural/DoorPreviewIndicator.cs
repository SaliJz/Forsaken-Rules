using UnityEngine;
using TMPro;

public class DoorPreviewIndicator : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private DoorPreviewData previewData;

    [Header("References")]
    [SerializeField] private MeshRenderer doorRenderer;
    [SerializeField] private Light doorLight;
    [SerializeField] private TextMeshPro roomTypeText;
    [SerializeField] private GameObject iconContainer;

    private Material doorMaterial;
    private GameObject currentActiveIcon;

    void Awake()
    {
        if (doorRenderer != null)
            doorMaterial = doorRenderer.material;

        HideAllIcons();
    }

    void Start()
    {
        ShowBaseColor();
    }

    public void SetRoomType(RoomType roomType)
    {
        if (previewData == null) return;
        var entry = previewData.GetEntry(roomType);
        if (entry != null) UpdateVisuals(entry.color, entry.displayName, entry.icon);
    }

    public void ShowBaseColor()
    {
        if (previewData == null) return;
        UpdateVisuals(previewData.baseColor, previewData.baseDisplayName, null);
    }

    private void UpdateVisuals(Color color, string label, GameObject icon)
    {
        if (doorMaterial != null)
        {
            doorMaterial.SetColor("_EmissionColor", color * previewData.emissionIntensity);
            doorMaterial.EnableKeyword("_EMISSION");
        }

        if (doorLight != null)
        {
            doorLight.color = color;
            doorLight.enabled = true;
        }

        if (previewData.showText && roomTypeText != null)
        {
            roomTypeText.text = label;
            roomTypeText.color = color;
            roomTypeText.gameObject.SetActive(true);
        }

        if (previewData.showIcon)
        {
            HideAllIcons();
            if (icon != null)
            {
                icon.SetActive(true);
                currentActiveIcon = icon;
            }
            if (iconContainer != null)
                iconContainer.SetActive(icon != null);
        }
    }

    public void ClearPreview()
    {
        if (doorMaterial != null)
        {
            doorMaterial.SetColor("_EmissionColor", Color.black);
            doorMaterial.DisableKeyword("_EMISSION");
        }

        if (doorLight != null) doorLight.enabled = false;
        if (roomTypeText != null) roomTypeText.gameObject.SetActive(false);

        HideAllIcons();
    }

    private void HideAllIcons()
    {
        if (currentActiveIcon != null)
        {
            currentActiveIcon.SetActive(false);
            currentActiveIcon = null;
        }
        if (iconContainer != null)
            iconContainer.SetActive(false);
    }
}