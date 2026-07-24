using UnityEngine;

public class AnimEventRelay : MonoBehaviour
{
    private IAnimEventHandler animEventHandler;

    private void Awake()
    {
        animEventHandler = GetComponentInParent<IAnimEventHandler>();

        if (animEventHandler == null) Debug.LogWarning("[AnimEventRelay] No se encontró ningún IAnimEventHandler en los padres.", this);
    }

    public void HandleAnimEvents(string eventName)
    {
        animEventHandler?.HandleAnimEvents(eventName);
    }
}