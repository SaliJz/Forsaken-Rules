using System.Collections;
using UnityEngine;

public class FlatOverlayDither : MonoBehaviour
{
    [Header("Activación")]
    public bool Activar;

    [Header("Renderer")]
    [SerializeField] private Renderer rendererObjetivo;

    [Header("Transición")]
    [SerializeField, Min(0.01f)] private float tiempo = 1f;

    [Header("Valores objetivo")]
    [SerializeField] private Color colorObjetivo = Color.white;
    [SerializeField] private float ditherScale = 3f;

    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int DissolveAmountID = Shader.PropertyToID("_DissolveAmount");
    private static readonly int DitherScaleID = Shader.PropertyToID("_DitherScale");

    private Material materialInstancia;
    private Coroutine rutina;
    private bool activarAnterior;

    private void Reset()
    {
        rendererObjetivo = GetComponent<Renderer>();
    }

    private void Awake()
    {
        if (rendererObjetivo == null)
            rendererObjetivo = GetComponent<Renderer>();

        if (rendererObjetivo != null)
            materialInstancia = rendererObjetivo.material;
    }

    private void Update()
    {
        if (Activar && !activarAnterior)
            Ejecutar();

        activarAnterior = Activar;
    }

    public void Ejecutar()
    {
        if (materialInstancia == null) return;

        if (rutina != null)
            StopCoroutine(rutina);

        rutina = StartCoroutine(RutinaActivacion());
    }

    private IEnumerator RutinaActivacion()
    {
        float dissolveInicial = materialInstancia.GetFloat(DissolveAmountID);
        Color colorInicial = materialInstancia.GetColor(ColorID);

        materialInstancia.SetFloat(DitherScaleID, ditherScale);

        float tiempoActual = 0f;

        while (tiempoActual < tiempo)
        {
            tiempoActual += Time.deltaTime;

            float t = Mathf.Clamp01(tiempoActual / tiempo);

            float nuevoDissolve = Mathf.Lerp(dissolveInicial, 0f, t);
            Color nuevoColor = Color.Lerp(colorInicial, colorObjetivo, t);

            materialInstancia.SetFloat(DissolveAmountID, nuevoDissolve);
            materialInstancia.SetColor(ColorID, nuevoColor);

            yield return null;
        }

        materialInstancia.SetFloat(DissolveAmountID, 0f);
        materialInstancia.SetColor(ColorID, colorObjetivo);

        Activar = false;
        activarAnterior = false;
        rutina = null;
    }
}