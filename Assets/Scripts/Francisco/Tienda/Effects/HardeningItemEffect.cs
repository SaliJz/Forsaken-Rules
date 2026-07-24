using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "HardeningEffect", menuName = "Item Effects/Defense/Hardening")]
public class HardeningItemEffect : ItemEffectBase
{
    [Header("Reducción de Daño")]
    public float damageReductionAmount = 0.15f;

    [Header("Vida Temporal por Muerte")]
    public float temporaryHealthPerKill = 10f;
    public float maxTemporaryHealth = 30f;

    private const string DR_MODIFIER_KEY = "Hardening_DR";
    private readonly List<float> temporaryHealthStacks = new List<float>();

    private void OnEnable()
    {
        EffectID = "Endurecimiento";
        category = EffectCategory.Defense;
        if (string.IsNullOrEmpty(effectDescription))
        {
            effectDescription = "Reduce el daño recibido y otorga vida temporal al eliminar enemigos.";
        }
    }

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        statsManager.ApplyNamedModifier(DR_MODIFIER_KEY, StatType.Endurance, -damageReductionAmount, isPercentage: true);
        CombatEventsManager.OnEnemyKilled += HandleEnemyKilled;

        Debug.Log("[HardeningItemEffect] Aplicado: Reducción de Daño y vida temporal activada.");
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        statsManager.RemoveNamedModifier(DR_MODIFIER_KEY);
        CombatEventsManager.OnEnemyKilled -= HandleEnemyKilled;

        temporaryHealthStacks.Clear();

        Debug.Log("[HardeningItemEffect] Removido.");
    }

    public override string GetFormattedDescription()
    {
        return $"Reduce el daño recibido en <b>{damageReductionAmount * 100:F0}%</b>.\n" +
               $"Otorga <b>{temporaryHealthPerKill}</b> de vida temporal al eliminar un enemigo, hasta un máximo de <b>{maxTemporaryHealth}</b>.";
    }

    private void HandleEnemyKilled(GameObject killedEnemy, float enemyBaseHealth)
    {
        PlayerStatsManager statsManager = FindAnyObjectByType<PlayerStatsManager>();

        if (statsManager != null)
        {
            PlayerHealth playerHealth = statsManager.GetComponent<PlayerHealth>();

            if (playerHealth != null)
            {
                if (!playerHealth.HasAmuletOfEndurance) playerHealth.AcquireAmuletOfEndurance();
                playerHealth.AddTemporaryHealth(temporaryHealthPerKill, maxTemporaryHealth);
                Debug.Log($"[HardeningItemEffect] {killedEnemy.name} eliminado. Se intenta añadir {temporaryHealthPerKill} de vida temporal.");
            }
        }
    }
}