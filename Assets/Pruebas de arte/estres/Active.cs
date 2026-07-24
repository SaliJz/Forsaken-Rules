using UnityEngine;

public class Active : MonoBehaviour
{
    public GameObject cosa;

    public void Activar()
    {
        cosa.SetActive(true);
    }
    public void Desactivar()
    {
        cosa.SetActive(false);
    }
}
