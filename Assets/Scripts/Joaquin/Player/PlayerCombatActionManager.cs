using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestor centralizado de acciones de combate del jugador.
/// Permite encolar una accion pendiente mientras otra esta en ejecucion.
/// </summary>
public class PlayerCombatActionManager : MonoBehaviour, PlayerControlls.ICombatActions
{
    #region Enums

    public enum CombatActionType
    {
        None,
        MeleeAttack,
        ShieldThrow,
        Dash
    }

    #endregion

    #region Inspector - References

    [Header("Referencias")]
    [SerializeField] private PlayerMeleeAttack meleeAttack;
    [SerializeField] private PlayerShieldController shieldController;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHealth playerHealth;

    #endregion

    #region Inspector - Feedback Visual
    /*
    [Header("Feedback Visual")]
    [SerializeField] private bool enableVisualFeedback = true;
    [SerializeField] private GameObject skillRequiredWarningPrefab;
    [SerializeField] private float warningOffsetY = 2.0f;
    [SerializeField] private float warningDuration = 1.5f;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private string meleeBlockedMessage = "Activa la habilidad especial!";
    [SerializeField] private string rangedBlockedMessage = "Desactiva la habilidad especial!";
    */
    #endregion

    #region Inspector - Settings

    // [SerializeField] private float inputBufferWindow = 0.12f; // Ventana de tiempo para bufferizar inputs

    #endregion

    #region Internal State

    private PlayerControlls playerControls;
    private bool isMeleeAttackBlocked = false;
    private bool isShieldThrowBlocked = false;
    private bool isDashBlocked = false;
    private bool isExecutingAction = false;
    private CombatActionType currentAction = CombatActionType.None;
    private CombatActionType queuedAction = CombatActionType.None;
    // private float queuedActionTimestamp = -Mathf.Infinity;
    private GameObject currentWarning;

    private bool wasSteamMeleePressed;
    private bool wasSteamShieldThrowPressed;
    private bool wasSteamDashPressed;

    #endregion

    #region Public Properties & Events

    public bool IsExecutingAction => isExecutingAction;
    public CombatActionType CurrentAction => currentAction;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerControls = new PlayerControlls();
        playerControls.Combat.SetCallbacks(this);

        meleeAttack = GetComponent<PlayerMeleeAttack>();
        shieldController = GetComponent<PlayerShieldController>();
        playerMovement = GetComponent<PlayerMovement>();

