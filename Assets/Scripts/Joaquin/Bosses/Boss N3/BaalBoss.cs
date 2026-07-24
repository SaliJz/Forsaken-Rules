using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gestiona el comportamiento de IA, fases y ataques del jefe 3 Baal/HOST.
/// </summary>
public class BaalBoss : MonoBehaviour, IAnimEventHandler
{
    #region Enums

    private enum BossState
    {
        Idle,
        Attacking,
        Teleporting,
        Phase50,
        Dead
    }

    #endregion

    #region Inspector - References

    [Header("Core References")]
    [Tooltip("Referencia al punto exacto donde se registran o aplican los impactos fisicos.")]
    [SerializeField] private Transform hitPoint;
    [Tooltip("Referencia al componente de salud principal del jefe.")]
    [SerializeField] private EnemyHealth enemyHealth;
    [Tooltip("Referencia al agente de navegacion para el movimiento en el mapa.")]
    [SerializeField] private NavMeshAgent agent;
    [Tooltip("Referencia al controlador de animaciones especifico del jefe.")]
    [SerializeField] private TheHostAnimCtrl animController;
    [Tooltip("Referencia al transform del jugador objetivo.")]
    [SerializeField] private Transform player;
    [Tooltip("Referencia al componente de efectos visuales propio de este enemigo.")]
    [SerializeField] private EnemyVisualEffects enemyVisualEffects;

    [Header("Audio")]
    [Tooltip("Fuente de audio principal emisora de los efectos de sonido.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Clip de sonido reproducido durante la anticipacion del ataque Excepcion Fatal.")]
    [SerializeField] private AudioClip exceptionAnticipationSFX;
    [Tooltip("Clip de sonido para advertir la carga de Excepcion Fatal.")]
    [SerializeField] private AudioClip exceptionFatalAnticipationSFX;
    [Tooltip("Clip de sonido reproducido al cargar el ataque Buffer Overrun.")]
    [SerializeField] private AudioClip bufferOverrunChargeSFX;
    [Tooltip("Clip de sonido para advertir la carga de Buffer Overrun.")]
    [SerializeField] private AudioClip bufferOverrunAnticipationSFX;
    [Tooltip("Clip de sonido reproducido al impactar con el ataque Buffer Overrun.")]
    [SerializeField] private AudioClip bufferOverrunImpactSFX;
    [Tooltip("Clip de sonido reproducido al ejecutar un teletransporte.")]
    [SerializeField] private AudioClip teleportSFX;
    [Tooltip("Clip de sonido reproducido al preparar el impacto tras un teletransporte lejano.")]
    [SerializeField] private AudioClip latenciaAnticipationSFX;
    [Tooltip("Clip de sonido reproducido al recibir dano.")]
    [SerializeField] private AudioClip damagedSFX;
    [Tooltip("Clip de sonido reproducido al morir.")]
    [SerializeField] private AudioClip deathSFX;

    [Header("VFX Prefabs")]
    [Tooltip("Prefab del proyectil disparado en el ataque Excepcion Fatal.")]
    [SerializeField] private GameObject exceptionProjectilePrefab;
    [Tooltip("Prefab del area de efecto que genera el Cluster Necrotico.")]
    [SerializeField] private GameObject necroticClusterPrefab;
    [Tooltip("Prefab del enemigo menor larva invocado por varios ataques.")]
    [SerializeField] private GameObject larvaPrefab;
    [Tooltip("Prefab del charco de dano dejado por Fuga de Memoria.")]
    [SerializeField] private GameObject memoryLeakPrefab;
    [Tooltip("Prefab visual instanciado en el suelo para previsualizar el charco antes de que inflija dano.")]
    [SerializeField] private GameObject memoryLeakIndicatorPrefab;
    [Tooltip("Prefab del efecto visual instanciado al entrar y salir del teletransporte.")]
    [SerializeField] private GameObject teleportVFXPrefab;
    [Tooltip("Prefab del efecto visual instanciado tras el impacto de Buffer Overrun.")]
    [SerializeField] private GameObject bufferOverrunImpactVFXPrefab;
    [Tooltip("Prefab del indicador proyectado en el suelo para previsualizar la embestida.")]
    [SerializeField] private GameObject bufferOverrunTrailIndicatorPrefab;
    [Tooltip("Prefab del indicador proyectado en el area de impacto para dar advertencia visual.")]
    [SerializeField] private GameObject latenciaGroundIndicatorPrefab;
    [Tooltip("Prefab del nodo instanciado durante la fase de Arquitectura Distribuida.")]
    [SerializeField] private GameObject nodePurulentosPrefab;
    [Tooltip("Efecto de particulas ejecutado al cambiar a la fase del 50 por ciento de salud.")]
    [SerializeField] private ParticleSystem phaseTransitionVFX;

    #endregion

    #region Inspector - Base Statistics

    [Header("Base Stats")]
    [Tooltip("Cantidad maxima de puntos de vida del jefe.")]
    [SerializeField] private float maxHealth = 120f;
    [Tooltip("Velocidad de movimiento base al desplazarse por el NavMesh.")]
    [SerializeField] private float moveSpeed = 3.5f;

    [Header("NavMesh")]
    [Tooltip("Radio de seguridad utilizado para buscar una posicion valida en el NavMesh al instanciar enemigos.")]
    [SerializeField] private float safeSpawnRadius = 3f;

    #endregion

    #region Inspector - Habilidades y Comportamientos

