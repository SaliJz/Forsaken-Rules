using UnityEngine;

[RequireComponent(typeof(BoardPiece))]
[RequireComponent(typeof(Collider))]
public class ChessPieceAttack : MonoBehaviour
{
    #region Settings

    [Header("Damage")]
    [SerializeField] private float pushForce = 10f;
    [SerializeField] private float duration = 1f;

    [Header("Hitbox")]
    [SerializeField] private Vector3 hitboxOffset = new Vector3(0f, 0.1f, 0f);
    [SerializeField] private Vector3 hitboxSize = new Vector3(0.8f, 0.1f, 0.8f);

    [Header("Layer")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Gizmos")]
    [SerializeField] private bool showHitboxGizmo = true;
    [SerializeField] private Color gizmoIdleColor = new Color(1f, 1f, 0f, 0.3f);
    [SerializeField] private Color gizmoHitColor = new Color(1f, 0f, 0f, 0.5f);

    #endregion

    #region State

    private bool isAttacking = false;
    private float cooldownTimer = 0f;
    private bool hitThisFrame = false;
    private BoardPiece piece;

    #endregion

    #region Unity Events

    private void Awake()
    {
        piece = GetComponent<BoardPiece>();

        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Update()
    {
        hitThisFrame = false;

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isAttacking) return;
        if (cooldownTimer > 0f) return;
        if (!IsInLayerMask(other.gameObject, playerLayer)) return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>()
                                 ?? other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null) return;

        Vector3 knockDir = (other.transform.position - transform.position).normalized;
        knockDir.y = 0;

        hitThisFrame = true;
        playerHealth.ApplyKnockback(knockDir, pushForce, duration);
        cooldownTimer = duration;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isAttacking) return;
        if (cooldownTimer > 0f) return;
        if (!IsInLayerMask(other.gameObject, playerLayer)) return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>()
                                 ?? other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null) return;

        Vector3 knockDir = (other.transform.position - transform.position).normalized;
        knockDir.y = 0;

        hitThisFrame = true;
        playerHealth.ApplyKnockback(knockDir, pushForce, duration);
        cooldownTimer = duration;
    }

    #endregion

    #region Attack

    public void SetAttacking(bool value) => isAttacking = value;

    public void ForceHitCheck(Vector3 attackDirection)
    {
        Vector3 worldCenter = transform.TransformPoint(hitboxOffset);
        Vector3 worldHalfExtents = Vector3.Scale(hitboxSize * 0.5f, transform.lossyScale);
        Collider[] hits = Physics.OverlapBox(worldCenter, worldHalfExtents, transform.rotation, playerLayer);

        foreach (var col in hits)
        {
            PlayerHealth playerHealth = col.GetComponent<PlayerHealth>()
                                     ?? col.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                Vector3 knockDir = attackDirection.normalized;
                knockDir.y = 0;
                playerHealth.ApplyKnockback(knockDir, pushForce, duration);
                hitThisFrame = true;
                cooldownTimer = duration;
                break;
            }
        }
    }

    #endregion

    #region Helpers

    private static bool IsInLayerMask(GameObject go, LayerMask mask)
        => (mask.value & (1 << go.layer)) != 0;

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (!showHitboxGizmo) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = hitThisFrame ? gizmoHitColor : gizmoIdleColor;
        Gizmos.DrawWireCube(hitboxOffset, hitboxSize);
        Gizmos.matrix = Matrix4x4.identity;
    }

    #endregion
}