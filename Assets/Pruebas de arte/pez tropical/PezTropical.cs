using UnityEngine;

public class PezTropical : MonoBehaviour
{
    private const float TwoPi = 6.28318530718f;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    [Header("Movimiento")]
    [SerializeField, Min(0.01f)] private float distanciaRecorrida = 10f;
    [SerializeField, Min(0.01f)] private float tiempoDeViaje = 5f;

    [Header("Zigzag")]
    [SerializeField, Range(0f, 80f)] private float rotacionYMaxima = 25f;
    [SerializeField, Range(0f, 1f)] private float velocidadOscilacion = 2f;

    [Header("Color tropical")]
    [SerializeField] private bool aplicarColorAleatorio = true;
    [SerializeField, Range(0f, 1f)] private float saturacionMinima = 0.65f;
    [SerializeField, Range(0f, 1f)] private float saturacionMaxima = 1f;
    [SerializeField, Range(0f, 1f)] private float brilloMinimo = 0.75f;
    [SerializeField, Range(0f, 1f)] private float brilloMaximo = 1f;

    [Header("Corrección opcional del modelo")]
    [Tooltip("Usa esto solo si el modelo visual del pez es un hijo y no mira hacia +Z.")]
    [SerializeField] private Transform visualDelPez;

    [Tooltip("Corrección local del modelo visual. Ejemplo: si mira al revés, prueba 0, 180, 0.")]
    [SerializeField] private Vector3 correccionRotacionModelo;

    [Header("Gizmos")]
    [SerializeField] private bool mostrarGizmos = true;
    [SerializeField] private Color colorGizmo = Color.yellow;

    private Vector3 posicionInicial;
    private Vector3 direccionBase;
    private Vector3 direccionArriba;
    private Quaternion rotacionBase;

    private float tiempoActual;
    private float avanceEnZ;
    private bool configurado;

    private MaterialPropertyBlock propertyBlock;

    private void Start()
    {
        if (configurado) return;

        Configurar(
            transform.forward,
            transform.up,
            distanciaRecorrida,
            tiempoDeViaje,
            rotacionYMaxima,
            velocidadOscilacion
        );
    }

    public void Configurar(
        Vector3 nuevaDireccion,
        Vector3 nuevaDireccionArriba,
        float nuevaDistancia,
        float nuevoTiempo,
        float nuevaRotacionYMaxima,
        float nuevaVelocidadOscilacion
    )
    {
        if (nuevaDireccion.sqrMagnitude <= 0.0001f)
            nuevaDireccion = Vector3.forward;

        if (nuevaDireccionArriba.sqrMagnitude <= 0.0001f)
            nuevaDireccionArriba = Vector3.up;

        direccionBase = nuevaDireccion.normalized;
        direccionArriba = nuevaDireccionArriba.normalized;

        if (Mathf.Abs(Vector3.Dot(direccionBase, direccionArriba)) > 0.98f)
            direccionArriba = Vector3.up;

        distanciaRecorrida = Mathf.Max(0.01f, nuevaDistancia);
        tiempoDeViaje = Mathf.Max(0.01f, nuevoTiempo);
        rotacionYMaxima = Mathf.Max(0f, nuevaRotacionYMaxima);
        velocidadOscilacion = Mathf.Max(0f, nuevaVelocidadOscilacion);

        posicionInicial = transform.position;
        tiempoActual = 0f;
        avanceEnZ = 0f;

        rotacionBase = Quaternion.LookRotation(direccionBase, direccionArriba);
        transform.rotation = rotacionBase;

        AplicarCorreccionVisual();

        if (aplicarColorAleatorio)
            AplicarColorTropicalAleatorio();

        configurado = true;
    }

    private void Update()
    {
        if (!configurado) return;

        float tiempoRestante = tiempoDeViaje - tiempoActual;
        float delta = Mathf.Min(Time.deltaTime, tiempoRestante);

        tiempoActual += delta;

        float velocidadZ = distanciaRecorrida / tiempoDeViaje;
        float avanceEsteFrame = velocidadZ * delta;

        float fase = tiempoActual * velocidadOscilacion * TwoPi;
        float anguloY = Mathf.Sin(fase) * rotacionYMaxima;

        Quaternion rotacionZigzag = rotacionBase * Quaternion.Euler(0f, anguloY, 0f);
        Vector3 direccionActual = rotacionZigzag * Vector3.forward;

        float factorZ = Vector3.Dot(direccionActual.normalized, direccionBase);
        factorZ = Mathf.Max(0.05f, factorZ);

        Vector3 desplazamiento = direccionActual.normalized * (avanceEsteFrame / factorZ);

        transform.position += desplazamiento;
        transform.rotation = rotacionZigzag;

        AplicarCorreccionVisual();

        avanceEnZ += avanceEsteFrame;

        if (avanceEnZ >= distanciaRecorrida || tiempoActual >= tiempoDeViaje)
            Destroy(gameObject);
    }

    private void AplicarColorTropicalAleatorio()
    {
        Color color = Random.ColorHSV(
            0f,
            1f,
            saturacionMinima,
            saturacionMaxima,
            brilloMinimo,
            brilloMaximo,
            1f,
            1f
        );

        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        foreach (Renderer rend in renderers)
        {
            propertyBlock.Clear();

            propertyBlock.SetColor(BaseColorID, color);
            propertyBlock.SetColor(ColorID, color);

            rend.SetPropertyBlock(propertyBlock);
        }
    }

    private void AplicarCorreccionVisual()
    {
        if (visualDelPez == null) return;
        if (visualDelPez == transform) return;

        visualDelPez.localRotation = Quaternion.Euler(correccionRotacionModelo);
    }

    private void OnValidate()
    {
        distanciaRecorrida = Mathf.Max(0.01f, distanciaRecorrida);
        tiempoDeViaje = Mathf.Max(0.01f, tiempoDeViaje);
        rotacionYMaxima = Mathf.Max(0f, rotacionYMaxima);
        velocidadOscilacion = Mathf.Max(0f, velocidadOscilacion);

        saturacionMinima = Mathf.Clamp01(saturacionMinima);
        saturacionMaxima = Mathf.Clamp01(saturacionMaxima);
        brilloMinimo = Mathf.Clamp01(brilloMinimo);
        brilloMaximo = Mathf.Clamp01(brilloMaximo);

        if (saturacionMaxima < saturacionMinima)
            saturacionMaxima = saturacionMinima;

        if (brilloMaximo < brilloMinimo)
            brilloMaximo = brilloMinimo;
    }

    private void OnDrawGizmosSelected()
    {
        if (!mostrarGizmos) return;

        Vector3 inicio = configurado ? posicionInicial : transform.position;
        Vector3 direccion = configurado ? direccionBase : transform.forward;
        Vector3 fin = inicio + direccion.normalized * distanciaRecorrida;

        Gizmos.color = colorGizmo;
        Gizmos.DrawLine(inicio, fin);
        Gizmos.DrawWireSphere(fin, 0.2f);
    }
}
