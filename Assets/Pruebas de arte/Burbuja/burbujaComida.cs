using UnityEngine;

public class burbujaComida : MonoBehaviour
{
    [Header("Objetivo")]
    public string nombreCabeza = "Cabeza_JOINT";

    [Header("Movimiento")]
    public float velocidadMovimiento = 3f;

    [Header("Destrucción por cercanía")]
    public float distanciaDestruir = 0.2f;

    private Transform cabezaJoint;

    private void Start()
    {
        BuscarCabezaMasCercana();
    }

    private void Update()
    {
        if (cabezaJoint == null)
        {
            BuscarCabezaMasCercana();

            if (cabezaJoint == null)
                return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            cabezaJoint.position,
            velocidadMovimiento * Time.deltaTime
        );

        float distancia = Vector3.Distance(transform.position, cabezaJoint.position);

        if (distancia <= distanciaDestruir)
        {
            Destroy(gameObject);
        }
    }

    private void BuscarCabezaMasCercana()
    {
        Transform[] todosLosTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);

        float distanciaMenor = Mathf.Infinity;
        Transform cabezaMasCercana = null;

        foreach (Transform t in todosLosTransforms)
        {
            if (t.name != nombreCabeza)
                continue;

            float distancia = Vector3.SqrMagnitude(t.position - transform.position);

            if (distancia < distanciaMenor)
            {
                distanciaMenor = distancia;
                cabezaMasCercana = t;
            }
        }

        cabezaJoint = cabezaMasCercana;
    }
}