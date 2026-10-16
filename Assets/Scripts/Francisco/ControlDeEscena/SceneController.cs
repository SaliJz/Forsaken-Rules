using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance;

    [Header("Input General")]
    [SerializeField] private KeyCode _inputKey;
    [SerializeField] private UnityEvent OnKeyPressed;

    [Header("Configuraci�n Centralizada")]
    [SerializeField] private SceneShortcutData shortcutData;

    public bool IsTransitioning { get; private set; }

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

    private void Update()
    {
        if ( Input.GetKeyDown(_inputKey))
        {
            OnKeyPressed.Invoke();
        }

        CheckSceneTransitions();
    }

    private void CheckSceneTransitions()
    {
        if (shortcutData == null || shortcutData.sceneTransitions == null) return;

        foreach (var transition in shortcutData.sceneTransitions)
        {
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(transition.inputKey))
            {
                LoadSceneByName(transition.targetSceneName);
                break; 
            }
        }
    }

    public void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("The scene name cannot be null or empty.");
            return;
        }

        if (IsTransitioning) return;
        StartCoroutine(LoadSceneWithFade(sceneName));
    }

    public void ReloadCurrentScene()
    {
        if (IsTransitioning) return;
        StartCoroutine(LoadSceneWithFade(SceneManager.GetActiveScene().name));
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private IEnumerator LoadSceneWithFade(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' is not available in the build settings.");
            yield break;
        }

        IsTransitioning = true;

        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen)
        {
            InventoryUIManager.Instance.CloseInventory();
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            PlayerShieldController shieldController = playerObj.GetComponent<PlayerShieldController>();
            if (shieldController != null) shieldController.ForceRecallShield();
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            IsTransitioning = false;
            Debug.LogError($"Could not start loading scene '{sceneName}'.");
            yield break;
        }

        loadOperation.allowSceneActivation = false;

        if (FadeController.Instance != null)
        {
            yield return FadeController.Instance.FadeOut();
        }
        else
        {
            Debug.LogWarning("FadeController.Instance is missing. Loading scene without fade.");
        }

        while (loadOperation.progress < 0.9f)
        {
            yield return null;
        }

        loadOperation.allowSceneActivation = true;
        while (!loadOperation.isDone)
        {
            yield return null;
        }
    }

    public void OnTutorialFinished()
    {
        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.CompleteTutorialAndSave();
        }
    }
}