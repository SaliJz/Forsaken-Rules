using UnityEngine;

public class ShieldDummy : DummyTarget
{
    [Header("SHIELD DUMMY CONFIGURATION")]
    [SerializeField] private int requiredShieldHits = 3;
    private int currentShieldHits = 0;
    private bool isDefeated = false;

    public override float CurrentHealth => isDefeated ? 0f : MaxHealth;

    public void HitByShield()
    {
        if (isDefeated || !canBeHit) return;

        StartCoroutine(HitCooldown());
        if (colorChangeCoroutine != null) StopCoroutine(colorChangeCoroutine);
        colorChangeCoroutine = StartCoroutine(FlashColor());
        canBeHit = false;

        currentShieldHits++;
        ShowNextLineCyclic();

        Debug.Log($"Shield Dummy hit! Hits: {currentShieldHits}/{requiredShieldHits}.");

        if (currentShieldHits >= requiredShieldHits)
        {
            DefeatDummy("Derrotado por 3 golpes de escudo.");
        }
    }

    public override void TakeDamage(float damageAmount, bool isCritical = false, AttackDamageType attackDamageType = AttackDamageType.Melee)
    {
        if (isDefeated) return;

        if (!canBeHit) return;

        StartCoroutine(HitCooldown());
        if (colorChangeCoroutine != null) StopCoroutine(colorChangeCoroutine);
        colorChangeCoroutine = StartCoroutine(FlashColor());
        canBeHit = false;

        ShowNextLineCyclic();
        Debug.Log($"Shield Dummy ignorando daño genérico: {damageAmount}");
    }

    private void DefeatDummy(string message)
    {
        isDefeated = true;
        StopAllCoroutines();
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        Renderer rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = false;

        if (logText != null) logText.text = message;
        if (logPanel != null) logPanel.SetActive(false);

        gameObject.SetActive(false);

        Debug.Log($"Dummy Defeated: {message}");
    }
}