using System.Collections;
using UnityEngine;

#region Enum de Modos

public enum TransitionMode
{
    Classic,
    Level1
}

#endregion

public class RoomTransitionController : MonoBehaviour
{
    #region Inspector 

    [Header("Modo de Transición")]
    [SerializeField] private TransitionMode transitionMode = TransitionMode.Classic;

    [Header("Configuración Classic")]
    [SerializeField] private float playerMoveDuration = 0.5f;
    [SerializeField] private float playerMoveDistance = 3f;
    [SerializeField] private float doorActivateDelay = 0.5f;

    [Header("Configuración Level 1")]
    [SerializeField] private TransitionInteractive transitionInteractive;
    [SerializeField] private SequenceTransition sequenceController;

    #endregion

    #region Dependencias

    private PlayerMovement playerMovement;

    #endregion

    #region Unity

    private void Start()
    {
        playerMovement = FindAnyObjectByType<PlayerMovement>();
    }

    #endregion

    #region API pública

    public TransitionMode Mode => transitionMode;
    public float PlayerMoveDuration => playerMoveDuration;
    public float PlayerMoveDistance => playerMoveDistance;

    public IEnumerator RunPreFadeSequence(Transform playerTransform)
    {
        if (transitionMode == TransitionMode.Level1 && sequenceController != null)
        {
            yield return StartCoroutine(sequenceController.ExecuteSequence(playerTransform));
        }
    }

    public bool HasPreFadeSequence =>
        transitionMode == TransitionMode.Level1 && sequenceController != null;

    public IEnumerator MovePlayerSmooth(Transform playerTransform, Vector3 targetPosition, float duration, Vector3? movementDirection = null)
    {
        Vector3 startPosition = playerTransform.position;
        float originalY = startPosition.y;
        targetPosition.y = originalY;

        PlayerAnimCtrl animCtrl = playerTransform.GetComponentInChildren<PlayerAnimCtrl>();
        if (animCtrl == null)
            animCtrl = playerTransform.GetComponent<PlayerAnimCtrl>();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            Vector3 newPos = Vector3.Lerp(startPosition, targetPosition, t);
            newPos.y = originalY;

            if (playerMovement != null)
                playerMovement.TeleportTo(newPos);
            else
                playerTransform.position = newPos;

            if (animCtrl != null && !animCtrl.isDashing && movementDirection.HasValue)
            {
                SetAnimDir(animCtrl, movementDirection.Value);
                animCtrl.PlayState(
                    PlayerAnimCtrl.PlayerState.run,
                    BaseAnimCtrl<PlayerAnimCtrl.PlayerState>.AnimPriority.locomotion,
                    false
                );
            }

            yield return null;
        }

        Vector3 finalPos = targetPosition;
        finalPos.y = originalY;

        if (playerMovement != null)
            playerMovement.TeleportTo(finalPos);
        else
            playerTransform.position = finalPos;

        if (animCtrl != null && !animCtrl.isDashing)
            animCtrl.SetInputAxes(0f, 0f);
    }

    public void SetAnimDirFromWorld(Transform playerTransform, Vector3 worldDir)
    {
        PlayerAnimCtrl animCtrl = playerTransform.GetComponentInChildren<PlayerAnimCtrl>();
        if (animCtrl == null) animCtrl = playerTransform.GetComponent<PlayerAnimCtrl>();
        if (animCtrl == null) return;
        SetAnimDir(animCtrl, worldDir);
    }

    private void SetAnimDir(PlayerAnimCtrl animCtrl, Vector3 worldDir)
    {
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.001f) return;
        worldDir.Normalize();

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;

        Vector3 camRight = cam.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 camFwd = cam.forward;
        camFwd.y = 0f;
        camFwd.Normalize();

        float h = Vector3.Dot(worldDir, camRight);
        float v = Vector3.Dot(worldDir, camFwd);

        h = Mathf.Abs(h) > 0.3f ? Mathf.Sign(h) : 0f;
        v = Mathf.Abs(v) > 0.3f ? Mathf.Sign(v) : 0f;

        animCtrl.SetInputAxes(h, v);
        animCtrl.UpdateDirection(h, v);
    }

    public IEnumerator ActivateDoorDelayed(GameObject door)
    {
        yield return new WaitForSeconds(doorActivateDelay);
        if (door != null) door.SetActive(true);
    }

    #endregion
}