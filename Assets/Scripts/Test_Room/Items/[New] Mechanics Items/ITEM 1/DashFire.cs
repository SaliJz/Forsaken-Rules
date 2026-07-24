using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DashFire : MonoBehaviour
{
    #region Private Fields

    private float dps;
    private float expandDuration;
    private float maxRadius;
    private float stayDuration;
    private float tickInterval;
    private LayerMask enemyLayer;
    private Action onReturn;

    private float currentRadius = 0f;
    private float tickTimer = 0f;
    private bool isActiveAndTicking = false;

    private HashSet<Collider> overlapping = new HashSet<Collider>();

    private Transform visual;
    private CapsuleCollider capsule;
    private Coroutine routine;

    private const float groundOffset = -1.5f;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        capsule = GetComponent<CapsuleCollider>();

        if (transform.childCount > 0) visual = transform.GetChild(0);
    }

    private void Update()
    {
        if (!isActiveAndTicking) return;

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval;

            overlapping.RemoveWhere(col => col == null || !col.gameObject.activeInHierarchy);

            foreach (var col in overlapping)
            {
                ApplyChargeOrDamage(col.gameObject);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((enemyLayer.value & (1 << other.gameObject.layer)) > 0)
        {
            overlapping.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        overlapping.Remove(other);
    }

    #endregion

    #region Public Methods

    public void Activate(Vector3 position, float damageAmount, float expandDuration, float maxRadius,
                          float stayDuration, float tickInterval, LayerMask enemyLayer, Action onReturn)
    {
        this.dps = damageAmount;
        this.expandDuration = expandDuration;
        this.maxRadius = maxRadius;
        this.stayDuration = stayDuration;
        this.tickInterval = tickInterval;
        this.enemyLayer = enemyLayer;
        this.onReturn = onReturn;

        Vector3 finalPos = position;

        if (Physics.Raycast(position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 5f))
        {
            finalPos = hit.point + (Vector3.up * groundOffset);
        }
        else
        {
            finalPos.y = groundOffset;
        }

        transform.position = finalPos;

        currentRadius = 0f;
        tickTimer = 0.1f;
        isActiveAndTicking = false;
        overlapping.Clear();

        SetSize(0f);
        gameObject.SetActive(true);

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(CircleRoutine());
    }

    public void ForceReturn()
    {
        if (routine != null) StopCoroutine(routine);
        isActiveAndTicking = false;
        overlapping.Clear();
        onReturn?.Invoke();
    }

    #endregion

    #region Private Methods

    private IEnumerator CircleRoutine()
    {
        isActiveAndTicking = true;
        float elapsed = 0f;

        while (elapsed < expandDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / expandDuration);
            currentRadius = Mathf.Lerp(0f, maxRadius, t);
            SetSize(currentRadius);
            yield return null;
        }

        currentRadius = maxRadius;
        SetSize(currentRadius);

        yield return new WaitForSeconds(stayDuration);

        isActiveAndTicking = false;
        overlapping.Clear();
        onReturn?.Invoke();
    }

    private void SetSize(float radius)
    {
        if (capsule != null)
            capsule.radius = radius;

        if (visual != null)
        {
            float diameter = radius * 2f;
            visual.localScale = new Vector3(diameter, 0.05f, diameter);
            visual.localPosition = Vector3.zero;
        }
    }

    private void ApplyChargeOrDamage(GameObject enemy)
    {
        var chargeSystem = enemy.GetComponentInParent<EntropyChargeSystem>();
        if (chargeSystem != null)
        {
            chargeSystem.ApplyCharge();
            return;
        }

        EnemyHealth health = enemy.GetComponentInParent<EnemyHealth>();
        if (health != null)
        {
            health.TakeDamage(dps * tickInterval);
        }
    }

    #endregion
}