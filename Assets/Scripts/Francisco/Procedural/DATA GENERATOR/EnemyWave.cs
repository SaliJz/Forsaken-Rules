using UnityEngine;

[System.Serializable]
public class EnemyWave
{
    public GameObject[] enemyPrefabs;
    public int enemyCount = 3;

    public EnemySpawnMode[] spawnModes;
}