using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

public class ActiveTargetTracker : MonoBehaviour
{
    public UnityEvent OnAllTargetsInactive;

    [SerializeField] private List<GameObject> targetsToTrack = new List<GameObject>();

    private bool isTracking = false;

    private void Start()
    {
        StartTracking();
    }

    public void StartTracking()
    {
        if (targetsToTrack.Count > 0)
        {
            isTracking = true;
        }
        else
        {
            CheckForEmptyList();
        }
    }

    public void StopTracking()
    {
        isTracking = false;
    }

    public void AddTarget(GameObject target)
    {
        if (target != null && !targetsToTrack.Contains(target))
        {
            targetsToTrack.Add(target);
            if (!isTracking)
            {
                StartTracking();
            }
        }
    }

    public void RemoveTarget(GameObject target)
    {
        if (targetsToTrack.Remove(target))
        {
            CheckForEmptyList();
        }
    }

    private void Update()
    {
        if (!isTracking)
        {
            return;
        }

        bool targetWasRemoved = false;

        for (int i = targetsToTrack.Count - 1; i >= 0; i--)
        {
            GameObject target = targetsToTrack[i];

            if (target == null || !target.activeInHierarchy)
            {
                targetsToTrack.RemoveAt(i);
                targetWasRemoved = true;
            }
        }

        if (targetWasRemoved)
        {
            CheckForEmptyList();
        }
    }

    private void CheckForEmptyList()
    {
        if (targetsToTrack.Count == 0)
        {
            Debug.Log("[TargetTracker] Todos los objetivos han sido eliminados o desactivados.");

            OnAllTargetsInactive?.Invoke();

            StopTracking();
        }
    }

    private void OnDisable()
    {
        StopTracking();
    }
}