using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BlueWill : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 10f;
    public bool canMove = true;

    [Header("Combat Settings")]
    public float attackRange = 5f;
    public float attackDamage = 50f;
    public KeyCode attackKey = KeyCode.Space;

    void Update()
    {
        if (canMove)
        {
            float moveHorizontal = Input.GetAxis("Horizontal");
            float moveVertical = Input.GetAxis("Vertical");
            Vector3 direction = new Vector3(moveHorizontal, 0, moveVertical);
            transform.Translate(direction * speed * Time.deltaTime, Space.World); 
        }

        if (Input.GetKeyDown(attackKey))
        {
            Attack();
        }
    }

    void Attack()
    {
        Debug.Log("Player atacando");

        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, attackRange);

        foreach (Collider enemyCollider in hitEnemies)
        {
            SoulEnemy enemy = enemyCollider.GetComponent<SoulEnemy>();

            if (enemy != null)
            {
                //enemy.TakeDamage(attackDamage);
                Debug.Log("Impacto directo al SoulEnemy. Daño: " + attackDamage + "</color>");
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}