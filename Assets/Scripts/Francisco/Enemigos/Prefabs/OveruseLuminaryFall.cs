using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class OveruseLuminaryFall : MonoBehaviour
{
    #region Enums

    private enum BeamType
    {
        None,
        Lantern,
        Projector
    }

    #endregion

    #region Inspector Fields

    [Header("Stomp Damage")]
    [SerializeField] private float stompDamage = 5f;
    [SerializeField] private float stompRadius = 3f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Beam Emission")]
    [SerializeField] private float beamGroundedDuration = 1.5f;

    [Header("Lantern Beam")]
    [SerializeField] private float lanternConeAngle = 60f;
    [SerializeField] private float lanternConeRadius = 7.5f;
    [SerializeField] private float lanternValuePerSecond = 0.15f;

    [Header("Projector Beam")]
    [SerializeField] private float projectorConeAngle = 120f;
    [SerializeField] private float projectorConeRadius = 5f;
    [SerializeField] private float projectorValuePerSecond = 0.3f;

    [Header("Deterioration")]
    [SerializeField] private float noBeamChanceIncreasePerImpact = 0.1f;
    [SerializeField] private float maxNoBeamChance = 0.4f;

    [Header("VFX")]
    [SerializeField] private GameObject lanternBeamVFXPrefab;
    [SerializeField] private GameObject projectorBeamVFXPrefab;
    [SerializeField] private GameObject flickerVFXPrefab;
    [SerializeField] private Transform beamOrigin;

    #endregion

    #region Private State

    private int impactCount = 0;
    private float currentNoBeamChance = 0f;
    private bool stompAppliedThisLanding = false;

    private GameObject activeBeamVFX;
    private GameObject activeFlickerVFX;
    private Coroutine beamRoutine;
    private Coroutine flickerRoutine;

    private Transform playerTransform;
    private OveruseScreenManager screenManager;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
            playerTransform = playerObj.transform;
        else
            Log("Player not found. Beam and stomp effects will not work.", 2);

        screenManager = OveruseScreenManager.Instance;

        if (screenManager == null)
            Log("OveruseScreenManager instance not found in scene.", 2);
    }

    private void OnDestroy()
    {
        CleanupBeamVFX();
        CleanupFlickerVFX();
    }

    #endregion

    #region Public API

    public void OnLanded()
    {
        impactCount++;
        stompAppliedThisLanding = false;
        currentNoBeamChance = Mathf.Clamp(noBeamChanceIncreasePerImpact * (impactCount - 1), 0f, maxNoBeamChance);

        ApplyStompDamage();

        BeamType selectedBeam = RollBeamType();

        Log($"Impact #{impactCount} | NoBeamChance: {currentNoBeamChance:P0} | Beam: {selectedBeam}", 1);

        if (beamRoutine != null)
        {
            StopCoroutine(beamRoutine);
            beamRoutine = null;
        }

        if (flickerRoutine != null)
        {
            StopCoroutine(flickerRoutine);
            flickerRoutine = null;
        }

        beamRoutine = StartCoroutine(BeamRoutine(selectedBeam));
    }

    #endregion

    #region Stomp Damage

    private void ApplyStompDamage()
    {
        if (stompAppliedThisLanding) return;
        stompAppliedThisLanding = true;

        if (playerLayer.value == 0)
        {
            Log("playerLayer is not assigned. Stomp damage will not be applied.", 2);
            return;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, stompRadius, playerLayer, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            damageable.TakeDamage(stompDamage, false, AttackDamageType.Melee);
            Log($"Stomp hit {hit.gameObject.name} for {stompDamage}.", 1);
        }
    }

    #endregion

    #region Beam Selection

    private BeamType RollBeamType()
    {
        float roll = Random.value;

        if (roll < currentNoBeamChance)
            return BeamType.None;

        float remapped = (roll - currentNoBeamChance) / (1f - currentNoBeamChance);
        return remapped < 0.5f ? BeamType.Lantern : BeamType.Projector;
    }

    #endregion

    #region Beam Routine

    private IEnumerator BeamRoutine(BeamType beamType)
    {
        CleanupBeamVFX();

        if (beamType == BeamType.None)
        {
            Log("No beam emitted (deterioration).", 1);
            flickerRoutine = StartCoroutine(FlickerRoutine());
            yield break;
        }

        SpawnBeamVFX(beamType);

        float elapsed = 0f;
        float coneAngle = beamType == BeamType.Lantern ? lanternConeAngle : projectorConeAngle;
        float coneRadius = beamType == BeamType.Lantern ? lanternConeRadius : projectorConeRadius;
        float valueRate = beamType == BeamType.Lantern ? lanternValuePerSecond : projectorValuePerSecond;

        while (elapsed < beamGroundedDuration)
        {
            if (playerTransform != null && IsPlayerInCone(coneAngle, coneRadius))
                screenManager?.AddValue(valueRate);

            elapsed += Time.deltaTime;
            yield return null;
        }

        CleanupBeamVFX();
        Log($"Beam {beamType} ended.", 1);
        beamRoutine = null;
    }

    #endregion

    #region Cone Detection

    private bool IsPlayerInCone(float coneAngle, float coneRadius)
    {
        if (playerTransform == null) return false;

        Vector3 origin = beamOrigin != null ? beamOrigin.position : transform.position;
        Vector3 toPlayer = playerTransform.position - origin;

        if (toPlayer.magnitude > coneRadius) return false;

        return Vector3.Angle(transform.forward, toPlayer) <= coneAngle * 0.5f;
    }

    #endregion

    #region Flicker Routine

    private IEnumerator FlickerRoutine()
    {
        CleanupFlickerVFX();

        if (flickerVFXPrefab == null) yield break;

        Transform parent = beamOrigin != null ? beamOrigin : transform;
        activeFlickerVFX = Instantiate(flickerVFXPrefab, parent);
        activeFlickerVFX.transform.localPosition = Vector3.zero;
        activeFlickerVFX.transform.localRotation = Quaternion.identity;

        yield return new WaitForSeconds(beamGroundedDuration);

        CleanupFlickerVFX();
        flickerRoutine = null;
    }

    #endregion

    #region VFX Helpers

    private void SpawnBeamVFX(BeamType beamType)
    {
        GameObject prefab = beamType == BeamType.Lantern ? lanternBeamVFXPrefab : projectorBeamVFXPrefab;
        if (prefab == null) return;

        Transform parent = beamOrigin != null ? beamOrigin : transform;
        activeBeamVFX = Instantiate(prefab, parent);
        activeBeamVFX.transform.localPosition = Vector3.zero;
        activeBeamVFX.transform.localRotation = Quaternion.identity;
    }

    private void CleanupBeamVFX()
    {
        if (activeBeamVFX == null) return;
        Destroy(activeBeamVFX);
        activeBeamVFX = null;
    }

    private void CleanupFlickerVFX()
    {
        if (activeFlickerVFX == null) return;
        Destroy(activeFlickerVFX);
        activeFlickerVFX = null;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, stompRadius);

        DrawConeGizmo(lanternConeAngle, lanternConeRadius, new Color(0f, 0.8f, 1f, 0.2f));
        DrawConeGizmo(projectorConeAngle, projectorConeRadius, new Color(1f, 0.9f, 0f, 0.15f));
    }

    private void DrawConeGizmo(float angle, float radius, Color color)
    {
        Gizmos.color = color;

        Vector3 origin = beamOrigin != null ? beamOrigin.position : transform.position;
        float halfAngle = angle * 0.5f * Mathf.Deg2Rad;
        Vector3 prevPoint = Vector3.zero;

        for (int i = 0; i <= 20; i++)
        {
            float t = (float)i / 20;
            float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 dir = Quaternion.AngleAxis(currentAngle * Mathf.Rad2Deg, Vector3.up) * transform.forward;
            Vector3 point = origin + dir * radius;

            if (i > 0) Gizmos.DrawLine(prevPoint, point);
            Gizmos.DrawLine(origin, point);
            prevPoint = point;
        }
    }

    #endregion

    #region Debug

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void Log(string message, int level)
    {
        switch (level)
        {
            case 1: Debug.Log($"[OveruseLuminaryFall] {message}"); break;
            case 2: Debug.LogWarning($"[OveruseLuminaryFall] {message}"); break;
            case 3: Debug.LogError($"[OveruseLuminaryFall] {message}"); break;
        }
    }

    #endregion
}