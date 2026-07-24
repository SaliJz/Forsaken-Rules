using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador del enemigo Drogath, un tanque de apoyo que vincula aliados
/// otorgandoles regeneracion de superarmor mientras esten en rango.
/// Al morir libera una onda que otorga superarmor temporal.
/// </summary>
public class DrogathEnemy : MonoBehaviour, IDamageBlocker, IAnimEventHandler
{
    #region Enums And Structs

    private class BondInfo
    {
        public GameObject ally;
        public EnemyToughness toughness;
        public LineRenderer lineRenderer;
        public float currentRegen;
        public ReelAnimCtrl.Cameras? cameraSlot;
        public Transform cameraTransform;
    }

    #endregion

    #region Inspector - References

    [Header("Core References")]
    [SerializeField] private Transform hitPoint;
    [SerializeField] private bool showVisualHit = false;
    [SerializeField] private GameObject visualHit;
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private EnemyToughness enemyToughness;
    [SerializeField] private EnemyVisualEffects enemyVisualEffects;
    [SerializeField] private ReelAnimCtrl animCtrl;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private Transform playerTransform;

    [Header("Layers")]
    [SerializeField] private LayerMask allyLayers = ~0;
    [SerializeField] private LayerMask playerLayer;

    #endregion

    #region Inspector - Bond System

    [Header("Bond System")]
    [Tooltip("Radio maximo para vincular aliados")]
    [SerializeField] private float bondRadius = 15f;
    [Tooltip("Maximo de vinculos simultaneos")]
    [SerializeField] private int maxBonds = 2;
    [Tooltip("Regeneracion de superarmor por segundo para aliados vinculados")]
    [SerializeField] private float toughnessRegenPerSecond = 6f;
    [Tooltip("Maximo de superarmor que puede regenerar en un aliado")]
    [SerializeField] private float maxToughnessRegen = 30f;
    [Tooltip("Cooldown antes de poder revincular al mismo aliado")]
    [SerializeField] private float rebondCooldown = 4f;
    [Tooltip("Intervalo para chequear vinculos y regenerar superarmor")]
    [SerializeField] private float bondUpdateInterval = 0.25f;
    [Tooltip("Si esta activo, puede activar la dureza en aliados que la tengan desactivada")]
    [SerializeField] private bool canEnableToughnessOnAllies = false;

    #endregion

    #region Inspector - Shield System

    [Header("Shield System")]
    [Tooltip("Angulo frontal del escudo (en grados, desde el centro)")]
    [SerializeField] private float frontalBlockAngle = 75f;
    [SerializeField] private Transform shieldForwardOverride = null;

    #endregion

    #region Inspector - Movement

    [Header("Movement")]
    [Tooltip("Delay desde el spawn hasta que el enemigo empieza a actuar")]
    [SerializeField] private float spawnDelay = 1f;
    [Tooltip("Velocidad de movimiento hacia el jugador")]
    [SerializeField] private float moveSpeed = 3.5f;
    [Tooltip("Aceleracion del NavMeshAgent")]
    [SerializeField] private float acceleration = 8f;
    [Tooltip("Distancia de parada respecto al jugador")]
    [SerializeField] private float stoppingDistance = 2f;
    [Tooltip("Distancia para considerar que debe atacar")]
    [SerializeField] private float distanceToAttack = 2.5f;
    [Tooltip("Velocidad de giro en grados por segundo")]
    [SerializeField] private float rotationSpeed = 60f;

    #endregion

    #region Inspector - Combat

    [Header("Combat")]
    [Tooltip("Dano del ataque cuerpo a cuerpo")]
    [SerializeField] private float meleeDamage = 10f;
    [Tooltip("Intervalo entre ataques")]
    [SerializeField] private float attackInterval = 1.5f;
    [Tooltip("Rango de ataque cuerpo a cuerpo")]
    [SerializeField] private float attackRange = 2.5f;
    [Tooltip("Empuje aplicado al jugador al ser golpeado")]
    [SerializeField] private float knockbackForce = 4f;
    [Tooltip("Tiempo de espera desde que inicia la animacion hasta el impacto")]
    [SerializeField] private float attackImpactDelay = 1.25f;
    [Tooltip("Tiempo de recuperacion tras un ataque")]
    [SerializeField] private float attackRecoveryTime = 0.75f;

    #endregion

    #region Inspector - Death Effect

    [Header("Death Effect")]
    [Tooltip("Radio de la onda demoniaca al morir")]
    [SerializeField] private float deathEffectRadius = 15f;
    [Tooltip("Cantidad de superarmor otorgado al morir")]
    [SerializeField] private float deathSuperArmor = 10f;
    [Tooltip("Duracion del superarmor otorgado al morir")]
    [SerializeField] private float deathSuperArmorDuration = 5f;

    #endregion

    #region Inspector - Visual Effects

    [Header("Visual Feedback")]
    [SerializeField] private LineRenderer bondLineRendererPrefab;
    [SerializeField] private Color bondLineColor = Color.cyan;
    [SerializeField] private float bondLineWidth = 0.1f;

    [Header("VFX Ataque")]
    [SerializeField] private GameObject attackVFXPrefab;
    [SerializeField] private Transform attackVFXSpawnPoint;

    #endregion

    #region Inspector - Sound And Audio

