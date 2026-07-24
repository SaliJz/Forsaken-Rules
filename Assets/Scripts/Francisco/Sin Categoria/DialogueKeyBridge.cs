using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class DialogueInputBridge : MonoBehaviour
{
    public enum StepType
    {
        Dialogue,
        WaitForInput
    }

    public enum SteamInputAction
    {
        Interact,
        MenuSelect,
        MenuCancel,
        Dash,
        Attack,
        Inventory,
        Skill
    }

    [System.Serializable]
    public struct SequenceStep
    {
        [Header("Settings")]
        public string stepLabel;
        public StepType type;

        [Header("Dialogue Settings")]
        public DialogLine[] dialogueLines;

        [Header("Input Settings")]
        public InputActionReference actionToWait;
        public SteamInputAction steamAction;
        public int requiredPresses;
        public float maxTimeBetweenPresses;

        [Header("Step Events")]
        public UnityEvent OnStepBefore;
        public UnityEvent OnStepAfter;
    }

    [Header("Sequence Config")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool playOnlyOnce = true;
    [SerializeField] private SequenceStep[] sequenceSteps;

    [Header("State")]
    [SerializeField] private bool isRunning;
    [SerializeField] private bool hasPlayed;
    [SerializeField] private int currentStepIndex;
    [SerializeField] private int currentPresses;

    private bool waitingForDialogue;

    private void Start()
    {
        if (playOnStart)
        {
            PlaySequence();
        }
    }

    public void PlaySequence()
    {
        if (isRunning) return;
        if (playOnlyOnce && hasPlayed) return;
        if (sequenceSteps == null || sequenceSteps.Length == 0) return;

        StartCoroutine(SequenceRoutine());
    }

    private IEnumerator SequenceRoutine()
    {
        isRunning = true;
        hasPlayed = true;

        for (currentStepIndex = 0; currentStepIndex < sequenceSteps.Length; currentStepIndex++)
        {
            SequenceStep currentStep = sequenceSteps[currentStepIndex];

            currentStep.OnStepBefore?.Invoke();

            if (currentStep.type == StepType.Dialogue)
            {
                yield return StartCoroutine(PlayDialogue(currentStep.dialogueLines));
            }
            else if (currentStep.type == StepType.WaitForInput)
            {
                yield return StartCoroutine(WaitForInputSequence(currentStep));
            }

            currentStep.OnStepAfter?.Invoke();
        }

        isRunning = false;
    }

    private IEnumerator PlayDialogue(DialogLine[] dialogue)
    {
        if (DialogManager.Instance == null || dialogue == null || dialogue.Length == 0)
        {
            yield break;
        }

        waitingForDialogue = true;

        UnityEvent onFinish = new UnityEvent();
        onFinish.AddListener(() => waitingForDialogue = false);

        DialogManager.Instance.StartDialog(dialogue, onFinish);

        yield return new WaitUntil(() => !waitingForDialogue);
    }

    private IEnumerator WaitForInputSequence(SequenceStep step)
    {
        currentPresses = 0;
        float timer = 0f;

        bool weEnabledAction = false;
        if (step.actionToWait != null && step.actionToWait.action != null && !step.actionToWait.action.enabled)
        {
            step.actionToWait.action.Enable();
            weEnabledAction = true;
        }

        int targetPresses = Mathf.Max(1, step.requiredPresses);
        while (currentPresses < targetPresses)
        {
            bool pressed = GetInput(step);

            if (pressed)
            {
                currentPresses++;
                timer = 0f;
            }

            if (step.maxTimeBetweenPresses > 0f && currentPresses > 0)
            {
                timer += Time.deltaTime;

                if (timer > step.maxTimeBetweenPresses)
                {
                    currentPresses = 0;
                    timer = 0f;
                }
            }

            yield return null;
        }

        if (weEnabledAction && step.actionToWait.action != null)
        {
            step.actionToWait.action.Disable();
        }
    }

    private bool GetInput(SequenceStep step)
    {
        bool inputSystemPressed = false;
        if (step.actionToWait != null && step.actionToWait.action != null && step.actionToWait.action.enabled)
        {
            inputSystemPressed = step.actionToWait.action.WasPressedThisFrame();
        }

        bool steamPressed = SteamInputManager.Instance != null && CheckSteamInput(step.steamAction);

        return inputSystemPressed || steamPressed;
    }


    private bool CheckSteamInput(SteamInputAction action)
    {
        if (SteamInputManager.Instance == null)
            return false;

        switch (action)
        {
            case SteamInputAction.Interact:
                return SteamInputManager.Instance.GetInteractPressed();

            case SteamInputAction.MenuSelect:
                return SteamInputManager.Instance.GetMenuSelectPressed();

            case SteamInputAction.MenuCancel:
                return SteamInputManager.Instance.GetMenuCancelPressed();

            case SteamInputAction.Dash:
                return SteamInputManager.Instance.GetDashPressed();

            case SteamInputAction.Attack:
                return SteamInputManager.Instance.GetMeleeAttackPressed();

            case SteamInputAction.Inventory:
                return SteamInputManager.Instance.GetInventoryPressed();

            case SteamInputAction.Skill:
                return SteamInputManager.Instance.GetActivateSkillPressed();
        }

        return false;
    }

    public void ResetSequence()
    {
        StopAllCoroutines();

        if (isRunning && currentStepIndex < sequenceSteps.Length)
        {
            var currentStep = sequenceSteps[currentStepIndex];
            if (currentStep.type == StepType.WaitForInput && currentStep.actionToWait != null && currentStep.actionToWait.action != null)
            {
                currentStep.actionToWait.action.Disable();
            }
        }

        isRunning = false;
        hasPlayed = false;
        waitingForDialogue = false;
        currentPresses = 0;
        currentStepIndex = 0;
    }
}