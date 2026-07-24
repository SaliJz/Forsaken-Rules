using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

#region Enums

/// <summary>
/// Enumeracion para tipos de dano.
/// </summary>
public enum AttackDamageType
{
    Melee,
    Ranged,
    Nothing,
}

#endregion

public class EnemyToughness : MonoBehaviour
{
    #region Inspector - References

    [Header("UI References")]
    [SerializeField] private Slider toughnessSlider;
    [SerializeField] private Image toughnessFillImage;
    [SerializeField] private TextMeshProUGUI toughnessPercentageText;
    [SerializeField] private TextMeshProUGUI toughnessMultiplierText;
    [SerializeField] private GameObject toughnessUIGroup;

    #endregion

    #region Inspector - Toughness Configuration

    [Header("Toughness Configuration")]
    [SerializeField] private bool useToughness = false;
    [Tooltip("Cantidad maxima de dureza del enemigo.")]
    [SerializeField] private float maxToughness = 10f;
    [SerializeField] private float currentToughness;

    #endregion

    #region Inspector - Damage Modifiers

    [Header("Damage Modifiers")]
    [Tooltip("Porcentaje de dano que los ataques melee infligen a la dureza (1.0 = 100%).")]
    [SerializeField, Range(0f, 2f)] private float meleeToughnessDamageMultiplier = 1.0f;
    [Tooltip("Porcentaje de dano que los ataques a distancia infligen a la dureza (0.25 = 25%).")]
    [SerializeField, Range(0f, 2f)] private float rangedToughnessDamageMultiplier = 0.25f;

    #endregion

    #region Inspector - Dynamic Bars Configuration

    [Header("Dynamic Bars Configuration")]
    [SerializeField] private bool useDynamicBars = false;
    [Tooltip("Valor de cada segmento de dureza.")]
    [SerializeField] private float toughnessPerBar = 1f;

    #endregion

    #region Inspector - Bar Colors

    [Header("Bar Colors")]
    [SerializeField] private Color toughnessBaseColor = Color.cyan;
    [Tooltip("Degradado de color entre barras (0 = sin degradado, 1 = degradado completo).")]
    [SerializeField, Range(0f, 1f)] private float colorGradientIntensity = 0.3f;

    #endregion

    #region Inspector - VFX References

    [Header("VFX Toughness Block")]
    [SerializeField] private GameObject toughnessBlockVFXPrefab;
    [SerializeField] private Transform toughnessBlockVFXSpawnPoint;

    #endregion

    #region Internal State

    private EnemyHealth enemyHealth;
    private int currentToughnessBars;
    private int totalToughnessBars;
    private bool isInitialized = false;
    private Coroutine temporaryBuffRoutine;

    #endregion

    #region Public Properties & Events

    public event Action<float, float> OnToughnessChanged;
    public event Action OnToughnessBreak;

