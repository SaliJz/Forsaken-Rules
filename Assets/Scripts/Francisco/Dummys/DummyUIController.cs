using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DummyUIController : MonoBehaviour, ICombatDummyUI
{
    [Header("Health State UI")]
    [SerializeField] private GameObject healthBarGroup;
    [SerializeField] private Slider healthSlider;

    [Header("Hit Count UI")]
    [SerializeField] private GameObject hitCountGroup;
    [SerializeField] private TMP_Text hitCountText;

    [Header("Super Armor UI")]
    [SerializeField] private GameObject armorBarGroup; 
    [SerializeField] private Slider armorSlider;

    public void UpdateHealthBar(float currentHealthRatio, Color? stateColor)
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentHealthRatio;
        }
    }

    public void UpdateHitCounter(int currentHits, int requiredHits)
    {
        float ratio = (float)currentHits / requiredHits;

        if (healthSlider != null)
        {
            healthSlider.value = ratio;
        }

        if (hitCountText != null)
        {
            hitCountText.text = $"Golpes: {currentHits}/{requiredHits}";
        }
    }

    public void UpdateArmorBar(float currentArmorRatio)
    {
        if (armorSlider != null)
        {
            armorSlider.value = currentArmorRatio;
        }

        if (armorBarGroup != null)
        {
            armorBarGroup.SetActive(currentArmorRatio > 0f);
        }
    }

    public void SetUIActive(DummyLogicType logicType, bool active)
    {
        if (healthBarGroup != null)
        {
            healthBarGroup.SetActive(active && logicType == DummyLogicType.HealthState);
        }

        if (hitCountGroup != null)
        {
            hitCountGroup.SetActive(active && logicType == DummyLogicType.RequiredHitCount);
        }
    }
}