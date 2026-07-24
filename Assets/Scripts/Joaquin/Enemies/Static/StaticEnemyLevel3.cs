using UnityEngine;
using System.Collections;

public class StaticEnemyLevel3 : StaticEnemyBase, IAnimEventHandler
{
    #region Inspector - Reactive Evasion

    [Header("Static Level 3 - Reactive Evasion")]
    [SerializeField] private float evasionCooldown = 6f;
    [SerializeField] private float evasionTeleportDistance = 8f;

    #endregion

    #region Inspector - Stress Residue

    [Header("Static Level 3 - Stress Residue")]
    [SerializeField] private GameObject stressResiduePrefab;
    [SerializeField] private float residueDuration = 4f;
    [SerializeField] private float residueDPS = 2f;
    [SerializeField] private float residueRadius = 2f;

    #endregion

    #region Inspector - VFX Screen

    [Header("Static Level 3 - VFX Screen")]
    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private Color evasionReadyBorderColor = Color.red;
    [SerializeField] private Color evasionCooldownBorderColor = Color.gray;

    #endregion

    #region Inspector - SFX

    [Header("Static Level 3 - SFX")]
    [SerializeField] private AudioClip evasionSFX;
    [SerializeField] private AudioClip retaliatoryShootSFX;

    #endregion

    #region Inspector - QuickSheet Balance

    [Header("QuickSheet Balance")]
    [SerializeField] private Enemies enemiesSheet;
    [SerializeField] private int ENEMY_ID = 9;

    [Header("Stats - Level 3")]
    [SerializeField] private float projectileDamage = 13;
    //[SerializeField] private float swarmDPS;

    #endregion

    #region Internal State

    private static readonly int shaderBorderColorId = Shader.PropertyToID("_BorderColor");
    private static readonly int shaderShowIconId = Shader.PropertyToID("_ShowIcon");
    private static readonly int shaderIconTypeId = Shader.PropertyToID("_IconType");

    private const int iconEye = 0;
    private const int iconNoSignal = 1;

