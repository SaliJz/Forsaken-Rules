using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class EmisorFuegoTexto : MonoBehaviour
{
    private TextMeshProUGUI _textMesh;
    public ParticleSystem particulas;

    [Header("Configuración")]
    public float particulasPorUnidad = 300f;

    void Awake()
    {
        _textMesh = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        Invoke("ConfigurarYDispersar", 0.05f);
    }

    public void ConfigurarYDispersar()
    {
        if (_textMesh == null || particulas == null) return;

        _textMesh.ForceMeshUpdate();
        
        float anchoReal = _textMesh.renderedWidth;
        float altoReal = _textMesh.renderedHeight;

        particulas.Stop();
        
        var shape = particulas.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        
        shape.scale = new Vector3(anchoReal, altoReal, 1f);
        shape.position = Vector3.zero;

        var emission = particulas.emission;
        emission.rateOverTime = anchoReal * particulasPorUnidad;

        ParticleSystemRenderer psr = particulas.GetComponent<ParticleSystemRenderer>();
        psr.sortingFudge = 1000f;

        particulas.Play();
    }
}