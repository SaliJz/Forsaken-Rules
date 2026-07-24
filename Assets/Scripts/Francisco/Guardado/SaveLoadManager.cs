using UnityEngine;

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    private const string SAVE_KEY_PREFIX = "SaveSlot_";
    private const string INTRO_SCENE = "INTRO";
    private const string MAIN_GAME_SCENE = "HUB";

    public int numberOfSlots = 3;

    private int currentActiveSlot = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool DoesSlotExist(int slotIndex)
    {
        return PlayerPrefs.HasKey(SAVE_KEY_PREFIX + slotIndex);
    }

    public SaveData LoadGame(int slotIndex)
    {
        string key = SAVE_KEY_PREFIX + slotIndex;

        if (PlayerPrefs.HasKey(key))
        {
            string json = PlayerPrefs.GetString(key);
            return JsonUtility.FromJson<SaveData>(json);
        }

        return new SaveData();
    }

    public void StartGameFromSlot(int slotIndex)
    {
        currentActiveSlot = slotIndex;

        SaveData data = LoadGame(slotIndex);

        string sceneToLoad;
        if (!data.hasPassedTutorial)
        {
            sceneToLoad = INTRO_SCENE;
        }
        else
        {
            sceneToLoad = data.lastSceneName;
        }

        if (SceneController.Instance != null)
        {
            SceneController.Instance.LoadSceneByName(sceneToLoad);
        }
    }

    public void StartTutorial(int slotIndex)
    {
        currentActiveSlot = slotIndex;

        if (SceneController.Instance != null)
        {
            SceneController.Instance.LoadSceneByName(INTRO_SCENE);
        }
    }

    public void CompleteTutorialAndSave()
    {
        if (currentActiveSlot == -1)
        {
            Debug.LogError("[SaveLoadManager] No hay un slot activo para guardar el progreso del tutorial.");
            return;
        }

        SaveData data = LoadGame(currentActiveSlot);

        data.hasPassedTutorial = true;
        data.lastSceneName = MAIN_GAME_SCENE;

        SaveCurrentGame(currentActiveSlot, data);
    }

    public void DeleteGameSlot(int slotIndex)
    {
        string key = SAVE_KEY_PREFIX + slotIndex;

        if (PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Debug.Log($"[SaveLoadManager] Datos borrados del Slot {slotIndex}.");

            if (currentActiveSlot == slotIndex)
            {
                currentActiveSlot = -1;
            }
        }
    }

    public void SaveCurrentGame(int slotIndex, SaveData data)
    {
        string key = SAVE_KEY_PREFIX + slotIndex;
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }
}