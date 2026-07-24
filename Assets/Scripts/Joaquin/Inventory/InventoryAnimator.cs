using UnityEngine;
using UnityEngine.UI;
using System.Collections;


/// <summary>
/// Componente opcional para animar la apertura/cierre del inventario
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class InventoryAnimator : MonoBehaviour
{
    [Header("Animación de Fade")]
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Animación de Escala")]
    [SerializeField] private bool useScaleAnimation = true;
    [SerializeField] private float scaleDuration = 0.25f;
    [SerializeField] private Vector3 startScale = new Vector3(0.9f, 0.9f, 1f);
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Efectos de Sonido")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Partículas (Opcional)")]
    [SerializeField] private ParticleSystem openParticles;
    [SerializeField] private ParticleSystem closeParticles;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Coroutine currentAnimation;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    /// <summary>
    /// Anima la apertura del inventario
    /// </summary>
    public void AnimateOpen()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        gameObject.SetActive(true);
        currentAnimation = StartCoroutine(OpenAnimation());
    }

    /// <summary>
    /// Anima el cierre del inventario
    /// </summary>
    public void AnimateClose(System.Action onComplete = null)
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        currentAnimation = StartCoroutine(CloseAnimation(onComplete));
    }

    private IEnumerator OpenAnimation()
    {
        // Sonido de apertura
        PlaySound(openSound);

        // Partículas de apertura
        if (openParticles != null)
        {
            openParticles.Play();
        }

        // Estado inicial
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (useScaleAnimation)
        {
            rectTransform.localScale = startScale;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(fadeDuration, useScaleAnimation ? scaleDuration : 0f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // Fade
            if (elapsed < fadeDuration)
            {
                float fadeT = fadeCurve.Evaluate(elapsed / fadeDuration);
                canvasGroup.alpha = fadeT;
            }
            else
            {
                canvasGroup.alpha = 1f;
            }

            // Escala
            if (useScaleAnimation && elapsed < scaleDuration)
            {
                float scaleT = scaleCurve.Evaluate(elapsed / scaleDuration);
                rectTransform.localScale = Vector3.Lerp(startScale, Vector3.one, scaleT);
            }
            else if (useScaleAnimation)
            {
                rectTransform.localScale = Vector3.one;
            }

            yield return null;
        }

        // Asegurar estado final
        canvasGroup.alpha = 1f;
        rectTransform.localScale = Vector3.one;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        currentAnimation = null;
    }

    private IEnumerator CloseAnimation(System.Action onComplete)
    {
        // Sonido de cierre
        PlaySound(closeSound);

        // Partículas de cierre
        if (closeParticles != null)
        {
            closeParticles.Play();
        }

        // Estado inicial
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;
        float duration = Mathf.Max(fadeDuration, useScaleAnimation ? scaleDuration : 0f);

        Vector3 initialScale = rectTransform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // Fade
            if (elapsed < fadeDuration)
            {
                float fadeT = fadeCurve.Evaluate(1f - (elapsed / fadeDuration));
                canvasGroup.alpha = fadeT;
            }
            else
            {
                canvasGroup.alpha = 0f;
            }

            // Escala
            if (useScaleAnimation && elapsed < scaleDuration)
            {
                float scaleT = scaleCurve.Evaluate(1f - (elapsed / scaleDuration));
                rectTransform.localScale = Vector3.Lerp(startScale, initialScale, scaleT);
            }

            yield return null;
        }

        // Estado final
        canvasGroup.alpha = 0f;
        rectTransform.localScale = startScale;
        gameObject.SetActive(false);

        currentAnimation = null;
        onComplete?.Invoke();
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// Configuración rápida desde el Inspector
    /// </summary>
    [ContextMenu("Setup Default Animation")]
    private void SetupDefaultAnimation()
    {
        fadeDuration = 0.3f;
        scaleDuration = 0.25f;
        useScaleAnimation = true;
        startScale = new Vector3(0.9f, 0.9f, 1f);

        fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        scaleCurve = new AnimationCurve(
            new Keyframe(0, 0, 0, 2),
            new Keyframe(1, 1, 0, 0)
        );
    }
}