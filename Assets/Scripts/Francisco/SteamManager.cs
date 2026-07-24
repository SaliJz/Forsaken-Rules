using UnityEngine;
using Steamworks;

#region Configuracion
public class SteamManager : MonoBehaviour
{
    private static SteamManager instance;
    private static AppId_t gameAppId = new AppId_t(4858720);
    private bool isInitialized;

    public static bool Initialized => instance != null && instance.isInitialized;
    public static bool OverlayActive { get; private set; }
    private Callback<GameOverlayActivated_t> gameOverlayActivatedCallback;
    #endregion

    #region Ciclo De Vida
    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeSteam();
    }

    private void Update()
    {
        if (isInitialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            if (isInitialized)
            {
                SteamAPI.Shutdown();
            }
            instance = null;
        }
    }
    #endregion

    #region Inicializacion
    private void InitializeSteam()
    {
        if (!Packsize.Test())
        {
            HandleInitFailure("Packsize Test failed");
            return;
        }

        if (!DllCheck.Test())
        {
            HandleInitFailure("DllCheck Test failed");
            return;
        }

        System.Environment.SetEnvironmentVariable("SteamAppId", "4858720");
        System.Environment.SetEnvironmentVariable("RestartAppIfNecessary", "false");

        try
        {
            isInitialized = SteamAPI.Init();
        }
        catch (System.Exception e)
        {
            HandleInitFailure(e.Message);
            return;
        }

        if (isInitialized)
        {
            gameOverlayActivatedCallback = Callback<GameOverlayActivated_t>.Create(OnGameOverlayActivated);
            Debug.Log("[Steamworks] Inicializado correctamente con el ID: " + SteamUtils.GetAppID());
        }
        else
        {
            HandleInitFailure("SteamAPI.Init() returned false");
        }
    }

    private void HandleInitFailure(string reason)
    {
        Debug.Log($"[Steamworks] Init failed: {reason}"); 
        isInitialized = false;
    }

    private void OnGameOverlayActivated(GameOverlayActivated_t callback)
    {
        OverlayActive = callback.m_bActive != 0;

        if (OverlayActive)
        {
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Debug.Log("[Steamworks] Overlay abierto. Juego pausado.");
        }
        else
        {
            if (PauseController.IsGamePaused)
            {
                Time.timeScale = 0f;
                Cursor.visible = false;
                Debug.Log("[Steamworks] Overlay cerrado. Se mantiene pausa del juego.");
            }
            else
            {
                Time.timeScale = 1f;
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Confined;
                Debug.Log("[Steamworks] Overlay cerrado. Juego reanudado.");
            }
        }
    }
    #endregion
}