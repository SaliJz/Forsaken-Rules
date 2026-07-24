using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DashSpawnSettings
{
    public GameObject larvaPrefab;
    public int spawnCount = 1;
}

[System.Serializable]
public class DashLevel
{
    public List<DashSpawnSettings> spawns;
}

[CreateAssetMenu(fileName = "UniversalLarvaDashEffect", menuName = "Item Effects/Utility/Universal Larva Dash")]
public class ItemLarvaDashEffect : ItemEffectBase
{
    #region Settings

    [Header("Larva stats")]
    [SerializeField] private float larvaHealth = 15f;

    [Header("Larva lifecycle")]
    [SerializeField] private float resetIdleTime = 2f;
    [SerializeField] private float larvaCooldown = 1.5f;

    [Header("Dash Configuration")]
    [SerializeField] private List<DashLevel> dashLevels;

    #endregion

    #region Private Fields

    private int dashCount = 0;
    private float lastDashTime = -999f;
    private float nextAvailableTime = -999f;

    #endregion

    #region ItemEffectBase

    private void OnEnable()
    {
        EffectID = "Larva Dash";
        category = EffectCategory.Combat;
    }

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        dashCount = 0;
        lastDashTime = -999f;
        nextAvailableTime = -999f;
        PlayerCombatEvents.OnDashStarted += HandleDashStarted;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnDashStarted -= HandleDashStarted;
    }

    #endregion

    #region Dash Handler

    private void HandleDashStarted(Vector3 playerPosition, Vector3 _)
    {
        float now = Time.time;

        if (now < nextAvailableTime) return;

        if (now - lastDashTime > resetIdleTime) dashCount = 0;

        lastDashTime = now;
        nextAvailableTime = now + larvaCooldown;
        dashCount++;

        int index = Mathf.Min(dashCount - 1, dashLevels.Count - 1);

        if (index >= 0 && index < dashLevels.Count)
        {
            foreach (var spawn in dashLevels[index].spawns)
            {
                for (int i = 0; i < spawn.spawnCount; i++)
                {
                    SpawnLarva(spawn.larvaPrefab, playerPosition);
                }
            }
        }
    }

    #endregion

    #region Logic

    private void SpawnLarva(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;

        GameObject larva = Object.Instantiate(prefab, position, Quaternion.identity);
        larva.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        if (larva.TryGetComponent<ResurrectedLarva>(out var res))
        {
            res.Initialize(larvaHealth);
        }
        else if (larva.TryGetComponent<CurativeLarva>(out var cur))
        {
            cur.Initialize(larvaHealth);
        }
    }

    #endregion
}