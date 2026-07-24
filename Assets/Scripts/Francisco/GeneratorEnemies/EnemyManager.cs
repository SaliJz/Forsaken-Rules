using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class EnemyManager : MonoBehaviour
{
    private DungeonGenerator dungeonGenerator;
    private Room parentRoom;
    private CombatContents combatConfig;

    private GameObject[] defaultEnemyPrefabs;
    private GameObject spawnEffectPrefab;

    private List<GameObject> activeEnemies = new List<GameObject>();

    private bool isAuraActiveInThisRoom = false;
    private DevilAuraType activeAura = DevilAuraType.None;
    private ResurrectionLevel activeResurrectionLevel = ResurrectionLevel.None;
    private float auraCoveragePercent = 0f;
    private float initialHealthMultiplier = 1f;
    public event System.Action onWavesStart;

    public int ExtraWavesCount { get; private set; } = 0;

    private bool _isLastWave = false;

    private struct EnemyMultipliers
    {
        public float HealthMultiplier { get; set; }
        public float DamageMultiplier { get; set; }
        public float SpeedMultiplier { get; set; }
    }

    private void Awake()
    {
        parentRoom = GetComponentInParent<Room>();
        dungeonGenerator = FindAnyObjectByType<DungeonGenerator>();
    }

    private void OnEnable()
    {
        if (DevilManipulationManager.Instance != null)
        {
            DevilManipulationManager.Instance.OnAuraManipulationActivated += HandleAuraManipulation;
        }
    }

    private void OnDisable()
    {
        if (DevilManipulationManager.Instance != null)
        {
            DevilManipulationManager.Instance.OnAuraManipulationActivated -= HandleAuraManipulation;
        }
    }

    private void HandleAuraManipulation(DevilAuraType aura, ResurrectionLevel level, float coverage)
    {
        isAuraActiveInThisRoom = true;
        activeAura = aura;
        activeResurrectionLevel = level;
        auraCoveragePercent = coverage;

        if (aura == DevilAuraType.PartialResurrection)
        {
            initialHealthMultiplier = 0.8f;
        }
        else
        {
            initialHealthMultiplier = 1.0f;
        }

        if (DevilManipulationManager.Instance != null)
        {
            DevilManipulationManager.Instance.ResetCleanRoomsCounter();
        }
    }

    public void Initialize(DungeonGenerator dg, Room parent, CombatContents rules, GameObject effectPrefab, GameObject[] defaultEnemies)
    {
        dungeonGenerator = dg;
        parentRoom = parent;
        combatConfig = rules;
        spawnEffectPrefab = effectPrefab;
        defaultEnemyPrefabs = defaultEnemies;
    }

    public IEnumerator StartCombatEncounter(ConnectionPoint entrancePoint)
    {
        if (combatConfig == null || combatConfig.waves == null || combatConfig.waves.Count == 0)
        {
            ReportDebug("No hay configuración de combate o no hay oleadas definidas. Saltando el combate.", 2);
            if (dungeonGenerator != null)
            {
                dungeonGenerator.OnCombatRoomCleared(parentRoom, entrancePoint);
            }
            yield break;
        }

        ReportDebug($"Iniciando encuentro de combate con {combatConfig.waves.Count} oleadas. El tiempo entre olas es {combatConfig.timeBetweenWaves}s", 1);

        if (parentRoom != null)
        {
            parentRoom.LockAllDoors();
        }

        int entranceDoorIndex = System.Array.IndexOf(parentRoom.connectionPoints, entrancePoint);
        if (entranceDoorIndex != -1 && parentRoom.connectionDoors.Length > entranceDoorIndex && parentRoom.connectionDoors[entranceDoorIndex] != null)
        {
            parentRoom.connectionDoors[entranceDoorIndex].SetActive(true);
        }

        if (dungeonGenerator != null)
        {
            dungeonGenerator.StartRoomTimer();
        }

        for (int i = 0; i < combatConfig.waves.Count; i++)
        {
            EnemyWave wave = combatConfig.waves[i];

            onWavesStart?.Invoke();

            ReportDebug($"--- Spawn de la Oleada {i + 1} de {combatConfig.waves.Count} ---", 1);

            yield return StartCoroutine(SpawnEnemiesInWave(wave));

            bool isLastWave = (i == combatConfig.waves.Count - 1);
            _isLastWave = isLastWave;

            yield return StartCoroutine(WaitForAllEnemiesToDie());
            if (!isLastWave)
            {
                yield return new WaitForSeconds(combatConfig.timeBetweenWaves);
            }
        }

        if (dungeonGenerator != null)
        {
            dungeonGenerator.EndRoomTimer();
        }

        if (dungeonGenerator != null)
        {
            dungeonGenerator.OnCombatRoomCleared(parentRoom, entrancePoint);
        }
    }

    public void AddExtraWaves(int count)
    {
        if (parentRoom.currentRoomType != RoomType.Combat && parentRoom.currentRoomType != RoomType.Boss)
        {
            ReportDebug("Intentando añadir oleadas extra en una sala que no es de combate.", 2);
            return;
        }

        if (combatConfig != null)
        {
            ExtraWavesCount += count;

            if (combatConfig.waves.Count > 0)
            {
                EnemyWave baseWave = combatConfig.waves.Last();

                for (int i = 0; i < count; i++)
                {
                    EnemyWave extraWave = new EnemyWave
                    {
                        enemyPrefabs = baseWave.enemyPrefabs,
                        enemyCount = baseWave.enemyCount,
                        spawnModes = baseWave.spawnModes
                    };

                    combatConfig.waves.Insert(combatConfig.waves.Count - 1, extraWave);
                }

                ReportDebug($"Añadidas {count} oleadas extra. El total de oleadas es ahora: {combatConfig.waves.Count}", 1);
            }
        }
        else
        {
            ReportDebug("combatConfig es nulo. No se pudieron añadir oleadas extra.", 3);
        }
    }

    public void ClearExtraWaves()
    {
        ExtraWavesCount = 0;
    }

    private IEnumerator SpawnEnemiesInWave(EnemyWave wave)
    {
        GameObject[] prefabsToUse = wave.enemyPrefabs.Length > 0 ? wave.enemyPrefabs : defaultEnemyPrefabs;
        int enemyCount = wave.enemyCount;

        if (wave.enemyPrefabs.Length > 0)
        {
            enemyCount = wave.enemyPrefabs.Length;
            ReportDebug($"Wave predefinida detectada: spawneando exactamente {enemyCount} enemigos.", 1);
        }

        if (prefabsToUse == null || prefabsToUse.Length == 0 || enemyCount == 0)
        {
            ReportDebug("No hay prefabs válidos o enemyCount es 0. Saltando wave.", 2);
            yield break;
        }

        var finalSpawnList = new List<(GameObject prefab, Vector3 position)>();
        List<(GameObject effect, Vector3 pos)> allEffects = new List<(GameObject, Vector3)>();

        var specificSpawnUsageCount = new Dictionary<SpecificSpawnPoint, int>();

        for (int i = 0; i < enemyCount; i++)
        {
            GameObject prefab = prefabsToUse[i % prefabsToUse.Length];

            EnemySpawnMode mode = (wave.spawnModes != null && i < wave.spawnModes.Length)
                ? wave.spawnModes[i]
                : EnemySpawnMode.General;

            Vector3 calculatedPosition = Vector3.zero;
            bool positionFound = false;

            if (mode == EnemySpawnMode.Specific && prefab != null && parentRoom != null)
            {
                SpecificSpawnPoint spawnPointGroup = parentRoom.GetSpecificSpawnPointForEnemy(prefab);
                Transform[] availableTransforms = spawnPointGroup != null ? spawnPointGroup.spawnPoints : null;

                if (availableTransforms != null && availableTransforms.Length > 0)
                {
                    if (!specificSpawnUsageCount.ContainsKey(spawnPointGroup))
                    {
                        specificSpawnUsageCount[spawnPointGroup] = 0;
                    }

                    int indexToUse = specificSpawnUsageCount[spawnPointGroup] % availableTransforms.Length;
                    Transform targetTransform = availableTransforms[indexToUse];

                    if (targetTransform != null)
                    {
                        calculatedPosition = targetTransform.position;
                        positionFound = true;
                        specificSpawnUsageCount[spawnPointGroup]++;
                    }
                }

                if (!positionFound)
                {
                    ReportDebug($"No se encontraron puntos Specific para el enemigo '{prefab.name}'. Pasando a spawn general.", 2);
                }
            }

            if (!positionFound)
            {
                if (parentRoom != null && parentRoom.spawnAreas != null && parentRoom.spawnAreas.Length > 0)
                {
                    BoxCollider spawnArea = parentRoom.spawnAreas[UnityEngine.Random.Range(0, parentRoom.spawnAreas.Length)];

                    calculatedPosition = new Vector3(
                        UnityEngine.Random.Range(spawnArea.bounds.min.x, spawnArea.bounds.max.x),
                        spawnArea.transform.position.y,
                        UnityEngine.Random.Range(spawnArea.bounds.min.z, spawnArea.bounds.max.z)
                    );
                }
                else
                {
                    calculatedPosition = transform.position;
                }
            }

            finalSpawnList.Add((prefab, calculatedPosition));

            float effectYOffset = 0f;
            GameObject effectToUse = dungeonGenerator != null
                ? dungeonGenerator.GetSpawnEffectForEnemy(prefab != null ? prefab.name : null, out effectYOffset)
                : spawnEffectPrefab;

            Vector3 effectPosition = calculatedPosition + new Vector3(0f, effectYOffset, 0f);

            GameObject effect = effectToUse != null
                ? Instantiate(effectToUse, effectPosition, Quaternion.identity)
                : null;

            allEffects.Add((effect, calculatedPosition));
        }

        yield return new WaitForSeconds(2.0f);

        int totalFinal = finalSpawnList.Count;

        List<int> auraIndices = new List<int>();
        if (isAuraActiveInThisRoom && activeAura != DevilAuraType.None)
        {
            int numAuraEnemies = Mathf.CeilToInt(totalFinal * auraCoveragePercent);
            ReportDebug($"Intentando aplicar {activeAura} a {numAuraEnemies} de {totalFinal} enemigos.", 1);

            auraIndices = Enumerable.Range(0, totalFinal)
                .OrderBy(x => UnityEngine.Random.value)
                .Take(numAuraEnemies)
                .ToList();
        }

        for (int i = 0; i < totalFinal; i++)
        {
            if (i < allEffects.Count && allEffects[i].effect != null)
            {
                Destroy(allEffects[i].effect);
            }

            var (enemyPrefab, spawnPos) = finalSpawnList[i];
            GameObject newEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

            ReportDebug($"Spawneado enemigo {i + 1}/{totalFinal}: {enemyPrefab.name} en {spawnPos}", 1);

            if (newEnemy != null)
            {
                if (!newEnemy.activeSelf) newEnemy.SetActive(true);

                EnemyHealth healthComponent = null;
                bool hasHealthComponent = newEnemy.TryGetComponent(out healthComponent);

                if (auraIndices.Contains(i))
                {
                    EnemyAuraManager auraManager = newEnemy.AddComponent<EnemyAuraManager>();
                    auraManager.ApplyAura(activeAura, activeResurrectionLevel);

                    if (hasHealthComponent)
                    {
                        healthComponent.SetInitialHealthMultiplier(initialHealthMultiplier);
                    }
                }

                if (hasHealthComponent)
                {
                    if (!healthComponent.enabled) healthComponent.enabled = true;

                    activeEnemies.Add(newEnemy);
                    healthComponent.OnDeath += (enemyGO) => OnEnemyDeath(enemyGO);
                }
                else
                {
                    ReportDebug($"El enemigo '{newEnemy.name}' no tiene 'EnemyHealth'. No contará para limpieza.", 3);
                }
            }
        }
    }

    private void OnEnemyDeath(GameObject enemyGO)
    {
        activeEnemies.Remove(enemyGO);

        if (activeEnemies.Count == 0 && _isLastWave)
        {
            SlowMotion.Instance?.TriggerSlowMotion();
        }
    }

    private IEnumerator WaitForAllEnemiesToDie()
    {
        while (activeEnemies.Count > 0)
        {
            yield return null;
        }
    }

    public void ResetAuraStatus()
    {
        isAuraActiveInThisRoom = false;
        activeAura = DevilAuraType.None;
        activeResurrectionLevel = ResurrectionLevel.None;
        auraCoveragePercent = 0f;
        initialHealthMultiplier = 1.0f;
    }

    private void OnDrawGizmos()
    {
        if (parentRoom != null && parentRoom.spawnAreas != null)
        {
            Gizmos.color = Color.red;
            foreach (var area in parentRoom.spawnAreas)
            {
                if (area != null)
                {
                    Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
                }
            }
        }
    }

    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[EnemyManager] {message}");
                break;
            case 2:
                Debug.LogWarning($"[EnemyManager] {message}");
                break;
            case 3:
                Debug.LogError($"[EnemyManager] {message}");
                break;
            default:
                Debug.Log($"[EnemyManager] {message}");
                break;
        }
    }
}