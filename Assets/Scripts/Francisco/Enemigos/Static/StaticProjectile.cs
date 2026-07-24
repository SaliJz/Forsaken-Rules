using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class StaticProjectile : MonoBehaviour
{
    #region Private State

    private float speed;
    [SerializeField] private float currentDamage;
    [SerializeField] private float maxDamageLimit;
    [SerializeField] private float damageGainPerSecond;
    private float swarmDuration;
    private float swarmDPS;
    private float swarmRadius;
    private GameObject swarmParticlePrefab;

    private bool hasImpacted = false;
    private float maxRange = 30f;
    private Vector3 originPosition;
    private Rigidbody rb;

    #endregion

    #region Init

    public void Initialize(float projectileSpeed, float minDamage, float maxDamage, float distanceToMax,
        float inSwarmDuration, float inSwarmDPS, float inSwarmRadius, GameObject inSwarmParticlePrefab, string word)
    {
        speed = projectileSpeed;
        currentDamage = minDamage;
        maxDamageLimit = maxDamage;
        swarmDuration = inSwarmDuration;
        swarmDPS = inSwarmDPS;
        swarmRadius = inSwarmRadius;
        swarmParticlePrefab = inSwarmParticlePrefab;
        originPosition = transform.position;

        MorlockProjectileWordTrail trail = GetComponent<MorlockProjectileWordTrail>();
        if (trail != null)
        {
            trail.InitializeWord(word);
        }

        float timeToMax = distanceToMax / speed;
        damageGainPerSecond = (maxDamage - minDamage) / timeToMax;

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.linearVelocity = transform.forward * speed;
    }

    #endregion

    #region Update

    private void Update()
    {
        if (hasImpacted) return;

        if (currentDamage < maxDamageLimit)
        {
            currentDamage += damageGainPerSecond * Time.deltaTime;
            currentDamage = Mathf.Min(currentDamage, maxDamageLimit);
        }

        if (Vector3.Distance(transform.position, originPosition) >= maxRange)
            TriggerImpact(transform.position);
    }

    #endregion

    #region Collision

    private void OnTriggerEnter(Collider other)
    {
        if (hasImpacted) return;

        if (other.CompareTag("Player"))
        {
            IDamageable damageable = other.GetComponent<IDamageable>();
            damageable?.TakeDamage(currentDamage);
        }

        TriggerImpact(transform.position);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasImpacted) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
            damageable?.TakeDamage(currentDamage);
        }

        TriggerImpact(transform.position);
    }

    #endregion

    #region Impact & Swarm

    private void TriggerImpact(Vector3 impactPosition)
    {
        hasImpacted = true;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        SpawnSwarm(impactPosition);
        Destroy(gameObject);
    }

    private void SpawnSwarm(Vector3 position)
    {
        if (swarmParticlePrefab != null)
        {
            GameObject areaObj = Instantiate(swarmParticlePrefab, position, Quaternion.identity);
            StaticSwarmArea areaScript = areaObj.GetComponent<StaticSwarmArea>();
            if (areaScript != null)
            {
                areaScript.Initialize(swarmDuration, swarmDPS, swarmRadius);
            }
        }
    }

    #endregion
}