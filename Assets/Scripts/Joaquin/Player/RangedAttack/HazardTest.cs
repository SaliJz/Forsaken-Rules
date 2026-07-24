using UnityEngine;

public class HazardTest : MonoBehaviour
{
    [SerializeField] private float damagePerSecond = 10f;
    [SerializeField] private float damageTickRate = 1f;

    private float nextDamageTime;

    private void OnTriggerStay(Collider other)
    {
        if (Time.time >= nextDamageTime)
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damagePerSecond);

                nextDamageTime = Time.time + damageTickRate;
            }
        }
    }
}