using UnityEngine;

[CreateAssetMenu(fileName = "NewEntityStats", menuName = "Stats/Morlock Stats")]
public class MorlockStats : ScriptableObject
{
    [Header("Salud")]
    public float health = 15f;

    [Header("Movimiento y Posicionamiento")]
    public float moveSpeed = 4f;
    public float optimalAttackDistance = 10f;
    public float teleportRange = 5f;

    [Header("Combate")]
    public float fireRate = 1f;
    public float projectileDamage = 1f;
    public float projectileSpeed = 12f;

    [Header("Efecto de Veneno")]
    public int poisonHitThreshold = 3;
    public float poisonInitialDamage = 2f;
    public float poisonResetTime = 5f;
}