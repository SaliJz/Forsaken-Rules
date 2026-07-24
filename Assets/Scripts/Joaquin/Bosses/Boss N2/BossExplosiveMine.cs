using UnityEngine;

/// <summary>
/// Mina explosiva estática colocada por el Jefe 2.
/// </summary>
public class BossExplosiveMine : BaseTrapMine
{
    #region Inspector

    [Header("Alineacion al suelo")]
    [SerializeField] private float groundOffset = 0.05f;

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
    }

    private void Start()
    {
        rb.useGravity = true;
        rb.isKinematic = false;

        Vector3 randomBounce = new Vector3(Random.Range(-2f, 2f), 3f, Random.Range(-2f, 2f));
        rb.AddForce(randomBounce, ForceMode.Impulse);

        Invoke(nameof(AlignToGround), duration/2);
        Invoke(nameof(ExplodePublic), duration);
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

    #region Helpers

    private void AlignToGround()
    {
        int groundLayerMask = LayerMask.GetMask("Ground");
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, groundLayerMask))
        {
            transform.position = hit.point + Vector3.up * groundOffset;
        }
    }

    // Wrapper para Invoke
    private void ExplodePublic() => Explode();

    #endregion
}