using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class InteractionEvents : MonoBehaviour
{
    [Header("Eventos")]
    [SerializeField] private UnityEvent OnObjectEntered;
    [SerializeField] private UnityEvent OnObjectExited;

    [SerializeField] private string tagName;

    [Header("Configuración de Trigger")]
    [SerializeField] private bool oneShotTrigger = false;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (oneShotTrigger && hasTriggered) return;

        bool shouldInvoke = false;

        if (tagName == "")
        {
            shouldInvoke = true;
        }
        else
        {
            if (other.CompareTag(tagName)) shouldInvoke = true;
        }

        if (shouldInvoke)
        {
            OnObjectEntered?.Invoke();

            if (oneShotTrigger)
            {
                hasTriggered = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (oneShotTrigger && hasTriggered) return;

        bool shouldInvoke = false;

        if (tagName == "")
        {
            shouldInvoke = true;
        }
        else
        {
            if (other.CompareTag(tagName)) shouldInvoke = true;
        }

        if (shouldInvoke)
        {
            OnObjectExited?.Invoke();
        }
    }

    public void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("The scene name cannot be null or empty.");
            return;
        }

        if (SceneController.Instance != null)
        {
            SceneController.Instance.LoadSceneByName(sceneName);
        }
        else
        {
            Debug.LogWarning("SceneController.Instance no encontrado. Cargando de golpe.");
            SceneManager.LoadScene(sceneName);
        }
    }

    /*
    private IEnumerator LoadSceneWithFade(string sceneName)
    {
        if (FadeController.Instance != null)
        {
            yield return FadeController.Instance.FadeOut();
        }
        else
        {
            Debug.LogWarning("FadeController.Instance is missing. Loading scene without fade.");
        }

        SceneManager.LoadScene(sceneName);
    }

    */
}