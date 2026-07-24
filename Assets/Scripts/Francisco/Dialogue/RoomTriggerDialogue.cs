using UnityEngine;
using System.Collections;
using UnityEngine.Events;

public class RoomTriggerDialogue : MonoBehaviour
{
    public DialogLine[] Sala1_IntroDialog;
    public UnityEvent onFinishDialog;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !other.CompareTag("Player"))
        {
            return;
        }

        hasTriggered = true;

        StartCoroutine(ExecuteIntroFlow());
    }

    private IEnumerator ExecuteIntroFlow()
    {
        if (DialogManager.Instance == null)
        {
            Debug.LogError("DialogManager no está en la escena.");
            yield break;
        }

        if (FadeController.Instance != null)
        {
            yield return new WaitUntil(() => !FadeController.Instance.IsFading);
        }

        DialogManager.Instance.StartDialog(Sala1_IntroDialog, onFinishDialog);

        while (DialogManager.Instance.IsActive)
        {
            yield return null;
        }

        Debug.Log("Diálogo de introducción terminado. El jugador puede moverse y el juego continúa.");
    }
}