    [Header("Excepcion Fatal")]
    [Tooltip("Valor de dano minimo aplicado por los proyectiles.")]
    [SerializeField] private float exceptionDamageMin = 2f;
    [Tooltip("Valor de dano maximo aplicado por los proyectiles.")]
    [SerializeField] private float exceptionDamageMax = 4f;
    [Tooltip("Velocidad de traslacion de los proyectiles en el espacio.")]
    [SerializeField] private float exceptionProjectileSpeed = 15f;
    [Tooltip("Tiempo de formacion (escalado de 0.1 a 1) antes de salir disparados.")]
    [SerializeField] private float exceptionProjectileFormationTime = 0.5f;
    [Tooltip("Distancia hacia adelante desde el centro del jefe donde se instancian los proyectiles para evitar clipping con su modelo.")]
    [SerializeField] private float exceptionProjectileSpawnOffset = 2.5f;
    [Tooltip("Distancia a partir de la cual el dano del proyectil comienza a incrementar.")]
    [SerializeField] private float exceptionScaleStartDist = 6f;
    [Tooltip("Distancia necesaria para que el proyectil alcance su valor maximo de dano.")]
    [SerializeField] private float exceptionScaleMaxDist = 20f;
    [Tooltip("Tiempo de duracion del efecto visual de anticipacion antes de lanzar los proyectiles.")]
    [SerializeField] private float exceptionAnticipationTime = 1.2f;
    [Tooltip("Tiempo previo al disparo durante el cual las flechas de direccion son visibles en el suelo.")]
    [SerializeField] private float exceptionDirPreviewTime = 0.8f;
    [Tooltip("Tiempo de pausa de la IA luego de ejecutar el ataque antes de continuar su ciclo.")]
    [SerializeField] private float exceptionShortRecovery = 1.5f;
    [Tooltip("Tiempo durante el cual el jefe pausa animaciones antes de lanzar Excepcion Fatal.")]
    [SerializeField] private float exceptionFatalAnticipationDuration = 0.5f;

    [Header("Buffer Overrun")]
    [Tooltip("Cantidad de dano infligido al impactar con la embestida.")]
    [SerializeField] private float bufferOverrunDamage = 10f;
    [Tooltip("Distancia maxima que recorre la IA al realizar la embestida.")]
    [SerializeField] private float bufferOverrunDashDistance = 7.5f;
    [Tooltip("Radio del area de efecto para evaluar si el jugador recibio impacto.")]
    [SerializeField] private float bufferOverrunAttackRange = 7.5f;
    [Tooltip("Tiempo de pausa de carga previa antes de ejecutar el movimiento de dash.")]
    [SerializeField] private float bufferOverrunChargeDuration = 1f;
    [Tooltip("Tiempo total requerido para completar la animacion y traslacion del dash.")]
    [SerializeField] private float bufferOverrunDashDuration = 1f;
    [Tooltip("Tiempo de pausa de la IA tras finalizar el impacto mientras el cluster opera.")]
    [SerializeField] private float bufferOverrunLongRecovery = 3f;
    [Tooltip("Tiempo adicional de espera luego de la recuperacion antes de reiniciar la secuencia principal.")]
    [SerializeField] private float cycleRepeatDelay = 2f;
    [Tooltip("Tiempo de espera para permitir nuevamente el ataque dinamico de Buffer Overrun.")]
    [SerializeField] private float bufferOverrunCooldown = 8.5f;
    //[Tooltip("Tiempo durante el cual el jefe pausa animaciones antes de iniciar la embestida.")]
    //[SerializeField] private float bufferOverrunAnticipationDuration = 0.6f;

    [Header("Cluster Necrotico")]
    [Tooltip("Duracion en segundos del area antes de colapsar y generar las larvas.")]
    [SerializeField] private float clusterDuration = 3f;
    [Tooltip("Cantidad de dano infligido por segundo a los objetivos dentro del area.")]
    [SerializeField] private float clusterDPS = 1f;
    [Tooltip("Radio fisico del area de efecto del cluster en el suelo.")]
    [SerializeField] private float clusterRadius = 1.5f;
    [Tooltip("Numero total de larvas instanciadas cuando finaliza la vida del cluster.")]
    [SerializeField] private int clusterLarvaCount = 2;
    [Tooltip("Porcentaje de velocidad restado al jugador al pisar el area (ej: 0.2 es un 20 por ciento menos).")]
    [SerializeField] private float clusterSlowFraction = 0.2f;
    [Tooltip("Determina si el cluster aplica dano continuo o si unicamente ralentiza al objetivo.")]
    [SerializeField] private bool clusterDealDamage = true;

    [Header("Desfragmentacion Evasiva")]
    [Tooltip("Cantidad de impactos requeridos para detonar el teletransporte defensivo.")]
    [SerializeField] private int defragHitThreshold = 3;
    [Tooltip("Tiempo maximo permitido entre impactos para que sumen al contador de activacion.")]
    [SerializeField] private float defragHitWindow = 2f;
    [Tooltip("Distancia maxima del jugador permitida para contabilizar sus impactos cuerpo a cuerpo.")]
    [SerializeField] private float defragActivationRange = 3f;
    [Tooltip("Distancia lineal utilizada para alejar al jefe del jugador tras el teletransporte.")]
    [SerializeField] private float defragTeleportDist = 7f;
    [Tooltip("Tiempo de espera necesario antes de permitir que la habilidad se active nuevamente.")]
    [SerializeField] private float defragCooldown = 6f;

    [Header("Fuga de Memoria")]
    [Tooltip("Duracion en segundos del area de daño en el suelo.")]
    [SerializeField] private float memoryLeakDuration = 4f;
    [Tooltip("Cantidad de dano aplicado por segundo a entidades sobre el charco.")]
    [SerializeField] private float memoryLeakDPS = 2f;
    [Tooltip("Radio que ocupa el charco toxico instanciado.")]
    [SerializeField] private float memoryLeakRadius = 2f;
    [Tooltip("Tiempo de demora tras completar un teletransporte antes de que aparezca el charco.")]
    [SerializeField] private float memoryLeakFormationDelay = 0.35f;

