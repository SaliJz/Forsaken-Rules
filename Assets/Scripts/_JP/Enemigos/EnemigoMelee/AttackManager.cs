// AttackManager.cs
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona la corrutina de ataque. Se encarga de preparar, activar hitbox y limpiar.
/// MeleeEnemyController la usa para iniciar/cancelar ataques.
/// </summary>
public class AttackManager : MonoBehaviour
{
    Coroutine running;
    bool canceled = false;

    public void StartAttack(float prepareTime, float attackDuration, float cooldown,
                            HitboxSpawner spawner, int damage, Action onComplete, Action onCanceled)
    {
        if (running != null) return;
        canceled = false;
        running = StartCoroutine(AttackRoutine(prepareTime, attackDuration, cooldown, spawner, damage, onComplete, onCanceled));
    }

    public void CancelAttack()
    {
        canceled = true;
    }

    IEnumerator AttackRoutine(float prepareTime, float attackDuration, float cooldown,
                              HitboxSpawner spawner, int damage, Action onComplete, Action onCanceled)
    {
        if (spawner == null)
        {
            onCanceled?.Invoke();
            running = null;
            yield break;
        }

        // Preparación: crear hitbox (sin daño) y "preview" (color)
        spawner.Create(false, damage, attackDuration + 0.2f);

        float t = 0f;
        while (t < prepareTime)
        {
            if (canceled)
            {
                spawner.Cleanup();
                onCanceled?.Invoke();
                running = null;
                yield break;
            }
            t += Time.deltaTime;
            yield return null;
        }

        if (canceled)
        {
            spawner.Cleanup();
            onCanceled?.Invoke();
            running = null;
            yield break;
        }

        // Activar daño
        spawner.ActivateDamage(true);

        float atk = 0f;
        while (atk < attackDuration)
        {
            if (canceled)
            {
                spawner.Cleanup();
                onCanceled?.Invoke();
                running = null;
                yield break;
            }
            atk += Time.deltaTime;
            yield return null;
        }

        // Fin de ataque: limpiar hitbox
        spawner.Cleanup();

        // cooldown
        float cd = 0f;
        while (cd < cooldown)
        {
            if (canceled)
            {
                onCanceled?.Invoke();
                running = null;
                yield break;
            }
            cd += Time.deltaTime;
            yield return null;
        }

        onComplete?.Invoke();
        running = null;
    }
}
