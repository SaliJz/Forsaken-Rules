using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clase que maneja el ataque cuerpo a cuerpo del jugador.
/// </summary>
public class PlayerMeleeAttack : MonoBehaviour
{
    #region Inspector – References

    [Header("References")]
    [Tooltip("Gestor de estadisticas del jugador para obtener danio, velocidad y radio de ataque.")]
    [SerializeField] private PlayerStatsManager statsManager;
    [Tooltip("Objeto visual para depurar el area de impacto esferica.")]
    [SerializeField] private GameObject visualSphereHit;
    [Tooltip("Objeto visual para depurar el area de impacto en forma de caja.")]
    [SerializeField] private GameObject visualBoxHit;
    [Tooltip("Controlador del escudo del jugador, necesario para saber si esta disponible para atacar.")]
    [SerializeField] private PlayerShieldController playerShieldController;
    [Tooltip("Controlador de audio para reproducir sonidos de ataque e impacto.")]
    [SerializeField] private PlayerAudioController playerAudioController;
    [Tooltip("Habilidad del escudo del jugador, usada para calcular bonificaciones de dureza.")]
    [SerializeField] private ShieldSkill playerShieldSkill;
    [Tooltip("Controlador de animaciones del jugador.")]
    [SerializeField] private PlayerAnimCtrl playerAnimCtrl;
    [Tooltip("Controlador de VFX del jugador")]
    [SerializeField] private PlayerVfxCtrl playerVfxCtrl;
    [Tooltip("Componente de auto-apuntado para dirigir los ataques hacia los enemigos cercanos.")]
    [SerializeField] private AutoAim autoAim;

    #endregion

    #region Inspector – Base Attack Configuration

    [Header("Attack Configuration")]
    [Tooltip("Punto de origen desde donde se calcula el area de impacto del ataque.")]
    [SerializeField] private Transform hitPoint;

    [Header("Fallback stats (if no StatsManager)")]
    [HideInInspector] private float fallbackHitRadius = 0.8f;
    [HideInInspector] private float fallbackAttackDamage = 10;
    [HideInInspector] private float fallbackAttackSpeed = 1f;

    [Header("Calculated stats")]
    [Tooltip("Radio de alcance del golpe cuerpo a cuerpo.")]
    [SerializeField] private float hitRadius = 0.8f;
    [Tooltip("Cantidad de danio base que inflige el ataque.")]
    [SerializeField] private float attackDamage = 10;
    [Tooltip("Velocidad a la que se ejecuta el ataque.")]
    [SerializeField] private float attackSpeed = 1f;
    [Tooltip("Valor de referencia para calcular el factor de velocidad de las animaciones.")]
    [SerializeField] private float baseSpeedReference = 1f;
    [Tooltip("Capa que define que objetos son considerados enemigos para recibir danio.")]
    [SerializeField] private LayerMask enemyLayer;
    [Tooltip("Si esta activo, dibuja en el editor el area de impacto de los ataques.")]
    [SerializeField] private bool canShowHitGizmo = false;

    [Header("Hit Detection")]
    [Tooltip("Intervalo de tiempo entre cada parpadeo visual del enemigo al ser aturdido.")]
    [SerializeField] private float blinkInterval = 0.1f;
    [Tooltip("Cantidad de veces que parpadeara el enemigo al ser aturdido.")]
    [SerializeField] private int blinkCount = 10;

    #endregion

    #region Inspector – Combo Configuration

    [Header("Combo Configuration")]
    [Tooltip("Distancia maxima para detectar y apuntar automaticamente a un enemigo.")]
    [SerializeField] private float autoAimRange = 5f;
    [Tooltip("Tiempo en segundos antes de que el combo de ataques se reinicie a cero.")]
    [SerializeField] private float comboResetTime = 2f;
    [Tooltip("Fuerza de impulso hacia adelante aplicada al jugador en cada golpe del combo.")]
    [SerializeField] private float[] baseComboMovementForces = new float[3] { 1.5f, 2f, 1.8f };
    [Tooltip("Tiempo en segundos que el jugador queda inmovilizado tras cada golpe del combo.")]
    [SerializeField] private float[] comboLockDurations = new float[3] { 0.4f, 0.6f, 0.8f };
    [Tooltip("Duracion del aturdimiento aplicado a los enemigos por cada golpe del combo.")]
    [SerializeField] private float[] comboStunDurations = new float[3] { 0.5f, 0.5f, 1f };

    #endregion

    #region Inspector – Attack Moves Settings

    [Header("Attack 1 (Basic)")]
    [Tooltip("Duracion total en segundos de la animacion del primer ataque.")]
    [SerializeField] private float attack1Duration = 0.4f;

    [Header("Attack 2 (Area/Spin)")]
    [Tooltip("Tiempo durante el cual el jugador se desplaza en el segundo ataque.")]
    [SerializeField] private float attack2MovementDuration = 0.6f;
    [Tooltip("Duracion del giro sobre su propio eje en el segundo ataque.")]
    [SerializeField] private float attack2SpinDuration = 0.4f;
    [Tooltip("Velocidad a la que el jugador gira en el segundo ataque.")]
    [SerializeField] private float attack2SpinSpeed = 900f;
    [Tooltip("Angulo total objetivo a girar durante el segundo ataque.")]
    [SerializeField] private float attack2TargetSpinAngle = 360f;

    [Header("Attack 3 (Heavy/Charge)")]
    [Tooltip("Tiempo de preparacion antes de lanzar el golpe final del combo.")]
    [SerializeField] private float attack3PreChargeDuration = 0.3f;
    [Tooltip("Duracion del desplazamiento hacia adelante en el golpe final.")]
    [SerializeField] private float attack3ChargeDuration = 0.3f;
    [Tooltip("Velocidad de giro del personaje durante la carga del tercer ataque.")]
    [SerializeField] private float attack3SpinSpeed = 90f;
    [Tooltip("Multiplicador de danio aplicado solo en el tercer golpe del combo")]
    [SerializeField] private float attack3DamageMultiplier = 1.5f;

    #endregion

    #region Inspector – Knockback Configuration