    [Header("Latencia Cero")]
    [Tooltip("Tiempo en segundos que el jugador debe permanecer lejos para forzar este comportamiento.")]
    [SerializeField] private float latenciaActivationTime = 10f;
    [Tooltip("Distancia limite a partir de la cual se considera al jugador fuera de rango.")]
    [SerializeField] private float latenciaActivationRange = 6f;
    //[Tooltip("Dano infligido por el impacto de esta habilidad especifica.")]
    //[SerializeField] private float latenciaDamage = 5f;
    [Tooltip("Tiempo de recarga antes de permitir otro castigador por distancia.")]
    [SerializeField] private float latenciaCooldown = 8f;
    [Tooltip("Tiempo de inmovilidad del jefe tras teletransportarse y antes de efectuar el dano.")]
    [SerializeField] private float latenciaAnticipationDuration = 0.6f;

    [Header("Arquitectura Distribuida (50 Porciento HP)")]
    [Tooltip("Radio de separacion entre el centro de la sala y los nodos defensivos creados.")]
    [SerializeField] private float nodeSpawnRadius = 5f;
    [Tooltip("Cantidad maxima de nodos defensivos instanciados al mismo tiempo.")]
    [SerializeField] private int maxNodes = 3;
    [Tooltip("Porcentaje de vulnerabilidad restaurado al jefe por cada nodo destruido por el jugador.")]
    [SerializeField] private float shieldReductionPerNode = 0.33f;

    #endregion

    #region Inspector - Debug

    [Header("Debug")]
    [Tooltip("Activa la renderizacion visual en el editor de zonas de rango y estados en pantalla.")]
    [SerializeField] private bool showDebugGUI = false;

    #endregion

    #region Internal State

    private const string ANIM_EVENT_ANTICIPATION_PAUSE = "AnimEvent_AnticipationPause";

    private BossState currentState = BossState.Idle;
    private float currentHealth;
    private bool phase50Triggered = false;

    private float effectiveMoveSpeed;
    private float effectiveProjSpeed;
    private float effectiveBufferCooldown;
    private float effectiveExceptionRecovery;

    private bool bufferOverrunOnCooldown = false;

    private int defragHitCount = 0;
    private float defragLastHitTime = -999f;
    private bool defragOnCooldown = false;
    private bool defragInterruptPending = false;

    private float timeOutOfRange = 0f;
    private bool latenciaOnCooldown = false;
    private bool latenciaInterruptPending = false;

    private Vector3 roomCenter;
    private bool architecturePhaseActive = false;
    private int activeNodeCount = 0;
    private float currentShieldReduction = 0f;

    private readonly List<GameObject> spawnedNodes = new List<GameObject>();
    private readonly List<GameObject> instantiatedEffects = new List<GameObject>();

