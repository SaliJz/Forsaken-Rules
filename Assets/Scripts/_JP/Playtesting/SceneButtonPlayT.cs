using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneButtonPlayT : MonoBehaviour
{
    public string sceneName = "";

    private Button btn;

    void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(() => SceneManager.LoadScene(sceneName));
    }

    void OnDestroy()
    {
        btn.onClick.RemoveAllListeners();
    }
}
