using UnityEngine;

public class LookAt : MonoBehaviour
{
    [Header("Target")]
    public Transform Target;

    [Header("General")]
    public bool lookAtEnabled = true;
    public bool lookYaxis = true;
    float rotationSpeed = 6f;
    

    [Header("Stepped Mode")]
    [SerializeField] bool useStepped;
    [SerializeField] float stepRate = 0.12f;
    public float multiplayer;
    float stepTimer;

    void Update()
    {
        if(!lookAtEnabled || Target == null) return;

        if (useStepped)
        {
            stepTimer -= Time.deltaTime;
            if(stepTimer > 0) return;

            stepTimer = stepRate;
            ApplyRotation(true);
        }
        else
        {
            ApplyRotation(false);
        }
    }

    void ApplyRotation(bool instant)
    {
        Vector3 direction = Target.position - transform.position;

        if(lookYaxis) direction.y = 0;

        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);

        if (instant)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * multiplayer * Time.deltaTime);
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