    [Header("Core Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("SFX Ambiente y Movimiento")]
    [Tooltip("Sonido aleatorio cuando esta quieto.")]
    [SerializeField] private AudioClip idleSFX;
    [Tooltip("Sonido de pasos al moverse.")]
    [SerializeField] private AudioClip runSFX;

    [Header("SFX Combate y Dano")]
    [SerializeField] private AudioClip attackSFX;
    [SerializeField] private AudioClip blockSFX;
    [SerializeField] private AudioClip deathSFX;
    [SerializeField] private AudioClip hitStunSFX;
    [SerializeField] private AudioClip toughnessBlockSFX;

    [Header("SFX Habilidades (Vinculo)")]
    [Tooltip("Suena una vez al establecer un vinculo nuevo.")]
    [SerializeField] private AudioClip bondActivateSFX;
    [Tooltip("Suena ritmicamente mientras regenera armadura a aliados.")]
    [SerializeField] private AudioClip bondRegenSFX;
    [SerializeField] private AudioClip bondBreakSFX;

    [Header("SFX Anticipacion")]
    [SerializeField] private AudioClip anticipationSFX;
    [SerializeField] private float anticipationSFXPitch = 1.0f;

    #endregion

    #region Inspector - Telegraphed Timings

    [Header("Hit Stun y Anticipacion")]
    [SerializeField] private float hitStunDuration = 0.3f;
    [SerializeField] private float forceIdleDuration = 0.8f;
    [SerializeField] private float anticipationPauseDuration = 1f;

    #endregion

    #region Inspector - Debugging

    [Header("Debugging")]
    [SerializeField] private bool canDebug = false;

    [Header("QuickSheet Balance")]
    [SerializeField] private Enemies enemiesSheet;
    [SerializeField] private int ENEMY_ID = 19;

    #endregion

    #region Internal State

    private List<BondInfo> activeBonds = new List<BondInfo>();
    private Dictionary<GameObject, float> rebondCooldowns = new Dictionary<GameObject, float>();
    private Queue<ReelAnimCtrl.Cameras> availableCameraSlots;
    private Coroutine bondUpdateRoutine;
    private Coroutine combatRoutine;
    private Coroutine hitStunCoroutine = null;
    private Coroutine attackSequenceCoroutine = null;
    private Coroutine anticipationCoroutine = null;

    private bool hasHitPlayer = false;
    private bool isAttacking = false;
    private bool isDead = false;
    private bool isReady = false;
    private bool isPerformingAttackAnim = false;
    private bool isInHitStun = false;
    private bool isInAnticipation = false;

    private float attackTimer = 0f;
    private float idleTimer;
    private float idleInterval;
    private float stepTimer;
    private float stepRate = 0.5f;
    private float regenSoundCooldown;
    private float hitStunRecoveryCooldown = 0f;
    private float hitStunRecoveryGrace = 0.1f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyToughness == null) enemyToughness = GetComponent<EnemyToughness>();
        if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();
        if (animCtrl == null) animCtrl = GetComponentInChildren<ReelAnimCtrl>();
        if (enemyVisualEffects == null) enemyVisualEffects = GetComponent<EnemyVisualEffects>();

        availableCameraSlots = new Queue<ReelAnimCtrl.Cameras>();
        availableCameraSlots.Enqueue(ReelAnimCtrl.Cameras.right);
        availableCameraSlots.Enqueue(ReelAnimCtrl.Cameras.left);

        //LoadStatsFromSheet();

        navAgent.stoppingDistance = Mathf.Max(stoppingDistance, distanceToAttack);
        navAgent.acceleration = acceleration;
        navAgent.angularSpeed = rotationSpeed;
    }

