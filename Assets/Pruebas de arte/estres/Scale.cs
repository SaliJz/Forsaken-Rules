using System.Collections;
using UnityEngine;

public class Scale : MonoBehaviour
{
    [Header("Ejes que cambiarán")]
    public bool X = true;
    public bool Y = true;
    public bool Z = true;

    [Header("Configuración")]
    public float tamano = 1f;
    public float tiempo = 1f;

    private Coroutine scaleCoroutine;
    void Start()
    {
        gameObject.SetActive(false);
    }
    private void OnEnable()
    {
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(CambiarEscala());
    }

    private void OnDisable()
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }
    }

    private IEnumerator CambiarEscala()
    {
        Vector3 escalaInicial = transform.localScale;

        Vector3 escalaFinal = new Vector3(X ? tamano : escalaInicial.x,Y ? tamano : escalaInicial.y,Z ? tamano : escalaInicial.z);

        if (tiempo <= 0f)
        {
            transform.localScale = escalaFinal;
            yield break;
        }

        float contador = 0f;

        while (contador < tiempo)
        {
            contador += Time.deltaTime;
            float t = contador / tiempo;

            transform.localScale = Vector3.Lerp(escalaInicial, escalaFinal, t);

            yield return null;
        }

        transform.localScale = escalaFinal;
    }
}
