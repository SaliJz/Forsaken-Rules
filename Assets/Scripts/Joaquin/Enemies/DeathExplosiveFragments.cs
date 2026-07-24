using UnityEngine;

/// <summary>
/// Controla la explosión de fragmentos y su limpieza.
/// </summary>
public class DeathExplosiveFragments : MonoBehaviour
{
    [Header("Configuración de Explosión")]
    [SerializeField] private float explosionForce = 500f;
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private float upwardModifier = 1f; // Para que los trozos salten un poco hacia arriba

    [Header("Limpieza")]
    [SerializeField] private float lifetime = 4f; // Tiempo antes de desaparecer

    private void Start()
    {
        Explode();
        Destroy(gameObject, lifetime);
    }

    private void Explode()
    {
        // Buscar todos los Rigidbody hijos en el prefab
        Rigidbody[] fragments = GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rb in fragments)
        {
            if (rb != null)
            {
                // Desvincular las piezas ligeramente para que la física fluya mejor si están muy pegados
                rb.transform.SetParent(null);

                // Aplicar fuerza explosiva desde el centro del objeto
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius, upwardModifier);

                // Asegurarnos de que también se destruyan los fragmentos sueltos
                Destroy(rb.gameObject, lifetime);
            }
        }
    }

    private void OnDrawGizmos()
    {
        // Dibujar la esfera de explosión en el editor para visualización
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * upwardModifier);
    }
}