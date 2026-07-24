// MonitorEnemyDeathLoadScene.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Vigila un EnemyHealth; al morir activa el Canvas (si está asignado),
/// realiza un fade-to-black fluido (alfa 0 -> 1) y luego carga la escena.
/// </summary>
public class MonitorEnemyDeathLoadScene : MonoBehaviour
{
    [Header("Enemigo a vigilar (asignar EnemyHealth)")]
    public EnemyHealth enemyToMonitor;

    [Header("Escena a cargar")]
    public string sceneName = "";
    public bool useBuildIndex = false;
    public int sceneBuildIndex = 0;

    [Tooltip("Retardo antes de iniciar la transición/fade (útil para reproducir sonidos de muerte).")]
    public float delayBeforeTransition = 0.25f;

    [Header("Transición UI (opcional)")]
    [Tooltip("Canvas que contiene la Image negra. Se activará antes del fade.")]
    public Canvas overlayCanvas;
    [Tooltip("Image negra que cubre la pantalla. Si no se asigna, se buscará dentro del Canvas.")]
    public Image overlayImage;
    [Tooltip("Duración del fade (segundos) desde alfa 0 hasta 1.")]
    public float fadeDuration = 0.6f;
    [Tooltip("Pequeña espera en segundos después del fade antes de cargar la escena.")]
    public float postFadeDelay = 0.05f;

    private bool alreadyLoading = false;

    private void OnEnable()
    {
        TrySubscribeToEnemy();
    }

    private void OnDisable()
    {
        UnsubscribeFromEnemy();
    }

    public void SetEnemyToMonitor(EnemyHealth newEnemy)
    {
        UnsubscribeFromEnemy();
        enemyToMonitor = newEnemy;
        TrySubscribeToEnemy();
    }

    private void TrySubscribeToEnemy()
    {
        if (enemyToMonitor == null)
        {
            Debug.LogWarning("[MonitorEnemyDeathLoadScene] enemyToMonitor no asignado.");
            return;
        }

        // Ajusta estas firmas si tu EnemyHealth usa otras
        enemyToMonitor.OnDeath += HandleEnemyDeath;
        enemyToMonitor.OnHealthChanged += HandleEnemyHealthChanged;
    }

    private void UnsubscribeFromEnemy()
    {
        if (enemyToMonitor == null) return;
        enemyToMonitor.OnDeath -= HandleEnemyDeath;
        enemyToMonitor.OnHealthChanged -= HandleEnemyHealthChanged;
    }

    private void HandleEnemyDeath(GameObject deadObject)
    {
        TryStartTransitionAndLoad();
    }

    private void HandleEnemyHealthChanged(float newCurrent, float newMax)
    {
        if (newCurrent <= 0f)
            TryStartTransitionAndLoad();
    }

    private void TryStartTransitionAndLoad()
    {
        if (alreadyLoading) return;
        alreadyLoading = true;

        if (delayBeforeTransition > 0f)
            StartCoroutine(DelayedTransitionCoroutine(delayBeforeTransition));
        else
            StartCoroutine(TransitionThenLoadCoroutine());
    }

    private IEnumerator DelayedTransitionCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        yield return TransitionThenLoadCoroutine();
    }

    private IEnumerator TransitionThenLoadCoroutine()
    {
        // 1) Activar Canvas si está asignado
        if (overlayCanvas != null)
        {
            overlayCanvas.gameObject.SetActive(true);
        }

        // 2) Si no hay Image asignada, intentar buscarla dentro del Canvas
        if (overlayImage == null && overlayCanvas != null)
        {
            overlayImage = overlayCanvas.GetComponentInChildren<Image>(true);
        }

        // 3) Si no hay Image válida, cargar escena directamente
        if (overlayImage == null)
        {
            LoadTargetScene();
            yield break;
        }

        // 4) Preparar la Image: asegurarnos que esté activa y comience en alpha = 0
        overlayImage.gameObject.SetActive(true);
        Color baseColor = overlayImage.color;
        overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        overlayImage.raycastTarget = true; // bloquear input mientras el fade ocurre

        // 5) Fade fluido de alpha 0 -> 1 usando SmoothStep (más suave que Lerp lineal)
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // usar tiempo real para que no afecte timeScale
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, eased);
            yield return null;
        }

        // 6) Asegurar alpha final = 1
        overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);

        // 7) Pequeña espera opcional para que el usuario perciba la transición
        if (postFadeDelay > 0f)
            yield return new WaitForSecondsRealtime(postFadeDelay);

        // 8) Cargar la escena objetivo
        LoadTargetScene();
    }

    private void LoadTargetScene()
    {
        if (useBuildIndex)
        {
            if (sceneBuildIndex < 0)
            {
                Debug.LogError("[MonitorEnemyDeathLoadScene] sceneBuildIndex inválido.");
                return;
            }
            SceneManager.LoadScene(sceneBuildIndex, LoadSceneMode.Single);
        }
        else
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[MonitorEnemyDeathLoadScene] sceneName vacío.");
                return;
            }
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
