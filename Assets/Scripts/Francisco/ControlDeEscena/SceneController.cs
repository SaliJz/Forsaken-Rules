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

    [Header("Configuración Centralizada")]
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

        StartCoroutine(LoadSceneWithFade(sceneName));
    }

    public void ReloadCurrentScene()
    {
        StartCoroutine(LoadSceneWithFade(SceneManager.GetActiveScene().name));
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private IEnumerator LoadSceneWithFade(string sceneName)
    {
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

    public void OnTutorialFinished()
    {
        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.CompleteTutorialAndSave();
        }
    }
}