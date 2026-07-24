using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class SlowMotion : MonoBehaviour
{
    #region Singleton

    public static SlowMotion Instance { get; private set; }

    #endregion

    #region Inspector

    [Header("Slow Motion Settings")]
    [Range(0.01f, 0.5f)] [SerializeField] public float slowTimeScale = 0.08f;
    [Range(0.05f, 1.5f)] [SerializeField] private float slowDuration = 0.18f;
    [Range(0f, 1f)] [SerializeField] private float enterSmoothness = 0f;
    [Range(0f, 0.3f)] [SerializeField] private float exitSmoothness = 0.08f;

    [Header("Audio")]
    [SerializeField] private AudioClip killSoundClip;
    [SerializeField] private AudioMixerGroup audioMixerGroup;
    [Range(0f, 1f)] [SerializeField] private float volume = 1f;
    [Range(0.5f, 2f)] [SerializeField] private float pitch = 1f;

    #endregion

    #region Private Fields

    private AudioSource _audioSource;
    private Coroutine _activeRoutine;
    private bool _isSlowing = false;
    private bool _pausedExternally = false;
    private float _remainingSlowTime = 0f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Instance = this;
        SetupAudioSource();
    }

    private void OnDestroy()
    {
        if (_isSlowing)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    #endregion

    #region Public API

    public bool IsSlowMotionActive => _isSlowing;

    public void TriggerSlowMotion()
    {
        if (_isSlowing) return;

        if (_activeRoutine != null)
            StopCoroutine(_activeRoutine);

        _activeRoutine = StartCoroutine(SlowMotionRoutine());
    }

    public void NotifyPaused()
    {
        _pausedExternally = true;
    }

    public void NotifyResumed()
    {
        _pausedExternally = false;

        if (_isSlowing)
        {
            Time.timeScale = slowTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }
    }

    #endregion

    #region Private Methods

    private void SetupAudioSource()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = volume;
        _audioSource.pitch = pitch;

        if (audioMixerGroup != null)
            _audioSource.outputAudioMixerGroup = audioMixerGroup;
    }

    private void PlayKillSound()
    {
        if (killSoundClip == null || _audioSource == null) return;

        _audioSource.volume = volume;
        _audioSource.pitch = pitch;

        if (audioMixerGroup != null)
            _audioSource.outputAudioMixerGroup = audioMixerGroup;

        _audioSource.PlayOneShot(killSoundClip, volume);
    }

    private IEnumerator SlowMotionRoutine()
    {
        _isSlowing = true;

        PlayKillSound();

        if (enterSmoothness <= 0f)
        {
            Time.timeScale = slowTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }
        else
        {
            float t = 0f;
            float startScale = Time.timeScale;
            while (t < 1f)
            {
                if (!_pausedExternally)
                {
                    t += Time.unscaledDeltaTime / enterSmoothness;
                    Time.timeScale = Mathf.Lerp(startScale, slowTimeScale, t);
                    Time.fixedDeltaTime = 0.02f * Time.timeScale;
                }
                yield return null;
            }
        }

        _remainingSlowTime = slowDuration;
        while (_remainingSlowTime > 0f)
        {
            if (!_pausedExternally)
                _remainingSlowTime -= Time.unscaledDeltaTime;

            yield return null;
        }

        while (_pausedExternally)
            yield return null;

        if (exitSmoothness <= 0f)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
        else
        {
            float t = 0f;
            float startScale = Time.timeScale;
            while (t < 1f)
            {
                if (!_pausedExternally)
                {
                    t += Time.unscaledDeltaTime / exitSmoothness;
                    Time.timeScale = Mathf.Lerp(startScale, 1f, t);
                    Time.fixedDeltaTime = 0.02f * Time.timeScale;
                }
                yield return null;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        _isSlowing = false;
        _activeRoutine = null;
    }

    #endregion
}