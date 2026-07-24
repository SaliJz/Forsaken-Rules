using UnityEngine;

public class CortinaColorController : MonoBehaviour
{
    [SerializeField] private Material cortinaMaterial;

    [Range(0f, 1f)]
    [SerializeField] private float intensidad;

    [SerializeField] private Color colorCortina = Color.black;

    private static readonly int IntensidadId = Shader.PropertyToID("_Intensidad");
    private static readonly int ColorCortinaId = Shader.PropertyToID("_ColorCortina");

    private void Update()
    {
        if (cortinaMaterial == null) return;

        cortinaMaterial.SetFloat(IntensidadId, intensidad);
        cortinaMaterial.SetColor(ColorCortinaId, colorCortina);
    }

    public void SetIntensidad(float valor)
    {
        intensidad = Mathf.Clamp01(valor);
    }

    public void SetColor(Color color)
    {
        colorCortina = color;
    }
}
