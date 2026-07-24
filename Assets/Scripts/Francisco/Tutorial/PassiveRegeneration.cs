using UnityEngine;
using System.Collections;

public class PassiveRegeneration : MonoBehaviour
{
    [Header("Regeneration Settings")]
    [SerializeField] private float timeUntilRegenStarts = 5.0f;
    [SerializeField] private float healPerSecond = 2.0f;
    [SerializeField] private float regenTickInterval = 1.0f;

    private PlayerHealth playerHealth;

    private float damageTimer = 0.0f;
    private bool isRegenerating = false;
    private Coroutine regenCoroutine;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            Debug.LogError("PassiveRegeneration requiere un componente PlayerHealth en el mismo GameObject.");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDamageReceived += HandleDamageReceived;
            PlayerHealth.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDamageReceived -= HandleDamageReceived;
            PlayerHealth.OnHealthChanged -= HandleHealthChanged;
        }
        if (regenCoroutine != null)
        {
            StopCoroutine(regenCoroutine);
            regenCoroutine = null;
        }
    }

    private void Update()
    {
        if (isRegenerating || playerHealth == null) return;

        if (playerHealth.CurrentHealth >= playerHealth.MaxHealth)
        {
            damageTimer = 0.0f; 
            return;
        }

        damageTimer += Time.deltaTime;

        if (damageTimer >= timeUntilRegenStarts)
        {
            StartRegeneration();
        }
    }

    private void HandleDamageReceived(float damageAmount)
    {
        Debug.Log($"[PassiveRegeneration] Daño recibido. Reiniciando contador.");

        damageTimer = 0.0f;

        StopRegeneration(false);
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (currentHealth >= maxHealth && isRegenerating)
        {
            Debug.Log("[PassiveRegeneration] Vida máxima alcanzada. Deteniendo regeneración.");
            StopRegeneration(false);
        }
    }

    private void StartRegeneration()
    {
        if (isRegenerating || playerHealth.CurrentHealth >= playerHealth.MaxHealth)
        {
            isRegenerating = false; 
            return;
        }

        isRegenerating = true;
        regenCoroutine = StartCoroutine(RegenerationRoutine());

        Debug.Log($"[PassiveRegeneration] Tiempo cumplido. Iniciando regeneración de {healPerSecond} HP/s.");
    }

    private void StopRegeneration(bool logMessage = true)
    {
        if (!isRegenerating) return;

        isRegenerating = false;
        if (regenCoroutine != null)
        {
            StopCoroutine(regenCoroutine);
            regenCoroutine = null;
        }

        if (logMessage)
        {
            Debug.Log("[PassiveRegeneration] Regeneración detenida.");
        }
    }

    private IEnumerator RegenerationRoutine()
    {
        while (isRegenerating && playerHealth != null && playerHealth.CurrentHealth < playerHealth.MaxHealth)
        {
            float healAmount = healPerSecond * regenTickInterval;

            playerHealth.Heal(healAmount);

            yield return new WaitForSeconds(regenTickInterval);
        }

        StopRegeneration(false);
    }
}