    public float CurrentToughness => currentToughness;
    public float MaxToughness => maxToughness;
    public bool HasToughness => useToughness && currentToughness > 0;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth == null)
        {
            ReportDebug("EnemyHealth no encontrado. EnemyToughnessSystem requiere EnemyHealth.", 3);
            enabled = false;
            return;
        }

        if (useToughness)
        {
            currentToughness = maxToughness;
            if (toughnessUIGroup != null && !toughnessUIGroup.activeSelf) toughnessUIGroup.SetActive(true);
            if (toughnessSlider != null && toughnessFillImage != null) toughnessFillImage.color = toughnessBaseColor;
        }
        else
        {
            if (toughnessUIGroup != null && toughnessUIGroup.activeSelf) toughnessUIGroup.SetActive(false);
        }
    }

    private void Start()
    {
        StartCoroutine(DelayedInitialization());
    }

    #endregion

    #region Initialization Logic

    private System.Collections.IEnumerator DelayedInitialization()
    {
        yield return null; // Esperar un frame

        InitializeSystem();
        UpdateUI();
        isInitialized = true;

        ReportDebug($"Sistema inicializado - Vida: {enemyHealth.CurrentHealth}/{enemyHealth.MaxHealth}, Dureza: {currentToughness}/{maxToughness}", 1);
    }

    private void InitializeSystem()
    {
        if (enemyHealth == null || enemyHealth.MaxHealth <= 0)
        {
            ReportDebug("No se puede inicializar: EnemyHealth no valido", 3);
            return;
        }

        if (useToughness)
        {
            totalToughnessBars = Mathf.Max(1, Mathf.CeilToInt(maxToughness / toughnessPerBar));
            currentToughnessBars = totalToughnessBars; // Dureza siempre empieza llena
        }

        // Reportar estado inicial
        if (useDynamicBars)
        {
            ReportDebug($"Barras dinamicas de dureza: {currentToughnessBars}/{totalToughnessBars}", 1);
        }

        // Configurar UI de dureza
        if (toughnessUIGroup != null)
        {
            toughnessUIGroup.SetActive(useToughness);
        }

        // Configurar slider de dureza
        if (toughnessSlider != null && useToughness)
        {
            if (useDynamicBars)
            {
                toughnessSlider.maxValue = toughnessPerBar;
                toughnessSlider.value = GetCurrentBarValue(currentToughness, toughnessPerBar);
            }
            else
            {
                toughnessSlider.maxValue = maxToughness;
                toughnessSlider.value = currentToughness;
            }
        }
    }

    #endregion

    #region Damage Processing Logic

    /// <summary>
    /// Procesa el dano considerando la dureza y el tipo de ataque.
    /// Retorna el dano que debe aplicarse a la vida.
    /// </summary>
    public float ProcessDamage(float rawDamage, AttackDamageType damageType, float attackerToughnessBonus = 0f)
    {
        if (!useToughness || currentToughness <= 0)
        {
            // Sin dureza o dureza rota: dano directo a vida
            return rawDamage;
        }

        // Calcular multiplicador segun tipo de ataque
        float baseMultiplier = GetToughnessMultiplier(damageType);
        float finalMultiplier = baseMultiplier + attackerToughnessBonus;
        float toughnessDamage = rawDamage * finalMultiplier;

        // Aplicar dano a dureza
        float overflow = ApplyToughnessDamage(toughnessDamage);

        // Si hay overflow, el exceso va a la vida
        if (overflow > 0)
        {
            ReportDebug($"Dureza rota. Overflow de {overflow} dano aplicado a vida.", 1);
            return overflow / finalMultiplier; // Convertir overflow proporcional de vuelta
        }
        else
        {
            SpawnToughnessBlockVFX();
        }

        // Dureza absorbio todo el dano
        ReportDebug($"Dureza absorbio {toughnessDamage} de dano ({finalMultiplier * 100}% del total).", 1);
        return 0f;
    }

    private float GetToughnessMultiplier(AttackDamageType damageType)
    {
        switch (damageType)
        {
            case AttackDamageType.Melee:
                return meleeToughnessDamageMultiplier;
            case AttackDamageType.Ranged:
                return rangedToughnessDamageMultiplier;
            default:
                return 1.0f;
        }
    }

    private float ApplyToughnessDamage(float damage)
    {
        currentToughness -= damage;

        float overflow = 0f;
        if (currentToughness < 0)
        {
            overflow = Mathf.Abs(currentToughness);
            currentToughness = 0;
            OnToughnessBreak?.Invoke();
            ReportDebug("Dureza rota!", 1);
        }

        currentToughness = Mathf.Max(0, currentToughness);
        OnToughnessChanged?.Invoke(currentToughness, maxToughness);

        UpdateToughnessBars();
        UpdateUI();

        return overflow;
    }

    #endregion

    #region UI & Bar Management Logic

    private void SpawnToughnessBlockVFX()
    {
        if (toughnessBlockVFXPrefab == null) return;

        Vector3 pos = toughnessBlockVFXSpawnPoint != null
            ? toughnessBlockVFXSpawnPoint.position
            : transform.position;

        Instantiate(toughnessBlockVFXPrefab, pos, Quaternion.identity);
    }

    private void UpdateToughnessBars()
    {
        if (!useDynamicBars) return;

        int newBars = Mathf.CeilToInt(currentToughness / toughnessPerBar);
        if (newBars != currentToughnessBars)
        {
            currentToughnessBars = newBars;
            ReportDebug($"Barras de dureza actualizadas: {currentToughnessBars}/{totalToughnessBars}", 1);
        }
    }

    // Metodo para obtener el valor actual dentro de la barra
    private float GetCurrentBarValue(float currentValue, float valuePerBar)
    {
        if (currentValue <= 0) return 0;

        float remainder = currentValue % valuePerBar;

        if (Mathf.Approximately(remainder, 0f) && currentValue > 0)
        {
            return valuePerBar;
        }

        return remainder;
    }

    private void UpdateUI()
    {
        if (!isInitialized) return;

        if (useToughness)
        {
            UpdateToughnessUI();
        }
    }

    private void UpdateToughnessUI()
    {
        if (toughnessSlider != null)
        {
            if (useDynamicBars)
            {
                toughnessSlider.value = GetCurrentBarValue(currentToughness, toughnessPerBar);
                toughnessSlider.maxValue = toughnessPerBar;
            }
            else
            {
                toughnessSlider.value = currentToughness;
                toughnessSlider.maxValue = maxToughness;
            }
        }

        if (toughnessFillImage != null)
        {
            Color barColor = GetGradientColor(toughnessBaseColor, currentToughnessBars, totalToughnessBars);
            toughnessFillImage.color = barColor;
        }

        if (toughnessMultiplierText != null && useDynamicBars)
        {
            if (currentToughnessBars > 1)
            {
                toughnessMultiplierText.text = $"x{currentToughnessBars}";
                toughnessMultiplierText.gameObject.SetActive(true);
            }
            else
            {
                toughnessMultiplierText.gameObject.SetActive(false);
            }
        }

        if (toughnessUIGroup != null)
        {
            bool shouldBeActive = currentToughness > 0;
            if (toughnessUIGroup.activeSelf != shouldBeActive)
            {
                toughnessUIGroup.SetActive(shouldBeActive);
            }
        }
    }

    private Color GetGradientColor(Color baseColor, int currentBar, int totalBars)
    {
        // Si solo hay una barra total, siempre usar color base
        if (totalBars <= 1) return baseColor;

        // Si no quedan barras, usar color base
        if (currentBar <= 0) return baseColor;

        // Si es la ultima barra (currentBar == totalBars), usar color base
        if (currentBar >= totalBars) return baseColor;

        // Calcular posicion en el degradado (la ultima barra es la mas intensa)
        float t = (float)(currentBar - 1) / (totalBars - 1);

        // Aplicar intensidad de degradado
        t = Mathf.Lerp(1f, t, colorGradientIntensity);

        // Crear color mas claro para barras anteriores
        Color lighterColor = Color.Lerp(Color.white, baseColor, 0.6f);

        return Color.Lerp(lighterColor, baseColor, t);
    }

    #endregion

    #region Public API Logic

    public void SetMaxToughness(float value)
    {
        maxToughness = value;
        currentToughness = maxToughness;

        if (useDynamicBars)
        {
            totalToughnessBars = Mathf.CeilToInt(maxToughness / toughnessPerBar);
            currentToughnessBars = totalToughnessBars;
        }

        UpdateUI();
    }

    public float AddCurrentToughness(float amount)
    {
        if (!useToughness || amount <= 0) return 0f;

        float previousToughness = currentToughness;
        currentToughness = Mathf.Clamp(currentToughness + amount, 0f, maxToughness);

        float addedAmount = currentToughness - previousToughness;

        if (addedAmount > 0)
        {
            OnToughnessChanged?.Invoke(currentToughness, maxToughness);
            UpdateUI();
        }

        return addedAmount;
    }

    public void ApplyToughnessBuff(float amount, float duration)
    {
        if (temporaryBuffRoutine != null)
        {
            StopCoroutine(temporaryBuffRoutine);
            temporaryBuffRoutine = null;
        }

        temporaryBuffRoutine = StartCoroutine(ToughnessBuffRoutine(amount, duration));
    }

    private IEnumerator ToughnessBuffRoutine(float amount, float duration)
    {
        if (amount <= 0f)
        {
            temporaryBuffRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            AddCurrentToughness(amount);

            OnToughnessChanged?.Invoke(currentToughness, maxToughness);
            UpdateUI();

            int remaining = Mathf.Max(0, Mathf.CeilToInt(duration - elapsed));
            ReportDebug($"Aplicando buff de dureza: +{amount} (Tiempo restante: {remaining} s)", 1);

            yield return new WaitForSeconds(1f);
            elapsed += 1f;
        }

        ReportDebug("Buff de dureza finalizado.", 1);

        temporaryBuffRoutine = null;
    }

    public void ResetToughness()
    {
        currentToughness = maxToughness;

        if (useDynamicBars)
        {
            currentToughnessBars = totalToughnessBars;
        }

        OnToughnessChanged?.Invoke(currentToughness, maxToughness);
        UpdateUI();
        ReportDebug("Dureza restaurada.", 1);
    }

    public void SetUseToughness(bool value)
    {
        useToughness = value;
        if (toughnessUIGroup != null)
        {
            toughnessUIGroup.SetActive(value);
        }
        UpdateUI();
    }

    #endregion

    #region Logging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[EnemyToughnessSystem] {message}");
                break;
            case 2:
                Debug.LogWarning($"[EnemyToughnessSystem] {message}");
                break;
            case 3:
                Debug.LogError($"[EnemyToughnessSystem] {message}");
                break;
        }
    }

    #endregion
}