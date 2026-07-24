using UnityEngine;

public class melting : MonoBehaviour
{
    [SerializeField] private GameObject gota;
    [SerializeField] private int Velocidad;
    [SerializeField] private int Tiempo;

    void Start()
    {
        gota.SetActive(false);
    }
    public void ActivarGota()
    {
        gota.SetActive(true);
        gota.transform.parent = null;
        
        Rigidbody2D rb = gota.GetComponent<Rigidbody2D>();
        rb.linearVelocity = new Vector2(Velocidad * 0.23f ,Velocidad);
        Destroy(gota,Tiempo);
    }
    
}
