using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;

public class SettingsPanel : MonoBehaviour
{
    public enum PanelDisplayType { AnimatedScale, Static, CanvasFade }

    #region [ References ]

    [Header("Panel Display Settings")]
    public PanelDisplayType displayType = PanelDisplayType.AnimatedScale;

    [Header("Canvas Group Reference")]
    [SerializeField] private CanvasGroup canvasGroup;

    [SerializeField] private AudioMixer masterMixer;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Volume Scaling")]
    [Range(0.1f, 0.8f)]
    [SerializeField] private float musicDuckingMultiplier = 0.35f;
    [SerializeField] private float duckingTransitionTime = 0.5f;

    [Header("DOTween Settings")]
    [SerializeField] private float openCloseDuration = 0.2f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    [Header("Scale Animation Vectors")]
    [SerializeField] private Vector3 startScale = Vector3.zero;
    [SerializeField] private Vector3 endScale = Vector3.one;

    [Header("Focus Control")]
    [SerializeField] private GameObject firstSelectedButton;

    private const string MasterVolumeParam = "MasterVolume";
    private const string MusicVolumeParam = "MusicVolume";
    private const string SfxVolumeParam = "SfxVolume";

    private const float DefaultVolumeValue = 0.75f;

    private float currentMusicVolume = DefaultVolumeValue;
    private bool isMusicDucked = false;
    private Coroutine musicDuckCoroutine;

    #endregion

    #region [ Unity Methods ]

    private void Awake()
    {
        SubscribeToSliders();
    }

    private void Start()
    {
        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.localScale = startScale;
        }
        else if (displayType == PanelDisplayType.CanvasFade && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
        LoadSettings();
    }

    private void OnDestroy()
    {
        UnsubscribeFromSliders();
    }

    #endregion

    #region [ DOTween Animation & State ]

