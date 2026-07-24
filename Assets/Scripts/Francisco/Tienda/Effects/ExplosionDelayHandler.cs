using UnityEngine;
using System.Collections;

public class ExplosionDelayHandler : MonoBehaviour
{
    private float _explosionRadius;
    private float _explosionDamagePercentage;
    private GameObject _explosionVisualizerPrefab;
    private float _enemyBaseHealth;

    public void StartExplosion(float damagePercent, float radius, GameObject visualizerPrefab, float baseHealth, float delay)
    {
        _explosionDamagePercentage = damagePercent;
        _explosionRadius = radius;
        _explosionVisualizerPrefab = visualizerPrefab;
        _enemyBaseHealth = baseHealth;

        transform.SetParent(null);

        StartCoroutine(ExplosionRoutine(delay));
    }

    private IEnumerator ExplosionRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_explosionVisualizerPrefab != null)
        {
            Instantiate(_explosionVisualizerPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("[ExplosionDelayHandler] explosionVisualizerPrefab es nulo. Asignalo en ExplosiveItemEffect.");
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        if (_explosionRadius > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _explosionRadius);
        }
    }
}