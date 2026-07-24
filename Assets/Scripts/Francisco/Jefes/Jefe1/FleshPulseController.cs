using System.Collections.Generic;
using UnityEngine;

public class FleshPulseController : MonoBehaviour
{
    [Header("Configuración Global")]
    [SerializeField] private LayerMask _obstacleLayer;
    [SerializeField] private LayerMask _playerLayer;
    [SerializeField] private float speedMultiplier = 1.0f;

    // Datos recibidos del Boss
    private float _maxRadius;
    private float _expansionDuration;
    private float _damage;
    private float _slowPercentage;
    private float _slowDuration;

    private bool _isExpanding = false;
    private float _expansionSpeed;
    private float _timer = 0f;

    private FleshPulseNerve[] _nerves;

    // Evita que múltiples brazos dañen al mismo jugador
    private HashSet<GameObject> _hitTargets = new HashSet<GameObject>();

    public void Initialize(float maxRadius, float duration, float damage, float slowPercent, float slowDur)
    {
        _maxRadius = maxRadius;
        _expansionDuration = duration;
        _damage = damage;
        _slowPercentage = slowPercent;
        _slowDuration = slowDur;

        _expansionSpeed = (_maxRadius / _expansionDuration) * speedMultiplier;

        _nerves = GetComponentsInChildren<FleshPulseNerve>();
        foreach (var arm in _nerves)
        {
            arm.Initialize(this, _obstacleLayer, _playerLayer);
        }

        transform.localScale = Vector3.one;
        _isExpanding = true;
    }

    private void Update()
    {
        if (!_isExpanding) return;

        _timer += Time.deltaTime;

        float growthStep = _expansionSpeed * Time.deltaTime;

        foreach (var nerve in _nerves)
        {
            if (nerve != null)
            {
                nerve.Expand(growthStep, _maxRadius);
            }
        }

        if (_timer >= _expansionDuration + 0.5f)
        {
            _isExpanding = false;
            Destroy(gameObject, 0.5f);
        }
    }

    public void TryHitPlayer(GameObject player)
    {
        if (_hitTargets.Contains(player)) return;

        _hitTargets.Add(player);
        ApplyEffects(player);
    }

    private void ApplyEffects(GameObject player)
    {
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        PlayerStatsManager stats = player.GetComponent<PlayerStatsManager>();
        PlayerMovement movement = player.GetComponent<PlayerMovement>();

        // Lógica de daño real
        if (health != null)
        {
            ExecuteAttack(player, _damage);
            health.IsMarkedByAstaroth = true;
        }

        if (stats != null)
        {
            float currentSpeed = stats.GetStat(StatType.MoveSpeed);
            float slowAmount = currentSpeed * -_slowPercentage;
            string uniqueKey = $"SlowEffect_{GetInstanceID()}";

            stats.ApplyTimedModifier(uniqueKey, StatType.MoveSpeed, slowAmount, _slowDuration);
        }

        if (movement != null)
        {
            movement.DisableDashForDuration(_slowDuration);
        }

        Debug.Log($"<color=red>[FleshPulseController] Jugador {player.name} golpeado por Nerves Pulse: Daño {_damage}, Slow {_slowPercentage * 100}% por {_slowDuration} segundos.</color>");
    }

    private void ExecuteAttack(GameObject target, float damageAmount)
    {
        if (target.TryGetComponent<PlayerHealth>(out var health))
        {
            // Verificar si el ataque es bloqueado
            if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) 
                && blockSystem.IsBlocking 
                && blockSystem.CanBlockAttack(transform.position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount);

                if (remainingDamage > 0f)
                {
                    health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                }

                Debug.Log($"<color=red>[FleshPulseController] Ataque bloqueado por el jugador.</color>");
                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
        else if (target.TryGetComponent<PlayerHealth>(out var healthOnly))
        {
            healthOnly.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
    }
}