    private bool isInAnticipation = false;
    private Coroutine anticipationCoroutine = null;

    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeComponents();
        InitializeEffectiveStats();
    }

    private void Start()
    {
        StartCoroutine(BossFlowSequence());
        StartCoroutine(DynamicBehaviorMonitor());
    }

    private void OnEnable()
    {
        if (enemyHealth == null) return;
        enemyHealth.OnDeath += HandleEnemyDeath;
        enemyHealth.OnHealthChanged += HandleHealthChanged;
        enemyHealth.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (enemyHealth == null) return;
        enemyHealth.OnDeath -= HandleEnemyDeath;
        enemyHealth.OnHealthChanged -= HandleHealthChanged;
        enemyHealth.OnDamaged -= HandleDamaged;
    }

    #endregion

    #region Initialization & Data Sync

    private void InitializeComponents()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null) enemyHealth.SetMaxHealth(maxHealth);
        currentHealth = maxHealth;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = moveSpeed;

        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                playerHealth = playerObj.GetComponent<PlayerHealth>();
                playerMovement = playerObj.GetComponent<PlayerMovement>();
            }
        }

        if (player == null) ReportDebug("Jugador no encontrado en la escena.", 3);
    }

    private void InitializeEffectiveStats()
    {
        effectiveMoveSpeed = moveSpeed;
        effectiveProjSpeed = exceptionProjectileSpeed;
        effectiveBufferCooldown = bufferOverrunCooldown;
        effectiveExceptionRecovery = exceptionShortRecovery;
    }

    #endregion

    #region Core Health & Combat

    private void HandleHealthChanged(float newCurrent, float newMax)
    {
        currentHealth = newCurrent;
        maxHealth = newMax;

        if (audioSource != null && damagedSFX != null)
        {
            audioSource.PlayOneShot(damagedSFX, 0.75f);
        }
    }

    private void HandleDamaged()
    {
        if (player == null || defragOnCooldown || defragInterruptPending) return;

        float sqrDist = (transform.position - player.position).sqrMagnitude;
        if (sqrDist > defragActivationRange * defragActivationRange) return;

        float now = Time.time;
        if (now - defragLastHitTime > defragHitWindow) defragHitCount = 0;

        defragHitCount++;
        defragLastHitTime = now;

        if (defragHitCount >= defragHitThreshold)
        {
            defragInterruptPending = true;
            defragHitCount = 0;
            ReportDebug("DESFRAGMENTACION: Umbral de agresion alcanzado. Interrupcion pendiente.", 1);
        }
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        currentState = BossState.Dead;

        CancelAnticipation();
        StopAllCoroutines();
        CleanUpEffects();

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        this.enabled = false;
    }

    #endregion

    #region AI Flow & Sequences

    private IEnumerator BossFlowSequence()
    {
        yield return new WaitForSeconds(1.5f);

        while (currentHealth > 0)
        {
            while (enemyHealth != null && enemyHealth.IsStunned)
            {
                StopAgent();
                yield return null;
            }

            if (!phase50Triggered && currentHealth <= maxHealth * 0.5f)
            {
                phase50Triggered = true;
                yield return StartCoroutine(ExecuteArquitecturaDistribuida());
                if (currentHealth <= 0) yield break;
            }

            if (HasPendingInterrupt())
            {
                yield return StartCoroutine(ResolveDynamicInterrupt());
                continue;
            }

            yield return StartCoroutine(ExecuteExcepcionFatal());
            if (currentHealth <= 0) yield break;
            if (HasPendingInterrupt()) { yield return StartCoroutine(ResolveDynamicInterrupt()); continue; }

            yield return StartCoroutine(ShortRecovery());
            if (currentHealth <= 0) yield break;
            if (HasPendingInterrupt()) { yield return StartCoroutine(ResolveDynamicInterrupt()); continue; }

            if (!bufferOverrunOnCooldown)
            {
                yield return StartCoroutine(ExecuteBufferOverrun());
                if (currentHealth <= 0) yield break;
                if (HasPendingInterrupt()) { yield return StartCoroutine(ResolveDynamicInterrupt()); continue; }

                yield return StartCoroutine(LongRecovery());
                if (currentHealth <= 0) yield break;
                if (HasPendingInterrupt()) { yield return StartCoroutine(ResolveDynamicInterrupt()); continue; }
            }
        }
    }

    private bool HasPendingInterrupt() => defragInterruptPending || latenciaInterruptPending;

    private IEnumerator ResolveDynamicInterrupt()
    {
        if (defragInterruptPending)
        {
            defragInterruptPending = false;
            yield return StartCoroutine(ExecuteDesfragmentacion());
        }
        else if (latenciaInterruptPending)
        {
            latenciaInterruptPending = false;
            yield return StartCoroutine(ExecuteLatenciaCero());
        }
    }

    private IEnumerator DynamicBehaviorMonitor()
    {
        float sqrActivationRange = latenciaActivationRange * latenciaActivationRange;

        while (currentHealth > 0)
        {
            if (currentState != BossState.Dead && player != null && !latenciaOnCooldown)
            {
                float sqrDist = (transform.position - player.position).sqrMagnitude;

                if (sqrDist > sqrActivationRange)
                {
                    timeOutOfRange += Time.deltaTime;
                    if (timeOutOfRange >= latenciaActivationTime && !latenciaInterruptPending)
                    {
                        latenciaInterruptPending = true;
                        timeOutOfRange = 0f;
                        ReportDebug("LATENCIA CERO: Jugador fuera de rango > 10 s. Interrupcion pendiente.", 1);
                    }
                }
                else
                {
                    timeOutOfRange = 0f;
                }
            }

            yield return null;
        }
    }

    #endregion

    #region Habilidades Ofensivas Base

    private IEnumerator ExecuteExcepcionFatal()
    {
        ReportDebug("EXCEPCION FATAL: Iniciando.", 1);
        currentState = BossState.Attacking;

        StopAgent();
        FacePlayer();

        if (audioSource != null && exceptionAnticipationSFX != null)
        {
            audioSource.PlayOneShot(exceptionAnticipationSFX);
        }

        if (animController != null) animController.PlayShotAttack();

        float anticipationFallbackTimer = 0f;
        while (!isInAnticipation && anticipationFallbackTimer < exceptionFatalAnticipationDuration + 1f)
        {
            anticipationFallbackTimer += Time.deltaTime;
            yield return null;
        }
        yield return new WaitUntil(() => !isInAnticipation);

        float glowOnlyTime = Mathf.Max(0f, exceptionAnticipationTime - exceptionDirPreviewTime);
        yield return new WaitForSeconds(glowOnlyTime);

        ReportDebug("EXCEPCION FATAL: Indicadores de direccion proyectados.", 1);
        yield return new WaitForSeconds(exceptionDirPreviewTime);

        yield return StartCoroutine(FireEightDirectionProjectilesRoutine());

        ReportDebug("EXCEPCION FATAL: 8 proyectiles formados y disparados.", 1);
        currentState = BossState.Idle;
    }

    private IEnumerator FireEightDirectionProjectilesRoutine()
    {
        Vector3[] dirs = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            dirs[i] = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
        }
        yield return StartCoroutine(SpawnAndFireProjectiles(dirs));
    }

    /// <summary>
    /// Lógica Unificada: Instancia con offset, Escala progresivamente desde 0.1 hasta 1.0, y luego Dispara.
    /// </summary>
    private IEnumerator SpawnAndFireProjectiles(Vector3[] directions)
    {
        if (exceptionProjectilePrefab == null) yield break;

        List<GameObject> projs = new List<GameObject>();
        Vector3 originalScale = exceptionProjectilePrefab.transform.localScale;

        // Instanciar en tamaño pequeño con el offset direccional
        foreach (Vector3 dir in directions)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f + (dir.normalized * exceptionProjectileSpawnOffset);

            GameObject proj = Instantiate(exceptionProjectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            proj.transform.localScale = originalScale * 0.1f;
            instantiatedEffects.Add(proj);
            projs.Add(proj);
        }

        // Animar crecimiento durante la formación
        float elapsed = 0f;
        while (elapsed < exceptionProjectileFormationTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / exceptionProjectileFormationTime;
            Vector3 currentScale = Vector3.Lerp(originalScale * 0.1f, originalScale, t);

            foreach (var p in projs)
            {
                if (p != null) p.transform.localScale = currentScale;
            }
            yield return null;
        }

        // Forzar el tamaño final correcto y disparar
        for (int i = 0; i < projs.Count; i++)
        {
            GameObject proj = projs[i];
            if (proj == null) continue;

            proj.transform.localScale = originalScale;

            BaalProjectile ps = proj.GetComponent<BaalProjectile>();
            if (ps != null)
            {
                ps.Initialize(directions[i], effectiveProjSpeed, exceptionDamageMin, exceptionDamageMax, exceptionScaleStartDist, exceptionScaleMaxDist, transform.position);
            }
            else
            {
                Rigidbody rb = proj.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = directions[i] * effectiveProjSpeed;
                Destroy(proj, 3f);
            }
        }
    }

    private IEnumerator ShortRecovery()
    {
        ReportDebug($"RECUPERACION CORTA: {effectiveExceptionRecovery} s.", 1);

        float elapsed = 0f;
        while (elapsed < effectiveExceptionRecovery)
        {
            FacePlayer();
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator BufferOverrunCooldownRoutine()
    {
        bufferOverrunOnCooldown = true;
        yield return new WaitForSeconds(effectiveBufferCooldown);
        bufferOverrunOnCooldown = false;
        ReportDebug("BUFFER OVERRUN: Cooldown finalizado.", 1);
    }

    private IEnumerator ExecuteBufferOverrun(bool calledByLatencia = false)
    {
        ReportDebug($"BUFFER OVERRUN: Iniciando{(calledByLatencia ? " (encadenado por Latencia Cero)" : "")}.", 1);
        currentState = BossState.Attacking;

        StopAgent();
        FacePlayer();

        if (audioSource != null && bufferOverrunChargeSFX != null)
        {
            audioSource.PlayOneShot(bufferOverrunChargeSFX);
        }

        if (animController != null) animController.PlayBufferPre();

        StartAnticipationPause();
        yield return new WaitUntil(() => !isInAnticipation);

        Vector3 dashTarget = CalculateBufferOverrunTarget();
        Vector3 dashDir = (dashTarget - transform.position).normalized;
        FaceDirection(dashDir);

        GameObject warningTrail = null;
        if (bufferOverrunTrailIndicatorPrefab != null)
        {
            float dist = Vector3.Distance(transform.position, dashTarget);
            Vector3 warningPosition = GetGroundPosition(transform.position + dashDir * (dist * 0.5f));

            warningTrail = Instantiate(bufferOverrunTrailIndicatorPrefab, warningPosition, Quaternion.LookRotation(dashDir));

            warningTrail.transform.localScale = new Vector3(5f * 2f, 0.05f, dist);
            instantiatedEffects.Add(warningTrail);
        }

        yield return new WaitForSeconds(bufferOverrunChargeDuration);

        if (warningTrail != null)
        {
            instantiatedEffects.Remove(warningTrail);
            Destroy(warningTrail);
        }

        if (animController != null) animController.PlayBufferAttack();

        yield return StartCoroutine(DashToPositionOrHit(dashTarget, bufferOverrunDashDuration));

        Vector3 impactPoint = hitPoint != null ? hitPoint.position : transform.position;
        DealAreaDamage(impactPoint, bufferOverrunAttackRange, bufferOverrunDamage);

        if (audioSource != null && bufferOverrunImpactSFX != null)
        {
            audioSource.PlayOneShot(bufferOverrunImpactSFX);
        }

        if (bufferOverrunImpactVFXPrefab != null)
        {
            GameObject vfx = Instantiate(bufferOverrunImpactVFXPrefab, impactPoint, Quaternion.identity);
            instantiatedEffects.Add(vfx);
            Destroy(vfx, 2f);
        }

        SpawnNecroticCluster(impactPoint);

        ReportDebug("BUFFER OVERRUN: Impacto aplicado. Cluster Necrotico creado.", 1);

        StartCoroutine(BufferOverrunCooldownRoutine());

        currentState = BossState.Idle;
    }

    private Vector3 CalculateBufferOverrunTarget()
    {
        Vector3 rawTarget;

        if (player == null)
        {
            rawTarget = transform.position + transform.forward * bufferOverrunDashDistance;
        }
        else
        {
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            dir.Normalize();

            rawTarget = transform.position + dir * bufferOverrunDashDistance;
        }

        if (NavMesh.SamplePosition(rawTarget, out NavMeshHit hit, bufferOverrunDashDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }

        ReportDebug("BUFFER OVERRUN: Destino del dash fuera de NavMesh, se usara posicion sin ajustar.", 2);
        return rawTarget;
    }

    private void SpawnNecroticCluster(Vector3 position)
    {
        if (necroticClusterPrefab == null)
        {
            ReportDebug("BUFFER OVERRUN: Falta necroticClusterPrefab.", 2);
            return;
        }

        Vector3 spawnPosition = position;
        NavMeshHit hit;

        if (NavMesh.SamplePosition(position, out hit, 10.0f, NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
        }
        else
        {
            ReportDebug("WARNING: No se encontro suelo NavMesh cerca de la posicion.", 1);
            return;
        }

        GameObject cluster = Instantiate(necroticClusterPrefab, spawnPosition, Quaternion.identity);
        instantiatedEffects.Add(cluster);

        NecroticCluster clusterScript = cluster.GetComponent<NecroticCluster>();
        if (clusterScript != null)
        {
            clusterScript.Initialize(
                clusterDuration,
                clusterDPS,
                clusterRadius,
                larvaPrefab,
                clusterLarvaCount,
                clusterSlowFraction,
                clusterDealDamage
                );
        }
        else Destroy(cluster, clusterDuration);
    }

    private IEnumerator LongRecovery()
    {
        ReportDebug("RECUPERACION LARGA: 3 s (colapso del cluster) + 2 s adicionales.", 1);
        yield return new WaitForSeconds(bufferOverrunLongRecovery);
        yield return new WaitForSeconds(cycleRepeatDelay);
    }

    #endregion

    #region Habilidades Dinamicas y Evasivas

    private IEnumerator ExecuteDesfragmentacion()
    {
        ReportDebug("DESFRAGMENTACION EVASIVA: Ejecutando.", 1);
        currentState = BossState.Teleporting;
        defragOnCooldown = true;

        StopAgent();

        if (animController != null)
        {
            animController.PlayTP_pre();
            yield return new WaitForSeconds(animController.TPanitipacionTime);
        }

        Vector3 evasionPos = GetEvasionPosition(defragTeleportDist);
        PerformTeleport(evasionPos);

        if (memoryLeakIndicatorPrefab != null)
        {
            GameObject indicator = Instantiate(memoryLeakIndicatorPrefab, transform.position, Quaternion.identity);
            Destroy(indicator, memoryLeakFormationDelay + 0.1f);
        }

        yield return new WaitForSeconds(memoryLeakFormationDelay);

        SpawnMemoryLeak(transform.position);

        yield return new WaitForSeconds(0.25f - Mathf.Min(0.25f, memoryLeakFormationDelay));

        FacePlayer();

        yield return StartCoroutine(FireReprisalShotRoutine());

        ReportDebug("DESFRAGMENTACION EVASIVA: Teleport y represalia completados.", 1);
        currentState = BossState.Idle;

        StartCoroutine(DefragCooldownRoutine());
    }

    private IEnumerator FireReprisalShotRoutine()
    {
        float[] angles = { -15f, 0f, 15f };
        Vector3[] dirs = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            dirs[i] = Quaternion.Euler(0f, angles[i], 0f) * transform.forward;
        }
        yield return StartCoroutine(SpawnAndFireProjectiles(dirs));
    }

    private IEnumerator DefragCooldownRoutine()
    {
        yield return new WaitForSeconds(defragCooldown);
        defragOnCooldown = false;
        ReportDebug("DESFRAGMENTACION EVASIVA: Cooldown finalizado.", 1);
    }

    private Vector3 GetEvasionPosition(float distance)
    {
        if (player == null)
        {
            return transform.position + transform.right * distance;
        }

        Vector3 awayDir = (transform.position - player.position).normalized;
        Vector3 candidate = transform.position + awayDir * distance;

        NavMeshHit hit;
        return NavMesh.SamplePosition(candidate, out hit, distance * 0.6f, NavMesh.AllAreas)
               ? hit.position
               : candidate;
    }

    private IEnumerator ExecuteLatenciaCero()
    {
        ReportDebug("LATENCIA CERO: Ejecutando.", 1);
        currentState = BossState.Teleporting;
        latenciaOnCooldown = true;

        StopAgent();

        if (animController != null)
        {
            animController.PlayTP_pre();
            yield return new WaitForSeconds(animController.TPanitipacionTime);
        }

        Vector3 blindFlank = GetPlayerBlindFlank();
        PerformTeleport(blindFlank);

        if (latenciaGroundIndicatorPrefab != null)
        {
            GameObject indicator = Instantiate(latenciaGroundIndicatorPrefab, transform.position, Quaternion.identity);
            Destroy(indicator, latenciaAnticipationDuration);
        }

        if (animController != null) animController.IsWalking = false;

        yield return new WaitForSeconds(latenciaAnticipationDuration);

        SpawnMemoryLeak(transform.position);

        yield return StartCoroutine(ExecuteBufferOverrun(calledByLatencia: true));

        currentState = BossState.Idle;
        StartCoroutine(LatenciaCooldownRoutine());
    }

    private IEnumerator LatenciaCooldownRoutine()
    {
        yield return new WaitForSeconds(latenciaCooldown);
        latenciaOnCooldown = false;
        timeOutOfRange = 0f;
        ReportDebug("LATENCIA CERO: Cooldown finalizado.", 1);
    }

    private Vector3 GetPlayerBlindFlank()
    {
        if (player == null) return transform.position;

        float side = (Random.value > 0.5f) ? 1f : -1f;
        Vector3 candidate = player.position
                          - player.forward * 1.5f
                          + player.right * side;
        candidate.y = transform.position.y;

        NavMeshHit hit;
        return NavMesh.SamplePosition(candidate, out hit, 3f, NavMesh.AllAreas)
               ? hit.position
               : candidate;
    }

    #endregion

    #region Hito 50 Porciento - Arquitectura Distribuida

    private IEnumerator ExecuteArquitecturaDistribuida()
    {
        ReportDebug("ARQUITECTURA DISTRIBUIDA: Hito 50 por ciento alcanzado.", 1);
        currentState = BossState.Phase50;
        architecturePhaseActive = true;

        SetBossVisible(false);
        enemyHealth?.SetInvulnerable(true);
        currentShieldReduction = 1f;
        enemyHealth?.SetDynamicVulnerability(currentShieldReduction);

        if (phaseTransitionVFX != null)
        {
            phaseTransitionVFX.Play();
            VFXHelper.StopAndDestroy(phaseTransitionVFX, phaseTransitionVFX.main.duration);
        }

        spawnedNodes.Clear();
        activeNodeCount = 0;
        roomCenter = ComputeRoomCenter();

        for (int i = 0; i < maxNodes; i++)
        {
            if (nodePurulentosPrefab == null)
            {
                ReportDebug("ARQUITECTURA DISTRIBUIDA: Falta nodePurulentosPrefab.", 2);
                break;
            }

            Vector3 nodePos = GetNodeSpawnPosition(i);
            GameObject node = Instantiate(nodePurulentosPrefab, nodePos, Quaternion.identity);
            instantiatedEffects.Add(node);
            spawnedNodes.Add(node);
            activeNodeCount++;

            EnemyHealth nodeHealth = node.GetComponent<EnemyHealth>();
            if (nodeHealth != null) nodeHealth.OnDeath += OnNodeDestroyed;
        }

        ReportDebug($"ARQUITECTURA DISTRIBUIDA: {activeNodeCount} nodos instanciados.", 1);

        while (activeNodeCount > 0 && currentHealth > 0) yield return null;

        SetBossVisible(true);
        enemyHealth?.SetInvulnerable(false);
        currentShieldReduction = 0f;
        enemyHealth?.SetDynamicVulnerability(0f);
        architecturePhaseActive = false;

        ReportDebug("ARQUITECTURA DISTRIBUIDA: Todos los nodos destruidos. Jefe reaparece.", 1);
        currentState = BossState.Idle;
    }

    private void OnNodeDestroyed(GameObject node)
    {
        activeNodeCount = Mathf.Max(0, activeNodeCount - 1);
        spawnedNodes.Remove(node);

        currentShieldReduction = Mathf.Max(0f, currentShieldReduction - shieldReductionPerNode);
        enemyHealth?.SetDynamicVulnerability(currentShieldReduction);

        SpawnLarva(node.transform.position);

        ReportDebug($"ARQUITECTURA DISTRIBUIDA: Nodo destruido. " +
            $"Escudo: {currentShieldReduction * 100f:F0}%  Nodos: {activeNodeCount}", 1);

        EnemyHealth nodeHealth = node.GetComponent<EnemyHealth>();
        if (nodeHealth != null) nodeHealth.OnDeath -= OnNodeDestroyed;
    }

    private Vector3 GetNodeSpawnPosition(int index)
    {
        Vector3 currentRoomCenter = roomCenter != Vector3.zero ? roomCenter : transform.position;

        Vector3[] cornerDirs = {
            new Vector3( 1f, 0f,  1f).normalized,
            new Vector3(-1f, 0f,  1f).normalized,
            new Vector3(-1f, 0f, -1f).normalized,
            new Vector3( 1f, 0f, -1f).normalized,
        };

        int dirIndex = index % cornerDirs.Length;
        Vector3 dir = cornerDirs[dirIndex];
        Vector3 candidate = currentRoomCenter + dir * nodeSpawnRadius;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(candidate, out hit, nodeSpawnRadius * 1.5f, NavMesh.AllAreas))
            return hit.position;

        return NavMesh.SamplePosition(transform.position + dir * nodeSpawnRadius * 0.5f,
                                       out hit, nodeSpawnRadius, NavMesh.AllAreas)
               ? hit.position
               : candidate;
    }

    private Vector3 ComputeRoomCenter()
    {
        Vector3 sum = transform.position;
        int count = 1;
        float probeDistance = nodeSpawnRadius * 3f;

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 probe = transform.position + dir * probeDistance;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(probe, out hit, probeDistance * 0.5f, NavMesh.AllAreas))
            {
                sum += hit.position;
                count++;
            }
        }

        return sum / count;
    }

    #endregion

    #region Status Effects & Utility

    private Vector3 GetGroundPosition(Vector3 rayOrigin)
    {
        int groundLayer = LayerMask.GetMask("Ground", "Default");
        if (Physics.Raycast(rayOrigin + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 30f, groundLayer))
        {
            return hit.point + Vector3.up * 0.05f;
        }

        return new Vector3(rayOrigin.x, transform.position.y + 0.05f, rayOrigin.z);
    }

    public void HandleAnimEvents(string eventName)
    {
        if (eventName == ANIM_EVENT_ANTICIPATION_PAUSE)
        {
            StartAnticipationPause();
        }
    }

    private void StartAnticipationPause()
    {
        if (anticipationCoroutine != null) StopCoroutine(anticipationCoroutine);
        anticipationCoroutine = StartCoroutine(AnticipationRoutine());
    }

    private IEnumerator AnticipationRoutine()
    {
        isInAnticipation = true;

        float duration = 0f;
        AudioClip sfx;

        if (currentState == BossState.Attacking)
        {
            duration = exceptionFatalAnticipationDuration;
            sfx = exceptionFatalAnticipationSFX;
        }
        else if (currentState == BossState.Teleporting)
        {
            duration = latenciaAnticipationDuration;
            sfx = latenciaAnticipationSFX;
        }
        else
        {
            duration = exceptionFatalAnticipationDuration;
            sfx = exceptionFatalAnticipationSFX;
        }

        if (animController != null) animController.PauseAnimation();
        if (audioSource != null && sfx != null) audioSource.PlayOneShot(sfx);
        if (enemyVisualEffects != null) enemyVisualEffects.PlayAnticipationBlink(duration);

        yield return new WaitForSeconds(duration);

        if (animController != null) animController.ResumeAnimation();
        isInAnticipation = false;
        anticipationCoroutine = null;
    }

    private void CancelAnticipation()
    {
        if (anticipationCoroutine != null) StopCoroutine(anticipationCoroutine);
        anticipationCoroutine = null;
        if (animController != null) animController.ResumeAnimation();
        if (enemyVisualEffects != null) enemyVisualEffects.CancelAnticipationBlink();
        isInAnticipation = false;
    }

    private void DealAreaDamage(Vector3 center, float radius, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                ExecuteAttack(hit.gameObject, damage);
                break;
            }
        }
    }

    private void ExecuteAttack(GameObject target, float damage)
    {
        if (target.TryGetComponent(out PlayerHealth health))
        {
            if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem)
                && blockSystem.IsBlocking
                && blockSystem.CanBlockAttack(transform.position))
            {
                float remaining = blockSystem.ProcessBlockedAttack(damage);
                if (remaining > 0f) health.TakeDamage(remaining, false, AttackDamageType.Melee);
                ReportDebug("Ataque bloqueado por el jugador.", 1);
                return;
            }
            health.TakeDamage(damage, false, AttackDamageType.Melee);
        }
        else if (target.TryGetComponent(out PlayerHealth healthOnly))
        {
            healthOnly.TakeDamage(damage, false, AttackDamageType.Melee);
        }
    }

    private IEnumerator DashToPositionOrHit(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;
        bool hitPlayer = false;

        FaceDirection((target - start).normalized);

        bool agentControlled = agent != null && agent.enabled;

        while (elapsed < duration && !hitPlayer)
        {
            Vector3 next = Vector3.Lerp(start, target, elapsed / duration);

            NavMeshHit navHit;
            Vector3 movePos = NavMesh.SamplePosition(next, out navHit, 2f, NavMesh.AllAreas)
                ? navHit.position
                : next;

            if (agentControlled) agent.Warp(movePos);
            else transform.position = movePos;

            Collider[] hits = Physics.OverlapSphere(transform.position,
                                                     bufferOverrunAttackRange * 0.5f,
                                                     LayerMask.GetMask("Player"));
            if (hits.Length > 0)
            {
                hitPlayer = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void PerformTeleport(Vector3 target)
    {
        if (teleportVFXPrefab != null)
        {
            GameObject vfxOut = Instantiate(teleportVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfxOut, 1.5f);
        }

        NavMeshHit hit;
        Vector3 finalPos = NavMesh.SamplePosition(target, out hit, 3f, NavMesh.AllAreas)
            ? hit.position
            : target;

        if (!hit.hit)
        {
            ReportDebug("TELEPORT: Destino fuera de NavMesh, se usara posicion sin ajustar.", 2);
        }

        if (agent != null && agent.enabled)
        {
            agent.Warp(finalPos);
        }
        else
        {
            transform.position = finalPos;
        }

        if (teleportVFXPrefab != null)
        {
            GameObject vfxIn = Instantiate(teleportVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfxIn, 1.5f);
        }

        if (audioSource != null && teleportSFX != null)
        {
            audioSource.PlayOneShot(teleportSFX);
        }

        if (animController != null) animController.PlayTP_Impact();

        ReportDebug($"TELEPORT: {transform.position}", 1);
    }

    private void SpawnMemoryLeak(Vector3 position)
    {
        if (memoryLeakPrefab == null)
        {
            ReportDebug("Falta memoryLeakPrefab.", 2);
            return;
        }

        GameObject leak = Instantiate(memoryLeakPrefab, position, Quaternion.identity);
        instantiatedEffects.Add(leak);

        MemoryLeak leakScript = leak.GetComponent<MemoryLeak>();
        if (leakScript != null)
        {
            leakScript.Initialize(memoryLeakDuration, memoryLeakDPS, memoryLeakRadius);
        }
        else Destroy(leak, memoryLeakDuration);
    }

    private void SpawnLarva(Vector3 position)
    {
        if (larvaPrefab == null) return;

        Vector2 randomDir = Random.insideUnitCircle.normalized * safeSpawnRadius;
        Vector3 candidatePos = position + new Vector3(randomDir.x, 0f, randomDir.y);

        Vector3 finalSpawnPos = candidatePos;

        if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, safeSpawnRadius * 2, NavMesh.AllAreas))
        {
            finalSpawnPos = hit.position;
        }

        GameObject larvaGO = Instantiate(larvaPrefab, finalSpawnPos, Quaternion.identity);

        if (larvaGO.TryGetComponent(out Larva larvaScript))
        {
            larvaScript.Initialize(this.player);
            instantiatedEffects.Add(larvaGO);
        }
        else
        {
            ReportDebug("Larva instanciada sin script Larva.", 2);
            Destroy(larvaGO);
        }
    }

    private void StopAgent()
    {
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (animController != null) animController.IsWalking = false;
    }

    private void FacePlayer()
    {
        if (player == null) return;
        FaceDirection((player.position - transform.position).normalized);
    }

    private void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
    }

    private void SetBossVisible(bool visible)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.enabled = visible;
        }

        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (c is not CharacterController) c.enabled = visible;
        }
    }

    private void CleanUpEffects()
    {
        for (int i = instantiatedEffects.Count - 1; i >= 0; i--)
        {
            if (instantiatedEffects[i] != null)
            {
                Destroy(instantiatedEffects[i]);
            }
        }

        instantiatedEffects.Clear();

        foreach (Larva larva in FindObjectsByType<Larva>(FindObjectsSortMode.None))
        {
            if (larva != null && larva.gameObject != null)
            {
                larva.Die();
            }
        }
    }

    #endregion

    #region Unity Debug & GUI

    private void OnDrawGizmos()
    {
        if (!showDebugGUI) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.18f);
        Gizmos.DrawWireSphere(transform.position, bufferOverrunAttackRange);

        Gizmos.color = new Color(0f, 1f, 1f, 0.18f);
        Gizmos.DrawWireSphere(transform.position, latenciaActivationRange);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.18f);
        Gizmos.DrawWireSphere(transform.position, defragActivationRange);

        Gizmos.color = new Color(0f, 1f, 0f, 0.18f);
        Gizmos.DrawWireSphere(transform.position, nodeSpawnRadius);

