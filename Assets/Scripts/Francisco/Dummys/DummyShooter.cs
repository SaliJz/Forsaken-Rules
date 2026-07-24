using UnityEngine;

public class DummyShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform modelTransform;

    [Header("Shoot Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float launchSpeed = 10f;
    [SerializeField] private float fireRate = 2.0f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 5.0f;

    [Header("Player Health Condition")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float stopShootingThreshold = 0.5f;

    private float nextFireTime;
    private PlayerHealth playerHealth;
    private Transform playerTransform;

    private void Start()
    {
        if (firePoint == null)
        {
            firePoint = transform;
        }

        if (modelTransform == null)
        {
            modelTransform = this.transform;
            Debug.LogWarning("[DummyShooter] 'Model Transform' no asignado. Usando el Transform del script como fallback.");
        }

        nextFireTime = Time.time + fireRate;

        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogError($"[DummyShooter] No se encontró el componente PlayerHealth en el objeto con tag '{playerTag}'.");
            }
        }
        else
        {
            Debug.LogError($"[DummyShooter] No se encontró ningún objeto con el tag '{playerTag}'.");
        }
    }

    private void Update()
    {
        if (ShouldStopShooting())
        {
            return;
        }

        RotateTowardsPlayer();

        if (Time.time > nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    private void RotateTowardsPlayer()
    {
        if (playerTransform == null || modelTransform == null) return;

        Vector3 directionToPlayer = playerTransform.position - modelTransform.position;
        directionToPlayer.y = 0;

        if (directionToPlayer == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);

        modelTransform.rotation = Quaternion.Slerp(modelTransform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }

    private bool ShouldStopShooting()
    {
        if (playerHealth == null)
        {
            return false;
        }

        float healthRatio = playerHealth.CurrentHealth / playerHealth.MaxHealth;

        if (healthRatio <= stopShootingThreshold)
        {
            return true;
        }

        return false;
    }

    private void Shoot()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("[DummyShooter] ¡Falta asignar el Prefab del Proyectil!");
            return;
        }

        GameObject projectileGO = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        Rigidbody rb = projectileGO.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = -firePoint.forward * launchSpeed;
        }
        else
        {
            Debug.LogWarning("[DummyShooter] El prefab del proyectil no tiene Rigidbody. Se mueve usando Transform.");
            projectileGO.transform.position += firePoint.forward * launchSpeed * Time.deltaTime;
        }
    }
}