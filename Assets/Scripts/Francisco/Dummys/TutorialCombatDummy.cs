using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public enum DummyLogicType { RequiredHitCount, HealthState }

public enum DamageType { Melee, Shield, Other }

public enum EnemyAuraState { Default, Fury, Static }

[System.Serializable]
public struct HealthStateColor
{
    [Range(0f, 1f)]
    public float healthThreshold;
    public Color stateColor;
    public string stateName;
}

public class TutorialCombatDummy : MonoBehaviour, IDamageable
{
    #region GENERAL FIELDS

    [Header("General Settings")]
    [SerializeField] private DummyLogicType dummyLogic = DummyLogicType.RequiredHitCount;
    [SerializeField] private float maxHealth = 1f;
    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public DummyLogicType DummyLogic => dummyLogic;

    [Header("Vulnerability")]
    [SerializeField] private DamageType requiredDamageType = DamageType.Melee;

    #endregion

    #region TUTORIAL_STATE_LOGIC 

    [Header("Aura State Logic")]
    [SerializeField] private Renderer slimeRenderer;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color furyColor = Color.red;
    [SerializeField] private Color staticColor = Color.blue;

    private EnemyAuraState currentStateAura = EnemyAuraState.Default;

    public UnityEvent OnDummyKilled;

    public EnemyAuraState CurrentStateAura => currentStateAura;

    #endregion

    #region HIT COUNT LOGIC  

    [Header("Hit Count Logic")]
    [SerializeField] private int requiredHits = 3;
    private int currentHits = 0;

    public int CurrentHits => currentHits;
    public int RequiredHits => requiredHits;

    #endregion

    #region HEALTH STATE LOGIC  

    [Header("Health State Logic")]
    [SerializeField] private HealthStateColor[] healthStates;
    private HealthStateColor? currentState;

    #endregion

    #region VISUALS, COOLDOWN & STATE

    [Header("Visuals & Cooldown")]
    [SerializeField] private float hitCooldownTime = 0.5f;
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;
    private Color originalColor;
    private bool canBeHit = true;
    private Coroutine colorChangeCoroutine;

    [Header("Delays")]
    [SerializeField] private float deathDelay = 0.5f;

    [Header("Dialogues & Audio")]
    public DialogLine[] FirstHitDialog;
    public DialogLine[] DeathDialog;
    public DialogLine[] DefeatedSequenceDialog;

    public AudioClip infernalSound;
    public AudioClip damageSound; // Nuevo AudioClip para el sonido de daño
    private AudioSource audioSource;

    private bool isFirstHit = true;
    private bool isDefeated = false;

    private Collider[] colliders;
    private Renderer[] renderers;

    #endregion

    #region UI & ANIMATION

    [Header("UI & Animation")]
    [SerializeField] private Animator dummyAnimator;
    [SerializeField] private string hitTriggerName = "OnHit";
    [SerializeField] private GameObject dummyUIObject;
    private ICombatDummyUI dummyUI;

    #endregion

    #region ROTATION LOGIC

    [Header("Rotation Logic")]
    [SerializeField] private Transform transformToRotate;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float rotationYOffset = 0f;
    private Coroutine rotationCoroutine;
    [SerializeField] private Transform playerTransform;
    private Quaternion initialRotation;

    #endregion

    #region EVENTS

    [Header("Events")]
    public UnityEvent OnDummyDefeated;
    public UnityEvent OnDeathSequenceStart;

    #endregion

    #region AWAKE / INITIALIZATION

    private void Awake()
    {
        if (dummyLogic == DummyLogicType.RequiredHitCount)
        {
            maxHealth = requiredHits;
            currentHealth = requiredHits;
        }
        else
        {
            currentHealth = maxHealth;
        }

        colliders = GetComponents<Collider>().Where(c => c.enabled).ToArray();
        renderers = GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        }

        if (dummyUIObject != null)
        {
            dummyUI = dummyUIObject.GetComponent<ICombatDummyUI>();
        }
        if (dummyUI == null)
        {
            dummyUI = GetComponent<ICombatDummyUI>();
        }

        if (dummyAnimator == null)
        {
            dummyAnimator = GetComponent<Animator>();
        }

        if (transformToRotate == null)
        {
            transformToRotate = transform.parent != null ? transform.parent : transform;
        }

        initialRotation = transformToRotate.rotation;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        if (renderers.Length > 0 && renderers[0].material != null)
        {
            originalColor = renderers[0].material.color;
            if (dummyLogic == DummyLogicType.HealthState)
            {
                CheckHealthState(true);
            }
        }

