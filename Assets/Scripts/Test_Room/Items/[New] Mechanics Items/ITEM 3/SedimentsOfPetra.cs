using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "SedimentsOfPetraEffect", menuName = "Item Effects/Combat/SedimentsOfPetra")]
public class SedimentsOfPetraItemEffect : ItemEffectBase
{
    #region Inspector Fields

    [Header("Pinchos de Rastro - Distancia")]
    [Range(0.01f, 1f)] public float smallSpikeDamagePercent = 0.10f;
    public float smallSpikeDuration = 4f;
    public float minDistanceBetweenSpikes = 1.5f;
    public float smallSpikeTiltAngle = 20f;

    [Header("Rastro Visual en Suelo")]
    public Material trailMaterial;
    public float trailWidth = 0.25f;
    public Color trailColor = new Color(0.45f, 0.28f, 0.10f, 0.75f);
    public float trailFadeDuration = 3f;

    [Header("Abanico de Picos - Melee")]
    [Range(0.01f, 2f)] public float largeSpikeDamagePercent = 0.50f;
    public float largeSpikeDuration = 0.3f;
    public int largeSpikeCount = 3;
    public float spikeSpreadAngle = 30f;
    public float spikeForwardDistance = 2f;
    public float spikeRandomDelayMin = 0f;
    public float spikeRandomDelayMax = 0.12f;
    public float largeSpikeAngle = 25f;

    [Header("Compartido")]
    public LayerMask enemyLayer;

    #endregion

    #region Private Fields

    private Vector3 lastSpikePosition = Vector3.positiveInfinity;
    private Vector3 lastShieldPosition = Vector3.positiveInfinity;
    private GameObject activeTrailGO;
    private LineRenderer activeTrail;
    private List<Vector3> trailPoints = new List<Vector3>();

    #endregion

    #region ItemEffectBase

    private void OnEnable()
    {
        EffectID = "Sedimentos de Petra";
        category = EffectCategory.Combat;
        if (string.IsNullOrEmpty(effectDescription))
            effectDescription = "El escudo deja un rastro de estacas de tierra inclinadas. Los ataques melee generan un abanico de picos frontales.";
    }

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        lastSpikePosition = Vector3.positiveInfinity;
        lastShieldPosition = Vector3.positiveInfinity;
        trailPoints.Clear();
        activeTrailGO = null;
        activeTrail = null;

        PlayerCombatEvents.OnShieldMoved += HandleShieldMoved;
        PlayerCombatEvents.OnShieldLanded += HandleShieldLanded;
        PlayerCombatEvents.OnMeleeHit += HandleMeleeHit;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnShieldMoved -= HandleShieldMoved;
        PlayerCombatEvents.OnShieldLanded -= HandleShieldLanded;
        PlayerCombatEvents.OnMeleeHit -= HandleMeleeHit;
        CloseCurrentTrail();
    }

    public override string GetFormattedDescription()
    {
        return $"El escudo deja estacas inclinadas que causan <b>{smallSpikeDamagePercent * 100:F0}%</b> del daño base durante <b>{smallSpikeDuration}s</b>. " +
               $"Cada golpe melee genera <b>{largeSpikeCount}</b> picos en abanico frontal que causan <b>{largeSpikeDamagePercent * 100:F0}%</b> del daño " +
               $"y desaparecen en <b>{largeSpikeDuration}s</b>.";
    }

    #endregion

    #region Shield Handlers

    private void HandleShieldMoved(Vector3 shieldPosition, float playerBaseDamage)
    {
        Vector3 groundPos = new Vector3(shieldPosition.x, 0.02f, shieldPosition.z);

        AddTrailPoint(groundPos);

        bool tooClose = !float.IsPositiveInfinity(lastSpikePosition.x) &&
                        Vector3.Distance(groundPos, lastSpikePosition) < minDistanceBetweenSpikes;
        if (tooClose)
        {
            lastShieldPosition = shieldPosition;
            return;
        }

        Vector3 travelDir;
        if (float.IsPositiveInfinity(lastShieldPosition.x))
            travelDir = Vector3.forward;
        else
        {
            travelDir = (shieldPosition - lastShieldPosition).normalized;
            if (travelDir == Vector3.zero) travelDir = Vector3.forward;
        }

        lastShieldPosition = shieldPosition;
        lastSpikePosition = groundPos;

        Vector3 flatDir = new Vector3(travelDir.x, 0f, travelDir.z).normalized;
        Quaternion rotation = Quaternion.LookRotation(flatDir) * Quaternion.Euler(-smallSpikeTiltAngle, 0f, 0f);

        float spikeDamage = playerBaseDamage * smallSpikeDamagePercent;

        if (ItemEffectPool.Instance != null)
        {
            ItemEffectPool.Instance.SpawnSpike(groundPos, rotation, spikeDamage, smallSpikeDuration, enemyLayer, false);
        }
    }

    private void HandleShieldLanded()
    {
        CloseCurrentTrail();
        lastSpikePosition = Vector3.positiveInfinity;
        lastShieldPosition = Vector3.positiveInfinity;
    }

    #endregion

    #region Melee Handlers

    private void HandleMeleeHit(Vector3 playerPosition, Vector3 playerForward, float meleeDamage)
    {
        float spikeDamage = meleeDamage * largeSpikeDamagePercent;
        float angleStep = largeSpikeCount > 1 ? (spikeSpreadAngle * 2f) / (largeSpikeCount - 1) : 0f;
        float startAngle = largeSpikeCount > 1 ? -spikeSpreadAngle : 0f;

        for (int i = 0; i < largeSpikeCount; i++)
        {
            float angle = startAngle + angleStep * i;
            float delay = Random.Range(spikeRandomDelayMin, spikeRandomDelayMax);

            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * playerForward;
            Vector3 spawnPos = new Vector3(
                playerPosition.x + dir.x * spikeForwardDistance,
                0.02f,
                playerPosition.z + dir.z * spikeForwardDistance);

            Quaternion rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(largeSpikeAngle, 0f, 0f);

            SpawnSpikeDelayed(spawnPos, rotation, spikeDamage, delay);
        }
    }

    private async void SpawnSpikeDelayed(Vector3 position, Quaternion rotation, float damage, float delay)
    {
        if (delay > 0f)
            await Task.Delay(System.TimeSpan.FromSeconds(delay));

        if (ItemEffectPool.Instance != null)
        {
            ItemEffectPool.Instance.SpawnSpike(position, rotation, damage, largeSpikeDuration, enemyLayer, true);
        }
    }

    #endregion

    #region Trail Visual

    private void AddTrailPoint(Vector3 point)
    {
        if (activeTrailGO == null)
        {
            activeTrailGO = new GameObject("PetraTrail");
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
        lr.endWidth = trailWidth * 0.5f;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.alignment = LineAlignment.TransformZ;
        lr.material = trailMaterial != null ? trailMaterial : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = trailColor;
        lr.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
    }

    private void CloseCurrentTrail()
    {
        if (activeTrailGO == null) return;
        PetraTrailFader fader = activeTrailGO.AddComponent<PetraTrailFader>();
        fader.Init(activeTrail, trailFadeDuration);
        activeTrailGO = null;
        activeTrail = null;
        trailPoints.Clear();
    }

    #endregion
}