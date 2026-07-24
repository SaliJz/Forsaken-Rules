using UnityEngine;

/// <summary>
/// Funcion que maneja las estadisticas del jugador.
/// </summary>
[CreateAssetMenu(fileName = "NewEntityStats", menuName = "Stats/Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("Vida")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    [Header("Ataque General")]
    public float attackDamage = 1.0f;
    public float attackSpeed = 1.0f;

    [Header("Ataque melee")]
    public float meleeAttackDamage = 10;
    public float meleeSpeed = 1f;
    public float meleeRadius = 0.8f;

    [Header("Ataque a distancia")]
    public float shieldAttackDamage = 10;
    public float shieldSpeed = 20f;
    public float shieldMaxDistance = 30f;
    public int shieldMaxRebounds = 1;
    public float shieldReboundRadius = 15f;

    [Header("Habilidades")]
    public float HealthDrainAmount = 2f;
    public float lifestealOnKillAmount = 5f;
    public bool isShieldBlockUpgradeActive = false;
    public float healthPerRoomRegenBase = 2f;

    [Header("Generales")]
    public float shopPriceReductionBase = 0f;
    public float luckStackBase = 0f;
    // --- VARIABLES DE STATS PENDIENTES DE CONEXION ---
    /*
    public float essenceCostReductionBase = 0f;
    */
    public float criticalChanceBase = 0f;
    public float criticalDamageMultiplierBase = 0f;
    public float dashRangeMultiplierBase = 1f;
    [Tooltip("Bonus plano (en unidades de distancia) que se suma a la distancia final del dash, luego de aplicar el multiplicador. Base = 0 (sin bonus). Permite reliquias/gangas que aumenten o reduzcan el alcance del dash de forma aditiva en vez de porcentual.")]
    public float dashRangeFlatBonusBase = 0f;

    [Header("Stats de combate")]
    [Tooltip("Multiplicador de daño recibido, expresado en unidades de porcentaje (100 = 100%, sin cambios). Se usa junto con ApplyModifier(isPercentage: true) en PlayerStatsManager para que las reliquias/gangas puedan reducir (Resistencia +) o aumentar (Resistencia -) el daño recibido.")]
    public float damageTakenBase = 0f;
    [Tooltip("Modificador aditivo del empuje (knockback) recibido por el jugador. Base = 0 (sin cambios respecto al empuje original). Los items pueden sumar (+) para recibir mas empuje o restar (-) para recibir menos. Se aplica como (1 + valor) sobre la fuerza original en PlayerKnockbackReceiver.")]
    public float knockbackReceivedBase = 0f;
    public float dashCooldownPostBase = 0f;
    public float meleeComboDisplacementBase = 1f;
    public float shieldPushForceBase = 0f;
    public float shieldReturnSpeedBase = 1f;
    public float staminaConsumptionBase = 1f;
}