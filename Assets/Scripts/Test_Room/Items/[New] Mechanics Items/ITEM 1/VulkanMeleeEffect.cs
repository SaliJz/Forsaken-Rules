using UnityEngine;

[CreateAssetMenu(fileName = "VulkanMeleeEffect", menuName = "Item Effects/Combat/VulkanMelee")]
public class VulkanMeleeEffect : ItemEffectBase
{
    public override void ApplyEffect(PlayerStatsManager statsManager) => CombatEventsManager.OnPlayerHitEnemy += HandleHit;
    public override void RemoveEffect(PlayerStatsManager statsManager) => CombatEventsManager.OnPlayerHitEnemy -= HandleHit;

    private void HandleHit(GameObject enemy, bool isMelee)
    {
        if (isMelee) enemy.GetComponentInParent<EntropyChargeSystem>()?.ApplyCharge();
    }
}