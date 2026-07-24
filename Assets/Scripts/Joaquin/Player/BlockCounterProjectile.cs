using UnityEngine;

public class BlockCounterProjectile : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private GameObject impactVFX;
    [SerializeField] private LayerMask collisionLayers;

    private float damageToDeal;
    private bool isInitialized = false;
    private Vector3 direction;
    private int enemyLayer;
    private int enemyProjectileLayer;

    public void Initialize(float damage, Vector3 fireDirection)
    {
        enemyLayer = LayerMask.NameToLayer("Enemy");
        enemyProjectileLayer = LayerMask.NameToLayer("EnemyProjectile");

        this.damageToDeal = damage;
        this.direction = fireDirection.normalized;
        this.isInitialized = true;

        transform.forward = this.direction;

        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        if (!isInitialized) return;

        transform.position += direction * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isInitialized) return;

        if (other.CompareTag("Player")) return;
        if (other.gameObject.layer == enemyProjectileLayer) Destroy(other.gameObject);

        if (other.CompareTag("TutorialDummy") || other.gameObject.layer == enemyLayer)
        {
            ExecuteAttack(other.gameObject, damageToDeal);
            Debug.Log("Golpe A dummy");
            if (impactVFX != null) Instantiate(impactVFX, transform.position, Quaternion.identity);
            Destroy(gameObject);
            return;
        }

        if ((collisionLayers.value & (1 << other.gameObject.layer)) != 0)
        {
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Enemy") || other.gameObject.layer == enemyLayer)
        {
            ExecuteAttack(other.gameObject, damageToDeal);
            if (impactVFX != null) Instantiate(impactVFX, transform.position, Quaternion.identity);
            Destroy(gameObject);
            return;
        }
    }

    private void ExecuteAttack(GameObject target, float damageAmount)
    {   
        if (target.TryGetComponent<TutorialCombatDummy>(out var dummy))
        {
            dummy.TakeDamage(damageAmount, false, DamageType.Shield);
        }
        else if(target.TryGetComponent<DrogathEnemy>(out var blockSystem) && target.TryGetComponent<EnemyHealth>(out var health))
        {
            if (blockSystem.ShouldBlockDamage(transform.position))
            {
                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Ranged);
        }
        else if (target.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable.TakeDamage(damageAmount, false, AttackDamageType.Ranged);
        }
    }
}