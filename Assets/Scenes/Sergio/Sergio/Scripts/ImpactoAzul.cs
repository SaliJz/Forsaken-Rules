using UnityEngine;

public class BlueAttack : MonoBehaviour
{
    public float attackDamage = 25f;
    public float attackRange = 2f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Attack();
        }
    }

    void Attack()
    {
        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, attackRange);

        foreach (Collider col in nearbyEnemies)
        {
            SoulEnemy enemy = col.GetComponent<SoulEnemy>();
            if (enemy != null)
            {
                //enemy.TakeDamage(attackDamage);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}