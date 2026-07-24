// FadeImageDownWithCanvas.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Activa el Canvas (si está asignado), garantiza que la Image comience con opacidad 100% (alpha=1),
/// y realiza un fade fluido a opacidad 0% en la cantidad de segundos indicada (tiempo real, unscaled).
/// </summary>
public class FadeImageDownWithCanvas : MonoBehaviour
{
    [Header("Referencias UI")]
    [Tooltip("Canvas que contiene la Image (se activará antes del fade).")]
    public Canvas overlayCanvas;

    [Tooltip("Image negra que cubre la pantalla. Si no se asigna, se buscará dentro del Canvas.")]
    public Image overlayImage;

    [Header("Ajustes de fade")]
    [Tooltip("Si true, el fade iniciará automáticamente en Start usando fadeSeconds.")]
    public bool autoStart = false;

    [Tooltip("Duración por defecto del fade (segundos).")]
    public float fadeSeconds = 0.6f;

    [Tooltip("Pequeña espera (segundos reales) después del fade.")]
    public float postFadeDelay = 0.05f;

    [Tooltip("Si true, desactiva el Canvas al completar el fade.")]
    public bool deactivateCanvasOnComplete = false;

    // Un único contador de opacidad (100 -> 0)
    private float opacityPercent = 100f;

    private Coroutine runningFade;

    private void Start()
    {
        if (autoStart)
        {
            StartFadeDown(fadeSeconds);
        }
    }

    /// <summary>
    /// Inicia el fade hacia opacidad 0 usando la duración indicada (en segundos reales).
    /// </summary>
    public void StartFadeDown(float seconds)
    {
        if (runningFade != null) StopCoroutine(runningFade);
        runningFade = StartCoroutine(FadeDownCoroutine(seconds));
    }

    private IEnumerator FadeDownCoroutine(float seconds)
    {
        // Activar Canvas si existe
        if (overlayCanvas != null)
            overlayCanvas.gameObject.SetActive(true);

        // Buscar Image dentro del Canvas si no está asignada
        if (overlayImage == null && overlayCanvas != null)
            overlayImage = overlayCanvas.GetComponentInChildren<Image>(true);

        // Si no hay Image válida -> salir
        if (overlayImage == null)
        {
            Debug.LogWarning("[FadeImageDownWithCanvas] No se encontró overlayImage.");
            yield break;
        }

        // Asegurar que la Image esté activa y comience con opacidad 100%
        overlayImage.gameObject.SetActive(true);
        Color baseColor = overlayImage.color;
        opacityPercent = 100f;
        overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, opacityPercent / 100f);
        overlayImage.raycastTarget = true; // bloquear input durante el fade

        // Si duración inválida -> set invisible inmediatamente
        if (seconds <= 0f)
        {
            overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            overlayImage.raycastTarget = false;
            if (postFadeDelay > 0f) yield return new WaitForSecondsRealtime(postFadeDelay);
            if (deactivateCanvasOnComplete && overlayCanvas != null)
                overlayCanvas.gameObject.SetActive(false);
            runningFade = null;
            yield break;
        }

        // Fade fluido de opacidad 100 -> 0 usando SmoothStep (unscaled time)
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            opacityPercent = Mathf.Lerp(100f, 0f, eased); // único contador
            overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, opacityPercent / 100f);
            yield return null;
        }

        // Asegurar opacidad final = 0
        overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        overlayImage.raycastTarget = false;

        // Espera opcional en tiempo real
        if (postFadeDelay > 0f)
            yield return new WaitForSecondsRealtime(postFadeDelay);

        // Desactivar Canvas si se pidió
        if (deactivateCanvasOnComplete && overlayCanvas != null)
            overlayCanvas.gameObject.SetActive(false);

        runningFade = null;
    }

    /// <summary>
    /// Cancela cualquier fade en curso y deja la imagen con opacidad 0 inmediatamente.
    /// </summary>
    public void CancelAndSetInvisible()
    {
        if (runningFade != null) StopCoroutine(runningFade);
        if (overlayImage != null)
        {
            overlayImage.color = new Color(overlayImage.color.r, overlayImage.color.g, overlayImage.color.b, 0f);
            overlayImage.raycastTarget = false;
        }
        if (deactivateCanvasOnComplete && overlayCanvas != null)
            overlayCanvas.gameObject.SetActive(false);
        runningFade = null;
    }
}
