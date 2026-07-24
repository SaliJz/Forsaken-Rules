using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Clase que gestiona la salud de un enemigo.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    #region Inspector - References

    [Header("UI - Sliders")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private TextMeshProUGUI healthPercentageText;
    [SerializeField] private TextMeshProUGUI healthMultiplierText;

    [Header("Toughness System")]
    [SerializeField] private EnemyToughness toughnessSystem;

    #endregion

    #region Inspector - Health Settings

    [Header("Health statistics")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool canDieAfterCooldown = true;
    [SerializeField] private float deathCooldown = 2f;
    [SerializeField] private bool canDestroy = true;
    [SerializeField] private bool canDisable = false;
    [SerializeField] private bool canBeStunned = true;
    [SerializeField] private UnityEvent onDeathEvent;

    #endregion

    #region Inspector - UI Settings

    [Header("UI Offsets & Delays")]
    [SerializeField] private float offsetAboveEnemy = 2f;
    [SerializeField] private float glowDelayAfterCritical = 2f;

    [Header("Dynamic Bar Configuration")]
    [SerializeField] private bool useDynamicHealthBars = false;
    [SerializeField] private float healthPerBar = 100f;
    [SerializeField] private Color healthBaseColor = Color.red;
    [SerializeField, Range(0f, 1f)] private float colorGradientIntensity = 0.3f;

    #endregion

    #region Inspector - Lifesteal Settings

    [Header("Lifesteal Control")]
    [SerializeField] private bool canGrantLifestealOnDeath = true; // deshabilitar para tutoriales u otros casos
    [Tooltip("Cantidad base de lifesteal otorgada al jugador al morir este enemigo por dano cuerpo a cuerpo.")]
    [SerializeField] private float lifestealAmountOnDeathByMelee = 10f;
    [Tooltip("Cantidad base de lifesteal otorgada al jugador al morir este enemigo por dano a distancia.")]
    [SerializeField] private float lifestealAmountOnDeathByDistance = 10f;

    #endregion

    #region Inspector - VFX References

    [Header("Death Feedback")]
    [SerializeField] private GameObject deathVFXPrefab;
    [SerializeField] private Transform deathVFXSpawnPoint;
    [SerializeField] private Vector3 deathVFXOffsetFallBack = new Vector3(0, 1f, 0);

    [Header("VFX Impact")]
    [SerializeField] private Transform impactVFXSpawnPoint;

    [Header("VFX Health Damage")]
    [SerializeField] private GameObject healthDamageVFXPrefab;
    [SerializeField] private Transform healthDamageVFXSpawnPoint;

    #endregion

    #region Internal State

    private PlayerStatsManager playerStatsManager;
    private EnemyAuraManager auraManager;
    private PlayerHealth playerHealth;
    private EnemyVisualEffects enemyVisualEffects;
    private Transform playerTransform;
    private Renderer enemyRenderer;
    private Collider enemyCollider;

    private Color originalColor;
    private AttackDamageType lastDamageType;
    private Vector3 lastDamageSourcePosition;

    private Coroutine stunCoroutine;
    private Coroutine currentCriticalDamageCoroutine;
    private Coroutine reduccionLocalRoutine;

    private int vulnerableLayerIndex;
    private int currentHealthBars;
    private int totalHealthBars;

    private float dynamicDamageReduction = 0.0f;
    private float initialHealthMultiplier = 1.0f;
    private float auraDamageReduction = 0.0f;
    private float localReduction = 0f;
    private float nextHitToughnessBonus = 0f;

    private bool canHealPlayer = true;
    private bool isDead = false;
    private bool isStunned = false;

    #endregion

    #region Public Properties & Events

    public int invulnerableLayerIndex;
    public bool ItemEffectHandledDeath { get; set; } = false;

    public static event Action<float, float> OnEnemyHealthChanged;
    public Action<GameObject> OnDeath;
    public event Action OnDamaged;
    public event Action OnToughnessHit;
    public event Action<float, float> OnHealthChanged;

    public Transform HealthBarAnchor => healthSlider.transform;
    public bool IsStunned => isStunned;
    public float MaxHealth => maxHealth;
    public AttackDamageType LastDamageType => lastDamageType;
    public Vector3 LastDamageSourcePosition => lastDamageSourcePosition;
    public Vector3 ImpactVFXPosition => impactVFXSpawnPoint != null ? impactVFXSpawnPoint.position : transform.position;

    public bool CanBeStunned
    {
        get { return canBeStunned; }
        set { canBeStunned = value; }
    }

    public float CurrentHealth
    {
        get
        {
            return currentHealth;
        }
        private set
        {
            if (!Mathf.Approximately(currentHealth, value))
            {
                currentHealth = value;
                OnEnemyHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }
    }

    public bool CanHealPlayer
    {
        get { return canHealPlayer; }
        set { canHealPlayer = value; }
    }

    public bool IsDead
    {
        get { return isDead; }
        set { isDead = value; }
    }

    public bool CanDestroy
    {
        get { return canDestroy; }
        set { canDestroy = value; }
    }

    public bool CanDisable
    {
        get { return canDisable; }
        set { canDisable = value; }
    }

    public float DeathCooldown
    {
        get { return deathCooldown; }
        set { deathCooldown = value; }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (enemyVisualEffects == null) enemyVisualEffects = GetComponent<EnemyVisualEffects>();
        if (enemyVisualEffects == null)
        {
            ReportDebug("Componente EnemyVisualEffects no encontrado en el enemigo.", 2);
        }

        toughnessSystem = GetComponent<EnemyToughness>();
        if (toughnessSystem == null)
        {
            ReportDebug("Componente EnemyToughness no encontrado en el enemigo.", 2);
        }

        enemyRenderer = GetComponentInChildren<Renderer>();
        if (enemyRenderer == null)
        {
            ReportDebug("Renderer no encontrado en el enemigo o sus hijos.", 2);
        }
        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            originalColor = enemyRenderer.material.color;
        }

        if (enemyCollider == null) enemyCollider = GetComponent<Collider>();
        if (enemyCollider == null)
        {
            ReportDebug("Collider no encontrado en el enemigo.", 2);
        }

        vulnerableLayerIndex = gameObject.layer;
        currentHealth = maxHealth;
        auraManager = GetComponent<EnemyAuraManager>();

        ApplyInitialHealth();

        // asegurar inicializacion de currentHealth (si no se configuro)
        currentHealth = Mathf.Clamp(currentHealth > 0f ? currentHealth : maxHealth, 0f, maxHealth);

        InitializeHealthUI();
    }

    private void Start()
    {
        if (CameraOcclusionFade.Instance != null)
        {
            CameraOcclusionFade.Instance.AddTarget(transform);
        }

        var playerGameObject = GameObject.FindGameObjectWithTag("Player");
        playerTransform = playerGameObject ? playerGameObject.transform : null;

        if (playerTransform == null)
        {
            ReportDebug("Jugador no encontrado en la escena.", 3);
        }
        else
        {
            playerTransform.TryGetComponent(out playerHealth);
            playerTransform.TryGetComponent(out playerStatsManager);
        }

        // emitir estado inicial
        OnEnemyHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateHealthUI();
    }

    private void OnDisable()
    {
        if (CameraOcclusionFade.Instance != null)
        {
            CameraOcclusionFade.Instance.RemoveTarget(transform);
        }
    }

    private void OnDestroy()
    {
        if (CameraOcclusionFade.Instance != null)
        {
            CameraOcclusionFade.Instance.RemoveTarget(transform);
        }

        if (isDead) StopAllCoroutines();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            DebugKill();
        }
    }

    #endregion

    #region Initialization & Data Sync

    public void SetInitialHealthMultiplier(float multiplier)
    {
        initialHealthMultiplier = multiplier;
    }

    private void ApplyInitialHealth()
    {
        currentHealth = maxHealth * initialHealthMultiplier;
        maxHealth *= initialHealthMultiplier;
    }

    public void SetMaxHealth(float health)
    {
        maxHealth = health;
        currentHealth = maxHealth;
        InitializeHealthUI();
        UpdateHealthUI();
    }

    public void SubscribeToDeath(UnityAction action)
    {
        onDeathEvent.AddListener(action);
    }

    public void UnsubscribeFromDeath(UnityAction action)
    {
        onDeathEvent.RemoveListener(action);
    }

    #endregion

    #region Core Health & Combat

    public void PrepareToughnessBonus(float bonusAmount)
    {
        nextHitToughnessBonus = bonusAmount;
    }

    public void TakeDamage(float damageAmount, AttackDamageType damageType, Vector3 damageSourcePosition)
    {
        lastDamageSourcePosition = damageSourcePosition;
        var drogathBlocker = GetComponent<DrogathEnemy>();
        if (drogathBlocker != null)
        {
            Vector3 damageDirection = (transform.position - damageSourcePosition).normalized;

            if (drogathBlocker.ShouldBlockDamage(damageDirection))
            {
                // Dano bloqueado por el escudo frontal.
                ReportDebug($"Dano {damageAmount} bloqueado por escudo de {gameObject.name}.", 1);
                return;
            }
        }

        TakeDamage(damageAmount: damageAmount, damageType: damageType);
    }

    public void TakeDamage(float damageAmount, bool isCritical = false, AttackDamageType damageType = AttackDamageType.Melee)
    {
        if (currentHealth <= 0) return;

        float finalDamage = damageAmount;

        // Procesar dureza
        if (toughnessSystem != null && toughnessSystem.HasToughness)
        {
            finalDamage = toughnessSystem.ProcessDamage(damageAmount, damageType, nextHitToughnessBonus);

            nextHitToughnessBonus = 0f; // resetear el bonus tras usarlo

            if (finalDamage <= 0)
            {
                if (enemyVisualEffects != null)
                {
                    enemyVisualEffects.PlayToughnessHitFeedback(transform.position
                        + Vector3.up * offsetAboveEnemy, damageAmount);
                }

                OnToughnessHit?.Invoke();
                return;
            }
            else if (finalDamage < damageAmount)
            {
                ReportDebug($"Dano reducido por dureza: {damageAmount} -> {finalDamage}", 1);
            }
        }
        else
        {
            nextHitToughnessBonus = 0f; // resetear si no hay sistema
        }

        float damageReductionTotal = localReduction + auraDamageReduction + dynamicDamageReduction;
        finalDamage *= (1f - damageReductionTotal);

        if (finalDamage <= 0 && damageAmount > 0)
        {
            ReportDebug($"Dano {damageAmount} completamente bloqueado por reduccion dinamica.", 1);
            return; // Dano inmune
        }

        currentHealth -= finalDamage;
        lastDamageType = damageType;
        ReportDebug($"Dano recibido. Base: {damageAmount}. Reduccion total: {damageReductionTotal * 100}%. Dano Final: {finalDamage}. Vida Restante: {currentHealth}", 1);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // emitir cambio de vida para listeners
        CurrentHealth = currentHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamaged?.Invoke();
        SpawnHealthDamageVFX();
        UpdateHealthUI();

        if (Mathf.RoundToInt(currentHealth) % 10 == 0) ReportDebug($"El enemigo ha recibido {finalDamage} de dano. Vida actual: {currentHealth}/{maxHealth}", 1);

        // Feedback visual/sonoro/numerico centralizado en EnemyVisualEffects
        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.PlayHealthHitFeedback(transform.position + Vector3.up * offsetAboveEnemy, finalDamage, isCritical);

            if (isCritical)
            {
                ReportDebug("El enemigo ha recibido dano critico.", 1);

                //enemyVisualEffects.StartArmorGlow();

                if (currentCriticalDamageCoroutine != null) StopCoroutine(currentCriticalDamageCoroutine);
                currentCriticalDamageCoroutine = StartCoroutine(StopGlowAfterDelay(glowDelayAfterCritical));
            }
        }

        if (currentHealth <= 0)
        {
            CurrentHealth = 0;
            Die(deathByDamageType: damageType);
        }
    }

    public void Heal(float healAmount)
    {
        if (currentHealth <= 0) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        CurrentHealth = currentHealth;

        ReportDebug($"{gameObject.name} ha sido curado por {healAmount}. Vida actual: {currentHealth}/{maxHealth}", 1);
        UpdateHealthUI();
    }

    public void Die(bool triggerEffects = true, AttackDamageType deathByDamageType = AttackDamageType.Melee)
    {
        if (isDead) return;

        if (triggerEffects)
        {
            InventoryManager inventoryManager = FindAnyObjectByType<InventoryManager>();
            if (inventoryManager != null)
            {
                var effects = inventoryManager.ActiveBehavioralEffects;
                if (effects.Count == 0)
                {
                    Debug.Log($"[EnemyHealth] '{gameObject.name}' muere. No hay efectos de muerte activos.");
                }
                else
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    sb.Append($"[EnemyHealth] '{gameObject.name}' muere. Efectos activos ({effects.Count}): ");
                    foreach (var e in effects)
                        sb.Append($"[{e.EffectID}] ");
                    Debug.Log(sb.ToString());
                }
            }

            CombatEventsManager.TriggerEnemyKilled(gameObject, maxHealth);
            CombatEventsManager.TriggerEnemyKilledType(gameObject, deathByDamageType);
        }

        isDead = true;
        currentHealth = 0;
        CurrentHealth = 0;

        if (enemyCollider != null) enemyCollider.enabled = false;

        onDeathEvent?.Invoke();
        OnDeath?.Invoke(gameObject);

        if (auraManager != null)
        {
            auraManager.HandleDeathEffect(transform, maxHealth);
        }

        ReportDebug($"{gameObject.name} ha muerto.", 1);

        if (canGrantLifestealOnDeath && playerHealth != null && playerStatsManager != null)
        {
            float lifestealAmount = playerStatsManager.GetCurrentStat(StatType.LifestealOnKill);

            if (canHealPlayer)
            {
                if (deathByDamageType == AttackDamageType.Melee)
                {
                    float totalLifesteal = lifestealAmountOnDeathByMelee + lifestealAmount;
                    if (totalLifesteal > 0)
                    {
                        playerHealth.Heal(totalLifesteal);
                        ReportDebug($"El jugador ha robado {totalLifesteal} de vida al matar a {gameObject.name} (LifestealOnKill) por ataque cuerpo a cuerpo.", 1);
                    }
                }
                else if (deathByDamageType == AttackDamageType.Ranged)
                {
                    float totalLifesteal = lifestealAmountOnDeathByDistance += lifestealAmount;
                    if (totalLifesteal > 0)
                    {
                        playerHealth.Heal(totalLifesteal);
                        ReportDebug($"El jugador ha robado {totalLifesteal} de vida al matar a {gameObject.name} (LifestealOnKill) por ataque a distancia.", 1);
                    }
                }
            }
        }
        else if (!canGrantLifestealOnDeath)
        {
            ReportDebug($"{gameObject.name} no otorga lifesteal (deshabilitado para tutorial).", 1);
        }

        if (canDestroy)
        {
            if (GetComponent<ExplosionDelayHandler>() != null)
            {
                StartCoroutine(DeathRoutine());
            }
            else if (canDieAfterCooldown)
            {
                StartCoroutine(DeathRoutine());
            }
            else
            {
                if (deathVFXPrefab != null)
                {
                    Vector3 pos = deathVFXSpawnPoint != null 
                        ? deathVFXSpawnPoint.position 
                        : transform.position + deathVFXOffsetFallBack;

                    Instantiate(deathVFXPrefab, pos, Quaternion.identity);
                }

                Destroy(gameObject);
            }
        }
        else if (canDisable)
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(deathCooldown);

        if (deathVFXPrefab != null)
        {
            Instantiate(deathVFXPrefab, transform.position + deathVFXOffsetFallBack, Quaternion.identity);
        }
        else
        {
            ReportDebug("No se ha asignado deathVFXPrefab en el inspector.", 2);
        }

        Destroy(gameObject);
    }

    #endregion

    #region UI Management

    private void SpawnHealthDamageVFX()
    {
        if (healthDamageVFXPrefab == null) return;

        Vector3 pos = healthDamageVFXSpawnPoint != null
            ? healthDamageVFXSpawnPoint.position
            : transform.position;

        Instantiate(healthDamageVFXPrefab, pos, Quaternion.identity);
    }

    private void InitializeHealthUI()
    {
        if (useDynamicHealthBars)
        {
            totalHealthBars = Mathf.Max(1, Mathf.CeilToInt(maxHealth / healthPerBar));
            currentHealthBars = Mathf.Max(1, Mathf.CeilToInt(currentHealth / healthPerBar));
        }
        else
        {
            totalHealthBars = 1;
            currentHealthBars = 1;
        }

        if (healthSlider != null)
        {
            if (useDynamicHealthBars)
            {
                healthSlider.maxValue = healthPerBar;
                healthSlider.value = GetCurrentBarValue(currentHealth, healthPerBar);
            }
            else
            {
                healthSlider.maxValue = Mathf.Max(1, maxHealth);
                healthSlider.value = currentHealth;
            }
        }
        UpdateHealthUI(); // Llamar para actualizar colores y texto
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            if (useDynamicHealthBars)
            {
                // Actualizar contador de barras
                int newBars = Mathf.CeilToInt(currentHealth / healthPerBar);
                if (newBars != currentHealthBars)
                {
                    currentHealthBars = newBars;
                }
                healthSlider.value = GetCurrentBarValue(currentHealth, healthPerBar);
                healthSlider.maxValue = healthPerBar;
            }
            else
            {
                healthSlider.value = currentHealth;
                healthSlider.maxValue = Mathf.Max(1, maxHealth);
            }
            if (!healthSlider.gameObject.activeSelf) healthSlider.gameObject.SetActive(true);
        }

        if (healthFillImage != null)
        {
            // Usar color base o degradado segun la configuracion
            Color barColor = useDynamicHealthBars ?
                GetGradientColor(healthBaseColor, currentHealthBars, totalHealthBars) :
                healthBaseColor;

            healthFillImage.color = barColor;

            if (!healthFillImage.gameObject.activeSelf) healthFillImage.gameObject.SetActive(true);
        }

        if (healthPercentageText != null)
        {
            float percentage = (maxHealth > 0) ? (currentHealth / maxHealth) * 100f : 0f;
            healthPercentageText.text = $"{percentage:F0}%";
        }

        if (healthMultiplierText != null)
        {
            if (useDynamicHealthBars && currentHealthBars > 1)
            {
                healthMultiplierText.text = $"x{currentHealthBars}";
                healthMultiplierText.gameObject.SetActive(true);
            }
            else
            {
                healthMultiplierText.gameObject.SetActive(false);
            }
        }
    }

    public void SetInvulnerable(bool invulnerable)
    {
        if (invulnerable)
        {
            healthSlider.gameObject.SetActive(false);
        }
        else
        {
            healthSlider.gameObject.SetActive(true);
        }
    }

    private Color GetGradientColor(Color baseColor, int currentBar, int totalBars)
    {
        if (totalBars <= 1) return baseColor;
        if (currentBar <= 0) return baseColor;
        if (currentBar >= totalBars) return baseColor;

        float t = (float)(currentBar - 1) / (totalBars - 1);
        t = Mathf.Lerp(1f, t, colorGradientIntensity);
        Color lighterColor = Color.Lerp(Color.white, baseColor, 0.6f);
        return Color.Lerp(lighterColor, baseColor, t);
    }

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

    #endregion

    #region Status Effects & Debuffs

    public void ApplyDamageReduction_Aura(float reductionPercent)
    {
        auraDamageReduction = reductionPercent;
        ReportDebug($"Reduccion de dano por Aura Endurecimiento: {reductionPercent * 100}%.", 1);
    }

    private IEnumerator StopGlowAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (enemyVisualEffects != null)
        {
            //enemyVisualEffects.StopArmorGlow();
        }
    }

    /// <summary>
    /// Establece una reduccion de dano dinamica y cambia la capa del objeto
    /// para evitar ser detectado por ataques (ej. rebotes de escudo).
    /// </summary>
    public void SetDynamicVulnerability(float damageReductionPercent)
    {
        dynamicDamageReduction = Mathf.Clamp01(damageReductionPercent);

        bool isInvulnerable = Mathf.Approximately(dynamicDamageReduction, 1.0f);
        int newLayer = isInvulnerable ? invulnerableLayerIndex : vulnerableLayerIndex;

        // Solo cambiar si la capa es diferente
        if (gameObject.layer != newLayer)
        {
            SetLayerRecursively(transform, newLayer);
        }
    }

    /// <summary>
    /// Metodo para cambiar la capa de este objeto y todos sus hijos.
    /// </summary>
    private void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
        {
            SetLayerRecursively(child, layer);
        }
    }

    public void ApplyStun(float duration)
    {
        if (!canBeStunned) return;

        if (isStunned)
        {
            if (stunCoroutine != null) StopCoroutine(stunCoroutine);
        }

        stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        ReportDebug("Iniciando aturdimiento.", 1);
        isStunned = true;

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.StartStunEffect(duration);
        }
        else
        {
            if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
            {
                enemyRenderer.material.color = Color.yellow;
            }
        }

        ReportDebug($"Aturdido por {duration}s.", 1);

        yield return new WaitForSeconds(duration);

        isStunned = false;

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.StopStunEffect();
        }
        else
        {
            if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
            {
                enemyRenderer.material.color = originalColor;
            }
        }

        stunCoroutine = null;
        ReportDebug("Aturdimiento finalizado.", 1);
    }

    /// <summary>
    /// Aplica una reduccion de dano local durante 'duration' segundos.
    /// Firma publica anadida para compatibilidad con otros scripts (ArmaduraDemonicaArea, prefabs, etc.).
    /// </summary>
    public void ApplyDamageReduction(float percent, float duration)
    {
        if (percent <= 0f || duration <= 0f) return;

        // si ya hay rutina, reiniciar
        if (reduccionLocalRoutine != null) StopCoroutine(reduccionLocalRoutine);
        reduccionLocalRoutine = StartCoroutine(RutinaReduccionLocal(percent, duration));
    }

    private IEnumerator RutinaReduccionLocal(float percent, float duration)
    {
        localReduction = percent;
        yield return new WaitForSeconds(duration);
        localReduction = 0f;
        reduccionLocalRoutine = null;
    }

    #endregion

    #region Logging

    public void DebugKill()
    {
        if (currentHealth > 0 && gameObject.name.Contains("Boss", StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("<color=red>[DEBUG] Boss eliminado instantaneamente con la tecla 'B'.</color>");
            TakeDamage(9999f);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector3.up * offsetAboveEnemy);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + deathVFXOffsetFallBack);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[EnemyHealth] {message}");
                break;
            case 2:
                Debug.LogWarning($"[EnemyHealth] {message}");
                break;
            case 3:
                Debug.LogError($"[EnemyHealth] {message}");
                break;
            default:
                Debug.Log($"[EnemyHealth] {message}");
                break;
        }
    }

    #endregion
}