    private bool evasionReady = true;
    private bool evasionOnCooldown = false;
    private Coroutine evasionCooldownRoutine;
    private Coroutine screenFeedbackRoutine;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        //LoadStatsFromSheet();
        base.Awake();
    }

    #endregion

    #region Initialization & Data Sync

    protected override void InitializedEnemy()
    {
        base.InitializedEnemy();
        UpdateEvasionFeedback();
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
            // swarmDPS = row.Explosionareadamage; // No hay valor en la tabla, Jamil

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

            // residueDPS = row.Explosionareadamage; // No hay valor en la tabla, Jamil

            Debug.Log($"[StaticLevel3] Cargado ID {ENEMY_ID}. SA: {row.Superarmor}");
            return;
        }
    }

    #endregion

    #region Core Health & Combat

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

    protected override void OnBeforeTeleport(Vector3 fromPosition)
    {
        if (stressResiduePrefab == null) return;

        GameObject residue = Instantiate(stressResiduePrefab, fromPosition, Quaternion.identity);
        residue.GetComponent<StaticStressResidue>()?.Initialize(residueDuration, residueDPS, residueRadius);
    }

    protected override void HandleDamageTaken()
    {
        base.HandleDamageTaken();

        if (isDead || !evasionReady || evasionOnCooldown || isInHitStun) return;

        if (enemyHealth != null && enemyHealth.LastDamageType == AttackDamageType.Melee)
        {
            StartCoroutine(ReactiveEvasionRoutine());
        }
    }

    private IEnumerator ReactiveEvasionRoutine()
    {
        evasionReady = false;
        evasionOnCooldown = true;

        if (audioSource != null && evasionSFX != null)
        {
            audioSource.PlayOneShot(evasionSFX);
        }

        if (currentBehaviorCoroutine != null)
        {
            StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }

        while (shootCoroutine != null || isInAnticipation) yield return null;

        if (playerTransform == null)
        {
            ReportDebug("playerTransform es null en ReactiveEvasionRoutine. Abortando evasion.", 3);
            evasionOnCooldown = false;
            evasionReady = true;
            yield break;
        }

        Vector3 escapeDir = (transform.position - playerTransform.position).normalized;
        Vector3 evasionTarget = transform.position + escapeDir * evasionTeleportDistance;

        yield return StartCoroutine(TeleportToPositionRoutine(evasionTarget));

        yield return new WaitForSeconds(0.1f);
        if (!isDead)
        {
            ForceFacePlayer();
            if (audioSource != null && retaliatoryShootSFX != null)
            {
                audioSource.PlayOneShot(retaliatoryShootSFX);
            }
            if (animCtrl != null) animCtrl.PlayShoot();
            //ExecuteProjectileSpawn();
        }

        ChangeState(currentState);

        if (evasionCooldownRoutine != null) StopCoroutine(evasionCooldownRoutine);
        evasionCooldownRoutine = StartCoroutine(EvasionCooldownRoutine());
    }

    protected override float CalculateDamageByDistance(float distance)
    {
        maxDistanceForDamageIncrease = projectileDamage;
        maxDistanceForDamageStart = projectileDamage - 7;

        if (distance <= maxDistanceForDamageIncrease) return maxDamageIncrease;
        if (distance >= maxDistanceForDamageStart) return minDamageIncrease;

        if (Mathf.Approximately(maxDistanceForDamageStart, maxDistanceForDamageIncrease))
        {
            ReportDebug("maxDistanceForDamageStart y maxDistanceForDamageIncrease son iguales. Evitando division por cero.", 2);
            return minDamageIncrease;
        }

        float t = (distance - maxDistanceForDamageIncrease) / (maxDistanceForDamageStart - maxDistanceForDamageIncrease);
        return Mathf.Lerp(maxDamageIncrease, minDamageIncrease, t);
    }

    protected override void InstantiateAndInitializeProjectile()
    {
        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        StaticProjectileBase projectile = projectileObj.GetComponent<StaticProjectileBase>();

        if (projectile != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            float calculatedDamage = CalculateDamageByDistance(distanceToPlayer);
            string selectedWord = wordLibrary != null ? wordLibrary.GetRandomWord() : "GLITCH";

            projectile.Initialize(projectileSpeed, calculatedDamage, selectedWord); // Faltaria agregar una variable para pasarle dato de dano del prefab de explosion
        }
    }

    public void HandleAnimEvents(string eventName)
    {
        if (eventName == "AnimEvent_Shoot") ExecuteProjectileSpawn();
        if (eventName == "AnimEvent_AnticipationPause") StartAnticipationPause();
    }

    #endregion

    #region Status Effects & Feedback

    private IEnumerator EvasionCooldownRoutine()
    {
        UpdateEvasionFeedback();
        yield return new WaitForSeconds(evasionCooldown);
        evasionOnCooldown = false;
        evasionReady = true;
        UpdateEvasionFeedback();
        evasionCooldownRoutine = null;
    }

    private void UpdateEvasionFeedback()
    {
        if (screenFeedbackRoutine != null) StopCoroutine(screenFeedbackRoutine);
        screenFeedbackRoutine = StartCoroutine(EvasionFeedbackRoutine());
    }

    private IEnumerator EvasionFeedbackRoutine()
    {
        if (screenRenderer == null) yield break;

        Material mat = screenRenderer.material;

        if (evasionReady && !evasionOnCooldown)
        {
            mat.SetColor(shaderBorderColorId, evasionReadyBorderColor);
            mat.SetFloat(shaderIconTypeId, iconEye);
        }
        else
        {
            mat.SetColor(shaderBorderColorId, evasionCooldownBorderColor);
            mat.SetFloat(shaderIconTypeId, iconNoSignal);
        }

        mat.SetFloat(shaderShowIconId, 1f);
        yield return null;
        screenFeedbackRoutine = null;
    }

    #endregion
}