        UpdateUI(true);
    }

    #endregion

    #region DAMAGE IMPLEMENTATION

    public void TakeDamage(float damageAmount, bool isCritical = false, AttackDamageType attackDamageType = AttackDamageType.Melee)
    {
        TakeDamage(damageAmount, isCritical, DamageType.Melee, transform.position + transform.forward);
    }

    public void TakeDamage(float damageAmount, bool isCritical, DamageType type)
    {
        TakeDamage(damageAmount, isCritical, type, transform.position + transform.forward);
    }

    public void TakeDamage(float damageAmount, bool isCritical, DamageType type, Vector3 attackSourcePosition)
    {
        if (isDefeated || !canBeHit) return;

        DummyInvulnerabilityController invulnerabilityController = GetComponent<DummyInvulnerabilityController>();
        if (invulnerabilityController != null && invulnerabilityController.IsInvulnerable)
        {
            if (dummyAnimator != null)
            {
                dummyAnimator.SetTrigger(hitTriggerName);
            }

            if (colorChangeCoroutine != null) StopCoroutine(colorChangeCoroutine);
            colorChangeCoroutine = StartCoroutine(FlashColor());

            if (playerTransform != null)
            {
                if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
                rotationCoroutine = StartCoroutine(RotateTowardsPlayer());
            }

            Debug.Log($"[TutorialCombatDummy] {gameObject.name} es invulnerable. Hit visual sin daño real.", this);
            return;
        }

        if (dummyLogic == DummyLogicType.RequiredHitCount && requiredDamageType != type)
        {
            Debug.Log($"Dummy de Contador ignoró daño de {type}. Requiere: {requiredDamageType}");
            return;
        }

        // Reproducir sonido de daño
        if (audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }

        if (dummyAnimator != null)
        {
            dummyAnimator.SetTrigger(hitTriggerName);
        }

        StartCoroutine(HitCooldown());

        if (colorChangeCoroutine != null) StopCoroutine(colorChangeCoroutine);
        colorChangeCoroutine = StartCoroutine(FlashColor());

        if (playerTransform != null)
        {
            if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
            rotationCoroutine = StartCoroutine(RotateTowardsPlayer());
        }

        if (isFirstHit)
        {
            isFirstHit = false;
            StartCoroutine(ExecuteFirstHitDialogueFlow());
        }

        switch (dummyLogic)
        {
            case DummyLogicType.RequiredHitCount:
                HandleHitCount(type);
                break;

            case DummyLogicType.HealthState:
                HandleHealthStateDamage(damageAmount);
                break;
        }
    }

    #endregion

    #region LOGIC HANDLERS

    private void HandleHitCount(DamageType type)
    {
        currentHits++;
        currentHealth--;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"Dummy de Contador golpeado! Tipo: {type}. Hits: {currentHits}/{requiredHits}. Vida: {currentHealth}/{maxHealth}", this);

        UpdateUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void HandleHealthStateDamage(float damageAmount)
    {
        float healthBefore = currentHealth;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"[HEALTH STATE] Daño: {damageAmount:F2} | Vida Anterior: {healthBefore:F2} | Vida Actual: {currentHealth:F2}", this);

        CheckHealthState();

        UpdateUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void CheckHealthState(bool forceUpdate = false)
    {
        float currentHealthRatio = currentHealth / maxHealth;
        HealthStateColor? newState = null;

        var sortedStates = healthStates.OrderByDescending(s => s.healthThreshold);

        foreach (var state in sortedStates)
        {
            if (currentHealthRatio >= state.healthThreshold)
            {
                newState = state;
                break;
            }
        }

        if (newState.HasValue && (forceUpdate || !currentState.HasValue || currentState.Value.stateColor != newState.Value.stateColor))
        {
            currentState = newState;
            Debug.Log($"Dummy cambió a estado: {newState.Value.stateName} ({currentHealthRatio * 100:F0}%)");

            foreach (var rend in renderers)
            {
                if (rend.material.HasProperty("_Color"))
                {
                    rend.material.color = newState.Value.stateColor;
                    originalColor = newState.Value.stateColor;
                }
            }

            UpdateUI();
        }
    }

    private void UpdateUI(bool initialSetup = false)
    {
        if (dummyUI == null) return;

        float healthRatio = currentHealth / maxHealth;
        Color? stateColor = dummyLogic == DummyLogicType.HealthState ? currentState?.stateColor : null;
        dummyUI.UpdateHealthBar(healthRatio, stateColor);
    }

    #endregion

    #region STATE_METHODS_AURA

    public void SetAuraState(EnemyAuraState newState)
    {
        currentStateAura = newState;
        UpdateAuraColor();
    }

    private void UpdateAuraColor()
    {
        if (slimeRenderer == null)
        {
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
            if (allRenderers.Length == 0) return;

            Color targetColor;
            switch (currentStateAura)
            {
                case EnemyAuraState.Fury:
                    targetColor = furyColor;
                    break;
                case EnemyAuraState.Static:
                    targetColor = staticColor;
                    break;
                case EnemyAuraState.Default:
                default:
                    targetColor = defaultColor;
                    break;
            }

            foreach (var rend in allRenderers)
            {
                if (rend.material.HasProperty("_Color"))
                {
                    rend.material.color = targetColor;
                    originalColor = targetColor;
                }
            }

            Debug.Log($"[TutorialCombatDummy] {gameObject.name} cambió a estado {currentStateAura} con color {targetColor}");
            return;
        }

        Color targetColorSpecific;
        switch (currentStateAura)
        {
            case EnemyAuraState.Fury:
                targetColorSpecific = furyColor;
                break;
            case EnemyAuraState.Static:
                targetColorSpecific = staticColor;
                break;
            case EnemyAuraState.Default:
            default:
                targetColorSpecific = defaultColor;
                break;
        }

        if (slimeRenderer.material.HasProperty("_Color"))
        {
            slimeRenderer.material.color = targetColorSpecific;
            originalColor = targetColorSpecific;
        }

        Debug.Log($"[TutorialCombatDummy] {gameObject.name} cambió a estado {currentStateAura} con color {targetColorSpecific}");
    }

    #endregion

    #region EFFECTS / COROUTINES

    private IEnumerator RotateTowardsPlayer()
    {
        if (transformToRotate == null || playerTransform == null)
        {
            rotationCoroutine = null;
            yield break;
        }

        Vector3 direction = (playerTransform.position - transformToRotate.position).normalized;
        direction.y = 0;

        if (direction == Vector3.zero)
        {
            rotationCoroutine = null;
            yield break;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, rotationYOffset, 0);
        while (Quaternion.Angle(transformToRotate.rotation, targetRotation) > 0.1f)
        {
            transformToRotate.rotation = Quaternion.Slerp(transformToRotate.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            yield return null;
        }
        transformToRotate.rotation = targetRotation;
        rotationCoroutine = null;
    }

    private IEnumerator FlashColor()
    {
        if (renderers.Length == 0) yield break;

        foreach (var rend in renderers)
        {
            if (rend.material.HasProperty("_Color"))
            {
                rend.material.color = flashColor;
            }
        }

        yield return new WaitForSeconds(flashDuration);

        foreach (var rend in renderers)
        {
            if (rend.material.HasProperty("_Color"))
            {
                rend.material.color = originalColor;
            }
        }
        colorChangeCoroutine = null;
    }

    private IEnumerator HitCooldown()
    {
        canBeHit = false;
        yield return new WaitForSeconds(hitCooldownTime);
        canBeHit = true;
    }

    #endregion

    #region DEATH LOGIC

    private void Die()
    {
        if (isDefeated) return;
        isDefeated = true;

        StopAllCoroutines();
        DisableDummyVisualsAndPhysics();

        OnDummyKilled?.Invoke(); 

        if (dummyUI != null)
        {
            dummyUI.SetUIActive(dummyLogic, false);
        }

        StartCoroutine(ExecuteDeathDialogueFlowWithDelay());
    }

    private void DisableDummyVisualsAndPhysics()
    {
        foreach (var r in renderers)
        {
            r.enabled = false;
        }

        foreach (var c in colliders)
        {
            c.enabled = false;
        }
    }

    private IEnumerator ExecuteDeathDialogueFlowWithDelay()
    {
        OnDeathSequenceStart?.Invoke();

        if (deathDelay > 0)
        {
            yield return new WaitForSeconds(deathDelay);
        }

        yield return StartCoroutine(ExecuteDeathDialogueFlow());
    }

    private IEnumerator ExecuteDeathDialogueFlow()
    {
        if (DialogManager.Instance == null)
        {
            Debug.LogError("DialogManager no está en la escena.");
            yield break;
        }

        if (DeathDialog != null && DeathDialog.Length > 0)
        {
            DialogManager.Instance.StartDialog(DeathDialog);
            while (DialogManager.Instance.IsActive) { yield return null; }
        }

        if (audioSource != null && infernalSound != null)
        {
            audioSource.PlayOneShot(infernalSound);
        }

        if (DefeatedSequenceDialog != null && DefeatedSequenceDialog.Length > 0)
        {
            DialogManager.Instance.StartDialog(DefeatedSequenceDialog);
            while (DialogManager.Instance.IsActive) { yield return null; }
        }

        OnDummyDefeated?.Invoke();

        gameObject.SetActive(false);
    }

    private IEnumerator ExecuteFirstHitDialogueFlow()
    {
        if (DialogManager.Instance == null)
        {
            Debug.LogError("DialogManager no está en la escena.");
            yield break;
        }

        if (FirstHitDialog != null && FirstHitDialog.Length > 0)
        {
            DialogManager.Instance.StartDialog(FirstHitDialog);
            while (DialogManager.Instance.IsActive) { yield return null; }
        }
    }

    #endregion
}
