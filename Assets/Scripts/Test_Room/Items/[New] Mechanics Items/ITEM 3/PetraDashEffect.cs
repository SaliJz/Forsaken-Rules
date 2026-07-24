using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "SedimentsOfPetraDashEffect", menuName = "Item Effects/Combat/SedimentsOfPetraDash")]
public class PetraDashEffect : ItemEffectBase
{
    #region Inspector Fields

    [Header("Dash Spikes")]
    [Range(0.01f, 2f)]
    [SerializeField] private float dashSpikeDamagePercent = 0.40f;
    [SerializeField] private float dashSpikeDuration = 0.5f;
    [SerializeField] private int spikesPerBurst = 4;
    [SerializeField] private float spawnSpacingMin = 0.4f;
    [SerializeField] private float burstDelayMin = 0f;
    [SerializeField] private float burstDelayMax = 0.09f;

    [Header("Delay Settings")]
    [SerializeField] private float initialDelay = 0.1f;

    [Header("Spike Visual Variation")]
    [SerializeField] private float tiltAngleCenter = 20f;
    [SerializeField] private float tiltAngleSide = 35f;
    [SerializeField] private float maxSideYawAngle = 18f;
    [SerializeField] private float scaleMin = 0.7f;
    [SerializeField] private float scaleMax = 1.25f;

    [Header("Trail Visual")]
    [SerializeField] private Material trailMaterial;
    [SerializeField] private float trailWidth = 0.3f;
    [SerializeField] private Color trailColor = new Color(0.45f, 0.28f, 0.10f, 0.80f);
    [SerializeField] private float trailFadeDuration = 2f;

    [Header("Shared")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundRayOriginHeight = 10f;
    [SerializeField] private float groundRayMaxDistance = 30f;
    [SerializeField] private float groundSurfaceOffset = 0.02f;

    #endregion

    #region Private Fields

    private MonoBehaviour coroutineRunner;

    private Vector3 lastSpikeSpawnPos = Vector3.positiveInfinity;

    private GameObject activeTrailGO;
    private LineRenderer activeTrail;
    private List<Vector3> trailPoints = new List<Vector3>();

    #endregion

    #region ItemEffectBase

    private void OnEnable()
    {
        EffectID = "Sedimentos de Petra Dash";
        category = EffectCategory.Combat;
    }

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        ResetState();

        coroutineRunner = FindFirstObjectByType<PlayerMovement>();

        PlayerCombatEvents.OnDashStarted += HandleDashStarted;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnDashStarted -= HandleDashStarted;

        coroutineRunner = null;

        CloseCurrentTrail();
    }

    #endregion

    #region Ground Detection

    private Vector3 GetGroundPosition(Vector3 worldPos)
    {
        Vector3 rayOrigin = new Vector3(worldPos.x, worldPos.y + groundRayOriginHeight, worldPos.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
            groundRayOriginHeight + groundRayMaxDistance, groundLayer))
        {
            return new Vector3(
                hit.point.x,
                hit.point.y + groundSurfaceOffset,
                hit.point.z
            );
        }

