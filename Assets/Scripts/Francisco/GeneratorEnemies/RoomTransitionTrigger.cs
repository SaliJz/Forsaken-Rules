using System.Collections;
using UnityEngine;

public class RoomTransitionTrigger : MonoBehaviour
{
    #region Inspector

    [Header("Room Generation")]
    [SerializeField] private GameObject roomPrefab;
    [SerializeField] private float generationDistance = 50f;
    [SerializeField] private string playerSpawnPointName = "SpawnPoint";

    [Header("Player Control References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerMeleeAttack playerMeleeAttack;
    [SerializeField] private PlayerShieldController playerShieldController;

    [Header("Sequence")]
    [SerializeField] private SequenceTransition sequenceTransition;

    #endregion

    #region Private State

    private bool hasTriggered = false;
    private GameObject playerGameObject;

    #endregion

    #region Public Properties

    public static bool IsTransitioning { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerGameObject = GameObject.FindGameObjectWithTag("Player");
        if (playerGameObject != null)
        {
            playerMovement = playerGameObject.GetComponent<PlayerMovement>();
            playerMeleeAttack = playerGameObject.GetComponent<PlayerMeleeAttack>();
            playerShieldController = playerGameObject.GetComponent<PlayerShieldController>();
        }
    }

    #endregion

    #region Trigger Detection

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            hasTriggered = true;
            StartCoroutine(TransitionToNextRoomRoutine());
        }
    }

    #endregion

    #region Transition Flow

    private IEnumerator TransitionToNextRoomRoutine()
    {
        IsTransitioning = true;

        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen)
        {
            InventoryUIManager.Instance.CloseInventory();
        }

        SetPlayerControls(false);

        Animator playerAnimator = playerGameObject != null ? playerGameObject.GetComponentInChildren<Animator>() : null;
        if (playerAnimator != null)
        {
            playerAnimator.SetFloat("Speed", 0f);
            playerAnimator.Play("Idle");
        }

        if (sequenceTransition != null)
        {
            yield return StartCoroutine(sequenceTransition.ExecuteSequence(playerGameObject.transform));
        }

        yield return StartCoroutine(FadeController.Instance.FadeOut());

        Vector3 spawnPosition = transform.position + transform.forward * generationDistance;
        GameObject spawnedRoom = Instantiate(roomPrefab, spawnPosition, Quaternion.identity);

        Transform playerSpawnPoint = FindPlayerSpawnPointDeep(spawnedRoom.transform);
        if (playerSpawnPoint != null)
        {
            if (playerMovement != null)
            {
                playerMovement.TeleportTo(playerSpawnPoint.position);
            }
            else
            {
                playerGameObject.transform.position = playerSpawnPoint.position;
            }
        }
        else
        {
            Debug.LogError($"RoomTransitionTrigger: '{playerSpawnPointName}' could not be found in the spawned room hierarchy.");
        }

        yield return StartCoroutine(FadeController.Instance.FadeIn());

        SetPlayerControls(true);
        IsTransitioning = false;
    }

    #endregion

    #region Helper Methods

    private Transform FindPlayerSpawnPointDeep(Transform parent)
    {
        if (parent.name == playerSpawnPointName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindPlayerSpawnPointDeep(parent.GetChild(i));
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void SetPlayerControls(bool enabled)
    {
        if (playerMovement != null) playerMovement.enabled = enabled;
        if (playerMeleeAttack != null) playerMeleeAttack.enabled = enabled;
        if (playerShieldController != null) playerShieldController.enabled = enabled;
    }

    #endregion
}