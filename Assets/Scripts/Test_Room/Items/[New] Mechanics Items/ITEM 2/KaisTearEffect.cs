using UnityEngine;

[CreateAssetMenu(fileName = "KaisTearEffect", menuName = "Item Effects/Combat/KaisTear")]
public class KaisTearEffect : ItemEffectBase
{
    #region Inspector Fields
    [Header("Olas de Escudo")]
    [SerializeField] private float waveSpeed = 5f;
    [SerializeField] private float timeToMaxWidth = 1.5f;
    [SerializeField] private float waveMaxWidth = 3f;
    [SerializeField] private float waveShieldTotalDuration = 4f;

    [Range(0.01f, 1f)]
    [SerializeField] private float waveShieldDamagePercent = 0.10f;

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
        PlayerCombatEvents.OnShieldThrown += HandleShieldThrown;
        PlayerCombatEvents.OnMeleeHit += HandleMeleeHit;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnShieldThrown -= HandleShieldThrown;
        PlayerCombatEvents.OnMeleeHit -= HandleMeleeHit;
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

    private void HandleMeleeHit(Vector3 playerPosition, Vector3 playerForward, float meleeDamage)
    {
        float waveDamage = meleeDamage * waveMeleeDamagePercent;
        Vector3 backDir = -playerForward;
        Vector3 spawnPos = new Vector3(playerPosition.x, 0.1f, playerPosition.z);

        if (ItemEffectPool.Instance != null)
        {
            ItemEffectPool.Instance.SpawnKaiMeleeWave(
                spawnPos,
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