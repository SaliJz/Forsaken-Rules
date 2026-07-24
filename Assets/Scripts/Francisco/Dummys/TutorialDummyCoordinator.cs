using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TutorialDummyCoordinator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TutorialCombatDummy[] slimes;
    [SerializeField] private DummyShooter[] slimeShooters;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Behavior Configuration")]
    [SerializeField] private float furyFireRateMultiplier = 1.5f;
    [SerializeField] private float furyProjectileSpeedMultiplier = 1.3f;

    [Header("Projectile Configuration")]
    [SerializeField] private string projectileTag = "EnemyProjectile";

    [Header("Dialogues")]
    [SerializeField] private DialogLine[] devilLaughDialog;
    [SerializeField] private DialogLine[] devilFreezeDialog;

    [Header("Events")]
    public UnityEvent OnFirstSlimeKilled;
    public UnityEvent OnAdultStageReached;
    public UnityEvent OnAllSlimesDefeated;

    private Dictionary<TutorialCombatDummy, bool> slimeStates = new Dictionary<TutorialCombatDummy, bool>();
    private Dictionary<TutorialCombatDummy, DummyShooter> slimeShooterMap = new Dictionary<TutorialCombatDummy, DummyShooter>();

    private bool firstSlimeKilled = false;
    private bool adultStageReached = false;
    private int aliveSlimesCount = 0;
    private PlayerHealth.LifeStage lastLifeStage;

    private Coroutine projectileCleanupCoroutine;

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        InitializeSlimes();
    }

    private void OnEnable()
    {
        PlayerHealth.OnLifeStageChanged += HandleLifeStageChanged;

        foreach (var slime in slimes)
        {
            if (slime != null)
            {
                slime.OnDummyKilled.AddListener(() => HandleSlimeDeath(slime));
            }
        }
    }

    private void OnDisable()
    {
        PlayerHealth.OnLifeStageChanged -= HandleLifeStageChanged;

        foreach (var slime in slimes)
        {
            if (slime != null)
            {
                slime.OnDummyKilled.RemoveListener(() => HandleSlimeDeath(slime));
            }
        }

        if (projectileCleanupCoroutine != null)
        {
            StopCoroutine(projectileCleanupCoroutine);
        }
    }

    private void Start()
    {
        lastLifeStage = playerHealth != null ? playerHealth.CurrentLifeStage : PlayerHealth.LifeStage.Young;
    }

    private void InitializeSlimes()
    {
        if (slimes.Length != slimeShooters.Length)
        {
            Debug.LogError("[TutorialDummyCoordinator] El número de slimes y shooters no coincide!");
            return;
        }

        aliveSlimesCount = slimes.Length;

        for (int i = 0; i < slimes.Length; i++)
        {
            if (slimes[i] != null && slimeShooters[i] != null)
            {
                slimeStates[slimes[i]] = true;
                slimeShooterMap[slimes[i]] = slimeShooters[i];
            }
        }

        Debug.Log($"[TutorialDummyCoordinator] Inicializados {aliveSlimesCount} slimes.");
    }

    private void HandleSlimeDeath(TutorialCombatDummy slime)
    {
        if (!slimeStates.ContainsKey(slime) || !slimeStates[slime])
        {
            return;
        }

        slimeStates[slime] = false;
        aliveSlimesCount--;

        Debug.Log($"[TutorialDummyCoordinator] Slime eliminado. Quedan {aliveSlimesCount} vivos.");

        //if (!firstSlimeKilled && !adultStageReached)
        //{
        //    firstSlimeKilled = true;
        //    StartCoroutine(HandleFirstSlimeKilled());
        //}
        //else
        if (aliveSlimesCount <= 0)
        {
            OnAllSlimesDefeated?.Invoke();
        }
    }

    private IEnumerator HandleFirstSlimeKilled()
    {
        OnFirstSlimeKilled?.Invoke();

        PauseAllShooters();
        DestroyAllProjectiles();

        if (projectileCleanupCoroutine != null)
        {
            StopCoroutine(projectileCleanupCoroutine);
        }
        projectileCleanupCoroutine = StartCoroutine(ContinuousProjectileCleanup());

        if (devilLaughDialog != null && devilLaughDialog.Length > 0)
        {
            if (DialogManager.Instance != null)
            {
                DialogManager.Instance.StartDialog(devilLaughDialog);
                while (DialogManager.Instance.IsActive) { yield return null; }
            }
        }

        if (projectileCleanupCoroutine != null)
        {
            StopCoroutine(projectileCleanupCoroutine);
            projectileCleanupCoroutine = null;
        }

        ResumeAllShooters();

        foreach (var kvp in slimeStates)
        {
            if (kvp.Value)
            {
                TutorialCombatDummy slime = kvp.Key;

                slime.SetAuraState(EnemyAuraState.Fury);
                MakeSlimeInvulnerable(slime, true);

                if (slimeShooterMap.ContainsKey(slime))
                {
                    IncreaseShooterAggressiveness(slimeShooterMap[slime]);
                }
            }
        }

        Debug.Log("[TutorialDummyCoordinator] Aura frenética aplicada a slimes sobrevivientes.");
    }

    private void HandleLifeStageChanged(PlayerHealth.LifeStage newStage)
    {
        if (lastLifeStage == PlayerHealth.LifeStage.Young &&
            newStage == PlayerHealth.LifeStage.Adult &&
            !adultStageReached)
        {
            adultStageReached = true;
            StartCoroutine(HandleAdultStageReached());
        }

        lastLifeStage = newStage;
    }

    private IEnumerator HandleAdultStageReached()
    {
        OnAdultStageReached?.Invoke();

        PauseAllShooters();
        DestroyAllProjectiles();

        if (projectileCleanupCoroutine != null)
        {
            StopCoroutine(projectileCleanupCoroutine);
        }
        projectileCleanupCoroutine = StartCoroutine(ContinuousProjectileCleanup());

        if (devilFreezeDialog != null && devilFreezeDialog.Length > 0)
        {
            if (DialogManager.Instance != null)
            {
                DialogManager.Instance.StartDialog(devilFreezeDialog);
                while (DialogManager.Instance.IsActive) { yield return null; }
            }
        }

        if (projectileCleanupCoroutine != null)
        {
            StopCoroutine(projectileCleanupCoroutine);
            projectileCleanupCoroutine = null;
        }

        foreach (var kvp in slimeStates)
        {
            if (kvp.Value)
            {
                TutorialCombatDummy slime = kvp.Key;

                slime.SetAuraState(EnemyAuraState.Static);

                if (slimeShooterMap.ContainsKey(slime))
                {
                    slimeShooterMap[slime].enabled = false;
                }

                MakeSlimeInvulnerable(slime, false);
            }
        }

        Debug.Log("[TutorialDummyCoordinator] Slimes congelados y vulnerables.");
    }

    #region Projectile Management

    private void DestroyAllProjectiles()
    {
        GameObject[] projectiles = GameObject.FindGameObjectsWithTag(projectileTag);

        foreach (GameObject projectile in projectiles)
        {
            Destroy(projectile);
        }

        Debug.Log($"[TutorialDummyCoordinator] {projectiles.Length} proyectiles destruidos.");
    }

    private IEnumerator ContinuousProjectileCleanup()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.1f); 

            GameObject[] projectiles = GameObject.FindGameObjectsWithTag(projectileTag);
            foreach (GameObject projectile in projectiles)
            {
                Destroy(projectile);
            }
        }
    }

    #endregion

    #region Shooter Control

    private void PauseAllShooters()
    {
        foreach (var shooter in slimeShooters)
        {
            if (shooter != null)
            {
                shooter.enabled = false;
            }
        }

        Debug.Log("[TutorialDummyCoordinator] Todos los shooters pausados.");
    }

    private void ResumeAllShooters()
    {
        foreach (var kvp in slimeStates)
        {
            if (kvp.Value && slimeShooterMap.ContainsKey(kvp.Key))
            {
                slimeShooterMap[kvp.Key].enabled = true;
            }
        }

        Debug.Log("[TutorialDummyCoordinator] Shooters reanudados.");
    }

    #endregion

    private void MakeSlimeInvulnerable(TutorialCombatDummy slime, bool invulnerable)
    {
        DummyInvulnerabilityController controller = slime.GetComponent<DummyInvulnerabilityController>();

        if (controller == null)
        {
            controller = slime.gameObject.AddComponent<DummyInvulnerabilityController>();
        }

        controller.SetInvulnerable(invulnerable);
    }

    private void IncreaseShooterAggressiveness(DummyShooter shooter)
    {
        if (shooter == null) return;

        var shooterType = shooter.GetType();

        var fireRateField = shooterType.GetField("fireRate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (fireRateField != null)
        {
            float currentFireRate = (float)fireRateField.GetValue(shooter);
            fireRateField.SetValue(shooter, currentFireRate / furyFireRateMultiplier);
        }

        var launchSpeedField = shooterType.GetField("launchSpeed",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (launchSpeedField != null)
        {
            float currentSpeed = (float)launchSpeedField.GetValue(shooter);
            launchSpeedField.SetValue(shooter, currentSpeed * furyProjectileSpeedMultiplier);
        }

        Debug.Log($"[TutorialDummyCoordinator] Agresividad aumentada para shooter {shooter.name}");
    }

    public void ResetCoordinator()
    {
        firstSlimeKilled = false;
        adultStageReached = false;
        aliveSlimesCount = slimes.Length;

        if (projectileCleanupCoroutine != null)
        {
            StopCoroutine(projectileCleanupCoroutine);
            projectileCleanupCoroutine = null;
        }

        foreach (var slime in slimes)
        {
            if (slime != null)
            {
                slimeStates[slime] = true;
                slime.SetAuraState(EnemyAuraState.Default);

                if (slimeShooterMap.ContainsKey(slime))
                {
                    slimeShooterMap[slime].enabled = true;
                }
            }
        }
    }
}