using UnityEngine;

[CreateAssetMenu(fileName = "VulkanEntropyEffect", menuName = "Item Effects/Combat/VulkanEntropy")]
public class VulkanEntropyEffect : ItemEffectBase
{
    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnPlayerHitEnemy += HandleHit;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnPlayerHitEnemy -= HandleHit;
    }

    private void HandleHit(GameObject enemy, bool isMelee)
    {
        EntropyChargeSystem chargeSystem = enemy.GetComponentInParent<EntropyChargeSystem>();

        if (chargeSystem != null)
        {
            chargeSystem.ApplyCharge();
        }
    }
}