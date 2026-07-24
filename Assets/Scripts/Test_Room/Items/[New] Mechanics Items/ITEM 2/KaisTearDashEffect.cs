using UnityEngine;

[CreateAssetMenu(fileName = "KaisTearDashEffect", menuName = "Item Effects/Combat/KaisDash")]
public class KaisTearDashEffect : ItemEffectBase
{
    #region Inspector Fields

    [Header("Ola de Dash")]
    [SerializeField] private float waveSpeed = 8f;
    [SerializeField] private float timeToMaxWidth = 0.4f;
    [SerializeField] private float waveMaxWidth = 7f;
    [SerializeField] private float waveTotalDuration = 1.2f;

    [Range(0.01f, 3f)]
    [SerializeField] private float waveDashDamagePercent = 0.75f;

    [Header("Compartido")]
    [SerializeField] private LayerMask enemyLayer;

    #endregion

    #region ItemEffectBase

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        _statsManager = statsManager;
        PlayerCombatEvents.OnDashStarted += HandleDashStarted;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnDashStarted -= HandleDashStarted;
        _statsManager = null;
    }

    #endregion

    #region Private

    private PlayerStatsManager _statsManager;

    private void HandleDashStarted(Vector3 playerPosition, Vector3 dashDirection)
    {
        float waveDamage = 50f * waveDashDamagePercent;

        if (ItemEffectPool.Instance != null)
        {
            ItemEffectPool.Instance.SpawnKaiDashWave(
                playerPosition,
                dashDirection,
                waveDamage,
                waveSpeed,
                waveMaxWidth,
                timeToMaxWidth,
                waveTotalDuration,
                enemyLayer
            );
        }
    }

    #endregion
}