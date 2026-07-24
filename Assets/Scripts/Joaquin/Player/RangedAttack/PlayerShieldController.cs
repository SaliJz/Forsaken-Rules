using UnityEngine;
using System.Collections;

/// <summary>
/// Clase que maneja el lanzamiento y recuperacion del escudo del jugador.
/// </summary>
public class PlayerShieldController : MonoBehaviour
{
    #region Enums (y Structs si los hay)

    private struct ShieldConfig
    {
        public int damage;
        public float speed;
        public float maxDistance;
        public bool canRebound;
        public int maxRebounds;
        public float reboundRadius;
        public float knockbackForce;
    }

    #endregion

    #region Inspector - References

    [Header("Referencias")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform shieldSpawnPoint;
    [SerializeField] private PlayerMeleeAttack playerMeleeAttack;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerAudioController playerAudioController;
    [SerializeField] private ShieldSkill shieldSkill;
    [SerializeField] private PlayerAnimCtrl playerAnimCtrl;
    [SerializeField] private AutoAim autoAim;

    #endregion

    #region Inspector - Stats

    [Header("Stats")]
    [Tooltip("Dano de ataque por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private int fallbackshieldDamage = 10;
    [SerializeField] private int shieldDamage = 10;

    [Tooltip("Velocidad del escudo por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackshieldSpeed = 25f;
    [SerializeField] private float shieldSpeed = 25f;

    [Tooltip("Distancia maxima del escudo por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackshieldMaxDistance = 30f;
    [SerializeField] private float shieldMaxDistance = 30f;

    [Tooltip("Cantidad maxima de rebotes del escudo por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private int fallbackshieldMaxRebounds = 1;
    [SerializeField] private int shieldMaxRebounds = 2;

    [Tooltip("Radio de rebote del escudo por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackshieldReboundRadius = 15f;
    [SerializeField] private float shieldReboundRadius = 15f;
    [SerializeField] private bool canShieldRebound = true;

    #endregion

    #region Inspector - Empuje del Escudo (compartido por las 3 etapas)

    [Header("Empuje del Escudo")]
    [Tooltip("Fuerza de empuje base del escudo. Es la misma para Young/Adult/Elder.")]
    [SerializeField] private float basePushForce = 1.5f;

    #endregion

    #region Inspector - Animacion

    [Header("Animacion")]
    [SerializeField] private float throwAnimationDuration = 0.15f;

    #endregion

    #region Internal State

    private float finalAttackDamage;
    private float finalAttackSpeed;
    private float damageMultiplier = 1f;
    private float speedMultiplier = 1f;

    private bool hasShield = true;
    private bool isThrowingShield = false;

    private float currentShieldDamage;
    private float currentShieldSpeed;

    private Shield activeShield;

    #endregion

    #region Public Properties & Events

    public bool CanShieldRebound => canShieldRebound;

    public int ShieldDamage
    {
        get { return shieldDamage; }
        set { shieldDamage = value; }
    }

    public bool HasShield
    {
        get { return hasShield; }
        set { hasShield = value; }
    }

    public bool IsThrowingShield => isThrowingShield;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        statsManager = GetComponent<PlayerStatsManager>();
        playerMeleeAttack = GetComponent<PlayerMeleeAttack>();
        playerMovement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        playerAnimCtrl = GetComponentInChildren<PlayerAnimCtrl>();
        playerAudioController = GetComponent<PlayerAudioController>();
        shieldSkill = GetComponent<ShieldSkill>();
        autoAim = GetComponent<AutoAim>();

        if (autoAim == null) ReportDebug("ShieldAutoAim no encontrado. El auto-aim no funcionara.", 2);
        if (statsManager == null) ReportDebug("StatsManager no esta asignado en PlayerShieldController. Usando valores de fallback.", 2);
        if (playerMeleeAttack == null) ReportDebug("PlayerMeleeAttack no encontrado. No se podra verificar estado de ataque melee.", 2);
        if (playerMovement == null) ReportDebug("PlayerMovement no encontrado. Lock de rotacion no funcionara.", 2);
        if (playerHealth == null) ReportDebug("PlayerHealth no encontrado. Configuracion por etapa no funcionara.", 2);
        if (playerAnimCtrl == null) ReportDebug("PlayerAnimCtrl no encontrado en hijos. Las animaciones de escudo no funcionaran.", 2);
        if (playerAudioController == null) ReportDebug("PlayerAudioController no encontrado. Los sonidos de escudo no funcionaran.", 2);
        if (shieldSkill == null) ReportDebug("ShieldSkill no encontrado. Los sonidos de escudo no funcionaran.", 2);
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
        ForceRecallShield();
    }

    private void OnDestroy()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
    }

    private void Start()
    {
        // Inicializar estadisticas del ataque a distancia desde PlayerStatsManager o usar valores de fallback
        float shieldDamageStat = statsManager != null ? statsManager.GetStat(StatType.ShieldAttackDamage) : fallbackshieldDamage;
        shieldDamage = Mathf.RoundToInt(shieldDamageStat);

        float shieldSpeedStat = statsManager != null ? statsManager.GetStat(StatType.ShieldSpeed) : fallbackshieldSpeed;
        shieldSpeed = shieldSpeedStat;

        float shieldMaxDistanceStat = statsManager != null ? statsManager.GetStat(StatType.ShieldMaxDistance) : fallbackshieldMaxDistance;
        shieldMaxDistance = shieldMaxDistanceStat;

        float shieldMaxReboundsStat = statsManager != null ? statsManager.GetStat(StatType.ShieldMaxRebounds) : fallbackshieldMaxRebounds;
        shieldMaxRebounds = Mathf.RoundToInt(shieldMaxReboundsStat);

        float shieldReboundRadiusStat = statsManager != null ? statsManager.GetStat(StatType.ShieldReboundRadius) : fallbackshieldReboundRadius;
        shieldReboundRadius = shieldReboundRadiusStat;

        // Inicializar estadisticas globales que afectan a todos los ataques desde PlayerStatsManager o usar valores fallback
        float damageMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackDamage) : 1f;
        damageMultiplier = damageMultiplierStat;

        float speedMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackSpeed) : 1f;
        speedMultiplier = speedMultiplierStat;

        if (shieldPrefab != null) shieldPrefab.SetActive(false);

        CalculateStats();
    }

    private void Update()
    {
        //if (Input.GetMouseButtonDown(1) && hasShield && !isThrowingShield)
        //{
        //    // Verificar que no este en medio de un ataque melee
        //    if (playerMeleeAttack != null && playerMeleeAttack.IsAttacking)
        //    {
        //        ReportDebug("No se puede lanzar el escudo mientras se ataca en melee.", 1);
        //        return;
        //    }

        //    StartCoroutine(ThrowShieldSequence());
        //}
    }

    #endregion

    #region Stat Management

    private void HandleStatChanged(StatType statType, float newValue)
    {
        switch (statType)
        {
            case StatType.ShieldAttackDamage:
                shieldDamage = Mathf.RoundToInt(newValue);
                break;
            case StatType.ShieldSpeed:
                shieldSpeed = newValue;
                break;
            case StatType.ShieldMaxDistance:
                shieldMaxDistance = newValue;
                break;
            case StatType.ShieldMaxRebounds:
                shieldMaxRebounds = Mathf.RoundToInt(newValue);
                break;
            case StatType.ShieldReboundRadius:
                shieldReboundRadius = newValue;
                break;
            case StatType.AttackDamage:
                damageMultiplier = newValue;
                break;
            case StatType.AttackSpeed:
                speedMultiplier = newValue;
                break;
            default:
                return;
        }

        CalculateStats();
        ReportDebug($"Estadistica {statType} actualizada a {newValue}.", 1);
    }

    // Metodo para recalcular las estadisticas del escudo
    private void CalculateStats()
    {
        finalAttackDamage = shieldDamage * damageMultiplier;
        finalAttackSpeed = shieldSpeed * speedMultiplier;

        currentShieldDamage = finalAttackDamage;
        currentShieldSpeed = finalAttackSpeed;
    }

    #endregion

    #region Shield Combat & Throw Logic

    /// <summary>
    /// Metodo publico llamado por PlayerCombatActionManager para ejecutar el lanzamiento del escudo.
    /// </summary>
    public IEnumerator ExecuteShieldThrowFromManager()
    {
        if (!hasShield || isThrowingShield) yield break;

        if (IsGameTransitioning()) yield break;

        if (playerMeleeAttack != null && playerMeleeAttack.IsAttacking)
        {
            ReportDebug("No se puede lanzar el escudo mientras se ataca en melee.", 1);
            yield break;
        }

        isThrowingShield = true;

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
            playerMovement.StopForcedMovement();
        }

        Vector3 aimDir;
        bool aimFound = TryGetAimDirection(out aimDir);

        if (!aimFound)
        {
            aimDir = transform.forward;
        }
        else
        {
            RotateTowardsAimInstant();
        }

        if (playerMovement != null)
        {
            playerMovement.LockFacingTo8Directions(aimDir, true);
            yield return StartCoroutine(WaitForRotationLock());
            playerMovement.ForceApplyLockedRotation();
        }

        playerAnimCtrl?.PlayDistanceAttack();

        ThrowShield();

        yield return new WaitForSeconds(throwAnimationDuration);

        if (playerMovement != null)
        {
            playerMovement.UnlockFacing();
            playerMovement.SetCanMove(true);
        }

        float remainingActionTime = 0.25f - throwAnimationDuration;
        if (remainingActionTime > 0) yield return new WaitForSeconds(remainingActionTime);

        isThrowingShield = false;
    }

    /// <summary>
    /// Secuencia de lanzamiento del escudo (similar a AttackSequence del melee):
    /// 1) Obtener direccion del mouse
    /// 2) Lockear rotacion snapped a 8 direcciones
    /// 3) Esperar a que la rotacion alcance el objetivo
    /// 4) Lanzar el escudo
    /// 5) Mantener bloqueo hasta que el escudo regrese
    /// </summary>
    private IEnumerator ThrowShieldSequence()
    {
        if (!hasShield || isThrowingShield) yield break;

        if (IsGameTransitioning()) yield break;

        // Verificar que no este en medio de un ataque melee
        if (playerMeleeAttack != null && playerMeleeAttack.IsAttacking)
        {
            ReportDebug("No se puede lanzar el escudo mientras se ataca en melee.", 1);
            yield break;
        }

        isThrowingShield = true;

        // Direccion objetivo
        Vector3 mouseWorldDir;
        if (!TryGetAimDirection(out mouseWorldDir))
        {
            mouseWorldDir = transform.forward;
        }
        else
        {
            // fallback: aplicar rotacion instantanea snappeada
            RotateTowardsAimInstant();
        }

        if (playerMovement != null)
        {
            playerMovement.LockFacingTo8Directions(mouseWorldDir, true);
            yield return StartCoroutine(WaitForRotationLock());
            playerMovement.ForceApplyLockedRotation();
        }

        // Ejecutar lanzamiento
        ThrowShield();

        yield return new WaitForSeconds(0.25f);

        // Desbloquear rotacion inmediatamente despues del lanzamiento
        // (el jugador puede moverse mientras el escudo vuela)
        if (playerMovement != null) playerMovement.UnlockFacing();

        isThrowingShield = false;
    }

    private IEnumerator WaitForRotationLock()
    {
        float maxWait = 0.25f;
        float start = Time.time;
        while (Time.time - start < maxWait)
        {
            if (playerMovement != null)
            {
                if (Quaternion.Angle(transform.rotation, playerMovement.GetLockedRotation()) <= 2f)
                    break;
            }
            else break;
            yield return null;
        }
    }

    // Rota instantaneamente al mouse proyectado en el plano horizontal (y = transform.position.y), con snap a 8 direcciones.
    private void RotateTowardsAimInstant()
    {
        if (!TryGetAimDirection(out Vector3 dir)) return;

        transform.rotation = Quaternion.LookRotation(dir);
    }

    /// <summary>
    /// Funcion que lanza el escudo en la direccion del mouse y lo instancia en el punto y altura del spawn point.
    /// </summary>
    private void ThrowShield()
    {
        hasShield = false;
        if (playerAnimCtrl != null) playerAnimCtrl.HasShield = false;

        bool isBerserker = shieldSkill != null && shieldSkill.IsActive;

        float toughnessBonus = 0f;
        if (isBerserker)
        {
            toughnessBonus = shieldSkill.CurrentToughnessMultiplier;
        }

        if (playerAudioController != null)
        {
            playerAudioController.PlayThrowShieldSound(isBerserker);
        }

        Vector3 direction;
        if (!TryGetAimDirection(out direction))
        {
            direction = transform.forward;
        }

        ItemEffectPool.Instance?.RegisterShieldLaunchDirection(direction);
        PlayerCombatEvents.RaiseShieldThrown(shieldSpawnPoint.position, direction, currentShieldDamage);

        GameObject shieldInstance = ShieldPooler.Instance.GetPooledObject();

        if (shieldInstance != null)
        {
            shieldInstance.transform.position = shieldSpawnPoint.position;
            shieldInstance.transform.rotation = Quaternion.LookRotation(direction);

            ShieldConfig config = GetShieldConfigForCurrentStage();

            Shield shieldScript = shieldInstance.GetComponent<Shield>();
            activeShield = shieldScript;
            shieldScript.Throw(
                this,
                direction,
                config.canRebound,
                config.maxRebounds,
                config.reboundRadius,
                config.damage,
                config.speed,
                config.maxDistance,
                config.knockbackForce,
                playerHealth.CurrentLifeStage,
                isBerserker,
                toughnessBonus
            );

            ReportDebug($"Escudo lanzado ({playerHealth.CurrentLifeStage}): Dano={config.damage}, Velocidad={config.speed}", 1);
        }
        else
        {
            hasShield = true;
            if (playerAnimCtrl != null) playerAnimCtrl.HasShield = true;
            ReportDebug("No se pudo obtener instancia del escudo del pool.", 2);
        }
    }

    public void CancelThrow()
    {
        if (!isThrowingShield) return;

        StopAllCoroutines();

        isThrowingShield = false;

        if (playerMovement != null)
        {
            playerMovement.UnlockFacing();
        }

        if (hasShield)
        {
            if (playerAnimCtrl != null)
            {
                playerAnimCtrl.HasShield = true;
            }
        }

        ReportDebug("Lanzamiento de escudo cancelado.", 1);
    }

    // El escudo llama a esta funcion cuando regresa
    public void CatchShield()
    {
        hasShield = true;

        activeShield = null;

        if (playerAnimCtrl != null)
        {
            playerAnimCtrl.HasShield = true;
        }

        bool isBerserker = shieldSkill != null && shieldSkill.IsActive;

        if (playerAudioController != null)
        {
            playerAudioController.PlayCatchShieldSound(isBerserker);
        }

        ReportDebug("Escudo recuperado.", 1);
    }

    public bool CanThrowShield()
    {
        if (IsGameTransitioning()) return false;

        return hasShield && !isThrowingShield;
    }

    /// <summary>
    /// Fuerza el regreso inmediato del escudo si estaba en el aire.
    /// </summary>
    public void ForceRecallShield()
    {
        if (isThrowingShield)
        {
            CancelThrow();
        }

        if (activeShield != null)
        {
            activeShield.ForceDeactivate();
            activeShield = null;
        }
        else if (!hasShield)
        {
            CatchShield();
        }
    }

    /// <summary>
    /// Verifica si el juego está en medio de alguna transición de sala, escena o secuencia.
    /// </summary>
    private bool IsGameTransitioning()
    {
        if (DungeonGenerator.Instance != null && DungeonGenerator.Instance.IsTransitioning) return true;
        if (SceneController.Instance != null && SceneController.Instance.IsTransitioning) return true;
        if (RoomTransitionTrigger.IsTransitioning) return true;
        if (BossIntroDirector.IsPlayingCutscene) return true;

        var transitions = UnityEngine.Object.FindObjectsByType<TransitionInteractive>
            (FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var t in transitions)
        {
            if (t.IsRunning) return true;
        }

        return false;
    }

    #endregion

    #region Aiming Logic

    private bool TryGetAimDirection(out Vector3 outDir)
    {
        outDir = transform.forward;

        bool isUsingGamepad = false;
        Vector3? manualAimDirection = null;

        if (SteamManager.Initialized && SteamInputManager.Instance != null)
        {
            Vector2 steamAim = SteamInputManager.Instance.GetAimAxis();

            if (steamAim.sqrMagnitude > 0.0001f)
            {
                Camera camera = Camera.main;
                if (camera == null) return false;

                Vector3 camForward = camera.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 camRight = camera.transform.right;
                camRight.y = 0f;
                camRight.Normalize();

                Vector3 targetDirection = camForward * steamAim.y + camRight * steamAim.x;

                if (targetDirection.sqrMagnitude > 0.0001f)
                {
                    isUsingGamepad = true;
                    manualAimDirection = targetDirection.normalized;
                }
            }
        }

        if (!manualAimDirection.HasValue &&
            GamepadPointer.Instance != null &&
            GamepadPointer.Instance.IsGamepadMode())
        {
            isUsingGamepad = true;
            Vector2 stickAim = GamepadPointer.Instance.GetAimDirectionValue();

            if (stickAim.magnitude > 0.0001f)
            {
                Camera camera = Camera.main;
                if (camera == null) return false;

                Vector3 camForward = camera.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 camRight = camera.transform.right;
                camRight.y = 0f;
                camRight.Normalize();

                Vector3 targetDirection = camForward * stickAim.y + camRight * stickAim.x;

                if (targetDirection.sqrMagnitude > 0.0001f)
                {
                    manualAimDirection = targetDirection.normalized;
                }
            }
        }

        if (!isUsingGamepad)
        {
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

            outDir = transform.forward;
            return false;
        }

        if (isUsingGamepad && autoAim != null && autoAim.EnableAutoAim)
        {
            bool foundTarget;
            outDir = autoAim.GetAimDirection(transform.position, transform.forward, manualAimDirection, out foundTarget);

            if (foundTarget)
            {
                ReportDebug($"Auto-aim activado hacia: {autoAim.GetCurrentTarget()?.name}", 1);
            }
            else if (manualAimDirection.HasValue)
            {
                outDir = manualAimDirection.Value;
            }
            else
            {
                outDir = transform.forward;
            }

            return true;
        }

        if (manualAimDirection.HasValue)
        {
            outDir = manualAimDirection.Value;
            return true;
        }

        outDir = transform.forward;
        return true;
    }

    public void SetAutoAimEnabled(bool enabled)
    {
        if (autoAim != null)
        {
            autoAim.EnableAutoAim = enabled;
            ReportDebug($"Auto-aim {(enabled ? "activado" : "desactivado")}", 1);
        }
    }

    public bool IsAutoAimEnabled()
    {
        return autoAim != null && autoAim.EnableAutoAim;
    }

    #endregion

    #region Configuration Logic

    private ShieldConfig GetShieldConfigForCurrentStage()
    {
        float currentDamageStat = statsManager != null ? statsManager.GetStat(StatType.ShieldAttackDamage) : fallbackshieldDamage;
        float currentSpeedStat = statsManager != null ? statsManager.GetStat(StatType.ShieldSpeed) : fallbackshieldSpeed;

        return new ShieldConfig
        {
            damage = Mathf.RoundToInt(currentDamageStat * damageMultiplier),
            speed = currentSpeedStat * speedMultiplier,
            maxDistance = shieldMaxDistance,
            canRebound = canShieldRebound,
            maxRebounds = shieldMaxRebounds,
            reboundRadius = shieldReboundRadius,
            knockbackForce = basePushForce
        };
    }

    // Permite cambiar si el escudo puede rebotar o no
    public void SetRebound(bool value) => canShieldRebound = value;

    #endregion

    #region Logging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Funcion de depuracion para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <param name="message">Mensaje a reportar.</param>
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[PlayerShieldController] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerShieldController] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerShieldController] {message}");
                break;
            default:
                Debug.Log($"[PlayerShieldController] {message}");
                break;
        }
    }

    #endregion
}