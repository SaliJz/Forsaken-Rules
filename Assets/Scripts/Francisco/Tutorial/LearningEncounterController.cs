using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System;

public interface IPlayerSpecialAbility
{
    event Action OnAbilityActivated;
    event Action OnAbilityDeactivated;
    bool IsActive { get; }
}

public class LearningEncounterController : MonoBehaviour
{
    private enum EncounterState
    {
        Phase1_Intro,
        Phase2_AwaitingSkill,
        Phase3_LearningCombat,
        Phase4_Completed
    }
    private EncounterState currentState = EncounterState.Phase1_Intro;
    private bool hasRevivedOnce = false;

    [Header("Dependencies")]
    [SerializeField] private DummyArmor slimeArmor;
    [SerializeField] private MonoBehaviour playerAbilityScript;
    private IPlayerSpecialAbility playerAbility;

    [Header("Objeto por Muerte Incorrecta")]
    [SerializeField] private GameObject specialObjectToActivate;
    [SerializeField] private float activationDuration = 2f;
    private Coroutine objectDeactivationCoroutine;

    [Header("Phase 1: Superarmor Intro")]
    [SerializeField] private int requiredInitialHits = 3;
    private int currentHitCount = 0;
    public DialogLine[] dialogue1_Intro;

    [Header("Phase 2 & 3: Special Ability")]
    public DialogLine[] dialogue2_Contract;
    public DialogLine[] dialogue3_DiabloLaugh;
    private Coroutine specialAbilityTimerCoroutine;

    [Header("Phase 4: Completion")]
    public DialogLine[] dialogue4_Success;

    [Header("Player Damage Dialogue Settings")]
    public DialogLine[] dialogue_OnPlayerDamaged;
    private bool playerDamageDialogueTriggered = false;

    [Header("Events")]
    public UnityEvent OnDisplaySkillReminder;
    public UnityEvent OnDisplayMeleeReminderBefore;
    public UnityEvent OnDisplayMeleeReminderAfter;
    public UnityEvent OnEncounterCompleteBefore;
    public UnityEvent OnEncounterComplete;
    public UnityEvent OnPlayerDamagedDialogBefore;
    public UnityEvent OnPlayerDamagedDialogAfter;

    private void Start()
    {
        if (slimeArmor == null || playerAbilityScript == null)
        {
            enabled = false;
            return;
        }

        playerAbility = playerAbilityScript as IPlayerSpecialAbility;
        if (playerAbility == null)
        {
            enabled = false;
            return;
        }

        if (specialObjectToActivate != null)
        {
            specialObjectToActivate.SetActive(false);
        }

        slimeArmor.OnHitByPlayer += HandleSlimeHit;
        slimeArmor.OnDummyDefeated.AddListener(HandleSlimeDefeated);
        playerAbility.OnAbilityActivated += ActivateSpecialMode;
        playerAbility.OnAbilityDeactivated += DeactivateSpecialMode;

        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnDamageReceived += HandlePlayerDamage;
        }
        else
        {
            PlayerHealth.OnPlayerInstantiated += HandlePlayerInstantiated;
        }

