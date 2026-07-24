using UnityEngine;

public class StaticProjectileLevel3 : StaticProjectileBase
{
    [Header("Level 3 Settings")]
    [SerializeField] private GameObject swarmPrefab;
    [SerializeField] private float swarmDuration = 1f;
    [SerializeField] private float swarmDPS = 1f;
    [SerializeField] private float swarmRadius = 1.5f;

    private float maxRange = 30f;

    private void Update()
    {
        if (hasImpacted) return;

        if (Vector3.Distance(transform.position, originPosition) >= maxRange)
        {
            TriggerImpact();
        }
    }

    protected override void OnPlayerHit(GameObject player)
    {
        hasImpacted = true;
        player.GetComponent<IDamageable>()?.TakeDamage(damage);
        if (audioSource != null && impactSound != null) audioSource.PlayOneShot(impactSound);
        TriggerImpact();
    }

    protected override void OnEnvironmentHit(GameObject obstacle)
    {
        hasImpacted = true;
        if (audioSource != null && impactSound != null) audioSource.PlayOneShot(impactSound);
        TriggerImpact();
    }

    private void TriggerImpact()
    {
        if (swarmPrefab != null)
        {
            GameObject swarm = Instantiate(swarmPrefab, transform.position, Quaternion.identity);
            swarm.GetComponent<StaticSwarmArea>()?.Initialize(swarmDuration, swarmDPS, swarmRadius);
        }
        Destroy(gameObject);
    }
}