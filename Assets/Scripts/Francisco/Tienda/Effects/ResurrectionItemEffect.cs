using UnityEngine;

[CreateAssetMenu(fileName = "ResurrectionEffect", menuName = "Item Effects/Utility/Resurrection")]
public class ResurrectionItemEffect : ItemEffectBase
{
    [Header("Configuración de Resurrección")]
    public float resurrectionChance = 0.20f;
    public bool hasPriority = true;

    [Header("Larvas Explosivas")]
    public GameObject explosiveLarvaPrefab;
    public int totalLarvasToInstantiate = 3;

    [Header("Larva Curativa")]
    public GameObject curativeLarvaPrefab;
    public float curativeLarvaChance = 0.20f;

    private void OnEnable()
    {
        EffectID = "Resurrección de Larvas";
        category = EffectCategory.Utility;
        if (string.IsNullOrEmpty(effectDescription))
        {
            effectDescription = "Los enemigos eliminados tienen probabilidad de generar larvas.";
        }
    }

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnEnemyKilled += HandleEnemyResurrection;
        Debug.Log($"[ResurrectionEffect] Aplicado. Probabilidad de resurrección: {resurrectionChance * 100}%.");
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnEnemyKilled -= HandleEnemyResurrection;
        Debug.Log("[ResurrectionEffect] Removido.");
    }

    public override string GetFormattedDescription()
    {
        return $"Los enemigos eliminados tienen un {resurrectionChance * 100:F0}% de probabilidad de generar larvas explosivas y curativas.";
    }

    private void HandleEnemyResurrection(GameObject killedEnemy, float enemyBaseHealth)
    {
        if (Random.value >= resurrectionChance) return;

        Debug.Log($"[ResurrectionEffect] Evento de resurrección activado para {killedEnemy.name}.");

        bool shouldInstantiateCurative = Random.value < curativeLarvaChance;

        int curativeCount = shouldInstantiateCurative ? 1 : 0;
        int explosiveCount = totalLarvasToInstantiate - curativeCount;

        if (explosiveLarvaPrefab == null)
        {
            Debug.LogError("[ResurrectionEffect] explosiveLarvaPrefab es nulo.");
            return;
        }

        if (curativeCount > 0 && curativeLarvaPrefab != null)
        {
            InstantiateLarva(curativeLarvaPrefab, killedEnemy.transform.position, enemyBaseHealth, true);
        }
        else if (curativeLarvaPrefab == null && curativeCount > 0)
        {
            explosiveCount++;
        }

        for (int i = 0; i < explosiveCount; i++)
        {
            InstantiateLarva(explosiveLarvaPrefab, killedEnemy.transform.position, enemyBaseHealth, false);
        }
    }

    private void InstantiateLarva(GameObject prefab, Vector3 position, float enemyBaseHealth, bool isCurative)
    {
        GameObject larva = Instantiate(prefab, position, Quaternion.identity);
        larva.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        if (isCurative)
        {
            if (larva.TryGetComponent<CurativeLarva>(out var curativeScript))
                curativeScript.Initialize(enemyBaseHealth);
        }
        else
        {
            if (larva.TryGetComponent<ResurrectedLarva>(out var explosiveScript))
                explosiveScript.Initialize(enemyBaseHealth);
        }

        Debug.Log($"[ResurrectionEffect] Instanciada larva: {(isCurative ? "CURATIVA" : "EXPLOSIVA")}");
    }
}