        StartCoroutine(InitializeEncounter());
    }

    private void OnDestroy()
    {
        PlayerHealth.OnPlayerInstantiated -= HandlePlayerInstantiated;
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnDamageReceived -= HandlePlayerDamage;
        }
    }

    private void HandlePlayerInstantiated(PlayerHealth playerHealth)
    {
        PlayerHealth.OnPlayerInstantiated -= HandlePlayerInstantiated;
        if (playerHealth != null)
        {
            playerHealth.OnDamageReceived += HandlePlayerDamage;
        }
    }

    private IEnumerator InitializeEncounter()
    {
        yield return new WaitForSeconds(0.1f);

        ActivateSlimeAfterIntro();
    }

    private void ActivateSlimeAfterIntro()
    {
        if (slimeArmor != null)
        {
            slimeArmor.gameObject.SetActive(true);
        }
    }

    private void SetState(EncounterState newState)
    {
        currentState = newState;

        switch (currentState)
        {
            case EncounterState.Phase2_AwaitingSkill:
                OnDisplaySkillReminder?.Invoke();
                break;

            case EncounterState.Phase3_LearningCombat:
                if (specialAbilityTimerCoroutine != null) StopCoroutine(specialAbilityTimerCoroutine);

                if (!hasRevivedOnce) OnDisplayMeleeReminderBefore?.Invoke();
                break;

            case EncounterState.Phase4_Completed:
                if (specialAbilityTimerCoroutine != null) StopCoroutine(specialAbilityTimerCoroutine);
                break;
        }
    }

    public void HandleSlimeHit(DamageType damageType, bool isFatal)
    {
        if (currentState == EncounterState.Phase1_Intro)
        {
            if (damageType == DamageType.Shield && !isFatal)
            {
                currentHitCount++;
                if (currentHitCount >= requiredInitialHits)
                {
                    if (DialogManager.Instance != null && dialogue1_Intro.Length > 0)
                    {
                        UnityEvent onIntroHitsFinished = new UnityEvent();
                        onIntroHitsFinished.AddListener(() => SetState(EncounterState.Phase2_AwaitingSkill));

                        DialogManager.Instance.StartDialog(dialogue1_Intro, onIntroHitsFinished);
                    }
                    else
                    {
                        SetState(EncounterState.Phase2_AwaitingSkill);
                    }
                }
            }
        }
    }

    private void HandlePlayerDamage(float damageAmount)
    {
        if (playerDamageDialogueTriggered || currentState == EncounterState.Phase4_Completed) return;

        playerDamageDialogueTriggered = true;

        if (DialogManager.Instance != null && dialogue_OnPlayerDamaged.Length > 0)
        {
            OnPlayerDamagedDialogBefore?.Invoke();
            DialogManager.Instance.StartDialog(dialogue_OnPlayerDamaged, OnPlayerDamagedDialogAfter);
        }
    }

    private void HandleSlimeDefeated()
    {
        bool isMeleeKill = slimeArmor.LastAttackDamageType == AttackDamageType.Melee;

        if (currentState == EncounterState.Phase3_LearningCombat && isMeleeKill)
        {
            OnEncounterCompleteBefore?.Invoke();

            if (DialogManager.Instance != null && dialogue4_Success.Length > 0)
            {
                UnityEvent onFinishSuccess = new UnityEvent();
                onFinishSuccess.AddListener(() => SetState(EncounterState.Phase4_Completed));
                onFinishSuccess.AddListener(() => OnEncounterComplete?.Invoke());
                DialogManager.Instance.StartDialog(dialogue4_Success, onFinishSuccess);
            }
            else
            {
                SetState(EncounterState.Phase4_Completed);
                OnEncounterComplete?.Invoke();
            }
        }
        else
        {
            if (DialogManager.Instance != null && dialogue3_DiabloLaugh.Length > 0)
            {
                UnityEvent onFinishRevive = new UnityEvent();
                onFinishRevive.AddListener(ReviveSlime);
                DialogManager.Instance.StartDialog(dialogue3_DiabloLaugh, onFinishRevive);
            }
            else
            {
                ReviveSlime();
            }
        }
    }

    public void ActivateSpecialMode()
    {
        if (currentState == EncounterState.Phase2_AwaitingSkill)
        {
            if (DialogManager.Instance != null && dialogue2_Contract.Length > 0)
            {
                DialogManager.Instance.StartDialog(dialogue2_Contract, OnDisplayMeleeReminderAfter);
            }
            SetState(EncounterState.Phase3_LearningCombat);
        }
    }

    public void DeactivateSpecialMode()
    {
    }

    private void ReviveSlime()
    {
        if (specialObjectToActivate != null)
        {
            specialObjectToActivate.SetActive(true);

            if (objectDeactivationCoroutine != null)
            {
                StopCoroutine(objectDeactivationCoroutine);
            }
            objectDeactivationCoroutine = StartCoroutine(DeactivateSpecialObjectAfterTime());
        }

        hasRevivedOnce = true;

        slimeArmor.ResetArmorState();
        currentHitCount = 0;
        SetState(EncounterState.Phase3_LearningCombat);
    }

    private IEnumerator DeactivateSpecialObjectAfterTime()
    {
        yield return new WaitForSeconds(activationDuration);

        if (specialObjectToActivate != null)
        {
            specialObjectToActivate.SetActive(false);
        }

        objectDeactivationCoroutine = null;
    }
}