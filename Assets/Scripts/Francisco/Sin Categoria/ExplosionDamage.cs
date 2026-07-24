using UnityEngine;
using System.Collections.Generic; 

[RequireComponent(typeof(SphereCollider))]
public class ExplosionDamage : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int damage = 8;
    [SerializeField] private float knockbackForce = 3f;
    [SerializeField] private float knockbackDuration = 0.5f;
    [SerializeField] private LayerMask damageableLayers;

    private SphereCollider sphereCollider;
    private HashSet<Collider> damagedTargets = new HashSet<Collider>(); 

    private void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider col)
    {
        if (damagedTargets.Contains(col))
        {
            return;
        }

        ApplyDamage(col);
    }

    private void ApplyDamage(Collider col)
    {
        //PlayerHealth playerHealth = col.GetComponent<PlayerHealth>();
        //if (playerHealth != null)
        //{
        //    playerHealth.TakeDamage(damage);
        //    damagedTargets.Add(col); 
        //    return;
        //}

        EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage, AttackDamageType.Nothing, transform.position);

            Vector3 knockbackDir = (col.transform.position - transform.position).normalized;
            knockbackDir.y = 0;

            EnemyKnockbackHandler knockback = col.GetComponent<EnemyKnockbackHandler>();
            if (knockback != null)
            {
                knockback.TriggerKnockback(knockbackDir, knockbackForce, knockbackDuration);
            }

            damagedTargets.Add(col); 
            return;
        }

        if (col.CompareTag("EnemyProjectile"))
        {
            Destroy(col.gameObject);
        }
    }

    public void FinishExplosion()
    {
        Destroy(gameObject);
    }

    public void SetDamage(int newDamage) => damage = newDamage;
    public void SetKnockback(float force, float duration)
    {
        knockbackForce = force;
        knockbackDuration = duration;
    }
}