    public void OpenPanel()
    {
        gameObject.SetActive(true);

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.localScale = startScale;
            transform.DOScale(endScale, openCloseDuration)
                .SetEase(openEase)
                .SetUpdate(true)
                .OnComplete(() => SetInitialFocus());
        }
        else if (displayType == PanelDisplayType.CanvasFade && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;

            canvasGroup.DOFade(1f, openCloseDuration)
                .SetEase(Ease.OutSine)
                .SetUpdate(true)
                .OnComplete(() => SetInitialFocus());

            transform.DOLocalMove(Vector3.zero, openCloseDuration).SetEase(Ease.OutSine).SetUpdate(true);
        }
        else
        {
            SetInitialFocus();
        }
    }

    public void ClosePanel()
    {
        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.DOScale(startScale, openCloseDuration)
                .SetEase(closeEase)
                .SetUpdate(true)
                .OnComplete(() => gameObject.SetActive(false));
        }
        else if (displayType == PanelDisplayType.CanvasFade && canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;

            canvasGroup.DOFade(0f, openCloseDuration)
                .SetEase(Ease.InSine)
                .SetUpdate(true)
                .OnComplete(() => gameObject.SetActive(false));
        }
        else
        {
            gameObject.SetActive(false);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    #endregion

    #region [ Sliders Subscription ]
    private void SubscribeToSliders()
    {
        masterSlider?.onValueChanged.AddListener(SetMasterVolume);
        musicSlider?.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider?.onValueChanged.AddListener(SetSfxVolume);
    }
    private void UnsubscribeFromSliders()
    {
        masterSlider?.onValueChanged.RemoveListener(SetMasterVolume);
        musicSlider?.onValueChanged.RemoveListener(SetMusicVolume);
        sfxSlider?.onValueChanged.RemoveListener(SetSfxVolume);
    }
    #endregion

    #region [ Volume Control ]
    private float LinearToDecibel(float linearValue)
    {
        linearValue = Mathf.Clamp01(linearValue);
        if (linearValue <= 0.0001f)
        {
            return -80f;
        }
        return 20f * Mathf.Log10(linearValue);
    }

    public void SetMasterVolume(float sliderValue) => SetVolume(MasterVolumeParam, sliderValue);

    public void SetMusicVolume(float sliderValue)
    {
        currentMusicVolume = sliderValue;

        float finalValue = isMusicDucked ? sliderValue * musicDuckingMultiplier : sliderValue;
        SetVolume(MusicVolumeParam, finalValue);
    }

    public void SetSfxVolume(float sliderValue) => SetVolume(SfxVolumeParam, sliderValue);

    private void SetVolume(string exposedParamName, float sliderValue)
    {
        if (masterMixer == null) return;
        float dbVolume = LinearToDecibel(sliderValue);
        masterMixer.SetFloat(exposedParamName, dbVolume);
        PlayerPrefs.SetFloat(exposedParamName, sliderValue);
        PlayerPrefs.Save();
    }
    #endregion

    #region [ Initialization and Load ]
    private void LoadSettings()
    {
        LoadVolume(masterSlider, MasterVolumeParam);
        LoadVolume(musicSlider, MusicVolumeParam);
        LoadVolume(sfxSlider, SfxVolumeParam);
    }

    private void LoadVolume(Slider slider, string paramName)
    {
        if (slider == null) return;
        float savedVolume = PlayerPrefs.GetFloat(paramName, DefaultVolumeValue);
        slider.value = savedVolume;

        if (paramName == MusicVolumeParam)
        {
            currentMusicVolume = savedVolume;
        }

        SetVolume(paramName, savedVolume);
    }
    #endregion

    #region [ Reset Control ]

    public void ResetAllVolumeSettings()
    {
        if (masterSlider != null) masterSlider.value = DefaultVolumeValue;
        if (musicSlider != null) musicSlider.value = DefaultVolumeValue;
        if (sfxSlider != null) sfxSlider.value = DefaultVolumeValue;

        SetMasterVolume(DefaultVolumeValue);
        SetMusicVolume(DefaultVolumeValue);
        SetSfxVolume(DefaultVolumeValue);

        PlayerPrefs.Save();

        if (isMusicDucked)
        {
            isMusicDucked = false;
        }
    }

    #endregion

    #region [ Gamepad Focus Control ]
    private void SetInitialFocus()
    {
        if (firstSelectedButton == null) return;

        bool isGamepadModeActive = GamepadPointer.Instance != null && GamepadPointer.Instance.IsGamepadMode();

        if (!isGamepadModeActive) return;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        ResetInputDevices();
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
    }
    private void ResetInputDevices()
    {
        if (Gamepad.current != null)
        {
            InputSystem.ResetDevice(Gamepad.current);
        }
        if (Keyboard.current != null)
        {
            InputSystem.ResetDevice(Keyboard.current);
        }
        if (Mouse.current != null)
        {
            InputSystem.ResetDevice(Mouse.current);
        }
    }
    #endregion

    #region [ Public Sliders Access ]
    public Slider[] GetMenuSliders()
    {
        var list = new System.Collections.Generic.List<Slider>();
        if (masterSlider != null) list.Add(masterSlider);
        if (musicSlider != null) list.Add(musicSlider);
        if (sfxSlider != null) list.Add(sfxSlider);
        return list.ToArray();
    }
    #endregion

    #region [ Music Ducking Control ]

    public void DuckMusic()
    {
        if (isMusicDucked || masterMixer == null) return;

        if (musicDuckCoroutine != null)
        {
            StopCoroutine(musicDuckCoroutine);
        }

        isMusicDucked = true;
        musicDuckCoroutine = StartCoroutine(TransitionMusicVolume(currentMusicVolume * musicDuckingMultiplier));
    }

    public void RestoreMusic()
    {
        if (!isMusicDucked || masterMixer == null) return;

        if (musicDuckCoroutine != null)
        {
            StopCoroutine(musicDuckCoroutine);
        }

        isMusicDucked = false;
        musicDuckCoroutine = StartCoroutine(TransitionMusicVolume(currentMusicVolume));
    }

    private IEnumerator TransitionMusicVolume(float targetLinearVolume)
    {
        if (masterMixer == null) yield break;

        masterMixer.GetFloat(MusicVolumeParam, out float currentDB);
        float startLinearVolume = DecibelToLinear(currentDB);

        float elapsed = 0f;

        while (elapsed < duckingTransitionTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duckingTransitionTime;

            float newLinearVolume = Mathf.Lerp(startLinearVolume, targetLinearVolume, t);
            float newDB = LinearToDecibel(newLinearVolume);

            masterMixer.SetFloat(MusicVolumeParam, newDB);

            yield return null;
        }

        masterMixer.SetFloat(MusicVolumeParam, LinearToDecibel(targetLinearVolume));
    }

    private float DecibelToLinear(float dB)
    {
        if (dB <= -80f) return 0f;
        return Mathf.Pow(10f, dB / 20f);
    }

    #endregion
}