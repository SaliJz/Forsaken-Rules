using System;
using UnityEngine;

public static class PlayerCombatEvents
{
    #region Shield Events

    public static event Action<Vector3, Vector3, float> OnShieldThrown;
    public static void RaiseShieldThrown(Vector3 position, Vector3 direction, float playerBaseDamage)
        => OnShieldThrown?.Invoke(position, direction, playerBaseDamage);

    public static event Action<Vector3, float> OnShieldMoved;
    public static void RaiseShieldMoved(Vector3 position, float playerBaseDamage)
        => OnShieldMoved?.Invoke(position, playerBaseDamage);

    public static event Action OnShieldLanded;
    public static void RaiseShieldLanded()
        => OnShieldLanded?.Invoke();

    #endregion

    #region Melee Events

    public static event Action<Vector3, Vector3, float> OnMeleeHit;
    public static void RaiseMeleeHit(Vector3 playerPosition, Vector3 playerForward, float meleeDamage)
        => OnMeleeHit?.Invoke(playerPosition, playerForward, meleeDamage);

    #endregion

    #region Dash Events

    public static event Action<Vector3, Vector3> OnDashStarted;
    public static void RaiseDashStarted(Vector3 playerPosition, Vector3 dashDirection)
        => OnDashStarted?.Invoke(playerPosition, dashDirection);

    #endregion
}