#if UNITY_EDITOR
        Color labelColor = currentState switch
        {
            BossState.Idle => Color.white,
            BossState.Attacking => Color.red,
            BossState.Teleporting => Color.cyan,
            BossState.Phase50 => Color.yellow,
            BossState.Dead => Color.grey,
            _ => Color.white
        };

        GUIStyle style = new GUIStyle
        {
            normal = { textColor = labelColor },
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        string label = $"[BaalBoss]\n{currentState}  HP: {(int)currentHealth}";
        if (architecturePhaseActive) label += $"\nNODOS: {activeNodeCount}";
        if (defragInterruptPending) label += "\nDEFRAG PENDING";
        if (latenciaInterruptPending) label += "\nLATENCIA PENDING";

        UnityEditor.Handles.Label(transform.position + Vector3.up * 3.2f, label, style);
#endif
    }

    private void OnGUI()
    {
        if (!showDebugGUI) return;
        GUILayout.BeginArea(new Rect(10, 10, 260, 165));
        GUILayout.Label($"[BaalBoss] State      : {currentState}");
        GUILayout.Label($"HP                    : {currentHealth:F0} / {maxHealth:F0}");
        GUILayout.Label($"Phase50               : {phase50Triggered}");
        GUILayout.Label($"TimeOutOfRange        : {timeOutOfRange:F1} s");
        GUILayout.Label($"ActiveNodes / Shield  : {activeNodeCount} / {currentShieldReduction * 100f:F0}%");
        GUILayout.Label($"DefragHits            : {defragHitCount} / {defragHitThreshold}");
        GUILayout.EndArea();
    }

    #endregion

    #region Logging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int priority)
    {
        switch (priority)
        {
            case 1: Debug.Log($"[BaalBoss] {message}"); break;
            case 2: Debug.LogWarning($"[BaalBoss] {message}"); break;
            case 3: Debug.LogError($"[BaalBoss] {message}"); break;
            default: Debug.Log($"[BaalBoss] {message}"); break;
        }
    }

    #endregion
}