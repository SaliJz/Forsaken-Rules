using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PetraSpike : MonoBehaviour
{
    #region Public Fields

    [SerializeField] private GameObject _PilarHide;
    [SerializeField] private GameObject _PilarShow;
    [SerializeField] private ParticleSystem _VFX;

    #endregion

    #region Private Fields

    private bool isDeactivating = false;
    private bool lifetimePaused = false;
    private float damage;
    private LayerMask enemyLayer;
    private bool isLargeSpike;
    private float tickCooldown = 0.5f;
    private float tickTimer = 0f;
    private HashSet<Collider> overlappingEnemies = new HashSet<Collider>();
    private CapsuleCollider capsuleCollider;
    private Action onReturn;

    #endregion

    #region Public Methods

    public void Initialize(float damage, float lifetime, LayerMask enemyLayer, bool isLargeSpike, Action onReturn)
    {
        this.damage = damage;
        this.enemyLayer = enemyLayer;
        this.isLargeSpike = isLargeSpike;
        this.onReturn = onReturn;

        overlappingEnemies.Clear();
        tickTimer = 0f;

        var torre = GetComponentInChildren<TorreMovimiento>();
        if (torre != null) torre.Initialize(transform);

        StopAllCoroutines();
        StartCoroutine(LifetimeRoutine(lifetime));
    }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        capsuleCollider = GetComponent<CapsuleCollider>();
    }

    private void Update()
    {
        if (isLargeSpike || overlappingEnemies.Count == 0) return;

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickCooldown;
            foreach (var col in overlappingEnemies)
            {
                if (col != null) DealDamageTo(col.gameObject);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsEnemy(other) || isDeactivating) return;
        DealDamageTo(other.gameObject);
        if (!isLargeSpike)
        {
            overlappingEnemies.Add(other);
            tickTimer = tickCooldown;
        }
        StartCoroutine(Desactivate());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isLargeSpike) overlappingEnemies.Remove(other);
    }

    #endregion

    #region Private Methods

    private bool IsEnemy(Collider col)
        => (enemyLayer.value & (1 << col.gameObject.layer)) > 0;

    private void DealDamageTo(GameObject enemy)
    {
        var health = enemy.GetComponent<EnemyHealth>();
        if (health != null) health.TakeDamage(Mathf.RoundToInt(damage));
    }

    private IEnumerator LifetimeRoutine(float lifetime)
    {
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            if (!lifetimePaused)
                elapsed += Time.deltaTime;
            yield return null;
        }
        if (!lifetimePaused)
            onReturn?.Invoke();
    }
    
    private IEnumerator Desactivate()
    {
        isDeactivating = true;
        lifetimePaused = true;
        _PilarHide.SetActive(false);
        _PilarShow.SetActive(true);
        _VFX.gameObject.SetActive(true);
        _VFX.Play();
        capsuleCollider.enabled = false;
        yield return new WaitForSeconds(1f);
        _PilarHide.SetActive(true);
        _PilarShow.SetActive(false);
        _VFX.gameObject.SetActive(false);
        capsuleCollider.enabled = true;
        isDeactivating = false;
        onReturn?.Invoke();
        lifetimePaused = false;
        gameObject.SetActive(false);
    }

    #endregion
}