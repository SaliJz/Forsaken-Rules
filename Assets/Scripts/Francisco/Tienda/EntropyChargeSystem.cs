using System;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EntropyChargeSystem : MonoBehaviour
{
    #region Inspector Fields

    [Header("Configuración")]
    [SerializeField] private EntropyConfig entropyConfig; 
    [SerializeField] private GameObject chargeVFX;

    #endregion

    #region Constants

    public const int MaxCharges = 3;

    #endregion

    #region Events

    public event Action<int, float, float> OnChargesChanged;
    public event Action OnChargesExpired;

    #endregion

    #region Private Fields

    private EnemyHealth enemyHealth;
    private int currentCharges = 0;
    private float timeRemaining = 0f;
    private float tickTimer = 0f;
    private float pendingDamagePerTick = 0f;
    private PlayerStatsManager cachedStats;
    private bool isActive = false;

    #endregion

    #region Properties

    public int CurrentCharges => currentCharges;
    public float TimeRemaining => timeRemaining;
    public EntropyConfig Config => entropyConfig;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        cachedStats = FindAnyObjectByType<PlayerStatsManager>();

        if (entropyConfig == null)
        {
            entropyConfig = Resources.Load<EntropyConfig>("Configs/EntropyConfig");
        }

        if (chargeVFX != null) chargeVFX.SetActive(false);
    }

    private void Update()
    {
        if (!isActive) return;

        if (enemyHealth.IsDead)
        {
            ClearCharges();
            return;
        }

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = entropyConfig.tickInterval;
            enemyHealth.TakeDamage(pendingDamagePerTick);
        }

        timeRemaining -= Time.deltaTime;
        NotifyChanged();

        if (timeRemaining <= 0f) ExpireCharges();
    }

    #endregion

    #region Public API

    public void ApplyCharge()
    {
        if (enemyHealth.IsDead) return;

        float baseDamage = cachedStats != null
            ? cachedStats.GetStat(StatType.AttackDamage) * cachedStats.GetStat(StatType.MeleeAttackDamage)
            : 10f;

        pendingDamagePerTick = baseDamage * (entropyConfig.damagePercent / 100f);

        if (currentCharges < MaxCharges) currentCharges++;

        float maxTotalTime = MaxCharges * entropyConfig.chargeDuration;
        timeRemaining = Mathf.Min(timeRemaining + entropyConfig.chargeDuration, maxTotalTime);

        isActive = true;
        tickTimer = entropyConfig.tickInterval;

        if (chargeVFX != null) chargeVFX.SetActive(true);
        NotifyChanged();
    }

    public void ClearCharges()
    {
        ResetState();
        OnChargesExpired?.Invoke();
        NotifyChanged();
    }

    #endregion

    #region Private Methods

    private void ExpireCharges()
    {
        ResetState();
        OnChargesExpired?.Invoke();
        NotifyChanged();
    }

    private void ResetState()
    {
        currentCharges = 0;
        timeRemaining = 0f;
        tickTimer = 0f;
        isActive = false;

        if (chargeVFX != null) chargeVFX.SetActive(false);
    }

    private void NotifyChanged()
    {
        OnChargesChanged?.Invoke(currentCharges, timeRemaining, MaxCharges * entropyConfig.chargeDuration);
    }

    #endregion
}