using UnityEngine;

/// <summary>
/// Rastro de cristales que aparece cuando el jefe choca contra una pared
/// durante "Carga de los Quebrados".
/// </summary>
public class CrystalTrail : MonoBehaviour
{
    #region Inspector - Dano

    [Header("Dano")]
    [SerializeField] private float damagePerSecond = 4f;

    #endregion

    #region Inspector - Tiempo de vida

    [Header("Tiempo de vida")]
    [SerializeField] private float lifetime = 8f;

    #endregion

    #region Inspector - Alineacion

    [Header("Alineacion")]
    [SerializeField] private float groundOffset = 0.01f;

    #endregion

    #region Internal State

    private float damageTimer;

    #endregion

    #region Public Properties & Events

    public float DamagePerSecond 
    { 
        get => damagePerSecond; 
        set => damagePerSecond = value; 
    }
    public float Lifetime 
    { 
        get => lifetime; 
        set => lifetime = value; 
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        AlignToGround();
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        damageTimer += Time.deltaTime;
        if (damageTimer < 1f) return;

        other.GetComponent<PlayerHealth>()?.TakeDamage(damagePerSecond);
        damageTimer = 0f;
    }

    #endregion

    #region Alineacion de Terreno

    private void AlignToGround()
    {
        int groundLayer = LayerMask.GetMask("Ground");
        if (Physics.Raycast(transform.position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, Mathf.Infinity, groundLayer))
            transform.position = hit.point + Vector3.up * groundOffset;
        else
            Debug.LogWarning("[CrystalTrail] No se encontro suelo. Verifica la capa 'Ground'.");
    }

    #endregion

    #region Logging

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.3f);
        Gizmos.DrawCube(transform.position, transform.localScale);
    }

    #endregion
}