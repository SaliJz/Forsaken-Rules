using UnityEngine;
using System.Collections;

public class TorreMovimiento : MonoBehaviour
{
    public float altura = 2f;
    public float duracion = 0.3f;
    public float tiempoDeVida = 2f;

    private Vector3 posicionInicial;
    private Vector3 posicionFinal;
    private Coroutine corrutinaActual;

    public void Initialize(Transform origin)
    {
        posicionInicial = new Vector3(
            origin.position.x,
            origin.position.y,
            origin.position.z
        );
        posicionFinal = new Vector3(
            origin.position.x,
            origin.position.y + altura,
            origin.position.z
        );

        transform.position = posicionInicial;

        if (corrutinaActual != null) StopCoroutine(corrutinaActual);
        corrutinaActual = StartCoroutine(MovimientoCompleto());
    }

    IEnumerator MovimientoCompleto()
    {
        float tiempo = 0f;
        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            float t = Mathf.Clamp01(tiempo / duracion);
            transform.position = Vector3.Lerp(posicionInicial, posicionFinal, t);
            yield return null;
        }
        transform.position = posicionFinal;

        yield return new WaitForSeconds(tiempoDeVida);

        tiempo = 0f;
        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            float t = Mathf.Clamp01(tiempo / duracion);
            transform.position = Vector3.Lerp(posicionFinal, posicionInicial, t);
            yield return null;
        }
        transform.position = posicionInicial;
    }
}