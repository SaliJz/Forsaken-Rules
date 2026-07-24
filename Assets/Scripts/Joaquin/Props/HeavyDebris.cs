using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeavyDebris : MonoBehaviour
{
    [Header("Feedback de Impacto")]
    [SerializeField] private GameObject impactVfx;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private LayerMask groundLayer;

    [Header("Configuraci�n de Da�o")]
    [SerializeField] private bool canDamaged = true;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private float impactRadius = 5f;
    [SerializeField] private float damageToEnemies = 10f;

    [Header("Configuraci�n de Encogimiento")]
    [SerializeField] private float shrinkScaleFactor = 0.25f;
    [SerializeField] private float shrinkDuration = 2.5f;
    [SerializeField] private float shrinkDelay = 1f;

    private bool hasLanded = false;
    private float debrisLifetime;
    private Rigidbody rb;
    private Collider debrisCollider;

    private List<Transform> hitTargets = new List<Transform>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        trailRenderer = GetComponentInChildren<TrailRenderer>();
        debrisCollider = GetComponent<Collider>();
    }

    public void Initialized(float lifeTime)
    {
        debrisLifetime = lifeTime;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Solo activar una vez y solo si tocamos suelo
        if (hasLanded) return;

        if (((1 << collision.gameObject.layer) & enemyLayers) != 0)
        {
            if (canDamaged) DamageNearbyEnemies();
        }

        // Verificar si chocamos con el suelo
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            LandEffect(collision.contacts[0].point);
        }
    }

    private void LandEffect(Vector3 hitPoint)
    {
        hasLanded = true;

        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
            trailRenderer.Clear();
        }

        if (canDamaged) DamageNearbyEnemies();

        if (debrisCollider != null)
        {
            debrisCollider.enabled = false;
        }

        StartCoroutine(Shrink(transform, shrinkDelay, shrinkDuration));

        if (impactVfx != null)
        {
            Instantiate(impactVfx, hitPoint + Vector3.up * 0.1f, Quaternion.LookRotation(Vector3.up));
        }

        // if (impactSound != null) AudioSource.PlayClipAtPoint(impactSound, hitPoint);

        rb.linearVelocity *= 0.2f;
        rb.angularVelocity *= 0.2f;
    }

    private void DamageNearbyEnemies()
    {
        // Detectar colliders en el radio definido
        Collider[] hits = Physics.OverlapSphere(transform.position, impactRadius, enemyLayers);

        foreach (Collider hit in hits)
        {
            if (hitTargets.Contains(hit.transform))
            {
                continue;
            }

            hitTargets.Add(hit.transform);

            // Evitar da�arse a s� mismo si el pilar estuviera en esa layer
            if (hit.gameObject == this.gameObject) continue;

            // Buscar componente de vida en el enemigo
            if (hit.TryGetComponent(out EnemyHealth enemyHealth))
            {
                // Aplicar da�o.
                enemyHealth.TakeDamage(damageToEnemies, AttackDamageType.Nothing, transform.position);
                Debug.Log($"Pilar da�� a {hit.name} por la ca�da de escombros.");
            }
        }
    }

    private IEnumerator Shrink(Transform transform, float delay, float duration)
    {
        yield return new WaitForSeconds(delay);

        // Si el tiempo restante es menor al tiempo de encogimiento, no hacer nada
        if (duration >= debrisLifetime - delay) yield break;

        // Verificar si el objeto a�n existe
        if (!this) yield break;

        Vector3 initialScale = transform.localScale;
        Vector3 newScale = new Vector3(shrinkScaleFactor, shrinkScaleFactor, shrinkScaleFactor);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(initialScale, newScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = newScale;
    }
}