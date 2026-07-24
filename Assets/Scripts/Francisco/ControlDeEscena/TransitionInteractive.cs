using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransitionInteractive : MonoBehaviour
{
    #region Inspector

    [Header("Listas de la Línea de Tiempo")]
    [SerializeField] protected List<PlayerNode> playerNodes = new List<PlayerNode>();
    [SerializeField] protected List<PlatformNode> platformNodes = new List<PlatformNode>();
    [SerializeField] protected List<FadeEvent> fadeEvents = new List<FadeEvent>();
    [SerializeField] protected List<AnimationEvent> animationEvents = new List<AnimationEvent>();

    [Header("Visualización de Gizmos")]
    [SerializeField] private Color playerGizmoColor = Color.green;
    [SerializeField] private Color platformGizmoColor = Color.yellow;
    [SerializeField] private float gizmoSphereSize = 0.3f;

    #endregion

    #region Referencias Internas

    private CharacterController characterController;
    private PlayerMovement playerMovement;
    private PlayerAnimCtrl playerAnimCtrl;
    private Transform playerTransform;

    #endregion

    #region Estado

    private bool isRunning = false;

    #endregion

    #region API Pública

    public bool IsRunning => isRunning;
    public List<PlayerNode> PlayerNodes { get => playerNodes; set => playerNodes = value; }
    public List<PlatformNode> PlatformNodes { get => platformNodes; set => platformNodes = value; }
    public List<FadeEvent> FadeEvents { get => fadeEvents; set => fadeEvents = value; }
    public List<AnimationEvent> AnimationEvents { get => animationEvents; set => animationEvents = value; }

    public IEnumerator RunNodes(Transform player)
    {
        playerTransform = player;
        characterController = player.GetComponent<CharacterController>();
        playerMovement = player.GetComponent<PlayerMovement>();
        playerAnimCtrl = player.GetComponentInChildren<PlayerAnimCtrl>();

        PlayerShieldController shieldController = player.GetComponent<PlayerShieldController>();
        if (shieldController != null)
        {
            shieldController.ForceRecallShield();
        }

        isRunning = true;

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        playerMovement?.SetCanMove(false);

        if (playerAnimCtrl != null)
        {
            playerAnimCtrl.SetInputAxes(0f, 0f);
            playerAnimCtrl.PlayState(
                PlayerAnimCtrl.PlayerState.idle,
                BaseAnimCtrl<PlayerAnimCtrl.PlayerState>.AnimPriority.dash,
                true
            );
        }

        float elapsedTime = 0f;
        int nextPlayerIdx = 0;
        int nextPlatformIdx = 0;
        int nextFadeIdx = 0;
        int nextAnimIdx = 0;

        SortAllEvents();

        bool isPlayerAttached = false;
        bool playerMovementFinished = false;

        while (nextPlayerIdx < playerNodes.Count ||
               nextPlatformIdx < platformNodes.Count ||
               nextFadeIdx < fadeEvents.Count ||
               nextAnimIdx < animationEvents.Count)
        {
            if (Time.timeScale == 0f)
            {
                yield return null;
                continue;
            }

            while (nextAnimIdx < animationEvents.Count && elapsedTime >= animationEvents[nextAnimIdx].timeTrigger)
            {
                var ev = animationEvents[nextAnimIdx];
                if (ev.animator != null && !string.IsNullOrEmpty(ev.paramName))
                    ev.animator.SetBool(ev.paramName, ev.value);
                nextAnimIdx++;
            }

            while (nextFadeIdx < fadeEvents.Count && elapsedTime >= fadeEvents[nextFadeIdx].timeTrigger)
            {
                if (FadeController.Instance != null)
                {
                    yield return StartCoroutine(FadeController.Instance.FadeOut(
                        onStart: null,
                        onUpdate: null,
                        onComplete: null,
                        fadeColor: null,
                        respectPause: true
                    ));
                }
                nextFadeIdx++;
            }

            if (nextPlatformIdx < platformNodes.Count && elapsedTime >= platformNodes[nextPlatformIdx].timeTrigger)
            {
                PlatformNode currentPlatform = platformNodes[nextPlatformIdx];
                if (!isPlayerAttached)
                {
                    playerTransform.SetParent(transform);
                    isPlayerAttached = true;
                }
                StartCoroutine(MovePlatformRoutine(currentPlatform));
                nextPlatformIdx++;
            }

            if (!playerMovementFinished &&
                 nextPlayerIdx < playerNodes.Count &&
                 elapsedTime >= playerNodes[nextPlayerIdx].timeTrigger)
            {
                PlayerNode currentPlayer = playerNodes[nextPlayerIdx];
                Vector3 startPos = playerTransform.position;
                Vector3 targetPos = currentPlayer.nodeTransform != null ? currentPlayer.nodeTransform.position : startPos;

                if (!currentPlayer.useYAxis)
                    targetPos.y = startPos.y;

                float speed = currentPlayer.speed > 0f ? currentPlayer.speed : 3f;

                while (Vector3.Distance(playerTransform.position, targetPos) > 0.05f)
                {
                    elapsedTime += Time.deltaTime;
                    Vector3 prevPos = playerTransform.position;

                    playerTransform.position = Vector3.MoveTowards(
                        playerTransform.position,
                        targetPos,
                        speed * Time.deltaTime);

                    Vector3 moveDir = playerTransform.position - prevPos;
                    moveDir.y = 0f;

                    if (moveDir.sqrMagnitude > 0.001f)
                    {
                        moveDir.Normalize();
                        playerTransform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);

                        SetAnimDir(moveDir);

                        playerAnimCtrl?.PlayState(
                            PlayerAnimCtrl.PlayerState.run,
                            BaseAnimCtrl<PlayerAnimCtrl.PlayerState>.AnimPriority.dash,
                            false);
                    }

                    ProcessTimelineTriggers(ref nextAnimIdx, ref nextFadeIdx, elapsedTime);
                    yield return null;
                }

                playerTransform.position = targetPos;

                if (nextPlayerIdx == playerNodes.Count - 1)
                {
                    if (playerAnimCtrl != null)
                    {
                        playerAnimCtrl.SetInputAxes(0f, 0f);
                        playerAnimCtrl.UpdateDirection(0f, 0f);

                        playerAnimCtrl.PlayState(
                            PlayerAnimCtrl.PlayerState.idle,
                            BaseAnimCtrl<PlayerAnimCtrl.PlayerState>.AnimPriority.dash,
                            true
                        );
                    }
                }

                nextPlayerIdx++;

                if (nextPlayerIdx >= playerNodes.Count)
                {
                    playerMovementFinished = true;
                }
            }

            if (playerMovementFinished || nextPlayerIdx >= playerNodes.Count || elapsedTime < playerNodes[nextPlayerIdx].timeTrigger)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }

        if (playerTransform != null && playerTransform.parent == transform)
            playerTransform.SetParent(null);

        yield return new WaitForEndOfFrame();

        if (playerAnimCtrl != null)
        {
            playerAnimCtrl.SetInputAxes(0f, 0f);
            playerAnimCtrl.ResetToGameplayState();
        }

        if (playerMovement != null)
        {
            playerMovement.ResetMovementState();
            playerMovement.SetCanMove(true);
        }

        if (characterController != null)
            characterController.enabled = true;

        isRunning = false;
    }

    public void RestoreControl()
    {
        if (characterController != null) characterController.enabled = true;
        playerMovement?.SetCanMove(true);
    }

    public void SortAllEvents()
    {
        playerNodes.Sort((a, b) => a.timeTrigger.CompareTo(b.timeTrigger));
        platformNodes.Sort((a, b) => a.timeTrigger.CompareTo(b.timeTrigger));
        fadeEvents.Sort((a, b) => a.timeTrigger.CompareTo(b.timeTrigger));
        animationEvents.Sort((a, b) => a.timeTrigger.CompareTo(b.timeTrigger));
    }

    #endregion

    #region Sub-Rutinas Independientes

    private IEnumerator MovePlatformRoutine(PlatformNode node)
    {
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + node.movementOffset;
        float speed = node.speed > 0f ? node.speed : 2f;

        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPos;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (platformNodes != null && platformNodes.Count > 0)
        {
            Vector3 currentPlatformPos = transform.position;
            Gizmos.color = platformGizmoColor;
            for (int i = 0; i < platformNodes.Count; i++)
            {
                Vector3 nextPos = currentPlatformPos + platformNodes[i].movementOffset;
                Gizmos.DrawLine(currentPlatformPos, nextPos);
                Gizmos.DrawWireSphere(nextPos, gizmoSphereSize);
                currentPlatformPos = nextPos;
            }
        }

        if (playerNodes != null && playerNodes.Count > 0)
        {
            Gizmos.color = playerGizmoColor;
            Vector3? lastPos = null;

            for (int i = 0; i < playerNodes.Count; i++)
            {
                if (playerNodes[i].nodeTransform != null)
                {
                    Vector3 targetPos = playerNodes[i].nodeTransform.position;
                    Gizmos.DrawSphere(targetPos, gizmoSphereSize * 0.6f);
                    if (lastPos.HasValue)
                        Gizmos.DrawLine(lastPos.Value, targetPos);
                    lastPos = targetPos;
                }
            }
        }
    }

    #endregion

    #region Lógica Interna

    private void ProcessTimelineTriggers(ref int nextAnimIdx, ref int nextFadeIdx, float currentElapsedTime)
    {
        while (nextAnimIdx < animationEvents.Count && currentElapsedTime >= animationEvents[nextAnimIdx].timeTrigger)
        {
            var ev = animationEvents[nextAnimIdx];
            if (ev.animator != null && !string.IsNullOrEmpty(ev.paramName)) ev.animator.SetBool(ev.paramName, ev.value);
            nextAnimIdx++;
        }
        while (nextFadeIdx < fadeEvents.Count && currentElapsedTime >= fadeEvents[nextFadeIdx].timeTrigger)
        {
            if (FadeController.Instance != null)
            {
                StartCoroutine(FadeController.Instance.FadeOut(
                    onStart: null,
                    onUpdate: null,
                    onComplete: null,
                    fadeColor: null,
                    respectPause: true
                ));
            }
            nextFadeIdx++;
        }
    }

    private void SetAnimDir(Vector3 worldDir)
    {
        if (playerAnimCtrl == null) return;
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

        playerAnimCtrl.SetInputAxes(h, v);
        playerAnimCtrl.UpdateDirection(h, v);
    }

    #endregion
}