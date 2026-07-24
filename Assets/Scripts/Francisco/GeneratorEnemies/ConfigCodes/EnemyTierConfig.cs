using UnityEngine;
using System;

[Serializable]
public class EnemyTierConfig
{
    public string TierName; 
    public GameObject EnemyPrefab;

    [Header("Pesos y Proporción")]
    public float SpawnWeight = 1f;
}