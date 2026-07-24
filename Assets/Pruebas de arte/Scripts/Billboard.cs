using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] Camera MainCamera;

    private void Awake()
    {
        MainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        transform.LookAt(transform.position + MainCamera.transform.rotation * Vector3.forward, MainCamera.transform.rotation * Vector3.up);
    }
}
