using UnityEngine;

[CreateAssetMenu(fileName = "EntropyConfig", menuName = "Combat/Entropy Config")]
public class EntropyConfig : ScriptableObject
{
    [Header("Configuración Global")]
    public float damagePercent = 5f;
    public float tickInterval = 0.2f;
    public float chargeDuration = 1f;
}