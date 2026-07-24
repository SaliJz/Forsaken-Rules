using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using static BaseAnimCtrl<PlayerAnimCtrl.PlayerState>;
using static PlayerAnimCtrl;

public class BossIntroDirector : MonoBehaviour
{
    #region Inspector

    [Header("Sequence and Control")]
    [SerializeField] private SequenceTransition introSequence;
    [SerializeField] private float cameraBlendDuration = 1.5f;
    [SerializeField] private float uiAnimationDuration = 5.0f;

    [Header("Boss References")]
    [SerializeField] private GameObject bossGameObject;

    [Header("User Interface")]
    [SerializeField] private GameObject bossIntroCanvas;

    [Header("Camera Configuration")]
    [SerializeField] private string canvasCameraName = "BossIntroCamera"; 

    [Header("Shake Settings")]
    [SerializeField] private float shakeDelayAfterUiActive = 0.5f;

    #endregion

    #region Private State

    private bool isIntroRunning = false;
    private CinemachineCamera activeCinemachineCamera;
    private Transform originalCameraTarget;
    private Camera canvasCamera;

    #endregion

    #region Public Properties

    public static bool IsPlayingCutscene { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        canvasCamera = FindCameraByName();

        if (canvasCamera != null)
        {
            canvasCamera.cullingMask = 0;
        }
    }

    #endregion

    #region Public API

    public IEnumerator BossIntroRoutine(Transform playerTransform)
    {
        if (isIntroRunning) yield break;
        isIntroRunning = true;
        IsPlayingCutscene = true;

        PlayerAnimCtrl playerAnimator = playerTransform.GetComponentInChildren<PlayerAnimCtrl>();

        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen)
        {
            InventoryUIManager.Instance.CloseInventory();
        }

        if (introSequence != null)
        {
            yield return StartCoroutine(introSequence.ExecuteSequence(playerTransform));

            while (introSequence.IsSequenceRunning)
            {
                yield return null;
            }

            if (playerAnimator != null)
            {
                playerAnimator.SetInputAxes(0f, 0f);
                playerAnimator.PlayState(PlayerState.idle, AnimPriority.dash, true);
            }
        }

        DisablePlayerControls(playerTransform, false);
        FindActiveCinemachineCamera();
        DisableBossLogicScripts();

        if (canvasCamera == null) canvasCamera = FindCameraByName();
        if (canvasCamera != null)
        {
            canvasCamera.cullingMask = LayerMask.GetMask("Enemy");
            canvasCamera.gameObject.SetActive(true);
        }

        if (activeCinemachineCamera != null && bossGameObject != null)
        {
            originalCameraTarget = activeCinemachineCamera.Target.TrackingTarget;
            activeCinemachineCamera.Target.TrackingTarget = bossGameObject.transform;
        }

        yield return new WaitForSeconds(cameraBlendDuration);

        if (bossIntroCanvas != null)
        {
            Canvas canvas = bossIntroCanvas.GetComponent<Canvas>();
            if (canvas != null && canvasCamera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = canvasCamera;
            }

            bossIntroCanvas.SetActive(true);

            Animator uiAnimator = bossIntroCanvas.GetComponent<Animator>();
            if (uiAnimator != null)
            {
                uiAnimator.updateMode = AnimatorUpdateMode.Normal;
            }
        }

        StartCoroutine(ExecuteDelayedShakeRoutine());

        yield return new WaitForSeconds(uiAnimationDuration);

        if (bossIntroCanvas != null)
        {
            bossIntroCanvas.SetActive(false);
        }

        if (canvasCamera != null)
        {
            canvasCamera.cullingMask = 0;
        }

        if (activeCinemachineCamera != null && originalCameraTarget != null)
        {
            activeCinemachineCamera.Target.TrackingTarget = originalCameraTarget;
            yield return new WaitForSeconds(cameraBlendDuration);
        }

        EnableBossLogicScripts();
        DisablePlayerControls(playerTransform, true);

        if (introSequence != null)
        {
            introSequence.RestoreControl();
        }

        if (playerAnimator != null)
        {
            playerAnimator.ResetToGameplayState();
        }

        isIntroRunning = false;
        IsPlayingCutscene = false;
    }

    #endregion

    #region Helper Methods

    private IEnumerator ExecuteDelayedShakeRoutine()
    {
        yield return new WaitForSeconds(shakeDelayAfterUiActive);

        CinemachineImpulseSource impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }
    }

    private Camera FindCameraByName()
    {
        if (string.IsNullOrEmpty(canvasCameraName))
        {
            Debug.LogWarning($"[{name}] El nombre de la cámara no está configurado en el inspector.");
            return null;
        }

        GameObject camObject = GameObject.Find(canvasCameraName);

        if (camObject != null)
        {
            Camera cam = camObject.GetComponent<Camera>();
            if (cam != null) return cam;

            Debug.LogError($"[{name}] Se encontró el objeto '{canvasCameraName}', pero no tiene un componente Camera adjunto.");
        }
        else
        {
            Debug.LogWarning($"[{name}] No se pudo encontrar ninguna cámara con el nombre '{canvasCameraName}' en la jerarquía.");
        }

        return null;
    }

    private void FindActiveCinemachineCamera()
    {
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;

        if (brain != null && brain.ActiveVirtualCamera != null)
        {
            activeCinemachineCamera = brain.ActiveVirtualCamera as CinemachineCamera;
        }
        else
        {
            activeCinemachineCamera = Object.FindFirstObjectByType<CinemachineCamera>();
        }
    }

    private void DisableBossLogicScripts()
    {
        if (bossGameObject == null) return;

        MonoBehaviour[] scripts = bossGameObject.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script == null || script == this || script.GetType().Namespace?.StartsWith("UnityEngine") == true)
                continue;

            script.enabled = false;
        }
    }

    private void EnableBossLogicScripts()
    {
        if (bossGameObject == null) return;

        MonoBehaviour[] scripts = bossGameObject.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script == null || script == this || script.GetType().Namespace?.StartsWith("UnityEngine") == true)
                continue;

            script.enabled = true;
        }
    }

    private void DisablePlayerControls(Transform playerTransform, bool state)
    {
        if (playerTransform == null) return;

        MonoBehaviour[] scripts = playerTransform.GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script == null || script == this)
                continue;

            if (script.GetType().Namespace?.StartsWith("UnityEngine") == true)
                continue;

            if (script is PlayerAnimCtrl ||
                script.GetType().Name.Contains("SpriteAnimator") ||
                script.GetType().Name.Contains("AnimationEvent"))
            {
                continue;
            }

            string scriptName = script.GetType().Name;
            if (scriptName.Contains("Cinemachine") || scriptName.Contains("Feedback") || scriptName.Contains("Distortion") || scriptName.Contains("Billboard"))
                continue;

            script.enabled = state;
        }

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = state;
        }
    }

    #endregion
}