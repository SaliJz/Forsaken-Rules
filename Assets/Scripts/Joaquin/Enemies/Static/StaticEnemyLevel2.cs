using UnityEngine;
using System.Collections;

public class StaticEnemyLevel2 : StaticEnemyBase, IAnimEventHandler
{
    [Header("QuickSheet Balance")]
    [SerializeField] private Enemies enemiesSheet;
    [SerializeField] private int ENEMY_ID = 5;

    [Header("Stats - Level 2")]
    [SerializeField] private float projectileDamage = 7;
    [SerializeField] private float mineDamage = 15;

    protected override void Awake()
    {
        //LoadStatsFromSheet();
        base.Awake();
    }

    private void LoadStatsFromSheet()
    {
        if (enemiesSheet == null) return;

        foreach (var row in enemiesSheet.dataArray)
        {
            if (row.ID != ENEMY_ID) continue;

            health = row.Health;
            moveSpeed = row.Movespeed;
            projectileDamage = row.Regulardamage;
            mineDamage = row.Minedamage;

            EnemyToughness toughnessComp = GetComponent<EnemyToughness>();
            if (toughnessComp != null)
            {
                if (row.Superarmor > 0)
                {
                    toughnessComp.SetUseToughness(true);
                    toughnessComp.SetMaxToughness(row.Superarmor);
                }
                else toughnessComp.SetUseToughness(false);
            }

            if (row.Attackfrequency > 0) fireRate = 1f / row.Attackfrequency;
        }
    }

    protected override IEnumerator ShootAfterDelayRoutine()
    {
        if (useRandomFireRate) fireRate = Random.Range(minFireRate, maxFireRate);

        yield return new WaitForSeconds(fireRate);
        //yield return new WaitForSeconds(animTeleportDelay);

        if (!isDead && currentState != StaticState.Patrol && currentState != StaticState.Repositioning)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer <= attackRange)
            {
                ForceFacePlayer();

                isAttacking = true;
                if (animCtrl != null) animCtrl.PlayShoot();
                //ExecuteProjectileSpawn();

                float safetyTimeout = 2.0f;
                while (isAttacking && safetyTimeout > 0 && !isDead && !isInHitStun)
                {
                    safetyTimeout -= Time.deltaTime;
                    yield return null;
                }

                isAttacking = false;
            }
        }

        shootCoroutine = null;
    }

    public void HandleAnimEvents(string eventName)
    {
        if (eventName == "AnimEvent_Shoot") ExecuteProjectileSpawn();
        if (eventName == "AnimEvent_AnticipationPause") StartAnticipationPause();
    }

    protected override void InstantiateAndInitializeProjectile()
    {
        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        StaticProjectileLevel2 projectile = projectileObj.GetComponent<StaticProjectileLevel2>();

        if (projectile != null)
        {
            string selectedWord = wordLibrary != null ? wordLibrary.GetRandomWord() : "TRAP";
            projectile.InitializeLevel2(projectileSpeed, projectileDamage, selectedWord, mineDamage);
        }
    }
}