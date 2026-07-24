using UnityEngine;

[CreateAssetMenu(fileName = "UniversalLarvaMeleeEffect", menuName = "Item Effects/Utility/Universal Larva Melee")]
public class ItemLarvaMeleeEffect : ItemEffectBase
{
    #region Settings
    [Header("Prefabs de Larvas")]
    public GameObject kamikazeLarvaPrefab;

    [Header("Configuración Ajustable")]
    [SerializeField] private AttackDamageType requiredDamageType = AttackDamageType.Melee;
    [SerializeField] private int larvaCount = 4;
    [SerializeField] private float larvaDamage = 15f;
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

        Vector3 spawnPos = enemy.transform.position;

        if (damageType == requiredDamageType)
        {
            for (int i = 0; i < larvaCount; i++)
            {
                SpawnLarva(kamikazeLarvaPrefab, spawnPos, larvaDamage);
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
            res.Initialize(damageValue);
        else if (larva.TryGetComponent<CurativeLarva>(out var cur))
            cur.Initialize(damageValue);
    }
    #endregion
}