// SwarmController.cs
// Versión modificada: soporta estar en child o root, reenvía triggers desde el collider root,
// reconoce agentes ya presentes (no duplica si ya hay SwarmAgent) y ahora se dispersa
// si el EnemyHealth "anfitrión" emite OnDeath (sin modificar EnemyHealth).
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwarmController : MonoBehaviour
{
    [Header("Visual / agentes")]
    [Tooltip("Parent para los agentes (si el prefab ya los incluye puedes dejar vacío).")]
    public Transform agentsParent;
    [Tooltip("Prefab de agente mosca (opcional si el prefab ya trae hijos).")]
    public GameObject swarmAgentPrefab;
    public int agentCount = 10;
    public float visualRadius = 1.6f;
    public float spawnHeight = 0.5f;

    [Header("Movimiento")]
    public float pursuitSpeed = 3.0f;
    public float stoppingDistance = 0.6f;

    [Header("Daño")]
    public float damagePerSecond = 2f;        // 2 DPS exactos
    public float damageTickInterval = 1f;     // aplicar cada 1s por defecto

    [Header("Dispersión")]
    public float disperseVisualDuration = 0.6f;

    // runtime
    private Transform _playerTransform;
    private PlayerHealth _playerHealth;
    private Collider _rootCollider; // puede estar en root o en un padre
    private Transform _rootTransform; // transform que se moverá y que se usa como centro
    private List<GameObject> _spawnedAgents = new List<GameObject>();
    private bool _playerInside = false;
    private bool _active = false;
    private Coroutine _damageCoroutine;
    private float _lastKnownPlayerHealth = -1f;
    private bool _lastDamageWasFromSwarm = false;
    private TriggerForwarder _forwarder;

    // --- vínculo con EnemyHealth del "host" (no modificamos EnemyHealth) ---
    private EnemyHealth _hostEnemyHealth;

    /// <summary>
    /// Llamado por el PentagramBelcebu al instanciar: posicion inicial.
    /// </summary>
    public void InitializeFromPentagram(Vector3 spawnPosition)
    {
        transform.position = spawnPosition;
        InitializeRuntime();
    }

    private void Awake()
    {
        // Buscar collider en este GO o en sus padres (soporte si el script está en un hijo)
        _rootCollider = GetComponent<Collider>() ?? GetComponentInParent<Collider>();

        if (_rootCollider == null)
        {
            Debug.LogWarning("[SwarmController] Añade un Collider (isTrigger) en el root del prefab del enjambre (o en un padre).");
        }
        else if (!_rootCollider.isTrigger)
        {
            Debug.LogWarning("[SwarmController] El Collider del prefab de enjambre debería estar marcado isTrigger.");
        }

        // Definir la transform raíz: preferimos la transform del objeto que tiene el collider
        _rootTransform = (_rootCollider != null) ? _rootCollider.transform : transform;
    }

    private void Start()
    {
        if (!_active)
            InitializeRuntime();
    }

    private void InitializeRuntime()
    {
        _active = true;
        if (agentsParent == null) agentsParent = transform;

        // Si el collider está en un GameObject diferente al que tiene este script,
        // añadimos un forwarder para recibir OnTriggerEnter/Exit.
        if (_rootCollider != null && _rootCollider.gameObject != gameObject)
        {
            _forwarder = _rootCollider.gameObject.GetComponent<TriggerForwarder>();
            if (_forwarder == null)
            {
                _forwarder = _rootCollider.gameObject.AddComponent<TriggerForwarder>();
                _forwarder.Init(this);
            }
            else
            {
                _forwarder.owner = this;
            }
        }

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            _playerTransform = playerGO.transform;
            playerGO.TryGetComponent(out _playerHealth);
            if (_playerHealth != null)
            {
                _lastKnownPlayerHealth = _playerHealth.CurrentHealth;
                PlayerHealth.OnHealthChanged += OnPlayerHealthChanged;
            }
        }

        // Suscribirnos al EnemyHealth "anfitrión" si existe en la jerarquía del rootTransform
        // (esto conecta la vida del enemigo con la dispersión del enjambre sin tocar EnemyHealth).
        TryBindHostEnemyHealth();

        // Si ya hay hijos en agentsParent que sean SwarmAgent, los registramos en lugar de duplicar
        bool hasAgentChildren = false;
        foreach (Transform child in agentsParent)
        {
            if (child == null) continue;
            var existingAgent = child.GetComponent<SwarmAgent>();
            if (existingAgent != null)
            {
                hasAgentChildren = true;
                _spawnedAgents.Add(child.gameObject);
                // Inicializamos visualmente por si falta
                existingAgent.Initialize(_rootTransform.position, visualRadius, UnityEngine.Random.Range(1.6f, 3.0f), UnityEngine.Random.Range(0f, Mathf.PI * 2f));
            }
        }

        // Si no hay agentes existentes y tenemos prefab -> spawn
        if (swarmAgentPrefab != null && !hasAgentChildren)
            SpawnAgents();
    }

    private void TryBindHostEnemyHealth()
    {
        // Buscar en el rootTransform hacia arriba (GetComponentInParent) o en hijos (fallback)
        if (_rootTransform == null) return;

        // 1) intentar encontrar EnemyHealth en parents (incluye el mismo transform)
        _hostEnemyHealth = _rootTransform.GetComponentInParent<EnemyHealth>();

        // 2) si no encontramos, intentar en hijos (útil si el Swarm está en el root y EnemyHealth es child)
        if (_hostEnemyHealth == null)
        {
            _hostEnemyHealth = _rootTransform.GetComponentInChildren<EnemyHealth>();
        }

        if (_hostEnemyHealth != null)
        {
            // asegurarnos de no duplicar suscripción
            _hostEnemyHealth.OnDeath -= HandleHostDeath;
            _hostEnemyHealth.OnDeath += HandleHostDeath;
            // opcional: debug corto
            Debug.Log($"[SwarmController] Suscrito a OnDeath de {_hostEnemyHealth.gameObject.name}");
        }
    }

    private void HandleHostDeath(GameObject who)
    {
        // Cuando el enemigo "host" muere -> dispersar el enjambre (visual) y destruir root
        // Ejecutar en el hilo principal (ya estamos en el hilo Unity porque es un evento llamado desde Unity)
        DisperseAndDestroy();
    }

    private void Update()
    {
        if (!_active || _playerTransform == null) return;

        float dist = Vector3.Distance(_rootTransform.position, _playerTransform.position);
        if (dist > stoppingDistance)
        {
            _rootTransform.position = Vector3.MoveTowards(_rootTransform.position, _playerTransform.position, pursuitSpeed * Time.deltaTime);
        }

        for (int i = 0; i < _spawnedAgents.Count; i++)
        {
            var g = _spawnedAgents[i];
            if (g == null) continue;
            var agent = g.GetComponent<SwarmAgent>();
            if (agent != null) agent.UpdateCenter(_rootTransform.position);
        }
    }

    private void SpawnAgents()
    {
        for (int i = 0; i < agentCount; i++)
        {
            if (swarmAgentPrefab == null) break;
            float angle = (i / (float)agentCount) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.4f, 0.4f);
            float r = visualRadius * UnityEngine.Random.Range(0.5f, 1f);
            Vector3 pos = _rootTransform.position + new Vector3(Mathf.Cos(angle) * r, spawnHeight + UnityEngine.Random.Range(-0.12f, 0.12f), Mathf.Sin(angle) * r);
            GameObject g = Instantiate(swarmAgentPrefab, pos, Quaternion.identity, agentsParent);
            var agent = g.GetComponent<SwarmAgent>();
            if (agent != null) agent.Initialize(_rootTransform.position, visualRadius, UnityEngine.Random.Range(1.6f, 3.0f), UnityEngine.Random.Range(0f, Mathf.PI * 2f));
            _spawnedAgents.Add(g);
        }
    }

    // ------------------ Área de daño (OnTriggerEnter/Exit) ------------------
    // Unity llamará a estos métodos solo si el collider está en el mismo GameObject.
    // Si el collider está en el root, el TriggerForwarder del root reenviará a RootOnTriggerEnter/Exit.
    private void OnTriggerEnter(Collider other)
    {
        RootOnTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        RootOnTriggerExit(other);
    }

    // Método público usado por el forwarder
    public void RootOnTriggerEnter(Collider other)
    {
        if (!_active) return;
        if (!other.CompareTag("Player")) return;

        _playerInside = true;
        if (_playerHealth != null)
        {
            _lastKnownPlayerHealth = _playerHealth.CurrentHealth;
            if (_damageCoroutine != null) StopCoroutine(_damageCoroutine);
            _damageCoroutine = StartCoroutine(DamageWhileInside());
        }
    }

    public void RootOnTriggerExit(Collider other)
    {
        if (!_active) return;
        if (!other.CompareTag("Player")) return;

        _playerInside = false;
        if (_damageCoroutine != null)
        {
            StopCoroutine(_damageCoroutine);
            _damageCoroutine = null;
        }
    }

    private IEnumerator DamageWhileInside()
    {
        if (_playerHealth == null) yield break;

        float intervalo = Mathf.Max(0.05f, damageTickInterval);
        float damagePerTick = damagePerSecond * intervalo;

        while (_playerInside && _active)
        {
            _lastDamageWasFromSwarm = true;
            _lastKnownPlayerHealth = _playerHealth.CurrentHealth;

            _playerHealth.TakeDamage(damagePerTick);

            yield return new WaitForSeconds(intervalo);

            _lastDamageWasFromSwarm = false;
            _lastKnownPlayerHealth = _playerHealth.CurrentHealth;
        }
    }

    // Si la vida del jugador baja por otra fuente -> dispersar
    private void OnPlayerHealthChanged(float newHealth, float maxHealth)
    {
        if (!_active || _playerHealth == null) return;

        if (newHealth < _lastKnownPlayerHealth - 0.0001f)
        {
            if (!_lastDamageWasFromSwarm)
            {
                DisperseAndDestroy();
            }
            else
            {
                _lastDamageWasFromSwarm = false;
            }
        }

        _lastKnownPlayerHealth = newHealth;
    }

    // Dispersión visual y destrucción del prefab root
    public void DisperseAndDestroy()
    {
        if (!_active) return;
        _active = false;
        _playerInside = false;
        if (_damageCoroutine != null) StopCoroutine(_damageCoroutine);
        PlayerHealth.OnHealthChanged -= OnPlayerHealthChanged;

        // Desuscribir del EnemyHealth host si corresponde
        if (_hostEnemyHealth != null)
        {
            _hostEnemyHealth.OnDeath -= HandleHostDeath;
            _hostEnemyHealth = null;
        }

        StartCoroutine(DisperseRoutine());
    }

    private IEnumerator DisperseRoutine()
    {
        foreach (var g in _spawnedAgents)
        {
            if (g == null) continue;
            var agent = g.GetComponent<SwarmAgent>();
            if (agent != null) agent.StartDisperse();
            else
            {
                var rb = g.GetComponent<Rigidbody>();
                if (rb != null) rb.AddExplosionForce(2f, _rootTransform.position, visualRadius * 1.5f);
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0.05f, disperseVisualDuration));

        // Destruir el root transform del enjambre (si existe)
        if (_rootTransform != null)
            Destroy(_rootTransform.gameObject);
        else
            Destroy(gameObject);
    }

    private void OnDisable()
    {
        PlayerHealth.OnHealthChanged -= OnPlayerHealthChanged;

        if (_forwarder != null && _forwarder.owner == this)
        {
            // opcional: limpiar owner del forwarder si lo dejamos
            _forwarder.owner = null;
        }

        // desuscribir del host EnemyHealth si sigue referenciado
        if (_hostEnemyHealth != null)
        {
            _hostEnemyHealth.OnDeath -= HandleHostDeath;
            _hostEnemyHealth = null;
        }
    }

    private void OnDestroy()
    {
        // aseguramos limpieza (por si acaso)
        PlayerHealth.OnHealthChanged -= OnPlayerHealthChanged;
        if (_hostEnemyHealth != null)
        {
            _hostEnemyHealth.OnDeath -= HandleHostDeath;
            _hostEnemyHealth = null;
        }
    }

    // ---- Helper: componente que reenvía OnTriggerEnter/Exit desde el collider root ----
    // Se añade dinámicamente al gameObject que contiene el collider (si es distinto).
    private class TriggerForwarder : MonoBehaviour
    {
        public SwarmController owner;
        public void Init(SwarmController c) { owner = c; }

        private void OnTriggerEnter(Collider other)
        {
            if (owner != null) owner.RootOnTriggerEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (owner != null) owner.RootOnTriggerExit(other);
        }
    }
}
