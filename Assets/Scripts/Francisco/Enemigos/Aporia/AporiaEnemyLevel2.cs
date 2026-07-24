using System.Collections;
using UnityEngine;

public class AporiaEnemyLevel2 : AporiaEnemyBase
{
    #region Inspector - Fragmentos De Vidrio Ataque

    [Header("Fragmentos de Vidrio - Ataque")]
    [SerializeField] private GameObject glassShardAttackPrefab;
    [SerializeField] private float shardAttackRadius = 1f;
    [SerializeField] private float shardAttackDamagePerSec = 4f;
    [SerializeField] private float shardAttackDuration = 3f;

    #endregion

    #region Inspector - Estela De Vidrio Dash

    [Header("Estela de Vidrio - Dash")]
    [SerializeField] private GameObject glassShardDashPrefab;
    [SerializeField] private float shardDashDamagePerSec = 4f;
    [SerializeField] private float shardDashDuration = 1f;
    [SerializeField] private float shardDashSpacing = 1f;

    #endregion

    #region Inspector - Muerte Explosion

    [Header("Muerte - Explosion")]
    [SerializeField] private float deathWarningDuration = 1.5f;
    [SerializeField] private float deathExplosionRadius = 3f;
    [SerializeField] private float deathExplosionDamage = 15f;
    [SerializeField] private float deathExplosionKnockback = 10f;
    [SerializeField] private GameObject glassShardDeathPrefab;
    [SerializeField] private float shardDeathRadius = 1f;
    [SerializeField] private float shardDeathDamagePerSec = 4f;
    [SerializeField] private float shardDeathDuration = 2f;
    [SerializeField] private AudioClip explosionSFX;

    #endregion

    #region Inspector - QuickSheet Balance

    [Header("QuickSheet Balance")]
    [SerializeField] private Enemies enemiesSheet;
    [SerializeField] private int ENEMY_ID = 7;

    #endregion

    #region Internal State

