using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "DevilConfig", menuName = "Devil/Manipulation Configuration")]
public class DevilConfiguration : ScriptableObject
{
    [Header("Regiones Generales de Activación")]
    public int CooldownInRooms = 2;
    public int MaxCleanRoomsCondition = 2; 

    [Header("Probabilidades de Manipulación")]
    [Range(0f, 1f)] public float AuraProbability = 0.75f; 

    [Header("Reglas de Aura Enemiga")]
    [Range(0f, 1f)] public float AuraEnemyCoveragePercent = 0.70f;
    [Range(0f, 1f)] public float AuraEnemyHealthReduction = 0.20f;
    public int HealthRewardPerAuraEnemyKill = 2; 

    [Header("Aura Frenética")]
    [Range(0f, 1f)] public float FrenzySpeedIncrease = 0.15f;

    [Header("Aura de Endurecimiento")]
    [Range(0f, 1f)] public float HardeningDamageReduction = 0.20f;
    [Range(0f, 1f)] public float HardeningHealthRegenPerSecond = 0.05f; 

    [Header("Aura de Aturdimiento")]
    [Range(0f, 2f)] public float StunningCritDamageIncrease = 0.50f; 
    [Range(0f, 1f)] public float StunningStunChance = 0.20f; 

    [Header("Aura Explosiva")]
    [Range(0f, 1f)] public float ExplosiveDamagePercent = 0.20f; 
    public float ExplosiveRadius = 10f;
    public GameObject ExplosiveVFXPrefab;

    [Header("Aura de Resurrección Parcial")]
    [Range(0f, 1f)] public float ResurrectionChance = 0.30f; 
    public int ResurrectionSplitCount = 3; 
    public float EscurridizoLifeTime = 5f; 
    public GameObject EscurridizoPrefab; 

    [Header("Efectos de Escurridizos")]
    [Range(0f, 1f)] public float SlowPercent = 0.10f;
    public float SlowDuration = 5f;
    public float SlowImpactDamage = 1f;
    public float PoisonDuration = 1f;
    public float PoisonFrequency = 4f; 
    public float PoisonDamagePerTick = 0.25f;
    public float ControlImpactDamage = 1f;

    [Header("Probabilades de distorción")]
    public float AbyssalConfusion_Chance = 0.125f;
    public float FloorOfTheDamned_Chance = 0.25f;
    public float DeceptiveDarkness_Chance = 0.25f;
    public float SealedLuck_Chance = 0.0625f;
    public float WitheredBloodthirst_Chance = 0.125f;
    public float InfernalJudgement_Chance = 0.1875f;
}