using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerStatsPanel : MonoBehaviour
{
    [Header("Referencias UI")]
    [Tooltip("El contenedor principal del panel (RectTransform).")]
    [SerializeField] private RectTransform panelRect;
    [Tooltip("El componente de texto donde se imprimirán las estadísticas.")]
    [SerializeField] private TextMeshProUGUI statsTextDisplay;

    [Header("Posicionamiento")]
    [Tooltip("Desplazamiento en el mundo 3D respecto a la posición del jugador.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(2f, 0f, 0f);

    private PlayerStatsManager statsManager;
    private Canvas parentCanvas;
    private bool wasInventoryOpen = false;

    private enum StatDisplayRule
    {
        RealValue,
        DirectPercent,
        RelativePercent,
        BonusOnly
    }

    private void Start()
    {
        parentCanvas = GetComponentInParent<Canvas>();

        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += OnStatChangedHandler;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= OnStatChangedHandler;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnStatChangedHandler(StatType type, float newValue)
    {
        if (panelRect != null && panelRect.gameObject.activeSelf)
        {
            UpdateStatsPanel();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        statsManager = FindAnyObjectByType<PlayerStatsManager>();
        if (panelRect != null && panelRect.gameObject.activeSelf) UpdateStatsPanel();
    }

    private void Update()
    {
        bool isInventoryOpen = InventoryUIManager.Instance != null &&
                               InventoryUIManager.Instance.IsOpen &&
                               !InventoryUIManager.Instance.IsConfirmPanelOpen;

        if (isInventoryOpen != wasInventoryOpen)
        {
            wasInventoryOpen = isInventoryOpen;

            if (isInventoryOpen)
            {
                if (statsManager == null) statsManager = FindAnyObjectByType<PlayerStatsManager>();
                UpdateStatsPanel();
                if (panelRect != null) panelRect.gameObject.SetActive(true);
            }
            else
            {
                if (panelRect != null) panelRect.gameObject.SetActive(false);
            }
        }

        if (isInventoryOpen && panelRect != null && statsManager != null)
        {
            FollowPlayerPosition();
        }
    }

    private void FollowPlayerPosition()
    {
        if (Camera.main == null || parentCanvas == null) return;

        Vector3 targetWorldPosition = statsManager.transform.position + worldOffset;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, targetWorldPosition);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            screenPoint,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
            out Vector2 localPoint);

        panelRect.localPosition = localPoint;
    }

    public void UpdateStatsPanel()
    {
        if (statsManager == null || statsTextDisplay == null)
        {
            Debug.LogWarning("[PlayerStatsPanel] UpdateStatsPanel abortado: statsManager o statsTextDisplay es null.");
            return;
        }
        StringBuilder sb = new StringBuilder();

        sb.AppendLine(" <b><color=Yellow>Estadística <pos=50%>Base <pos=75%>Actual</color></b>");

        // Porcentajes
        sb.AppendLine(FormatStat("Resistencia", StatType.Endurance, StatDisplayRule.DirectPercent, inverseColors: false));
        sb.AppendLine(FormatStat("Vel. a Melé", StatType.MeleeAttackSpeed, StatDisplayRule.RelativePercent, inverseColors: false));
        sb.AppendLine(FormatStat("Vel. a Distancia", StatType.ShieldSpeed, StatDisplayRule.RelativePercent, inverseColors: false));

        // Valores Reales
        sb.AppendLine(FormatStat("Daño a melé", StatType.MeleeAttackDamage, StatDisplayRule.RealValue));
        sb.AppendLine(FormatStat("Daño a distancia", StatType.ShieldAttackDamage, StatDisplayRule.RealValue));
        sb.AppendLine(FormatStat("Vel. Movimiento", StatType.MoveSpeed, StatDisplayRule.RealValue));

        // Asumiendo que el alcance de impulso usa el base del movimiento, lo tratamos como RealValue
        sb.AppendLine(FormatStat("Alc. impulso", StatType.DashRangeFlatBonus, StatDisplayRule.RealValue));
        sb.AppendLine(FormatStat("Enfriamiento impulso", StatType.DashCooldownPost, StatDisplayRule.RealValue, inverseColors: true));
        sb.AppendLine(FormatStat("Alc. Distancia", StatType.ShieldMaxDistance, StatDisplayRule.RealValue));
        sb.AppendLine(FormatStat("Alc. rebote escudo", StatType.ShieldReboundRadius, StatDisplayRule.RealValue));
        sb.AppendLine(FormatStat("Rebotes del escudo", StatType.ShieldMaxRebounds, StatDisplayRule.RealValue));

        // Solo cantidad adicional / Bonus
        sb.AppendLine(FormatStat("Desplaz. por golpe", StatType.MeleeComboDisplacement, StatDisplayRule.BonusOnly));
        sb.AppendLine(FormatStat("Empuje a distancia", StatType.ShieldPushForce, StatDisplayRule.BonusOnly));
        sb.AppendLine(FormatStat("Empuje recibido", StatType.KnockbackReceived, StatDisplayRule.BonusOnly, inverseColors: true));
        //sb.AppendLine(FormatStat("Consumo de vida", StatType.HealthDrainAmount, StatDisplayRule.BonusOnly, inverseColors: true));
        sb.AppendLine(FormatStat("Consumo de energia", StatType.StaminaConsumption, StatDisplayRule.RealValue, inverseColors: true));
        sb.AppendLine(FormatStat("Vida al matar", StatType.LifestealOnKill, StatDisplayRule.BonusOnly));

        statsTextDisplay.text = sb.ToString();
    }

    /// <summary>
    /// Extrae del Gestor y formatea aplicando la regla de color correspondiente.
    /// </summary>
    private string FormatStat(string statName, StatType type, StatDisplayRule rule, bool inverseColors = false)
    {
        float baseValue = statsManager.GetBaseStat(type);
        float currentValue = statsManager.GetCurrentStat(type);

        float displayBase = baseValue;
        float displayCurrent = currentValue;
        bool usePercentSymbol = false;

        switch (rule)
        {
            case StatDisplayRule.RealValue:
                displayBase = baseValue;
                displayCurrent = currentValue;
                break;

            case StatDisplayRule.DirectPercent:
                displayBase = baseValue;
                displayCurrent = currentValue;
                usePercentSymbol = true;
                break;

            case StatDisplayRule.RelativePercent:
                displayBase = 100f;
                displayCurrent = baseValue > 0.001f ? (currentValue / baseValue) * 100f : 100f;
                usePercentSymbol = true;
                break;

            case StatDisplayRule.BonusOnly:
                displayBase = 0f;
                displayCurrent = currentValue - baseValue;
                break;
        }

        return FormatCustomLine(statName, displayBase, displayCurrent, usePercentSymbol, inverseColors);
    }

    /// <summary>
    /// Función base de formateo estructurada en 3 columnas y control de colores (Rojo/Verde)
    /// </summary>
    private string FormatCustomLine(string statName, float displayBase, float displayCurrent, bool usePercentSymbol, bool inverseColors)
    {
        string symbol = usePercentSymbol ? "%" : "";
        string changedString = $"{displayCurrent:0.##}{symbol}";

        float difference = displayCurrent - displayBase;

        if (Mathf.Abs(difference) > 0.01f)
        {
            if (difference > 0)
            {
                string color = inverseColors ? "red" : "green";
                changedString = $"<color={color}>{displayCurrent:0.##}{symbol} ↑</color>";
            }
            else
            {
                string color = inverseColors ? "green" : "red";
                changedString = $"<color={color}>{displayCurrent:0.##}{symbol} ↓</color>";
            }
        }

        return $" {statName} <pos=50%>{displayBase:0.##}{symbol} <pos=75%>{changedString}";
    }
}