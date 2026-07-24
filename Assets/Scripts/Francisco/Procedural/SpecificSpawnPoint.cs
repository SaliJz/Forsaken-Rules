using UnityEngine;

public class SpecificSpawnPoint : MonoBehaviour
{
    [Header("Enemy Code")]
    public MonoBehaviour enemyCode;

    [Header("Spawn Points")]
    public Transform[] spawnPoints;

    public bool MatchesEnemyPrefab(GameObject enemyPrefab)
    {
        if (enemyCode == null || enemyPrefab == null)
        {
            return false;
        }

        System.Type enemyCodeType = enemyCode.GetType();

        return enemyPrefab.GetComponent(enemyCodeType) != null ||
               enemyPrefab.GetComponentInChildren(enemyCodeType, true) != null;
    }

    private void OnDrawGizmos()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        Gizmos.color = enemyCode != null ? Color.green : Color.cyan;

        foreach (var point in spawnPoints)
        {
            if (point != null)
            {
                Gizmos.DrawSphere(point.position, 0.3f);
            }
        }
    }
}