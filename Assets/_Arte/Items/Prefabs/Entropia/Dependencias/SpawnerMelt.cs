using UnityEngine;
using System.Collections;

public class SpawnerMelt : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject prefab;

    [Header("Área")]
    [SerializeField] private float radio;

    [Header("Spawn")]
    [SerializeField] private float instanciasPorSegundo;
    [SerializeField] private float interbalo;

    [Header("Vida")]
    [SerializeField] private bool infinito = true;
    [SerializeField] private float tiempoDeVida;

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (prefab != null && instanciasPorSegundo > 0f)
            {
                int cantidad = Mathf.FloorToInt(instanciasPorSegundo);
                float fraccion = instanciasPorSegundo - cantidad;

                if (Random.value < fraccion)
                    cantidad++;

                for (int i = 0; i < cantidad; i++)
                {
                    SpawnUno();
                }
            }

            yield return new WaitForSeconds(interbalo);
        }
    }

    private void SpawnUno()
    {
        Vector2 punto = Random.insideUnitCircle * radio;

        Vector3 posicion = new Vector3(transform.position.x + punto.x, transform.position.y, transform.position.z + punto.y
        );

        GameObject obj = Instantiate(prefab, posicion, Quaternion.identity);
        obj.transform.rotation = Quaternion.Euler(30f, 45f, 0f);

        if (!infinito)
        {
            Destroy(obj, tiempoDeVida);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        int segmentos = 64;
        Vector3 centro = transform.position;

        Vector3 prev = centro + new Vector3(radio, 0, 0);

        for (int i = 1; i <= segmentos; i++)
        {
            float ang = i * Mathf.PI * 2f / segmentos;

            Vector3 next = centro + new Vector3(
                Mathf.Cos(ang) * radio,
                0,
                Mathf.Sin(ang) * radio
            );

            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
