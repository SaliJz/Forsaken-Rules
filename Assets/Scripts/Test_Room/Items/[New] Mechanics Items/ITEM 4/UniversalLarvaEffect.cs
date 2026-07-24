using UnityEngine;

[CreateAssetMenu(fileName = "UniversalLarvaEffect", menuName = "Item Effects/Utility/Universal Larva")]
public class TripleLarvaImpactEffect : ItemEffectBase
{
    #region Settings
    [Header("Prefabs de Larvas")]
    public GameObject attackLarvaPrefab;
    public GameObject curativeLarvaPrefab;
    public GameObject kamikazeLarvaPrefab;
    #endregion

    #region Life Cycle
    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnEnemyKilledType += HandleKilledEnemy;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnEnemyKilledType -= HandleKilledEnemy;
    }
    #endregion

    #region Event Handlers
    private void HandleKilledEnemy(GameObject enemy, AttackDamageType damageType)
    {
        if (damageType == AttackDamageType.Nothing) return;

        Vector3 spawnPos = enemy.transform.position;

        if (damageType == AttackDamageType.Melee)
        {
            for (int i = 0; i < 4; i++)
            {
                SpawnLarva(kamikazeLarvaPrefab, spawnPos, 15f);
            }
        }
        else
        {
            for (int i = 0; i < 2; i++)
            {
                SpawnLarva(curativeLarvaPrefab, spawnPos, 15f);
            }

            SpawnLarva(attackLarvaPrefab, spawnPos, 15f);
        }
    }
    #endregion

    #region Logic
    private void SpawnLarva(GameObject prefab, Vector3 position, float damageValue)
    {
        if (prefab == null) return;

        GameObject larva = Instantiate(prefab, position, Quaternion.identity);

        larva.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        if (larva.TryGetComponent<ResurrectedLarva>(out var res))
            res.Initialize(damageValue);
        else if (larva.TryGetComponent<CurativeLarva>(out var cur))
            cur.Initialize(damageValue);
    }
    #endregion
}