using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Activador : MonoBehaviour
{
    [Header("Objetos")]
    [SerializeField] private List<GameObject> objetos = new List<GameObject>();

    [Header("Inicio")]
    [SerializeField] private bool setFalseEnStart;

    [Header("Activación")]
    public bool Activar;

    [Header("Activación en orden")]
    [SerializeField] private bool EnOrden;
    [SerializeField] private float tiempoDemora = 0.25f;

    private bool yaActivado;
    private Destroy dt;

    private void Start()
    {
        dt = GetComponent<Destroy>();

        if (setFalseEnStart)
            SetObjetos(false);
    }

    private void Update()
    {
        if (Activar && !yaActivado)
        {
            yaActivado = true;

            if (EnOrden)
            {
                StartCoroutine(ActivarEnOrden());
            }
            else
            {
                SetObjetos(true);

                if (dt != null)
                    dt.Activar = true;
            }
        }
    }

    private IEnumerator ActivarEnOrden()
    {
        foreach (GameObject obj in objetos)
        {
            if (obj != null)
                obj.SetActive(true);

            yield return new WaitForSeconds(tiempoDemora);
        }

        if (dt != null)
            dt.Activar = true;
    }

    private void SetObjetos(bool estado)
    {
        foreach (GameObject obj in objetos)
        {
            if (obj != null)
                obj.SetActive(estado);
        }
    }

    public void ActivarObjetos()
    {
        Activar = true;
    }
}
