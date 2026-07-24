using System.Collections;
using UnityEngine;

/// <summary>
/// Clase centralizada que recibe y aplica todo el empuje (knockback) que sufre el jugador.
/// Es la UNICA fuente de verdad para calcular el modificador de empuje recibido
/// (StatType.KnockbackReceived) que proveen las reliquias/gangas a traves de PlayerStatsManager.
/// Enemigos y hazards siguen enviando su propia cantidad de fuerza base, pero el resultado final
/// siempre pasa por <see cref="GetModifiedKnockbackForce"/> antes de aplicarse.
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(PlayerMovement))]
public class PlayerKnockbackReceiver : MonoBehaviour
{
    private CharacterController cc;
    private PlayerMovement playerMovement;
    private PlayerStatsManager statsManager;
    private Coroutine activeKnockback;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        playerMovement = GetComponent<PlayerMovement>();
        statsManager = GetComponent<PlayerStatsManager>();
    }

    /// <summary>
    /// Calcula la fuerza final de empuje aplicando el modificador aditivo del jugador
    /// (StatType.KnockbackReceived, base 0 = sin cambio). Un valor positivo aumenta el empuje
    /// recibido, uno negativo lo reduce.
    /// </summary>
    /// <param name="baseForce">Fuerza original enviada por el enemigo/hazard.</param>
    /// <returns>Fuerza final ya modificada, nunca negativa.</returns>
    public float GetModifiedKnockbackForce(float baseForce)
    {
        float knockbackModifier = statsManager != null ? statsManager.GetStat(StatType.KnockbackReceived) : 0f;

        float finalForce = baseForce + knockbackModifier;

        return Mathf.Max(0f, finalForce);
    }

    /// <summary>
    /// Aplica un empuje directo al jugador usando el CharacterController, pasando primero
    /// la fuerza base por el modificador centralizado de empuje recibido.
    /// </summary>
    public void ApplyKnockback(Vector3 direction, float force, float duration = 0.25f)
    {
        if (playerMovement != null && playerMovement.IsDashing) return;

        float finalForce = GetModifiedKnockbackForce(force);

        if (activeKnockback != null) StopCoroutine(activeKnockback);

        activeKnockback = StartCoroutine(KnockbackRoutine(direction.normalized * finalForce, duration));
    }

    private IEnumerator KnockbackRoutine(Vector3 velocity, float duration)
    {
        float elapsed = 0f;

        float originalStepOffset = cc != null ? cc.stepOffset : 0.3f;
        if (cc != null) cc.stepOffset = 0.01f;

        while (elapsed < duration)
        {
            // Valida por si el jugador tira un dash a mitad del empuje
            if (playerMovement != null && playerMovement.IsDashing) break;

            if (cc != null && cc.enabled)
            {
                Vector3 moveVector = velocity;
                if (!cc.isGrounded)
                {
                    moveVector.y = -5f; // Fuerza de agarre al suelo
                }

                cc.Move(moveVector * Time.deltaTime);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (cc != null) cc.stepOffset = originalStepOffset;
    }
}