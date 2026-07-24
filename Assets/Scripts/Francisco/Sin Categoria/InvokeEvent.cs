using UnityEngine;
using UnityEngine.Events;

public class InvokeEvent : MonoBehaviour
{
    [Header("Event")]
    [SerializeField]private UnityEvent OnMyMethodCalled = new UnityEvent();

    public void InvokeEvents()
    {
        OnMyMethodCalled?.Invoke();
    }
}