    [Header("knockback Configuration")]
    [Tooltip("Fuerza de empuje aplicada a los enemigos cuando el jugador esta en la etapa Joven.")]
    [SerializeField] private float knockbackYoung = 0.25f;
    [Tooltip("Fuerza de empuje aplicada a los enemigos cuando el jugador esta en la etapa Adulta.")]
    [SerializeField] private float knockbackAdult = 0.5f;
    [Tooltip("Fuerza de empuje aplicada a los enemigos cuando el jugador esta en la etapa Anciana.")]
    [SerializeField] private float knockbackElder = 0.75f;
    [Tooltip("Distancia maxima a la que un enemigo puede ser empujado.")]
    [SerializeField] private float knockbackMaxDistance = 3f; // Distancia maxima de knockback

    #endregion

    #region Inspector – VFX & Debug

    [Header("Melee Impact VFX")]
    [Tooltip("Prefab del efecto visual que aparece cuando se impacta a un enemigo.")]
    [SerializeField] private GameObject meleeImpactVFX;
    //[SerializeField] private int impactParticleCount = 20;

    [Header("Debug")]
    [Tooltip("Si se activa, el area de impacto tendra forma de caja en lugar de esfera.")]
    [SerializeField] private bool useBoxCollider = false;
    [Tooltip("Dibuja el area de impacto en la vista de escena si esta habilitado.")]
    [SerializeField] private bool showGizmo = false;
    [Tooltip("Tiempo que permanecen visibles los gizmos de depuracion.")]
    [SerializeField] private float gizmoDuration = 0.2f;

    #endregion

    #region Internal State

    // Core Combat Stats
    private float attackCooldown = 0f;
    private float finalAttackDamage;
    private float finalAttackSpeed;
    private float damageMultiplier = 1f;
    private float speedMultiplier = 1f;
    private float currentSpeedFactor = 1f;

    // Combo & Movement State
    private bool isAttacking = false;
    private float[] currentComboMovementForces = new float[3];
    private int comboCount = 0;
    private float lastAttackTime = 0f;
    private int currentAttackIndex = -1;

    // Detection Buffers & Tracking
    private HashSet<Collider> hitEnemiesThisCombo = new HashSet<Collider>();
    private Collider[] hitBuffer = new Collider[64]; // Buffer para deteccion de enemigos
    private GamepadPointer gamepadPointer;

    // Cached Components
    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;

    // Coroutines
    private Coroutine cleanupCoroutine;
    private Coroutine showGizmoRoutine = null;

    //Confirmacion de mejora del desplazamiento melee
    private bool _hasMeleeDisplacement = false;
    public bool HsMeleeDisplacement => _hasMeleeDisplacement;

    #endregion

    #region Public Properties & Events

    public float AttackDamage
    {
        get { return attackDamage; }
        set { attackDamage = value; }
    }
    public bool IsAttacking => isAttacking;
    public int ComboCount => comboCount;
    public event Action<bool> OnAttacked;
    public bool IsOnLastComboAttack => isAttacking && currentAttackIndex == 2;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (visualSphereHit != null) visualSphereHit.SetActive(false);
        if (visualBoxHit != null) visualBoxHit.SetActive(false);

        //if (attack1Slash != null) attack1Slash.SetActive(false);
        //if (attack2Slash != null) attack2Slash.SetActive(false);
        //if (attack3Slash != null) attack3Slash.SetActive(false);

        statsManager = GetComponent<PlayerStatsManager>();
        playerHealth = GetComponent<PlayerHealth>();
        playerShieldController = GetComponent<PlayerShieldController>();
        playerMovement = GetComponent<PlayerMovement>();
        playerShieldSkill = GetComponent<ShieldSkill>();
        playerAnimCtrl = GetComponentInChildren<PlayerAnimCtrl>();
        playerVfxCtrl = GetComponentInChildren<PlayerVfxCtrl>();
        playerAudioController = GetComponent<PlayerAudioController>();
        gamepadPointer = FindAnyObjectByType<GamepadPointer>();
        autoAim = GetComponent<AutoAim>();

        if (autoAim == null) ReportDebug("ShieldAutoAim no encontrado. El auto-aim del melee no funcionara.", 2);
        if (statsManager == null) ReportDebug("StatsManager no esta asignado en PlayerMeleeAttack. Usando valores de fallback.", 2);
        if (playerHealth == null) ReportDebug("PlayerHealth no se encuentra en el objeto.", 3);
        if (playerShieldController == null) ReportDebug("PlayerShieldController no se encuentra en el objeto.", 3);
        if (playerMovement == null) ReportDebug("PlayerMovement no se encuentra en el objeto. Lock de rotacion no funcionara.", 2);
        if (playerAnimCtrl == null) ReportDebug("PlayerAnimCtrl no se encuentra en los hijos del objeto.", 2);
        if (playerVfxCtrl == null) ReportDebug("PlayerVfxCtrl no se encuentra en los hijos del objeto", 2);
        if (playerAudioController == null) ReportDebug("PlayerAudioController no se encuentra en el objeto.", 3);
        if (gamepadPointer == null) ReportDebug("GamepadPointer no se encuentra en la escena. La deteccion de dispositivo activo para el ataque podria fallar.", 2);
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void Start()
    {
        showGizmo = false;

        float hitRadiusStat = statsManager != null ? statsManager.GetStat(StatType.MeleeRadius) : fallbackHitRadius;
        hitRadius = hitRadiusStat;

        float attackDamageStat = statsManager != null ? statsManager.GetStat(StatType.MeleeAttackDamage) : fallbackAttackDamage;
        attackDamage = attackDamageStat;

        float attackSpeedStat = statsManager != null ? statsManager.GetStat(StatType.MeleeAttackSpeed) : fallbackAttackSpeed;
        attackSpeed = attackSpeedStat;

        float damageMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackDamage) : 1f;
        damageMultiplier = damageMultiplierStat;

