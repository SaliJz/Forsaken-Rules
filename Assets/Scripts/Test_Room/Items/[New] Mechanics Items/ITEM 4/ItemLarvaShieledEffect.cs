using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DamageTypeSpawnSettings
{
    public AttackDamageType damageType;
    public List<DashSpawnSettings> spawns;
}

[CreateAssetMenu(fileName = "UniversalLarvaShieldEffect", menuName = "Item Effects/Utility/Universal Larva Shield")]
public class ItemLarvaShieledEffect : ItemEffectBase
{
    #region Settings

    [Header("Configuración de Spawns por Daño")]
    [SerializeField] private List<DamageTypeSpawnSettings> damageSpawnConfigs;

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
        if (enemy == null) return;

        //if (enemy.GetComponent<Larva>() != null) return;

        var config = damageSpawnConfigs.Find(x => x.damageType == damageType);

        if (config != null)
        {
            Vector3 spawnPos = enemy.transform.position;
            foreach (var spawn in config.spawns)
            {
                for (int i = 0; i < spawn.spawnCount; i++)
                {
                    SpawnLarva(spawn.larvaPrefab, spawnPos, 15f);
                }
            }
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
        {
            res.Initialize(damageValue);
        }
        else if (larva.TryGetComponent<CurativeLarva>(out var cur))
        {
            cur.Initialize(damageValue);
        }
    }

    #endregion
}