using UnityEngine;

public interface ICombatDummyUI
{
    void UpdateHealthBar(float currentHealthRatio, Color? stateColor);
    void UpdateHitCounter(int currentHits, int requiredHits);
    void SetUIActive(DummyLogicType logicType, bool active);
}