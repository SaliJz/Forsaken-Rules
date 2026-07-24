//using UnityEngine;

using UnityEngine;

public static class CriticalHitSystem
{
    private static PlayerStatsManager cachedStatsManager;

    /// <summary>
    /// Calcula el daño basado en la posición relativa del atacante y el objetivo.
    /// </summary>
    /// <param name="baseDamage">Daño base del ataque.</param>
    /// <param name="attacker">El Transform de quien ataca.</param>
    /// <param name="target">El Transform de quien recibe el ataque.</param>
    /// <param name="isCritical">Devuelve true si el golpe fue crítico.</param>
    /// <returns>El daño final calculado.</returns>
    public static float CalculateDamage(float baseDamage, Transform attacker, Transform target, out bool isCritical)
    {
        isCritical = false;

        // Obtener el StatsManager si no está cacheado
        if (cachedStatsManager == null && attacker != null)
        {
            cachedStatsManager = attacker.GetComponent<PlayerStatsManager>();
        }

        if (cachedStatsManager == null)
        {
            ReportDebug("No se encontró PlayerStatsManager. Usando valores por defecto.", 2);
            return baseDamage;
        }

        // Obtener probabilidad de crítico desde stats
        float critChance = cachedStatsManager.GetStat(StatType.CriticalChance);

        // Roll de crítico
        float roll = Random.Range(0f, 100f);

        if (roll <= critChance)
        {
            isCritical = true;

            // Obtener multiplicador de daño crítico desde stats (por defecto 2.0)
            float critMultiplier = cachedStatsManager.GetStat(StatType.CriticalDamageMultiplier);

            // Si el stat no está inicializado, usar 2.0 por defecto
            if (critMultiplier <= 0f)
            {
                critMultiplier = 2.0f;
            }

            float criticalDamage = baseDamage * critMultiplier;

            ReportDebug($"¡CRÍTICO! Daño: {baseDamage} x {critMultiplier} = {criticalDamage}", 1);

            return criticalDamage;
        }

        return baseDamage;
    }

    public static void ClearCache()
    {
        cachedStatsManager = null;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Función de depuración para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <<param name="message">Mensaje a reportar.</param> >
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[CriticalHitSystem] {message}");
                break;
            case 2:
                Debug.LogWarning($"[CriticalHitSystem] {message}");
                break;
            case 3:
                Debug.LogError($"[CriticalHitSystem] {message}");
                break;
            default:
                Debug.Log($"[CriticalHitSystem] {message}");
                break;
        }
    }
}