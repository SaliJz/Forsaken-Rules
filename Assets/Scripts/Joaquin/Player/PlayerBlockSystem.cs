using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Sistema de bloqueo para el jugador con múltiples tipos de límites configurables.
/// </summary>
public class PlayerBlockSystem : MonoBehaviour, PlayerControlls.IDefenseActions
{
    #region Inspector - References

    [Header("References")]
    [SerializeField] private PlayerAudioController playerAudioController;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerAnimCtrl playerAnimCtrl;
    [SerializeField] private Transform shieldForwardOverride = null;
    //[SerializeField] private GameObject counterProjectilePrefab;
    [SerializeField] private Slider durabilitySlider;
    [SerializeField] private Image durabilityFillImage;
    [SerializeField] private TextMeshProUGUI durabilityPercentageText;
    [SerializeField] private GameObject durabilityUIGroup;
    [SerializeField] private ParticleSystem blockParticles;
    [SerializeField] private GameObject blockVFX;
    [SerializeField] private GameObject blockBreakVFX;

    #endregion

    #region Inspector - Core Settings

    [Header("Core Configuration")]
    [SerializeField] private bool blockingEnabled = true;
    [SerializeField, Range(0f, 360f)] private float frontBlockAngle = 170f;

    #endregion

    #region Inspector - Rotation Settings

    [Header("Rotation Mode")]
    [SerializeField] private bool useMouseRotation = true;
    [SerializeField] private float rotationSpeed = 10f;
    [Tooltip("Tiempo en segundos que el personaje forzará la mirada al objetivo al disparar.")]
    [SerializeField] private float counterRotationDuration = 0.25f;

    #endregion

    #region Inspector - Durability Settings

    [Header("Durability System")]
    [Tooltip("Vida del escudo (Base 30).")]
    [SerializeField] private float maxDurability = 30f;
    [SerializeField] private float currentDurability;
    [SerializeField] private float durabilityRechargeTime = 5f;
    [SerializeField] private float durabilityDrainRate = 15.24f;
    [SerializeField, Range(0f, 1f)] private float minimumDurabilityToUse = 0.99f;
    [SerializeField] private float rechargeDelay = 2f;
    [SerializeField] private float drainDelay = 0.2f;
    #endregion

    #region Inspector - Counter Attack Settings

    [Header("Counter Attack System")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float autoAimRange = 20f;
    //[SerializeField] private float multiplierYoung = 1.5f;
    //[SerializeField] private float multiplierAdult = 2.0f;
    //[SerializeField] private float multiplierElder = 2.5f;

    #endregion

    #region Inspector - Stun Settings

    [Header("Stun Settings")]
    [SerializeField] private float stunDurationYoung = 1.5f;
    [SerializeField] private float stunDurationAdult = 1.0f;
    [SerializeField] private float stunDurationElder = 0.5f;

    #endregion

    #region Inspector - UI Polish Settings

    [Header("UI Polish")]
    [SerializeField] private float hideDelay = 2f;
    [SerializeField] private bool useSmoothing = true;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private bool pulseWhenLow = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseMinScale = 0.95f;
    [SerializeField] private float pulseMaxScale = 1.05f;
    [SerializeField] private Color fullDurabilityColor = new Color(0.2f, 0.8f, 1f);
    [SerializeField] private Color midDurabilityColor = new Color(1f, 0.8f, 0f);
    [SerializeField] private Color lowDurabilityColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private Color emptyDurabilityColor = new Color(0.5f, 0.5f, 0.5f);

    #endregion

    #region Inspector - Debug Settings

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    #endregion

    #region Internal State

    private PlayerControlls playerControls;
    private PlayerMovement playerMovement;
    private PlayerShieldController playerShieldController;
    private PlayerCombatActionManager combatActionManager;
    private Camera mainCamera;

    private bool isStunned = false;
    private bool isDurabilityRecharging = false;
    private float accumulatedDamage = 0f;

    private Coroutine hideDurabilityCoroutine;
    private Coroutine durabilityRechargeCoroutine;
    private Coroutine durabilityDrainCoroutine;
    private Coroutine stunCoroutine;
    private Coroutine counterRotationCoroutine;

    private Vector2 movementInput;
    private Vector3 originalScale;
    private bool isInitialized = false;

    #endregion

    #region Public Properties & Events

    public bool IsInputBlocked { get; set; } = false;
    public bool IsBlocking { get; private set; }

    public event Action OnBlockStart;
    public event Action OnBlockEnd;
    public event Action OnBlockBreak;
    public event Action<float> OnDurabilityChanged;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerControls = new PlayerControlls();
        playerControls.Defense.SetCallbacks(this);
        playerControls.Movement.SetCallbacks(null);

        playerMovement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        playerShieldController = GetComponent<PlayerShieldController>();
        combatActionManager = GetComponent<PlayerCombatActionManager>();
        playerAudioController = GetComponent<PlayerAudioController>();
        playerAnimCtrl = GetComponentInChildren<PlayerAnimCtrl>();

        if (playerAnimCtrl == null) Debug.LogWarning("[PlayerBlockSystem] PlayerAnimCtrl no encontrado.");

        mainCamera = Camera.main;
        currentDurability = maxDurability;

        if (durabilityUIGroup != null) durabilityUIGroup.SetActive(false);
        originalScale = durabilityUIGroup != null ? durabilityUIGroup.transform.localScale : Vector3.one;
        isInitialized = true;
        UpdateUI();
    }

