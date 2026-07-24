// Archivo: MarcadorTrail.cs
using UnityEngine;

public class MarcadorTrail : MonoBehaviour
{
    public float vida = 2f;
    void Start() { Destroy(gameObject, vida); }
}
