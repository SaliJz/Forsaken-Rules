using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PassScene : MonoBehaviour
{
    PlayerStatsManager statsManager;

    private void Awake()
    {
        statsManager = FindAnyObjectByType<PlayerStatsManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        StartCoroutine(FadeAndReloadScene("MainMenu"));
        Time.timeScale = 0f;
    }

    private IEnumerator FadeAndReloadScene(string sceneName)
    {
        // if (FadeController.Instance != null && FadeController.Instance.fade != null)
        if (FadeController.Instance != null)
        {
            yield return StartCoroutine(FadeController.Instance.FadeOut());
        }

        Time.timeScale = 1f;
        if (statsManager != null) statsManager.ResetRunStatsToDefaults();
        InventoryManager inventory = FindAnyObjectByType<InventoryManager>();
        inventory.ClearInventory(); 
        SceneManager.LoadScene(sceneName);
    }
}