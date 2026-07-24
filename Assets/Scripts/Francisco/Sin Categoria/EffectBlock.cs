using UnityEngine;

public class EffectBlock : MonoBehaviour
{
    [Header("Configuración de Movimiento y Escala")]
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float forwardSpeed = 10f;
    [SerializeField] private float sidewaysScaleRate = 10f; 
    [SerializeField] private float maxSidewaysScaleFactor = 3f;
    [SerializeField] private float minorScaleFactorY = 0.3f; 

    [Header("Interacción")]
    [SerializeField] private float knockbackForce = 15f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask projectileLayer;

    private float timeElapsed = 0f;
    private Vector3 initialScale;
    private Vector3 moveDirection;

    public void Initialize(Vector3 direction, Vector3 playerPosition, Quaternion playerRotation)
    {
        moveDirection = direction.normalized;
        initialScale = transform.localScale;

        transform.position = playerPosition + moveDirection * 0.5f;
        transform.rotation = playerRotation;
    }

    private void Update()
    {
        timeElapsed += Time.deltaTime;

        if (timeElapsed < duration)
        {
            transform.position += moveDirection * forwardSpeed * Time.deltaTime;

            Vector3 currentScale = transform.localScale;

            float scaleIncreaseBase = sidewaysScaleRate * Time.deltaTime;

            currentScale.x = Mathf.Min(
                currentScale.x + scaleIncreaseBase,
                initialScale.x * maxSidewaysScaleFactor
            );

            float scaleIncreaseY = scaleIncreaseBase * minorScaleFactorY;
            currentScale.y = Mathf.Min(
                currentScale.y + scaleIncreaseY,
                initialScale.y * maxSidewaysScaleFactor
            );

            transform.localScale = currentScale;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & projectileLayer) != 0)
        {
            Destroy(other.gameObject);
            Debug.Log($"[EffectBlock] Proyectil {other.gameObject.name} destruido.");
            return;
        }

        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            if (other.TryGetComponent<EnemyKnockbackHandler>(out var knockbackHandler))
            {
                Vector3 pushDirection = moveDirection;
                pushDirection.y = 0;

                float knockbackDuration = 0.3f;

                knockbackHandler.TriggerKnockback(
                    pushDirection.normalized,
                    knockbackForce,
                    knockbackDuration
                );

                Debug.Log($"[EffectBlock] Aplicado empuje a {other.gameObject.name} usando EnemyKnockbackHandler.");
            }
        }
    }
}