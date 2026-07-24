using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    #region Enums

    // Tipos de etapas de vida del jugador.
    public enum LifeStage
    {
        Young,
        Adult,
        Elder
    }

    #endregion

    #region Inspector – References

    [Header("Referencias")]
    [SerializeField] private SpriteRenderer sprtRenderer;
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private PlayerCombatActionManager combatActionManager;
    [SerializeField] private PlayerBlockSystem blockSystem;
    [SerializeField] private PlayerAudioController audioController;
    [SerializeField] private PlayerAnimCtrl animCtrl;
    [SerializeField] private PlayerShaderCtrl shaderCtrl;

    #endregion

    #region Inspector – Health & Death Settings

    [Header("Configuracion de Vida")]
    [Tooltip("Vida maxima por defecto si no se encuentra PlayerStatsManager.")]
    [SerializeField][HideInInspector] private float fallbackMaxHealth = 100;
    [SerializeField] private float damageInvulnerabilityTime = 0.5f;
    [SerializeField] private float shakeCooldown = 0.5f;

    [Header("Configuracion de Muerte")]
    [SerializeField] private string sceneToLoadOnDeath = "Tuto";
    [SerializeField] private Color deathFadeColor = Color.red;
    [SerializeField] private DeathSequenceCtrl deathSequence;

    #endregion

    #region Inspector – Life Stage Settings

    [Header("Configuracion de Modelo por Etapa")]
    [Tooltip("El objeto hijo que contiene el modelo visual del jugador.")]
    [SerializeField] private Transform playerModelTransform;
    [SerializeField] private Vector3 scaleYoung = new Vector3(0.75f, 0.75f, 0.75f);
    [SerializeField] private Vector3 scaleAdult = new Vector3(1f, 1f, 1f);
    [SerializeField] private Vector3 scaleElder = new Vector3(1.25f, 1.25f, 1.25f);
    [Tooltip("El desplazamiento vertical para mantener los pies en el suelo. Depende de la altura base del modelo.")]
    [SerializeField] private float yOffsetYoung = 0.375f;
    [SerializeField] private float yOffsetAdult = 0.5f;
    [SerializeField] private float yOffsetElder = 0.625f;

    #endregion

    #region Inspector – VFX Settings

    [Header("VFX - Transicion de Vida")]
    [SerializeField] private GameObject afterimagePrefab;
    [SerializeField] private Material whiteFlashMaterial;
    [SerializeField] private float transitionDuration = 0.8f;

    [Header("VFX - Daño recibido")]
    [SerializeField] private Color damageEmissionColor = Color.red;
    [SerializeField] private float damageEmissionIntensity = 2f;
    [SerializeField] private float damageFlashDuration = 0.1f;
    [SerializeField] private int damageFlashCount = 3;
    [SerializeField] private EstadoDeVida damageUIEffect;

    #endregion

    #region Inspector – Upgrades & Temporary Health

    [Header("Mejora de Escudo")]
    [SerializeField] private float shieldBlockCooldown = 18f;

    [Header("Vida Temporal")]
    [SerializeField] private float temporaryHealthDuration = 10f;
    [SerializeField] private float temporaryHealthDecaySpeed = 0.5f;

    #endregion

    #region Inspector – UI

    [Header("UI")]
    [SerializeField] private TMP_Text lifeStageText;

    #endregion

    #region Internal State

    // Constants
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private const float LifeStageYoungThreshold = 0.666f;
    private const float LifeStageAdultThreshold = 0.333f;

    // Core Health State
    private float currentHealth;
    private float currentTemporaryHealth = 0f;
    private float maxTemporaryHealthLimit = 0f;
    private bool isDamageInvulnerable = false;
    private bool isInitialized = false;
    private bool isDying = false;

    // Status Flags
    private bool isShieldBlockReady = true;
    private bool isStunned = false;
    private bool isShakeCooldown = false;

    // Coroutines
    private Coroutine temporaryHealthDecayCoroutine;
    private Coroutine damageInvulnerabilityCoroutine;
    private Coroutine damageFlashCoroutine;
    private Coroutine stunCoroutine;
    private Coroutine poisonCoroutine;
    private Coroutine slowCoroutine;

    // Cached Components
    private PlayerMovement playerMovement;
    private PlayerKnockbackReceiver playerKnockbackReceiver;
    private PlayerMeleeAttack playerMeleeAttack;
    private PlayerShieldController playerShieldController;
    private InventoryManager inventoryManager;
    private CinemachineImpulseSource _cinemachineShake;

    // Material Caching
    private MeshRenderer[] cachedMeshRenderers;
    private Dictionary<MeshRenderer, MaterialPropertyBlock> materialPropertyBlocks;
    private Dictionary<MeshRenderer, Color[]> originalEmissionColors;
    private Dictionary<MeshRenderer, bool> originalEmissionEnabled;

    #endregion

    #region Public Properties & Events

    public static PlayerHealth Instance { get; private set; }

    public float MaxHealth => statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;
    public float CurrentHealth => currentHealth;
    public float CurrentHealthPercent
    {
        get
        {
            float maxHealth = MaxHealth;
            if (maxHealth <= 0) return 0f;
            return currentHealth / maxHealth;
        }
    }
    public bool IsLowHealth => currentHealth < (MaxHealth * 0.25f);

    public LifeStage CurrentLifeStage { get; private set; }

    // Upgrades & Status Properties
    public bool IsKillHealBlocked { get; private set; } = false;
    public bool HasAmuletOfEndurance { get; private set; } = false;
    public bool HasShieldBlockUpgrade { get; private set; } = false;
    public bool IsInvulnerable { get; set; } = false;
    public bool IsMarkedByAstaroth { get; set; } = false;

    // Slow Properties
    public bool isSlowed { get; private set; } = false;
    public float slowSpeedMultiplier { get; private set; } = 1f;

    // Model Properties
    public Transform PlayerModelTransform => playerModelTransform;
    public Vector3 CurrentModelLocalScale => playerModelTransform != null ? playerModelTransform.localScale : Vector3.one;
    public Vector3 CurrentModelWorldScale => playerModelTransform != null ? playerModelTransform.lossyScale : Vector3.one;
    public float CurrentModelYOffset => playerModelTransform != null ? playerModelTransform.localPosition.y : 0f;

    // Events
    public static event Action<float, float> OnHealthChanged;
    public static event Action<LifeStage> OnLifeStageChanged;
    public static event Action<PlayerHealth> OnPlayerInstantiated;
    public event Action<float> OnDamageReceived;
    public static event Action onHealling;
    public static event Action onDeathing; //jejej

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        statsManager = GetComponent<PlayerStatsManager>();
        if (statsManager == null) ReportDebug("StatsManager no esta asignado en PlayerHealth. Usando vida maxima de fallback.", 2);

        // FindAnyObjectByType puede devolver null; lo guardamos y comprobamos antes de usar.
        inventoryManager = FindAnyObjectByType<InventoryManager>();

        playerMovement = GetComponent<PlayerMovement>();
        playerKnockbackReceiver = GetComponent<PlayerKnockbackReceiver>();
        playerMeleeAttack = GetComponent<PlayerMeleeAttack>();
        playerShieldController = GetComponent<PlayerShieldController>();
        blockSystem = GetComponent<PlayerBlockSystem>();
        combatActionManager = GetComponent<PlayerCombatActionManager>();
        _cinemachineShake = GetComponent<CinemachineImpulseSource>();

        shaderCtrl = GetComponentInChildren<PlayerShaderCtrl>();
        if (shaderCtrl == null) ReportDebug("PlayerShaderCtrl no encontrado en PlayerHealth.", 2);

        animCtrl = GetComponentInChildren<PlayerAnimCtrl>();
        if (animCtrl == null) ReportDebug("PlayerAnimCtrl no encontrado en PlayerHealth.", 2);

        audioController = GetComponentInChildren<PlayerAudioController>();
        if (audioController == null) ReportDebug("PlayerAudioController no encontrado en PlayerHealth.", 2);

        InitializeMaterialCache();

        OnPlayerInstantiated?.Invoke(this);
    }

    private void Start()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        bool isTutoScene = activeSceneName == "HUB";
        bool isTutoSceneComplete = activeSceneName == "TutorialCompleto";

        if ((isTutoScene || isTutoSceneComplete) && statsManager != null)
        {
            statsManager.ResetRunStatsToDefaults();

            if (inventoryManager != null)
            {
                inventoryManager.ClearInventory();
            }
            else
            {
                ReportDebug("InventoryManager es null en Start() al reiniciar tutorial.", 2);
            }

            float maxHealth = statsManager.GetStat(StatType.MaxHealth);
            if (statsManager._currentStatSO != null)
            {
                statsManager._currentStatSO.currentHealth = maxHealth;
                ReportDebug($"Vida del SO forzada a MaxHealth ({maxHealth}) para el reinicio en la escena {sceneToLoadOnDeath}.", 1);
            }
            else
            {
                ReportDebug("currentStatSO es null en Start() al forzar vida en tutorial.", 2);
            }
        }

        InitializeCurrentHealthFromSO();
        InitializeShieldUpgradeFromSO();

        SyncCurrentHealthToSO();

        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        UpdateLifeStage(true);

        isInitialized = true;
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
    }

    private void OnDestroy()
    {
        CleanupMaterialCache();
    }

    #endregion

    #region Initialization & Data Sync

    private void InitializeShieldUpgradeFromSO()
    {
        if (statsManager != null && statsManager._currentStatSO != null)
        {
            HasShieldBlockUpgrade = statsManager._currentStatSO.isShieldBlockUpgradeActive;
        }
    }

    private void InitializeCurrentHealthFromSO()
    {
        if (statsManager != null && statsManager._currentStatSO != null)
        {
            float maxHealthValue = MaxHealth;
            float soCurrentHealth = statsManager._currentStatSO.currentHealth;

            bool isTutoScene = SceneManager.GetActiveScene().name == sceneToLoadOnDeath;

            if (isTutoScene)
            {
                currentHealth = maxHealthValue;
                statsManager._currentStatSO.currentHealth = maxHealthValue;
                ReportDebug($"Escena de reinicio ({SceneManager.GetActiveScene().name}) detectada. Vida restaurada a MaxHealth: {currentHealth}", 1);
            }
            else
            {
                currentHealth = Mathf.Clamp(soCurrentHealth, 0, maxHealthValue);
                ReportDebug($"Vida actual cargada desde SO en escena {SceneManager.GetActiveScene().name}: {currentHealth}/{maxHealthValue}", 1);
            }
        }
        else
        {
            currentHealth = fallbackMaxHealth;
        }
    }

    private void SyncCurrentHealthToSO()
    {
        if (statsManager != null && statsManager._currentStatSO != null)
        {
            statsManager._currentStatSO.currentHealth = currentHealth;
        }
    }

    /// <summary>
    /// Maneja los cambios de stats.
    /// </summary>
    private void HandleStatChanged(StatType statType, float newValue)
    {
        if (statType == StatType.MaxHealth)
        {
            float maxHealthBeforeChange = MaxHealth;
            float percentage = currentHealth / Mathf.Max(1, maxHealthBeforeChange);

            currentHealth = Mathf.Clamp(newValue * percentage, 0, newValue);

            if (isInitialized)
            {
                SyncCurrentHealthToSO();
            }
            else
            {
                ReportDebug("Sincronizacion de vida omitida debido a inicializacion temprana (currentHealth=0).", 1);
            }

            OnHealthChanged?.Invoke(currentHealth, newValue);
            UpdateLifeStage();

            ReportDebug($"Nueva vida maxima: {newValue}, vida actual ajustada a {currentHealth}", 1);
        }
    }

    #endregion

    #region Core Health & Combat

    /// <summary>
    /// Funcion que aplica dano al jugador.
    /// </summary>
    public void TakeDamage(float damageAmount, bool isCostDamage = false, AttackDamageType attackDamageType = AttackDamageType.Melee)
    {
        if (isDying) return;

        // Si el dano no es por costo de esencia, verifica invulnerabilidades y bloqueos.
        if (!isCostDamage && (isDamageInvulnerable || IsInvulnerable))
        {
            ReportDebug("El jugador es invulnerable y no recibe dano.", 1);
            return;
        }

        // Si el dano no es por costo de esencia y el jugador tiene la mejora de bloqueo de escudo, intenta bloquear el dano.
        if (!isCostDamage && HasShieldBlockUpgrade)
        {
            if (isShieldBlockReady)
            {
                isShieldBlockReady = false;
                ReportDebug("El escudo ha bloqueado el dano entrante.", 1);

                StartCoroutine(ShieldBlockCooldownRoutine());
                return;
            }
        }

        // Si el jugador esta marcado por Astaroth y el dano no es por costo de esencia, aplica el efecto de la marca.
        if (IsMarkedByAstaroth && !isCostDamage)
        {
            damageAmount *= 2f; // Duplicar el daño entrante
            IsMarkedByAstaroth = false;   // Consumir la marca inmediatamente

            ReportDebug($"<color=red>Marca consumida: El jugador recibe el doble de daño ({damageAmount}).</color>", 1);
        }

        float damageToApply = ApplyDamageTakenStat(damageAmount, isCostDamage);

        // Primero aplica el daño a la vida temporal si el jugador tiene.
        if (currentTemporaryHealth > 0f)
        {
            float remainingTemporaryHealth = currentTemporaryHealth - damageToApply;
            if (remainingTemporaryHealth >= 0f)
            {
                currentTemporaryHealth = remainingTemporaryHealth;
                damageToApply = 0f;
            }
            else
            {
                damageToApply = -remainingTemporaryHealth;
                currentTemporaryHealth = 0f;
            }

            if (currentTemporaryHealth <= 0f && temporaryHealthDecayCoroutine != null)
            {
                StopCoroutine(temporaryHealthDecayCoroutine);
                temporaryHealthDecayCoroutine = null;
            }

            OnDamageReceived?.Invoke(damageToApply);
            ReportDebug($"Vida temporal restante después del golpe: {currentTemporaryHealth}. Daño restante a vida normal: {damageToApply}", 1);
        }

        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;

        // Luego aplica el daño restante a la vida normal.
        if (damageToApply > 0f)
        {
            OnDamageReceived?.Invoke(damageToApply);

            animCtrl?.PlayDamage();
            audioController?.PlayDamageSound();
            shaderCtrl?.DamageTrigger();

            Vector3 customVelocity = damageAmount switch
            {
                < 10f => new Vector3(0.2f, -1f, 0f),
                < 20f => new Vector3(0.5f, -2.5f, 0f),
                < 30f => new Vector3(0.9f, -5f, 0f),
                _ => new Vector3(1.4f, -10f, 0f)
            };

            if (!isShakeCooldown)
            {
                _cinemachineShake.DefaultVelocity = customVelocity;
                _cinemachineShake.GenerateImpulse();
                isShakeCooldown = true;
                Invoke(nameof(ResetShakeCooldown), shakeCooldown);
            }

            if (damageUIEffect != null) damageUIEffect.PrimeraHerida = true;

            currentHealth -= damageToApply;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

            if (Mathf.RoundToInt(currentHealth) % 10 == 0) ReportDebug($"El jugador ha recibido {damageToApply} de dano. Vida actual: {currentHealth}/{maxHealth}", 1);

            if (!isCostDamage)
            {
                if (damageFlashCoroutine != null) StopCoroutine(damageFlashCoroutine);
                damageFlashCoroutine = StartCoroutine(DamageFlashRoutine());

                isDamageInvulnerable = true;
                if (damageInvulnerabilityCoroutine != null) StopCoroutine(damageInvulnerabilityCoroutine);
                damageInvulnerabilityCoroutine = StartCoroutine(DamageInvulnerabilityRoutine());
            }

            if (currentHealth <= 0)
            {
                Die();
                return;
            }
        }

        SyncCurrentHealthToSO();

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateLifeStage();

        UpdateTemporaryHealthUI();
    }

    private float ApplyDamageTakenStat(float damageAmount, bool isCostDamage)
    {
        if (isCostDamage)
        {
            Debug.LogWarning($"[DAÑO] El daño es de tipo 'isCostDamage', ignorando reducciones. Daño aplicado: {damageAmount}");
            return damageAmount;
        }

        if (statsManager == null)
        {
            Debug.LogError("[DAÑO] ¡statsManager es NULL! No se puede leer la reducción de daño.");
            return damageAmount;
        }

        float endurance = statsManager.GetCurrentStat(StatType.Endurance);
        float finalDamage;

        // Lógica de cálculo de resistencia dividida
        if (endurance >= 100f)
        {
            // Para 100 o más: El daño se reduce inversamente proporcional.
            finalDamage = damageAmount * (100f / endurance);
        }
        else
        {
            // Para menos de 100: El multiplicador sube de forma sucesiva.
            float damageMultiplier = 2f - (endurance / 100f);
            finalDamage = damageAmount * damageMultiplier;
        }

        Debug.Log($"[DAÑO] Base: {damageAmount} | Resistencia: {endurance} | Final -> {finalDamage}");

        return finalDamage;
    }

    private void ResetShakeCooldown()
    {
        isShakeCooldown = false;
    }

    /// <summary>
    /// Funcion que cura al jugador.
    /// </summary>
    public void Heal(float healAmount)
    {
        // Verifica si esta bloqueada por algun efecto.
        if (IsKillHealBlocked)
        {
            ReportDebug($"Curación BLOQUEADA por Distorsión.", 2);
            return;
        }

        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;

        // Si el jugador tiene el Amuleto de Resistencia, la curación se aplica a la vida temporal en lugar de la vida normal.
        if (HasAmuletOfEndurance)
        {
            AddTemporaryHealth(healAmount, maxHealth);
            ReportDebug($"Curación desviada a vida temporal debido al Amuleto. Cantidad: {healAmount}", 1);
        }
        else
        {
            currentHealth += healAmount;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            ReportDebug($"El jugador ha sido curado {healAmount} de vida normal.", 1);
        }

        // Reproducir sonido de absorción de vida solo si realmente se ha curado algo y el audio controller está asignado.
        if (healAmount > 0 && audioController != null)
        {
            audioController.PlayLifeAbsorbSound();
        }

        SyncCurrentHealthToSO();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        UpdateLifeStage();
        UpdateTemporaryHealthUI();

        onHealling?.Invoke();

        // if (currentHealth == maxHealth)
        shaderCtrl?.HealingTrigger();
    }

    private void Die()
    {
        if (isDying) return;

        if (combatActionManager != null)
        {
            combatActionManager.InterruptCombatActions();
        }

        isDying = true;

        if (audioController != null)
        {
            audioController.PlayDeathSound();
        }

        ReportDebug("El jugador ha muerto. Cargando escena: " + sceneToLoadOnDeath, 1);

        if (playerMovement != null) playerMovement.enabled = false;
        if (playerMeleeAttack != null) playerMeleeAttack.enabled = false;
        if (playerShieldController != null) playerShieldController.enabled = false;

        Collider playerCollider = GetComponent<Collider>();
        if (playerCollider != null) playerCollider.enabled = false;

        var sm = statsManager;
        var im = inventoryManager;

        if (deathSequence != null)
        {
            deathSequence.StartSequence(() => StartCoroutine(CleanupDelay(sm, im, 1f)));
            onDeathing?.Invoke();
        }
        else
        {
            ExecuteDeathCleanup(sm, im);
        }

        // deathSequence.deathDirector.stopped += 

        // if (FadeController.Instance != null)
        // {
        //     StartCoroutine(FadeController.Instance.FadeOut(
        //         fadeColor: deathFadeColor,
        //         onComplete: () => ExecuteDeathCleanup(sm, im)));
        // }
        // else
        // {
            // ExecuteDeathCleanup(sm, im);
        // }
    }

    /// <summary>
    /// Ejecuta la limpieza de estado al morir el jugador: reset de stats, inventario y carga de escena.
    /// </summary>
    private void ExecuteDeathCleanup(PlayerStatsManager sm, InventoryManager im)
    {
        if (sm != null)
        {
            if (im != null && im.ActiveBehavioralEffects != null)
            {
                sm.RemoveAllBehavioralEffects(im.ActiveBehavioralEffects);
            }

            sm.ClearAllNamedModifiers();
            sm.ResetRunStatsToDefaults();
            sm.ResetStatsOnDeath();
        }
        else
        {
            ReportDebug("StatsManager es null en ExecuteDeathCleanup().", 2);
        }

        if (im != null)
        {
            im.ClearInventory();
        }
        else
        {
            ReportDebug("InventoryManager es null en ExecuteDeathCleanup().", 2);
        }

        MerchantDialogHandler.ResetReputationState();
        SceneManager.LoadScene(sceneToLoadOnDeath);
    }

    private IEnumerator CleanupDelay(PlayerStatsManager sm, InventoryManager im, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        ExecuteDeathCleanup(sm, im);
    }

    public float GetCurrentHealth() { return currentHealth; }
    public float GetMaxHealth() { return MaxHealth; }
    public bool IsDead() { return isDying; }
    public void BlockKillHeal(bool isBlocked)
    {
        IsKillHealBlocked = isBlocked;
        Debug.Log($"[PlayerHealth] Curación por muerte {(isBlocked ? "BLOQUEADA" : "RESTAURADA")} por Distorsión.");
    }

    #endregion

    #region Life Stage System

    /// <summary>
    /// Funcion que actualiza la etapa de vida del jugador y notifica si ha cambiado.
    /// </summary>
    private void UpdateLifeStage(bool forceNotify = false)
    {
        LifeStage oldStage = CurrentLifeStage;
        float maxHealth = statsManager != null ? statsManager.GetStat(StatType.MaxHealth) : fallbackMaxHealth;
        float healthPercentage = currentHealth / maxHealth;

        // Determina la etapa de vida actual basada en el porcentaje de salud.
        if (healthPercentage > LifeStageYoungThreshold) CurrentLifeStage = LifeStage.Young;
        else if (healthPercentage > LifeStageAdultThreshold) CurrentLifeStage = LifeStage.Adult;
        else CurrentLifeStage = LifeStage.Elder;

        // Solo notificar si la etapa realmente ha cambiado, o si se fuerza al inicio
        if (CurrentLifeStage != oldStage || forceNotify)
        {
            OnLifeStageChanged?.Invoke(CurrentLifeStage);
            UpdateModelForLifeStage(CurrentLifeStage);

            int ageStageValue = 0;
            switch (CurrentLifeStage)
            {
                case PlayerHealth.LifeStage.Young:
                    ageStageValue = 3;
                    break;
                case PlayerHealth.LifeStage.Adult:
                    ageStageValue = 2;
                    break;
                case PlayerHealth.LifeStage.Elder:
                    ageStageValue = 1;
                    break;
            }

            animCtrl?.SetAgeStage(ageStageValue);
            shaderCtrl?.AgeChangeTrigger();
            ReportDebug($"Etapa de vida cambiada a {CurrentLifeStage}. Animator AgeStage seteado a {ageStageValue}.", 1);

            if (lifeStageText != null)
            {
                lifeStageText.text = GetLifeStageString(CurrentLifeStage);
            }

            if (!forceNotify && isInitialized)
            {
                TriggerAgeTransitionEffect();
            }
        }
    }

    /// <summary>
    /// Ajusta la escala y la posicion vertical del modelo del jugador segun su etapa de vida.
    /// </summary>
    private void UpdateModelForLifeStage(LifeStage newStage)
    {
        if (playerModelTransform == null)
        {
            ReportDebug("No se ha asignado 'playerModelTransform'. No se puede actualizar el modelo.", 2);
            return;
        }

        Vector3 targetScale = Vector3.one;
        float targetYOffset = 0f;

        switch (newStage)
        {
            case LifeStage.Young:
                targetScale = scaleYoung;
                targetYOffset = yOffsetYoung;
                break;
            case LifeStage.Adult:
                targetScale = scaleAdult;
                targetYOffset = yOffsetAdult;
                break;
            case LifeStage.Elder:
                targetScale = scaleElder;
                targetYOffset = yOffsetElder;
                break;
        }

        playerModelTransform.localScale = targetScale;
        playerModelTransform.localPosition = new Vector3(0, targetYOffset, 0);
        ReportDebug($"Modelo actualizado para la etapa {newStage}. Escala: {targetScale}, Posicion Y: {targetYOffset}", 1);
    }

    private string GetLifeStageString(LifeStage stage)
    {
        switch (stage)
        {
            case LifeStage.Young: return "Joven";
            case LifeStage.Adult: return "Adulto";
            case LifeStage.Elder: return "Anciano";
            default: return stage.ToString();
        }
    }

    private void TriggerAgeTransitionEffect()
    {
        if (afterimagePrefab == null || sprtRenderer == null) return;

        GameObject ghost = Instantiate(afterimagePrefab, sprtRenderer.transform.position, sprtRenderer.transform.rotation);
        ghost.transform.localScale = sprtRenderer.transform.lossyScale;

        SpriteRenderer ghostSR = ghost.GetComponent<SpriteRenderer>();
        if (ghostSR != null)
        {
            ghostSR.sprite = sprtRenderer.sprite;
            ghostSR.flipX = sprtRenderer.flipX;
            ghostSR.flipY = sprtRenderer.flipY;

            if (whiteFlashMaterial != null)
            {
                ghostSR.material = whiteFlashMaterial;
            }
            else
            {
                ghostSR.color = new Color(10f, 10f, 10f, 1f);
            }

            ghostSR.sortingLayerID = sprtRenderer.sortingLayerID;
            ghostSR.sortingOrder = sprtRenderer.sortingOrder + 1;

            StartCoroutine(AnimateAgeGhost(ghostSR, ghost));
        }
        else
        {
            Destroy(ghost);
        }
    }

    private IEnumerator AnimateAgeGhost(SpriteRenderer ghostSR, GameObject ghostObj)
    {
        float timer = 0f;
        Color startColor = ghostSR.color;

        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / transitionDuration;
            float currentAlpha = Mathf.Lerp(1f, 0f, progress);

            Color newColor = startColor;
            newColor.a = currentAlpha;
            ghostSR.color = newColor;

            yield return null;
        }

        Destroy(ghostObj);
    }

    #endregion

    #region Material Cache Management & VFX

    private void InitializeMaterialCache()
    {
        if (playerModelTransform == null) return;

        cachedMeshRenderers = playerModelTransform.GetComponentsInChildren<MeshRenderer>();
        materialPropertyBlocks = new Dictionary<MeshRenderer, MaterialPropertyBlock>();
        originalEmissionColors = new Dictionary<MeshRenderer, Color[]>();
        originalEmissionEnabled = new Dictionary<MeshRenderer, bool>();

        foreach (var renderer in cachedMeshRenderers)
        {
            if (renderer == null) continue;

            materialPropertyBlocks[renderer] = new MaterialPropertyBlock();

            Material[] materials = renderer.sharedMaterials;
            Color[] colors = new Color[materials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null)
                {
                    if (materials[i].HasProperty(EmissionColorID))
                    {
                        colors[i] = materials[i].GetColor(EmissionColorID);
                        originalEmissionEnabled[renderer] = materials[i].IsKeywordEnabled("_EMISSION");
                    }
                    else
                    {
                        colors[i] = Color.black;
                        originalEmissionEnabled[renderer] = false;
                    }
                }
            }
            originalEmissionColors[renderer] = colors;
        }

        ReportDebug($"Cache de materiales inicializado con {cachedMeshRenderers.Length} renderers.", 1);
    }

    private void CleanupMaterialCache()
    {
        if (materialPropertyBlocks != null)
        {
            materialPropertyBlocks.Clear();
            materialPropertyBlocks = null;
        }

        if (originalEmissionColors != null)
        {
            originalEmissionColors.Clear();
            originalEmissionColors = null;
        }

        if (originalEmissionEnabled != null)
        {
            originalEmissionEnabled.Clear();
            originalEmissionEnabled = null;
        }

        cachedMeshRenderers = null;
    }

    private void ApplyDamageEmission()
    {
        if (cachedMeshRenderers == null || materialPropertyBlocks == null) return;

        Color emissionColor = damageEmissionColor * damageEmissionIntensity;

        foreach (var renderer in cachedMeshRenderers)
        {
            if (renderer == null) continue;

            if (!materialPropertyBlocks.TryGetValue(renderer, out MaterialPropertyBlock mpb)) continue;
            renderer.GetPropertyBlock(mpb);

            mpb.SetColor(EmissionColorID, emissionColor);
            renderer.SetPropertyBlock(mpb);

            Material[] materials = renderer.sharedMaterials;
            foreach (var mat in materials)
            {
                if (mat != null && mat.HasProperty(EmissionColorID))
                {
                    mat.EnableKeyword("_EMISSION");
                }
            }
        }
    }

    private void RestoreOriginalEmission()
    {
        if (cachedMeshRenderers == null || materialPropertyBlocks == null) return;

        foreach (var renderer in cachedMeshRenderers)
        {
            if (renderer == null) continue;

            if (!materialPropertyBlocks.TryGetValue(renderer, out MaterialPropertyBlock mpb)) continue;
            renderer.GetPropertyBlock(mpb);

            if (originalEmissionColors.TryGetValue(renderer, out Color[] originalColors))
            {
                mpb.SetColor(EmissionColorID, originalColors[0]);
                renderer.SetPropertyBlock(mpb);
            }

            if (originalEmissionEnabled.TryGetValue(renderer, out bool wasEnabled))
            {
                Material[] materials = renderer.sharedMaterials;
                foreach (var mat in materials)
                {
                    if (mat != null && mat.HasProperty(EmissionColorID))
                    {
                        if (originalEmissionEnabled[renderer])
                        {
                            mat.EnableKeyword("_EMISSION");
                        }
                        else
                        {
                            mat.DisableKeyword("_EMISSION");
                        }
                    }
                }
            }
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        for (int i = 0; i < damageFlashCount; i++)
        {
            ApplyDamageEmission();
            yield return new WaitForSeconds(damageFlashDuration);

            RestoreOriginalEmission();
            yield return new WaitForSeconds(damageFlashDuration);
        }
        damageFlashCoroutine = null;
    }

    private IEnumerator DamageInvulnerabilityRoutine()
    {
        isDamageInvulnerable = true;
        ReportDebug($"El jugador es invulnerable por dano continuo durante {damageInvulnerabilityTime} segundos.", 1);

        float blinkInterval = 0.1f;
        float timer = 0f;

        if (sprtRenderer == null)
        {
            yield return new WaitForSeconds(damageInvulnerabilityTime);
            isDamageInvulnerable = false;
            damageInvulnerabilityCoroutine = null;
            ReportDebug("La invulnerabilidad por dano ha terminado (no hay SpriteRenderer).", 1);
            yield break;
        }

        while (timer < damageInvulnerabilityTime)
        {
            sprtRenderer.color = new Color(1f, 1f, 1f, 0.5f);
            yield return new WaitForSeconds(blinkInterval);

            sprtRenderer.color = Color.white;
            yield return new WaitForSeconds(blinkInterval);

            timer += blinkInterval * 2;
        }

        isDamageInvulnerable = false;
        damageInvulnerabilityCoroutine = null;
        ReportDebug("La invulnerabilidad por dano ha terminado.", 1);

        sprtRenderer.color = Color.white;
    }

    #endregion

    #region Upgrades & Temporary Health

    public void EnableShieldBlockUpgrade()
    {
        HasShieldBlockUpgrade = true;

        if (statsManager != null && statsManager._currentStatSO != null)
        {
            statsManager._currentStatSO.isShieldBlockUpgradeActive = true;
        }

        ReportDebug("La mejora de bloqueo de escudo ha sido activada.", 1);
    }

    public void DisableShieldBlockUpgrade()
    {
        HasShieldBlockUpgrade = false;

        if (statsManager != null && statsManager._currentStatSO != null)
        {
            statsManager._currentStatSO.isShieldBlockUpgradeActive = false;
        }

        ReportDebug("La mejora de bloqueo de escudo ha sido desactivada.", 1);
    }

    private IEnumerator ShieldBlockCooldownRoutine()
    {
        ReportDebug($"El escudo bloqueara de nuevo en {shieldBlockCooldown} segundos.", 1);
        yield return new WaitForSeconds(shieldBlockCooldown);

        isShieldBlockReady = true;
        ReportDebug("El escudo esta listo para bloquear de nuevo.", 1);
    }

    public void AcquireAmuletOfEndurance()
    {
        HasAmuletOfEndurance = true;
        ReportDebug("Amuleto adquirido. La curación normal está deshabilitada.", 1);
    }

    public void ActivateEnduranceAmulet()
    {
        if (HasAmuletOfEndurance) return;

        HasAmuletOfEndurance = true;
        statsManager.ApplyNamedModifier("EnduranceAmulet_DR", StatType.Endurance, 0.15f);

        ReportDebug("¡Amuleto de Endurecimiento ACTIVADO! Reducción de daño +15% aplicada.", 2);
    }

    public void AddTemporaryHealth(float amount, float maxLimit)
    {
        maxTemporaryHealthLimit = Mathf.Max(maxTemporaryHealthLimit, maxLimit);

        currentTemporaryHealth += amount;
        currentTemporaryHealth = Mathf.Min(currentTemporaryHealth, maxTemporaryHealthLimit);

        if (temporaryHealthDecayCoroutine != null)
        {
            StopCoroutine(temporaryHealthDecayCoroutine);
        }
        temporaryHealthDecayCoroutine = StartCoroutine(TemporaryHealthDecayRoutine());

        UpdateTemporaryHealthUI();
        ReportDebug($"Vida temporal añadida: {amount}. Total: {currentTemporaryHealth}/{maxTemporaryHealthLimit}. Tiempo de decaimiento reseteado.", 1);
    }

    public void GrantTemporaryHealthOnKill(float amount)
    {
        if (!HasAmuletOfEndurance) return;

        float maxLimit = 30f;
        float currentMax = statsManager.GetCurrentStat(StatType.MaxHealth);

        if (currentHealth < currentMax)
        {
            ReportDebug("Vida no está al máximo, no se puede añadir vida temporal.", 1);
            return;
        }

        AddTemporaryHealth(amount, maxLimit);
    }

    private IEnumerator TemporaryHealthDecayRoutine()
    {
        float graceTimer = 0f;
        while (graceTimer < temporaryHealthDuration)
        {
            if (DialogManager.Instance != null && DialogManager.Instance.IsActive)
            {
                yield return null;
                continue;
            }

            graceTimer += Time.deltaTime;
            yield return null;
        }

        ReportDebug("Tiempo de gracia de vida temporal terminado. Iniciando decaimiento rápido.", 1);

        float startHealth = currentTemporaryHealth;
        float decayTimer = 0f;

        while (decayTimer < temporaryHealthDecaySpeed)
        {
            if (DialogManager.Instance != null && DialogManager.Instance.IsActive)
            {
                yield return null;
                continue;
            }

            decayTimer += Time.deltaTime;
            currentTemporaryHealth = Mathf.Lerp(startHealth, 0f, decayTimer / temporaryHealthDecaySpeed);
            UpdateTemporaryHealthUI();
            yield return null;
        }

        currentTemporaryHealth = 0f;
        UpdateTemporaryHealthUI();
        temporaryHealthDecayCoroutine = null;
        maxTemporaryHealthLimit = 0f;

        ReportDebug("Decaimiento de vida temporal completado.", 1);
    }

    public void UpdateTemporaryHealthUI()
    {
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.SetTemporaryHealthValues(currentTemporaryHealth, MaxHealth);
        }
    }

    #endregion

    #region Status Effects & Debuffs

    public void ApplyStun(float stunDuration)
    {
        if (isDying) return;

        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
        }

        stunCoroutine = StartCoroutine(StunRoutine(stunDuration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        ReportDebug($"Jugador aturdido por {duration}s debido a rotura de escudo.", 2);

        yield return new WaitForSeconds(duration);

        isStunned = false;

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(true);
        }

        stunCoroutine = null;
        ReportDebug("Stun finalizado. Recarga de durabilidad iniciada.", 1);
    }

    public bool IsStunned()
    {
        return isStunned;
    }

    public void ApplyPoison(float duration, float damagePerSecond, float tickInterval = 1f)
    {
        if (poisonCoroutine != null) StopCoroutine(poisonCoroutine);
        poisonCoroutine = StartCoroutine(PoisonRoutine(duration, damagePerSecond, tickInterval));
    }

    private IEnumerator PoisonRoutine(float duration, float damagePerSecond, float tickInterval)
    {
        float tiempo = 0f;
        while (tiempo < duration)
        {
            if (isDying) yield break;
            float tickDamage = damagePerSecond * tickInterval;
            TakeDamage(tickDamage);
            yield return new WaitForSeconds(tickInterval);
            tiempo += tickInterval;
        }
        poisonCoroutine = null;
    }

    public void ApplySlow(float slowFraction, float duration)
    {
        if (slowFraction < 0.01f) slowFraction = 0.01f;
        if (slowCoroutine != null) StopCoroutine(slowCoroutine);
        slowSpeedMultiplier = Mathf.Clamp01(slowFraction);
        slowCoroutine = StartCoroutine(SlowRoutine(duration));
    }

    private IEnumerator SlowRoutine(float duracion)
    {
        isSlowed = true;
        float tiempo = 0f;
        while (tiempo < duracion)
        {
            if (isDying) yield break;
            tiempo += Time.deltaTime;
            yield return null;
        }
        isSlowed = false;
        slowSpeedMultiplier = 1f;
        slowCoroutine = null;
    }

    public void RemoveSlow()
    {
        if (slowCoroutine != null) StopCoroutine(slowCoroutine);
        isSlowed = false;
        slowSpeedMultiplier = 1f;
        slowCoroutine = null;
    }

    #endregion

    #region Legacy & Experimental Features

    public void ApplyKnockback(Vector3 direction, float force, float duration)
    {
        if (isDying || playerMovement.IsDashing) return;

        if (TryGetComponent<PlayerKnockbackReceiver>(out var knockbackReceiver))
        {
            direction.y = 0f;

            knockbackReceiver.ApplyKnockback(direction, force, duration);
        }
    }

    #endregion

    #region Logging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[PlayerHealth] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerHealth] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerHealth] {message}");
                break;
            default:
                Debug.Log($"[PlayerHealth] {message}");
                break;
        }
    }

    #endregion
}