using System;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private HealthController healthController;
    public Action OnDeath;

    void Start()
    {
        healthController = GetComponent<HealthController>();
        if (healthController != null)
        {
            healthController.OnDamaged += () => { };
        }
    }

    void OnDestroy()
    {
        OnDeath?.Invoke();
        if (transform.parent != null)
        {
            Destroy(transform.parent.gameObject);
        }
    }

    private void OnDisable()
    {
        if (transform.parent != null)
        {
            Destroy(transform.parent.gameObject);
        }
    }
}