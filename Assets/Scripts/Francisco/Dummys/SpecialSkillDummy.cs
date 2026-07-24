using UnityEngine;
using static PlayerHealth; 

public class SpecialSkillDummy : DummyTarget
{
    private bool isDefeated = false;

    private bool ageConditionMet = false;

    private PlayerHealth health;

    public override float CurrentHealth => isDefeated ? 0f : MaxHealth;

    protected override void Awake()
    {
        base.Awake();

        OnLifeStageChanged += DefeatCheck;

        health = FindAnyObjectByType<PlayerHealth>();

        Debug.Log("[SpecialSkillDummy] Suscrito al evento estático de cambio de etapa de vida.");
    }

    public void DefeatCheck(LifeStage newStage)
    {
        if (isDefeated) return;

        if (newStage == LifeStage.Adult || newStage == LifeStage.Elder)
        {
            if (!ageConditionMet)
            {
                ageConditionMet = true;
                Debug.Log($"[SpecialSkillDummy] Condición de edad cumplida: Jugador es {newStage}. Esperando golpe final.");
            }
        }
        else if (newStage == LifeStage.Young)
        {
            ageConditionMet = false;
            Debug.Log($"[SpecialSkillDummy] Condición de edad reiniciada: Jugador es Young.");
        }
    }

    public override void TakeDamage(float damageAmount, bool isCritical = false, AttackDamageType attackDamageType = AttackDamageType.Melee)
    {
        if (isDefeated) return;

        if (ageConditionMet)
        {
            DefeatDummy($"Derrotado: El jugador cambió a la etapa de vida requerida y asestó el golpe final.");
            return; 
        }

        if (!canBeHit) return;

        canBeHit = false;
        StartCoroutine(HitCooldown());
        if (colorChangeCoroutine != null) StopCoroutine(colorChangeCoroutine);
        colorChangeCoroutine = StartCoroutine(FlashColor());

        ShowNextLineCyclic();
        Debug.Log($"Special Skill Dummy ignorando daño: {damageAmount}. Condición de edad: Pendiente. (Actual: {health.CurrentLifeStage})");
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
        if (logPanel != null) logPanel.SetActive(true);

        gameObject.SetActive(false);

        Debug.Log($"Dummy Defeated: {message}");
    }

    protected override void OnDestroy()
    {
        OnLifeStageChanged -= DefeatCheck;

        base.OnDestroy();
    }
}