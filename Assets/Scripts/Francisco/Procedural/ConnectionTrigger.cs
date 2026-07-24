using System.Collections;
using UnityEngine;

public class ConnectionTrigger : MonoBehaviour
{
    [Header("SFX Configuration")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip transitionClip;

    [Header("Transition")]
    [SerializeField] private SequenceTransition transition;

    private PlayerCombatActionManager combatActionManager;
    private DungeonGenerator dungeonGenerator;
    private ConnectionPoint connectionPoint;
    private bool hasTriggered = false;
    public bool isUnlocked = false;

    private void Start()
    {
        dungeonGenerator = FindAnyObjectByType<DungeonGenerator>();
        combatActionManager = FindAnyObjectByType<PlayerCombatActionManager>();
        connectionPoint = GetComponent<ConnectionPoint>();

        if (audioSource == null)
            audioSource = GetComponentInChildren<AudioSource>();
    }

    public void Unlock()
    {
        isUnlocked = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;

        if (hasTriggered ||
            !isUnlocked ||
            connectionPoint == null ||
            connectionPoint.isConnected ||
            dungeonGenerator == null ||
            !other.CompareTag("Player"))
            return;

        hasTriggered = true;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null) box.enabled = false;

        if (audioSource != null && transitionClip != null)
        {
            audioSource.PlayOneShot(transitionClip);
        }

        if (transition != null)
        {
            dungeonGenerator.StartCoroutine(ElevatorThenTransition(other.transform));
        }
        else
        {
            dungeonGenerator.StartCoroutine(dungeonGenerator.TransitionToNextRoom(connectionPoint, other.transform));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && hasTriggered)
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null) box.enabled = false;
        }
    }

    private IEnumerator ElevatorThenTransition(Transform playerTransform)
    {
        var characterController = playerTransform.GetComponent<CharacterController>();

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (combatActionManager != null)
        {
            combatActionManager.enabled = false;
        }

        yield return dungeonGenerator.StartCoroutine(transition.ExecuteSequence(playerTransform));

        yield return dungeonGenerator.StartCoroutine(
            dungeonGenerator.TransitionToNextRoom(
                connectionPoint,
                playerTransform,
                true));
    }
}