    private Coroutine deathCoroutine;

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
                }
                else
                {
                    toughnessComp.SetUseToughness(false);
                }
            }

            shardAttackDamagePerSec = row.Minedamage;
            shardDashDamagePerSec = row.Dashtraildamage;
            deathExplosionDamage = row.Explosionareadamage;

            if (row.Attackfrequency > 0f)
            {
                float interval = 1f / row.Attackfrequency;
                cooldownShort = interval * 0.75f;
                cooldownMedium = interval;
                cooldownLong = interval * 1.5f;
            }

            Debug.Log($"[AporiaEnemyLevel2] ID {ENEMY_ID} cargado. SA: {row.Superarmor}, Mina: {row.Minedamage}");
            return;
        }

        Debug.LogWarning($"[AporiaEnemyLevel1] ID {ENEMY_ID} no encontrado en el sheet.");
    }

    #endregion

    #region Core Health & Combat

    public override void OnAttackHit()
    {
        if (enemyHealth != null && (enemyHealth.IsStunned || enemyHealth.IsDead)) return;

        SpawnAttackVFX();

        if (audioSource && attackSFX) audioSource.PlayOneShot(attackSFX);

        Collider[] targets = Physics.OverlapSphere(hitPoint.position, attackRadius, playerLayer);
        foreach (var t in targets)
        {
            if (t.TryGetComponent<PlayerHealth>(out var pHealth))
            {
                pHealth.TakeDamage(attackDamage);
                ApplyKnockback(t.transform, knockbackForce);
                if(pHealth.CurrentHealth > 0)
                {
                    SpawnGlassArea(glassShardAttackPrefab, t.transform.position, shardAttackRadius,
                               shardAttackDamagePerSec, shardAttackDuration);
                }
            }
        }
    }

    protected override IEnumerator PerformDash(Vector3 startPos, Vector3 dashEnd, Vector3 attackDirection)
    {
        float elapsed = 0;
        float distanceCovered = 0;
        Vector3 lastShardPos = startPos;

        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            Vector3 nextPos = Vector3.Lerp(startPos, dashEnd, elapsed / dashDuration);

            if (Physics.Raycast(nextPos + Vector3.up, Vector3.down, 2f, groundLayer))
            {
                transform.position = nextPos;
            }

            if (agent != null && agent.enabled) agent.nextPosition = transform.position;

            distanceCovered += Vector3.Distance(transform.position, lastShardPos);
            if (distanceCovered >= shardDashSpacing)
            {
                SpawnGlassShard(glassShardDashPrefab, transform.position,
                                shardDashDamagePerSec, shardDashDuration);
                lastShardPos = transform.position;
                distanceCovered = 0;
            }

            if (Vector3.Distance(transform.position, playerTransform.position) < 1.2f)
            {
                break;
            }

            yield return null;
        }
    }

    #endregion

    #region Death & Destruction

    protected override void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        CancelAnticipation();

        if (hitStunCoroutine != null)
        {
            StopCoroutine(hitStunCoroutine);
            hitStunCoroutine = null;
        }

        if (attackSequenceCoroutine != null)
        {
            StopCoroutine(attackSequenceCoroutine);
            attackSequenceCoroutine = null;
        }

        isAttacking = false;
        deathCoroutine = StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        if (agent != null) agent.enabled = false;
        isAttacking = true;

        if (animCtrl) animCtrl.SendMessage("PlayDeathWarning", SendMessageOptions.DontRequireReceiver);
        yield return new WaitForSeconds(deathWarningDuration);

        if (audioSource && explosionSFX) audioSource.PlayOneShot(explosionSFX);
        else if (audioSource && deathSFX) audioSource.PlayOneShot(deathSFX);

        TriggerExplosion();

        if (animCtrl) animCtrl.PlayDeath();

        if (glassShardDeathPrefab == null)
        {
            Debug.LogWarning($"[{GetType().Name}] '{name}': glassShardDeathPrefab no está asignado. No se generarán vidrios de muerte.");
        }
        else
        {
            GameObject shard = Instantiate(glassShardDeathPrefab, transform.position, Quaternion.identity);
            shard.transform.localScale = Vector3.one * (shardDeathRadius * 2f);

            if (shard.TryGetComponent<GlassShardDamage>(out var dmg))
            {
                dmg.damagePerSecond = shardDeathDamagePerSec;
                dmg.playerLayer = playerLayer;
                dmg.shardDeathDuration = shardDeathDuration;
            }
        }

        this.enabled = false;
    }

    private void TriggerExplosion()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, deathExplosionRadius, playerLayer);
        foreach (var t in targets)
        {
            if (t.TryGetComponent<PlayerHealth>(out var pHealth))
            {
                pHealth.TakeDamage(deathExplosionDamage);
                ApplyKnockback(t.transform, deathExplosionKnockback);
            }
        }
    }

    #endregion

    #region Helpers

    private void SpawnGlassArea(GameObject prefab, Vector3 position, float radius,
                                 float damagePerSec, float duration)
    {
        if (prefab == null) return;
        GameObject shard = Instantiate(prefab, position, Quaternion.identity);
        shard.transform.localScale = Vector3.one * (radius * 2f);

        if (shard.TryGetComponent<GlassShardDamage>(out var dmg))
        {
            dmg.damagePerSecond = damagePerSec;
            dmg.playerLayer = playerLayer;
        }
        else
        {
            Debug.LogWarning($"[{GetType().Name}] '{name}': el prefab '{prefab.name}' no tiene componente GlassShardDamage. El área de vidrio no hará daño.");
        }
    }

    private void SpawnGlassShard(GameObject prefab, Vector3 position,
                                  float damagePerSec, float duration)
    {
        if (prefab == null) return;
        GameObject shard = Instantiate(prefab, position, Quaternion.identity);

        if (shard.TryGetComponent<GlassShardDamage>(out var dmg))
        {
            dmg.damagePerSecond = damagePerSec;
            dmg.playerLayer = playerLayer;
            dmg.shardDeathDuration = duration;
        }
        else
        {
            Debug.LogWarning($"[{GetType().Name}] '{name}': el prefab '{prefab.name}' no tiene componente GlassShardDamage. El shard de estela no hará daño.");
        }
    }

    #endregion
}