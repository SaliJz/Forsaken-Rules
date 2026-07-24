using System.Collections.Generic;

[System.Serializable]
public class CombatContents
{
    public List<EnemyWave> waves = new List<EnemyWave>();
    public float timeBetweenWaves = 5f;
}