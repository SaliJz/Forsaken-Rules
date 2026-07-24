using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PauseController : MonoBehaviour, PlayerControlls.IUIActions
{
    [Header("Menu Panel References")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private SettingsPanel settingsPanel;
    [SerializeField] private GameObject firstSelectedButton;

    [Header("SFX / Music Sources")]
    [SerializeField] private AudioSource pauseMusicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip openPauseClip;
    [SerializeField] private AudioClip clickButtonSFX;

    [Header("Audio Exclusion Settings")]
    [SerializeField] private string ignorePauseTag = "IgnorePause";

    private class AudioState
    {
        public bool enabled;
        public bool wasPlaying;
        public int timeSamples;
    }
    private Dictionary<AudioSource, AudioState> previousAudioStates = new Dictionary<AudioSource, AudioState>();

    private static bool isPaused = false;
    private bool isSettingsOpen = false;

    private PlayerControlls playerControls;
    private bool canTogglePause = true;
    private const float ToggleCooldown = 0.15f;

    [SerializeField] private PlayerStatsManager statsManager;
    private float previousTimeScale;
    private bool pauseButtonPressedLastFrame = false;

    public static bool IsGamePaused => isPaused;
    public static PauseController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        playerControls = new PlayerControlls();
        playerControls.UI.SetCallbacks(this);

        if (statsManager == null)
        {
            statsManager = FindAnyObjectByType<PlayerStatsManager>();
        }

        if (pauseMusicSource == null)
        {
            pauseMusicSource = GetComponent<AudioSource>();
        }
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    private void OnEnable()
    {
        playerControls?.UI.Enable();

        if (IsGamepadActive())
        {
            if (firstSelectedButton != null)
            {
                EventSystem.current?.SetSelectedGameObject(null);
                EventSystem.current?.SetSelectedGameObject(firstSelectedButton);
            }
        }
    }

    private void OnDisable()
    {
        playerControls?.UI.Disable();

        if (isPaused)
        {
            Time.timeScale = previousTimeScale;
            RestoreAudioSources();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        playerControls?.Dispose();
    }

    private void Update()
    {
        if (SteamInputManager.Instance == null) return;
        if (!canTogglePause) return;

        bool pausePressed = SteamInputManager.Instance.GetPauseMenuPressed();

        if (pausePressed && !pauseButtonPressedLastFrame)
        {
            if (isSettingsOpen) CloseSettings();
            else if (isPaused) ResumeGame();
            else if (InventoryUIManager.Instance == null || !InventoryUIManager.Instance.IsOpen)
            {
                PauseGame();
            }
        }

        pauseButtonPressedLastFrame = pausePressed;
    }

    public void OnCancel(InputAction.CallbackContext context)
    {
        if (context.performed && canTogglePause)
        {
            if (isSettingsOpen) CloseSettings();
            else if (isPaused) ResumeGame();
            else if (InventoryUIManager.Instance == null || !InventoryUIManager.Instance.IsOpen)
            {
                PauseGame();
            }
        }
    }

    public void OnNavigate(InputAction.CallbackContext context) { }
    public void OnSubmit(InputAction.CallbackContext context) { }
    public void OnPoint(InputAction.CallbackContext context) { }
    public void OnClick(InputAction.CallbackContext context) { }
    public void OnScrollWheel(InputAction.CallbackContext context) { }
    public void OnMiddleClick(InputAction.CallbackContext context) { }
    public void OnRightClick(InputAction.CallbackContext context) { }
    public void OnTrackedDevicePosition(InputAction.CallbackContext context) { }
    public void OnTrackedDeviceOrientation(InputAction.CallbackContext context) { }
    public void OnToggleInventory(InputAction.CallbackContext context) { }

    public void PauseGame()
    {
        if (isPaused) return;
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;

        DisableOtherAudioSources();

        if (openPauseClip != null && pauseMusicSource != null)
        {
            if (pauseMusicSource.clip != openPauseClip)
            {
                pauseMusicSource.clip = openPauseClip;
                pauseMusicSource.loop = true;
            }
            if (!pauseMusicSource.isPlaying) pauseMusicSource.Play();
        }

        if (playerControls != null)
        {
            playerControls.Movement.Disable();
            playerControls.Combat.Disable();
        }

        if (SlowMotion.Instance != null && SlowMotion.Instance.IsSlowMotionActive)
        {
            SlowMotion.Instance.NotifyPaused();
            previousTimeScale = SlowMotion.Instance.slowTimeScale;
        }
        else
        {
            previousTimeScale = Time.timeScale;
        }

        Time.timeScale = 0f;
        isPaused = true;
        StartCoroutine(PauseCooldown());

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
            if (IsGamepadActive())
                SetFocus(firstSelectedButton, "PauseController");
            else
                EventSystem.current?.SetSelectedGameObject(null);
        }

        SteamInputManager.Instance?.ActivateMenuSet();
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        PlayClickSFX();
        if (isSettingsOpen) return;

        EventSystem.current?.SetSelectedGameObject(null);

        if (Gamepad.current != null) InputSystem.ResetDevice(Gamepad.current);
        if (Keyboard.current != null) InputSystem.ResetDevice(Keyboard.current);
        if (Mouse.current != null) InputSystem.ResetDevice(Mouse.current);

        if (pauseMusicSource != null && pauseMusicSource.isPlaying)
            pauseMusicSource.Stop();

        RestoreAudioSources();

        if (SlowMotion.Instance != null && SlowMotion.Instance.IsSlowMotionActive)
        {
            SlowMotion.Instance.NotifyResumed();
            previousTimeScale = Time.timeScale;
        }
        else
        {
            Time.timeScale = previousTimeScale;
        }

        isPaused = false;
        StartCoroutine(PauseCooldown());

        if (playerControls != null)
        {
            playerControls.Movement.Enable();
            playerControls.Combat.Enable();
        }

        if (pausePanel != null) pausePanel.SetActive(false);

        SteamInputManager.Instance?.ActivateInGameSet();
    }

    public void OpenSettings()
    {
        if (settingsPanel == null) return;

        PlayClickSFX();
        settingsPanel.OpenPanel();
        isSettingsOpen = true;
    }

    public void CloseSettings()
    {
        if (settingsPanel == null || !isSettingsOpen) return;

        PlayClickSFX();
        settingsPanel.ClosePanel();
        isSettingsOpen = false;

        if (IsGamepadActive())
            SetFocus(firstSelectedButton, "Settings Close");
    }

    public void ResetGame()
    {
        PlayClickSFX();
        Time.timeScale = 1f;
        StartCoroutine(FadeAndReloadScene(SceneManager.GetActiveScene().name));
    }

    public void LoadMainMenu()
    {
        PlayClickSFX();
        Time.timeScale = 1f;
        StartCoroutine(FadeAndReloadScene("MainMenu"));
    }

    private IEnumerator FadeAndReloadScene(string sceneName)
    {
        isPaused = false;
        isSettingsOpen = false;

        RestoreAudioSources();

        if (FadeController.Instance != null)
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);

            yield return StartCoroutine(FadeController.Instance.FadeOut());
        }

        Time.timeScale = 1f;

        InventoryManager inventory = FindAnyObjectByType<InventoryManager>();

        if (statsManager != null)
        {
            if (inventory != null && inventory.ActiveBehavioralEffects != null)
                statsManager.RemoveAllBehavioralEffects(inventory.ActiveBehavioralEffects);

            statsManager.ClearAllNamedModifiers();
            statsManager.ResetRunStatsToDefaults();
            statsManager.ResetStatsOnDeath();

            float maxHealth = statsManager.GetStat(StatType.MaxHealth);
            if (statsManager._currentStatSO != null)
                statsManager._currentStatSO.currentHealth = maxHealth;
        }

        if (inventory != null) inventory.ClearInventory();

        MerchantDialogHandler.ResetReputationState();
        MerchantRoomManager.ResetPactsForNewLevel();

        SceneManager.LoadScene(sceneName);
    }

    private bool IsGamepadActive()
    {
        return GamepadPointer.Instance != null && GamepadPointer.Instance.IsGamepadMode();
    }

    private IEnumerator PauseCooldown()
    {
        canTogglePause = false;
        yield return new WaitForSecondsRealtime(ToggleCooldown);
        canTogglePause = true;
    }

    private void SetFocus(GameObject focusObject, string context)
    {
        if (focusObject == null) return;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(focusObject);
        }
    }

    private void PlayClickSFX()
    {
        if (clickButtonSFX == null) return;

        if (sfxSource != null)
            sfxSource.PlayOneShot(clickButtonSFX);
        else if (pauseMusicSource != null)
            pauseMusicSource.PlayOneShot(clickButtonSFX);
    }

    private bool ShouldIgnoreAudioSource(AudioSource src)
    {
        if (src == null) return true;
        if (src == pauseMusicSource || src == sfxSource) return true;
        if (!string.IsNullOrEmpty(ignorePauseTag) && src.gameObject.CompareTag(ignorePauseTag)) return true;
        return false;
    }

    private void DisableOtherAudioSources()
    {
        previousAudioStates.Clear();

        AudioSource[] allSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (AudioSource src in allSources)
        {
            if (src == null || ShouldIgnoreAudioSource(src)) continue;

            AudioState state = new AudioState();
            state.enabled = src.enabled;
            state.wasPlaying = src.isPlaying;
            state.timeSamples = 0;

            try { if (src.isPlaying && src.clip != null) state.timeSamples = src.timeSamples; }
            catch { }

            previousAudioStates[src] = state;

            try { src.enabled = false; }
            catch { }
        }
    }

    private void RestoreAudioSources()
    {
        if (previousAudioStates == null || previousAudioStates.Count == 0) return;

        var keys = new List<AudioSource>(previousAudioStates.Keys);
        foreach (AudioSource src in keys)
        {
            if (src == null) continue;

            AudioState prev = previousAudioStates[src];

            try
            {
                src.enabled = prev.enabled;

                if (prev.wasPlaying && src.enabled && src.gameObject.activeInHierarchy && src.clip != null)
                {
                    try { src.timeSamples = prev.timeSamples; } catch { }
                    src.Play();
                }
            }
            catch { }
        }

        previousAudioStates.Clear();
    }
}

