using UnityEngine;

[CreateAssetMenu(fileName = "KaisTearMeleeEffect", menuName = "Item Effects/Combat/KaisMelee")]
public class KaisTearMeleeEffect : ItemEffectBase
{
    #region Inspector Fields

    [Header("Ola de Melee")]
    [SerializeField] private float waveMeleeMaxWidth = 2f;
    [SerializeField] private float waveMeleeDuration = 0.3f;
    [SerializeField] private float waveMeleeSpeed = 3f;

    [Range(0.01f, 2f)]
    [SerializeField] private float waveMeleeDamagePercent = 0.30f;

    [Header("Compartido")]
    [SerializeField] private LayerMask enemyLayer;

    #endregion

    #region ItemEffectBase

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnMeleeHit += HandleMeleeHit;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnMeleeHit -= HandleMeleeHit;
    }

    #endregion

    #region Handlers

    private void HandleMeleeHit(Vector3 playerPosition, Vector3 playerForward, float meleeDamage)
    {
        float waveDamage = meleeDamage * waveMeleeDamagePercent;
        Vector3 backDir = -playerForward;

        if (ItemEffectPool.Instance != null)
        {
            ItemEffectPool.Instance.SpawnKaiMeleeWave(
                playerPosition,
                backDir,
                waveDamage,
                waveMeleeSpeed,
                waveMeleeMaxWidth,
                waveMeleeDuration,
                enemyLayer
            );
        }
    }

    #endregion
}