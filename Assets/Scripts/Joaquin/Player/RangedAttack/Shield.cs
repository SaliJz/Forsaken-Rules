using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clase que maneja el comportamiento del escudo lanzado por el jugador.
/// </summary>
public class Shield : MonoBehaviour
{
    #region Enums

    private enum ShieldState
    {
        Inactive,
        Thrown,
        Returning,
        Rebounding
    }

    #endregion

    #region Inspector - Stats

    [Header("Stats")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float baseSpeed = 25f;
    [SerializeField] private float maxSpeed = 100f;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private LayerMask collisionLayers;

    #endregion

    #region Inspector - Dynamic Stats

    [Header("Dynamic Stats")]
    [SerializeField] private float baseReturnSpeedMultiplier = 1.2f;
    [SerializeField] private float currentReturnSpeedMultiplier = 1.2f;

    #endregion

    #region Rebound Damage Scaling

    // Multiplicador de daño según el número de golpe en la secuencia (indice 0 = impacto inicial = 100%).
    // Índice 1 = primer rebote (50%), índice 2 = segundo rebote (25%), índice 3+ = 25% (se clampea al último valor).
    private static readonly float[] hitDamageMultipliers = { 1f, 0.25f, 0.25f };

    #endregion

    #region Inspector - Rebound Settings

    [Header("Rebound Settings")]
    [SerializeField] private bool canRebound = true;
    [SerializeField] private int reboundCount = 0;
    [SerializeField] private int maxRebounds = 1;
    [SerializeField] private float reboundDetectionRadius = 15f;
    [SerializeField] private LayerMask enemyLayer;

    #endregion

    #region Inspector - Knockback Settings

    [Header("Knockback Settings (Young/Adult/Elder)")]
    [SerializeField] private float knockbackForce = 0f;

    #endregion

    #region Inspector - Sandy Wall Settings

    [Header("Sandy Wall Settings")]
    [SerializeField] private LayerMask sandyWallLayer;
    [SerializeField] private Renderer sandyVisualRenderer;
    [SerializeField] private string shaderAmountProperty = "_Amount";
    [SerializeField] private ParticleSystem sandyAreaVFX;
    [SerializeField] private float sandyBounceDuration = 0.13f;
    [SerializeField] private float sandyBounceSpeed = 12f;

    #endregion

    #region Inspector - Shield Trail VFX

    [Header("Shield Trail VFX")]
    [SerializeField] private ParticleSystem shieldTrailVFX;
    [SerializeField] private TrailRenderer shieldTrail;

    #endregion

    #region Inspector - Shield Impact VFX

    [Header("Shield Impact VFX")]
    [SerializeField] private GameObject shieldImpactVFX;

    #endregion

    #region Inspector - SFX

    [Header("SFX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shieldImpactClip;
    [SerializeField] private AudioClip shieldTrailClip;

    #endregion

    #region Inspector - Berserker SFX

    [Header("Berserker SFX")]
    [SerializeField] private AudioClip shieldImpactBerserkerClip;
    [SerializeField] private AudioClip shieldTrailBerserkerClip;

    #endregion

    #region Inspector - Sprite Offset (Billboard 2.5D)

    [Header("Sprite Offset por Direccion")]
    [Tooltip("Transform del hijo que contiene el SpriteRenderer. Se reposiciona al lanzar segun la direccion.")]
    [SerializeField] private Transform spriteChild;

    #endregion

    #region Inspector - Debug

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    #endregion

    #region Internal State

    private ShieldState currentState = ShieldState.Inactive;

    private float currentSpeed;
    private Vector3 startPosition;
    private Vector3 lastPosition;

    private Transform returnTarget;
    private Transform currentTarget;
    private List<Transform> hitTargets = new List<Transform>();

    private PlayerShieldController owner;
    private PlayerStatsManager cachedStatsManager;
    private PlayerHealth.LifeStage currentLifeStage;

    private bool isSandy = false;
    private float sandyBounceTimer = 0f;
    private Vector3 sandyBounceDir = Vector3.zero;
    private Vector3 currentMoveDir = Vector3.zero;

    private Collider currentSandyWall = null;
    private Material sandyVisualMat = null;

    private Coroutine deactivationCoroutine;
    private Material shieldTrailVFXMatInstance;
    private Material shieldTrailMatInstance;

    private bool isBerserkerMode = false;

    private float storedToughnessBonus = 0f;

    public static event Action OnShieldBounce;
    public static event Action<GameObject> OnShieldThrown;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeShieldVFX();
    }

    /// <summary>
    /// Funcion que maneja el movimiento y estado del escudo en cada frame.
    /// </summary>
    private void Update()
    {
        if (currentState == ShieldState.Inactive) return;

        currentSpeed = Mathf.Min(currentSpeed + acceleration * Time.deltaTime, maxSpeed);

        if (currentState == ShieldState.Thrown)
        {
            currentMoveDir = transform.forward;
            transform.position += currentMoveDir * currentSpeed * Time.deltaTime;
            PlayerCombatEvents.RaiseShieldMoved(transform.position, attackDamage);

            if (Vector3.Distance(startPosition, transform.position) >= maxDistance)
            {
                StartReturning();
            }
        }
        else if (currentState == ShieldState.Rebounding)
        {
            if (currentTarget == null)
            {
                StartReturning();
                return;
            }

            Vector3 directionToTarget = (currentTarget.position - transform.position).normalized;
            currentMoveDir = directionToTarget;
            transform.forward = directionToTarget;
            transform.position += currentMoveDir * currentSpeed * Time.deltaTime;
            PlayerCombatEvents.RaiseShieldMoved(transform.position, attackDamage);
        }
        else if (currentState == ShieldState.Returning)
        {
            Vector3 directionToTarget = (returnTarget.position - transform.position).normalized;
            currentMoveDir = directionToTarget;
            float returnSpeed = currentSpeed * currentReturnSpeedMultiplier;
            transform.position += currentMoveDir * returnSpeed * Time.deltaTime;
            PlayerCombatEvents.RaiseShieldMoved(transform.position, attackDamage);

            if (Vector3.Distance(transform.position, returnTarget.position) < 1.0f)
            {
                if (deactivationCoroutine == null)
                {
                    deactivationCoroutine = StartCoroutine(SafeDeactivateShieldCoroutine());
                }
            }
        }

        if (currentState != ShieldState.Inactive)
        {
            if (sandyBounceTimer > 0f)
            {
                sandyBounceTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(sandyBounceTimer / sandyBounceDuration);
                transform.position += sandyBounceDir * (sandyBounceSpeed * t * Time.deltaTime);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentState == ShieldState.Inactive) return;

        if (!isSandy && IsSandyWall(other))
        {
            EnterSandyState(other);
            return; // no procesar como colision normal
        }

        if ((collisionLayers.value & (1 << other.gameObject.layer)) > 0)
        {
            PerformHitDetection(other.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (currentState == ShieldState.Inactive) return;

        if (isSandy && other == currentSandyWall)
        {
            ExitSandyState();
            return;
        }

        if ((collisionLayers.value & (1 << other.gameObject.layer)) > 0)
        {
            if (hitTargets.Contains(other.transform))
            {
                hitTargets.Remove(other.transform);
            }
        }
    }

    private void OnDestroy()
    {
        if (shieldTrailVFXMatInstance != null)
        {
            Destroy(shieldTrailVFXMatInstance);
            shieldTrailVFXMatInstance = null;
        }

        if (shieldTrailMatInstance != null)
        {
            Destroy(shieldTrailMatInstance);
            shieldTrailMatInstance = null;
        }

        if (sandyVisualMat != null)
        {
            Destroy(sandyVisualMat);
            sandyVisualMat = null;
        }
    }

    #endregion

    #region Initialization & Data Sync

    private void UpdateDynamicStatsFromManager()
    {
        if (cachedStatsManager == null)
        {
            currentReturnSpeedMultiplier = baseReturnSpeedMultiplier;
            return;
        }

        float returnSpeedMod = cachedStatsManager.GetStat(StatType.ShieldReturnSpeed);
        if (returnSpeedMod <= 0f) returnSpeedMod = 1f;

        currentReturnSpeedMultiplier = baseReturnSpeedMultiplier * returnSpeedMod;

        float pushForceMod = cachedStatsManager.GetStat(StatType.ShieldPushForce);
        knockbackForce = Mathf.Max(0f, knockbackForce + pushForceMod);

        ReportDebug($"Stats dinamicos actualizados: ReturnSpeed={currentReturnSpeedMultiplier}x, KnockbackForce={knockbackForce} (+{pushForceMod})", 1);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Funcion que es llamada por el PlayerShieldController para lanzar el escudo.
    /// </summary>
    /// <param name="owner"> Referencia al controlador del jugador </param>
    /// <param name="direction"> Orientacion del escudo en la direccion del lanzamiento </param>
    /// <param name="canRebound"> Indica si el escudo puede rebotar entre enemigos </param>
    public void Throw(PlayerShieldController owner, Vector3 direction, bool canRebound, int maxRebounds,
        float reboundDetectionRadius, float damage, float speed, float distance, float knockbackForce, PlayerHealth.LifeStage lifeStage,
        bool isBerserker, float toughnessBonus)
    {
        if (deactivationCoroutine != null)
        {
            StopCoroutine(deactivationCoroutine);
            deactivationCoroutine = null;
        }

        this.owner = owner;
        this.returnTarget = owner.transform;
        transform.forward = direction;
        ApplySpriteOffsetForDirection(direction);
        startPosition = transform.position;
        lastPosition = transform.position;

        this.attackDamage = Mathf.RoundToInt(damage);
        this.baseSpeed = speed;
        this.maxDistance = distance;
        currentSpeed = baseSpeed;

        reboundCount = 0;
        this.canRebound = canRebound;
        this.maxRebounds = maxRebounds;
        this.reboundDetectionRadius = reboundDetectionRadius;

        this.knockbackForce = knockbackForce;
        this.currentLifeStage = lifeStage;

        hitTargets.Clear();

        if (cachedStatsManager == null && owner != null)
        {
            cachedStatsManager = owner.GetComponent<PlayerStatsManager>();
        }

        UpdateDynamicStatsFromManager();

        currentState = ShieldState.Thrown;
        gameObject.SetActive(true);

        this.isBerserkerMode = isBerserker;
        this.storedToughnessBonus = toughnessBonus;

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = isBerserkerMode ? shieldTrailBerserkerClip : shieldTrailClip;
            audioSource.loop = true;
            audioSource.Play();
        }

        PlayTrailVFX(true);

        OnShieldThrown?.Invoke(this.gameObject);

        ReportDebug($"Escudo lanzado en modo {lifeStage}: Dano={damage}, Rebote={canRebound}, ReturnSpeed={currentReturnSpeedMultiplier}x", 1);
    }

    #endregion

    #region Sprite Offset

    // Offsets calibrados para cada una de las 8 direcciones cardinales/diagonales.
    private static readonly Vector3[] SpriteDirectionOffsets = new Vector3[]
    {
        new Vector3(-0.25f, 0f, -0.30f), // 0: +Z
        new Vector3( 0.00f, 0f, -0.40f), // 1: +X +Z
        new Vector3( 0.30f, 0f, -0.40f), // 2: +X
        new Vector3( 0.40f, 0f,  0.00f), // 3: +X -Z
        new Vector3( 0.25f, 0f,  0.35f), // 4: -Z
        new Vector3( 0.00f, 0f,  0.45f), // 5: -X -Z
        new Vector3(-0.30f, 0f,  0.35f), // 6: -X
        new Vector3(-0.40f, 0f, -0.10f), // 7: -X +Z
    };

    /// <summary>
    /// Ajusta la posicion local del sprite hijo interpolando entre los offsets de las
    /// 8 direcciones calibradas, segun el angulo exacto de lanzamiento.
    /// Esto cubre cualquier direccion intermedia sin necesidad de calibracion manual.
    /// </summary>
    private void ApplySpriteOffsetForDirection(Vector3 direction)
    {
        if (spriteChild == null) return;

        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        float exactSector = angle / 45f;
        int sectorA = Mathf.FloorToInt(exactSector) % 8;
        int sectorB = (sectorA + 1) % 8;
        float t = exactSector - Mathf.Floor(exactSector);

        Vector3 offset = Vector3.Lerp(SpriteDirectionOffsets[sectorA], SpriteDirectionOffsets[sectorB], t);

        spriteChild.localPosition = offset;
        ReportDebug($"SpriteOffset aplicado: angulo={angle:F1} sectorA={sectorA} sectorB={sectorB} t={t:F2} offset={offset}", 1);
    }

    #endregion

    #region Hit Detection & Damage Application

    private void PerformHitDetection(Transform hitTransform)
    {
        Collider[] hitEnemies = Physics.OverlapSphere(hitTransform.position, 0.5f, enemyLayer);

        const DamageType damageTypeForDummy = DamageType.Shield;
        const AttackDamageType shieldDamageType = AttackDamageType.Ranged;

        bool hasHitAnyEnemy = false;
        bool hasHitHealthThisFrame = false;
        bool hasHitToughnessThisFrame = false;

        OnShieldBounce?.Invoke();

        foreach (Collider enemy in hitEnemies)
        {
            if (enemy.TryGetComponent<GlassShardDamage>(out var shard))
            {
                shard.Shatter();
                continue;
            }

            if (hitTargets.Contains(enemy.transform))
            {
                continue;
            }
            hitTargets.Add(enemy.transform);

            hasHitAnyEnemy = true;

            EnemyToughness toughness = enemy.GetComponent<EnemyToughness>();
            if (toughness != null && toughness.HasToughness)
            {
                hasHitToughnessThisFrame = true;
            }
            else
            {
                hasHitHealthThisFrame = true;
            }

            if (audioSource != null)
            {
                AudioClip clipToPlay = isBerserkerMode ? shieldImpactBerserkerClip : shieldImpactClip;
                if (clipToPlay != null) audioSource.PlayOneShot(clipToPlay);
            }

            CombatEventsManager.TriggerPlayerHitEnemy(enemy.gameObject, false);

            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            Vector3 vfxPos = enemyHealth != null ? enemyHealth.ImpactVFXPosition : enemy.transform.position;
            PlayImpactVFX(vfxPos);

            // reboundCount refleja cuántos rebotes ya ocurrieron antes de dicho golpe:
            // 0 = impacto inicial (100%), 1 = tras el primer rebote (50%), 2 = tras el segundo (25%), 3+ = 25%.
            int hitMultiplierIndex = Mathf.Min(reboundCount, hitDamageMultipliers.Length - 1);
            float scaledAttackDamage = attackDamage * hitDamageMultipliers[hitMultiplierIndex];

            bool isCritical;
            float finalDamage = CriticalHitSystem.CalculateDamage(scaledAttackDamage, transform, enemy.transform, out isCritical);

            IDamageable damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null)
            {
                const string TUTORIAL_DUMMY_TAG = "TutorialDummy";

                if (damageable is MonoBehaviour monoBehaviour && monoBehaviour.gameObject.CompareTag(TUTORIAL_DUMMY_TAG))
                {
                    IDamageable iDamageable = monoBehaviour.GetComponent<IDamageable>();

                    TutorialCombatDummy tutorialDummy = damageable as TutorialCombatDummy;

                    if (tutorialDummy != null)
                    {
                        tutorialDummy.TakeDamage(finalDamage, false, damageTypeForDummy);
                    }
                    else if (iDamageable != null)
                    {
                        if (enemy.TryGetComponent<EnemyHealth>(out var eh) && storedToughnessBonus > 0)
                        {
                            eh.PrepareToughnessBonus(storedToughnessBonus);
                        }
                        damageable.TakeDamage(Mathf.RoundToInt(finalDamage), isCritical, shieldDamageType);
                        ReportDebug($"Golpe a {enemy.name}: DUMMY DE TUTORIAL DETECTADO (Tag). Enviando {finalDamage:F2} de dano de {damageTypeForDummy}", 1);
                    }
                }
                else
                {
                    ExecuteAttack(enemy.gameObject, scaledAttackDamage);
                    ReportDebug($"Golpe a {enemy.name}: Enviando {scaledAttackDamage:F2} de dano de tipo {shieldDamageType} (multiplicador de golpe x{hitDamageMultipliers[hitMultiplierIndex]})", 1);
                }
            }

            if (knockbackForce > 0)
            {
                if (toughness == null || !toughness.HasToughness)
                {
                    EnemyKnockbackHandler knockbackHandler = enemy.GetComponent<EnemyKnockbackHandler>();
                    if (knockbackHandler != null)
                    {
                        Vector3 knockbackDir = (enemy.transform.position - transform.position).normalized;
                        knockbackDir.y = 0;
                        knockbackHandler.TriggerKnockback(knockbackDir, knockbackForce, 0.3f);
                        ReportDebug($"Knockback aplicado a {enemy.name}: Fuerza={knockbackForce} (modificado por stats)", 1);
                    }
                }
            }

            MeatPillar meatPillar = enemy.GetComponent<MeatPillar>();
            if (meatPillar != null)
            {
                meatPillar.TakeDamage(AttackDamageType.Ranged);
            }

            ExplosiveHead explosiveHead = enemy.GetComponent<ExplosiveHead>();
            if (explosiveHead != null)
            {
                explosiveHead.StartPriming(true);
            }
        }

        // Ejecutar el Zoom global consolidado del frame
        if (hasHitHealthThisFrame)
        {
            CameraHitZoomFeedback.Instance?.TriggerHitZoom(false);
        }
        else if (hasHitToughnessThisFrame)
        {
            CameraHitZoomFeedback.Instance?.TriggerHitZoom(true);
        }

        if (!hasHitAnyEnemy)
        {
            StartReturning();
            return;
        }

        if (canRebound)
        {
            Transform nextTarget = FindNextReboundTarget();
            if (nextTarget != null)
            {
                reboundCount++;
                currentTarget = nextTarget;
                currentState = ShieldState.Rebounding;
                ReportDebug($"Rebotando hacia {nextTarget.name} (rebote {reboundCount}/{maxRebounds})", 1);
            }
            else
            {
                StartReturning();
            }
        }
        else
        {
            StartReturning();
        }
    }

    private void ExecuteAttack(GameObject target, float damageAmount)
    {
        if (target.TryGetComponent<IDamageBlocker>(out var blockSystem)
            && target.TryGetComponent<EnemyHealth>(out var healthB))
        {
            // Verificar si el ataque es bloqueado
            if (blockSystem.ShouldBlockDamage(transform.position))
            {
                ReportDebug("Ataque bloqueado por DrogathEnemy.", 1);
                return;
            }

            if (storedToughnessBonus > 0) healthB.PrepareToughnessBonus(storedToughnessBonus);
            healthB.TakeDamage(damageAmount, false, AttackDamageType.Ranged);
        }
        else if (target.TryGetComponent<IDamageable>(out var damageable))
        {
            if (target.TryGetComponent<EnemyHealth>(out var simpleHealth) && storedToughnessBonus > 0)
            {
                simpleHealth.PrepareToughnessBonus(storedToughnessBonus);
            }
            damageable.TakeDamage(damageAmount, false, AttackDamageType.Ranged);
        }
    }

    #endregion

    #region Rebound System

    /// <summary>
    /// Funcion que encuentra el siguiente enemigo para rebotar.
    /// Solo busca en enemyLayer, no en collisionLayers general.
    /// </summary>
    private Transform FindNextReboundTarget()
    {
        if (!canRebound || reboundCount >= maxRebounds)
        {
            ReportDebug("Maximo de rebotes alcanzado o rebotes desactivados.", 1);
            return null;
        }

        // Buscar solo enemigos en el radio (enemyLayer)
        Collider[] potentialTargets = Physics.OverlapSphere(transform.position, reboundDetectionRadius, enemyLayer);

        Transform bestTarget = null;
        float closestDistance = float.MaxValue;

        foreach (Collider targetCollider in potentialTargets)
        {
            // Omitir enemigos ya golpeados
            if (hitTargets.Contains(targetCollider.transform))
            {
                ReportDebug("Enemigo " + targetCollider.name + " omitido (ya golpeado).", 1);
                continue;
            }

            float distanceToTarget = Vector3.Distance(transform.position, targetCollider.transform.position);

            // Cualquier enemigo no golpeado dentro del radio de deteccion es un candidato valido.
            if (distanceToTarget < closestDistance)
            {
                closestDistance = distanceToTarget;
                bestTarget = targetCollider.transform;
                ReportDebug("Candidato a rebote: " + bestTarget.name + " a " + distanceToTarget + "m", 1);
            }
        }

        return bestTarget;
    }

    private void StartReturning()
    {
        currentState = ShieldState.Returning;
        ReportDebug("Escudo retornando al jugador.", 1);
    }

    /// <summary>
    /// Fuerza la desactivación inmediata del escudo y lo devuelve al jugador.
    /// </summary>
    public void ForceDeactivate()
    {
        if (currentState == ShieldState.Inactive) return;

        if (deactivationCoroutine != null)
        {
            StopCoroutine(deactivationCoroutine);
            deactivationCoroutine = null;
        }

        currentState = ShieldState.Inactive;

        if (isSandy) ExitSandyState();

        VFXHelper.SafeStop(shieldTrailVFX, clear: true);
        VFXHelper.SafeSetEmitting(shieldTrail, false);
        if (shieldTrail != null) shieldTrail.Clear();

        if (owner != null) owner.CatchShield();

        PlayerCombatEvents.RaiseShieldLanded();

        gameObject.SetActive(false);
    }

    #endregion

    #region Visual & Audio Effects

    private void EnterSandyState(Collider wall)
    {
        isSandy = true;
        currentSandyWall = wall;

        SetSandyShaderAmount(1f);

        sandyBounceDir = -currentMoveDir;
        sandyBounceTimer = sandyBounceDuration;

        VFXHelper.SafeStop(shieldTrailVFX, clear: true);
        VFXHelper.SafeSetEmitting(shieldTrail, false);
        VFXHelper.SafePlay(sandyAreaVFX);

        ReportDebug("Sandy state ENTER: atravesando muro vertical.", 1);
    }

    private void ExitSandyState()
    {
        isSandy = false;
        currentSandyWall = null;
        sandyBounceTimer = 0f;
        sandyBounceDir = Vector3.zero;

        SetSandyShaderAmount(0f);

        VFXHelper.SafeStop(sandyAreaVFX, clear: true);
        VFXHelper.SafePlay(shieldTrailVFX);
        VFXHelper.SafeSetEmitting(shieldTrail, true);

        ReportDebug("Sandy state EXIT: escudo restaurado.", 1);
    }

    private void SetSandyShaderAmount(float value)
    {
        if (sandyVisualMat == null) return;
        sandyVisualMat.SetFloat(shaderAmountProperty, value);
    }

    private bool IsSandyWall(Collider col)
    {
        return (sandyWallLayer.value & (1 << col.gameObject.layer)) > 0;
    }

    /// <summary>
    /// Inicializa el sistema de VFX del escudo (particulas y trail renderer).
    /// </summary>
    private void InitializeShieldVFX()
    {
        if (shieldTrailVFX == null || shieldTrail == null) return;

        shieldTrailVFX.gameObject.SetActive(true);
        shieldTrail.gameObject.SetActive(true);

        var psRenderer = shieldTrailVFX.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null && psRenderer.sharedMaterial != null)
        {
            shieldTrailVFXMatInstance = new Material(psRenderer.sharedMaterial);
            psRenderer.material = shieldTrailVFXMatInstance;
        }

        var trailRenderer = shieldTrail.GetComponent<TrailRenderer>();
        if (trailRenderer != null && trailRenderer.sharedMaterial != null)
        {
            shieldTrailMatInstance = new Material(trailRenderer.sharedMaterial);
            trailRenderer.material = shieldTrailMatInstance;
        }

        VFXHelper.SafeStop(shieldTrailVFX, clear: true);

        shieldTrail.emitting = false;
        shieldTrail.Clear();

        if (sandyVisualRenderer != null && sandyVisualRenderer.sharedMaterial != null)
        {
            sandyVisualMat = new Material(sandyVisualRenderer.sharedMaterial);
            sandyVisualRenderer.material = sandyVisualMat;
            SetSandyShaderAmount(0f);
        }

        VFXHelper.SafeStop(sandyAreaVFX, clear: true);
    }

    /// <summary>
    /// Activa los efectos visuales del escudo.
    /// </summary>
    private void PlayTrailVFX(bool active)
    {
        if (shieldTrailVFX == null || shieldTrail == null) return;

        var emission = shieldTrailVFX.emission;
        emission.enabled = active;

        if (active)
        {
            VFXHelper.SafePlay(shieldTrailVFX);

            shieldTrail.Clear();
            shieldTrail.emitting = true;
        }
        else
        {
            VFXHelper.SafeStop(shieldTrailVFX, clear: true);
            VFXHelper.SafeSetEmitting(shieldTrail, false);
        }
    }

    private IEnumerator SafeDeactivateShieldCoroutine()
    {
        if (isSandy) ExitSandyState();

        owner.CatchShield();
        currentState = ShieldState.Inactive;

        PlayerCombatEvents.RaiseShieldLanded();

        VFXHelper.SafeStop(shieldTrailVFX, clear: true);
        VFXHelper.SafeSetEmitting(shieldTrail, false);

        yield return null;
        yield return null;

        if (shieldTrailVFX != null && gameObject.activeInHierarchy)
        {
            shieldTrailVFX.Clear(true);
        }

        if (shieldTrail != null)
        {
            shieldTrail.Clear();
        }
        gameObject.SetActive(false);

        deactivationCoroutine = null;
    }

    private void PlayImpactVFX(Vector3 position)
    {
        if (shieldImpactVFX == null) return;

        Instantiate(shieldImpactVFX, position, Quaternion.identity);
    }

    #endregion

    #region Logging

    private void OnDrawGizmos()
    {
        if (!debugMode) return;

        switch (currentState)
        {
            case ShieldState.Thrown:
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(startPosition, transform.position);
                break;
            case ShieldState.Rebounding:
                Gizmos.color = Color.yellow;
                if (currentTarget != null) Gizmos.DrawLine(transform.position, currentTarget.position);
                break;
            case ShieldState.Returning:
                Gizmos.color = Color.green;
                if (returnTarget != null) Gizmos.DrawLine(transform.position, returnTarget.position);
                break;
        }

        // Radio de busqueda de rebote (solo para enemigos)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, reboundDetectionRadius);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Funcion de depuracion para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <<param name="message">Mensaje a reportar.</param> >
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[Shield] {message}");
                break;
            case 2:
                Debug.LogWarning($"[Shield] {message}");
                break;
            case 3:
                Debug.LogError($"[Shield] {message}");
                break;
            default:
                Debug.Log($"[Shield] {message}");
                break;
        }
    }

    #endregion
}