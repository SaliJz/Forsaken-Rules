using UnityEngine;
using System;
using System.Collections;

public class FadeController : FullScreenEffectsBase
{
    #region Singleton
    public static FadeController Instance;
    #endregion

    #region Inspector Fields

    [Header("Configuration")]
    public float fadeOutDuration = 1.3f;
    // public float fadeInDuration = 2f;
    // public float fadeInTimer = 2f;


    [Header("Global Settings")]
    public Color globalFadeColor = Color.black;
    #endregion

    #region Properties
    public bool IsFading { get; private set; }

    private static readonly string ColorProp = "_Color";

    private static readonly string ProgressProp = "_Progress";
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    protected override void Start()
    {
        base.Start();
        if (!enabled) return;

        SetupInitialState();

        StartCoroutine(FadeIn());
    }
    #endregion

    #region Initialization

    private void SetupInitialState()
    {
        ApplyGlobalColor();
        SetFloat(ProgressProp, 1f);

    }
    #endregion

    #region Public API
    public IEnumerator FadeOut(
        Action onStart = null,
        Action<float> onUpdate = null,
        Action onComplete = null,
        Color? fadeColor = null,
        bool respectPause = false)
    {
        IsFading = true;

        onStart?.Invoke();

        if (fadeColor.HasValue)
        {
            globalFadeColor = fadeColor.Value;
        }

        ApplyGlobalColor();

        float timer = 0f;

        while (timer < fadeOutDuration)
        {
            if (respectPause && Time.timeScale == 0f)
            {
                yield return null;
                continue;
            }

            timer += Time.unscaledDeltaTime;

            float t = timer / fadeOutDuration;

            SetFloat(ProgressProp, Mathf.Lerp(1f, 0f, t));

            onUpdate?.Invoke(t);

            yield return null;
        }

        FinalizeState(true);

        onComplete?.Invoke();

        IsFading = false;
    }

    public IEnumerator FadeIn(
        Action onStart = null,
        Action<float> onUpdate = null,
        Action onComplete = null,
        bool respectPause = false)
    {
        IsFading = true;

        onStart?.Invoke();

        ApplyGlobalColor();

        float timer = 0f;

        while (timer < fadeOutDuration)
        {
            if (respectPause && Time.timeScale == 0f)
            {
                yield return null;
                continue;
            }

            timer += Time.unscaledDeltaTime;

            float t = timer / fadeOutDuration;

            SetFloat(ProgressProp, Mathf.Lerp(0f, 1f, t));

            onUpdate?.Invoke(t);

            yield return null;
        }

        FinalizeState(false);

        onComplete?.Invoke();

        IsFading = false;
    }
    #endregion

    #region Helpers
    private void ApplyGlobalColor()
    {
        SetColor(ColorProp, globalFadeColor);
    }

    private void FinalizeState(bool isFullDark)
    {
        SetFloat(ProgressProp, isFullDark ? 0f : 1f);
    }
    #endregion
}