        if (meleeAttack == null) ReportDebug("PlayerMeleeAttack no encontrado.", 3);
        if (shieldController == null) ReportDebug("PlayerShieldController no encontrado.", 3);
        if (playerMovement == null) ReportDebug("PlayerMovement no encontrado.", 3);
    }

    private void Update()
    {
        if (SteamInputManager.Instance == null) return;

        ReadSteamCombatInput();
    }

    private void ReadSteamCombatInput()
    {
        if (SteamInputManager.Instance == null) return;

        if (PauseController.Instance != null && PauseController.IsGamePaused) return;
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;

        bool meleePressed = SteamInputManager.Instance.GetMeleeAttackPressed();
        bool shieldThrowPressed = SteamInputManager.Instance.GetShieldThrowPressed();
        bool dashPressed = SteamInputManager.Instance.GetDashPressed();

        if (meleePressed && !wasSteamMeleePressed && !isMeleeAttackBlocked)
        {
            ProcessCombatInput(CombatActionType.MeleeAttack);
        }

        if (shieldThrowPressed && !wasSteamShieldThrowPressed && !isShieldThrowBlocked)
        {
            ProcessCombatInput(CombatActionType.ShieldThrow);
        }

        if (dashPressed && !wasSteamDashPressed && !isDashBlocked)
        {
            ProcessCombatInput(CombatActionType.Dash);
        }

        wasSteamMeleePressed = meleePressed;
        wasSteamShieldThrowPressed = shieldThrowPressed;
        wasSteamDashPressed = dashPressed;
    }

    private void OnEnable()
    {
        playerControls.Combat.Enable();
    }

    private void OnDisable()
    {
        playerControls.Combat.Disable();
    }

    private void OnDestroy()
    {
        Destroy(currentWarning);
    }

    #endregion

    #region Input Handlers

    public void OnMelee(InputAction.CallbackContext context)
    {
        if (isMeleeAttackBlocked) return;
        if (PauseController.Instance != null && PauseController.IsGamePaused) return;
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;
        if (!context.started) return;

        ProcessCombatInput(CombatActionType.MeleeAttack);
    }

    public void OnShieldThrow(InputAction.CallbackContext context)
    {
        if (isShieldThrowBlocked) return;

        if (PauseController.Instance != null && PauseController.IsGamePaused) return;
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;

        if (!context.started) return;

        ProcessCombatInput(CombatActionType.ShieldThrow);
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (isDashBlocked) return;

        if (PauseController.Instance != null && PauseController.IsGamePaused) return;
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;

        if (!context.started) return;

        ProcessCombatInput(CombatActionType.Dash);
    }

    #endregion

    #region Combat Processing

    private void ProcessCombatInput(CombatActionType actionType)
    {
        if (actionType == CombatActionType.Dash)
        {
            if (currentAction == CombatActionType.Dash) return;

            if (isExecutingAction) InterruptCombatActions();

            TryExecuteDash();
            return;
        }

        if (isExecutingAction)
        {
            TryQueueAction(actionType);
        }
        else
        {
            ExecuteActionImmediately(actionType);
        }
    }

    /// <summary>
    /// Intenta encolar una accion si ya hay una en ejecucion.
    /// Solo guarda la primera accion solicitada durante la ejecucion actual.
    /// </summary>
    private void TryQueueAction(CombatActionType actionType)
    {
        if (queuedAction != CombatActionType.None) return;

        bool canQueue = false;
        switch (actionType)
        {
            case CombatActionType.MeleeAttack:
                canQueue = CanQueueMeleeAttack();
                break;
            case CombatActionType.ShieldThrow:
                canQueue = CanQueueShieldThrow();
                break;
            case CombatActionType.Dash:
                canQueue = CanQueueDash();
                break;
        }

        if (canQueue) TryBufferAction(actionType);
    }

    // Funcion para encolar con buffer y prioridad de sobrescritura opcional
    private bool TryBufferAction(CombatActionType action)
    {
        queuedAction = action;
        ReportDebug($"Accion {action} bufferizada", 1);
        return true;
    }

    private void ExecuteActionImmediately(CombatActionType actionType)
    {
        switch (actionType)
        {
            case CombatActionType.MeleeAttack:
                TryExecuteMeleeAttack();
                break;
            case CombatActionType.ShieldThrow:
                TryExecuteShieldThrow();
                break;
            case CombatActionType.Dash:
                TryExecuteDash();
                break;
            case CombatActionType.None:
            default:
                break;
        }
    }

    #endregion

    #region Execution Routines

    private void TryExecuteMeleeAttack()
    {
        if (playerHealth != null)
        {
            if (playerHealth.IsStunned())
            {
                ReportDebug("Accion bloqueada: Jugador aturdido.", 2);
                return;
            }

            if (playerHealth.IsDead())
            {
                ReportDebug("Accion bloqueada: Jugador muerto.", 2);
                return;
            }
        }

        if (meleeAttack != null && meleeAttack.CanAttack())
        {
            StartCoroutine(ExecuteActionRoutine(CombatActionType.MeleeAttack, meleeAttack.ExecuteAttackFromManager()));
        }
    }

    private void TryExecuteShieldThrow()
    {
        if (playerHealth != null)
        {
            if (playerHealth.IsStunned())
            {
                ReportDebug("Accion bloqueada: Jugador aturdido.", 2);
                return;
            }

            if (playerHealth.IsDead())
            {
                ReportDebug("Accion bloqueada: Jugador muerto.", 2);
                return;
            }
        }

        if (shieldController != null && shieldController.HasShield)
        {
            if (CanQueueShieldThrow())
            {
                StartCoroutine(ExecuteActionRoutine(CombatActionType.ShieldThrow, shieldController.ExecuteShieldThrowFromManager()));
            }
        }
    }

    private void TryExecuteDash()
    {
        if (playerHealth != null)
        {
            if (playerHealth.IsStunned())
            {
                ReportDebug("Accion bloqueada: Jugador aturdido.", 2);
                return;
            }

            if (playerHealth.IsDead())
            {
                ReportDebug("Accion bloqueada: Jugador muerto.", 2);
                return;
            }
        }

        if (CanQueueDash())
        {
            StartCoroutine(ExecuteActionRoutine(CombatActionType.Dash, playerMovement.ExecuteDashFromManager()));
        }
    }

    private IEnumerator ExecuteActionRoutine(CombatActionType actionType, IEnumerator actionCoroutine)
    {
        isExecutingAction = true;
        currentAction = actionType;
        yield return StartCoroutine(actionCoroutine);
        FinishAction();
    }

    #endregion

    #region Conditionals

    /// <summary>
    /// Interrumpe cualquier accion en curso y limpia la cola de inputs.
    /// </summary>
    public void InterruptCombatActions()
    {
        // Limpiar la cola inmediatamente para evitar que se dispare nada despues
        queuedAction = CombatActionType.None;

        if (!isExecutingAction) return;

        StopAllCoroutines();

        if (currentAction == CombatActionType.MeleeAttack && meleeAttack != null)
        {
            meleeAttack.CancelAttack();
        }
        else if (currentAction == CombatActionType.ShieldThrow && shieldController != null)
        {
            shieldController.CancelThrow();
        }
        else if (currentAction == CombatActionType.Dash && playerMovement != null)
        {
            playerMovement.CancelDash();
        }

        if (playerMovement != null) playerMovement.SetCanMove(true);

        isExecutingAction = false;
        currentAction = CombatActionType.None;

        ReportDebug("Combate interrumpido por bloqueo/accion defensiva.", 1);
    }

    private bool CanQueueMeleeAttack()
    {
        if (meleeAttack == null) return false;

        if (currentAction == CombatActionType.MeleeAttack && meleeAttack.IsOnLastComboAttack)
        {
            ReportDebug("No se puede bufferizar melee: tercer ataque del combo en ejecución.", 1);
            return false;
        }

        if (shieldController != null) return shieldController.HasShield;
        return true;
    }

    private bool CanQueueShieldThrow()
    {
        return shieldController != null && shieldController.CanThrowShield();
    }

    private bool CanQueueDash()
    {
        return playerMovement != null && !playerMovement.IsDashing && !playerMovement.IsDashDisabled && playerMovement.DashCooldownTimer <= 0;
    }

    #endregion

    #region Action Finishers

    private void FinishAction()
    {
        isExecutingAction = false;
        currentAction = CombatActionType.None;

        if (queuedAction != CombatActionType.None)
        {
            CombatActionType actionToExecute = queuedAction;
            queuedAction = CombatActionType.None;

            if (actionToExecute != CombatActionType.MeleeAttack && meleeAttack != null)
            {
                meleeAttack.ResetCombo();
                ReportDebug("Combo de melee reiniciado: acción no-melee encolada interrumpió la cadena.", 1);
            }

            ReportDebug($"Ejecutando acción encolada: {actionToExecute}", 1);
            StartCoroutine(ExecuteQueuedAction(actionToExecute));
        }
    }

    private IEnumerator ExecuteQueuedAction(CombatActionType action)
    {
        yield return null;
        switch (action)
        {
            case CombatActionType.MeleeAttack:
                TryExecuteMeleeAttack();
                break;
            case CombatActionType.ShieldThrow:
                TryExecuteShieldThrow();
                break;
            case CombatActionType.Dash:
                TryExecuteDash();
                break;
        }
    }

    #endregion

    #region Public Methods

    public void BlockMeleeAttack()
    {
        isMeleeAttackBlocked = true;
    }

    public void UnblockMeleeAttack()
    {
        isMeleeAttackBlocked = false;
    }

    public void BlockShieldThrow()
    {
        isShieldThrowBlocked = true;
    }

    public void UnblockShieldThrow()
    {
        isShieldThrowBlocked = false;
    }

    public void BlockDash()
    {
        isDashBlocked = true;
    }

    public void UnblockDash()
    {
        isDashBlocked = false;
    }

    #endregion

    #region Visual Feedback

    /*
    private void ShowSkillRequiredWarning(string message)
    {
        if (!enableVisualFeedback) return;
        if (skillRequiredWarningPrefab == null) return;

        // Destruir advertencia anterior si existe
        if (currentWarning != null)
        {
            Destroy(currentWarning);
        }

        // Crear nueva advertencia
        Vector3 spawnPosition = transform.position + Vector3.up * warningOffsetY;
        currentWarning = Instantiate(skillRequiredWarningPrefab, spawnPosition, Quaternion.identity);

        // Configurar mensaje
        WarningMessageFloater floater = currentWarning.GetComponent<WarningMessageFloater>();
        if (floater != null)
        {
            floater.SetLifetime(warningDuration);
            floater.SetColor(warningColor);
            floater.SetText(message);
        }

        // Destruir despues de duracion
        StartCoroutine(DestroyWarningAfterDelay());
    }

    private IEnumerator DestroyWarningAfterDelay()
    {
        yield return new WaitForSeconds(warningDuration);

        if (currentWarning != null)
        {
            Destroy(currentWarning);
            currentWarning = null;
        }
    }
    */

    #endregion

    #region Logging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[PlayerCombatActionManager] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerCombatActionManager] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerCombatActionManager] {message}");
                break;
            default:
                Debug.Log($"[PlayerCombatActionManager] {message}");
                break;
        }
    }

    #endregion
}