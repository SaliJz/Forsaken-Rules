using System.Collections;
using UnityEngine;

public class BlendShapeCrecerAlActivarse : MonoBehaviour
{
    [Header("Skinned Mesh")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;

    [Header("Blend Shape")]
    [SerializeField] private string nombreBlendShape;

    [Header("Valores")]
    [SerializeField] private float valorMinimo = 0f;
    [SerializeField] private float valorMaximo = 100f;

    [Header("Tiempo")]
    [SerializeField, Min(0.01f)] private float tiempoEnSegundos = 1f;

    [Header("Loop")]
    [SerializeField] private bool loop = false;

    private int blendShapeIndex = -1;
    private Coroutine rutina;

    private void Reset()
    {
        skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    private void Awake()
    {
        BuscarBlendShape();
    }

    private void OnEnable()
    {
        BuscarBlendShape();

        if (blendShapeIndex == -1) return;

        if (rutina != null)
            StopCoroutine(rutina);

        rutina = StartCoroutine(AnimarBlendShape());
    }

    private void OnDisable()
    {
        if (rutina != null)
        {
            StopCoroutine(rutina);
            rutina = null;
        }
    }

    private void BuscarBlendShape()
    {
        if (skinnedMeshRenderer == null)
            skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        if (skinnedMeshRenderer == null)
        {
            Debug.LogError("No se encontró SkinnedMeshRenderer.", this);
            return;
        }

        Mesh mesh = skinnedMeshRenderer.sharedMesh;

        if (mesh == null)
        {
            Debug.LogError("El SkinnedMeshRenderer no tiene mesh.", this);
            return;
        }

        blendShapeIndex = mesh.GetBlendShapeIndex(nombreBlendShape);

        if (blendShapeIndex == -1)
        {
            Debug.LogError($"No se encontró el blend shape: {nombreBlendShape}", this);

            for (int i = 0; i < mesh.blendShapeCount; i++)
                Debug.Log($"BlendShape disponible {i}: {mesh.GetBlendShapeName(i)}", this);
        }
    }

    private IEnumerator AnimarBlendShape()
    {
        skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, valorMinimo);

        if (!loop)
        {
            yield return AnimarDeA(valorMinimo, valorMaximo);
            skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, valorMaximo);
        }
        else
        {
            while (true)
            {
                yield return AnimarDeA(valorMinimo, valorMaximo);
                yield return AnimarDeA(valorMaximo, valorMinimo);
            }
        }

        rutina = null;
    }

    private IEnumerator AnimarDeA(float desde, float hasta)
    {
        float tiempoActual = 0f;

        while (tiempoActual < tiempoEnSegundos)
        {
            tiempoActual += Time.deltaTime;

            float t = Mathf.Clamp01(tiempoActual / tiempoEnSegundos);
            float valor = Mathf.Lerp(desde, hasta, t);

            skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, valor);

            yield return null;
        }

        skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, hasta);
    }
}
