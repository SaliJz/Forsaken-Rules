using UnityEngine;

[DisallowMultipleComponent]
public class Bodyblocker : MonoBehaviour
{
    #region Inspector Fields
    [Header("Block Zone")]
    [SerializeField] private Vector3 boxSize = new Vector3(0.5f, 0.5f, 2f);
    [SerializeField] private Vector3 boxOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField] private bool autoSizeFromCollider = true;            
    [SerializeField] private float sizeMultiplier = 1.05f;                  
    [SerializeField] private LayerMask playerLayer;

    [Header("Push Force")]
    [SerializeField] private float pushForce = 8f;
    [SerializeField] private float upwardBias = 2f;
    [SerializeField] private float pushDuration = 0.15f;
    #endregion

    #region Private State
    private Collider enemyCollider;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        enemyCollider = GetComponent<Collider>();
        if (enemyCollider == null)
            Log("No Collider found. Using manual boxSize/boxOffset.", 2);
    }

    private void FixedUpdate()
    {
        if (playerLayer.value == 0) return;

        GetBoxParams(out Vector3 center, out Vector3 halfExtents);

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            transform.rotation,  
            playerLayer,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null) continue;

            Vector3 pushDir = hit.transform.position - transform.position;
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude < 0.001f)
                pushDir = transform.right;
            pushDir.Normalize();
            pushDir.y = upwardBias;

            playerHealth.ApplyKnockback(pushDir, pushForce, pushDuration);
            Log($"Pushed player. Dir: {pushDir}, Force: {pushForce}", 1);
        }
    }
    #endregion

    #region Helpers
    private void GetBoxParams(out Vector3 center, out Vector3 halfExtents)
    {
        if (autoSizeFromCollider && enemyCollider != null)
        {
            Bounds b = enemyCollider.bounds;
            center = b.center;
            halfExtents = b.extents * sizeMultiplier;
        }
        else
        {
            center = transform.TransformPoint(boxOffset);
            halfExtents = boxSize * 0.5f;
        }
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmos()
    {
        if (enemyCollider == null)
            enemyCollider = GetComponent<Collider>();

        GetBoxParams(out Vector3 center, out Vector3 halfExtents);

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        Gizmos.matrix = Matrix4x4.identity;
    }
    #endregion

    #region Debug
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void Log(string message, int level)
    {
        switch (level)
        {
            case 1: Debug.Log($"[Bodyblocker] {message}"); break;
            case 2: Debug.LogWarning($"[Bodyblocker] {message}"); break;
            case 3: Debug.LogError($"[Bodyblocker] {message}"); break;
        }
    }
    #endregion
}