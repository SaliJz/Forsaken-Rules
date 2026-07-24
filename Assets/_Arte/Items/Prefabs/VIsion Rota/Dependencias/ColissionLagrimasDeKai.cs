using UnityEngine;

public class ColissionLagrimasDeKai : MonoBehaviour
{
    [SerializeField] private GameObject prefabImpacto;
    [SerializeField] private LayerMask capasValidas;
    [SerializeField] private Transform camara;
    [Range(0f, 5f)]
    [SerializeField] private float offsetHaciaCamara = 0.2f;
    [Range(0f, 5f)]
    [SerializeField] float separacion = 0.5f;

    void Start()
    {
        GameObject cam = GameObject.Find("Main Camera");

        if (cam != null)
        {
            camara = cam.transform;
        }
        else
        {
            Debug.LogWarning("No se encontró un objeto llamado 'Main Camera'");
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if ((capasValidas.value & (1 << other.gameObject.layer)) != 0)
        {
            Vector3 punto = other.ClosestPoint(transform.position);
            Vector3 dirCam = (camara.position - punto).normalized;
            Vector3 puntoFinal = punto + dirCam * offsetHaciaCamara;
            Vector3 right = camara.right;
            Vector3 up = camara.up;
            Vector3 centro = puntoFinal;
            Vector3 izquierdaArriba = puntoFinal + (-right + up) * separacion;
            Vector3 derechaArriba = puntoFinal + (right + up) * separacion;

            GameObject obj1 = Instantiate(prefabImpacto, puntoFinal, Quaternion.identity);
            GameObject obj2 = Instantiate(prefabImpacto, izquierdaArriba, Quaternion.identity);
            GameObject obj3 = Instantiate(prefabImpacto, derechaArriba, Quaternion.identity);
            obj1.transform.parent = null;
            obj2.transform.parent = null;
            obj3.transform.parent = null;
        }
    }
}
