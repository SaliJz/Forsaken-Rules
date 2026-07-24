using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Audio3D : MonoBehaviour
{
    [Header("Referencia")]
    public Transform jugador;

    [Header("Distancias")]
    public float distanciaInicioFade = 10f;
    public float distanciaMaxima = 25f;

    [Header("Configuración")]
    public AnimationCurve curvaVolumen = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public float velocidadFade = 5f;

    private AudioSource source;
    private float volumenObjetivo;


    void Awake()
    {
        source = GetComponent<AudioSource>();

        if (jugador == null)
            jugador = GameObject.FindGameObjectWithTag("Player")?.transform;
    }


    void Update()
    {
        if (jugador == null) return;


        float distancia = Vector3.Distance(transform.position, jugador.position);


        if (distancia <= distanciaInicioFade)
        {
            volumenObjetivo = 1f;
        }
        // Zona de transición
        else if (distancia < distanciaMaxima)
        {
            float porcentaje = 
                Mathf.InverseLerp(distanciaInicioFade, distanciaMaxima, distancia);

            volumenObjetivo = curvaVolumen.Evaluate(porcentaje);
        }

        else
        {
            volumenObjetivo = 0f;
        }


        // Fade profesional
        source.volume = Mathf.Lerp(
            source.volume,
            volumenObjetivo,
            Time.deltaTime * velocidadFade
        );


        if (source.volume <= 0.01f)
        {
            source.volume = 0f;

            if(source.isPlaying)
                source.Pause();
        }
        else
        {
            if(!source.isPlaying)
                source.UnPause();
        }


        Debug.Log($"Distancia: {distancia:F2} | Volumen: {source.volume:F2}");
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, distanciaInicioFade);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, distanciaMaxima);
    }
}