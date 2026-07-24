using UnityEngine;

[CreateAssetMenu(fileName = "DashFireEffect", menuName = "Item Effects/Combat/DashFire")]
public class DashFiretemEffect : ItemEffectBase
{
    #region Inspector Fields

    [Header("Daño")]
    [Tooltip("Daño total que se aplicará por cada segundo")]
    [SerializeField] private float damagePerSecond = 1f;

    [Header("Expansion")]
    [SerializeField] private float expandDuration = 0.4f;
    [SerializeField] private float maxRadius = 3f;

    [Header("Permanencia")]
    [SerializeField] private float stayDuration = 2.5f;
    [Tooltip("Cada cuántos segundos hace daño.")]
    [SerializeField] private float tickInterval = 0.3f;

    [Header("Compartido")]
    [SerializeField] private LayerMask enemyLayer;

    #endregion

    #region ItemEffectBase

    private void OnEnable()
    {
        EffectID = "Tierra Ardiente";
        category = EffectCategory.Combat;
        if (string.IsNullOrEmpty(effectDescription))
        {
            effectDescription = "Al dashear deja un circulo de tierra ardiente que se expande y quema a los enemigos que permanecen en el.";
        }
    }

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnDashStarted += HandleDash;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnDashStarted -= HandleDash;
    }

    public override string GetFormattedDescription()
    {
        return $"Al dashear genera un circulo de tierra ardiente que hace <b>{damagePerSecond}</b> de DPS y se expande hasta <b>{maxRadius}</b> unidades en <b>{expandDuration}s</b>, ";
    }

    #endregion

    #region Private Methods

    private void HandleDash(Vector3 playerPosition, Vector3 _)
    {
        if (ItemEffectPool.Instance == null) return;

        ItemEffectPool.Instance.SpawnDashFire(playerPosition, damagePerSecond, expandDuration,
                                      maxRadius, stayDuration, tickInterval, enemyLayer);
    }

    #endregion
}