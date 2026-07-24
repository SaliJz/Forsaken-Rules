using UnityEngine;
using System.Collections;

public class MeleeDummy : DummyTarget
{
    [Header("MELEE DUMMY CONFIGURATION")]
    [SerializeField] private int requiredMeleeHits = 3;
    private int currentMeleeHits = 0;
    private bool isDefeated = false;

    public override float CurrentHealth => isDefeated ? 0f : MaxHealth;

    public void HitByMelee()
    {
        if (isDefeated || !canBeHit) return;

        StartCoroutine(HitCooldown());
        if (colorChangeCoroutine != null) StopCoroutine(colorChangeCoroutine);
        colorChangeCoroutine = StartCoroutine(FlashColor());
        canBeHit = false;

        currentMeleeHits++;
        ShowNextLineCyclic(); 

        Debug.Log($"Melee Dummy hit! Hits: {currentMeleeHits}/{requiredMeleeHits}.");

        if (currentMeleeHits >= requiredMeleeHits)
        {
            DefeatDummy("Derrotado por 3 golpes cuerpo a cuerpo.");
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
        Debug.Log($"Melee Dummy ignorando daño genérico: {damageAmount}");
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