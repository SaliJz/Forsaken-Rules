using UnityEngine;

/// <summary>
/// Mina rebotante del enemigo Static.
/// Extiende BaseTrapMine añadiendo física de rebote con Rigidbody.
/// </summary>
public class StaticTrapMine : BaseTrapMine
{
    #region Inspector

    [Header("Física de rebote")]
    [SerializeField] private LayerMask environmentLayer;

    #endregion

    #region Referencias internas

    private Rigidbody rb;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError($"[{name}] Falta el componente Rigidbody en la mina.", this);
            enabled = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if ((environmentLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            rb.linearDamping = 3f;
            rb.angularDamping = 3f;
        }
    }

    #endregion

    #region Inicialización pública

    /// <summary>
    /// Inicializa la mina con impulso de lanzamiento.
    /// </summary>
    /// <param name="wordToDisplay">Texto decorativo (heredado de la versión original).</param>
    /// <param name="dmg">Daño de la explosión.</param>
    /// <param name="SpawnVFX">VFX se instancia
    public void InitializeTrap(string wordToDisplay, float dmg, bool SpawnVFX = true)
    {
        if (rb == null)
        {
            Debug.LogError($"[{name}] InitializeTrap llamado sin Rigidbody valido. Destruyendo mina.", this);
            Destroy(gameObject);
            return;
        }

        CancelInvoke(nameof(ExplodePublic)); // seguridad ante reuso por pooling

        rb.useGravity = true;
        rb.isKinematic = false;

        damage = dmg;

        Vector3 randomBounce = new Vector3(Random.Range(-2f, 2f), 3f, Random.Range(-2f, 2f));
        rb.AddForce(randomBounce, ForceMode.Impulse);

        if (!SpawnVFX) explosionSpherePrefab = null;

        Invoke(nameof(ExplodePublic), duration);
    }

    // Wrapper para que Invoke pueda llamar a Explode (que es protected en la base)
    private void ExplodePublic() => Explode();

    #endregion
}