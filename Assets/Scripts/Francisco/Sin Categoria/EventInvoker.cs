using UnityEngine;
using UnityEngine.Events;

public class EventInvoker : MonoBehaviour
{
    [Header("Event")]
    public UnityEvent OnMyMethodCalled = new UnityEvent();

    public void InvokeEvent()
    {
        OnMyMethodCalled?.Invoke();
    }
}