//using UnityEngine;
//using UnityEngine.SceneManagement;
//using System.Collections;
//using UnityEngine.InputSystem;
//using UnityEngine.EventSystems;

//public class PauseController : MonoBehaviour, PlayerControlls.IUIActions
//{
//    [Header("Menu Panel References")]
//    [SerializeField] private GameObject pausePanel;
//    [SerializeField] private SettingsPanel settingsPanel;
//    [SerializeField] private GameObject firstSelectedButton;

//    [Header("SFX Configuration")]
//    [SerializeField] private AudioSource audioSource;
//    [SerializeField] private AudioClip openPauseClip;
//    [SerializeField] private AudioClip clickButtonSFX;

//    private static bool isPaused = false;
//    private bool isSettingsOpen = false; 

//    private PlayerControlls playerControls;
//    private bool canTogglePause = true;
//    private const float ToggleCooldown = 0.15f;

//    [SerializeField] private PlayerStatsManager statsManager;
//    private float previousTimeScale;
//    public static bool IsGamePaused => isPaused;
//    public static PauseController Instance { get; private set; }

//    void Awake()
//    {
//        if (Instance != null && Instance != this)
//        {
//            Destroy(gameObject);
//            return;
//        }
//        Instance = this;

//        playerControls = new PlayerControlls();
//        playerControls.UI.SetCallbacks(this);

