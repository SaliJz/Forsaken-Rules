using UnityEngine;

public class RotateLoop : MonoBehaviour
{
    [Header("Main")]
    public bool LOOP;

    [Header("Rotation Axis")]
    public bool X;
    public float xForce = 0f;

    public bool Y;
    public float yForce = 0f;

    public bool Z;
    public float zForce = 0f;

    private void Update()
    {
        if (!LOOP)
        {
            return;
        }

        float rotationX = X ? xForce : 0f;
        float rotationY = Y ? yForce : 0f;
        float rotationZ = Z ? zForce : 0f;

        transform.Rotate( rotationX * Time.deltaTime, rotationY * Time.deltaTime, rotationZ * Time.deltaTime, Space.Self);
    }
}