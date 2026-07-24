// SwarmAgent.cs
// Script ligero para el comportamiento visual de cada mosca.
// En el prefab de la mosca asigna este script y un mesh/particle. Opcional: Rigidbody para físicas visuales.
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class SwarmAgent : MonoBehaviour
{
    private Vector3 centro;
    private float radioBase = 1f;
    private float velocidad = 2f;
    private float fase = 0f;
    private float amplitudVertical = 0.12f;
    private float jitterVel = 0.8f;
    private float jitterRad = 0.25f;

    private bool dispersando = false;
    private Vector3 velocidadDispersa;

    public void Initialize(Vector3 centroMundo, float radio, float velocidadVal, float faseOffset)
    {
        centro = centroMundo;
        radioBase = Mathf.Max(0.1f, radio * Random.Range(0.4f, 1.0f));
        velocidad = Mathf.Max(0.1f, velocidadVal);
        fase = faseOffset;
        amplitudVertical = Random.Range(0.05f, 0.18f);
        jitterRad = Random.Range(0.05f, 0.3f);
        jitterVel = Random.Range(0.4f, 1.2f);
    }

    private void Update()
    {
        if (dispersando)
        {
            transform.position += velocidadDispersa * Time.deltaTime;
            transform.Rotate(Vector3.up, 360f * Time.deltaTime * 0.6f, Space.Self);
            return;
        }

        fase += Time.deltaTime * velocidad;
        float ang = fase;
        float jitter = Mathf.PerlinNoise(Time.time * jitterVel, fase) * jitterRad;
        float r = radioBase + jitter;

        Vector3 objetivo = centro + new Vector3(Mathf.Cos(ang) * r, amplitudVertical * Mathf.Sin(Time.time * (0.9f + jitter)), Mathf.Sin(ang) * r);
        transform.position = Vector3.Lerp(transform.position, objetivo, Time.deltaTime * Mathf.Clamp01(velocidad * 0.7f));

        Vector3 dir = centro - transform.position;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 4f);
    }

    public void UpdateCenter(Vector3 nuevoCentro)
    {
        centro = nuevoCentro;
    }

    public void StartDisperse()
    {
        dispersando = true;
        Vector3 radial = (transform.position - centro).normalized;
        if (radial.sqrMagnitude < 0.01f) radial = Random.onUnitSphere;
        radial.y = Mathf.Abs(radial.y) * 0.5f + 0.1f;
        velocidadDispersa = radial * Random.Range(1.5f, 3.0f);
        transform.rotation = Quaternion.LookRotation(velocidadDispersa);
    }
}