    private void Start()
    {
        // Buscar jugador si no esta asignado
        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (playerTransform == null)
        {
            ReportDebug("Jugador no encontrado en la escena.", 2);
        }

        // Verificar que el agente este en NavMesh antes de iniciar
        if (IsNavAgentValid())
        {
            StartCoroutine(SpawnDelayRoutine());
            ReportDebug($"Drogath inicializado. Vida: {enemyHealth.MaxHealth}, Radio: {bondRadius}m", 1);
        }
        else
        {
            ReportDebug("NavMeshAgent no esta en NavMesh valido. Esperando posicionamiento...", 2);
            StartCoroutine(WaitForNavMeshPlacement());
        }

        ResetIdleTimer();
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleEnemyDeath;
            enemyHealth.OnDamaged += HandleDamageTaken;
            enemyHealth.OnToughnessHit += HandleToughnessHit;
        }
    }

    private void OnDisable()
    {
        isInHitStun = false;

        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
            enemyHealth.OnToughnessHit -= HandleToughnessHit;
        }
    }

    private void OnDestroy()
    {
        // Desuscribirse del evento
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
            enemyHealth.OnToughnessHit -= HandleToughnessHit;
        }

        // Limpiar vinculos
        ClearAllBonds();
    }

    private void Update()
    {
        if (isDead || !enabled || !isReady) return;

        if (hitStunRecoveryCooldown > 0f)
        {
            hitStunRecoveryCooldown -= Time.deltaTime;
        }

        HandleAudioLogic();
        UpdateAnimationState();
    }

    #endregion

    #region Initialization And Data Sync

    private void LoadStatsFromSheet()
    {
        if (enemiesSheet == null) return;

        foreach (var row in enemiesSheet.dataArray)
        {
            if (row.ID != ENEMY_ID) continue;

            if (enemyHealth != null)
            {
                enemyHealth.SetMaxHealth(row.Health);
            }

            if (enemyToughness != null)
            {
                if (row.Superarmor > 0)
                {
                    enemyToughness.SetUseToughness(true);
                    enemyToughness.SetMaxToughness(row.Superarmor);
                }
                else enemyToughness.SetUseToughness(false);
            }

            moveSpeed = row.Movespeed;

            if (navAgent != null)
            {
                navAgent.speed = moveSpeed;
            }

            toughnessRegenPerSecond = row.Superarmorregen;

            Debug.Log($"[Drogath] ID {ENEMY_ID} cargado. Regen: {toughnessRegenPerSecond}");
            return;
        }
    }

    private IEnumerator WaitForNavMeshPlacement()
    {
        int maxAttempts = 60; // 3 segundos maximo (60 frames a 20fps)
        int attempts = 0;

        while (attempts < maxAttempts && !IsNavAgentValid())
        {
            attempts++;
            yield return new WaitForSeconds(0.05f);
        }

        if (IsNavAgentValid())
        {
            bondUpdateRoutine = StartCoroutine(BondUpdateRoutine());
            combatRoutine = StartCoroutine(CombatRoutine());
            isReady = true;
            ReportDebug("Drogath colocado en NavMesh correctamente", 1);
        }
        else
        {
            ReportDebug("ERROR: Drogath no pudo ser colocado en NavMesh despues de varios intentos", 3);
        }
    }

    private IEnumerator SpawnDelayRoutine()
    {
        ReportDebug($"Drogath en delay de spawn ({spawnDelay}s)...", 1);
        yield return new WaitForSeconds(spawnDelay);

        bondUpdateRoutine = StartCoroutine(BondUpdateRoutine());
        combatRoutine = StartCoroutine(CombatRoutine());
        isReady = true;

        ReportDebug("Drogath listo para actuar.", 1);
    }

    #endregion

    #region Movement And NavMesh Control

    /// <summary>
    /// Verifica si el NavMeshAgent esta en condiciones validas para ser usado
    /// </summary>
    private bool IsNavAgentValid()
    {
        return navAgent != null
            && navAgent.isActiveAndEnabled
            && navAgent.isOnNavMesh;
    }

    /// <summary>
    /// Intenta detener el NavMeshAgent de forma segura
    /// </summary>
    private void SafeStopAgent()
    {
        if (!IsNavAgentValid()) return;

        try
        {
            if (!navAgent.isStopped)
            {
                navAgent.isStopped = true;
            }
        }
        catch (System.Exception e)
        {
            ReportDebug($"Error al detener NavMeshAgent: {e.Message}", 2);
        }
    }

    /// <summary>
    /// Intenta reanudar el NavMeshAgent de forma segura
    /// </summary>
    private void SafeResumeAgent()
    {
        if (!IsNavAgentValid()) return;

        try
        {
            if (navAgent.isStopped)
            {
                navAgent.isStopped = false;
            }
        }
        catch (System.Exception e)
        {
            ReportDebug($"Error al reanudar NavMeshAgent: {e.Message}", 2);
        }
    }

    /// <summary>
    /// Intenta resetear el path del NavMeshAgent de forma segura
    /// </summary>
    private void SafeResetPath()
    {
        if (!IsNavAgentValid()) return;

        try
        {
            navAgent.ResetPath();
        }
        catch (System.Exception e)
        {
            ReportDebug($"Error al resetear path: {e.Message}", 2);
        }
    }

    /// <summary>
    /// Intenta establecer un destino de forma segura
    /// </summary>
    private void SafeSetDestination(Vector3 destination)
    {
        if (!IsNavAgentValid()) return;

        try
        {
            navAgent.SetDestination(destination);
        }
        catch (System.Exception e)
        {
            ReportDebug($"Error al establecer destino: {e.Message}", 2);
        }
    }

    #endregion

    #region Animation And Audio Control

    /// <summary>
    /// Controla el parametro de movimiento en el Animator
    /// </summary>
    private void UpdateAnimationState()
    {
        if (animCtrl == null || navAgent == null) return;

        // Comprobamos si se mueve calculando la velocidad del NavMeshAgent
        bool isMoving = navAgent.velocity.sqrMagnitude > 0.1f && !navAgent.isStopped;

        animCtrl.walking = isMoving;
    }

    private void HandleAudioLogic()
    {
        if (navAgent != null && navAgent.enabled && !navAgent.isStopped && navAgent.velocity.sqrMagnitude > 0.2f)
        {
            stepTimer += Time.deltaTime;
            if (stepTimer >= stepRate)
            {
                if (audioSource != null && runSFX != null)
                {
                    audioSource.pitch = Random.Range(0.9f, 1.05f);
                    audioSource.PlayOneShot(runSFX, 0.35f);
                    audioSource.pitch = 1f;
                }
                stepTimer = 0f;
            }

            ResetIdleTimer();
        }
        else
        {
            stepTimer = stepRate;

            idleTimer += Time.deltaTime;
            if (idleTimer >= idleInterval)
            {
                if (audioSource != null && idleSFX != null)
                {
                    audioSource.PlayOneShot(idleSFX);
                }
                ResetIdleTimer();
            }
        }

        if (regenSoundCooldown > 0) regenSoundCooldown -= Time.deltaTime;
    }

    private void ResetIdleTimer()
    {
        idleTimer = 0f;
        idleInterval = Random.Range(4f, 8f);
    }

    public void HandleAnimEvents(string eventName)
    {
        switch (eventName)
        {
            case "AnimEvent_AttackHit":
                if (isDead || (enemyHealth != null && enemyHealth.IsStunned)) break;
                CheckMeleeHitbox();
                break;

            case "AnimEvent_AttackEnd":
                isPerformingAttackAnim = false;
                if (attackSequenceCoroutine != null)
                {
                    StopCoroutine(attackSequenceCoroutine);
                    attackSequenceCoroutine = null;
                }
                break;

            case "AnimEvent_AnticipationPause":
                StartAnticipationPause();
                break;
        }
    }

    #endregion

    #region Bond System Logic

    private IEnumerator BondUpdateRoutine()
    {
        while (!isDead)
        {
            UpdateRebondCooldowns();
            UpdateExistingBonds();
            TryCreateNewBonds();
            RegenerateBondedAlliesToughness();

            yield return new WaitForSeconds(bondUpdateInterval);
        }
    }

    private void UpdateRebondCooldowns()
    {
        List<GameObject> toRemove = new List<GameObject>();
        List<GameObject> keys = new List<GameObject>(rebondCooldowns.Keys);

        foreach (var key in keys)
        {
            if (key == null)
            {
                toRemove.Add(key);
                continue;
            }

            rebondCooldowns[key] -= bondUpdateInterval;
            if (rebondCooldowns[key] <= 0)
            {
                toRemove.Add(key);
            }
        }

        foreach (var key in toRemove)
        {
            rebondCooldowns.Remove(key);
            if (key != null)
            {
                ReportDebug($"Cooldown de revinculo completado para {key.name}", 1);
            }
        }
    }

    private void UpdateExistingBonds()
    {
        for (int i = activeBonds.Count - 1; i >= 0; i--)
        {
            var bond = activeBonds[i];

            // Verificar si el aliado sigue existiendo
            if (bond.ally == null)
            {
                RemoveBond(i, "Aliado destruido");
                continue;
            }

            // Verificar si el aliado murio
            var allyHealth = bond.ally.GetComponent<EnemyHealth>();
            if (allyHealth != null && allyHealth.IsDead)
            {
                RemoveBond(i, "Aliado murio");
                continue;
            }

            // Verificar distancia
            float distance = Vector3.Distance(transform.position, bond.ally.transform.position);
            if (distance > bondRadius)
            {
                RemoveBond(i, "Fuera de rango");
                continue;
            }

            // Actualizar LineRenderer
            if (bond.lineRenderer != null)
            {
                Vector3 origin = bond.cameraTransform != null
                    ? bond.cameraTransform.position
                    : transform.position + Vector3.up;

                bond.lineRenderer.SetPosition(0, origin);
                bond.lineRenderer.SetPosition(1, bond.ally.transform.position + Vector3.up);
            }
        }
    }

    private void TryCreateNewBonds()
    {
        if (activeBonds.Count >= maxBonds) return;

        // Buscar aliados en rango
        Collider[] hits = Physics.OverlapSphere(transform.position, bondRadius, allyLayers, QueryTriggerInteraction.Ignore);

        foreach (var hit in hits)
        {
            if (activeBonds.Count >= maxBonds) break;

            GameObject rootObj = hit.transform.root.gameObject;

            // No vincularse a si mismo
            if (rootObj == gameObject) continue;

            // No vincularse con otros Drogath
            if (rootObj.GetComponent<DrogathEnemy>() != null) continue;

            // No vincular si esta en cooldown
            if (rebondCooldowns.ContainsKey(rootObj)) continue;

            // No vincular si ya esta vinculado
            if (activeBonds.Exists(b => b.ally == rootObj)) continue;

            // Verificar que el aliado no este muerto
            var allyHealth = rootObj.GetComponent<EnemyHealth>();
            if (allyHealth != null && allyHealth.IsDead) continue;

            // Buscar componente EnemyToughness
            EnemyToughness toughness = rootObj.GetComponent<EnemyToughness>();
            if (toughness == null) continue;

            // Si no puede activar dureza en otros, verificar que ya este activa
            if (!canEnableToughnessOnAllies && !toughness.HasToughness)
            {
                continue;
            }

            // Si puede activar dureza, hacerlo
            if (canEnableToughnessOnAllies && !toughness.HasToughness)
            {
                toughness.SetUseToughness(true);
                ReportDebug($"Dureza activada en {rootObj.name} por vinculo de Drogath", 1);
            }

            // Crear vinculo
            CreateBond(rootObj, toughness);
        }
    }

    private void CreateBond(GameObject ally, EnemyToughness toughness)
    {
        // Tomar slot de camara si hay uno disponible
        ReelAnimCtrl.Cameras? assignedSlot = null;
        Transform slotTransform = null;
        if (animCtrl != null && availableCameraSlots.Count > 0)
        {
            assignedSlot = availableCameraSlots.Dequeue();
            slotTransform = animCtrl.GetCameraTransform(assignedSlot.Value);
            animCtrl.SetTarget(assignedSlot.Value, ally.transform);
        }

        BondInfo bond = new BondInfo
        {
            ally = ally,
            toughness = toughness,
            currentRegen = 0f,
            cameraSlot = assignedSlot,
            cameraTransform = slotTransform
        };

        // Crear LineRenderer visual
        if (bondLineRendererPrefab != null)
        {
            LineRenderer lr = Instantiate(bondLineRendererPrefab, transform);
            lr.positionCount = 2;
            lr.startWidth = bondLineWidth;
            lr.endWidth = bondLineWidth;
            // lr.startColor = bondLineColor;
            // lr.endColor = bondLineColor;
            lr.SetPosition(0, slotTransform != null ? slotTransform.position : transform.position + Vector3.up);
            lr.SetPosition(1, ally.transform.position + Vector3.up);
            bond.lineRenderer = lr;
        }

        activeBonds.Add(bond);

        if (animCtrl != null && activeBonds.Count > 0)
        {
            animCtrl.isConnected = true;
        }

        if (audioSource != null && bondActivateSFX != null)
        {
            audioSource.PlayOneShot(bondActivateSFX, 0.75f);
        }

        ReportDebug($"Vinculo creado con {ally.name}. Vinculos activos: {activeBonds.Count}/{maxBonds}", 1);
    }

    private void RemoveBond(int index, string reason)
    {
        if (index < 0 || index >= activeBonds.Count) return;

        var bond = activeBonds[index];

        // Anadir cooldown
        if (bond.ally != null)
        {
            rebondCooldowns[bond.ally] = rebondCooldown;
            ReportDebug($"Vinculo roto con {bond.ally.name} ({reason}). Cooldown: {rebondCooldown}s", 1);
        }

        // Destruir LineRenderer
        if (bond.lineRenderer != null)
        {
            Destroy(bond.lineRenderer.gameObject);
        }

        if (animCtrl != null && bond.cameraSlot.HasValue)
        {
            animCtrl.ClearTarget(bond.cameraSlot.Value);
            availableCameraSlots.Enqueue(bond.cameraSlot.Value);
        }

        activeBonds.RemoveAt(index);

        if (animCtrl != null && activeBonds.Count == 0)
        {
            animCtrl.isConnected = false;
        }

        if (audioSource != null && bondBreakSFX != null)
        {
            audioSource.PlayOneShot(bondBreakSFX, 0.75f);
        }
    }

    private void ClearAllBonds()
    {
        for (int i = activeBonds.Count - 1; i >= 0; i--)
        {
            var bond = activeBonds[i];
            if (bond.lineRenderer != null)
            {
                Destroy(bond.lineRenderer.gameObject);
            }
        }

        activeBonds.Clear();

        if (animCtrl != null)
        {
            animCtrl.isConnected = false;
        }
    }

    private void RegenerateBondedAlliesToughness()
    {
        // Calcular regeneracion
        float regenThisFrame = toughnessRegenPerSecond * bondUpdateInterval;
        bool anyRegenerationHappened = false;

        foreach (var bond in activeBonds)
        {
            if (bond.toughness == null || bond.ally == null) continue;

            // Verificar limite de regeneracion total
            if (bond.currentRegen + regenThisFrame > maxToughnessRegen)
            {
                regenThisFrame = maxToughnessRegen - bond.currentRegen;
            }

            if (regenThisFrame > 0)
            {
                // Calcular cuanto puede regenerar realmente
                float currentToughness = bond.toughness.CurrentToughness;
                float maxToughness = bond.toughness.MaxToughness;
                float possibleRegen = Mathf.Min(regenThisFrame, maxToughness - currentToughness);

                if (possibleRegen > 0)
                {
                    float addedToughness = bond.toughness.AddCurrentToughness(regenThisFrame);
                    if (addedToughness > 0)
                    {
                        bond.currentRegen += addedToughness;
                        anyRegenerationHappened = true;
                    }

                    if (Mathf.FloorToInt(bond.currentRegen) % 6 == 0 && bond.currentRegen > 0)
                    {
                        ReportDebug($"{bond.ally.name} regenero {bond.currentRegen:F1}/{maxToughnessRegen} de superarmor", 1);
                    }
                }
            }
        }

        if (anyRegenerationHappened && regenSoundCooldown <= 0)
        {
            if (audioSource != null && bondRegenSFX != null)
            {
                audioSource.PlayOneShot(bondRegenSFX, 0.75f);
                regenSoundCooldown = 1.0f; // Suena maximo 1 vez por segundo
            }
        }
    }

    #endregion

    #region Combat And Attack Logic

    private IEnumerator CombatRoutine()
    {
        while (!isDead)
        {
            bool shouldAttack = activeBonds.Count == 0;

            if (shouldAttack)
            {
                if (!isAttacking)
                {
                    StartAttackMode();
                }

                UpdateCombat();
            }
            else
            {
                if (isAttacking)
                {
                    StopAttackMode();
                }
            }

            yield return null;
        }
    }

    private void StartAttackMode()
    {
        isAttacking = true;
        attackTimer = 0f;

        SafeResumeAgent();

        ReportDebug("Modo ofensivo activado (sin aliados vinculados)", 1);
    }

    private void StopAttackMode()
    {
        isAttacking = false;
        attackTimer = 0f;

        SafeStopAgent();
        SafeResetPath();

        ReportDebug("Modo ofensivo desactivado (vinculos activos)", 1);
    }

    private void UpdateCombat()
    {
        if (playerTransform == null || navAgent == null) return;

        if (isPerformingAttackAnim)
        {
            SafeStopAgent(); // Frenar para dar el golpe
            return;
        }

        if (isInHitStun)
        {
            SafeStopAgent(); // Frenar mientras esta aturdido
            return;
        }

        if (isInAnticipation)
        {
            SafeStopAgent(); // Frenar mientras se prepara para atacar
            return;
        }

        // Verificar si esta aturdido
        if (enemyHealth != null && enemyHealth.IsStunned)
        {
            SafeStopAgent();
            return;
        }
        else
        {
            // Solo reanudar si esta en modo ataque
            if (isAttacking)
            {
                SafeResumeAgent();
            }
        }

        // Mover hacia el jugador solo si el agente esta valido y no detenido
        if (IsNavAgentValid() && !navAgent.isStopped)
        {
            SafeSetDestination(playerTransform.position);
        }

        // Verificar si esta en rango de ataque
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= distanceToAttack)
        {
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
            directionToPlayer.y = 0;
            if (directionToPlayer != Vector3.zero)
            {
                float maxDegreesDelta = rotationSpeed * Time.deltaTime;
                Vector3 newForward = Vector3.RotateTowards(transform.forward, directionToPlayer, maxDegreesDelta * Mathf.Deg2Rad, 0f);
                transform.forward = newForward;
            }

            attackTimer += Time.deltaTime;

            if (attackTimer >= attackInterval)
            {
                hasHitPlayer = false;
                attackSequenceCoroutine = StartCoroutine(ExecuteAttackSequence());
                attackTimer = 0f;
            }
        }
    }

    private IEnumerator ExecuteAttackSequence()
    {
        isPerformingAttackAnim = true;
        hasHitPlayer = false;

        if (animCtrl != null) animCtrl.PlayAttack();

        float timeout = attackImpactDelay + attackRecoveryTime + 0.5f;
        float elapsed = 0f;

        while (isPerformingAttackAnim && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        isPerformingAttackAnim = false;
        attackSequenceCoroutine = null;
    }

    private void CheckMeleeHitbox()
    {
        if (playerTransform == null) return;

        if (audioSource != null && attackSFX != null)
        {
            audioSource.PlayOneShot(attackSFX);
        }

        Vector3 impactPos = hitPoint != null ? hitPoint.position : transform.position + transform.forward;

        Collider[] hitPlayer = Physics.OverlapSphere(impactPos, attackRange, playerLayer);

        foreach (var hit in hitPlayer)
        {
            if (hasHitPlayer) break;

            var hitTransform = hit.transform;

            // Ejecutar ataque
            ExecuteAttack(hit.gameObject, meleeDamage);

            // Aplicar empuje
            ApplyKnockback(hitTransform, knockbackForce);

            ReportDebug($"Drogath golpeo al jugador por {meleeDamage} de dano tras {attackImpactDelay}s de delay", 1);

            hasHitPlayer = true;
        }

        StartCoroutine(ShowGizmoCoroutine());
    }

    private void ExecuteAttack(GameObject target, float damageAmount)
    {
        if (target.TryGetComponent<PlayerHealth>(out var health))
        {
            if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) 
                && blockSystem.IsBlocking 
                && blockSystem.CanBlockAttack(transform.position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount);

                if (remainingDamage > 0f)
                {
                    health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                }

                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
    }

    private void ApplyKnockback(Transform target, float force)
    {
        if (target.TryGetComponent<PlayerKnockbackReceiver>(out var knockbackReceiver))
        {
            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0f;

            knockbackReceiver.ApplyKnockback(direction, force);
        }
    }

    private IEnumerator ShowGizmoCoroutine()
    {
        if (visualHit == null || hitPoint == null) yield break;
        if (!showVisualHit) yield break;

        Vector3 originalScale = visualHit.transform.localScale;

        if (visualHit != null && hitPoint != null)
        {
            visualHit.transform.localScale = Vector3.one * attackRange * 2f;
            visualHit.SetActive(true);
        }

        yield return new WaitForSeconds(0.5f);

        if (visualHit != null && hitPoint != null)
        {
            visualHit.SetActive(false);
            visualHit.transform.localScale = originalScale;
        }
    }

    #endregion

    #region Shield And Block Logic

    /// <summary>
    /// Verifica si un ataque desde una direccion dada debe ser bloqueado por el escudo.
    /// </summary>
    public bool ShouldBlockDamage(Vector3 attackerPosition)
    {
        Vector3 toAttacker = attackerPosition - transform.position;
        toAttacker.y = 0;

        if (toAttacker.sqrMagnitude < 0.1f)
        {
            ReportDebug("Atacante demasiado cerca o en misma posicion", 2);
            return false;
        }

        toAttacker.Normalize();

        // Obtener direccion frontal del escudo en plano XZ
        Vector3 shieldForward = GetShieldForward();
        shieldForward.y = 0;
        shieldForward.Normalize();

        // Calcular angulo entre el forward del escudo y la direccion hacia el atacante
        float angle = Vector3.Angle(shieldForward, toAttacker);

        if (canDebug)
        {
            Debug.DrawRay(transform.position, shieldForward * 3f, Color.green, 0.5f);
            Debug.DrawRay(transform.position, toAttacker * 2.5f, Color.red, 0.5f);
            Debug.DrawLine(transform.position, attackerPosition, Color.yellow, 0.5f);
            DrawBlockCone(shieldForward, frontalBlockAngle * 0.5f);
        }

        // El escudo bloquea si el atacante esta en el arco frontal
        bool isBlocked = angle <= (frontalBlockAngle * 0.5f);

        if (isBlocked)
        {
            if (animCtrl != null)
            {
                animCtrl.PlayInvulnerabilityVFX();
            }

            if (audioSource != null && blockSFX != null)
            {
                audioSource.PlayOneShot(blockSFX);
            }
        }

        ReportDebug($"Atacante a {angle:F1} grados del frente, Cobertura: +-{frontalBlockAngle * 0.5f} grados, {(isBlocked ? "BLOQUEADO" : "NO BLOQUEADO")}", 1);

        return isBlocked;
    }

    private Vector3 GetShieldForward()
    {
        if (shieldForwardOverride != null)
        {
            return shieldForwardOverride.forward;
        }
        return transform.forward;
    }

    #endregion

    #region Hit Stun And Anticipation Logic

    public void StartAnticipationPause()
    {
        if (isDead || isInHitStun || hitStunRecoveryCooldown > 0f) return;

        if (anticipationCoroutine != null) StopCoroutine(anticipationCoroutine);
        anticipationCoroutine = StartCoroutine(AnticipationRoutine());
    }

    protected virtual IEnumerator AnticipationRoutine()
    {
        isInAnticipation = true;

        if (animCtrl != null) animCtrl.PauseAnimation();

        if (audioSource != null && anticipationSFX != null)
        {
            audioSource.pitch = anticipationSFXPitch;
            audioSource.PlayOneShot(anticipationSFX);
            audioSource.pitch = 1f;
        }

        // Blink rojo de anticipacion
        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.PlayAnticipationBlink(anticipationPauseDuration);
        }

        // Shake de anticipacion
        if (animCtrl != null) animCtrl.PlayAnticipationShake(anticipationPauseDuration);

        yield return new WaitForSeconds(anticipationPauseDuration);

        if (animCtrl != null) animCtrl.ResumeAnimation();

        isInAnticipation = false;
        anticipationCoroutine = null;
    }

    protected void CancelAnticipation()
    {
        if (anticipationCoroutine != null)
        {
            StopCoroutine(anticipationCoroutine);
            anticipationCoroutine = null;
        }

        if (animCtrl != null) animCtrl.ResumeAnimation();
        if (animCtrl != null) animCtrl.StopAnticipationShake();
        if (enemyVisualEffects != null) enemyVisualEffects.CancelAnticipationBlink();
        isInAnticipation = false;
    }

    private void HandleDamageTaken()
    {
        if (isDead) return;

        bool hasToughness = enemyToughness != null && enemyToughness.HasToughness;

        if (hasToughness)
        {
            return;
        }

        if (hitStunCoroutine != null) StopCoroutine(hitStunCoroutine);
        hitStunCoroutine = StartCoroutine(HitStunRoutine());
    }

    protected void HandleToughnessHit()
    {
        if (enemyToughness == null || !enemyToughness.HasToughness) return;
        if (enemyHealth != null && enemyHealth.IsDead) return;

        //if (animCtrl != null) animCtrl.PlayDamage();
        if (audioSource != null && toughnessBlockSFX != null)
        {
            audioSource.PlayOneShot(toughnessBlockSFX);
        }
    }

    private IEnumerator HitStunRoutine()
    {
        isInHitStun = true;

        // Cancelar ataque en curso
        if (attackSequenceCoroutine != null)
        {
            StopCoroutine(attackSequenceCoroutine);
            attackSequenceCoroutine = null;
            isPerformingAttackAnim = false;
        }

        CancelAnticipation();

        SafeStopAgent();
        SafeResetPath();

        //if (animCtrl != null) animCtrl.PlayDamage();
        if (audioSource != null && hitStunSFX != null)
        {
            audioSource.PlayOneShot(hitStunSFX);
        }

        yield return new WaitForSeconds(hitStunDuration);
        yield return new WaitForSeconds(forceIdleDuration);

        isInHitStun = false;
        hitStunCoroutine = null;
        attackTimer = 0f;

        hitStunRecoveryCooldown = hitStunRecoveryGrace;

        if (isAttacking) SafeResumeAgent();
    }

    #endregion

    #region Death Effect Logic

    private void HandleEnemyDeath(GameObject deadEnemy)
    {
        if (isDead) return;
        isDead = true;

        CancelAnticipation();

        isInHitStun = false;
        if (hitStunCoroutine != null)
        {
            StopCoroutine(hitStunCoroutine);
            hitStunCoroutine = null;
        }
        if (attackSequenceCoroutine != null)
        {
            StopCoroutine(attackSequenceCoroutine);
            attackSequenceCoroutine = null;
        }
        isPerformingAttackAnim = false;

        if (audioSource != null && deathSFX != null)
        {
            audioSource.PlayOneShot(deathSFX);
        }

        ReportDebug("Drogath murio. Activando Armadura Demoniaca...", 1);

        SafeStopAgent();
        SafeResetPath();

        if (bondUpdateRoutine != null)
        {
            StopCoroutine(bondUpdateRoutine);
            bondUpdateRoutine = null;
        }
        if (combatRoutine != null)
        {
            StopCoroutine(combatRoutine);
            combatRoutine = null;
        }

        StopAllCoroutines();

        ClearAllBonds();

        // Aplicar efecto de muerte
        ApplyDemonicArmorEffect();

        if (animCtrl != null)
        {
            animCtrl.walking = false;
            animCtrl.PlayDeath();
        }
    }

    private void ApplyDemonicArmorEffect()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, deathEffectRadius, allyLayers, QueryTriggerInteraction.Ignore);

        int affectedCount = 0;

        foreach (var hit in hits)
        {
            GameObject rootObj = hit.transform.root.gameObject;

            if (rootObj == gameObject) continue; // Verificar que el aliado no sea el mismo

            if (rootObj.GetComponent<DrogathEnemy>() != null) continue;

            var allyHealth = rootObj.GetComponent<EnemyHealth>();
            if (allyHealth != null && allyHealth.IsDead) continue;

            EnemyToughness allyToughness = rootObj.GetComponent<EnemyToughness>();
            if (allyToughness == null) continue;

            // Activar dureza si esta permitido
            if (canEnableToughnessOnAllies && !allyToughness.HasToughness)
            {
                allyToughness.SetUseToughness(true);
            }

            if (allyToughness.HasToughness)
            {
                allyToughness.ApplyToughnessBuff(deathSuperArmor, deathSuperArmorDuration);
            }

            affectedCount++;
        }

        ReportDebug($"Armadura Demoniaca aplicada a {affectedCount} aliados (+{deathSuperArmor} superarmor por {deathSuperArmorDuration}s)", 1);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Obtiene el numero de vinculos activos actuales
    /// </summary>
    public int GetActiveBondsCount()
    {
        return activeBonds.Count;
    }

    /// <summary>
    /// Verifica si Drogath esta en modo ataque
    /// </summary>
    public bool IsInAttackMode()
    {
        return isAttacking;
    }

    /// <summary>
    /// Fuerza la rotura de todos los vinculos activos
    /// </summary>
    public void ForceBreakAllBonds()
    {
        for (int i = activeBonds.Count - 1; i >= 0; i--)
        {
            RemoveBond(i, "Forzado manualmente");
        }

        ReportDebug("Todos los vinculos fueron rotos manualmente", 2);
    }

    #endregion

    #region Logging

    private void DrawBlockCone(Vector3 forward, float halfAngle)
    {
        int segments = 20;
        float totalAngle = frontalBlockAngle;
        float segmentAngle = totalAngle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = -halfAngle + (i * segmentAngle);
            Vector3 dir = Quaternion.Euler(0, currentAngle, 0) * forward;
            Debug.DrawRay(transform.position, dir * 2f, Color.cyan, 0.5f);
        }
    }

    private void OnDrawGizmos()
    {
        if (!canDebug) return;

        Vector3 center = transform.position + transform.up * 1.875f;
        Vector3 forward = transform.forward;

#if UNITY_EDITOR
        GUIStyle labelStyle = new GUIStyle();
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontSize = 11;
        labelStyle.fontStyle = FontStyle.Bold;
#endif

        // Rangos

        // Stopping distance (verde oscuro, punteado visual)
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(center, stoppingDistance);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(center + transform.right * stoppingDistance, $"  Stop ({stoppingDistance}m)", labelStyle);
#endif

        // Distancia de ataque (amarillo)
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.35f);
        Gizmos.DrawWireSphere(center, distanceToAttack);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(center + transform.right * distanceToAttack, $"  Ataque ({distanceToAttack}m)", labelStyle);
