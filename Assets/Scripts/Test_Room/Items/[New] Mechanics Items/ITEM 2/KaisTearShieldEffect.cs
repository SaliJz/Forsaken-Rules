using UnityEngine;

[CreateAssetMenu(fileName = "KaisTearShieldEffect", menuName = "Item Effects/Combat/KaisTearShield")]
public class KaisTearShieldEffect : ItemEffectBase
{
    #region Inspector Fields

    [Header("Olas de Escudo")]
    [SerializeField] private float waveSpeed = 5f;
    [SerializeField] private float timeToMaxWidth = 1.5f;
    [SerializeField] private float waveMaxWidth = 3f;
    [SerializeField] private float waveShieldTotalDuration = 4f;

    [Range(0.01f, 1f)]
    [SerializeField] private float waveShieldDamagePercent = 0.10f;

    [Header("Compartido")]
    [SerializeField] private LayerMask enemyLayer;

    #endregion

    #region ItemEffectBase

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnShieldThrown += HandleShieldThrown;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnShieldThrown -= HandleShieldThrown;
    }

    #endregion

    #region Handlers

    private void HandleShieldThrown(Vector3 throwPosition, Vector3 direction, float playerBaseDamage)
    {
        float waveDamage = playerBaseDamage * waveShieldDamagePercent;

        if (ItemEffectPool.Instance != null)
        {
            ItemEffectPool.Instance.SpawnKaiShieldWaves(
                throwPosition,
                waveDamage,
                waveSpeed,
                waveMaxWidth,
                timeToMaxWidth,
                waveShieldTotalDuration,
                enemyLayer
            );
        }
    }

    #endregion
}