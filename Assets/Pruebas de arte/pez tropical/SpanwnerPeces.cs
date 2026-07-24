using System.Collections;
using UnityEngine;

public class SpanwnerPeces : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject prefabPez;
    [SerializeField] private bool spawnearAlActivarse = true;
    [SerializeField] private bool spawnearUnoAlInicio = true;
    [SerializeField] private bool agregarPezTropicalSiFalta = true;

    [Header("Tiempo de spawn aleatorio")]
    [SerializeField, Min(0.01f)] private float tiempoMinSpawn = 1f;
    [SerializeField, Min(0.01f)] private float tiempoMaxSpawn = 3f;

    [Header("Movimiento del pez")]
    [SerializeField, Min(0.01f)] private float distanciaRecorrida = 10f;
    [SerializeField, Min(0.01f)] private float tiempoDeViaje = 5f;

    [Header("Zigzag")]
    [SerializeField, Range(0f, 80f)] private float rotacionYMaxima = 25f;
    [SerializeField, Range(0f, 1f)] private float velocidadOscilacion = 2f;

    [Header("Gizmos")]
    [SerializeField] private bool mostrarGizmos = true;
    [SerializeField] private Color colorGizmo = Color.cyan;
    [SerializeField, Min(0.01f)] private float radioGizmo = 0.2f;

    private Coroutine rutinaSpawn;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (!spawnearAlActivarse) return;

        if (spawnearUnoAlInicio)
            SpawnearPez();

        rutinaSpawn = StartCoroutine(RutinaSpawn());
    }

    private void OnDisable()
    {
        if (rutinaSpawn != null)
        {
            StopCoroutine(rutinaSpawn);
            rutinaSpawn = null;
        }
    }

    private IEnumerator RutinaSpawn()
    {
        while (true)
        {
            float espera = Random.Range(tiempoMinSpawn, tiempoMaxSpawn);
            yield return new WaitForSeconds(espera);

            SpawnearPez();
        }
    }

    [ContextMenu("Spawnear pez ahora")]
    public void SpawnearPez()
    {
        if (prefabPez == null)
        {
            Debug.LogWarning("SpanwnerPeces no tiene un prefab de pez asignado.", this);
            return;
        }

        Vector3 direccionZSpawner = transform.forward.normalized;

        Quaternion rotacionInicial = Quaternion.LookRotation(
            direccionZSpawner,
            transform.up
        );

        GameObject pezInstanciado = Instantiate(
            prefabPez,
            transform.position,
            rotacionInicial
        );

        PezTropical pez = pezInstanciado.GetComponent<PezTropical>();

        if (pez == null && agregarPezTropicalSiFalta)
            pez = pezInstanciado.AddComponent<PezTropical>();

        if (pez == null)
        {
            Debug.LogWarning(
                "El prefab no tiene PezTropical y no se permitió agregarlo automáticamente.",
                pezInstanciado
            );
            return;
        }

        pez.Configurar(
            direccionZSpawner,
            transform.up,
            distanciaRecorrida,
            tiempoDeViaje,
            rotacionYMaxima,
            velocidadOscilacion
        );
    }

    private void OnValidate()
    {
        tiempoMinSpawn = Mathf.Max(0.01f, tiempoMinSpawn);
        tiempoMaxSpawn = Mathf.Max(tiempoMinSpawn, tiempoMaxSpawn);
        distanciaRecorrida = Mathf.Max(0.01f, distanciaRecorrida);
        tiempoDeViaje = Mathf.Max(0.01f, tiempoDeViaje);
        velocidadOscilacion = Mathf.Max(0f, velocidadOscilacion);
        radioGizmo = Mathf.Max(0.01f, radioGizmo);
    }

    private void OnDrawGizmos()
    {
        if (!mostrarGizmos) return;

        Vector3 inicio = transform.position;
        Vector3 fin = inicio + transform.forward.normalized * distanciaRecorrida;

        Gizmos.color = colorGizmo;
        Gizmos.DrawLine(inicio, fin);
        Gizmos.DrawSphere(inicio, radioGizmo);
        Gizmos.DrawWireSphere(fin, radioGizmo);
    }
}
