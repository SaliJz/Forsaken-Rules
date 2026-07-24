using System;
using UnityEngine;

public class ShopItemVisualRotation : MonoBehaviour
{
    Transform t;
    [SerializeField] float rotationSpeed;
    [Serializable]
    public struct ActiveAxis 
    {
        public bool x;
        public bool y;
        public bool z;
    }

    [SerializeField] ActiveAxis activeAxis;

    Vector3 directionsActive;

    void Start()
    {
        t = gameObject.transform;

        UpdateDirections();
    }

    void Update()
    {
        UpdateDirections();
        t.Rotate(directionsActive, rotationSpeed * Time.deltaTime);
    }

    void UpdateDirections()
    {
        float Xvalue = activeAxis.x? 1:0;
        float Yvalue = activeAxis.y? 1:0;
        float Zvalue = activeAxis.z? 1:0;

        directionsActive = new Vector3(Xvalue, Yvalue, Zvalue);
    }
}
