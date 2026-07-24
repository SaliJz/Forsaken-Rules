using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class StaticEnemy : BaseEnemyRanged
{
    #region Inspector Fields
    [Header("Static - Base Stats")]
    [SerializeField] private float staticHealth = 35f;
    [SerializeField] private float staticProjectileSpeed = 15f;
    [SerializeField] private float staticFireInterval = 1.2f;

    [Header("Static - Swarm")]
    [SerializeField] private GameObject swarmParticlePrefab;
    [SerializeField] private float swarmDuration = 1f;
    [SerializeField] private float swarmDPS = 1f;
    [SerializeField] private float swarmRadius = 1.5f;

    [Header("Static - Stress Residue")]
    [SerializeField] private GameObject stressResiduePrefab;
    [SerializeField] private float residueDuration = 4f;
    [SerializeField] private float residueDPS = 2f;
    [SerializeField] private float residueRadius = 2f;

    [Header("Static - Reactive Evasion")]
    [SerializeField] private float evasionCooldown = 6f;
    [SerializeField] private float evasionTeleportDistance = 8f;

    [Header("Static - Pursue Overrides")]
    [SerializeField] private float staticP1_teleportCooldown = 2.5f;
    [SerializeField] private float staticP1_advanceDistance = 5f;
    [SerializeField] private float staticP1_lateralMin = 3f;
    [SerializeField] private float staticP1_lateralMax = 5f;

    [Header("Static - Pursue Overrides Phase 2")]
    [SerializeField] private float staticP2_teleportCooldown = 2f;
    [SerializeField] private float staticP2_teleportRange = 7.5f;

    [Header("Static - VFX Screen")]
    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private Color evasionReadyBorderColor = Color.red;
    [SerializeField] private Color evasionCooldownBorderColor = Color.gray;

    [Header("Static - SFX")]
    [SerializeField] private AudioClip evasionSFX;
    [SerializeField] private AudioClip residueSFX;
    [SerializeField] private AudioClip swarmSFX;
    [SerializeField] private AudioClip retaliatoryShootSFX;

    [Header("Static - Animation")]
    [SerializeField] private StaticAnimCtrl animCtrl;
    #endregion

    #region Private State
    private bool evasionOnCooldown = false;
    private bool evasionReady = true;
    private bool isTeleporting = false;
    private Coroutine evasionCooldownRoutine;
    private Coroutine screenFeedbackRoutine;
    private List<GameObject> activeResidues = new List<GameObject>();
    private Vector3 pendingTeleportPosition;

    private static readonly int ShaderBorderColorId = Shader.PropertyToID("_BorderColor");
    private static readonly int ShaderShowIconId = Shader.PropertyToID("_ShowIcon");
    private static readonly int ShaderIconTypeId = Shader.PropertyToID("_IconType");

    private const int ICON_EYE = 0;
    private const int ICON_NO_SIGNAL = 1;
    #endregion

    #region Init
    protected override void InitializeEnemy()
    {
        base.InitializeEnemy();
        Health = staticHealth;
        ProjectileSpeed = staticProjectileSpeed;
        FireRate = staticFireInterval;

        if (animCtrl == null)
            animCtrl = GetComponentInChildren<StaticAnimCtrl>();

        UpdateEvasionFeedback();
    }

    public void FixedUpdate()
    {
        if (animCtrl != null) animCtrl.transform.localPosition = Vector3.zero;
    }
    #endregion

    #region Animation Events
    public void HandleAnimEvents(string eventName)
    {
        if (eventName == "AnimEvent_Shoot") ExecuteActualFire();
        if (eventName == "AnimEvent_TPOutComplete") ExecuteActualTeleport();
        if (eventName == "AnimEvent_TPInComplete") OnTeleportFinished();
    }

    private void ExecuteActualFire()
    {
        if (isDead || playerTransform == null) return;

        if (AudioSource != null && swarmSFX != null) AudioSource.PlayOneShot(swarmSFX);

        Vector3 dir = (playerTransform.position - FirePoint.position).normalized;
        FirePoint.rotation = Quaternion.LookRotation(dir);

        GameObject projObj = Instantiate(ProjectilePrefab, FirePoint.position, FirePoint.rotation);
        StaticProjectile staticProjectile = projObj.GetComponent<StaticProjectile>();

        if (staticProjectile != null)
        {
            string word = wordLibrary != null ? wordLibrary.GetRandomWord() : "STATIC";
            staticProjectile.Initialize(staticProjectileSpeed, 5f, 30f, 20f, swarmDuration, swarmDPS, swarmRadius, swarmParticlePrefab, word);
        }
    }

    private void ExecuteActualTeleport()
    {
        Vector3 targetPos = pendingTeleportPosition;

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            targetPos = hit.position;

        TogglePresence(false);

        if (agent != null) agent.enabled = false;

        transform.position = targetPos;

        if (animCtrl != null) animCtrl.transform.localPosition = Vector3.zero;

        animCtrl?.PlayTPin();
    }

    private void OnTeleportFinished()
    {
        animCtrl?.restoreOriginalMaterials();

        if (animCtrl != null) animCtrl.transform.localPosition = Vector3.zero;

        if (agent != null)
        {
            agent.enabled = true;
            agent.ResetPath();
        }

        TogglePresence(true);
        StartCoroutine(FinalizeTeleportNextFrame());
    }

    private IEnumerator FinalizeTeleportNextFrame()
    {
        yield return null;
        yield return null;
        isTeleporting = false;
    }
    #endregion

    #region Core Overrides
    protected override void Update()
    {
        if (isTeleporting || isDead) return;
        base.Update();
    }

    protected override void OnBeforeTeleport(Vector3 fromPosition)
    {
        if (isTeleporting) return;
        isTeleporting = true;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        SpawnStressResidue(fromPosition);
        animCtrl?.PlayTPout();
    }

    protected override void OnAfterTeleport(Vector3 toPosition) { }

    private void TogglePresence(bool active)
    {
        CapsuleCollider col = GetComponent<CapsuleCollider>();
        if (col != null) col.enabled = active;

        Transform vfxFolder = transform.Find("Enemy2_VisualEffects");
        if (vfxFolder != null) vfxFolder.gameObject.SetActive(active);
        if (screenRenderer != null) screenRenderer.enabled = active;
    }
    #endregion

    #region Pursue Routines
    protected override IEnumerator TeleportToPositionRoutine(Vector3 targetPos)
    {
        while (isTeleporting) yield return null;

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            pendingTeleportPosition = hit.position;
        else
            pendingTeleportPosition = targetPos;

        OnBeforeTeleport(transform.position);

        while (isTeleporting) yield return null;
    }

    protected override IEnumerator Pursue1Routine()
    {
        while (currentState == EnemyState.Pursue1)
        {
            Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 advancePos = transform.position + dirToPlayer * staticP1_advanceDistance;
            Vector3 lateral = Vector3.Cross(dirToPlayer, Vector3.up);
            float lateralOffset = Random.Range(staticP1_lateralMin, staticP1_lateralMax) * (Random.value > 0.5f ? 1f : -1f);
            Vector3 targetPos = advancePos + lateral * lateralOffset;

            yield return StartCoroutine(TeleportToPositionRoutine(targetPos));
            if (!isDead) StartShootCoroutine();
            yield return new WaitForSeconds(staticP1_teleportCooldown);
        }
    }

    protected override IEnumerator Pursue2Routine()
    {
        int currentPointIndex = 0;
        float[] cardinals = { 0f, 90f, 180f, 270f };

        while (currentState == EnemyState.Pursue2)
        {
            float angle = cardinals[currentPointIndex];
            Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * staticP2_teleportRange;
            Vector3 targetPos = playerTransform.position + offset;

            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                yield return StartCoroutine(TeleportToPositionRoutine(hit.position));
                if (!isDead) StartShootCoroutine();
            }

            currentPointIndex = (currentPointIndex + 1) % cardinals.Length;
            yield return new WaitForSeconds(staticP2_teleportCooldown);
        }
    }
    #endregion

    #region Combat & Evasion
    protected override void FireProjectile()
    {
        if (isTeleporting || isDead || (enemyHealth != null && enemyHealth.IsStunned)) return;
        ForceFacePlayer();
        animCtrl?.PlayShoot();
    }

    protected override void HandleDamageTaken()
    {
        if (isDead || isTeleporting || !evasionReady || evasionOnCooldown) return;
        StartCoroutine(ReactiveEvasionRoutine());
    }

    private IEnumerator ReactiveEvasionRoutine()
    {
        evasionReady = false;
        evasionOnCooldown = true;

        if (AudioSource != null && evasionSFX != null)
            AudioSource.PlayOneShot(evasionSFX);

        Vector3 awayFromPlayer = (transform.position - playerTransform.position).normalized;
        Vector3 evasionTarget = transform.position + awayFromPlayer * evasionTeleportDistance;

        yield return StartCoroutine(TeleportToPositionRoutine(evasionTarget));

        yield return new WaitForSeconds(0.1f);
        if (!isDead) FireRetaliatory();

        if (evasionCooldownRoutine != null) StopCoroutine(evasionCooldownRoutine);
        evasionCooldownRoutine = StartCoroutine(EvasionCooldownRoutine());
    }

    private void FireRetaliatory()
    {
        if (isTeleporting || isDead || playerTransform == null) return;
        ForceFacePlayer();
        if (AudioSource != null && retaliatoryShootSFX != null)
            AudioSource.PlayOneShot(retaliatoryShootSFX);
        animCtrl?.PlayShoot();
    }
    #endregion

    #region VFX & Feedback
    private IEnumerator EvasionCooldownRoutine()
    {
        UpdateEvasionFeedback();
        yield return new WaitForSeconds(evasionCooldown);
        evasionOnCooldown = false;
        evasionReady = true;
        UpdateEvasionFeedback();
        evasionCooldownRoutine = null;
    }

    private void SpawnStressResidue(Vector3 position)
    {
        if (stressResiduePrefab == null) return;
        GameObject residue = Instantiate(stressResiduePrefab, position, Quaternion.identity);
        activeResidues.Add(residue);
        if (AudioSource != null && residueSFX != null) AudioSource.PlayOneShot(residueSFX);
        StaticStressResidue residueComp = residue.GetComponent<StaticStressResidue>();
        if (residueComp != null) residueComp.Initialize(residueDuration, residueDPS, residueRadius);
        StartCoroutine(RemoveResidueAfterDelay(residue, residueDuration));
    }

    private IEnumerator RemoveResidueAfterDelay(GameObject residue, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (residue != null)
        {
            activeResidues.Remove(residue);
            Destroy(residue);
        }
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
            mat.SetColor(ShaderBorderColorId, evasionReadyBorderColor);
            mat.SetFloat(ShaderIconTypeId, ICON_EYE);
        }
        else
        {
            mat.SetColor(ShaderBorderColorId, evasionCooldownBorderColor);
            mat.SetFloat(ShaderIconTypeId, ICON_NO_SIGNAL);
        }

        mat.SetFloat(ShaderShowIconId, 1f);
        yield return null;
        screenFeedbackRoutine = null;
    }
    #endregion

    #region Death
    protected override void HandleEnemyDeath(GameObject enemy)
    {
        if (isDead || enemy != gameObject) return;
        isTeleporting = false;
        animCtrl?.PlayDeath();
        TogglePresence(false);
        for (int i = activeResidues.Count - 1; i >= 0; i--)
            if (activeResidues[i] != null) Destroy(activeResidues[i]);
        activeResidues.Clear();
        base.HandleEnemyDeath(enemy);
    }
    #endregion
}