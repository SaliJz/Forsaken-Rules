using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System.Timers;
using Unity.VisualScripting;

public class DialogManager : MonoBehaviour
{
    #region Singleton Setup

    public static DialogManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Editor Settings

    [Header("UI Dependencies")]
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI lineText;
    [SerializeField] private GameObject backgroundPanel;

    [Header("Input Setup")]
    [SerializeField] private InputActionAsset inputActions;
    private InputAction advanceDialogueAction;

    [Header("Typing/Input Settings")]
    [SerializeField] private float typingSpeed = 0.03f;
    [SerializeField] private float inputBufferTime = 0.2f;

    [Header("Voice Audio Settings")]
    [SerializeField] private AudioSource voiceAudioSource;
    [SerializeField][Range(0f, 1f)] private float voiceVolume = 0.6f;

    [Header("Music Ducking")]
    [SerializeField] private SettingsPanel settingsPanel;

    [Header("System Dependencies")]
    private PlayerMovement playerMovement;

    [Header("Player Action Scripts")]
    [SerializeField] private MonoBehaviour[] playerActionScripts;

    public bool IsActive => isDialogActive;

    #endregion

    #region Private Fields

    public static System.Action OnAnyDialogEnded;

    private Queue<DialogLine> dialogQueue = new Queue<DialogLine>();
    private bool isDialogActive = false;
    private bool isTyping = false;
    private float lastInputTime = 0f;
    private UnityEvent onDialogFinishedEvent;
    private bool isMerchantFlow = false;

    private RectTransform bcTransform;
    private Image bcImage;

    #endregion

    #region Input Integration & Unity Methods

    private void OnEnable()
    {
        InputActionMap uiMap = inputActions?.FindActionMap("UI");
        if (uiMap != null)
        {
            advanceDialogueAction = uiMap.FindAction("Click");
        }

        if (advanceDialogueAction != null)
        {
            advanceDialogueAction.performed += OnAdvanceDialogue;
            advanceDialogueAction.Enable();
        }

        bcTransform = backgroundPanel.GetComponent<RectTransform>();
        bcImage = backgroundPanel.GetComponent<Image>();
    }

    private void OnDisable()
    {
        if (advanceDialogueAction != null)
        {
            advanceDialogueAction.performed -= OnAdvanceDialogue;
            advanceDialogueAction.Disable();
        }
    }

    private void Start()
    {
        playerMovement = FindAnyObjectByType<PlayerMovement>();

        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }

