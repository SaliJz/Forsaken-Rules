using UnityEngine;
using UnityEngine.EventSystems;

public class ControladorFuego : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ParticleSystem fuego;

    void Start()
    {
        var emission = fuego.emission;
        emission.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var emission = fuego.emission;
        emission.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        var emission = fuego.emission;
        emission.enabled = false;
    }
}