using System.Collections;
using UnityEngine;

public class Destroy : MonoBehaviour
{
    [Header("Activación")]
    public bool Activar;

    [Header("Tiempo antes de destruir")]
    public float tiempoParaDestruir = 1f;

    private bool yaActivado;
    private Coroutine rutina;

    private void Update()
    {
        if (Activar && !yaActivado)
        {
            yaActivado = true;

            if (rutina != null)
                StopCoroutine(rutina);

            rutina = StartCoroutine(DestruirDespuesDeTiempo());
        }
    }

    private IEnumerator DestruirDespuesDeTiempo()
    {
        yield return new WaitForSeconds(tiempoParaDestruir);
        Destroy(gameObject);
    }

    public void Destruir()
    {
        Destroy(gameObject);
    }
}
