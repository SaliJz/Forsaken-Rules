using System;
using System.Collections.Generic;
using UnityEngine;

public class KaiWave : MonoBehaviour
{
    #region Private State

    private float damage;
    private float speed;           
    private float maxWidth;       
    private float growthDuration; 
    private float totalDuration;   
    private LayerMask enemyLayer;
    private Action onReturn;

    private float elapsed;
    private float currentWidth;
    private bool isActive;

    private Vector3 halfExtents;

    private HashSet<Collider> hitThisLife = new HashSet<Collider>();

    private Renderer waveRenderer;
    private MaterialPropertyBlock mpb;
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    #endregion

    #region Unity

    private void Awake()
    {
        waveRenderer = GetComponentInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (!isActive) return;

        elapsed += Time.deltaTime;

        float growT = Mathf.Clamp01(elapsed / growthDuration);
        currentWidth = Mathf.Lerp(0f, maxWidth, growT);

        transform.localScale = new Vector3(currentWidth, transform.localScale.y, transform.localScale.z);

        if (speed > 0f)
            transform.position += transform.forward * speed * Time.deltaTime;

        halfExtents = new Vector3(currentWidth * 0.5f, 0.75f, transform.localScale.z * 0.5f);
        Collider[] hits = Physics.OverlapBox(transform.position, halfExtents, transform.rotation, enemyLayer);

        foreach (Collider col in hits)
        {
            if (hitThisLife.Contains(col)) continue;
            hitThisLife.Add(col);

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(damage);
        }

        float lifeT = elapsed / totalDuration;
        if (lifeT > 0.8f && waveRenderer != null)
        {
            float fadeT = (lifeT - 0.8f) / 0.2f;
            waveRenderer.GetPropertyBlock(mpb);
            Color c = mpb.GetVector(ColorID);
            c.a = Mathf.Lerp(1f, 0f, fadeT);
            mpb.SetColor(ColorID, c);
            waveRenderer.SetPropertyBlock(mpb);
        }

        if (elapsed >= totalDuration)
            Return();
    }

    private void OnDisable()
    {
        isActive = false;
    }

    #endregion

    #region Public API

    public void Activate(
        Vector3 spawnPosition,
        Vector3 direction,
        float waveDamage,
        float waveSpeed,
        float waveMaxWidth,
        float waveGrowthDuration,
        float waveTotalDuration,
        LayerMask layer,
        Action returnCallback)
    {
        elapsed = 0f;
        currentWidth = 0f;
        hitThisLife.Clear();
        isActive = true;

        damage = waveDamage;
        speed = waveSpeed;
        maxWidth = waveMaxWidth;
        growthDuration = waveGrowthDuration > 0f ? waveGrowthDuration : 0.001f;
        totalDuration = waveTotalDuration;
        enemyLayer = layer;
        onReturn = returnCallback;

        transform.position = spawnPosition;
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);

        transform.localScale = new Vector3(0f, transform.localScale.y, transform.localScale.z);

        if (waveRenderer != null)
        {
            waveRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(ColorID, Color.white);
            waveRenderer.SetPropertyBlock(mpb);
        }

        gameObject.SetActive(true);
    }

    public void ForceReturn() => Return();

    #endregion

    #region Private

    private void Return()
    {
        isActive = false;
        gameObject.SetActive(false);
        onReturn?.Invoke();
    }

    #endregion

    #region Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!isActive) return;
        Gizmos.color = new Color(1f, 0.2f, 0.4f, 0.4f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);
    }
#endif

    #endregion
}