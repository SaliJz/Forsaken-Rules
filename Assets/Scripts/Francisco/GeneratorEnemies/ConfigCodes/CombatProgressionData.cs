using UnityEngine;
using System.Collections.Generic;
using System;

public enum EnemySpawnMode
{
    General,
    Specific
}

[Serializable]
public class EnemyWaveDetail
{
    public GameObject EnemyPrefab;
    [Range(1, 20)] public int Count = 1;

    [Header("Modo de Spawn")]
    public EnemySpawnMode SpawnMode = EnemySpawnMode.General;
}

[Serializable]
public class PredefinedWave
{
    public List<EnemyWaveDetail> enemiesInWave;
}

[Serializable]
public class PredefinedCombatCombination
{
    public string CombinationName = "Combinacion #";

    [Header("Configuración de Oleadas")]
    public List<PredefinedWave> waves;
    public float timeBetweenWaves = 5f;
}