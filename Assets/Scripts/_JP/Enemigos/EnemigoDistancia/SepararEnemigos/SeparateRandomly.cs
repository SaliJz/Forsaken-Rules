using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Utilities/Separate Randomly")]
public class SeparateRandomly : MonoBehaviour
{
    [Header("Rango de separación aleatoria")]
    [Tooltip("Distancia mínima que puede haber entre dos objetos.")]
    public float minDistance = 1f;
    [Tooltip("Distancia máxima que puede haber entre dos objetos.")]
    public float maxDistance = 4f;

    [Header("Ajuste")]
    [Tooltip("Número máximo de iteraciones para intentar separar el objeto (evita bucles infinitos).")]
    public int maxIterations = 10;
    [Tooltip("Si está activo, la separación solo se aplica en XZ (útil para juegos 3D sobre un plano).")]
    public bool lockY = true;

    [Header("Comportamiento")]
    [Tooltip("Si está activo, el script intentará repartir el movimiento entre ambos objetos (si el otro también tiene este componente).")]
    public bool shareMovement = true;

    // Lista global de instancias (se actualiza en tiempo de ejecución)
    private static List<SeparateRandomly> instances = new List<SeparateRandomly>();

    void Awake()
    {
        // Registrar instancia
        if (!instances.Contains(this)) instances.Add(this);
    }

    void OnDestroy()
    {
        // Quitar instancia
        instances.Remove(this);
    }

    IEnumerator Start()
    {
        // Esperamos un frame para que otros objetos con este componente también se registren.
        yield return null;
        // Intentamos resolver la posición al inicio
        ResolveSeparation();
    }

    /// <summary>
    /// Llamar cuando quieras forzar una re-separación (por ejemplo al spawnear dinámicamente).
    /// </summary>
    public void ResolveSeparation()
    {
        // Protecciones
        if (minDistance < 0f) minDistance = 0f;
        if (maxDistance < minDistance) maxDistance = minDistance;

        // Ejecutamos varias iteraciones para converger
        for (int iter = 0; iter < maxIterations; iter++)
        {
            bool anyMoved = false;

            // Recorremos todas las demás instancias
            for (int i = 0; i < instances.Count; i++)
            {
                var other = instances[i];
                if (other == null || other == this) continue;

                Vector3 posA = transform.position;
                Vector3 posB = other.transform.position;

                if (lockY)
                {
                    posA.y = 0f;
                    posB.y = 0f;
                }

                Vector3 dir = posA - posB;
                float dist = dir.magnitude;

                // Queremos una distancia aleatoria entre minDistance y maxDistance para esta pareja.
                // Usamos Random.value para variar "un poquito o mucho".
                float desired = Random.Range(minDistance, maxDistance);

                if (dist < Mathf.Epsilon)
                {
                    // Si están exactamente en el mismo punto, elige una dirección aleatoria
                    dir = Random.onUnitSphere;
                    if (lockY) dir.y = 0f;
                    dir.Normalize();
                    dist = 0f;
                }
                else
                {
                    dir /= dist; // dirección normalizada de other -> this
                }

                if (dist < desired)
                {
                    float delta = desired - dist;

                    if (shareMovement && other != null)
                    {
                        // Si el otro también tiene el componente, movemos ambos la mitad.
                        // Si no queremos mover el otro (por ejemplo está en runtime estático), solo movemos este.
                        transform.position += (Vector3)(dir * (delta * 0.5f));
                        other.transform.position -= (Vector3)(dir * (delta * 0.5f));
                    }
                    else
                    {
                        // Mueve solo este objeto en la dirección away-from-other
                        transform.position += (Vector3)(dir * delta);
                    }

                    // Si lockY está activo, preservamos la coordenada Y original del objeto
                    if (lockY)
                    {
                        Vector3 p = transform.position;
                        p.y = transform.position.y; // mantener Y (se movió solo en XZ por la dirección con y=0)
                        transform.position = p;
                    }

                    anyMoved = true;
                }
            }

            if (!anyMoved) break; // ya no hay solapamientos suficientes
        }
    }
     
    // Opcional: dibuja esferas en el editor para ver rangos
    void OnDrawGizmosSelected()
    {
        // Muestra el rango mínimo y máximo alrededor del objeto
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.2f);
        Gizmos.DrawSphere(transform.position, minDistance);

        Gizmos.color = new Color(0.2f, 0.2f, 0.8f, 0.12f);
        Gizmos.DrawSphere(transform.position, maxDistance);
    }
}
