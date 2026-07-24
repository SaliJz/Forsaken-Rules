using UnityEngine;
using System.Collections;

public class EscudoController : MonoBehaviour
{
    [Header("Activar Escudo")]
    public bool Escudo;

    [Header("Configuración")]
    public float tiempoEscudo = 0.5f;

    [Header("Valores Iniciales")]
    [Range(0, 1)] public float Alpha = 1f;
    [Range(0, 1)] public float AmbientOcclusion = 1f;

    private Coroutine escudoCoroutine;

    private string AlphaShader = "_Alpha";
    private string OclussionShader = "_Ambient_Occlusion";

    private Renderer rend;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();

        SetShaderValues(0,0);
    }

    private void Update()
    {
        if (Escudo)
        {
            Escudo = false;

            if (escudoCoroutine != null)
            {
                StopCoroutine(escudoCoroutine);
            }

            escudoCoroutine = StartCoroutine(EfectoEscudo());
        }
    }

    private IEnumerator EfectoEscudo()
    {
        Alpha = 1f;
        SetShaderValues(Alpha, AmbientOcclusion);

        float tiempo = 0f;

        while (tiempo < tiempoEscudo)
        {
            tiempo += Time.deltaTime;

            float t = Mathf.Clamp01(tiempo / tiempoEscudo);

            SetShaderValues(
                Mathf.Lerp(Alpha, 0f, t),
                Mathf.Lerp(AmbientOcclusion, 0f, t)
            );

            yield return null;
        }

        SetShaderValues(0f, 0f);

        escudoCoroutine = null;
    }

    private void SetShaderValues(float alpha, float ao)
    {
        rend.GetPropertyBlock(block);

        block.SetFloat(AlphaShader, alpha);
        block.SetFloat(OclussionShader, ao);

        rend.SetPropertyBlock(block);
    }
}