using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnerBurbujasChibi : MonoBehaviour
{
    [Header("Activación")]
    public bool Empezar;

    [Header("Detección de burbujas")]
    public float distanciaDeteccion = 5f;

    [Header("Burbujas dentro de distancia")]
    public List<GameObject> burbujasCercanas = new List<GameObject>();

    [Header("Prefab a instanciar")]
    public GameObject burbujasChibi;

    [Header("Control de instancia")]
    public float velocidadInstancia = 0.5f;
    public int cantidadMaximaInstanciar = 10;

    private int cantidadInstanciada;
    private int indiceBurbuja;
    private bool estabaEmpezando;
    private Coroutine rutinaInstancia;

    private void Update()
    {
        if (Empezar && !estabaEmpezando)
        {
            IniciarSpawner();
        }

        if (!Empezar && estabaEmpezando)
        {
            DetenerSpawner();
        }

        estabaEmpezando = Empezar;
    }

    private void IniciarSpawner()
    {
        cantidadInstanciada = 0;
        indiceBurbuja = 0;

        BuscarBurbujasDentroDeDistancia();

        if (rutinaInstancia != null)
        {
            StopCoroutine(rutinaInstancia);
        }

        rutinaInstancia = StartCoroutine(InstanciarBurbujas());
    }

    private void DetenerSpawner()
    {
        if (rutinaInstancia != null)
        {
            StopCoroutine(rutinaInstancia);
            rutinaInstancia = null;
        }

        cantidadInstanciada = 0;
        indiceBurbuja = 0;
        burbujasCercanas.Clear();
    }

    private IEnumerator InstanciarBurbujas()
    {
        while (Empezar && cantidadInstanciada < cantidadMaximaInstanciar)
        {
            if (burbujasChibi == null)
            {
                Debug.LogWarning("No pusiste el prefab burbujasChibi.");
                yield break;
            }
            BuscarBurbujasDentroDeDistancia();

            if (burbujasCercanas.Count == 0)
            {
                Debug.LogWarning("No hay burbujas dentro de la distancia de detección.");
                yield return new WaitForSeconds(Mathf.Max(0.01f, velocidadInstancia));
                continue;
            }

            GameObject burbujaOrigen = ObtenerSiguienteBurbuja();

            if (burbujaOrigen != null)
            {
                Instantiate(
                    burbujasChibi,
                    burbujaOrigen.transform.position,
                    burbujasChibi.transform.rotation
                );

                cantidadInstanciada++;
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, velocidadInstancia));
        }

        rutinaInstancia = null;
    }

    private void BuscarBurbujasDentroDeDistancia()
    {
        burbujasCercanas.Clear();

        GameObject[] todasLasBurbujas = GameObject.FindGameObjectsWithTag("Burbuja");

        float distanciaMaximaSqr = distanciaDeteccion * distanciaDeteccion;

        foreach (GameObject burbuja in todasLasBurbujas)
        {
            if (burbuja == null)
                continue;

            float distanciaSqr = Vector3.SqrMagnitude(burbuja.transform.position - transform.position);

            if (distanciaSqr <= distanciaMaximaSqr)
            {
                burbujasCercanas.Add(burbuja);
            }
        }

        burbujasCercanas.Sort((a, b) =>
        {
            float distanciaA = Vector3.SqrMagnitude(a.transform.position - transform.position);
            float distanciaB = Vector3.SqrMagnitude(b.transform.position - transform.position);

            return distanciaA.CompareTo(distanciaB);
        });
    }

    private GameObject ObtenerSiguienteBurbuja()
    {
        if (burbujasCercanas.Count == 0)
            return null;

        int intentos = 0;

        while (intentos < burbujasCercanas.Count)
        {
            if (indiceBurbuja >= burbujasCercanas.Count)
                indiceBurbuja = 0;

            GameObject burbuja = burbujasCercanas[indiceBurbuja];
            indiceBurbuja++;

            if (burbuja != null)
                return burbuja;

            intentos++;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, distanciaDeteccion);
    }
}