#endif

        // Hitbox de ataque (naranja)
        Gizmos.color = new Color(1f, 0.45f, 0f, 0.35f);
        Gizmos.DrawWireSphere(center, attackRange);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(center + transform.right * attackRange, $"  Hitbox ({attackRange}m)", labelStyle);
#endif

        // Radio de vinculacion (cyan)
        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        Gizmos.DrawWireSphere(center, bondRadius);
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Gizmos.DrawWireSphere(center, bondRadius);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(center + transform.right * bondRadius, $"  Bond ({bondRadius}m)", labelStyle);
#endif

        // Radio de efecto de muerte (rojo suave)
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.12f);
        Gizmos.DrawWireSphere(center, deathEffectRadius);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(center + transform.right * deathEffectRadius, $"  Muerte ({deathEffectRadius}m)", labelStyle);
#endif

        // Escudo frontal
        Vector3 shieldOrigin = shieldForwardOverride != null
            ? shieldForwardOverride.position
            : transform.position;
        Vector3 shieldFwd = shieldForwardOverride != null
            ? shieldForwardOverride.forward
            : forward;

        float halfAngle = frontalBlockAngle / 2f;
        Vector3 rightEdge = Quaternion.Euler(0, halfAngle, 0) * shieldFwd;
        Vector3 leftEdge = Quaternion.Euler(0, -halfAngle, 0) * shieldFwd;

        Gizmos.color = new Color(0.1f, 0.5f, 1f, 0.55f);
        Gizmos.DrawLine(shieldOrigin, shieldOrigin + shieldFwd * distanceToAttack);
        Gizmos.DrawLine(shieldOrigin, shieldOrigin + rightEdge * distanceToAttack);
        Gizmos.DrawLine(shieldOrigin, shieldOrigin + leftEdge * distanceToAttack);

        // Arco del cono
        Gizmos.color = new Color(0.1f, 0.5f, 1f, 0.3f);
        int arcSegments = 24;
        Vector3 prevPoint = shieldOrigin + leftEdge * distanceToAttack;
        for (int i = 1; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * shieldFwd;
            Vector3 point = shieldOrigin + dir * distanceToAttack;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(shieldOrigin + shieldFwd * (distanceToAttack + 0.3f), $"  Escudo ({frontalBlockAngle}°)", labelStyle);
#endif

        // Estado actual
        if (Application.isPlaying)
        {
#if UNITY_EDITOR
            string stateLabel = isDead ? "MUERTO"
                              : !isReady ? "SPAWN DELAY"
                              : isAttacking ? "OFENSIVO"
                              : $"BONDS: {activeBonds.Count}/{maxBonds}";

            GUIStyle stateStyle = new GUIStyle();
            stateStyle.normal.textColor = isDead ? Color.red
                                        : !isReady ? Color.gray
                                        : isAttacking ? new Color(1f, 0.5f, 0f)
                                        : Color.cyan;
            stateStyle.fontSize = 13;
            stateStyle.fontStyle = FontStyle.Bold;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 4.5f, stateLabel, stateStyle);
#endif

            // Vinculos activos
            // Gizmos.color = bondLineColor;
            foreach (var bond in activeBonds)
            {
                if (bond.ally == null) continue;

                Vector3 from = bond.cameraTransform != null
                    ? bond.cameraTransform.position
                    : transform.position + Vector3.up;

                Gizmos.DrawLine(from, bond.ally.transform.position + Vector3.up);
                Gizmos.DrawSphere(bond.ally.transform.position + Vector3.up, 0.25f);
            }
        }
    }

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
                Debug.Log($"[DrogathEnemy] {message}");
                break;
            case 2:
                Debug.LogWarning($"[DrogathEnemy] {message}");
                break;
            case 3:
                Debug.LogError($"[DrogathEnemy] {message}");
                break;
            default:
                Debug.Log($"[DrogathEnemy] {message}");
                break;
        }
    }

    #endregion
}