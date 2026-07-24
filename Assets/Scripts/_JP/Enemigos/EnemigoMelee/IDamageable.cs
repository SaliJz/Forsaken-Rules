using System;
using UnityEngine;

/// <summary>
/// Define el contrato para cualquier objeto en el juego que pueda recibir daño,
/// tener vida y morir.
/// </summary>
public interface IDamageable
{
    public float CurrentHealth { get; }
    public float MaxHealth { get; }

    /// <summary>
    /// Método universal para aplicar daño.
    /// NOTA: PlayerHealth tiene parámetros adicionales opcionales que no están en la interfaz base
    /// </summary>
    /// <param name="damageAmount">La cantidad de daño a aplicar.</param>
    /// <param name="isCritical">Indica si el daño es crítico (para efectos visuales/sonoros).</param>
    /// <param name="attackDamageType">Tipo de ataque (Melee/Ranged)</param>
    public void TakeDamage(float damageAmount, bool isCritical = false, AttackDamageType attackDamageType = AttackDamageType.Melee);
}