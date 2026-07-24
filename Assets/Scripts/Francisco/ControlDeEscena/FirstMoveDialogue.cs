using UnityEngine;
using System.Collections;
using UnityEngine.Events; 

public class FirstMoveDialogue : MonoBehaviour
{
    #region Editor Settings
    [Header("Dialogo a Mostrar")]
    public DialogLine[] FirstMoveDialog;
    public float requiredMoveDistance = 0.5f;

    [Header("Eventos")]
    public UnityEvent OnDialogueFinished;
    #endregion

    #region Private Fields
    private Transform playerTransform;
    private Vector3 lastPosition;
    private float totalDistanceMoved = 0f;
    private bool shouldMonitor = false;
    private bool hasTriggered = false;
    #endregion

    #region Unity Methods
    private IEnumerator Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogError("No se encontró un GameObject con el tag 'Player'");
            yield break;
        }

        playerTransform = player.transform;
        lastPosition = playerTransform.position;

        if (FadeController.Instance != null)
        {
            yield return new WaitUntil(() => !FadeController.Instance.IsFading);
        }

        shouldMonitor = true;
    }

    private void Update()
    {
        if (hasTriggered || !shouldMonitor || playerTransform == null)
        {
            return;
        }

        Vector3 currentPosition = playerTransform.position;
        float frameDeltaDistance = Vector3.Distance(lastPosition, currentPosition);

        totalDistanceMoved += frameDeltaDistance;

        lastPosition = currentPosition;

        if (totalDistanceMoved >= requiredMoveDistance)
        {
            hasTriggered = true;
            shouldMonitor = false;
            StartCoroutine(ExecuteDialogFlow());
        }
    }
    #endregion

    #region Core Dialog Logic
    private IEnumerator ExecuteDialogFlow()
    {
        if (DialogManager.Instance == null)
        {
            Debug.LogError("DialogManager no está en la escena.");
            yield break;
        }

        DialogManager.Instance.StartDialog(FirstMoveDialog);

        while (DialogManager.Instance.IsActive)
        {
            yield return null;
        }

        OnDialogueFinished?.Invoke();

        Destroy(this);
    }
    #endregion
}