    private void OnEnable()
    {
        playerControls?.Defense.Enable();
        playerControls?.Movement.Enable();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        playerControls?.Defense.Disable();
        playerControls?.Movement.Disable();
        if (IsBlocking) StopBlocking(false);
        if (playerMovement != null)
        {
            playerMovement.IsRotationExternallyControlled = false;
            playerMovement.UnlockFacing();
        }
    }

    private void Update()
    {
        if (blockingEnabled) UpdateUI();
        if (!blockingEnabled || !IsBlocking) return;

        movementInput = playerControls.Movement.Move.ReadValue<Vector2>();

        if (counterRotationCoroutine == null)
        {
            HandleRotationWhileBlocking();
        }

        if (!isInitialized || durabilitySlider == null) return;

        float targetFill = currentDurability;
        float currentDisplayFill = durabilitySlider.value;

        if (useSmoothing && Mathf.Abs(currentDisplayFill - targetFill) > 0.01f)
        {
            durabilitySlider.value = Mathf.Lerp(currentDisplayFill, targetFill, smoothSpeed * Time.deltaTime);
        }
        else
        {
            durabilitySlider.value = targetFill;
        }

        durabilitySlider.maxValue = maxDurability;

        float fillPercent = currentDurability / maxDurability;
        if (durabilityFillImage != null)
        {
            if (fillPercent <= 0.25f) durabilityFillImage.color = lowDurabilityColor;
            else if (fillPercent <= 0.5f) durabilityFillImage.color = midDurabilityColor;
            else durabilityFillImage.color = fullDurabilityColor;
        }

        if (durabilityPercentageText != null) durabilityPercentageText.text = $"{(fillPercent * 100f):F0}%";

        if (durabilityUIGroup != null)
        {
            if (pulseWhenLow && fillPercent < minimumDurabilityToUse)
            {
                float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI) + 1f) * 0.5f);
                durabilityUIGroup.transform.localScale = originalScale * scale;
            }
            else
            {
                durabilityUIGroup.transform.localScale = originalScale;
            }
        }
    }

    #endregion

    #region Input Handling

    public void OnShieldBlock(InputAction.CallbackContext context)
    {
        if (IsInputBlocked || PauseController.IsGamePaused || !blockingEnabled || isStunned) return;

        if (context.started || context.performed)
        {
            if (!IsBlocking && CanUseBlock())
            {
                StartBlocking();
            }
        }
        else if (context.canceled)
        {
            if (IsBlocking)
            {
                StopBlocking(true);
            }
        }
    }

    #endregion

    #region Blocking System

    private bool CanUseBlock()
    {
        if (currentDurability < (maxDurability * minimumDurabilityToUse))
        {
            if (debugMode) Debug.LogWarning($"[Block] Durabilidad insuficiente: {currentDurability}/{maxDurability}");
            return false;
        }

        if (playerShieldController != null && !playerShieldController.HasShield)
        {
            return false;
        }

        return true;
    }

    private void StartBlocking()
    {
        if (combatActionManager != null) combatActionManager.InterruptCombatActions();

        IsBlocking = true;
        accumulatedDamage = 0f;

        StopCounterRotation();

        playerAnimCtrl?.PlayBlock();

        if (playerMovement != null) playerMovement.SetCanMove(false);

        if (durabilityRechargeCoroutine != null)
        {
            StopCoroutine(durabilityRechargeCoroutine);
            durabilityRechargeCoroutine = null;
            isDurabilityRecharging = false;
        }

        if (durabilityDrainCoroutine != null)
        {
            StopCoroutine(durabilityDrainCoroutine);
        }

        durabilityDrainCoroutine = StartCoroutine(DrainDurability());

        if (durabilityUIGroup != null) durabilityUIGroup.SetActive(true);
        if (blockVFX != null) blockVFX.SetActive(true);
        if (blockParticles != null) blockParticles.Play();
        if (playerAudioController != null) playerAudioController.PlayActiveBlockSound();

        OnBlockStart?.Invoke();
    }

    /// <summary>
    /// Finaliza el bloqueo.
    /// </summary>
    /// <param name="fireProjectile">Si es true, intenta disparar el proyectil de contraataque.</param>
    private void StopBlocking(bool fireProjectile)
    {
        IsBlocking = false;

        playerAnimCtrl?.EndBlock();

        bool willActuallyFire = fireProjectile && accumulatedDamage > 0;

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(true);

            if (!willActuallyFire)
            {
                playerMovement.UnlockFacing();
                playerMovement.IsRotationExternallyControlled = false;
            }
        }

        if (blockVFX != null) blockVFX.SetActive(false);
        if (blockParticles != null) blockParticles.Stop();

        if (willActuallyFire)
        {
            //FireCounterProjectile(isBroken: false);
        }
        else
        {
            if (playerMovement != null) playerMovement.IsRotationExternallyControlled = false;
        }

        if (durabilityRechargeCoroutine == null && currentDurability < maxDurability)
        {
            durabilityRechargeCoroutine = StartCoroutine(RechargeDurability());
        }

        if (durabilityDrainCoroutine != null)
        {
            StopCoroutine(durabilityDrainCoroutine);
            durabilityDrainCoroutine = null;
        }

        if (durabilityUIGroup != null && !isStunned) HideDurabilityBar(hideDelay);

        OnBlockEnd?.Invoke();
    }

    private void BreakBlock()
    {
        if (accumulatedDamage > 0)
        {
            //FireCounterProjectile(isBroken: true);
        }

        StopBlocking(false);

        if (blockBreakVFX != null) Instantiate(blockBreakVFX, transform.position + Vector3.up, Quaternion.identity);
        if (playerAudioController != null) playerAudioController.PlayBlockBreakSound();

        OnBlockBreak?.Invoke();

        // Stun según edad
        ApplyStunBasedOnAge();

        currentDurability = 0f;
        OnDurabilityChanged?.Invoke(0f);
    }

    #endregion

    #region Combat & Damage System

    public bool CanBlockAttack(Vector3 attackerPosition)
    {
        if (!IsBlocking) return false;

        Vector3 toAttacker = attackerPosition - GetShieldPosition();
        toAttacker.y = 0;

        if (toAttacker.sqrMagnitude < 0.1f) return true;

        toAttacker.Normalize();

        Vector3 shieldDir = GetShieldForward();
        float angle = Vector3.Angle(shieldDir, toAttacker);

        return angle <= (frontBlockAngle * 0.5f);
    }

    public float ProcessBlockedAttack(float incomingDamage, GameObject attacker = null)
    {
        if (!IsBlocking) return incomingDamage;

        if (playerAudioController != null) playerAudioController.PlayBlockHitSound();

        currentDurability -= incomingDamage;

        accumulatedDamage += incomingDamage;

        OnDurabilityChanged?.Invoke(GetDurabilityPercentage());

        if (currentDurability <= 0)
        {
            if (currentDurability < 0)
            {
                // El escudo se rompió y hubo exceso de daño
                float leakageDamage = Mathf.Abs(currentDurability); // Convertir el negativo en positivo

                currentDurability = 0; // Resetear visualmente a 0
                BreakBlock();

                // Devolver el daño sobrante para que se lo aplique a la vida
                return leakageDamage;
            }
            else
            {
                // El escudo se rompió exacto, sin daño sobrante
                BreakBlock();
                return 0f;
            }
        }

        return 0f;
    }

    //private void FireCounterProjectile(bool isBroken)
    //{
    //    if (counterProjectilePrefab == null)
    //    {
    //        if (debugMode) Debug.LogError("CounterProjectilePrefab no asignado en PlayerBlockSystem.");
    //        return;
    //    }

    //    if (playerHealth == null) return;

    //    float ageMultiplier = 1f;
    //    switch (playerHealth.CurrentLifeStage)
    //    {
    //        case PlayerHealth.LifeStage.Young: ageMultiplier = multiplierYoung; break; // 150%
    //        case PlayerHealth.LifeStage.Adult: ageMultiplier = multiplierAdult; break; // 200%
    //        case PlayerHealth.LifeStage.Elder: ageMultiplier = multiplierElder; break; // 250%
    //    }

    //    float finalDamage = accumulatedDamage * ageMultiplier;

    //    if (isBroken)
    //    {
    //        finalDamage *= 1f;
    //        //if (debugMode) Debug.Log("Escudo roto: Daño del proyectil reducido al 50%.");
    //    }

    //    Transform target = FindNearestEnemy();
    //    Vector3 fireDirection;
    //    bool hasTarget = false;

    //    if (target != null)
    //    {
    //        fireDirection = (target.position - GetSpawnPosition()).normalized;
    //        fireDirection.y = 0;
    //        if (fireDirection.sqrMagnitude < 0.001f) fireDirection = transform.forward;
    //        else fireDirection.Normalize();

    //        hasTarget = true;
    //    }
    //    else
    //    {
    //        fireDirection = GetShieldForward();
    //        fireDirection.y = 0;
    //        fireDirection.Normalize();
    //    }

    //    if (playerMovement != null)
    //    {
    //        StopCounterRotation();
    //        counterRotationCoroutine = StartCoroutine(HandleCounterRotation(fireDirection, hasTarget));
    //    }

    //    var projObj = Instantiate(counterProjectilePrefab, GetSpawnPosition(), Quaternion.LookRotation(fireDirection));
    //    var projScript = projObj.GetComponent<BlockCounterProjectile>();

    //    if (projScript != null)
    //    {
    //        projScript.Initialize(finalDamage, fireDirection);
    //    }

    //    if (debugMode) Debug.Log($"Contraataque disparado. Daño acumulado: {accumulatedDamage}, Final: {finalDamage}, Objetivo: {(target ? target.name : "Frente")}");

    //    accumulatedDamage = 0f;
    //}

    private Transform FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, autoAimRange, enemyLayer);
        Transform bestTarget = null;
        float closestDistSqr = Mathf.Infinity;

        foreach (var hit in hits)
        {
            if (hit.transform == transform) continue;

            Vector3 diff = hit.transform.position - transform.position;
            float distSqr = diff.sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                bestTarget = hit.transform;
            }
        }
        return bestTarget;
    }

    private Vector3 GetShieldPosition()
    {
        if (shieldForwardOverride != null)
        {
            return shieldForwardOverride.position;
        }
        return transform.position;
    }

    private Vector3 GetShieldForward()
    {
        if (shieldForwardOverride != null)
        {
            return shieldForwardOverride.forward;
        }
        return transform.forward;
    }

    private Vector3 GetSpawnPosition()
    {
        return shieldForwardOverride != null ? shieldForwardOverride.position : transform.position + Vector3.up;
    }

    #endregion

    #region Rotation System

    private void HandleRotationWhileBlocking()
    {
        if (counterRotationCoroutine != null) return;

        if (useMouseRotation) RotateTowardsMouse();
        else RotateWithMovementInput();
    }

    private void RotateTowardsMouse()
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 direction = worldPoint - transform.position;
            direction.y = 0f;
            direction.Normalize();

            if (direction != Vector3.zero)
            {
                playerMovement.LockFacingTo8Directions(direction, true);
            }
        }
    }

    private void RotateWithMovementInput()
    {
        if (movementInput.sqrMagnitude < 0.01f) return;

        Vector3 cameraForward = mainCamera != null ? mainCamera.transform.forward : Vector3.forward;
        Vector3 cameraRight = mainCamera != null ? mainCamera.transform.right : Vector3.right;

        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 direction = (cameraForward * movementInput.y + cameraRight * movementInput.x).normalized;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private IEnumerator HandleCounterRotation(Vector3 targetDirection, bool forceSnap)
    {
        playerMovement.IsRotationExternallyControlled = true;

        if (forceSnap && targetDirection.sqrMagnitude > 0.001f)
        {
            playerMovement.LockFacingTo8Directions(targetDirection, true);
            playerMovement.ForceApplyLockedRotation();
        }

        yield return new WaitForSeconds(counterRotationDuration);

        playerMovement.IsRotationExternallyControlled = false;

        playerMovement.UnlockFacing();

        counterRotationCoroutine = null;
    }

    private void StopCounterRotation()
    {
        if (counterRotationCoroutine != null)
        {
            StopCoroutine(counterRotationCoroutine);
            counterRotationCoroutine = null;
        }

        if (playerMovement != null)
        {
            playerMovement.IsRotationExternallyControlled = false;
            playerMovement.UnlockFacing();
        }
    }

    #endregion

    #region Stun System

    private void ApplyStunBasedOnAge()
    {
        if (playerHealth == null) return;

        float stunDuration = stunDurationAdult;

        switch (playerHealth.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young: stunDuration = stunDurationYoung; break; // 1.5s
            case PlayerHealth.LifeStage.Adult: stunDuration = stunDurationAdult; break; // 1.0s
            case PlayerHealth.LifeStage.Elder: stunDuration = stunDurationElder; break; // 0.5s
        }

        if (stunCoroutine != null) StopCoroutine(stunCoroutine);
        stunCoroutine = StartCoroutine(StunRoutine(stunDuration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        if (playerMovement != null) playerMovement.SetCanMove(false);

        yield return new WaitForSeconds(duration);

        isStunned = false;
        if (playerMovement != null && !IsBlocking) playerMovement.SetCanMove(true);

        if (durabilityRechargeCoroutine == null) durabilityRechargeCoroutine = StartCoroutine(RechargeDurability());

        stunCoroutine = null;
    }

    #endregion

    #region Durability System

    private IEnumerator DrainDurability()
    {
        yield return new WaitForSeconds(drainDelay);

        while (IsBlocking)
        {
            float drainAmount = durabilityDrainRate * Time.deltaTime;

            currentDurability -= drainAmount;

            accumulatedDamage += drainAmount;

            OnDurabilityChanged?.Invoke(GetDurabilityPercentage());

            if (currentDurability <= 0f)
            {
                currentDurability = 0f;
                BreakBlock();
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator RechargeDurability()
    {
        yield return new WaitForSeconds(rechargeDelay);
        isDurabilityRecharging = true;

        float rechargeRate = maxDurability / durabilityRechargeTime;

        while (currentDurability < maxDurability)
        {
            currentDurability += rechargeRate * Time.deltaTime;
            currentDurability = Mathf.Min(currentDurability, maxDurability);
            OnDurabilityChanged?.Invoke(GetDurabilityPercentage());
            yield return null;
        }

        isDurabilityRecharging = false;
        durabilityRechargeCoroutine = null;

        if (durabilityUIGroup != null)
        {
            durabilityUIGroup.SetActive(true);
            UpdateUI();
            HideDurabilityBar(hideDelay);
        }
    }

    #endregion

    #region Visual & UI Updates

    public void UpdateUI()
    {
        if (durabilitySlider == null) return;

        float fillPercent = currentDurability / maxDurability;
        durabilitySlider.value = currentDurability;
        durabilitySlider.maxValue = maxDurability;

        if (durabilityFillImage != null)
        {
            if (fillPercent <= 0.25f) durabilityFillImage.color = lowDurabilityColor;
            else if (fillPercent <= 0.5f) durabilityFillImage.color = midDurabilityColor;
            else durabilityFillImage.color = fullDurabilityColor;
        }

        if (durabilityPercentageText != null) durabilityPercentageText.text = $"{(fillPercent * 100f):F0}%";
    }

    private void HideDurabilityBar(float delay)
    {
        if (hideDurabilityCoroutine != null) StopCoroutine(hideDurabilityCoroutine);
        hideDurabilityCoroutine = StartCoroutine(HideRoutine(delay));
    }

    private IEnumerator HideRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (durabilityUIGroup != null && !IsBlocking) durabilityUIGroup.SetActive(false);
        hideDurabilityCoroutine = null;
    }

    #endregion

    #region Public API

    public void SetBlockingEnabled(bool enabled)
    {
        blockingEnabled = enabled;
        if (!enabled && IsBlocking) StopBlocking(false);
    }

    public bool IsRecharging() => isDurabilityRecharging;
    public bool IsBlockingState() => IsBlocking;
    public bool IsStunned() => isStunned;
    public void SetInputBlocked(bool isBlocked) => IsInputBlocked = isBlocked;
    public float GetDurabilityPercentage() => (currentDurability / maxDurability) * 100f;
    public float GetCurrentDurability() => currentDurability;
    public float GetMaxDurability() => maxDurability;
    
    #endregion

    #region Debugging

    private void OnDrawGizmos()
    {
        if (!debugMode) return;

        // Dibujar rango de auto aim
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, autoAimRange);

        // Dibujar ángulo de bloqueo
        Gizmos.color = IsBlocking ? Color.green : Color.yellow;

        // Dirección frontal del escudo
        Vector3 forward = GetShieldForward();
        if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
        forward.Normalize();

        Vector3 origin = GetShieldPosition();
        float radius = 2f;
        float halfAngle = frontBlockAngle * 0.5f;

        // Líneas del cono
        Vector3 leftDir = Quaternion.Euler(0, -halfAngle, 0) * forward;
        Vector3 rightDir = Quaternion.Euler(0, halfAngle, 0) * forward;

        Gizmos.DrawLine(origin, origin + forward * radius);
        Gizmos.DrawLine(origin, origin + leftDir * radius);
        Gizmos.DrawLine(origin, origin + rightDir * radius);

        if (counterRotationCoroutine != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 3f);
        }

        // Dibujar arco
        int segments = 15;
        Vector3 prevPoint = origin + leftDir * radius;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            // Interpolar angularmente entre el límite izquierdo y derecho
            Vector3 currentDir = Vector3.Slerp(leftDir, rightDir, t);
            Vector3 nextPoint = origin + currentDir * radius;

            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        // Color según estado
        if (isStunned)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
        }
        else if (currentDurability < maxDurability * minimumDurabilityToUse)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[PlayerBlockSystem] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerBlockSystem] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerBlockSystem] {message}");
                break;
        }
    }

    #endregion

    #region Legacy & Commented Code

    //[Tooltip("Velocidad de interpolación para volver a la rotación original.")]
    //[SerializeField] private float counterRotationSmoothReturn = 15f;

    #endregion
}