//        if (statsManager == null)
//        {
//            statsManager = FindAnyObjectByType<PlayerStatsManager>();
//        }

//        if (pausePanel != null) pausePanel.SetActive(false);
//    }

//    void OnEnable()
//    {
//        playerControls?.UI.Enable();

//        if (Gamepad.current != null)
//        {
//            if (firstSelectedButton != null)
//            {
//                if (EventSystem.current != null)
//                {
//                    EventSystem.current.SetSelectedGameObject(null);
//                    EventSystem.current.SetSelectedGameObject(firstSelectedButton);
//                }
//            }
//            else
//            {
//                Debug.LogWarning("[ControlMenu] No se pudo establecer el foco inicial. Asigna 'First Selected Button' en el Inspector.");
//            }
//        }
//        else
//        {
//            Debug.LogWarning("[ControlMenu] No se pudo establecer el foco inicial. Asigna 'First Selected Button' en el Inspector.");
//        }
//    }

//    void OnDisable()
//    {
//        playerControls?.UI.Disable();

//        if (isPaused)
//        {
//            Time.timeScale = previousTimeScale;
//        }
//    }

//    void OnDestroy()
//    {
//        playerControls?.Dispose();
//    }

//    public void OnCancel(InputAction.CallbackContext context)
//    {
//        if (context.performed && canTogglePause)
//        {
//            if (isSettingsOpen)
//            {
//                CloseSettings();
//            }
//            else if (isPaused)
//            {
//                ResumeGame();
//            }
//            else
//            {
//                PauseGame();
//            }
//        }
//    }

//    public void OnNavigate(InputAction.CallbackContext context) { }
//    public void OnSubmit(InputAction.CallbackContext context) { }

//    public void PauseGame()
//    {
//        if (isPaused) return;

//        if (audioSource != null && openPauseClip != null)
//        {
//            audioSource.PlayOneShot(openPauseClip);
//        }

//        if (playerControls != null)
//        {
//            playerControls.Movement.Disable();
//            playerControls.Combat.Disable();
//        }

//        previousTimeScale = Time.timeScale;
//        Time.timeScale = 0f;
//        isPaused = true;
//        StartCoroutine(PauseCooldown());

//        if (pausePanel != null)
//        {
//            pausePanel.SetActive(true);

//            if (Gamepad.current != null) 
//            {
//                SetFocus(firstSelectedButton, "PauseController");
//            }
//            else
//            {
//                EventSystem.current?.SetSelectedGameObject(null);
//            }
//        }
//    }

//    public void ResumeGame()
//    {
//        if (!isPaused) return;

