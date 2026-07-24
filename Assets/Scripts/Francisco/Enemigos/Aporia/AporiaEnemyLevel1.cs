using UnityEngine;

public class AporiaEnemyLevel1 : AporiaEnemyBase
{
    #region Inspector - QuickSheet Balance

    // Nota : Siempre supe que serviria de algo hacer script hijo para cada variante, tomala Senor J
    [Header("QuickSheet Balance")]
    [SerializeField] private Enemies enemiesSheet;
    [SerializeField] private int ENEMY_ID = 3;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        //LoadStatsFromSheet();
        base.Awake();
    }

    #endregion

    #region Initialization & Data Sync

    private void LoadStatsFromSheet()
    {
        if (enemiesSheet == null)
        {
            Debug.LogWarning($"[AporiaEnemyLevel1] No hay Enemies asset asignado en {name}. Usando valores del Inspector.");
            return;
        }

        foreach (var row in enemiesSheet.dataArray)
        {
            if (row.ID != ENEMY_ID) continue;

            health = row.Health;
            moveSpeed = row.Movespeed;
            attackDamage = row.Regulardamage;

            EnemyToughness toughnessComp = GetComponent<EnemyToughness>();
            if (toughnessComp != null)
            {
                if (row.Superarmor > 0)
                {
                    toughnessComp.SetUseToughness(true);
                    toughnessComp.SetMaxToughness(row.Superarmor);
                    Debug.Log($"[AporiaEnemyLevel1] SuperArmor activado: {row.Superarmor}");
                }
                else
                {
                    toughnessComp.SetUseToughness(false);
                    Debug.Log($"[AporiaEnemyLevel1] SuperArmor desactivado (valor era 0).");
                }
            }

            if (row.Attackfrequency > 0f)
            {
                float interval = 1f / row.Attackfrequency;
                cooldownShort = interval * 0.75f;
                cooldownMedium = interval;
                cooldownLong = interval * 1.5f;
            }

            Debug.Log($"[AporiaEnemyLevel1] Stats cargados desde sheet: HP={health} Speed={moveSpeed} Dmg={attackDamage}");
            return;
        }

        Debug.LogWarning($"[AporiaEnemyLevel1] ID {ENEMY_ID} no encontrado en el sheet.");
    }

    #endregion
}