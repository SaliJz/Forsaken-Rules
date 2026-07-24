using UnityEngine;
using System;

[RequireComponent(typeof(Animator))]
public class AnimationEventBridge : MonoBehaviour
{
    public event Action OnAnimationTriggered;
    public event Action<string> OnAnimationStringTriggered;
    public event Action<int> OnAnimationIntTriggered;

    public void TriggerAnimationEvent()
    {
        OnAnimationTriggered?.Invoke();
    }

    public void TriggerAnimationStringEvent(string eventName)
    {
        OnAnimationStringTriggered?.Invoke(eventName);
    }

    public void TriggerAnimationIntEvent(int value)
    {
        OnAnimationIntTriggered?.Invoke(value);
    }
}