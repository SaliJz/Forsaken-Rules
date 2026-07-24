using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour, PlayerControlls.IMovementActions
{
    #region Inspector – References

    [Header("References")]
    [Tooltip("Controlador principal de las estadisticas (salud, velocidad, etc).")]
    [SerializeField] private PlayerStatsManager playerStatsManager;
    [Tooltip("Componente fisico que mueve y detecta colisiones del jugador.")]
    [SerializeField] private CharacterController controller;
    [Tooltip("La camara principal que sigue al jugador.")]
    [SerializeField] private Transform mainCameraTransform;
    [Tooltip("Controlador de las animaciones del jugador.")]
    [SerializeField] private PlayerAnimCtrl playerAnimCtrl;
    [Tooltip("Controlador de los VFX del jugador")]
    [SerializeField] private PlayerVfxCtrl playerVfxCtrl;
    [Tooltip("Maneja la vida y danio del jugador.")]
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Controlador para los sonidos del jugador (pasos, dash, etc).")]
    [SerializeField] private PlayerAudioController playerAudioController;
    [Tooltip("Permite modificar o invertir los controles de entrada si es necesario.")]
    [SerializeField] private PlayerInputModifier inputModifier;

    #endregion

    #region Inspector – Movement Settings

    [Header("Movement")]
    [HideInInspector] private float fallbackMoveSpeed = 5f;
    [Tooltip("Que tan rapido camina o corre el jugador.")]
    [SerializeField] private float moveSpeed = 5f;
    [HideInInspector] private float fallbackGravity = -9.81f;
    [Tooltip("Fuerza con la que el jugador es atraido hacia el suelo.")]
    [SerializeField] private float gravity = -9.81f;

    #endregion

    #region Inspector – Audio Step Settings

    [Header("Audio Step Settings")]
    [Tooltip("Tipo de superficie actual para cambiar el sonido de los pasos.")]
    [SerializeField] private int level = 0;
    [Tooltip("Tiempo de espera entre cada sonido de paso (mas bajo = pasos mas rapidos).")]
    [SerializeField] private float stepInterval = 0.35f;

    #endregion

    #region Inspector – Dash Settings

    [Header("Dash")]
    [Tooltip("Distancia normal del impulso (dash).")]
    [SerializeField] private float baseDashDistance = 10f;
    [Tooltip("Tiempo de espera para poder usar el dash de nuevo.")]
    [SerializeField] private float baseDashCooldown = 0.3f;
    //[Tooltip("Distancia final del dash despues de aplicar mejoras u objetos.")]
    //[SerializeField] private float dashDistance = 10f;
    //[Tooltip("Cooldown actual del dash despues de aplicar modificadores.")]
    //[SerializeField] private float dashCooldown = 0.3f;
    [Tooltip("Cuanto tiempo dura la animacion y el movimiento del dash.")]
    [SerializeField] private float dashDuration = 0.3f;
    [Tooltip("Capas del mapa por las que el jugador puede caminar.")]
    [SerializeField] private LayerMask traversableLayers;
    [Tooltip("Capas del mapa que detendran el dash (paredes, obstaculos, etc).")]
    [SerializeField] private LayerMask dashCollisionLayers;

    [Header("Dash - Safety")]
    [Tooltip("Distancia minima util para permitir un dash recortado.")]
    [SerializeField] private float minEffectiveDashDistance = 0.75f;
    [Tooltip("Paso de muestreo horizontal para validar suelo durante el dash.")]
    [SerializeField] private float dashGroundSampleStep = 0.2f;
    [Tooltip("Altura desde donde se comprueba el suelo durante el dash.")]
    [SerializeField] private float dashGroundProbeHeight = 1.5f;
    //[Tooltip("Distancia maxima del raycast hacia abajo durante el dash.")]
    //[SerializeField] private float dashGroundProbeDistance = 3f;
    [Tooltip("Tolerancia extra para considerar grounded en el arranque del dash.")]
    [SerializeField] private float dashGroundedGrace = 0.12f;
    [Tooltip("stepOffset reducido durante el dash para evitar trepar colliders grandes de enemigos.")]
    [SerializeField] private float dashStepOffsetOverride = 0.02f;
    [Tooltip("Separacion minima respecto a obstaculos solidos al recortar la distancia.")]
    [SerializeField] private float dashObstacleSkin = 0.08f;

    [Header("Dash - Gap Crossing")]
    [Tooltip("Distancia extra que se perdona si el dash termina justo en el borde de un abismo.")]
    [SerializeField] private float gapDashBonusDistance = 3f;
    [Tooltip("Capas consideradas como suelo o piso firme.")]
    [SerializeField] private LayerMask groundLayerMask;

    #endregion

    #region Inspector – Edge Detection

    [Header("Edge Detection")]
    [Tooltip("Evita que el jugador se caiga por los bordes de las plataformas accidentalmente.")]
    [SerializeField] private bool enableEdgeDetection = true;
    [Tooltip("Distancia de anticipacion para detectar si hay un borde cerca.")]
    [SerializeField] private float edgeDetectionDistance = 0.5f;
    [Tooltip("Altura desde donde se lanzan los rayos invisibles para detectar el suelo.")]
    [SerializeField] private float edgeRaycastHeight = 0.2f;
    [Tooltip("Margen de seguridad extra para evitar quedarse colgando del borde.")]
    [SerializeField] private float edgeSafetyMargin = 0.05f;
    [Tooltip("Cantidad de verificaciones que hace el sistema para encontrar un borde.")]
    [SerializeField] private int edgeSampleMax = 6;
    [Tooltip("Paso fijo para muestrear bordes en GetMaxSafeDistance (mas preciso que solo edgeSampleMax).")]
    [SerializeField] private float edgeSampleStepDistance = 0.2f;
    [Tooltip("Movimiento minimo necesario para activar la deteccion de bordes.")]
    [SerializeField] private float minHorizontalMagnitude = 0.01f;
    [Tooltip("Distancia de tolerancia para considerar que el jugador sigue tocando el suelo.")]
    [SerializeField] private float groundTolerance = 0.15f;

    #endregion

    #region Inspector – Effects

    [Header("Effects")]
    [Header("Afterimage Settings")]
    [Tooltip("El objeto visual que hace el efecto de fantasma durante el dash.")]
    [SerializeField] private GameObject afterimagePrefab;
    [Tooltip("Que tan transparente es el efecto de fantasma (0 es invisible, 1 es solido).")]
    [SerializeField, Range(0f, 1f)] private float afterimageAlpha = 0.5f;
    [Tooltip("Cuanto tiempo dura el efecto de fantasma antes de desaparecer.")]
    [SerializeField] private float afterimageLifetime = 0.5f;

    [Header("Dash VFX")]
    [Tooltip("Efecto de particulas (polvo) que sale al hacer el dash.")]
    [SerializeField] private ParticleSystem dashDustVFX;

    #endregion

    #region Inspector – Debug

    [Header("Debug")]
    [Tooltip("Muestra lineas de colores en la escena para ver como funcionan los rayos y colisiones.")]
    [SerializeField] private bool canDebug = true;

    #endregion

    #region Internal State

    // Input & Controllers
    private PlayerControlls playerControls;
    private Vector2 currentInputVector = Vector2.zero;
    private int playerLayer;

    // Movement Physics
    private Vector3 moveDirection;
    private float yVelocity;
    private float lastMoveX;
    private float lastMoveY;
    private float prevStepOffset = 0f;

    // Flags & Modifiers
    private bool canMove = true;
    private bool allowExternalForces = true;
    private bool inForcedMove = false;
    private bool ignoreGravityDuringForcedMove = false;
    private bool rotationLocked = false;
    private Quaternion lockedRotation = Quaternion.identity;

    // Dash State
    private float dashCooldownTimer = 0f;
    private float currentDashDistance;
    private float currentDashCooldown;
    private bool isDashDisabled = false;
    private Coroutine _dashDisableCoroutine;
    private readonly Vector3[] safeCheckDirections = new Vector3[9];

    // VFX & Audio State
    private Material dashVFXMaterialInstance;
    private float stepTimer = 0f;

    // Debugging State
    private Vector3 lastTargetCheck = Vector3.zero;
    private bool lastTargetHit = false;

    #endregion

    #region Public Properties

    public float LastCalculatedDashDistance { get; private set; }
    public int Level
    {
        get { return level; }
        set { level = value; }
    }

    public bool IsDashing { get; private set; }
    public float DashCooldownTimer => dashCooldownTimer;
    public float MoveSpeed
    {
        get { return moveSpeed; }
        set { moveSpeed = value; }
    }

    public bool IsDashDisabled
    {
        get { return isDashDisabled; }
        set { isDashDisabled = value; }
    }

    public bool IsRotationExternallyControlled { get; set; } = false;

    public static event System.Action OnDashPerformed;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerControls = new PlayerControlls();
        playerControls.Movement.SetCallbacks(this);

        controller = GetComponent<CharacterController>();
        playerStatsManager = GetComponent<PlayerStatsManager>();
        playerAnimCtrl = GetComponentInChildren<PlayerAnimCtrl>();
        playerVfxCtrl = GetComponentInChildren<PlayerVfxCtrl>();
        playerHealth = GetComponent<PlayerHealth>();
        playerAudioController = GetComponent<PlayerAudioController>();

        mainCameraTransform = Camera.main != null ? Camera.main.transform : mainCameraTransform;

        playerLayer = LayerMask.NameToLayer("Player");
    }

    private void Start()
    {
        safeCheckDirections[0] = Vector3.zero;
        safeCheckDirections[1] = Vector3.forward;
        safeCheckDirections[2] = Vector3.back;
        safeCheckDirections[3] = Vector3.right;
        safeCheckDirections[4] = Vector3.left;
        safeCheckDirections[5] = Vector3.forward + Vector3.right;
        safeCheckDirections[6] = Vector3.forward + Vector3.left;
        safeCheckDirections[7] = Vector3.back + Vector3.right;
        safeCheckDirections[8] = Vector3.back + Vector3.left;

        float moveSpeedStat = playerStatsManager != null ? playerStatsManager.GetStat(StatType.MoveSpeed) : fallbackMoveSpeed;
        moveSpeed = moveSpeedStat;

        float gravityStat = playerStatsManager != null ? playerStatsManager.GetStat(StatType.Gravity) : fallbackGravity;
        gravity = gravityStat;

        lastMoveY = -1;
        lastMoveX = 0;

        InitializeDashVFX();
        UpdateDashStatsFromManager();
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
        playerControls?.Movement.Enable();
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
        playerControls?.Movement.Disable();
    }

    private void Update()
    {
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (canMove && !IsDashing)
        {
            HandleMovementInput();
        }
        else
        {
            moveDirection = Vector3.zero;
            playerAnimCtrl?.SetInputAxes(lastMoveX, lastMoveY);
        }

        ApplyGravity();
    }

    private void FixedUpdate()
    {
        if (IsDashing) return;

        if (controller.enabled)
        {
            Vector3 finalMove = moveDirection * moveSpeed;
            finalMove.y = yVelocity;

            if (enableEdgeDetection && moveDirection.magnitude > 0.1f)
            {
                finalMove = ApplyEdgeDetection(finalMove);
            }

            controller.Move(finalMove * Time.fixedDeltaTime);

            if (IsRotationExternallyControlled)
            {
                return;
            }

            RotateTowardsMovement();
        }
    }

    private void OnDestroy()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;

        if (dashDustVFX != null)
        {
            dashDustVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            dashDustVFX.Clear(true);
        }

        if (dashVFXMaterialInstance != null)
        {
            Destroy(dashVFXMaterialInstance);
            dashVFXMaterialInstance = null;
        }

        StopAllCoroutines();
    }

    #endregion

    #region Initialization & Event Handlers

    public void ResetMovementState()
    {
        moveDirection = Vector3.zero;

        lastMoveX = 0f;
        lastMoveY = 0f;

        UnlockFacing();

        if (playerAnimCtrl != null)
        {
            playerAnimCtrl.SetInputAxes(0f, 0f);
        }
    }

    /// <summary>
    /// Recalcula la distancia y el cooldown del dash tomando los valores del gestor de estadísticas.
    /// </summary>
    private void UpdateDashStatsFromManager()
    {
        if (playerStatsManager == null)
        {
            currentDashDistance = baseDashDistance;
            currentDashCooldown = baseDashCooldown;
            return;
        }

        float dashRangeMultiplier = playerStatsManager.GetStat(StatType.DashRangeMultiplier);
        if (dashRangeMultiplier <= 0f) dashRangeMultiplier = 1f;

        float dashRangeFlatBonus = playerStatsManager.GetStat(StatType.DashRangeFlatBonus);

        currentDashDistance = Mathf.Max(0.1f, (baseDashDistance * dashRangeMultiplier) + dashRangeFlatBonus);

        float dashCooldownMod = playerStatsManager.GetStat(StatType.DashCooldownPost);
        currentDashCooldown = Mathf.Max(0.1f, baseDashCooldown + dashCooldownMod);

        ReportDebug($"Dash Stats actualizados: Distancia={currentDashDistance} (base:{baseDashDistance} x {dashRangeMultiplier} + {dashRangeFlatBonus}), Cooldown={currentDashCooldown} (base:{baseDashCooldown} + {dashCooldownMod}s)", 1);
    }

    /// <summary>
    /// Evento que reacciona cuando una estadística del jugador cambia para actualizarla en el movimiento.
    /// </summary>
    /// <param name="statType">El tipo de estadística modificada.</param>
    /// <param name="newValue">El nuevo valor a aplicar.</param>
    private void HandleStatChanged(StatType statType, float newValue)
    {
        if (statType == StatType.MoveSpeed)
        {
            moveSpeed = newValue;
        }
        else if (statType == StatType.Gravity)
        {
            gravity = newValue;
        }
        else if (statType == StatType.DashRangeMultiplier || statType == StatType.DashCooldownPost || statType == StatType.DashRangeFlatBonus)
        {
            UpdateDashStatsFromManager();
        }

        ReportDebug($"Stat {statType} cambiado a {newValue}.", 1);
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// Lee el input direccional del nuevo sistema de inputs.
    /// </summary>
    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 rawInput = context.ReadValue<Vector2>();

        if (inputModifier != null)
        {
            currentInputVector = inputModifier.ProcessInput(rawInput);
        }
        else
        {
            currentInputVector = rawInput;
        }
    }

    private static (float h, float v) SnapTo8Dir(float x, float y)
    {
        if (x == 0 && y == 0) return (0, 0);

        float angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / 45f) * 45f;

        float h = Mathf.Round(Mathf.Cos(snapped * Mathf.Deg2Rad));
        float v = Mathf.Round(Mathf.Sin(snapped * Mathf.Deg2Rad));
        return (h, v);
    }

    /// <summary>
    /// Procesa el input leído, orientándolo respecto a la cámara principal y gestionando sonidos de paso y animaciones.
    /// </summary>
    private void HandleMovementInput()
    {
        if (PauseController.Instance != null && PauseController.IsGamePaused) return;
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;

        Vector2 inputVector = currentInputVector;

        if (SteamInputManager.Instance != null)
        {
            Vector2 steamMove = SteamInputManager.Instance.GetMoveAxis();

            if (steamMove.sqrMagnitude > 0.0001f)
            {
                inputVector = inputModifier != null
                    ? inputModifier.ProcessInput(steamMove)
                    : steamMove;
            }
        }

        float moveX = inputVector.x;
        float moveY = inputVector.y;

        Vector3 cameraForward = mainCameraTransform != null ? mainCameraTransform.forward : Vector3.forward;
        Vector3 cameraRight = mainCameraTransform != null ? mainCameraTransform.right : Vector3.right;

        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        moveDirection = (cameraForward * moveY + cameraRight * moveX).normalized;

        bool hasInput = moveDirection.magnitude > 0.1f;
        bool timeIsRunning = Time.timeScale > 0.01f;
        bool isMoving = hasInput && timeIsRunning;

        if (playerAudioController != null)
        {
            if (isMoving && controller.isGrounded)
            {
                HandleFootstepsTimer();
            }
            else
            {
                playerAudioController.StopFootsteps();
                stepTimer = 0f;
            }
        }

        if (timeIsRunning)
        {
            if (!rotationLocked)
            {
                if (hasInput)
                {
                    var (h, v) = SnapTo8Dir(moveX, moveY);
                    lastMoveX = h;
                    lastMoveY = v;
                    playerAnimCtrl?.SetInputAxes(lastMoveX, lastMoveY);
                }
                else
                {
                    playerAnimCtrl?.SetInputAxes(0f, 0f);
                }
            }
        }
    }

    #endregion

    #region Core Movement Physics

    /// <summary>
    /// Aplica la fuerza de gravedad al jugador.
    /// </summary>
    private void ApplyGravity()
    {
        if (IsDashing) return;

        if (inForcedMove && ignoreGravityDuringForcedMove)
        {
            yVelocity = -0.5f;
            return;
        }

        if (controller.enabled && controller.isGrounded)
        {
            yVelocity = -0.5f;
        }
        else if (controller.enabled)
        {
            yVelocity += gravity * Time.deltaTime;
        }
    }

    /// <summary>
    /// Mueve forzosamente al personaje ignorando su input, pasando por el sistema de detección de bordes si es necesario.
    /// </summary>
    /// <param name="displacement">El vector de desplazamiento aplicado.</param>
    public void MoveCharacter(Vector3 displacement)
    {
        if (controller != null && controller.enabled)
        {
            if (enableEdgeDetection && controller.isGrounded)
            {
                displacement = ApplyEdgeDetectionToDisplacement(displacement);
            }

            controller.Move(displacement);
        }
    }

    /// <summary>
    /// Devuelve la direccion de input cruda convertida a espacio de mundo relativo a camara,
    /// sin pasar por canMove ni rotationLocked.
    /// </summary>
    public Vector3 GetRawInputWorldDirection()
    {
        Vector2 inputVector = currentInputVector;

        if (SteamInputManager.Instance != null)
        {
            Vector2 steamMove = SteamInputManager.Instance.GetMoveAxis();
            if (steamMove.sqrMagnitude > 0.0001f) inputVector = steamMove;
        }

        Vector3 cameraForward = mainCameraTransform != null ? mainCameraTransform.forward : Vector3.forward;
        Vector3 cameraRight = mainCameraTransform != null ? mainCameraTransform.right : Vector3.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        return cameraForward * inputVector.y + cameraRight * inputVector.x;
    }

    /// <summary>
    /// Bloquea o desbloquea la capacidad del jugador para moverse.
    /// </summary>
    public void SetCanMove(bool state)
    {
        canMove = state;

        if (!state)
        {
            moveDirection = Vector3.zero;
            playerAnimCtrl?.SetInputAxes(0f, 0f);
        }
    }

    /// <summary>
    /// Inicia un movimiento controlado por un sistema externo (ej. cinemáticas o empujes).
    /// </summary>
    public void StartForcedMovement(bool ignoreGravity)
    {
        inForcedMove = true;
        ignoreGravityDuringForcedMove = ignoreGravity;
        allowExternalForces = false;
        if (ignoreGravity)
        {
            yVelocity = -0.5f;
        }
    }

    /// <summary>
    /// Detiene el movimiento externo forzado.
    /// </summary>
    public void StopForcedMovement()
    {
        inForcedMove = false;
        ignoreGravityDuringForcedMove = false;
        allowExternalForces = true;
    }

    public void SetExternalForcesAllowed(bool allowed) { allowExternalForces = allowed; }

    /// <summary>
    /// Teletransporta al jugador de forma segura desactivando y reactivando su colisionador.
    /// </summary>
    public void TeleportTo(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;

        yVelocity = -0.5f;
        moveDirection = Vector3.zero;
    }

    /// <summary>
    /// Revisa si el jugador está en el suelo de manera estricta o mediante un rayo pequeño debajo de él.
    /// </summary>
    public bool IsEffectivelyGrounded()
    {
        if (controller == null) return false;
        if (controller.isGrounded) return true;

        Vector3 origin = transform.position + Vector3.up * 0.1f;
        float maxDistance = groundTolerance + dashGroundedGrace;

        return Physics.Raycast(origin, Vector3.down, out _, maxDistance, groundLayerMask, QueryTriggerInteraction.Ignore);
    }

    #endregion

    #region Facing & Rotation Control

    /// <summary>
    /// Suaviza la rotación del personaje hacia donde se está moviendo.
    /// </summary>
    private void RotateTowardsMovement()
    {
        if (rotationLocked)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, lockedRotation, 12f * Time.fixedDeltaTime);
            return;
        }

        Vector3 direction = new Vector3(moveDirection.x, 0, moveDirection.z);
        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 12f * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Bloquea la dirección del jugador en uno de los 8 ejes principales (Norte, Sur, Noreste, etc).
    /// </summary>
    public void LockFacingTo8Directions(Vector3 worldDirection, bool setAnimatorAxes = true)
    {
        if (worldDirection.sqrMagnitude < 0.0001f) return;

        Vector3 dir = worldDirection;
        dir.y = 0f;
        dir.Normalize();

        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / 45f) * 45f;
        Quaternion target = Quaternion.Euler(0f, snapped, 0f);

        rotationLocked = true;
        lockedRotation = target;

        if (setAnimatorAxes && playerAnimCtrl != null && mainCameraTransform != null)
        {
            Vector3 camForward = mainCameraTransform.forward;
            Vector3 camRight = mainCameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 snappedDir = target * Vector3.forward;
            float x = Mathf.Round(Vector3.Dot(snappedDir, camRight));
            float y = Mathf.Round(Vector3.Dot(snappedDir, camForward));

            lastMoveX = x;
            lastMoveY = y;

            playerAnimCtrl.SetInputAxes(lastMoveX, lastMoveY);
        }
    }

    /// <summary>
    /// Desbloquea la rotación del jugador permitiendo giro libre nuevamente.
    /// </summary>
    public void UnlockFacing()
    {
        rotationLocked = false;

        if (playerAnimCtrl != null && mainCameraTransform != null)
        {
            Vector3 camForward = mainCameraTransform.forward;
            Vector3 camRight = mainCameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            if (moveDirection.magnitude > 0.1f)
            {
                Vector3 currentDir = moveDirection.normalized;
                float x = Mathf.Round(Vector3.Dot(currentDir, camRight));
                float y = Mathf.Round(Vector3.Dot(currentDir, camForward));

                lastMoveX = x;
                lastMoveY = y;
            }

            playerAnimCtrl.SetInputAxes(lastMoveX, lastMoveY);
        }
    }

    public Quaternion GetLockedRotation()
    {
        return lockedRotation;
    }

    public void ForceApplyLockedRotation()
    {
        transform.rotation = lockedRotation;
    }

    #endregion

    #region Dash Mechanics

    public float DashDistanceReturn() => GetMaxSafeDistance(moveDirection.magnitude > 0.1f ? moveDirection : transform.forward, currentDashDistance);

    public IEnumerator ExecuteDashFromManager()
    {
        yield return StartCoroutine(DashRoutine());
    }

    /// <summary>
    /// Corrutina principal que controla todo el flujo del dash: orientación, validación de ruta, animación y desplazamiento.
    /// </summary>
    private IEnumerator DashRoutine()
    {
        Vector3 dashDirection;

        if (currentInputVector.sqrMagnitude > 0.01f)
        {
            Vector3 cameraForward = mainCameraTransform != null ? mainCameraTransform.forward : Vector3.forward;
            Vector3 cameraRight = mainCameraTransform != null ? mainCameraTransform.right : Vector3.right;

            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();

            dashDirection = (cameraForward * currentInputVector.y + cameraRight * currentInputVector.x).normalized;
            transform.rotation = Quaternion.LookRotation(dashDirection);
        }
        else
        {
            dashDirection = moveDirection.magnitude > 0.1f ? moveDirection : transform.forward;
        }

        if (!ValidateDashPath(dashDirection, out Vector3 targetDashPosition))
        {
            yield break;
        }

        LastCalculatedDashDistance = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(targetDashPosition.x, 0f, targetDashPosition.z));

        IsDashing = true;
        PlayerCombatEvents.RaiseDashStarted(transform.position, dashDirection);
        OnDashPerformed?.Invoke();
        yVelocity = 0f;
        moveDirection = Vector3.zero;
        if (playerHealth != null) playerHealth.IsInvulnerable = true;
        ToggleLayerCollisions(true);

        playerAnimCtrl?.StartDash();

        if (playerAudioController != null)
        {
            playerAudioController.PlayDashSound();
        }

        if (dashDustVFX != null) PlayDashVFX(true);

        playerVfxCtrl?.StartDashEffect();
        // if (afterimagePrefab != null) StartCoroutine(AfterimageRoutine());

        yield return StartCoroutine(PerformDash(targetDashPosition, dashDuration));

        float safetyPushSpeed = (currentDashDistance / dashDuration) * 0.4f;
        float maxStuckTime = 0.3f;
        float stuckTimer = 0f;
        int maxPushAttempts = 5;
        int pushAttempts = 0;

        Vector3 capsuleCenter = transform.position + controller.center;
        Vector3 sp1 = capsuleCenter + Vector3.up * (controller.height / 2f - controller.radius);
        Vector3 sp2 = capsuleCenter - Vector3.up * (controller.height / 2f - controller.radius);

        while (Physics.CheckCapsule(sp1, sp2, controller.radius, traversableLayers, QueryTriggerInteraction.Ignore)
               && stuckTimer < maxStuckTime
               && pushAttempts < maxPushAttempts)
        {
            Vector3 pushDelta = dashDirection * safetyPushSpeed * Time.deltaTime;

            if (!IsPositionSafeForCapsule(transform.position + pushDelta))
            {
                ReportDebug("Safety push detenido: posición insegura detectada.", 1);
                break;
            }

            if (Physics.SphereCast(transform.position + controller.center, controller.radius * 0.8f,
                Vector3.up, out _, controller.height * 0.3f, dashCollisionLayers, QueryTriggerInteraction.Ignore))
            {
                ReportDebug("Safety push detenido: superficie encima detectada.", 1);
                break;
            }

            CollisionFlags pushFlags = controller.Move(pushDelta);
            if ((pushFlags & CollisionFlags.Above) != 0)
            {
                ReportDebug("Safety push detenido: colisión superior.", 1);
                break;
            }

            stuckTimer += Time.deltaTime;
            pushAttempts++;

            capsuleCenter = transform.position + controller.center;
            sp1 = capsuleCenter + Vector3.up * (controller.height / 2f - controller.radius);
            sp2 = capsuleCenter - Vector3.up * (controller.height / 2f - controller.radius);

            yield return null;
        }

        if (dashDustVFX != null) PlayDashVFX(false);
        ToggleLayerCollisions(false);
        if (playerHealth != null) playerHealth.IsInvulnerable = false;

        IsDashing = false;

        playerVfxCtrl?.StopDashEffect();

        playerAnimCtrl?.EndDash();

        dashCooldownTimer = currentDashCooldown;
    }

    /// <summary>
    /// Realiza el desplazamiento suave del dash a lo largo del tiempo indicado.
    /// </summary>
    /// <param name="targetPosition">Punto final de destino.</param>
    /// <param name="duration">Tiempo en segundos que tardará el recorrido.</param>
    private IEnumerator PerformDash(Vector3 targetPosition, float duration)
    {
        if (controller == null || !controller.enabled) yield break;

        float savedGravity = gravity;
        float savedYVelocity = yVelocity;
        float originalStepOffset = controller.stepOffset;

        gravity = 0f;
        yVelocity = 0f;
        controller.stepOffset = dashStepOffsetOverride;

        Vector3 startPosition = transform.position;
        Vector3 startPosXZ = new Vector3(startPosition.x, 0f, startPosition.z);
        Vector3 targetPosXZ = new Vector3(targetPosition.x, 0f, targetPosition.z);

        float startY = startPosition.y;
        float targetY = targetPosition.y;
        bool isGapCrossing = Mathf.Abs(targetY - startY) > 0.3f;

        float startTime = Time.time;
        float endTime = startTime + duration;
        float journeyLength = Vector3.Distance(startPosXZ, targetPosXZ);

        if (journeyLength < 0.1f)
        {
            gravity = savedGravity;
            yVelocity = savedYVelocity;
            controller.stepOffset = originalStepOffset;
            yield break;
        }

        while (Time.time < endTime)
        {
            if (!controller.enabled) break;

            float t = Mathf.Clamp01((Time.time - startTime) / duration);
            Vector3 lerpedXZ = Vector3.Lerp(startPosXZ, targetPosXZ, t);

            Vector3 current = transform.position;
            Vector3 currentFixedY = new Vector3(current.x, startY, current.z);
            Vector3 next = new Vector3(lerpedXZ.x, startY, lerpedXZ.z);
            Vector3 delta = next - currentFixedY;
            delta.y = 0f;

            CollisionFlags flags = controller.Move(delta);
            yVelocity = 0f;

            if ((flags & CollisionFlags.Above) != 0)
            {
                Vector3 correctedPos = transform.position;
                correctedPos.y = startY;
                transform.position = correctedPos;

                ReportDebug("PerformDash: colisión superior detectada, Y corregida.", 2);
            }

            yield return null;
        }

        if (controller != null && controller.enabled)
        {
            Vector3 finalCurrent = transform.position;
            Vector3 finalCurrentXZ = new Vector3(finalCurrent.x, startY, finalCurrent.z);
            Vector3 finalTargetXZ = new Vector3(targetPosXZ.x, startY, targetPosXZ.z);
            Vector3 finalDelta = finalTargetXZ - finalCurrentXZ;
            finalDelta.y = 0f;

            if (finalDelta.sqrMagnitude > 0.0001f)
            {
                controller.Move(finalDelta);
            }
        }

        if (isGapCrossing)
        {
            yVelocity = 0f;
        }

        gravity = savedGravity;
        yVelocity = savedYVelocity;
        controller.stepOffset = originalStepOffset;
    }

    /// <summary>
    /// Comprueba el trayecto del dash para asegurar que no traspasa obstáculos y que termina en un piso válido (sin caer a abismos).
    /// </summary>
    /// <param name="direction">Vector de dirección del dash.</param>
    /// <param name="finalPosition">Variable de salida con la posición final segura calculada.</param>
    private bool ValidateDashPath(Vector3 direction, out Vector3 finalPosition)
    {
        Vector3 origin = transform.position;
        float playerRadius = controller.radius;
        float playerHeight = controller.height;

        Vector3 p1 = origin + controller.center + Vector3.up * (playerHeight / 2f - playerRadius);
        Vector3 p2 = origin + controller.center - Vector3.up * (playerHeight / 2f - playerRadius);

        if (Physics.CapsuleCast(p1, p2, playerRadius, direction, out RaycastHit obstacleHit,
            currentDashDistance, dashCollisionLayers, QueryTriggerInteraction.Ignore))
        {
            float adjustedDistance = Mathf.Max(0f, obstacleHit.distance - playerRadius - dashObstacleSkin);

            if (!HasGroundAlongPath(direction, adjustedDistance))
            {
                finalPosition = origin;
                lastTargetCheck = origin;
                lastTargetHit = false;
                ReportDebug("Dash cancelado: vacío detectado entre jugador y obstáculo.", 2);
                return false;
            }

            if (adjustedDistance < minEffectiveDashDistance)
            {
                finalPosition = origin;
                lastTargetCheck = origin;
                lastTargetHit = false;
                ReportDebug("Dash cancelado: obstáculo demasiado cerca.", 2);
                return false;
            }

            finalPosition = origin + direction * adjustedDistance;
            lastTargetCheck = finalPosition;
            lastTargetHit = true;
            ReportDebug("El camino está bloqueado por un obstáculo. Ajustando la distancia.", 1);
            return true;
        }

        float scanHeight = dashGroundProbeHeight;
        Vector3 baseTarget = origin + direction * currentDashDistance + Vector3.up * scanHeight;

        if (Physics.Raycast(baseTarget, Vector3.down, out RaycastHit baseGroundHit,
            playerHeight + scanHeight + 1f, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            finalPosition = baseGroundHit.point;
            lastTargetCheck = finalPosition;
            lastTargetHit = true;
            ReportDebug("Dash estándar seguro.", 1);
            return true;
        }

        float scanStart = currentDashDistance + 0.1f;
        float scanMax = currentDashDistance + gapDashBonusDistance;
        float scanStep = Mathf.Max(0.15f, dashGroundSampleStep);

        RaycastHit foundHit = new RaycastHit();
        bool found = false;

        for (float t = scanStart; t <= scanMax; t += scanStep)
        {
            Vector3 probe = origin + direction * t + Vector3.up * scanHeight;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit gh,
                playerHeight + scanHeight + 1f, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                foundHit = gh;
                found = true;
                break;
            }
        }

        if (found)
        {
            float safetyPenetration = playerRadius + 0.3f;
            Vector3 idealLandingPos = foundHit.point + (direction.normalized * safetyPenetration);

            if (IsPositionSafeForCapsule(idealLandingPos))
            {
                finalPosition = idealLandingPos;
                lastTargetCheck = finalPosition;
                lastTargetHit = true;
                ReportDebug("Aterrizaje profundo seguro.", 1);
                return true;
            }

            Vector3 midPoint = Vector3.Lerp(foundHit.point, idealLandingPos, 0.5f);
            if (IsPositionSafeForCapsule(midPoint))
            {
                finalPosition = midPoint;
                lastTargetCheck = finalPosition;
                lastTargetHit = true;
                ReportDebug("Aterrizaje ajustado (Punto medio).", 1);
                return true;
            }

            finalPosition = foundHit.point + (direction.normalized * 0.05f);
            lastTargetCheck = finalPosition;
            lastTargetHit = true;
            ReportDebug("Aterrizaje en borde crítico (Plataforma pequeña).", 2);
            return true;
        }

        float safeLedgeDistance = GetMaxSafeDistance(direction, currentDashDistance);

        if (safeLedgeDistance > edgeSafetyMargin)
        {
            if (safeLedgeDistance < minEffectiveDashDistance)
            {
                finalPosition = origin;
                lastTargetCheck = origin;
                lastTargetHit = false;
                ReportDebug("Dash bloqueado por borde (Distancia segura insuficiente).", 1);
                return false;
            }

            Vector3 safeTarget = origin + direction * safeLedgeDistance + Vector3.up * scanHeight;
            if (Physics.Raycast(safeTarget, Vector3.down, out RaycastHit ledgeGroundHit,
                playerHeight + scanHeight + 1f, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                finalPosition = ledgeGroundHit.point;
                lastTargetCheck = finalPosition;
                lastTargetHit = true;
                ReportDebug("Dash limitado por borde (Safe Ledge).", 1);
                return true;
            }

            finalPosition = origin + direction * safeLedgeDistance;
            lastTargetCheck = finalPosition;
            lastTargetHit = true;
            return true;
        }

        finalPosition = origin;
        lastTargetCheck = origin;
        lastTargetHit = false;
        ReportDebug("Dash cancelado por inseguridad total.", 2);
        return false;
    }

    /// <summary>
    /// Cancela abruptamente la acción de dash y reinicia los valores físicos.
    /// </summary>
    public void CancelDash()
    {
        if (!IsDashing) return;

        StopAllCoroutines();

        IsDashing = false;

        playerAnimCtrl?.EndDash();

        if (dashDustVFX != null) PlayDashVFX(false);

        ToggleLayerCollisions(false);

        if (playerHealth != null) playerHealth.IsInvulnerable = false;

        if (controller != null && prevStepOffset > 0)
        {
            controller.stepOffset = prevStepOffset;
            prevStepOffset = 0f;
        }

        yVelocity = 0f;

        ReportDebug("Dash cancelado.", 1);
    }

    /// <summary>
    /// Desactiva el dash durante una cantidad específica de segundos.
    /// </summary>
    public void DisableDashForDuration(float duration)
    {
        if (_dashDisableCoroutine != null)
        {
            StopCoroutine(_dashDisableCoroutine);
        }

        _dashDisableCoroutine = StartCoroutine(DashDisableRoutine(duration));
    }

    private IEnumerator DashDisableRoutine(float duration)
    {
        IsDashDisabled = true;
        yield return new WaitForSeconds(duration);
        IsDashDisabled = false;
        _dashDisableCoroutine = null;
    }

    #endregion

    #region Edge Detection & Environment Navigation

    /// <summary>
    /// Modifica el vector de movimiento base para que el jugador se detenga antes de caer por un borde.
    /// </summary>
    /// <param name="movement">El vector original de movimiento sugerido.</param>
    /// <returns>Retorna el mismo vector o un vector truncado (sin avance) si hay un borde.</returns>
    private Vector3 ApplyEdgeDetection(Vector3 movement)
    {
        if (!controller.isGrounded)
        {
            return movement;
        }

        Vector3 horizontalMovement = new Vector3(movement.x, 0f, movement.z);
        if (horizontalMovement.magnitude < 0.01f)
        {
            return movement;
        }

        Vector3 movementDirection = horizontalMovement.normalized;
        Vector3 rayOrigin = transform.position + Vector3.up * edgeRaycastHeight;
        Vector3 checkPosition = rayOrigin + movementDirection * (controller.radius + edgeDetectionDistance);

        float rayDistance = controller.height + 0.5f;

        if (!Physics.Raycast(checkPosition, Vector3.down, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            ReportDebug("Borde detectado. Bloqueando movimiento para prevenir caída.", 1);
            return new Vector3(0f, movement.y, 0f);
        }

        return movement;
    }

    /// <summary>
    /// Verifica mediante múltiples puntos de control (raycasts) si la posición indicada tiene suelo sólido por debajo.
    /// </summary>
    public bool IsPositionSafeForCapsule(Vector3 pos)
    {
        if (controller == null) return false;

        float checkRadius = controller.radius * 0.95f;
        float rayLength = controller.height + 1.0f;

        for (int i = 0; i < safeCheckDirections.Length; i++)
        {
            Vector3 checkPos = pos + safeCheckDirections[i] * checkRadius;
            Vector3 origin = checkPos + Vector3.up * edgeRaycastHeight;
            if (!Physics.Raycast(origin, Vector3.down, rayLength, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Verifica si un trayecto lineal entero tiene un piso que lo soporte, previniendo dashes o movimientos que crucen grandes abismos.
    /// </summary>
    private bool HasGroundAlongPath(Vector3 direction, float distance)
    {
        if (distance < 0.1f) return true;

        int samples = Mathf.CeilToInt(distance / 0.5f);
        samples = Mathf.Clamp(samples, 3, 15);

        float step = distance / samples;

        for (int i = 1; i <= samples; i++)
        {
            Vector3 checkPos = transform.position + direction * (step * i) + Vector3.up * 2f;

            if (!Physics.Raycast(checkPos, Vector3.down, controller.height + 3f, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                ReportDebug($"Vacío detectado en el camino a {(step * i):F2}m de {distance:F2}m totales.", 2);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Calcula la distancia máxima que el jugador puede avanzar en una dirección dada antes de tocar un borde peligroso.
    /// </summary>
    public float GetMaxSafeDistance(Vector3 dir, float maxDesiredDistance)
    {
        if (!enableEdgeDetection || controller == null || !IsEffectivelyGrounded())
        {
            return maxDesiredDistance;
        }

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return 0f;
        dir.Normalize();

        float step = Mathf.Max(0.05f, edgeSampleStepDistance);
        int samples = Mathf.Min(Mathf.CeilToInt(maxDesiredDistance / step), edgeSampleMax * 2);
        Vector3 basePos = transform.position;
        float safeDistance = 0f;

        for (int i = 1; i <= samples; i++)
        {
            float traveled = Mathf.Min(maxDesiredDistance, step * i);
            Vector3 testPos = basePos + dir * traveled;

            if (!IsPositionSafeForCapsule(testPos))
            {
                return Mathf.Max(0f, safeDistance - edgeSafetyMargin);
            }

            safeDistance = traveled;
        }

        return Mathf.Max(0f, safeDistance - edgeSafetyMargin);
    }

    /// <summary>
    /// Reduce el desplazamiento horizontal si el final de este cruza el borde de la plataforma.
    /// </summary>
    private Vector3 ComputeSafeHorizontalDisplacement(Vector3 desiredDisplacement)
    {
        Vector3 horizontal = new Vector3(desiredDisplacement.x, 0f, desiredDisplacement.z);
        float mag = horizontal.magnitude;
        if (mag < minHorizontalMagnitude || controller == null) return desiredDisplacement;

        Vector3 dir = horizontal.normalized;
        int samples = Mathf.Clamp(Mathf.CeilToInt((mag) / (controller.radius + edgeDetectionDistance)), 1, edgeSampleMax);
        float step = mag / samples;
        Vector3 rayOriginBase = transform.position + Vector3.up * edgeRaycastHeight;
        float rayDistance = controller.height + 0.6f;

        for (int i = 1; i <= samples; i++)
        {
            float traveled = step * i;
            Vector3 samplePos = rayOriginBase + dir * Mathf.Max(0f, traveled + controller.radius + edgeDetectionDistance);

            if (!Physics.Raycast(samplePos, Vector3.down, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                float safeDistance = Mathf.Max(0f, (step * (i - 1)) - edgeSafetyMargin);
                Vector3 safeHorizontal = dir * safeDistance;
                return new Vector3(safeHorizontal.x, desiredDisplacement.y, safeHorizontal.z);
            }
        }

        return desiredDisplacement;
    }

    /// <summary>
    /// Intenta desplazar al jugador hacia los lados (deslizarse) en vez de detenerlo completamente si choca contra un borde en un ángulo diagonal.
    /// </summary>
    private Vector3 TryComputeSlideAlongEdge(Vector3 forwardDir, float maxAllowedForwardDistance)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forwardDir).normalized;
        Vector3[] lateralDirs = new Vector3[] { right, -right };
        Vector3 rayOriginBase = transform.position + Vector3.up * edgeRaycastHeight;
        float rayDistance = controller.height + 0.6f;
        float lateralMagnitude = Mathf.Max(controller.radius, 0.25f);

        foreach (var lat in lateralDirs)
        {
            Vector3 testPos = rayOriginBase + forwardDir * (maxAllowedForwardDistance + controller.radius) + lat * lateralMagnitude;
            if (Physics.Raycast(testPos, Vector3.down, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 combined = forwardDir * maxAllowedForwardDistance + lat * lateralMagnitude;
                return combined;
            }
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Método maestro para aplicar todos los controles de borde al desplazamiento libre (como ataques o empujes).
    /// </summary>
    private Vector3 ApplyEdgeDetectionToDisplacement(Vector3 displacement)
    {
        if (!enableEdgeDetection || controller == null || !IsEffectivelyGrounded()) return displacement;

        Vector3 horizontal = new Vector3(displacement.x, 0f, displacement.z);
        if (horizontal.magnitude < minHorizontalMagnitude) return displacement;

        Vector3 safe = ComputeSafeHorizontalDisplacement(displacement);
        Vector3 desiredHorizontal = new Vector3(displacement.x, 0f, displacement.z);
        Vector3 safeHorizontal = new Vector3(safe.x, 0f, safe.z);

        if (safeHorizontal.sqrMagnitude + 0.0001f < desiredHorizontal.sqrMagnitude)
        {
            ReportDebug("Borde detectado durante ataque. Desplazamiento recortado", 1);

            Vector3 slide = TryComputeSlideAlongEdge(desiredHorizontal.normalized, safeHorizontal.magnitude);
            if (slide.sqrMagnitude > 0.0001f)
            {
                return new Vector3(slide.x, displacement.y, slide.z);
            }

            return new Vector3(0f, displacement.y, 0f);
        }

        return displacement;
    }

    /// <summary>
    /// Confirma con un solo raycast si moverse en cierta dirección por 'X' metros evitará caer por un borde.
    /// </summary>
    public bool IsMovementSafeDirection(Vector3 dir, float distance)
    {
        if (!enableEdgeDetection || controller == null) return true;
        if (!IsEffectivelyGrounded()) return false;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return true;
        dir.Normalize();

        Vector3 rayOrigin = transform.position + Vector3.up * edgeRaycastHeight;
        Vector3 targetCheck = rayOrigin + dir * (Mathf.Max(0f, distance) + controller.radius + edgeDetectionDistance);
        float rayDistance = controller.height + 0.6f;

        if (Physics.Raycast(targetCheck, Vector3.down, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Activa o desactiva la colisión del jugador contra capas especificadas para permitir "atravesar" temporalmente objetos o enemigos.
    /// </summary>
    private void ToggleLayerCollisions(bool ignore)
    {
        for (int i = 0; i < 32; i++)
        {
            if (traversableLayers == (traversableLayers | (1 << i)))
            {
                Physics.IgnoreLayerCollision(playerLayer, i, ignore);
            }
        }
    }

    #endregion

    #region Visual & Audio Effects

    /// <summary>
    /// Maneja el contador interno que dispara el sonido de las pisadas.
    /// </summary>
    private void HandleFootstepsTimer()
    {
        stepTimer -= Time.deltaTime;

        if (stepTimer <= 0f)
        {
            if (playerAudioController != null)
            {
                playerAudioController.PlayStepSound(level);
            }

            stepTimer = stepInterval;
        }
    }

    /// <summary>
    /// Instancia una copia limpia del material para las partículas del dash.
    /// </summary>
    private void InitializeDashVFX()
    {
        if (dashDustVFX == null) return;

        dashVFXMaterialInstance = new Material(dashDustVFX.GetComponent<ParticleSystemRenderer>().sharedMaterial);
        dashDustVFX.GetComponent<ParticleSystemRenderer>().material = dashVFXMaterialInstance;

        dashDustVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        dashDustVFX.Clear(true);
    }

    /// <summary>
    /// Prende o apaga el sistema de partículas que genera polvo durante el dash.
    /// </summary>
    private void PlayDashVFX(bool active)
    {
        if (dashDustVFX == null) return;
        // var emission = dashDustVFX.emission;
        // emission.enabled = active;

        // if (active)
        // {
        //     if (!dashDustVFX.isPlaying)
        //     {
        //         dashDustVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        //         dashDustVFX.Play(true);
        //     }
        // }
        // else
        // {
        //     if(!dashDustVFX.isStopped) dashDustVFX.Stop();
        // }
    }

    /// <summary>
    /// Genera clones transparentes del sprite del jugador cada ciertos microsegundos durante el dash.
    /// </summary>
    private IEnumerator AfterimageRoutine()
    {
        float interval = 0.05f;

        while (IsDashing)
        {
            if (afterimagePrefab != null)
            {
                SpriteRenderer srcSprite = GetComponentInChildren<SpriteRenderer>();

                if (srcSprite != null)
                {
                    GameObject afterimage = Instantiate(afterimagePrefab, srcSprite.transform.position, srcSprite.transform.rotation);

                    afterimage.transform.localScale = srcSprite.transform.lossyScale;

                    SpriteRenderer dstSprite = afterimage.GetComponent<SpriteRenderer>();

                    if (dstSprite != null)
                    {
                        dstSprite.sprite = srcSprite.sprite;
                        dstSprite.flipX = srcSprite.flipX;
                        dstSprite.flipY = srcSprite.flipY;
                        Color color = srcSprite.color;
                        color.a = afterimageAlpha;
                        dstSprite.color = color;

                        dstSprite.sortingLayerID = srcSprite.sortingLayerID;
                        dstSprite.sortingOrder = srcSprite.sortingOrder - 1;
                    }
                    Destroy(afterimage, afterimageLifetime);
                }
            }
            yield return new WaitForSeconds(interval);
        }
    }

    #endregion

    #region Legacy & Animation Support

    /*
    private void UpdateMovementAnimationSpeed(bool isMoving)
    {
        if (playerAnimator == null)
        {
            return;
        }

        if (hasIsAttackingParameter && playerAnimator.GetBool("IsAttacking"))
        {
            return;
        }

        float targetAnimatorSpeed = 1f;

        if (isMoving && runAnimationSpeedOverrides.Count > 0)
        {
            targetAnimatorSpeed = GetLowestRunAnimationSpeedOverride();
        }

        playerAnimator.speed = targetAnimatorSpeed;
    }
    
    private bool AnimatorHasParameter(Animator animator, string parameterName)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    public void SetRunAnimationSpeedOverride(string key, float animatorSpeed)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        runAnimationSpeedOverrides[key] = Mathf.Clamp(animatorSpeed, 0.1f, 3f);
        UpdateMovementAnimationSpeed(playerAnimator != null && playerAnimator.GetBool("Running"));
    }

    public void ClearRunAnimationSpeedOverride(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        runAnimationSpeedOverrides.Remove(key);
        UpdateMovementAnimationSpeed(playerAnimator != null && playerAnimator.GetBool("Running"));
    }

    private float GetLowestRunAnimationSpeedOverride()
    {
        float lowestSpeed = 1f;

        foreach (float overrideSpeed in runAnimationSpeedOverrides.Values)
        {
            if (overrideSpeed < lowestSpeed)
            {
                lowestSpeed = overrideSpeed;
            }
        }

        return lowestSpeed;
    }
    */

    #endregion

    #region Debugging

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying || controller == null) return;
        if (!canDebug) return;

        Vector3 origin = transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, 0.15f);

        Vector3 dashDirection = moveDirection.magnitude > 0.1f ? moveDirection.normalized : transform.forward;

        Gizmos.color = Color.yellow;
        Vector3 baseEnd = origin + dashDirection * currentDashDistance;
        Gizmos.DrawLine(origin, baseEnd);
        Gizmos.DrawWireSphere(baseEnd, 0.15f);

        Gizmos.color = Color.magenta;
        Vector3 bonusEnd = origin + dashDirection * (currentDashDistance + gapDashBonusDistance);
        Gizmos.DrawLine(baseEnd, bonusEnd);
        Gizmos.DrawWireSphere(bonusEnd, 0.1f);

        float scanHeight = dashGroundProbeHeight;
        Vector3 downOrigin = baseEnd + Vector3.up * scanHeight;
        float downRayMax = controller.height + scanHeight + 1f;

        if (Physics.Raycast(downOrigin, Vector3.down, out RaycastHit baseGroundHit, downRayMax, groundLayerMask))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(downOrigin, baseGroundHit.point);
            Gizmos.DrawWireSphere(baseGroundHit.point, 0.2f);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(downOrigin, downOrigin + Vector3.down * downRayMax);
        }

        if (enableEdgeDetection && controller.isGrounded && moveDirection.magnitude > 0.1f)
        {
            Vector3 movementDirection = new Vector3(moveDirection.x, 0f, moveDirection.z).normalized;
            Vector3 rayOriginBase = transform.position + Vector3.up * edgeRaycastHeight;
            float rayDistance = controller.height + 0.6f;
            float totalDistance = currentDashDistance + gapDashBonusDistance;
            float gizmoStep = Mathf.Max(0.1f, edgeSampleStepDistance);
            int numSamples = Mathf.CeilToInt(totalDistance / gizmoStep);

            Vector3 firstCheck = rayOriginBase + movementDirection * (controller.radius + edgeDetectionDistance);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(rayOriginBase, firstCheck);
            Gizmos.DrawWireSphere(firstCheck, 0.1f);

            for (int i = 1; i <= numSamples; i++)
            {
                float traveled = Mathf.Min(totalDistance, gizmoStep * i);
                Vector3 samplePos = rayOriginBase + movementDirection * traveled;

                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(samplePos, Mathf.Max(0.03f, controller.radius * 0.2f));

                if (Physics.Raycast(samplePos, Vector3.down, out RaycastHit hit, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(samplePos, hit.point);
                    Gizmos.DrawWireSphere(hit.point, 0.12f);
                }
                else
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(samplePos, samplePos + Vector3.down * rayDistance);
                    Gizmos.DrawWireSphere(samplePos + Vector3.down * rayDistance, 0.12f);
                }
            }
        }

        if (lastTargetCheck != Vector3.zero)
        {
            Gizmos.color = lastTargetHit ? Color.green : Color.red;
            Gizmos.DrawWireSphere(lastTargetCheck, 0.12f);
            Gizmos.DrawLine(lastTargetCheck, lastTargetCheck + Vector3.down * (controller.height + 0.6f));

            UnityEditor.Handles.Label(lastTargetCheck + Vector3.up * 0.2f, lastTargetHit
                ? $"{lastTargetCheck} hit" : $"{lastTargetCheck} miss");
        }

        UnityEditor.Handles.Label(origin + Vector3.up * 1.5f, "Origen de Dash");
        UnityEditor.Handles.Label(baseEnd + Vector3.up * 1.5f, "Alcance base");
        UnityEditor.Handles.Label(bonusEnd + Vector3.up * 1.5f, "Máximo con bonificación");

        if (enableEdgeDetection && controller.isGrounded)
        {
            UnityEditor.Handles.Label(origin + Vector3.up * 2.0f, "Detección de bordes activada");
        }
        else
        {
            UnityEditor.Handles.Label(origin + Vector3.up * 2.0f, "Detección de bordes desactivada");
        }

        Vector3 testDir = moveDirection.magnitude > 0.1f ? moveDirection.normalized : transform.forward;
        Vector3 testP1 = origin + controller.center + Vector3.up * (controller.height / 2f - controller.radius);
        Vector3 testP2 = origin + controller.center - Vector3.up * (controller.height / 2f - controller.radius);

        if (Physics.CapsuleCast(testP1, testP2, controller.radius, testDir,
            out RaycastHit obsHit, currentDashDistance, dashCollisionLayers, QueryTriggerInteraction.Ignore))
        {
            float checkDist = Mathf.Max(0f, obsHit.distance - controller.radius);
            float step = Mathf.Max(0.1f, edgeSampleStepDistance);
            int samples = Mathf.CeilToInt(checkDist / step);

            samples = Mathf.Clamp(samples, 2, edgeSampleMax * 2);

            for (int i = 1; i <= samples; i++)
            {
                Vector3 checkPos = origin + testDir * (step * i);
                Vector3 rayStart = checkPos + Vector3.up * 2f;
                bool hasGround = Physics.Raycast(rayStart, Vector3.down, controller.height + 3f, groundLayerMask, QueryTriggerInteraction.Ignore);

                Gizmos.color = hasGround ? new Color(0, 1, 0, 0.7f) : new Color(1, 0, 0, 0.9f);
                Gizmos.DrawWireSphere(checkPos, 0.15f);
                Gizmos.DrawLine(rayStart, rayStart + Vector3.down * (controller.height + 3f));

                if (!hasGround)
                {
                    UnityEditor.Handles.color = Color.red;
                    UnityEditor.Handles.Label(checkPos + Vector3.up * 0.5f, "VACÍO!");
                }
            }

            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.Label(origin + Vector3.up * 2.5f,
                $"Obstáculo a {obsHit.distance:F1}m - Verificando camino");
        }

        if (lastTargetCheck != Vector3.zero)
        {
            Vector3 fwdHint = dashDirection;
            fwdHint.y = 0f;
            fwdHint.Normalize();
            Vector3 rightHint = Vector3.Cross(Vector3.up, fwdHint).normalized;
            float checkRadius = controller.radius * 0.92f;

            Vector3[] safePoints = new Vector3[]
            {
            lastTargetCheck,
            lastTargetCheck + fwdHint * checkRadius,
            lastTargetCheck - fwdHint * checkRadius,
            lastTargetCheck + rightHint * checkRadius,
            lastTargetCheck - rightHint * checkRadius,
            lastTargetCheck + (fwdHint + rightHint).normalized * checkRadius,
            lastTargetCheck + (fwdHint - rightHint).normalized * checkRadius,
            lastTargetCheck + (-fwdHint + rightHint).normalized * checkRadius,
            lastTargetCheck + (-fwdHint - rightHint).normalized * checkRadius
            };

            foreach (var pt in safePoints)
            {
                Vector3 checkPoint = pt + Vector3.up * edgeRaycastHeight;
                bool hasGround = Physics.Raycast(checkPoint, Vector3.down,
                    controller.height + 1f, groundLayerMask, QueryTriggerInteraction.Ignore);

                Gizmos.color = hasGround ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.8f);
                Gizmos.DrawLine(checkPoint, checkPoint + Vector3.down * (controller.height + 1f));
                Gizmos.DrawWireSphere(checkPoint, 0.05f);
            }
        }
#endif
    }

    #endregion

    #region Logging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[PlayerMovement] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerMovement] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerMovement] {message}");
                break;
            default:
                Debug.Log($"[PlayerMovement] {message}");
                break;
        }
    }

    #endregion
}