        if (voiceAudioSource == null)
        {
            voiceAudioSource = gameObject.AddComponent<AudioSource>();
            voiceAudioSource.playOnAwake = false;
            voiceAudioSource.volume = voiceVolume;
        }
    }
    private void OnAdvanceDialogue(InputAction.CallbackContext context)
    {
        AdvanceFromSteam();
    }

    private void Update()
    {
        if (SteamInputManager.Instance == null) return;

        if (SteamInputManager.Instance.GetInteractPressed())
        {
            AdvanceFromSteam();
        }
    }

    #endregion

    #region Core Dialog Logic

    private void AdvanceFromSteam()
    {
        if (!isDialogActive) return;

        if (Time.time < lastInputTime + inputBufferTime)
            return;

        lastInputTime = Time.time;

        if (isTyping)
        {
            StopAllCoroutines();
            isTyping = false;
            lineText.maxVisibleCharacters = int.MaxValue;

            if (voiceAudioSource != null && voiceAudioSource.isPlaying)
                voiceAudioSource.Stop();

            if (isMerchantFlow)
                onDialogFinishedEvent?.Invoke();

            return;
        }

        if (isMerchantFlow) return;

        if (dialogQueue.Count > 0)
        {
            DialogLine currentLine = dialogQueue.Peek();

            if (currentLine.WaitForInput)
            {
                DisplayNextLine();
            }
        }
        else
        {
            EndDialog();
        }
    }
    public IEnumerator DialogStrech(bool Activating,float duration = 0.25f)
    {
        float startSize = Activating? 0f: 1f;
        bcTransform.localScale = new Vector3 (bcTransform.localScale.x, startSize, bcTransform.localScale.z);

        float endSize = Activating? 1f: 0f;

        float elapse = 0f;

        while (elapse < duration)
        {
            elapse += Time.deltaTime;
            float t = elapse/duration;
            
            Vector3 targetScale = new Vector3(bcTransform.localScale.x, Mathf.Lerp(startSize, endSize, t), bcTransform.localScale.z);

            bcTransform.localScale = targetScale;
            yield return null;
        }

        bcTransform.localScale = Vector3.one;
    }

    public void StartDialog(DialogLine[] lines, UnityEvent onFinished = null, bool isMerchant = false)
    {
        if (isDialogActive) return;

        StopAllCoroutines();
        StartCoroutine(StartDialogRoutine(lines, onFinished, isMerchant));
        
    }

    public IEnumerator StartDialogRoutine(DialogLine[] lines, UnityEvent onFinished, bool isMerchant)
    {
        isMerchantFlow = isMerchant;
        onDialogFinishedEvent = onFinished;

        dialogQueue.Clear();
        foreach (DialogLine line in lines)
        {
            dialogQueue.Enqueue(line);
        }

        if (dialogPanel != null)
        {
            dialogPanel.SetActive(true);
        }

        LockPlayerControl(true);
        DisablePlayerScripts(true);

        DialogLine fisrtline = dialogQueue.Peek();
        nameText.text = fisrtline.CharacterName;

        if (backgroundPanel != null)
        {
            if (fisrtline.ProfileImage != null)
            {
                bcImage.sprite = fisrtline.ProfileImage;
                bcImage.enabled = true;
            }
            else
            {
                bcImage.enabled = false;
            }
        }

        lineText.text = "";
        lineText.maxVisibleCharacters = 0;

        yield return DialogStrech(true);

        isDialogActive = true;

        DisplayNextLine();
    }

    public void DisplayNextLine()
    {
        if (dialogQueue.Count == 0)
        {
            if (isMerchantFlow)
            {
                onDialogFinishedEvent?.Invoke();
                return;
            }

            EndDialog();
            return;
        }

        DialogLine line = dialogQueue.Dequeue();

        StopAllCoroutines();
        isTyping = true;

        nameText.text = line.CharacterName;
        if (backgroundPanel != null)
        {
            if (line.ProfileImage != null)
            {
                bcImage.sprite = line.ProfileImage;
                bcImage.enabled = true;
            }
            else
            {
                bcImage.enabled = false;
            }
        }

        bool shouldWaitForInput = isMerchantFlow ? false : line.WaitForInput;

        if (typingSpeed > 0)
        {
            StartCoroutine(TypeLine(line, shouldWaitForInput));
        }
        else
        {
            lineText.text = line.Text;
            lineText.maxVisibleCharacters = int.MaxValue;
            isTyping = false;

            if (isMerchantFlow && dialogQueue.Count == 0)
            {
                onDialogFinishedEvent?.Invoke();
            }
            else if (!isMerchantFlow && !line.WaitForInput)
            {
                StartCoroutine(AutoAdvance(dialogQueue.Count > 0 ? inputBufferTime : 0f));
            }
        }
    }

    private IEnumerator TypeLine(DialogLine line, bool waitForInput)
    {
        string fullText = line.Text;
        lineText.text = fullText;
        lineText.maxVisibleCharacters = 0;

        for (int i = 0; i < fullText.Length; i++)
        {
            if (lineText.maxVisibleCharacters >= fullText.Length) break;

            lineText.maxVisibleCharacters++;

            if (line.VoiceClip != null && i % line.VoiceFrequency == 0)
            {
                PlayVoiceBlip(line.VoiceClip, line.VoicePitch);
            }

            yield return new WaitForSecondsRealtime(typingSpeed);
        }

        lineText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;

        if (isMerchantFlow && dialogQueue.Count == 0)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            onDialogFinishedEvent?.Invoke();
        }
        else if (!isMerchantFlow && !waitForInput)
        {
            StartCoroutine(AutoAdvance(dialogQueue.Count > 0 ? inputBufferTime : 0f));
        }
    }

    private IEnumerator TypeLineForUpdate(string fullText, AudioClip voiceClip = null, float voicePitch = 1f, int voiceFrequency = 2)
    {
        lineText.text = fullText;
        lineText.maxVisibleCharacters = 0;

        for (int i = 0; i < fullText.Length; i++)
        {
            if (lineText.maxVisibleCharacters >= fullText.Length) break;

            lineText.maxVisibleCharacters++;

            if (voiceClip != null && i % voiceFrequency == 0)
            {
                PlayVoiceBlip(voiceClip, voicePitch);
            }

            yield return new WaitForSecondsRealtime(typingSpeed);
        }

        lineText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;

        if (isMerchantFlow && onDialogFinishedEvent != null)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            onDialogFinishedEvent.Invoke();
        }
    }

    public void UpdateCurrentDialogText(string newText, string characterName = null, Sprite profileSprite = null, AudioClip voiceClip = null, float voicePitch = 1f, int voiceFrequency = 2)
    {
        if (!isDialogActive) return;

        if (!string.IsNullOrEmpty(characterName) && nameText != null)
        {
            nameText.text = characterName;
        }

        if (profileSprite != null && backgroundPanel != null)
        {
            bcImage.sprite = profileSprite;
            bcImage.enabled = true;
        }

        StopAllCoroutines();

        isTyping = true;
        StartCoroutine(TypeLineForUpdate(newText, voiceClip, voicePitch, voiceFrequency));
    }

    private void PlayVoiceBlip(AudioClip clip, float pitch)
    {
        if (voiceAudioSource == null || clip == null) return;

        voiceAudioSource.pitch = pitch;
        voiceAudioSource.PlayOneShot(clip, voiceVolume);
    }

    private void EndDialog()
    {
        StartCoroutine(EndDialogRoutine());
    }

    private IEnumerator EndDialogRoutine()
    {
        if (isMerchantFlow) yield break;

        isDialogActive = false;
        isMerchantFlow = false;

        UnityEvent tempEvent = onDialogFinishedEvent;
        onDialogFinishedEvent = null;

        yield return DialogStrech(false);

        if (voiceAudioSource != null && voiceAudioSource.isPlaying)
        {
            voiceAudioSource.Stop();
        }

        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
        LockPlayerControl(false);
        DisablePlayerScripts(false);

        OnAnyDialogEnded?.Invoke();

        tempEvent?.Invoke();
    }

    public void ForceEndDialog()
    {
        isDialogActive = false;
        isMerchantFlow = false;

        UnityEvent tempEvent = onDialogFinishedEvent;
        onDialogFinishedEvent = null;

        if (voiceAudioSource != null && voiceAudioSource.isPlaying)
        {
            voiceAudioSource.Stop();
        }

        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
        LockPlayerControl(false);
        DisablePlayerScripts(false);

        tempEvent?.Invoke();
    }

    private void LockPlayerControl(bool isLocked)
    {
        if (playerMovement != null)
        {
            playerMovement.SetCanMove(!isLocked);
        }
    }

    private void DisablePlayerScripts(bool disable)
    {
        if (playerActionScripts == null || playerActionScripts.Length == 0)
        {
            return;
        }

        foreach (MonoBehaviour script in playerActionScripts)
        {
            if (script != null)
            {
                script.enabled = !disable;
            }
        }
    }

    private IEnumerator AutoAdvance(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        DisplayNextLine();
    }

    #endregion

    #region Public Volume Control

    public void SetVoiceVolume(float volume)
    {
        voiceVolume = Mathf.Clamp01(volume);
        if (voiceAudioSource != null)
        {
            voiceAudioSource.volume = voiceVolume;
        }
    }

    #endregion
}