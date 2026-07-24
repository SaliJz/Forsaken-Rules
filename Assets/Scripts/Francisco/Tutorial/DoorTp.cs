using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.Audio;

[RequireComponent(typeof(BoxCollider))]
public class DoorTp : MonoBehaviour
{
    #region Editor Settings

    [Header("Dependencies")]
    [SerializeField]
    private Transform destinationDoor;
    [SerializeField] private UnityEvent onEnter;

    [Header("Optional Sequence")]
    [SerializeField] private SequenceTransition sequenceTransition;

    [Header("Transition Settings")]
    [SerializeField] private float preTeleportMoveDistance = 3f;
    [SerializeField] private float playerMoveDuration = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip transitionStartClip;
    [SerializeField] private float transitionStartVolume = 1f;
    [SerializeField] private AudioMixerGroup outputAudioMixerGroup;

    [Header("Debug/Gizmos")]
    public float gizmoRadius = 1f;
    public Color gizmoColor = Color.cyan;
    public Color pathColor = Color.yellow;

    #endregion

    #region Private Fields

    private PlayerCombatActionManager combatActionManager;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private PlayerAnimCtrl playerAnimCtrl;
    private Transform playerTransform;
    private bool isTransitioning = false;
    private AudioSource audioSource;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        BoxCollider collider = GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (outputAudioMixerGroup != null)
        {
            audioSource.outputAudioMixerGroup = outputAudioMixerGroup;
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        Inicilizate();

        if (destinationDoor == null)
        {
            Debug.LogError($"[DoorTp] Faltan datos de destino en {gameObject.name}. Deshabilitando script.");
            enabled = false;
        }

        if (playerMovement == null)
        {
            Debug.LogWarning("[DoorTp] PlayerMovement no encontrado. El movimiento del jugador no ser� controlado durante la transici�n.");
        }
    }