        float speedMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackSpeed) : 1f;
        speedMultiplier = speedMultiplierStat;

        CalculateStats();

        UpdateComboDisplacementFromStats();
    }

    private void Update()
    {
        if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;

        if (!isAttacking && Time.time - lastAttackTime > comboResetTime) comboCount = 0;

        //if (Input.GetMouseButtonDown(0) && attackCooldown <= 0f && !isAttacking)
        //{
        //    if (!CanAttack()) return;

        //    lastAttackTime = Time.time;
        //    StartCoroutine(AttackSequence(comboCount));
        //    comboCount = (comboCount + 1) % 3; // Ciclo: 0 -> 1 -> 2 -> 0
        //}
    }

    private void OnDestroy()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;

        if (cleanupCoroutine != null)
        {
            StopCoroutine(cleanupCoroutine);
            cleanupCoroutine = null;
        }

        StopAllCoroutines();
    }

    #endregion

    #region Initialization & Stats Synchronization

    // Sincroniza la distancia de empuje del combo con las estadisticas actuales del jugador.
    private void UpdateComboDisplacementFromStats()
    {
        float displacementMod = statsManager != null ? statsManager.GetStat(StatType.MeleeComboDisplacement) : 0f;

        if (displacementMod == 0f) displacementMod = 1f;

        _hasMeleeDisplacement = displacementMod > 1f;

        for (int i = 0; i < baseComboMovementForces.Length; i++)
        {
            currentComboMovementForces[i] = baseComboMovementForces[i] * displacementMod;
        }

        ReportDebug($"Desplazamiento de combo actualizado: Multiplicador x{displacementMod} -> [{currentComboMovementForces[0]:F2}, {currentComboMovementForces[1]:F2}, {currentComboMovementForces[2]:F2}]", 1);
    }

    /// <summary>
    /// Actualiza las estadisticas internas cuando hay un cambio en el StatsManager.
    /// </summary>
    /// <param name="statType">El tipo de estadistica que ha sido modificada.</param>
    /// <param name="newValue">El nuevo valor de la estadistica modificada.</param>
    private void HandleStatChanged(StatType statType, float newValue)
    {
        switch (statType)
        {
            case StatType.AttackDamage:
                damageMultiplier = newValue;
                break;
            case StatType.AttackSpeed:
                speedMultiplier = newValue;
                break;
            case StatType.MeleeAttackDamage:
                attackDamage = newValue;
                break;
            case StatType.MeleeAttackSpeed:
                attackSpeed = newValue;
                break;
            case StatType.MeleeRadius:
                hitRadius = newValue;
                break;
            case StatType.MeleeComboDisplacement:
                UpdateComboDisplacementFromStats();
                break;
            default:
                return;
        }

        CalculateStats();
        ReportDebug($"Estadistica {statType} actualizada a {newValue}.", 1);
    }

    // Calcula los valores finales de danio y velocidad de ataque aplicando los multiplicadores.
    private void CalculateStats()
    {
        finalAttackDamage = attackDamage * damageMultiplier;
        finalAttackDamage = Mathf.Max(1, finalAttackDamage);

        finalAttackSpeed = attackSpeed * speedMultiplier;
        finalAttackSpeed = Mathf.Max(0.1f, finalAttackSpeed);

        currentSpeedFactor = finalAttackSpeed / baseSpeedReference;
    }

    #endregion

    #region Combat Flow & Core Logic

    /// <summary>
    /// Reinicia el combo a índice 0.
    /// </summary>
    public void ResetCombo()
    {
        comboCount = 0;
        lastAttackTime = 0f;
    }

    /// <summary>
    /// Verifica si el jugador cumple las condiciones necesarias para realizar un ataque (no atacando actualmente, escudo disponible y no arrojado).
    /// </summary>
    public bool CanAttack()
    {
        if (isAttacking) return false;
        if (playerShieldController != null)
        {
            if (!playerShieldController.HasShield)
            {
                ReportDebug("No se puede atacar: escudo no disponible.", 1);
                return false;
            }

            if (playerShieldController.IsThrowingShield)
            {
                ReportDebug("No se puede atacar: escudo esta siendo lanzado.", 1);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Interrumpe el ataque en curso, reiniciando los combos, la deteccion de enemigos y devolviendo el control al jugador.
    /// </summary>
    public void CancelAttack()
    {
        if (!isAttacking) return;

        StopAllCoroutines();

        isAttacking = false;

        playerVfxCtrl?.StopMeleeDisplacementffect();

        // Resetear combo para que no guarde el estado intermedio
        comboCount = 0;
        lastAttackTime = 0; // Forzar reset de tiempo

        // Limpiar listas de impacto
        hitEnemiesThisCombo.Clear();

        // Limpieza visual y de movimiento
        if (playerMovement != null)
        {
            playerMovement.StopForcedMovement();
            playerMovement.UnlockFacing();
            playerMovement.SetCanMove(true);
        }

        if (playerAnimCtrl != null)
        {
            playerAnimCtrl.damageActive = false;
        }

        // Ocultar gizmos de debug si se quedaron encendidos
        if (visualBoxHit != null) visualBoxHit.SetActive(false);
        if (visualSphereHit != null) visualSphereHit.SetActive(false);

        ReportDebug("Ataque cancelado por bloqueo.", 1);
    }

    /// <summary>
    /// Metodo publico llamado por PlayerCombatActionManager para iniciar y ejecutar el ataque de combo.
    /// </summary>
    public IEnumerator ExecuteAttackFromManager()
    {
        if (!CanAttack()) yield break;

        yield return StartCoroutine(AttackSequence(comboCount));
        lastAttackTime = Time.time;
        comboCount = (comboCount + 1) % 3; // Ciclo: 0 -> 1 -> 2 -> 0
    }

    /// <summary>
    /// Gestiona la logica secuencial de un golpe especifico dentro del combo, incluyendo apuntado, movimiento y ejecucion.
    /// </summary>
    /// <param name="attackIndex">El indice del ataque en el combo (0 para el primero, 1 para el segundo, 2 para el tercero).</param>
    private IEnumerator AttackSequence(int attackIndex)
    {
        currentAttackIndex = attackIndex;

        isAttacking = true;

        OnAttacked?.Invoke(true);

        if (playerMovement != null) playerMovement.SetCanMove(false);

        hitEnemiesThisCombo.Clear();

        Vector3 targetDir;
        bool enemyFound = TryGetNearestEnemyDirection(out Vector3 autoAimDir);

        if (enemyFound)
        {
            targetDir = autoAimDir;
        }
        else
        {
            bool isGamepadActive = gamepadPointer != null && gamepadPointer.IsGamepadMode();

            if (isGamepadActive)
            {
                targetDir = transform.forward;
            }
            else if (!TryGetMouseWorldDirection(out targetDir))
            {
                targetDir = transform.forward;
            }
        }

        if (playerMovement != null)
        {
            playerMovement.LockFacingTo8Directions(targetDir, true);
        }
        else RotateTowardsMouseInstant();

        yield return StartCoroutine(WaitForRotationLock());

        if (playerMovement != null) playerMovement.StartForcedMovement(true);

        playerAnimCtrl?.PlayMelee(currentAttackIndex + 1);

        switch (attackIndex)
        {
            case 0:
                PlayerCombatEvents.RaiseMeleeHit(transform.position, transform.forward, finalAttackDamage);
                if (playerAudioController != null)
                {
                    playerAudioController.PlayMeleeSound("BasicSlash");
                }
                yield return StartCoroutine(ExecuteAttack1());
                break;
            case 1:
                PlayerCombatEvents.RaiseMeleeHit(transform.position, transform.forward, finalAttackDamage);
                if (playerAudioController != null)
                {
                    playerAudioController.PlayMeleeSound("SpinSlash");
                }
                yield return StartCoroutine(ExecuteAttack2());
                break;
            case 2:
                PlayerCombatEvents.RaiseMeleeHit(transform.position, transform.forward, finalAttackDamage * attack3DamageMultiplier);
                if (playerAudioController != null)
                {
                    playerAudioController.PlayMeleeSound("HeavySlash");
                }
                yield return StartCoroutine(ExecuteAttack3());
                break;
        }

        if (playerMovement != null)
        {
            playerMovement.StopForcedMovement();
            playerMovement.UnlockFacing();
            playerMovement.SetCanMove(true);
        }

        isAttacking = false;

        OnAttacked?.Invoke(false);

        if (playerAnimCtrl != null) playerAnimCtrl.damageActive = false;

        hitEnemiesThisCombo.Clear();
    }

    // Metodo llamado al final de la animacion de ataque para limpiar estados residuales.
    public void OnAttackAnimationEnd()
    {
        //if (playerMovement != null)
        //{
        //    playerMovement.StopForcedMovement();
        //    playerMovement.UnlockFacing();
        //}

        //isAttacking = false;
        //if (playerAnimator != null) playerAnimator.SetBool("IsAttacking", false);

        //hitEnemiesThisCombo.Clear();
    }

    #endregion

    #region Targeting & Rotation

    /// <summary>
    /// Busca la direccion hacia la que debe orientarse el golpe.
    /// </summary>
    /// <param name="enemyDir">Vector de salida con la direccion elegida.</param>
    private bool TryGetNearestEnemyDirection(out Vector3 enemyDir)
    {
        enemyDir = Vector3.forward;

        bool isGamepadActive = gamepadPointer != null && gamepadPointer.IsGamepadMode();

        if (isGamepadActive)
        {
            Vector2 stickAim = gamepadPointer.GetAimDirectionValue();
            if (stickAim.magnitude > 0.0001f)
            {
                Camera camera = Camera.main;
                if (camera != null)
                {
                    Vector3 camForward = camera.transform.forward;
                    camForward.y = 0f;
                    camForward.Normalize();

                    Vector3 camRight = camera.transform.right;
                    camRight.y = 0f;
                    camRight.Normalize();

                    Vector3 targetDirection = camForward * stickAim.y + camRight * stickAim.x;
                    if (targetDirection.sqrMagnitude > 0.0001f)
                    {
                        enemyDir = targetDirection.normalized;
                        return true;
                    }
                }
            }
        }
        else if (TryGetMouseWorldDirection(out Vector3 mouseDir))
        {
            enemyDir = mouseDir;
            return true;
        }

        int layerMask = LayerMask.GetMask("Enemy");
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, autoAimRange, hitBuffer, layerMask);

        if (hitCount > 0)
        {
            Collider nearestEnemy = null;
            float closestDistanceSqr = Mathf.Infinity;
            Vector3 currentPos = transform.position;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;

                Vector3 dirToEnemy = col.transform.position - currentPos;
                float dSqrToTarget = dirToEnemy.sqrMagnitude;

                if (dSqrToTarget < closestDistanceSqr)
                {
                    closestDistanceSqr = dSqrToTarget;
                    nearestEnemy = col;
                }
            }

            if (nearestEnemy != null)
            {
                Vector3 direction = nearestEnemy.transform.position - currentPos;
                direction.y = 0;
                if (direction.sqrMagnitude > 0.001f)
                {
                    enemyDir = direction.normalized;
                    return true;
                }
            }
        }

        if (isGamepadActive && playerMovement != null)
        {
            Vector3 rawMoveDir = playerMovement.GetRawInputWorldDirection();
            if (rawMoveDir.sqrMagnitude > 0.0001f)
            {
                enemyDir = rawMoveDir.normalized;
                return true;
            }
        }

        if (autoAim != null && autoAim.EnableAutoAim)
        {
            bool foundTarget;
            enemyDir = autoAim.GetAimDirection(transform.position, transform.forward, null, out foundTarget);

            if (foundTarget)
            {
                ReportDebug($"Auto-aim de melee activado hacia: {autoAim.GetCurrentTarget()?.name}", 1);
                return true;
            }
        }

        return false;
    }

    // Activa o desactiva la opcion de auto-apuntado para el combate cuerpo a cuerpo.
    public void SetMeleeAutoAimEnabled(bool enabled)
    {
        if (autoAim != null)
        {
            autoAim.EnableAutoAim = enabled;
            ReportDebug($"Auto-aim de melee {(enabled ? "activado" : "desactivado")}", 1);
        }
    }

    // Retorna si la opcion de auto-apuntado se encuentra habilitada.
    public bool IsMeleeAutoAimEnabled()
    {
        return autoAim != null && autoAim.EnableAutoAim;
    }

    // Espera hasta que el jugador termine de rotar y encarar la direccion del ataque antes de golpear.
    private IEnumerator WaitForRotationLock()
    {
        float maxWait = 0f;
        float start = Time.time;
        float angleThreshold = 2f;

        while (Time.time - start < maxWait)
        {
            if (playerMovement != null)
            {
                Quaternion target = playerMovement.GetLockedRotation();
                float angle = Quaternion.Angle(transform.rotation, target);
                if (angle <= angleThreshold) break;
            }
            else
            {
                break;
            }
            yield return null;
        }

        if (playerMovement != null) playerMovement.ForceApplyLockedRotation();
    }

    // Rota instantaneamente al mouse proyectado en el plano horizontal (y = transform.position.y), con snap a 8 direcciones.
    private void RotateTowardsMouseInstant()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float snapped = Mathf.Round(angle / 45f) * 45f;
                Quaternion snappedRot = Quaternion.Euler(0f, snapped, 0f);
                transform.rotation = snappedRot;
            }
        }
    }

    // Obtiene la direccion en el plano horizontal hacia donde apunta el raton en el mundo.
    private bool TryGetMouseWorldDirection(out Vector3 outDir)
    {
        outDir = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                outDir = dir.normalized;
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Attack Moves (Coroutines)

    /// <summary>
    /// Logica de movimiento y deteccion de danio para el primer ataque del combo.
    /// </summary>
    private IEnumerator ExecuteAttack1()
    {
        float duration = attack1Duration / currentSpeedFactor;
        float elapsedTime = 0f;

        float desiredTotalDistance = currentComboMovementForces[0];
        Vector3 forward = transform.forward;

        float safeDistance = desiredTotalDistance;
        if (playerMovement != null)
        {
            if (!playerMovement.IsMovementSafeDirection(forward, desiredTotalDistance))
            {
                safeDistance = playerMovement.GetMaxSafeDistance(forward, desiredTotalDistance);
                ReportDebug($"ExecuteAttack1: fast-path fallo. safeDistance recortada a {safeDistance}", 1);
            }
        }

        if (safeDistance > 0.001f && playerMovement != null)
        {
            // Verificar que el punto final sea seguro para la capsula
            Vector3 finalPos = transform.position + forward * safeDistance;
            if (!playerMovement.IsPositionSafeForCapsule(finalPos))
            {
                safeDistance = 0f;
                ReportDebug("ExecuteAttack1: Posicion final insegura. Movimiento cancelado.", 2);
            }
        }

        Vector3 attackMoveVelocity = (forward * safeDistance) / Mathf.Max(0.0001f, duration);

        float accumulated = 0f;

        bool spawningAfterImages = _hasMeleeDisplacement && safeDistance > 0.001f;
        if (spawningAfterImages) playerVfxCtrl?.StartMeleeDisplacementEffect();

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            Vector3 frameDesired = attackMoveVelocity * Time.deltaTime;

            float frameHorMag = new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
            float remaining = Mathf.Max(0f, safeDistance - accumulated);
            if (frameHorMag > remaining)
            {
                if (remaining <= 0f) frameDesired = new Vector3(0f, frameDesired.y, 0f);
                else
                {
                    Vector3 hor = new Vector3(frameDesired.x, 0f, frameDesired.z).normalized * remaining;
                    frameDesired = new Vector3(hor.x, frameDesired.y, hor.z);
                }
            }

            if (playerMovement != null)
            {
                if (safeDistance <= 0.001f)
                {
                    frameDesired = Vector3.zero;
                    ReportDebug("ExecuteAttack1: No se aplica movimiento de carga por espacio insuficiente.", 1);
                }
                else
                {
                    playerMovement.MoveCharacter(frameDesired);
                    accumulated += new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
                }
            }
            else
            {
                transform.position += frameDesired;
                accumulated += new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
            }

            PerformHitDetectionWithTracking();

            yield return null;
        }


        // Mantener lock
        float lockDuration = comboLockDurations[0] / currentSpeedFactor;
        attackCooldown = lockDuration;

        float scaledAttack1Duration = attack1Duration / currentSpeedFactor;
        float remainingTime = Mathf.Max(0f, lockDuration - scaledAttack1Duration);
        if (remainingTime > 0) yield return new WaitForSeconds(remainingTime);
        if (spawningAfterImages) playerVfxCtrl?.StopMeleeDisplacementffect();
    }

    // Activa los efectos visuales y de sonido para el primer ataque.
    public void ActiveAttack1Slash()
    {
    }

    /// <summary>
    /// Logica de movimiento en espiral/giro y deteccion de danio para el segundo ataque del combo.
    /// </summary>
    private IEnumerator ExecuteAttack2()
    {
        float movementDuration = Mathf.Max(0f, attack2MovementDuration / currentSpeedFactor);
        float spinDuration = Mathf.Max(0f, attack2SpinDuration / currentSpeedFactor);

        float desiredTotalDistance = currentComboMovementForces[1];
        Vector3 forward = transform.forward;

        float safeDistance = desiredTotalDistance;
        if (playerMovement != null)
        {
            if (!playerMovement.IsMovementSafeDirection(forward, desiredTotalDistance))
            {
                safeDistance = playerMovement.GetMaxSafeDistance(forward, desiredTotalDistance);
                ReportDebug($"ExecuteAttack2: fast-path fallo. safeDistance recortada a {safeDistance}", 1);
            }
        }

        if (safeDistance > 0.001f && playerMovement != null)
        {
            // Verificar que el punto final sea seguro para la capsula
            Vector3 finalPos = transform.position + forward * safeDistance;
            if (!playerMovement.IsPositionSafeForCapsule(finalPos))
            {
                safeDistance = 0f;
                ReportDebug("ExecuteAttack2: Posicion final insegura. Movimiento cancelado.", 2);
            }
        }

        float elapsedTime = 0f;
        Vector3 attackMoveVelocity = (forward * safeDistance) / Mathf.Max(0.0001f, movementDuration);
        float accumulated = 0f;

        bool spawningAfterImages = _hasMeleeDisplacement && safeDistance > 0.001f;
        if (spawningAfterImages) playerVfxCtrl?.StartMeleeDisplacementEffect();

        while (elapsedTime < movementDuration)
        {
            elapsedTime += Time.deltaTime;
            Vector3 frameDesired = attackMoveVelocity * Time.deltaTime;

            float frameHorMag = new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
            float remaining = Mathf.Max(0f, safeDistance - accumulated);
            if (frameHorMag > remaining)
            {
                if (remaining <= 0f) frameDesired = new Vector3(0f, frameDesired.y, 0f);
                else
                {
                    Vector3 hor = new Vector3(frameDesired.x, 0f, frameDesired.z).normalized * remaining;
                    frameDesired = new Vector3(hor.x, frameDesired.y, hor.z);
                }
            }

            if (playerMovement != null)
            {
                if (safeDistance <= 0.001f)
                {
                    frameDesired = Vector3.zero;
                    ReportDebug("ExecuteAttack2: No se aplica movimiento de carga por espacio insuficiente.", 1);
                }
                else
                {
                    playerMovement.MoveCharacter(frameDesired);
                    accumulated += new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
                }
            }
            else
            {
                transform.position += frameDesired;
                accumulated += new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
            }

            yield return null;
        }


        if (playerMovement != null)
        {
            playerMovement.UnlockFacing();
        }

        if (spinDuration > 0f && Mathf.Abs(attack2TargetSpinAngle) > 0.0001f)
        {
            try
            {
                if (playerMovement != null) playerMovement.IsRotationExternallyControlled = true;

                float targetAngle = Mathf.Abs(attack2TargetSpinAngle);
                float sign = Mathf.Sign(attack2TargetSpinAngle);

                // velocidad angular necesaria para cubrir targetAngle en spinDuration
                float requiredAngularSpeed = targetAngle / spinDuration; // grados por segundo

                float maxSpeed = Mathf.Abs(attack2SpinSpeed) * currentSpeedFactor;
                float angularSpeed = Mathf.Min(requiredAngularSpeed, maxSpeed);

                float rotated = 0f;
                float elapsedSpin = 0f;

                while (elapsedSpin < spinDuration && rotated < targetAngle - 0.0001f)
                {
                    float dt = Time.deltaTime;
                    elapsedSpin += dt;

                    float angleThisFrame = angularSpeed * dt;
                    float remaining = targetAngle - rotated;
                    if (angleThisFrame > remaining) angleThisFrame = remaining;

                    transform.Rotate(0f, sign * angleThisFrame, 0f, Space.Self);
                    rotated += angleThisFrame;

                    PerformHitDetectionWithTracking();

                    yield return null;
                }

                // correccion final por errores numericos
                if (Mathf.Abs(rotated - targetAngle) > 0.01f)
                {
                    float correction = (targetAngle - rotated) * sign;
                    transform.Rotate(0f, correction, 0f, Space.Self);
                    rotated = targetAngle;
                }
            }
            finally
            {
                if (playerMovement != null)
                {
                    playerMovement.IsRotationExternallyControlled = false;
                }
            }
        }
        else
        {
            ReportDebug("ExecuteAttack2: spinDuration o targetSpinAngle invalidos, se omite giro.", 1);
        }


        float lockDuration = comboLockDurations[1] / currentSpeedFactor;
        attackCooldown = lockDuration;

        float totalAttackDuration = movementDuration + spinDuration;

        float remainingTime = Mathf.Max(0f, lockDuration - totalAttackDuration);
        if (remainingTime > 0) yield return new WaitForSeconds(remainingTime);
        if (spawningAfterImages) playerVfxCtrl?.StopMeleeDisplacementffect();
    }

    // Activa los efectos visuales y de sonido para el segundo ataque (giro).
    public void ActiveAttack2Slash()
    {
    }

    /// <summary>
    /// Logica de carga y ejecucion final pesada para el tercer ataque del combo.
    /// </summary>
    private IEnumerator ExecuteAttack3()
    {
        float preChargeDuration = attack3PreChargeDuration / currentSpeedFactor;
        float chargeDuration = attack3ChargeDuration / currentSpeedFactor;
        float preChargeElapsed = 0f;

        while (preChargeElapsed < preChargeDuration)
        {
            preChargeElapsed += Time.deltaTime;
            float spinAmount = (attack3SpinSpeed * currentSpeedFactor) * Time.deltaTime;
            transform.Rotate(0f, spinAmount, 0f, Space.Self);
            yield return null;
        }

        float desiredTotalDistance = currentComboMovementForces[2];
        Vector3 forward = transform.forward;

        float safeDistance = desiredTotalDistance;
        if (playerMovement != null)
        {
            if (!playerMovement.IsMovementSafeDirection(forward, desiredTotalDistance))
            {
                safeDistance = playerMovement.GetMaxSafeDistance(forward, desiredTotalDistance);
                ReportDebug($"ExecuteAttack3: fast-path fallo. safeDistance recortada a {safeDistance}", 1);
            }
        }

        if (safeDistance > 0.001f && playerMovement != null)
        {
            // Verificar que el punto final sea seguro para la capsula
            Vector3 finalPos = transform.position + forward * safeDistance;
            if (!playerMovement.IsPositionSafeForCapsule(finalPos))
            {
                safeDistance = 0f;
                ReportDebug("ExecuteAttack3: Posicion final insegura. Movimiento cancelado.", 2);
            }
        }

        float chargeElapsed = 0f;
        Vector3 attackMoveVelocity = (forward * safeDistance) / Mathf.Max(0.0001f, chargeDuration);
        float accumulated = 0f;

        bool spawningAfterImages = _hasMeleeDisplacement && safeDistance > 0.001f;
        if (spawningAfterImages) playerVfxCtrl?.StartMeleeDisplacementEffect();

        while (chargeElapsed < chargeDuration)
        {
            chargeElapsed += Time.deltaTime;
            Vector3 frameDesired = attackMoveVelocity * Time.deltaTime;

            float frameHorMag = new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
            float remaining = Mathf.Max(0f, safeDistance - accumulated);
            if (frameHorMag > remaining)
            {
                if (remaining <= 0f) frameDesired = new Vector3(0f, frameDesired.y, 0f);
                else
                {
                    Vector3 hor = new Vector3(frameDesired.x, 0f, frameDesired.z).normalized * remaining;
                    frameDesired = new Vector3(hor.x, frameDesired.y, hor.z);
                }
            }

            if (playerMovement != null)
            {
                if (safeDistance <= 0.001f)
                {
                    frameDesired = Vector3.zero;
                    ReportDebug("ExecuteAttack3: No se aplica movimiento de carga por espacio insuficiente.", 1);
                }
                else
                {
                    playerMovement.MoveCharacter(frameDesired);
                    accumulated += new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
                }
            }
            else
            {
                transform.position += frameDesired;
                accumulated += new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
            }

            PerformHitDetectionWithTracking();
            yield return null;
        }


        float lockDuration = comboLockDurations[2] / currentSpeedFactor;
        attackCooldown = lockDuration;

        float remainingTime = Mathf.Max(0f, lockDuration - (preChargeDuration + chargeDuration));
        if (remainingTime > 0f) yield return new WaitForSeconds(remainingTime);
        if (spawningAfterImages) playerVfxCtrl?.StopMeleeDisplacementffect();
    }

    // Activa los efectos visuales y de sonido para el tercer ataque (pesado).
    public void ActiveAttack3Slash()
    {
    }

    #endregion

    #region Hit Detection & Damage Application

    /// <summary>
    /// Escanea fisicamente el area designada para detectar enemigos, aplicarles danio, estatus e impacto.
    /// </summary>
    public void PerformHitDetectionWithTracking()
    {
        bool hitAnyEnemy = false;
        bool hasHitHealthThisFrame = false;
        bool hasHitToughnessThisFrame = false;

        Vector3 damageSourcePos = hitPoint != null ? hitPoint.position : transform.position;

        Collider[] hitEnemies = useBoxCollider
            ? Physics.OverlapBox(hitPoint.position, new Vector3(hitRadius, hitRadius, hitRadius), Quaternion.identity, enemyLayer)
            : Physics.OverlapSphere(hitPoint.position, hitRadius, enemyLayer);

        const DamageType damageTypeForDummy = DamageType.Melee;
        const AttackDamageType meleeDamageType = AttackDamageType.Melee;

        float currentToughnessBonus = 0f;
        if (playerShieldSkill != null && playerShieldSkill.IsActive)
        {
            currentToughnessBonus = playerShieldSkill.CurrentToughnessMultiplier;
        }

        float calculatedDamage = finalAttackDamage;
        if (currentAttackIndex == 2)
        {
            calculatedDamage *= attack3DamageMultiplier;
        }

        foreach (Collider enemy in hitEnemies)
        {
            if (enemy.TryGetComponent<GlassShardDamage>(out var shard))
            {
                shard.Shatter();
                continue;
            }

            if (hitEnemiesThisCombo.Contains(enemy))
            {
                continue;
            }

            hitEnemiesThisCombo.Add(enemy);
            hitAnyEnemy = true;

            EnemyToughness toughness = enemy.GetComponent<EnemyToughness>();
            if (toughness != null && toughness.HasToughness)
            {
                hasHitToughnessThisFrame = true;
            }
            else
            {
                hasHitHealthThisFrame = true;
            }

            if (hitAnyEnemy && playerAudioController != null) playerAudioController.PlayHitSound();

            ApplyKnockbackSafe(enemy);

            CombatEventsManager.TriggerPlayerHitEnemy(enemy.gameObject, true);

            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            Vector3 vfxPos = enemyHealth != null ? enemyHealth.ImpactVFXPosition : enemy.transform.position;
            PlayImpactVFX(vfxPos);

            bool isCritical;
            float finalDamageWithCrit = CriticalHitSystem.CalculateDamage(calculatedDamage, transform, enemy.transform, out isCritical);

            IDamageable damageable = enemy.GetComponent<IDamageable>();

            if (damageable != null)
            {
                const string TUTORIAL_DUMMY_TAG = "TutorialDummy";

                if (damageable is MonoBehaviour monoBehaviour && monoBehaviour.gameObject.CompareTag(TUTORIAL_DUMMY_TAG))
                {
                    IDamageable iDamageable = monoBehaviour.GetComponent<IDamageable>();

                    TutorialCombatDummy tutorialDummy = damageable as TutorialCombatDummy;

                    if (tutorialDummy != null)
                    {
                        tutorialDummy.TakeDamage(finalDamageWithCrit, false, meleeDamageType);
                    }
                    else if (iDamageable != null)
                    {
                        damageable.TakeDamage(Mathf.RoundToInt(finalDamageWithCrit), isCritical, meleeDamageType);
                        ReportDebug($"Golpe a {enemy.name}: DUMMY DE TUTORIAL DETECTADO (Tag). Enviando {finalDamageWithCrit:F2} de danio de {damageTypeForDummy}", 1);
                    }
                }
                else
                {
                    bool attackSuccessful = ExecuteAttack(enemy.gameObject, calculatedDamage, currentToughnessBonus);

                    ReportDebug($"Golpe a {enemy.name} por {calculatedDamage} de danio.", 1);

                    if (attackSuccessful)
                    {
                        if (enemyHealth != null)
                        {
                            float stunDelay = blinkInterval * blinkCount;
                            StartCoroutine(ApplyStunDelayed(enemyHealth, comboStunDurations[currentAttackIndex], stunDelay));

                        }
                    }
                    else
                    {
                        ReportDebug($"Efectos secundarios (Stun) cancelados en {enemy.name} porque el ataque fue bloqueado.", 1);
                    }
                }
            }

            if (enemy.gameObject.layer == LayerMask.NameToLayer("MeatPillar"))
            {
                MeatPillar meatPillar = enemy.GetComponent<MeatPillar>();
                if (meatPillar != null)
                {
                    meatPillar.TakeDamage(AttackDamageType.Melee);
                }
            }

            ExplosiveHead explosiveHead = enemy.GetComponent<ExplosiveHead>();
            if (explosiveHead != null)
            {
                explosiveHead.StartPriming(true);
            }
        }

        // Ejecutar el Zoom global consolidado del frame
        if (hasHitHealthThisFrame)
        {
            CameraHitZoomFeedback.Instance?.TriggerHitZoom(false);
        }
        else if (hasHitToughnessThisFrame)
        {
            CameraHitZoomFeedback.Instance?.TriggerHitZoom(true);
        }

        float displayDuration = gizmoDuration;

        switch (currentAttackIndex)
        {
            case 0: // ataque 1
                if (attack1Duration > 0f) displayDuration = attack1Duration;
                break;

            case 1: // ataque 2
                if (attack2SpinDuration > 0f) displayDuration = attack2SpinDuration;
                break;

            case 2: // ataque 3
                if (attack3ChargeDuration > 0f) displayDuration = attack3ChargeDuration;
                break;

            default:
                // fallback
                break;
        }

        if (currentAttackIndex == 0 || currentAttackIndex == 1)
        {
            float quickMax = 0.25f;
            if (displayDuration > quickMax) displayDuration = quickMax;
        }

        if (showGizmoRoutine != null)
        {
            StopCoroutine(showGizmoRoutine);
            showGizmoRoutine = null;
        }

        showGizmoRoutine = StartCoroutine(ShowGizmoCoroutine(displayDuration));
    }

    // Corrutina que aplica el aturdimiento al enemigo de manera retrasada en base a la duracion de su animacion de hit.
    private IEnumerator ApplyStunDelayed(EnemyHealth enemyHealth, float stunDuration, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (enemyHealth != null) enemyHealth.ApplyStun(stunDuration);
    }

    /// <summary>
    /// Intenta aplicar el danio a un enemigo si este no bloquea el impacto.
    /// </summary>
    /// <param name="target">Objeto enemigo a recibir el golpe.</param>
    /// <param name="damageAmount">Cantidad final de danio a aplicar.</param>
    /// <param name="toughnessBonus">Bonus extra opcional derivado de escudos/habilidades especiales.</param>
    private bool ExecuteAttack(GameObject target, float damageAmount, float toughnessBonus)
    {
        if (target.TryGetComponent<IDamageBlocker>(out var blocker)
            && target.TryGetComponent<EnemyHealth>(out var healthB))
        {
            if (blocker.ShouldBlockDamage(transform.position))
            {
                ReportDebug("Ataque bloqueado por escudo frontal.", 1);
                return false;
            }

            if (toughnessBonus > 0) healthB.PrepareToughnessBonus(toughnessBonus);
            healthB.TakeDamage(damageAmount, false, AttackDamageType.Melee);
            return true;
        }
        else if (target.TryGetComponent<EnemyHealth>(out var enemyHealth))
        {
            if (toughnessBonus > 0) enemyHealth.PrepareToughnessBonus(toughnessBonus);
            enemyHealth.TakeDamage(damageAmount, false, AttackDamageType.Melee);
            return true;
        }

        return true;
    }

    // Calcula y aplica un impulso fisico de empuje (knockback) basado en la etapa de vida del jugador.
    private void ApplyKnockbackSafe(Collider enemy)
    {
        EnemyKnockbackHandler knockbackHandler = enemy.GetComponent<EnemyKnockbackHandler>();
        if (knockbackHandler == null || playerHealth == null) return;

        EnemyToughness toughness = enemy.GetComponent<EnemyToughness>();
        if (toughness != null && toughness.HasToughness) return;

        float knockbackForce = 0f;

        switch (playerHealth.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young:
                knockbackForce = knockbackYoung;
                break;
            case PlayerHealth.LifeStage.Adult:
                knockbackForce = knockbackAdult;
                break;
            case PlayerHealth.LifeStage.Elder:
                knockbackForce = knockbackElder;
                break;
        }

        if (knockbackForce > 0)
        {
            Vector3 knockbackDirection = (enemy.transform.position - transform.position).normalized;
            knockbackDirection.y = 0;

            float limitedForce = Mathf.Min(knockbackForce, knockbackMaxDistance);
            knockbackHandler.TriggerKnockback(knockbackDirection, limitedForce, 0.25f);
        }
    }

    #endregion

    #region VFX & Debugging

    /// <summary>
    /// Reproduce el efecto de impacto visual instanciandolo en la posicion exacta del enemigo atacado.
    /// </summary>
    /// <param name="position">Coordenada en el mundo 3D donde sucedio el golpe.</param>
    private void PlayImpactVFX(Vector3 position)
    {
        if (meleeImpactVFX == null) return;

        Instantiate(meleeImpactVFX, position, Quaternion.identity);
    }

    // Corrutina auxiliar que controla cuanto tiempo se muestra el gizmo de la colision al momento de golpear.
    private IEnumerator ShowGizmoCoroutine(float duration)
    {
        if (!canShowHitGizmo) yield break;

        float time = (duration > 0f) ? duration : gizmoDuration;

        showGizmo = true;
        if (useBoxCollider && visualBoxHit != null) visualBoxHit.SetActive(true);
        else if (visualSphereHit != null) visualSphereHit.SetActive(true);

        yield return new WaitForSeconds(time);

        showGizmo = false;
        if (useBoxCollider && visualBoxHit != null) visualBoxHit.SetActive(false);
        else if (visualSphereHit != null) visualSphereHit.SetActive(false);

        showGizmoRoutine = null;
    }

    private void OnDrawGizmos()
    {
        if (hitPoint == null || !showGizmo) return;

        Gizmos.color = Color.red;

        if (useBoxCollider)
        {
            Vector3 boxCenter = hitPoint.position;
            Vector3 boxHalfExtents = new Vector3(hitRadius, hitRadius, hitRadius);
            Quaternion boxRotation = Quaternion.identity;

            Gizmos.matrix = Matrix4x4.TRS(boxCenter, boxRotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2f);
        }
        else
        {
            Gizmos.DrawWireSphere(hitPoint.position, hitRadius);
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[PlayerMeleeAttack] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerMeleeAttack] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerMeleeAttack] {message}");
                break;
            default:
                Debug.Log($"[PlayerMeleeAttack] {message}");
                break;
        }
    }

    #endregion
}