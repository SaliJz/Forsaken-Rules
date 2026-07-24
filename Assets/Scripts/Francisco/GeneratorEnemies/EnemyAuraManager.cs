using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyAuraManager : MonoBehaviour
{
    private EnemyHealth enemyHealth;
    private DevilConfiguration config;

    public DevilAuraType ActiveAura { get; private set; } = DevilAuraType.None;
    public ResurrectionLevel ActiveResurrectionLevel { get; private set; } = ResurrectionLevel.None;

    public float MoveSpeedMultiplier { get; private set; } = 1.0f;
    public float AttackSpeedMultiplier { get; private set; } = 1.0f;
    public float DamageReductionPercent { get; private set; } = 0.0f;
    public float StunningStunChance { get; private set; } = 0.0f;
    public float CritDamageMultiplier { get; private set; } = 1.0f;

    private Coroutine regenCoroutine;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth == null)
        {
            Debug.LogError("[EnemyAuraManager] requiere un componente EnemyHealth en el mismo GameObject.", this);
            Destroy(this);
        }
        config = Resources.Load<DevilConfiguration>("DevilConfig");
        if (config == null)
        {
            Debug.LogError("[EnemyAuraManager] No se encontro DevilConfiguration en Resources.");
        }
    }

    public void ApplyAura(DevilAuraType aura, ResurrectionLevel level)
    {
        if (config == null) return;

        ActiveAura = aura;
        ActiveResurrectionLevel = level;

        MoveSpeedMultiplier = 1.0f;
        AttackSpeedMultiplier = 1.0f;
        DamageReductionPercent = 0.0f;
        StunningStunChance = 0.0f;
        CritDamageMultiplier = 1.0f;

        if (regenCoroutine != null) StopCoroutine(regenCoroutine);

        switch (aura)
        {
            case DevilAuraType.Frenzy:
                MoveSpeedMultiplier += config.FrenzySpeedIncrease;
                AttackSpeedMultiplier += config.FrenzySpeedIncrease;
                break;
            case DevilAuraType.Hardening:
                DamageReductionPercent = config.HardeningDamageReduction;
                if (enemyHealth != null)
                {
                    enemyHealth.ApplyDamageReduction(DamageReductionPercent, -1f);
                }
                regenCoroutine = StartCoroutine(RegenerationRoutine());
                break;
            case DevilAuraType.Stunning:
                StunningStunChance = config.StunningStunChance;
                CritDamageMultiplier += config.StunningCritDamageIncrease;
                break;
            case DevilAuraType.Explosive:
            case DevilAuraType.PartialResurrection:
                break;
        }

        BroadcastMessage("UpdateAuraStats", SendMessageOptions.DontRequireReceiver);
        ReportDebug($"Aura '{ActiveAura}' aplicada. Multiplicador de Velocidad: {MoveSpeedMultiplier}", 2);
    }

    private IEnumerator RegenerationRoutine()
    {
        if (enemyHealth == null || config == null) yield break;

        float regenRate = config.HardeningHealthRegenPerSecond;
        float delay = 1f;

        while (true)
        {
            yield return new WaitForSeconds(delay);
            if (!enemyHealth.IsDead)
            {
                enemyHealth.Heal(enemyHealth.MaxHealth * regenRate);
            }
        }
    }

    public void HandleDeathEffect(Transform enemyTransform, float enemyBaseHealth) 
    {
        if (config == null) return;

        if (ActiveAura == DevilAuraType.Explosive)
        {
            ExplodeOnDeath(enemyTransform.position, enemyBaseHealth);
        }

        if (ActiveAura == DevilAuraType.PartialResurrection && ActiveResurrectionLevel != ResurrectionLevel.None)
        {
            ResurrectAsMinions(enemyTransform);
        }
    }

    private void ExplodeOnDeath(Vector3 position, float enemyBaseHealth)
    {
        if (config.ExplosiveVFXPrefab == null)
        {
            ReportDebug("ExplosiveVFXPrefab no asignado. No se puede crear explosión.", 3);
            return;
        }

        GameObject explosionHandlerGO = new GameObject($"ExplosionAuraHandler_{gameObject.name}");
        explosionHandlerGO.transform.position = position;

        ExplosionDelayHandler handler = explosionHandlerGO.AddComponent<ExplosionDelayHandler>();

        handler.StartExplosion(
            config.ExplosiveDamagePercent,
            config.ExplosiveRadius,
            config.ExplosiveVFXPrefab,
            enemyBaseHealth,
            0f 
        );

        ReportDebug($"Aura Explosiva: Lógica transferida a ExplosionAuraHandler.", 1);
    }

    private void ResurrectAsMinions(Transform spawnCenter)
    {
        if (config.EscurridizoPrefab == null)
        {
            ReportDebug("Prefab de Escurridizo no asignado. No se pueden generar minions.", 3);
            return;
        }

        ReportDebug($"Resurrección Parcial: Generados {config.ResurrectionSplitCount} minions (Nivel {ActiveResurrectionLevel}).", 3);
    }

    private void OnDestroy()
    {
        if (regenCoroutine != null)
        {
            StopCoroutine(regenCoroutine);
            regenCoroutine = null;
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1: Debug.Log($"[EnemyAuraManager] {message}"); break;
            case 2: Debug.LogWarning($"[EnemyAuraManager] {message}"); break;
            case 3: Debug.LogError($"[EnemyAuraManager] {message}"); break;
        }
    }
}