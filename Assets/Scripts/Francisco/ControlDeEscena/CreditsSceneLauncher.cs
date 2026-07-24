using UnityEngine;

public class CreditsSceneLauncher : MonoBehaviour
{
    #region Inspector

    [Header("References")]
    [SerializeField] private CreditsPanel creditsPanel;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (creditsPanel == null)
        {
            creditsPanel = Object.FindFirstObjectByType<CreditsPanel>();
        }

        if (creditsPanel != null)
        {
            creditsPanel.gameObject.SetActive(true);
            creditsPanel.OpenPanel();
        }
        else
        {
            Debug.LogError("CreditsSceneLauncher: No se encontró ningún CreditsPanel en la escena.");
        }
    }

    #endregion
}