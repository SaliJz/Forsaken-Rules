using UnityEngine;

public class Nodo : MonoBehaviour
{
    [Header("Configuración de Nodo")]
    [SerializeField] private bool useYAxis = false;

    public Transform NodeTransform => transform;
    public bool UseYAxis => useYAxis;
}