    private void Inicilizate()
    {
        playerMovement = FindAnyObjectByType<PlayerMovement>();
        combatActionManager = FindAnyObjectByType<PlayerCombatActionManager>();
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        playerTransform = FindAnyObjectByType<PlayerMovement>()?.transform;
        playerAnimCtrl = playerTransform != null ? playerTransform.GetComponentInChildren<PlayerAnimCtrl>() : null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isTransitioning)
        {
            if (playerMovement == null || playerHealth == null || playerTransform == null) Inicilizate();

            StartTransition();
        }
    }

    #endregion

    #region Public Methods

    public void StartTransition()
    {
        if (destinationDoor == null || playerTransform == null)
        {
            Debug.LogWarning("[DoorTp] No se puede iniciar la transici�n. Faltan dependencias.");
            return;
        }

        PlayTransitionStartAudio();
        isTransitioning = true;
        StartCoroutine(TransitionCoroutine());
    }

    private void PlayTransitionStartAudio()
    {
        if (transitionStartClip == null)
        {
            return;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        audioSource.PlayOneShot(transitionStartClip, Mathf.Clamp01(transitionStartVolume));
    }

    #endregion

    #region Private Transition Logic

    private IEnumerator TransitionCoroutine()
    {
        if (combatActionManager != null)
        {
            combatActionManager.enabled = false;
        }

        onEnter?.Invoke();

        if (sequenceTransition != null)
        {
            yield return SequenceTransitionCoroutine();
            yield break;
        }

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        Transform currentDoor = transform;
        float originalPlayerY = playerTransform.position.y;

        Vector3 orthogonalDirection = CalculateOrthogonalDirection(currentDoor.position, destinationDoor.position);

        if (FadeController.Instance != null)
        {
            yield return FadeController.Instance.FadeOut(
               onStart: () =>
               {
                   Vector3 targetPosition = playerTransform.position + orthogonalDirection * preTeleportMoveDistance;
                   targetPosition.y = originalPlayerY;
                   StartCoroutine(MovePlayer(playerTransform, targetPosition, playerMoveDuration, originalPlayerY));
               },
               onComplete: () =>
               {
                   Vector3 spawnPosition = new Vector3(destinationDoor.position.x, originalPlayerY, destinationDoor.position.z);

                   if (playerMovement != null)
                   {
                       playerMovement.TeleportTo(spawnPosition);
                   }
                   else
                   {
                       playerTransform.position = spawnPosition;
                   }
               }
           );
        }
        else
        {
            Vector3 spawnPosition = new Vector3(destinationDoor.position.x, originalPlayerY, destinationDoor.position.z);
            if (playerMovement != null)
            {
                playerMovement.TeleportTo(spawnPosition);
            }
            else
            {
                playerTransform.position = spawnPosition;
            }
        }

        Vector3 exitDirection = orthogonalDirection;

        if (FadeController.Instance != null)
        {
            yield return FadeController.Instance.FadeIn(
                onStart: () =>
                {
                    Vector3 targetPosition = playerTransform.position + exitDirection * preTeleportMoveDistance;
                    targetPosition.y = originalPlayerY;
                    StartCoroutine(MovePlayer(playerTransform, targetPosition, playerMoveDuration, originalPlayerY));
                },
                onComplete: () =>
                {
                    if (playerMovement != null)
                    {
                        playerMovement.SetCanMove(true);
                    }
                    if (combatActionManager != null)
                    {
                        combatActionManager.enabled = true;
                    }
                    isTransitioning = false;
                }
            );
        }
        else
        {
            if (playerMovement != null)
            {
                playerMovement.SetCanMove(true);
            }
            if (combatActionManager != null)
            {
                combatActionManager.enabled = true;
            }
            isTransitioning = false;
        }
    }

    private IEnumerator SequenceTransitionCoroutine()
    {
        yield return StartCoroutine(sequenceTransition.ExecuteSequence(playerTransform));

        while (sequenceTransition.IsSequenceRunning)
        {
            yield return null;
        }

        if (playerAnimCtrl != null)
        {
            playerAnimCtrl.SetInputAxes(0f, 0f);
            playerAnimCtrl.PlayState(PlayerAnimCtrl.PlayerState.idle, BaseAnimCtrl<PlayerAnimCtrl.PlayerState>.AnimPriority.dash, true);
        }

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        float originalPlayerY = playerTransform.position.y;

        if (FadeController.Instance != null)
        {
            yield return FadeController.Instance.FadeOut(
                onStart: null,
                onComplete: () =>
                {
                    Vector3 spawnPosition = new Vector3(destinationDoor.position.x, originalPlayerY, destinationDoor.position.z);

                    if (playerMovement != null)
                    {
                        playerMovement.TeleportTo(spawnPosition);
                    }
                    else
                    {
                        playerTransform.position = spawnPosition;
                    }
                }
            );

            yield return FadeController.Instance.FadeIn(null, null);
        }
        else
        {
            Vector3 spawnPosition = new Vector3(destinationDoor.position.x, originalPlayerY, destinationDoor.position.z);
            if (playerMovement != null)
            {
                playerMovement.TeleportTo(spawnPosition);
            }
            else
            {
                playerTransform.position = spawnPosition;
            }
        }

        sequenceTransition.RestoreControl();

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(true);
        }

        if (combatActionManager != null)
        {
            combatActionManager.enabled = true;
        }

        if (playerAnimCtrl != null)
        {
            playerAnimCtrl.ResetToGameplayState();
        }

        isTransitioning = false;
    }

    private Vector3 CalculateOrthogonalDirection(Vector3 origin, Vector3 destination)
    {
        Vector3 direction = (destination - origin);
        direction.y = 0;

        float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;

        float roundedAngle = Mathf.Round(angle / 90f) * 90f;

        float radianAngle = roundedAngle * Mathf.Deg2Rad;
        Vector3 orthogonalDirection = new Vector3(Mathf.Cos(radianAngle), 0, Mathf.Sin(radianAngle)).normalized;

        if (Mathf.Abs(orthogonalDirection.x) > Mathf.Abs(orthogonalDirection.z))
        {
            return new Vector3(Mathf.Sign(orthogonalDirection.x), 0, 0);
        }
        else
        {
            return new Vector3(0, 0, Mathf.Sign(orthogonalDirection.z));
        }
    }

    private IEnumerator MovePlayer(Transform playerTransform, Vector3 targetPosition, float duration, float originalY)
    {

        Vector3 startPosition = playerTransform.position;
        targetPosition.y = originalY;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            Vector3 newPosition = Vector3.Lerp(startPosition, targetPosition, t);
            newPosition.y = originalY;

            if (playerMovement != null)
            {
                playerMovement.TeleportTo(newPosition);
            }
            else
            {
                playerTransform.position = newPosition;
            }
            yield return null;
        }

        Vector3 finalPos = targetPosition;
        finalPos.y = originalY;

        if (playerMovement != null)
        {
            playerMovement.TeleportTo(finalPos);
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (destinationDoor != null)
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, gizmoRadius);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, destinationDoor.position);
            Gizmos.DrawSphere(destinationDoor.position, gizmoRadius * 0.5f);

            Vector3 orthogonalDirection = CalculateOrthogonalDirection(transform.position, destinationDoor.position);

            Vector3 startPathIn = transform.position;
            Vector3 endPathIn = startPathIn + orthogonalDirection * preTeleportMoveDistance;

            Gizmos.color = pathColor;
            Gizmos.DrawLine(startPathIn, endPathIn);
            Gizmos.DrawSphere(endPathIn, gizmoRadius * 0.3f);

            Vector3 startPathOut = destinationDoor.position;
            Vector3 endPathOut = startPathOut + orthogonalDirection * preTeleportMoveDistance;

            Gizmos.color = pathColor;
            Gizmos.DrawLine(startPathOut, endPathOut);
            Gizmos.DrawSphere(endPathOut, gizmoRadius * 0.3f);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position, gizmoRadius);
            Gizmos.DrawRay(transform.position, transform.forward * (gizmoRadius * 2f));
        }
    }

    #endregion
}