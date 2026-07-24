using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemEffectPool : MonoBehaviour
{
    #region Singleton

    public static ItemEffectPool Instance { get; private set; }

    #endregion

    #region Inspector Fields

    [Header("Dash Fire Pool")]
    [SerializeField] private GameObject dashCirclePrefab;
    [SerializeField] private int dashPoolSize = 8;

    [Header("Petra Spike Pool (Small - Shield)")]
    [SerializeField] private GameObject smallSpikePrefab;
    [SerializeField] private int smallSpikePoolSize = 15;

    [Header("Petra Spike Pool (Large - Melee)")]
    [SerializeField] private GameObject largeSpikePrefab;
    [SerializeField] private int largeSpikePoolSize = 10;

    [Header("Kai Wave Pool")]
    [SerializeField] private GameObject kaiWavePrefab;
    [SerializeField] private int kaiWavePoolSize = 9;

    #endregion

    #region Private Fields

    private Queue<DashFire> dashAvailable = new Queue<DashFire>();
    private List<DashFire> dashAll = new List<DashFire>();

    private Queue<PetraSpike> smallSpikeAvailable = new Queue<PetraSpike>();
    private List<PetraSpike> smallSpikeAll = new List<PetraSpike>();

    private Queue<PetraSpike> largeSpikeAvailable = new Queue<PetraSpike>();
    private List<PetraSpike> largeSpikeAll = new List<PetraSpike>();

    private Queue<KaiWave> kaiWaveAvailable = new Queue<KaiWave>();
    private List<KaiWave> kaiWaveAll = new List<KaiWave>();

    private Vector3 _lastKnownShieldLaunchDir = Vector3.forward;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        InitializeDashPool();
        InitializeSmallSpikePool();
        InitializeLargeSpikePool();
        InitializeKaiWavePool();
    }

    #endregion

    #region Initialization

    private void InitializeDashPool()
    {
        if (dashCirclePrefab == null) return;
        for (int i = 0; i < dashPoolSize; i++)
        {
            GameObject go = Instantiate(dashCirclePrefab, Vector3.zero, Quaternion.identity, transform);
            DashFire circle = go.GetComponent<DashFire>();
            go.SetActive(false);
            dashAvailable.Enqueue(circle);
            dashAll.Add(circle);
        }
    }

    private void InitializeSmallSpikePool()
    {
        if (smallSpikePrefab == null) return;
        for (int i = 0; i < smallSpikePoolSize; i++)
        {
            GameObject go = Instantiate(smallSpikePrefab, Vector3.zero, Quaternion.identity, transform);
            PetraSpike spike = go.GetComponent<PetraSpike>();
            go.SetActive(false);
            smallSpikeAvailable.Enqueue(spike);
            smallSpikeAll.Add(spike);
        }
    }

    private void InitializeLargeSpikePool()
    {
        if (largeSpikePrefab == null) return;
        for (int i = 0; i < largeSpikePoolSize; i++)
        {
            GameObject go = Instantiate(largeSpikePrefab, Vector3.zero, Quaternion.identity, transform);
            PetraSpike spike = go.GetComponent<PetraSpike>();
            go.SetActive(false);
            largeSpikeAvailable.Enqueue(spike);
            largeSpikeAll.Add(spike);
        }
    }

    private void InitializeKaiWavePool()
    {
        if (kaiWavePrefab == null) return;
        for (int i = 0; i < kaiWavePoolSize; i++)
        {
            GameObject go = Instantiate(kaiWavePrefab, Vector3.zero, Quaternion.identity, transform);
            KaiWave wave = go.GetComponent<KaiWave>();
            go.SetActive(false);
            kaiWaveAvailable.Enqueue(wave);
            kaiWaveAll.Add(wave);
        }
    }

    #endregion

    #region Ground Detection

    private float GetGroundY(Vector3 worldPosition)
    {
        int groundLayer = 1 << LayerMask.NameToLayer("Ground");
        Vector3 origin = new Vector3(worldPosition.x, worldPosition.y + 5f, worldPosition.z);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 15f, groundLayer))
            return hit.point.y;

        Vector3 belowOrigin = new Vector3(worldPosition.x, worldPosition.y - 2f, worldPosition.z);
        if (Physics.Raycast(belowOrigin, Vector3.up, out RaycastHit hitUp, 10f, groundLayer))
            return hitUp.point.y;

        return worldPosition.y;
    }

    #endregion

    #region Dash Fire Methods

    public void SpawnDashFire(Vector3 position, float damagePerSecond, float expandDuration, float maxRadius,
                      float stayDuration, float tickInterval, LayerMask enemyLayer)
    {
        DashFire circle;

        if (dashAvailable.Count > 0)
        {
            circle = dashAvailable.Dequeue();
        }
        else
        {
            circle = dashAll[0];
            circle.ForceReturn();
        }

        circle.Activate(position, damagePerSecond, expandDuration, maxRadius,
                        stayDuration, tickInterval, enemyLayer,
                        () => ReturnDashFire(circle));
    }

    public void ReturnDashFire(DashFire circle)
    {
        circle.gameObject.SetActive(false);
        if (!dashAvailable.Contains(circle)) dashAvailable.Enqueue(circle);
    }

    #endregion

    #region Petra Spike Methods

    private PetraSpike GetSpikeFromPool(bool isLargeSpike)
    {
        Queue<PetraSpike> targetQueue = isLargeSpike ? largeSpikeAvailable : smallSpikeAvailable;
        List<PetraSpike> targetList = isLargeSpike ? largeSpikeAll : smallSpikeAll;
        GameObject prefab = isLargeSpike ? largeSpikePrefab : smallSpikePrefab;

        if (targetQueue.Count > 0)
            return targetQueue.Dequeue();

        GameObject go = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
        PetraSpike newSpike = go.GetComponent<PetraSpike>();
        targetList.Add(newSpike);
        return newSpike;
    }

    public void SpawnSpike(Vector3 position, Quaternion rotation, float damage, float lifetime, LayerMask enemyLayer, bool isLargeSpike, float scale = 1f)
    {
        PetraSpike spike = GetSpikeFromPool(isLargeSpike);

        float groundY = GetGroundY(position);
        float sinkAmount = isLargeSpike ? 0.3f : 0.15f;
        Vector3 finalPos = new Vector3(position.x, groundY - sinkAmount, position.z);

        spike.transform.SetPositionAndRotation(finalPos, rotation);
        spike.transform.localScale = Vector3.one * scale;
        spike.gameObject.SetActive(true);

        spike.Initialize(damage, lifetime, enemyLayer, isLargeSpike, () =>
        {
            spike.transform.localScale = Vector3.one;
            ReturnSpike(spike, isLargeSpike);
        });
    }

    public void ReturnSpike(PetraSpike spike, bool isLargeSpike)
    {
        spike.gameObject.SetActive(false);
        Queue<PetraSpike> targetQueue = isLargeSpike ? largeSpikeAvailable : smallSpikeAvailable;

        if (!targetQueue.Contains(spike))
            targetQueue.Enqueue(spike);
    }

    public PetraSpike SpawnSpikeWithScale(Vector3 position, Quaternion rotation, float damage, float lifetime,
        LayerMask enemyLayer, bool isLargeSpike, float scale = 1f)
    {
        Queue<PetraSpike> targetQueue = isLargeSpike ? largeSpikeAvailable : smallSpikeAvailable;
        List<PetraSpike> targetList = isLargeSpike ? largeSpikeAll : smallSpikeAll;

        PetraSpike spike;

        if (targetQueue.Count > 0)
        {
            spike = targetQueue.Dequeue();
        }
        else
        {
            GameObject prefab = isLargeSpike ? largeSpikePrefab : smallSpikePrefab;
            GameObject go = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            spike = go.GetComponent<PetraSpike>();
            targetList.Add(spike);
        }

        float groundY = GetGroundY(position);
        float sinkAmount = isLargeSpike ? 0.3f : 0.15f;
        Vector3 finalPos = new Vector3(position.x, groundY - sinkAmount, position.z);

        spike.transform.SetPositionAndRotation(finalPos, rotation);
        spike.transform.localScale = Vector3.one * scale;
        spike.gameObject.SetActive(true);

        spike.Initialize(damage, lifetime, enemyLayer, isLargeSpike, () =>
        {
            spike.transform.localScale = Vector3.one;
            ReturnSpike(spike, isLargeSpike);
        });

        return spike;
    }

    #endregion

    #region Kai Wave Methods

    public void RegisterShieldLaunchDirection(Vector3 dir)
    {
        if (dir != Vector3.zero)
            _lastKnownShieldLaunchDir = dir.normalized;
    }

    public void SpawnKaiShieldWaves(
        Vector3 originPosition,
        float damage,
        float speed,
        float maxWidth,
        float growthDuration,
        float totalDuration,
        LayerMask enemyLayer)
    {
        Vector3 launchDir = _lastKnownShieldLaunchDir;
        float groundY = GetGroundY(originPosition);
        Vector3 groundOrigin = new Vector3(originPosition.x, groundY + 0.05f, originPosition.z);

        Vector3 right = Vector3.Cross(Vector3.up, launchDir).normalized;

        Vector3[] directions = new Vector3[]
        {
            right,
            -right,
            -launchDir
        };

        foreach (Vector3 dir in directions)
        {
            KaiWave wave = GetKaiWaveFromPool();
            if (wave == null) continue;

            wave.Activate(
                groundOrigin,
                dir,
                damage,
                speed,
                maxWidth,
                growthDuration,
                totalDuration,
                enemyLayer,
                () => ReturnKaiWave(wave)
            );
        }
    }

    public void SpawnKaiMeleeWave(
        Vector3 originPosition,
        Vector3 backDirection,
        float damage,
        float speed,
        float maxWidth,
        float duration,
        LayerMask enemyLayer)
    {
        KaiWave wave = GetKaiWaveFromPool();
        if (wave == null) return;

        float groundY = GetGroundY(originPosition);
        Vector3 groundOrigin = new Vector3(originPosition.x, groundY + 0.05f, originPosition.z);

        wave.Activate(
            groundOrigin,
            backDirection,
            damage,
            speed,
            maxWidth,
            duration,
            duration,
            enemyLayer,
            () => ReturnKaiWave(wave)
        );
    }

    public void SpawnKaiDashWave(
        Vector3 originPosition,
        Vector3 dashDirection,
        float damage,
        float speed,
        float maxWidth,
        float growthDuration,
        float totalDuration,
        LayerMask enemyLayer)
    {
        KaiWave wave = GetKaiWaveFromPool();
        if (wave == null) return;

        float groundY = GetGroundY(originPosition);
        Vector3 groundOrigin = new Vector3(originPosition.x, groundY + 0.05f, originPosition.z);

        wave.Activate(
            groundOrigin,
            dashDirection,
            damage,
            speed,
            maxWidth,
            growthDuration,
            totalDuration,
            enemyLayer,
            () => ReturnKaiWave(wave)
        );
    }

    private KaiWave GetKaiWaveFromPool()
    {
        if (kaiWaveAvailable.Count > 0)
            return kaiWaveAvailable.Dequeue();

        if (kaiWaveAll.Count > 0)
        {
            KaiWave oldest = kaiWaveAll[0];
            oldest.ForceReturn();
            if (kaiWaveAvailable.Count > 0)
                return kaiWaveAvailable.Dequeue();
        }

        Debug.LogWarning("[ItemEffectPool] Pool de KaiWave agotado y sin candidatos para reciclar.");
        return null;
    }

    private void ReturnKaiWave(KaiWave wave)
    {
        wave.gameObject.SetActive(false);
        if (!kaiWaveAvailable.Contains(wave))
            kaiWaveAvailable.Enqueue(wave);
    }

    #endregion
}