// VidaEnemigoEscudo.cs (actualizado: manejo por-source de áreas)
/*
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class VidaEnemigoEscudo : MonoBehaviour, IDamageable
{
    [Header("Health / UI")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = -1f; // si es <=0 se inicializa a maxHealth en Awake
    [SerializeField] private float deathCooldown = 2f;
    [SerializeField] private float healthSteal = 5f;

    [Header("UI - Slider (opcional)")]
    [SerializeField] private Slider lifeSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private float offsetAboveEnemy = 2f;
    [SerializeField] private float glowDelayAfterCritical = 2f;

    public float CurrentHealth
    {
        get => currentHealth;
        private set
        {
            if (!Mathf.Approximately(currentHealth, value))
            {
                currentHealth = value;
                OnEnemyHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }
    }
    public float MaxHealth => maxHealth;

    public static event Action<float, float> OnEnemyHealthChanged;
    public event Action<GameObject> OnDeath;

    private bool isDead = false;
    private Coroutine currentCriticalDamageCoroutine;
    private EnemyVisualEffects enemyVisualEffects;

    // Reduccion local que aplica al TakeDamage (cuando la propia IA activa su armadura)
    private float _reduccionLocal = 0f;

    // NUEVO: tracking por-area (source) para aplicar/quitar reducción solo mientras la entidad está dentro
    private readonly Dictionary<GameObject, float> activeAreaReductions = new Dictionary<GameObject, float>();
    private readonly Dictionary<GameObject, Coroutine> areaTimers = new Dictionary<GameObject, Coroutine>();

    private void Awake()
    {
        enemyVisualEffects = GetComponent<EnemyVisualEffects>();
        currentHealth = Mathf.Clamp(currentHealth > 0f ? currentHealth : maxHealth, 0f, maxHealth);
        UpdateSlidersSafely();
    }

    #region IDamageable / Salud
    public void TakeDamage(float damageAmount, bool isCritical = false, AttackDamageType attackDamageType = AttackDamageType.Melee)
    {
        if (isDead) return;
        if (currentHealth <= 0f) return;

        float cantidad = damageAmount;

        // aplicar reducción local (si existe)
        if (_reduccionLocal > 0f)
        {
            cantidad = cantidad * (1f - _reduccionLocal);
        }

        currentHealth -= cantidad;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        // actualizar propiedad y UI
        CurrentHealth = currentHealth;
        UpdateSlidersSafely();

        // feedback visual / sonido
        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.PlayHealthHitFeedback(transform.position + Vector3.up * offsetAboveEnemy, cantidad, isCritical);

            if (isCritical)
            {
                enemyVisualEffects.StartArmorGlow();

                if (currentCriticalDamageCoroutine != null) StopCoroutine(currentCriticalDamageCoroutine);
                currentCriticalDamageCoroutine = StartCoroutine(StopGlowAfterDelay(glowDelayAfterCritical));
            }
        }

        if (currentHealth <= 0f)
        {
            CurrentHealth = 0f;
            Die();
        }
    }

    private IEnumerator StopGlowAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.StopArmorGlow();
        }
    }

    public void Heal(float healAmount)
    {
        if (isDead) return;
        if (currentHealth <= 0f) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        CurrentHealth = currentHealth;
        UpdateSlidersSafely();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        OnDeath?.Invoke(gameObject);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.TryGetComponent<PlayerHealth>(out var ph))
        {
            ph.Heal(healthSteal);
        }

        Destroy(gameObject, deathCooldown);
    }
    #endregion

    #region UI y util
    private void UpdateSlidersSafely()
    {
        if (lifeSlider != null)
        {
            lifeSlider.maxValue = Mathf.Max(1f, maxHealth);
            lifeSlider.value = Mathf.Clamp(currentHealth, 0f, maxHealth);
            if (!lifeSlider.gameObject.activeSelf) lifeSlider.gameObject.SetActive(true);
        }
        if (fillImage != null)
        {
            if (!fillImage.gameObject.activeSelf) fillImage.gameObject.SetActive(true);
            fillImage.color = Color.red;
        }
    }
    #endregion

    #region Reduccion local (expuesto para que la IA active la armadura)
    /// <summary>
    /// Legacy: aplica reducción sin source (compatibilidad).
    /// </summary>
    public void ApplyDamageReduction(float percent, float duration)
    {
        if (percent <= 0f || duration <= 0f) return;
        // En caso legacy, crear una "área implícita" temporal (null-source) usando la lógica nueva:
        ApplyDamageReductionFromArea(percent, duration, null);
    }

    /// <summary>
    /// Nuevo: aplica reducción proveniente de un prefab-area concreto (source).
    /// El efecto se mantiene mientras el source esté presente; también se quitará automáticamente tras 'duration' si no llega un Remove.
    /// </summary>
    public void ApplyDamageReductionFromArea(float percent, float duration, GameObject sourceArea)
    {
        if (percent <= 0f || duration <= 0f) return;

        // usar source==null para compatibilidad: generar una key interna
        GameObject key = sourceArea ?? this.gameObject; // si es null, usamos self como key temporal (legacy)
        percent = Mathf.Clamp01(percent);

        // cancelar timer anterior si existe
        if (areaTimers.TryGetValue(key, out Coroutine existing))
        {
            StopCoroutine(existing);
            areaTimers.Remove(key);
        }

        activeAreaReductions[key] = percent;
        RecalculateReduction();

        // iniciar timer de respaldo (por si el prefab no envía Remove)
        Coroutine timer = StartCoroutine(RemoveAreaAfterDelay(key, duration));
        areaTimers[key] = timer;
    }

    private IEnumerator RemoveAreaAfterDelay(GameObject key, float delay)
    {
        yield return new WaitForSeconds(delay);
        RemoveDamageReductionFromArea(key);
    }

    /// <summary>
    /// Remueve la reducción asociada a 'sourceArea' inmediatamente.
    /// </summary>
    public void RemoveDamageReductionFromArea(GameObject sourceArea)
    {
        GameObject key = sourceArea ?? this.gameObject;

        if (areaTimers.TryGetValue(key, out Coroutine c)) { StopCoroutine(c); areaTimers.Remove(key); }

        if (activeAreaReductions.ContainsKey(key))
        {
            activeAreaReductions.Remove(key);
            RecalculateReduction();
        }
    }

    private void RecalculateReduction()
    {
        float max = 0f;
        foreach (var v in activeAreaReductions.Values) max = Mathf.Max(max, v);
        _reduccionLocal = max;
    }
    #endregion
}
*/