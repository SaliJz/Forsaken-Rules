using UnityEngine;
using UnityEngine.Events;

public class ShieldHitManager : MonoBehaviour
{
    public static ShieldHitManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int hitsToTrigger = 5;
    [SerializeField] private UnityEvent onShieldHitThresholdReached;

    private bool isComplete;
    private int currentHitCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void RegisterShieldHit()
    {
        if (isComplete) return;

        currentHitCount++;

        if (currentHitCount >= hitsToTrigger)
        {
            ExecuteEvent();
            currentHitCount = 5;
            isComplete = true;
        }
    }

    private void ExecuteEvent()
    {
        onShieldHitThresholdReached?.Invoke();
    }
}