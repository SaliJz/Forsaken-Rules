using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System;

public class DummyArmor : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Super Armor Settings")]
    [SerializeField] private float maxArmorHealth = 50f;
    private float currentArmorHealth;
    [SerializeField, Range(0f, 1f)] private float reductionPercentage = 0.5f;

    [Header("Shield Specific Settings")]
    [SerializeField, Range(0f, 1f)] private float rangedDamageMultiplierToHealth = 0.05f;

    [Header("Damage Cooldown")]
    [SerializeField] private float damageCooldownTime = 0.2f;
    private float nextDamageTime = 0f;

    [Header("References")]
    [SerializeField] private DummyUIController uiController;
    [SerializeField] private float maxArmorValueForUI = 1.0f;
    [SerializeField] private Animator animator;
    [SerializeField] private string hitTriggerName = "OnHit";

    [Header("Rotation Logic")]
    [SerializeField] private Transform transformToRotate;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float rotationYOffset = 0f;
    private Transform playerTransform;
    private Coroutine rotationCoroutine;

    [Header("Events")]
    public UnityEvent OnDummyDefeated;

    public event Action<DamageType, bool> OnHitByPlayer;

    private AttackDamageType lastAttackDamageType;

    // Nuevas variables para el audio - Asegurado como SerializeField para el Inspector
    [Header("Audio")]
    [SerializeField] private AudioClip damageSound;
    private AudioSource audioSource;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float CurrentArmorHealth => currentArmorHealth;
    public float CurrentReduction => currentArmorHealth > 0 ? reductionPercentage : 0f;
    public AttackDamageType LastAttackDamageType => lastAttackDamageType;

    void Awake()
    {
        currentHealth = maxHealth;
        currentArmorHealth = maxArmorHealth;

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (transformToRotate == null)
        {
            transformToRotate = transform;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        // Inicializar AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        }

        UpdateHealthUI();
        UpdateArmorUI();
    }

    public void TakeDamage(float damageAmount, bool isCritical, AttackDamageType damageType)
    {
        if (Time.time < nextDamageTime) return;
        if (currentHealth <= 0) return;

        nextDamageTime = Time.time + damageCooldownTime;
        lastAttackDamageType = damageType;

        if (playerTransform != null)
        {
            if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
            rotationCoroutine = StartCoroutine(RotateTowardsPlayer());
        }

        float damageToArmor = 0f;
        float damageToHealth = 0f;
        DamageType hitType = DamageType.Melee;

        if (damageType == AttackDamageType.Ranged)
        {
            float reductionFactor = (1f - reductionPercentage);
            damageToArmor = damageAmount * reductionFactor;
        }
        else
        {
            damageToArmor = damageAmount; 
        }

        if (currentArmorHealth > 0f)
        {
            hitType = DamageType.Shield;

            if (damageToArmor <= currentArmorHealth)
            {
                currentArmorHealth -= damageToArmor;
                damageToHealth = 0f;
            }
            else
            {
                float excessDamage = damageToArmor - currentArmorHealth;
                currentArmorHealth = 0f;

                if (damageType == AttackDamageType.Ranged)
                {
                    damageToHealth = excessDamage * rangedDamageMultiplierToHealth;
                }
                else
                {
                    damageToHealth = excessDamage;
                }
            }
        }
        else
        {
            if (damageType == AttackDamageType.Ranged)
            {
                hitType = DamageType.Shield; 
                damageToHealth = damageAmount * rangedDamageMultiplierToHealth;
            }
            else
            {
                hitType = DamageType.Melee;
                damageToHealth = damageAmount;
            }
        }

        if (animator != null)
        {
            animator.SetTrigger(hitTriggerName);
        }

        float previousHealth = currentHealth; // Guardar la vida antes de aplicar el daño
        currentHealth -= damageToHealth;
        currentHealth = Mathf.Max(0, currentHealth);

        // Reproducir sonido de daño si la vida ha disminuido
        if (currentHealth < previousHealth && audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }

        Debug.Log($"[DUMMY] Golpe ({damageType}). Daño aplicado a vida: {damageToHealth:F2}. Vida restante: {currentHealth}/{maxHealth}");

        bool isFatal = currentHealth <= 0;
        OnHitByPlayer?.Invoke(hitType, isFatal);

        UpdateHealthUI();
        UpdateArmorUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator RotateTowardsPlayer()
    {
        if (transformToRotate == null || playerTransform == null) yield break;

        Vector3 direction = (playerTransform.position - transformToRotate.position).normalized;
        direction.y = 0;

        if (direction == Vector3.zero) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        targetRotation *= Quaternion.Euler(0, rotationYOffset, 0);

        float elapsedTime = 0f;
        float maxRotationTime = 2f;

        while (Quaternion.Angle(transformToRotate.rotation, targetRotation) > 0.1f && elapsedTime < maxRotationTime)
        {
            transformToRotate.rotation = Quaternion.Slerp(
                transformToRotate.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transformToRotate.rotation = targetRotation;
        rotationCoroutine = null;
    }

    private void Die()
    {
        Debug.Log("[DUMMY H&A] Dummy Defeated!");

        OnDummyDefeated.Invoke();

        if (uiController != null) uiController.SetUIActive(DummyLogicType.HealthState, false);
        gameObject.SetActive(false);
    }

    private void UpdateHealthUI()
    {
        if (uiController != null)
        {
            float healthRatio = currentHealth / maxHealth;
            uiController.UpdateHealthBar(healthRatio, null);
        }
    }

    private void UpdateArmorUI()
    {
        if (uiController != null)
        {
            float armorRatio = maxArmorHealth > 0 ? currentArmorHealth / maxArmorHealth : 0f;

            float uiValue = armorRatio * maxArmorValueForUI;

            uiController.UpdateArmorBar(uiValue);
        }
    }

    public void ResetArmorState()
    {
        currentHealth = maxHealth;
        currentArmorHealth = maxArmorHealth;
        lastAttackDamageType = AttackDamageType.Ranged;

        gameObject.SetActive(true);
        if (uiController != null) uiController.SetUIActive(DummyLogicType.HealthState, true);

        UpdateHealthUI();
        UpdateArmorUI();
        Debug.Log("[DUMMY H&A] Dummy revivido!");
    }
}
