using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class JitterEnemy : MonoBehaviour
{
    #region Estado Interno

    private enum JitterState
    {
        Patrol,        
        Chase,         
        ErraticFlee,   
        ErraticChase,  
        PreExplosion,  
        Dead           
    }

    #endregion

    #region Configuración en el Inspector

    [Header("Spawn")]
    [Tooltip("Tiempo de espera desde que el Jitter aparece en escena hasta que comienza a actuar.")]
    [SerializeField] private float spawnDelay = 1.0f;

    [Header("Patrol")]
    [Tooltip("Waypoints que el Jitter recorre en bucle. Si no hay, permanece estático.")]
    [SerializeField] private Transform[] patrolPoints;
    [Tooltip("Tiempo de espera en cada waypoint antes de continuar.")]
    [SerializeField] private float patrolWaitTime = 1.5f;
    [Tooltip("Distancia al waypoint para considerarlo alcanzado.")]
    [SerializeField] private float waypointReachedThreshold = 0.4f;
    [SerializeField] private float patrolSpeed = 2f;

    [Header("Detection & Chase")]
    [Tooltip("Radio en el que el Jitter detecta al jugador.")]
    [SerializeField] private float detectionRange = 10f;
    [Tooltip("Velocidad base de persecución.")]
    [SerializeField] private float baseChaseSpeed = 8f;
    [Tooltip("Velocidad máxima alcanzable tras múltiples estados errático.")]
    [SerializeField] private float maxChaseSpeed = 10.4f;

    [Header("Erratic Flee")]
    [Tooltip("Número exacto de cambios de dirección aleatorios antes de retomar la persecución.")]
    [SerializeField] private int erraticFleeDirectionCount = 5;
    [Tooltip("Intervalo mínimo en segundos entre cada cambio de dirección.")]
    [SerializeField] private float erraticFleeMinInterval = 0.15f;
    [Tooltip("Intervalo máximo en segundos entre cada cambio de dirección.")]
    [SerializeField] private float erraticFleeMaxInterval = 0.3f;   
    [Tooltip("Distancia mínima de cada destino de huida aleatorio.")]
    [SerializeField] private float erraticFleeMinDistance = 3f;
    [Tooltip("Distancia máxima de cada destino de huida aleatorio.")]
    [SerializeField] private float erraticFleeMaxDistance = 8f;

    [Header("Erratic Chase")]
    [Tooltip(
        "Si TRUE: acumula 'erraticSpeedBonus' a la velocidad actual cada vez que recibe daño, " +
        "sin superar maxChaseSpeed.\n" +
        "Si FALSE: salta directamente de la velocidad base a maxChaseSpeed en cuanto entra en estado errático."
    )]
    [SerializeField] private bool erraticSpeedIncremental = false;
    [Tooltip("Bonus de velocidad por cada golpe recibido. Solo se usa si 'Erratic Speed Incremental' = true.")]
    [SerializeField] private float erraticSpeedBonus = 1.5f;
    [Tooltip("Duración máxima del estado ErraticChase. Al agotarse, vuelve a Chase con velocidad base.")]
    [SerializeField] private float erraticChaseDuration = 5f;

    [Header("Explosion — Triggers")]
    [Tooltip("Distancia al jugador que activa el conteo de explosión por proximidad.")]
    [SerializeField] private float explosionProximityRange = 5f;
    [Tooltip("Radio del área de daño de la explosión.")]
    [SerializeField] private float explosionRadius = 5f;
    [Tooltip("Daño que aplica la explosión a cada objetivo.")]
    [SerializeField] private float explosionDamage = 5f;
    [Tooltip("Espera antes de explotar cuando el Jitter muere.")]
    [SerializeField] private float deathExplosionDelay = 1f;
    [Tooltip("Espera antes de explotar cuando el jugador está demasiado cerca.")]
    [SerializeField] private float proximityExplosionDelay = 1.5f;
    [SerializeField] private LayerMask explosionTargetLayers;
    [Tooltip("Prefab VFX instanciado al explotar.")]
    [SerializeField] private GameObject explosionVFXPrefab;
    [SerializeField] private Vector3 explosionVFXOffset = Vector3.up;

    [Header("Explosion — Blink")]
    [Tooltip("Intervalo de blink al inicio del conteo (lento = poca frecuencia).")]
    [SerializeField] private float blinkIntervalSlow = 0.5f;
    [Tooltip("Intervalo de blink al final del conteo (rápido = alta frecuencia).")]
    [SerializeField] private float blinkIntervalFast = 0.05f;
    [Tooltip("Material aplicado a todos los renderers durante el conteo de explosión. " +
             "Si es null, solo se alterna la visibilidad.")]
    [SerializeField] private Material explosionBlinkMaterial;

    [Header("Visual")]
    [Tooltip("Raíz del visual del Jitter. " +
             "Si es null, se buscará automáticamente en los hijos del GameObject.")]
    [SerializeField] private Transform visualRoot;

    [Header("Debug Gizmos")]
    [SerializeField] private bool showGizmos = true;

    #endregion

    #region Referencias Privadas

    // Componentes
    private EnemyHealth enemyHealth;
    private NavMeshAgent agent;
    private Transform playerTransform;

    // Visuales 
    private Renderer[] cachedRenderers;
    private Material[] originalMaterials;

    #endregion

    #region Estado de Tiempo de Ejecución

    /// <summary>
    /// Indica si el Jitter ha completado su spawn y puede actuar.
    /// Previene que se ejecute lógica antes de tiempo.
    /// </summary>
    private bool isReady = false;

    private JitterState currentState;
    private JitterState stateBeforeExplosion; // Para restaurar al cancelar explosión por proximidad

    // Velocidad de persecución actual
    private float currentChaseSpeed;

    // Patrullaje
    private int patrolIndex;
    private bool isWaitingAtPatrolPoint;

    // Explosion
    private bool explosionIsFromDeath;
    private float currentExplosionDelay;
    private float explosionCountdownProgress;

    #endregion

    #region Coroutines
    
    private Coroutine spawnCoroutine;
    private Coroutine patrolWaitCoroutine;
    private Coroutine erraticCoroutine;
    private Coroutine erraticChaseCoroutine;
    private Coroutine explosionCountdownCoroutine;
    private Coroutine blinkCoroutine;

    #endregion

    #region Ciclo de Vida de Unity

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();

        // Cachear renderers para el blink de explosión
        CacheRenderers();
    }

    private void Start()
    {
        // Localizar jugador
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        playerTransform = playerGO != null ? playerGO.transform : null;

        if (playerTransform == null) ReportDebug($"Jugador no encontrado en la escena ('{gameObject.name}').", 2);

        // Suscribir eventos de salud
        enemyHealth.OnDamaged += HandleDamaged;
        enemyHealth.OnDeath += HandleDeath;
        enemyHealth.CanDestroy = false;
        enemyHealth.CanDisable = false;

        // Estado inicial
        currentChaseSpeed = baseChaseSpeed;
        agent.speed = patrolSpeed;

        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        isReady = false;

        yield return new WaitForSeconds(spawnDelay);

        isReady = true;
        TransitionTo(JitterState.Patrol);
        spawnCoroutine = null;
    }

    private void Update()
    {
        if (currentState == JitterState.Dead || !isReady) return;

        switch (currentState)
        {
            case JitterState.Patrol:
                UpdatePatrol();
                TryDetectPlayer();
                break;

            case JitterState.Chase:
            case JitterState.ErraticChase:
                UpdateChase();
                TryCheckProximityExplosion();
                break;

            case JitterState.ErraticFlee:
                // Navegación gestionada por ErraticBehaviorCoroutine.
                // Solo verifica si el jugador está demasiado cerca.
                TryCheckProximityExplosion();
                break;

            case JitterState.PreExplosion:
                // Si la explosión fue por proximidad y no por muerte, chequea
                // que el jugador siga dentro del rango; si no, lo cancela.
                if (!explosionIsFromDeath) CheckExplosionCancellation();
                break;
        }
    }

    private void OnDestroy()
    {
        // Desuscribe para evitar callbacks sobre objeto destruido
        if (enemyHealth != null)
        {
            enemyHealth.OnDamaged -= HandleDamaged;
            enemyHealth.OnDeath -= HandleDeath;
        }
    }

    #endregion

    #region Maquina de Estados y Transiciones
    
    private void TransitionTo(JitterState newState)
    {
        ExitState(currentState);
        currentState = newState;
        EnterState(newState);
    }

    private void ExitState(JitterState state)
    {
        switch (state)
        {
            case JitterState.Patrol:
                StopPatrolWait();
                break;
            case JitterState.ErraticChase:
                // Cancela el timer de duración si se interrumpe antes de que acabe
                if (erraticChaseCoroutine != null)
                {
                    StopCoroutine(erraticChaseCoroutine);
                    erraticChaseCoroutine = null;
                }
                break;
        }
    }

    private void EnterState(JitterState state)
    {
        switch (state)
        {
            case JitterState.Patrol:
                agent.speed = patrolSpeed;
                SetAgentMoving(true);
                MoveToNextPatrolPoint();
                break;

            case JitterState.Chase:
                agent.speed = currentChaseSpeed;
                SetAgentMoving(true);
                break;

            case JitterState.ErraticFlee:
                // La coroutine es iniciada antes de llamar TransitionTo.
                // Solo sincroniza la velocidad del agente con la nueva velocidad errática.
                agent.speed = currentChaseSpeed;
                SetAgentMoving(true);
                break;

            case JitterState.ErraticChase:
                agent.speed = currentChaseSpeed;
                SetAgentMoving(true);
                // Inicia el timer de duración máxima de este estado
                if (erraticChaseCoroutine != null) StopCoroutine(erraticChaseCoroutine);
                erraticChaseCoroutine = StartCoroutine(ErraticChaseTimerCoroutine());
                break;

            case JitterState.PreExplosion:
                StopAgentSafe();
                break;

            case JitterState.Dead:
                StopAgentSafe();
                break;
        }
    }

    #endregion

    #region Patrullaje

    private void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        if (isWaitingAtPatrolPoint) return;
        if (!agent.enabled || !agent.isOnNavMesh) return;

        // NavMeshAgent.remainingDistance solo es fiable cuando pathPending == false
        if (!agent.pathPending && agent.remainingDistance <= waypointReachedThreshold) StartPatrolWait();
    }

    private void MoveToNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        Transform target = patrolPoints[patrolIndex];
        if (target != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.SetDestination(target.position);
        }

        isWaitingAtPatrolPoint = false;
    }

    private void StartPatrolWait()
    {
        StopPatrolWait(); // cancela si había una espera previa
        isWaitingAtPatrolPoint = true;
        patrolWaitCoroutine = StartCoroutine(PatrolWaitCoroutine());
    }

    private void StopPatrolWait()
    {
        if (patrolWaitCoroutine == null) return;
        StopCoroutine(patrolWaitCoroutine);
        patrolWaitCoroutine = null;
        isWaitingAtPatrolPoint = false;
    }

    private IEnumerator PatrolWaitCoroutine()
    {
        StopAgentSafe();
        yield return new WaitForSeconds(patrolWaitTime);

        // Avanza al siguiente punto en bucle
        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        SetAgentMoving(true);
        MoveToNextPatrolPoint();

        isWaitingAtPatrolPoint = false;
        patrolWaitCoroutine = null;
    }

    private void TryDetectPlayer()
    {
        if (playerTransform == null) return;
        if (Vector3.Distance(transform.position, playerTransform.position) <= detectionRange)
        {
            TransitionTo(JitterState.Chase);
        }
    }

    #endregion

    #region Persecución normal y errática
    
    private void UpdateChase()
    {
        if (playerTransform == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.SetDestination(playerTransform.position);
    }

    private void HandleDamaged()
    {
        if (currentState == JitterState.Dead) return;
        if (currentState == JitterState.PreExplosion) return;

        // Acumula bonus de velocidad limitado por maxChaseSpeed
        if (erraticSpeedIncremental)
        {
            currentChaseSpeed = Mathf.Min(currentChaseSpeed + erraticSpeedBonus, maxChaseSpeed);
        }
        else currentChaseSpeed = maxChaseSpeed;

        // Cancela el timer de ErraticChase si estaba corriendo
        if (erraticChaseCoroutine != null)
        {
            StopCoroutine(erraticChaseCoroutine);
            erraticChaseCoroutine = null;
        }

        // Reinicia huida errática y cancela cualquier vuelta previa
        if (erraticCoroutine != null)
        {
            StopCoroutine(erraticCoroutine);
            erraticCoroutine = null;
        }

        // Arranca coroutine antes de TransitionTo para que EnterState
        // encuentre el agente ya configurado correctamente.
        erraticCoroutine = StartCoroutine(ErraticBehaviorCoroutine());
        TransitionTo(JitterState.ErraticFlee);
    }

    private IEnumerator ErraticBehaviorCoroutine()
    {
        // Ejecuta exactamente erraticFleeDirectionCount cambios de dirección.
        // Cada cambio espera un intervalo aleatorio entre erraticFleeMinInterval y erraticFleeMaxInterval.
        for (int i = 0; i < erraticFleeDirectionCount; i++)
        {
            Vector3 fleeTarget = GetRandomNavMeshPosition();
            if (agent.enabled && agent.isOnNavMesh) agent.SetDestination(fleeTarget);

            float waitTime = Random.Range(erraticFleeMinInterval, erraticFleeMaxInterval);
            yield return new WaitForSeconds(waitTime);
        }

        erraticCoroutine = null;
        TransitionTo(JitterState.ErraticChase);
    }

    /// <summary>
    /// Controla la duración máxima del estado ErraticChase.
    /// Al agotarse, restaura la velocidad a la base y vuelve a Chase normal.
    /// </summary>
    private IEnumerator ErraticChaseTimerCoroutine()
    {
        yield return new WaitForSeconds(erraticChaseDuration);

        erraticChaseCoroutine = null;
        currentChaseSpeed = baseChaseSpeed;
        TransitionTo(JitterState.Chase);
    }

    /// <summary>
    /// Busca un punto válido en el NavMesh en una dirección y distancia aleatorias.
    /// Realiza hasta <c>maxAttempts</c> intentos; si falla, retorna la posición actual.
    /// </summary>
    private Vector3 GetRandomNavMeshPosition(int maxAttempts = 8)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 rand2D = Random.insideUnitCircle.normalized;
            float dist = Random.Range(erraticFleeMinDistance, erraticFleeMaxDistance);
            Vector3 candidate = transform.position + new Vector3(rand2D.x, 0f, rand2D.y) * dist;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, erraticFleeMaxDistance, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        return transform.position; // fallback seguro
    }

    #endregion

    #region Explosión - Detección por proximidad
    
    private void TryCheckProximityExplosion()
    {
        if (playerTransform == null) return;
        if (currentState == JitterState.PreExplosion) return;

        if (Vector3.Distance(transform.position, playerTransform.position) <= explosionProximityRange)
        {
            BeginExplosionCountdown(proximityExplosionDelay, fromDeath: false);
        }
    }

    /// <summary>
    /// Llamado cada frame mientras el Jitter está en PreExplosion por proximidad.
    /// Si el jugador sale del rango, cancela la explosión y restaura el estado anterior.
    /// </summary>
    private void CheckExplosionCancellation()
    {
        if (playerTransform == null) return;

        if (Vector3.Distance(transform.position, playerTransform.position) > explosionProximityRange)
        {
            CancelExplosionCountdown();
            // Vuelve a la persecución; el jugador estaba lo bastante cerca como para
            // estar en rango de detección, así que Chase es el estado apropiado.
            TransitionTo(JitterState.Chase);
        }
    }

    #endregion

    #region Explosión - Muerte
    
    private void HandleDeath(GameObject _)
    {
        if (currentState == JitterState.Dead) return;

        // Cancela cualquier explosión de proximidad que pudiera estar en curso
        AbortAllExplosionRoutines();

        TransitionTo(JitterState.Dead);
        BeginExplosionCountdown(deathExplosionDelay, fromDeath: true);
    }

    #endregion

    #region Explosión - Logica Compartida
    
    /// <summary>
    /// Inicia(o reinicia el conteo regresivo de explosión.
    /// </summary>
    private void BeginExplosionCountdown(float delay, bool fromDeath)
    {
        // Detener cualquier rutina errática que estuviera corriendo
        if (erraticCoroutine != null)
        {
            StopCoroutine(erraticCoroutine);
            erraticCoroutine = null;
        }

        AbortAllExplosionRoutines();

        explosionIsFromDeath = fromDeath;
        currentExplosionDelay = delay;
        explosionCountdownProgress = 0f;

        if (!fromDeath)
        {
            stateBeforeExplosion = currentState;
            TransitionTo(JitterState.PreExplosion);
        }

        explosionCountdownCoroutine = StartCoroutine(ExplosionCountdownCoroutine(delay));
    }

    /// <summary>
    /// Cancela la cuenta regresiva activa y lanza el blink inverso.
    /// Solo válido para explosiones por proximidad y no por muerte.
    /// </summary>
    private void CancelExplosionCountdown()
    {
        if (explosionCountdownCoroutine == null) return;

        StopCoroutine(explosionCountdownCoroutine);
        explosionCountdownCoroutine = null;

        float progressSnapshot = explosionCountdownProgress;

        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(BlinkReverseCoroutine(progressSnapshot));
    }

    private void AbortAllExplosionRoutines()
    {
        if (explosionCountdownCoroutine != null)
        {
            StopCoroutine(explosionCountdownCoroutine);
            explosionCountdownCoroutine = null;
        }

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
    }

    #endregion

    #region Explosión - Efectos visuales
    
    private IEnumerator ExplosionCountdownCoroutine(float delay)
    {
        float elapsed = 0f;
        bool rendVisible = true;
        float nextBlinkAt = 0f;

        // Aplica material de explosión una sola vez al inicio
        if (explosionBlinkMaterial != null) ApplyMaterialToAll(explosionBlinkMaterial);

        while (elapsed < delay)
        {
            explosionCountdownProgress = elapsed / delay; // [0, 1]

            // El intervalo de blink disminuye linealmente, de lento a rápido
            float interval = Mathf.Lerp(blinkIntervalSlow, blinkIntervalFast, explosionCountdownProgress);

            if (elapsed >= nextBlinkAt)
            {
                rendVisible = !rendVisible;
                SetRenderersVisible(rendVisible);
                nextBlinkAt = elapsed + interval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Conteo completado y explota
        explosionCountdownProgress = 1f;
        explosionCountdownCoroutine = null;

        SetRenderersVisible(true);
        Explode();
    }

    private IEnumerator BlinkReverseCoroutine(float startProgress)
    {
        // Si el progreso era casi 0, no hay nada que revertir
        if (startProgress <= Mathf.Epsilon)
        {
            SetRenderersVisible(true);
            RestoreOriginalMaterials();
            blinkCoroutine = null;
            yield break;
        }

        // La duración del blink inverso es proporcional al progreso alcanzado,
        // de modo que la velocidad de deceleleración es simétrica a la de aceleración.
        float reverseDuration = startProgress * currentExplosionDelay;
        float elapsed = 0f;
        bool rendVisible = true;
        float nextBlinkAt = 0f;

        while (elapsed < reverseDuration)
        {
            float t = elapsed / reverseDuration;
            float progress = Mathf.Lerp(startProgress, 0f, t); // [startProgress, 0]

            float interval = Mathf.Lerp(blinkIntervalSlow, blinkIntervalFast, progress);

            if (elapsed >= nextBlinkAt)
            {
                rendVisible = !rendVisible;
                SetRenderersVisible(rendVisible);
                nextBlinkAt = elapsed + interval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Blink apagado y restaura estado visual normal
        SetRenderersVisible(true);
        RestoreOriginalMaterials();
        blinkCoroutine = null;
    }

    #endregion

    #region Explosión - Lógica de Ejecución

    private void Explode()
    {
        // VFX
        if (explosionVFXPrefab != null) Instantiate(explosionVFXPrefab, transform.position + explosionVFXOffset, Quaternion.identity);

        // Daño en área
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, explosionTargetLayers);
        foreach (var col in hits)
        {
            if (col == null) continue;

            // Intentar aplicar daño a cualquier entidad con la interfaz IDamageable incluida.
            var targetHealth = col.GetComponentInParent<IDamageable>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(explosionDamage, false, AttackDamageType.Melee);
                continue;
            }
        }

        Destroy(gameObject);
    }

    #endregion

    #region Helpers Visuales

    /// <summary>
    /// Cachea los Renderers en Awake. Soporta la clase base Renderer.
    /// </summary>
    private void CacheRenderers()
    {
        Transform root = visualRoot != null ? visualRoot : transform;
        // true por si algún renderer está desactivado al inicio
        Renderer[] all = root.GetComponentsInChildren<Renderer>(true);

        if (all == null || all.Length == 0)
        {
            ReportDebug($"No se encontraron Renderers bajo '{root.name}'. " +
                             $"Asigna 'Visual Root' en el Inspector.", 2);
            return;
        }

        // Contar los habilitados
        int activeCount = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].enabled) activeCount++;
        }

        cachedRenderers = new Renderer[activeCount];
        originalMaterials = new Material[activeCount];

        int idx = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || !all[i].enabled) continue;
            cachedRenderers[idx] = all[i];
            originalMaterials[idx] = all[i].sharedMaterial;
            idx++;
        }
    }

    private void SetRenderersVisible(bool visible)
    {
        if (cachedRenderers == null) return;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null) cachedRenderers[i].enabled = visible;
        }
    }

    private void ApplyMaterialToAll(Material mat)
    {
        if (cachedRenderers == null || mat == null) return;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null) cachedRenderers[i].material = mat;
        }
    }

    private void RestoreOriginalMaterials()
    {
        if (cachedRenderers == null || originalMaterials == null) return;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null && originalMaterials[i] != null)
            {
                cachedRenderers[i].material = originalMaterials[i];
            }
        }
    }

    #endregion

    #region Helpers de NavMeshAgent

    /// <summary>
    /// Reactiva o pausa el movimiento del agente.
    /// Solo actúa si el agente está habilitado y sobre el NavMesh.
    /// </summary>
    private void SetAgentMoving(bool moving)
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = !moving;
    }

    /// <summary>
    /// Para el agente y limpia el path actual de forma segura.
    /// </summary>
    private void StopAgentSafe()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.ResetPath();
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        // Rango de detección (verde)
        Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 0.20f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Rango de activación de explosión por proximidad (naranja)
        Gizmos.color = new Color(1.0f, 0.5f, 0.0f, 0.30f);
        Gizmos.DrawWireSphere(transform.position, explosionProximityRange);

        // Radio de daño de la explosión (rojo)
        Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.20f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        // Waypoints de patrulla (cyan, con líneas de ruta)
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null) continue;
            Gizmos.DrawSphere(patrolPoints[i].position, 0.18f);

            int prev = (i - 1 + patrolPoints.Length) % patrolPoints.Length;
            if (patrolPoints[prev] != null)
            {
                Gizmos.DrawLine(patrolPoints[prev].position, patrolPoints[i].position);
            }
        }
    }
#endif

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[JitterEnemy] {message}");
                break;
            case 2:
                Debug.LogWarning($"[JitterEnemy] {message}");
                break;
            case 3:
                Debug.LogError($"[JitterEnemy] {message}");
                break;
            default:
                Debug.Log($"[JitterEnemy] {message}");
                break;
        }
    }
}