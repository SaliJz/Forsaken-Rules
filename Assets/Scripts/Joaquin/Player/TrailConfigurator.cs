using UnityEngine;

public class TrailConfigurator : MonoBehaviour
{
    [Header("Trail Settings")]
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private Material additiveMaterial;
    [SerializeField] private float trailTime = 0.25f;
    [SerializeField] private float startWidth = 0.6f;
    [SerializeField] private float endWidth = 0f;
    [SerializeField] private float minVertexDistance = 0.05f;

    private void Reset()
    {
        trail = GetComponent<TrailRenderer>();
    }

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        if (trail == null) Debug.LogError("Componente de TrailRenderer no encontrado en " + gameObject.name);
        ConfigureTrail();
    }

    /// <summary>
    /// Configura las propiedades visibles del TrailRenderer.
    /// </summary>
    public void ConfigureTrail()
    {
        // Tiempo y densidad
        trail.time = trailTime;
        trail.minVertexDistance = minVertexDistance;
        trail.emitting = false; // por defecto apagado

        // Alineación: View (importante en isométrico para que siempre "mire" a cámara)
        // En algunas versiones de Unity la enum puede llamarse LineAlignment.View
        trail.alignment = LineAlignment.View;

        // Curva de anchura (empieza ancha - se afila)
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, startWidth);
        widthCurve.AddKey(0.4f, startWidth * 0.6f); // estrecha algo en medio
        widthCurve.AddKey(1f, endWidth);
        trail.widthCurve = widthCurve;

        // --- Gradient (Color over lifetime) ---
        Gradient gradient = new Gradient();
        // Estas son las líneas concretas que pedías para el Color over Lifetime:
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0.0f), // naranja inicial
                new GradientColorKey(new Color(1f, 0.05f, 0.6f), 0.45f), // magenta intermedio
                new GradientColorKey(new Color(0.05f, 0.05f, 0.06f), 1.0f) // casi negro al final
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0.0f), // totalmente opaco al inicio
                new GradientAlphaKey(0f, 1.0f)  // totalmente transparente al final
            }
        );
        trail.colorGradient = gradient;

        // Material (aditivo si se puede)
        if (additiveMaterial != null)
        {
            trail.material = additiveMaterial;
        }
        else
        {
            Shader additive = Shader.Find("Particles/Additive");
            if (additive != null)
            {
                trail.material = new Material(additive);
            }
            else
            {
                // Fallback a algo que casi siempre existe
                Shader s = Shader.Find("Sprites/Default");
                trail.material = new Material(s);
            }
        }

        // Ajustes opcionales útiles:
        trail.numCapVertices = 4;    // redondeo de extremos
        trail.numCornerVertices = 2; // suaviza curvas
    }
}