        return new Vector3(
            worldPos.x,
            worldPos.y + groundSurfaceOffset,
            worldPos.z
        );
    }

    #endregion

    #region Dash Handlers

    private void HandleDashStarted(Vector3 playerPosition, Vector3 dashDirection)
    {
        if (coroutineRunner == null)
        {
            coroutineRunner = FindFirstObjectByType<PlayerMovement>();

            if (coroutineRunner == null)
                return;
        }

        coroutineRunner.StartCoroutine(
            GenerateSpikesDuringDash(playerPosition, dashDirection)
        );
    }

    private System.Collections.IEnumerator GenerateSpikesDuringDash(
    Vector3 startPosition,
    Vector3 dashDirection)
    {
        Vector3 dashStartPosition = startPosition;

        float dashDistance = coroutineRunner is PlayerMovement playerMove ? playerMove.LastCalculatedDashDistance : 10f;

        Vector3 dashFinalPosition =
            dashStartPosition + dashDirection.normalized * dashDistance;

        yield return new WaitForSeconds(initialDelay);

        float elapsed = 0f;
        float dashDuration = 0.3f;

        lastSpikeSpawnPos = dashStartPosition;

        Vector3 groundStart =
            GetGroundPosition(dashStartPosition);

        AddTrailPoint(groundStart);

        SpawnBurst(groundStart, dashDirection);

        while (elapsed < dashDuration)
        {
            float t = elapsed / dashDuration;

            Vector3 simulatedDashPos =
                Vector3.Lerp(
                    dashStartPosition,
                    dashFinalPosition,
                    t
                );

            Vector3 from =
                new Vector3(
                    lastSpikeSpawnPos.x,
                    0f,
                    lastSpikeSpawnPos.z
                );

            Vector3 to =
                new Vector3(
                    simulatedDashPos.x,
                    0f,
                    simulatedDashPos.z
                );

            Vector3 delta = to - from;

            float distance = delta.magnitude;

            while (distance >= spawnSpacingMin)
            {
                Vector3 dir = delta.normalized;

                Vector3 nextFlat =
                    from + dir * spawnSpacingMin;

                Vector3 nextPoint = new Vector3(
                    nextFlat.x,
                    simulatedDashPos.y,
                    nextFlat.z
                );

                lastSpikeSpawnPos = nextPoint;

                Vector3 groundPoint =
                    GetGroundPosition(nextPoint);

                AddTrailPoint(groundPoint);

                SpawnBurst(groundPoint, dashDirection);

                from = new Vector3(
                    lastSpikeSpawnPos.x,
                    0f,
                    lastSpikeSpawnPos.z
                );

                delta = to - from;

                distance = delta.magnitude;
            }

            elapsed += Time.deltaTime;

            yield return null;
        }

        Vector3 finalGround =
            GetGroundPosition(dashFinalPosition);

        AddTrailPoint(finalGround);

        SpawnBurst(finalGround, dashDirection);

        CloseCurrentTrail(dashSpikeDuration);
    }

    private void SpawnBurst(Vector3 pos, Vector3 dir)
    {
        float damage = 50f * dashSpikeDamagePercent;

        for (int i = 0; i < spikesPerBurst; i++)
        {
            float delay = Random.Range(burstDelayMin, burstDelayMax);

            SpawnDashSpikeDelayed(
                pos,
                dir,
                damage,
                delay,
                i,
                spikesPerBurst
            );
        }
    }

    private async void SpawnDashSpikeDelayed(
        Vector3 position,
        Vector3 dashDirection,
        float damage,
        float delay,
        int spikeIndex,
        int totalSpikes)
    {
        if (delay > 0f)
            await Task.Delay(System.TimeSpan.FromSeconds(delay));

        if (ItemEffectPool.Instance == null)
            return;

        float yaw = 0f;
        float tilt = tiltAngleCenter;

        if (totalSpikes > 1)
        {
            float t = (float)spikeIndex / (totalSpikes - 1);

            yaw = Mathf.Lerp(-maxSideYawAngle, maxSideYawAngle, t);

            float absT = Mathf.Abs(t - 0.5f) * 2f;

            tilt = Mathf.Lerp(tiltAngleCenter, tiltAngleSide, absT);

            float lateralSign = (spikeIndex % 2 == 0) ? 1f : -1f;

            tilt *= lateralSign;
        }

        Vector3 spikeDir =
            Quaternion.Euler(0f, yaw, 0f) * dashDirection;

        Quaternion rotation =
            Quaternion.LookRotation(spikeDir) *
            Quaternion.Euler(tilt, 0f, 0f);

        float scale = Random.Range(scaleMin, scaleMax);

        ItemEffectPool.Instance.SpawnSpikeWithScale(
            position,
            rotation,
            damage,
            dashSpikeDuration,
            enemyLayer,
            isLargeSpike: true,
            scale: scale
        );
    }

    #endregion

    #region Trail Visual

    private void AddTrailPoint(Vector3 point)
    {
        if (activeTrailGO == null)
        {
            activeTrailGO = new GameObject("PetraDashTrail");

            activeTrail = activeTrailGO.AddComponent<LineRenderer>();

            SetupLineRenderer(activeTrail);

            trailPoints.Clear();
        }

        trailPoints.Add(point);

        activeTrail.positionCount = trailPoints.Count;

        activeTrail.SetPositions(trailPoints.ToArray());
    }

    private void SetupLineRenderer(LineRenderer lr)
    {
        lr.useWorldSpace = true;

        lr.startWidth = trailWidth;
        lr.endWidth = trailWidth * 0.4f;

        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;

        lr.alignment = LineAlignment.TransformZ;

        lr.material = trailMaterial != null
            ? trailMaterial
            : new Material(Shader.Find("Sprites/Default"));

        lr.startColor = trailColor;

        lr.endColor = new Color(
            trailColor.r,
            trailColor.g,
            trailColor.b,
            0f
        );
    }

    private void CloseCurrentTrail(float overrideDuration = -1f)
    {
        if (activeTrailGO == null)
            return;

        PetraTrailFader fader =
            activeTrailGO.AddComponent<PetraTrailFader>();

        float finalFade =
            overrideDuration > 0
            ? overrideDuration
            : trailFadeDuration;

        fader.Init(activeTrail, finalFade);

        activeTrailGO = null;
        activeTrail = null;

        trailPoints.Clear();
    }

    private void ResetState()
    {
        lastSpikeSpawnPos = Vector3.positiveInfinity;

        activeTrailGO = null;
        activeTrail = null;

        trailPoints.Clear();
    }

    #endregion
}