//        if (audioSource != null && clickButtonSFX != null)
//        {
//            audioSource.PlayOneShot(clickButtonSFX);
//        }

//        if (isSettingsOpen) return;

//        if (EventSystem.current != null)
//        {
//            EventSystem.current.SetSelectedGameObject(null);
//        }

//        if (Gamepad.current != null)
//        {
//            InputSystem.ResetDevice(Gamepad.current);
//        }
//        if (Keyboard.current != null)
//        {
//            InputSystem.ResetDevice(Keyboard.current);
//        }
//        if (Mouse.current != null)
//        {
//            InputSystem.ResetDevice(Mouse.current);
//        }

//        Time.timeScale = previousTimeScale;
//        isPaused = false;
//        StartCoroutine(PauseCooldown());

//        if (playerControls != null)
//        {
//            playerControls.Movement.Enable();
//            playerControls.Combat.Enable();
//        }

//        if (pausePanel != null)
//        {
//            pausePanel.SetActive(false);
//        }
//    }

//    public void OpenSettings()
//    {
//        if (settingsPanel == null)
//        {
//            Debug.LogError("[PauseController] No se pudo abrir Settings: 'Settings Panel' no est� asignado.");
//            return;
//        }

//        if (audioSource != null && clickButtonSFX != null)
//        {
//            audioSource.PlayOneShot(clickButtonSFX);
//        }

//        settingsPanel.OpenPanel();
//        isSettingsOpen = true;
//    }

//    public void CloseSettings()
//    {
//        if (settingsPanel == null || !isSettingsOpen) return;

//        if (audioSource != null && clickButtonSFX != null)
//        {
//            audioSource.PlayOneShot(clickButtonSFX);
//        }

//        settingsPanel.ClosePanel();
//        isSettingsOpen = false;

//        if (Gamepad.current != null)
//        {
//            SetFocus(firstSelectedButton, "Settings Close");
//        }
//    }

//    private IEnumerator PauseCooldown()
//    {
//        canTogglePause = false;
//        yield return new WaitForSecondsRealtime(ToggleCooldown);
//        canTogglePause = true;
//    }

//    private void SetFocus(GameObject focusObject, string context)
//    {
//        if (focusObject == null) return;

//        if (EventSystem.current != null)
//        {
//            EventSystem.current.SetSelectedGameObject(null);
//            EventSystem.current.SetSelectedGameObject(focusObject);
//        }
//        else
//        {
//            Debug.LogWarning($"[{context} - PauseController] EventSystem.current es nulo. No se pudo establecer el foco.");
//        }
//    }

//    public void ResetGame()
//    {
//        if (audioSource != null && clickButtonSFX != null)
//        {
//            audioSource.PlayOneShot(clickButtonSFX);
//        }

//        Time.timeScale = 1f;
//        StartCoroutine(FadeAndReloadScene(SceneManager.GetActiveScene().name));
//    }

//    public void LoadMainMenu()
//    {
//        if (audioSource != null && clickButtonSFX != null)
//        {
//            audioSource.PlayOneShot(clickButtonSFX);
//        }

//        Time.timeScale = 1f;
//        StartCoroutine(FadeAndReloadScene("MainMenu"));
//    }

//    private IEnumerator FadeAndReloadScene(string sceneName)
//    {
//        isPaused = false;
//        isSettingsOpen = false; 

//        if (FadeController.Instance != null && FadeController.Instance.fade != null)
//        {
//            if (pausePanel != null) pausePanel.SetActive(false);
//            if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);

//            yield return StartCoroutine(FadeController.Instance.FadeOut());
//        }

//        Time.timeScale = 1f;

//        if (statsManager != null)
//        {
//            statsManager.ResetRunStatsToDefaults();
//            statsManager.ResetStatsOnDeath();

//            float maxHealth = statsManager.GetStat(StatType.MaxHealth);
//            if (statsManager._currentStatSO != null)
//            {
//                statsManager._currentStatSO.currentHealth = maxHealth;
//            }
//        }

//        InventoryManager inventory = FindAnyObjectByType<InventoryManager>();
//        if (inventory != null)
//        {
//            inventory.ClearInventory();
//        }

//        SceneManager.LoadScene(sceneName);
//    }

//    public void OnPoint(InputAction.CallbackContext context) { }
//    public void OnClick(InputAction.CallbackContext context) { }
//    public void OnScrollWheel(InputAction.CallbackContext context) { }
//    public void OnMiddleClick(InputAction.CallbackContext context) { }
//    public void OnRightClick(InputAction.CallbackContext context) { }
//    public void OnTrackedDevicePosition(InputAction.CallbackContext context) { }
//    public void OnTrackedDeviceOrientation(InputAction.CallbackContext context) { }
//}