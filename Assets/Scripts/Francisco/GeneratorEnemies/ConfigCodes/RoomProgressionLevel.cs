using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class RoomProgressionLevel
{
    public enum EnemyGenerationMode
    {
        ProceduralFromPool,
        PredefinedCombination,
        CombinationAndProcedural
    }

    public int startRoomNumber = 1;

    [Header("MODO DE GENERACIÓN DE ENEMIGOS")]
    public EnemyGenerationMode GenerationMode = EnemyGenerationMode.ProceduralFromPool;

    [Header("Generación PROCEDURAL")]
    public int minWaves = 1;
    public int maxWaves = 2;
    public float timeBetweenWaves = 5f;

    [Header("Contenido de la Ola")]
    public int minEnemyCountPerWave = 3;
    public int maxEnemyCountPerWave = 6;
    public List<EnemyTierConfig> availableEnemyPool;

    [Range(1f, 5f)]
    public float totalDifficultyMultiplier = 1f;

    [Header("Contenido Predefinido")]
    public List<PredefinedCombatCombination> predefinedCombinations;
}