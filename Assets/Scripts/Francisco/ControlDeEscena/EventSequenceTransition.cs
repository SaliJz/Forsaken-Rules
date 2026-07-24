using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class EventSequenceTransition : SequenceTransition
{
    [Header("Configuración del Trigger")]
    [SerializeField] private string playerTag = "Player";

    [Header("Eventos de Finalización")]
    [SerializeField] private UnityEvent onSequenceEnded;

    private bool hasTriggered = false;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        if (other.CompareTag(playerTag))
        {
            hasTriggered = true;
            StartCoroutine(ExecuteSequenceWithEvent(other.transform));
        }
    }

    private IEnumerator ExecuteSequenceWithEvent(Transform playerTransform)
    {
        yield return StartCoroutine(ExecuteSequence(playerTransform));

        if (onSequenceEnded != null)
        {
            onSequenceEnded.Invoke();
        }
    }
}