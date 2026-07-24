using UnityEngine;

public class ProjectileDummy : MonoBehaviour
{
    #region [ Settings ]

    [Header("Settings")]
    [SerializeField] private string[] ignoreTags;
    [SerializeField] private float damage = 5f;
    [SerializeField] private float lifetime = 5f;

    #endregion

    private void Awake()
    {
        if (!gameObject.CompareTag("EnemyProjectile"))
        {
            gameObject.tag = "EnemyProjectile";
        }
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject);
    }

    private void HandleCollision(GameObject collidedObject)
    {
        foreach (string tag in ignoreTags)
        {
            if (collidedObject.CompareTag(tag))
            {
                return;
            }
        }

        if (collidedObject.CompareTag("Player"))
        {
            if (collidedObject.TryGetComponent<PlayerHealth>(out var playerHealth))
            {
                if (collidedObject.TryGetComponent<PlayerBlockSystem>(out var blockSystem) 
                    && blockSystem.IsBlockingState() 
                    && blockSystem.CanBlockAttack(this.transform.position))
                {
                    float remainingDamage = blockSystem.ProcessBlockedAttack(damage, this.gameObject);

                    ShieldHitManager.Instance?.RegisterShieldHit();

                    if (remainingDamage > 0f)
                    {
                        playerHealth.TakeDamage(remainingDamage);
                    }

                    Destroy(gameObject);
                    return;
                }

                playerHealth.TakeDamage(damage);
            }
            else
            {
                collidedObject.GetComponent<PlayerHealth>()?.TakeDamage(damage);
            }

            Destroy(gameObject);
            return;
        }

        Destroy(gameObject);
    }
}