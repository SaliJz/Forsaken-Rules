using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct StatModifier
{
    public StatType type;
    public float amount;
    public bool isPercentage;
}

[CreateAssetMenu(fileName = "New Pact", menuName = "Shop/Pact")]
public class Pact : ScriptableObject
{
    [Header("Pact Info")]
    public string pactName;
    [TextArea] public string description;

    [Header("Stats")]
    public int lifeRecoveryAmount;
    public List<StatModifier> drawbacks = new List<StatModifier>();

    [Header("Desventajas de Inventario")]
    public bool removeRandomRelic = false;
}