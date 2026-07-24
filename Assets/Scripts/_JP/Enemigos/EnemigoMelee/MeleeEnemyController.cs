// MeleeEnemyController.cs
using UnityEngine;
using UnityEngine.AI;
using System;

/// <summary>
/// Control principal del enemigo (movimiento, estados, interacción con AttackManager/HitboxSpawner).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AttackManager))]
[RequireComponent(typeof(HitboxSpawner))]
public class MeleeEnemyController : MonoBehaviour, IDamageable
{
    public Transform player;
    public float chaseSpeed = 3.5f;
    public float stoppingDistance = 1.5f;

    [Header("Ataque")]
    public float attackPrepareTime = 0.6f;
    public float attackDuration = 0.25f;
    public float attackCooldown = 0.6f;
    public int attackDamage = 1;

    [Header("Vida / recibir")]
    public float maxHealth = 10;
    public float receiveResetTime = 1.0f;
    public Renderer[] renderersToColor;

    private NavMeshAgent agent;
    private AttackManager attackManager;
    HitboxSpawner spawner;

    private float currentHealth;
    private float receiveTimer = 0f;
    private Color[] originalColors;

    private enum State { Chase, Receiving, Dead }
    private State state = State.Chase;

    public float CurrentHealth
    {
        get => currentHealth;
        private set
        {
            if (currentHealth != value)
            {
                currentHealth = value;
            }
        }
    }

    public float MaxHealth => maxHealth;

    private void Awake()
    {
        GameObject go = GameObject.FindWithTag("Player");
        if (go != null)
            player = go.transform;
        else
            Debug.LogWarning("No se encontró ningún GameObject con tag 'Player'");

        agent = GetComponent<NavMeshAgent>();
        attackManager = GetComponent<AttackManager>();
        spawner = GetComponent<HitboxSpawner>();

        if (agent == null || attackManager == null || spawner == null)
        {
            Debug.LogError($"{name}: faltan componentes requeridos.");
            enabled = false;
            return;
        }

        // Ajustes iniciales (velocidad)
        agent.speed = chaseSpeed;
        currentHealth = maxHealth;

        if (renderersToColor != null && renderersToColor.Length > 0)
        {
            originalColors = new Color[renderersToColor.Length];
            for (int i = 0; i < renderersToColor.Length; i++)
            {
                if (renderersToColor[i] != null && renderersToColor[i].material != null)
                    originalColors[i] = renderersToColor[i].material.color;
            }
        }
    }

    // --- FIX: sincronizar posición del NavMeshAgent con el Transform para evitar "teletransporte" al play/instanciar ---
    private void Start()
    {
        if (agent != null)
        {
            // Asegurar que la posición interna del agente coincide con la posición del transform.
            // Warp evita que el agente se mueva abruptamente al "pegarse" al NavMesh.
            try
            {
                agent.Warp(transform.position);
                // nextPosition ayuda a mantener la sincronía inmediata si Unity lo necesita.
                agent.nextPosition = transform.position;
            }
            catch
            {
                // En caso raro de que Warp falle (ej. no hay NavMesh), nos limitamos a no romper la ejecución.
            }

            // Mantener detenido hasta que la lógica de chase lo active conscientemente.
            agent.isStopped = true;
        }
    }

    private void Update()
    {
        if (state == State.Dead) return;

        if (player == null)
        {
            agent.isStopped = true;
            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        if (state == State.Chase)
        {
            // Al entrar en chase, permitir que el agente se mueva y establecer destino.
            agent.isStopped = false;

            // Evitar llamar SetDestination si el agente no está listo o si ya tiene destino cercano.
            // Esto reduce micro-jumps por recalculaciones.
            if (agent.isOnNavMesh)
            {
                // Solo llamar si la diferencia es relevante
                if (!agent.hasPath || Vector3.Distance(agent.destination, player.position) > 0.5f)
                    agent.SetDestination(player.position);
            }
            else
            {
                // Fallback: intentar Warp si no está sobre el NavMesh (no romper)
                try { agent.Warp(transform.position); } catch { }
            }

            if (dist <= stoppingDistance)
            {
                // iniciar ataque via AttackManager
                agent.isStopped = true;
                SetColor(Color.red);
                attackManager.StartAttack(attackPrepareTime, attackDuration, attackCooldown, spawner, attackDamage,
                    onComplete: () =>
                    {
                        // volver a chase
                        SetColorToOriginal();
                        if (state != State.Receiving && state != State.Dead)
                        {
                            state = State.Chase;
                            agent.isStopped = false;
                        }
                    },
                    onCanceled: () =>
                    {
                        // si fue cancelado (por recibir daño), ya manejado en TakeDamage
                        SetColorToOriginal();
                    });
            }
        }
        else if (state == State.Receiving)
        {
            receiveTimer -= Time.deltaTime;
            if (receiveTimer <= 0f)
            {
                EndReceiving();
            }
        }
    }

    public void TakeDamage(float amount, bool isCritical = false, AttackDamageType attackDamageType = AttackDamageType.Melee)
    {
        if (state == State.Dead) return;

        currentHealth -= amount;

        // cancelar ataque en progreso
        attackManager.CancelAttack();
        spawner.Cleanup();

        // pasar a recibir
        state = State.Receiving;
        receiveTimer = receiveResetTime;
        agent.isStopped = true;
        SetColorToOriginal(); // mostrar color normal mientras recibe; cámbialo si quieres otro feedback

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void EndReceiving()
    {
        if (state == State.Dead) return;
        state = State.Chase;
        agent.isStopped = false;
    }

    private void Die()
    {
        state = State.Dead;
        agent.isStopped = true;
        spawner.Cleanup();
        // destruir o hacer pooling
        Destroy(gameObject, 1.0f);
    }

    private void SetColor(Color c)
    {
        if (renderersToColor == null) return;
        foreach (var r in renderersToColor)
        {
            if (r == null || r.material == null) continue;
            r.material.color = c;
        }
    }

    private void SetColorToOriginal()
    {
        if (renderersToColor == null || originalColors == null) return;
        for (int i = 0; i < renderersToColor.Length; i++)
        {
            if (renderersToColor[i] == null || renderersToColor[i].material == null) continue;
            if (i < originalColors.Length) renderersToColor[i].material.color = originalColors[i];
        }
    }
}
