using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static RoomProgressionLevel;

[System.Serializable]
public struct EnemySpawnEffectMapping
{
    public string nameContains;
    public GameObject effectPrefab;
    public float yOffset;
}

public class DungeonGenerator : MonoBehaviour
{
    [Header("Room Prefabs")]
    public Room startRoomPrefab;
    public Room[] endRoomPrefabs;

    [Header("Generation Rules")]
    public List<RoomGenerationRule> roomGenerationRules;
    public List<RoomTypeProbability> roomTypeProbabilities;

    [Header("Room Generation Rules")]
    public RoomGenerationRule[] generationRules;

    [Header("Room Progression Rules")]
    public RoomProgressionRule[] progressionRules;

    [Header("Enemy Prefabs")]
    public GameObject[] enemyPrefabs;

    [Header("Visual Effects")]
    public GameObject spawnEffectPrefab;
    public float defaultSpawnEffectYOffset;
    public EnemySpawnEffectMapping[] enemySpawnEffects;

    [Header("Door Preview")]
    public bool enableDoorPreview = true;

    [Header("Generation Settings")]
    public int minRooms = 8;
    public int maxRooms = 12;
    public int maxRoomAttempts = 15;
    [Range(0.1f, 2.0f)]
    public float repetitionPenalty = 0.8f;
    [Range(0.1f, 1.0f)]
    public float weightDecay = 0.7f;
    [Range(5f, 500f)]
    public float roomDistance = 20f;

    [Header("Player Movement Settings")]
    [Range(0.1f, 2.0f)]
    public float playerMoveDuration = 0.5f;
    [Range(1f, 10f)]
    public float playerMoveDistance = 3f;

    [Header("Door Settings")]
    [Range(0.1f, 5.0f)]
    public float doorActivateDelay = 0.5f;

    [Header("Enemy Progressive System")]
    public ProgressiveEnemySystemConfig defaultEnemyConfig;

    [Header("Dependencies")]
    [SerializeField] private RoomTransitionController roomTransitionController;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private PlayerCombatActionManager combatActionManager;
    private PlayerMovement playerMovement;

    [Header("Debug")]
    [SerializeField] private KeyCode _debugCompleteRoomKey = KeyCode.O;

    private List<Room> generatedRooms = new List<Room>();
    private int roomsGenerated = 0;
    private int targetRoomCount;
    private Transform designatedMandatoryExit = null;
    private int designatedMandatoryRoomNumber = -1;

    private Dictionary<ConnectionPoint, RoomType> doorPreviewCache = new Dictionary<ConnectionPoint, RoomType>();
    private Dictionary<RoomType, List<RoomData>> roomDataDictionary = new Dictionary<RoomType, List<RoomData>>();
    private Dictionary<RoomType, RoomType[]> generationRuleDictionary = new Dictionary<RoomType, RoomType[]>();
    private Dictionary<RoomType, RoomProgressionRule> progressionRuleDictionary = new Dictionary<RoomType, RoomProgressionRule>();
    private List<RoomProgressionRule> usedOnceRules = new List<RoomProgressionRule>();
    private Queue<Room> recentlyGeneratedPrefabs = new Queue<Room>();
    private RoomType lastGeneratedRoomType = RoomType.Normal;

    public static event Action<RoomType> OnRoomEntered;
    public static event Action<RoomType, float> OnRoomCompleted;

    private RoomProgressionRule mandatoryRoomToPlace;
    private int mandatoryRoomNumber = -1;
    private RoomProgressionRule probableMandatoryToPlace;
    private bool hasProbableMandatoryBeenGenerated = false;
    private float currentProbableMandatoryProbability;
    private const int RECENT_HISTORY_SIZE = 3;

    private float roomStartTime = 0f;
    public int CurrentRoomCount { get; private set; } = 0;
    public int currentRoomNumber { get; private set; } = 0;
    public Room CurrentRoom { get; private set; }
    public static DungeonGenerator Instance { get; private set; }
    public bool IsTransitioning { get; private set; }
    private DevilManipulationManager devilManager;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        if (DevilManipulationManager.Instance != null)
        {
            devilManager = DevilManipulationManager.Instance;
        }
        else
        {
            Debug.LogError("[DungeonGenerator] DevilManipulationManager.Instance no encontrado. La lógica del Diablo no funcionará.");
        }
    }

    void Start()
    {
        foreach (var roomProb in roomTypeProbabilities)
        {
            roomDataDictionary[roomProb.roomType] = new List<RoomData>();
            foreach (var prefab in roomProb.roomPrefabs)
            {
                roomDataDictionary[roomProb.roomType].Add(new RoomData { prefab = prefab, weight = 1.0f });
            }
        }

        foreach (var rule in generationRules)
        {
            generationRuleDictionary[rule.currentRoomType] = rule.allowedNextRoomTypes;
        }

        foreach (var rule in progressionRules)
        {
            progressionRuleDictionary[rule.roomType] = rule;
        }

        targetRoomCount = UnityEngine.Random.Range(minRooms, maxRooms + 1);

        GenerateInitialRoom();
        playerMovement = FindAnyObjectByType<PlayerMovement>();
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        statsManager = FindAnyObjectByType<PlayerStatsManager>();
        combatActionManager = FindAnyObjectByType<PlayerCombatActionManager>();

        if (AsyncMusicController.Instance != null)
        {
            AsyncMusicController.Instance.PlayMusic(MusicState.Calm);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(_debugCompleteRoomKey))
        {
            CompleteCurrentRoomShortcut();
        }
    }

    public void StartRoomTimer()
    {
        roomStartTime = Time.time;
    }

    public float EndRoomTimer()
    {
        float timeElapsed = Time.time - roomStartTime;

        roomStartTime = 0f;

        return timeElapsed;
    }

    public void CompleteCurrentRoomShortcut()
    {
        Room currentRoom = generatedRooms.LastOrDefault();
        int destroyedCount = 0;

        if (currentRoom == null || currentRoom.isEndRoom || currentRoom.isStartRoom)
        {
            return;
        }

        if (currentRoom.roomType != RoomType.Combat)
        {
            return;
        }

        EnemyManager enemyManager = currentRoom.GetComponent<EnemyManager>();

        if (enemyManager == null)
        {
            return;
        }

        float roomDetectionRadius = 30f;
        Vector3 roomPosition = currentRoom.transform.position;

        int enemyLayer = LayerMask.NameToLayer("Enemy");

        if (enemyLayer != -1)
        {
            Collider[] colliders = Physics.OverlapSphere(roomPosition, roomDetectionRadius);

            foreach (Collider col in colliders)
            {
                if (col != null && col.gameObject.layer == enemyLayer)
                {
                    Destroy(col.gameObject);
                    destroyedCount++;
                }
            }
        }
        else
        {
        }

        if (spawnEffectPrefab != null)
        {
            string effectName = spawnEffectPrefab.name;
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (GameObject obj in allObjects)
            {
                string cleanName = obj.name.Replace("(Clone)", "");

                if (cleanName == effectName && Vector3.Distance(obj.transform.position, roomPosition) < roomDetectionRadius)
                {
                    Destroy(obj);
                    destroyedCount++;
                }
            }
        }

        Destroy(enemyManager);

        ConnectionPoint entrancePoint = currentRoom.connectionPoints.FirstOrDefault(conn => conn.isConnected);

        if (entrancePoint == null)
        {
            return;
        }

        OnCombatRoomCleared(currentRoom, entrancePoint);
    }

    public GameObject GetSpawnEffectForEnemy(string enemyPrefabName, out float yOffset)
    {
        if (!string.IsNullOrEmpty(enemyPrefabName) && enemySpawnEffects != null)
        {
            foreach (var mapping in enemySpawnEffects)
            {
                if (!string.IsNullOrEmpty(mapping.nameContains) && enemyPrefabName.Contains(mapping.nameContains))
                {
                    yOffset = mapping.yOffset;
                    return mapping.effectPrefab;
                }
            }
        }

        yOffset = defaultSpawnEffectYOffset;
        return spawnEffectPrefab;
    }

    void GenerateInitialRoom()
    {
        if (startRoomPrefab == null) return;

        Room initialRoom = Instantiate(startRoomPrefab, Vector3.zero, Quaternion.identity);
        initialRoom.name = "StartRoom";
        initialRoom.isStartRoom = true;

        generatedRooms.Add(initialRoom);
        initialRoom.LockAllDoors();

        foreach (ConnectionPoint connectionPoint in initialRoom.connectionPoints)
        {
            if (connectionPoint.GetComponent<BoxCollider>() == null)
            {
                BoxCollider collider = connectionPoint.gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;

                collider.size = new Vector3(3f, 2f, 1f);

                collider.center = new Vector3(0f, 1f, 0.5f);

                connectionPoint.gameObject.AddComponent<ConnectionTrigger>();
            }
        }
    }

    Vector3 GetDirectionFromConnectionType(ConnectionType connectionType)
    {
        switch (connectionType)
        {
            case ConnectionType.North: return Vector3.forward;
            case ConnectionType.South: return Vector3.back;
            case ConnectionType.East: return Vector3.left;
            case ConnectionType.West: return Vector3.right;
            default: return Vector3.forward;
        }
    }

    float GetEscalatingProbability(int currentRoomNumber)
    {
        if (probableMandatoryToPlace == null) return 0f;

        float roomRange = probableMandatoryToPlace.maxRoomNumber - probableMandatoryToPlace.minRoomNumber;
        if (roomRange <= 0) return probableMandatoryToPlace.probability;

        float roomProgress = (currentRoomNumber - probableMandatoryToPlace.minRoomNumber) / roomRange;
        float scalingFactor = 1f + (roomProgress * 2f);

        return Mathf.Min(100f, probableMandatoryToPlace.probability * scalingFactor);
    }

    public void GenerateNextRoom(ConnectionPoint entrancePoint)
    {
        if (entrancePoint.isConnected) return;

        if (roomsGenerated >= targetRoomCount)
        {
            PlaceEndRoom(entrancePoint);
            return;
        }

        RoomType forcedType = RoomType.Normal;
        bool hasCachedType = doorPreviewCache.TryGetValue(entrancePoint, out forcedType);

        int attempts = 0;
        bool roomPlaced = false;
        HashSet<Room> failedPrefabs = new HashSet<Room>();

        while (attempts < maxRoomAttempts && !roomPlaced)
        {
            if (entrancePoint.isConnected) return;

            RoomSelectionResult selectionResult;

            if (hasCachedType)
            {
                selectionResult = GetRoomDataOfType(forcedType, failedPrefabs);
            }
            else
            {
                RoomType previousRoomType = entrancePoint.GetComponentInParent<Room>().roomType;
                selectionResult = GetRandomRoomData(previousRoomType, roomsGenerated + 1, failedPrefabs);
            }

            if (selectionResult == null || selectionResult.RoomData == null)
            {
                attempts++;
                continue;
            }

            roomPlaced = PlaceRoom(selectionResult.RoomData.prefab, selectionResult.RoomData, entrancePoint, selectionResult.ProgressionRule);

            if (roomPlaced)
                UpdateRoomWeights(selectionResult.RoomData);
            else
                failedPrefabs.Add(selectionResult.RoomData.prefab);

            attempts++;
        }
    }

    private RoomSelectionResult GetRoomDataOfType(RoomType roomType, HashSet<Room> excludedPrefabs = null)
    {
        if (!roomDataDictionary.ContainsKey(roomType) || roomDataDictionary[roomType].Count == 0)
            return null;

        var matchingRule = progressionRules
            .FirstOrDefault(r => r.GetRoomType() == roomType &&
                                 roomsGenerated + 1 >= r.minRoomNumber &&
                                 roomsGenerated + 1 <= r.maxRoomNumber &&
                                 (!r.generateOnce || !usedOnceRules.Contains(r)));

        if (matchingRule != null && matchingRule.HasCustomPrefabs())
        {
            Room customPrefab = matchingRule.GetRandomCustomPrefab();
            if (customPrefab != null && (excludedPrefabs == null || !excludedPrefabs.Contains(customPrefab)))
            {
                return new RoomSelectionResult
                {
                    RoomData = new RoomData { prefab = customPrefab, weight = 1.0f },
                    ProgressionRule = matchingRule
                };
            }
        }

        var available = roomDataDictionary[roomType]
            .Where(rd => excludedPrefabs == null || !excludedPrefabs.Contains(rd.prefab))
            .ToList();

        if (!available.Any()) return null;

        var preferred = available.Where(rd => !recentlyGeneratedPrefabs.Contains(rd.prefab)).ToList();
        var pool = preferred.Any() ? preferred : available;

        return new RoomSelectionResult
        {
            RoomData = pool[UnityEngine.Random.Range(0, pool.Count)],
            ProgressionRule = matchingRule
        };
    }

    public RoomType PredictNextRoomType(ConnectionPoint connectionPoint)
    {
        Room parentRoom = connectionPoint.GetComponentInParent<Room>();
        RoomType previousType = parentRoom != null ? parentRoom.roomType : RoomType.Normal;
        return DetermineNextRoomType(previousType, roomsGenerated + 1);
    }

    bool PlaceRoom(Room newRoomPrefab, RoomData roomData, ConnectionPoint entrancePoint, RoomProgressionRule progressionRule)
    {
        Room newRoom = Instantiate(newRoomPrefab);

        List<ConnectionPoint> exitPoints = GetAllMatchingConnectionPoints(newRoom, entrancePoint);

        if (exitPoints.Count > 0)
        {
            ConnectionPoint exitPoint = exitPoints[UnityEngine.Random.Range(0, exitPoints.Count)];

            Vector3 offset = exitPoint.transform.position - newRoom.transform.position;

            Vector3 connectionDirection = GetDirectionFromConnectionType(entrancePoint.connectionType);
            Vector3 finalPosition = entrancePoint.transform.position - offset + (connectionDirection * roomDistance);

            if (IsPositionValid(finalPosition, newRoom))
            {
                newRoom.transform.position = finalPosition;
                newRoom.name = $"Room_{roomsGenerated + 1}_{newRoomPrefab.name}";

                exitPoint.isConnected = true;
                exitPoint.connectedTo = entrancePoint.transform;
                entrancePoint.isConnected = true;
                entrancePoint.connectedTo = exitPoint.transform;

                generatedRooms.Add(newRoom);
                roomsGenerated++;
                lastGeneratedRoomType = newRoom.roomType;

                recentlyGeneratedPrefabs.Enqueue(newRoomPrefab);
                if (recentlyGeneratedPrefabs.Count > RECENT_HISTORY_SIZE)
                {
                    recentlyGeneratedPrefabs.Dequeue();
                }

                if (newRoom.roomType == RoomType.Combat)
                {
                    CombatContents contents = GetCombatContentsForRoom(newRoom.roomType, progressionRule);

                    if (contents.waves.Any() && newRoom.GetComponent<EnemyManager>() == null)
                    {
                        var enemyManager = newRoom.gameObject.AddComponent<EnemyManager>();

                        enemyManager.Initialize(
                            this,
                            newRoom,
                            contents,
                            this.spawnEffectPrefab,
                            this.enemyPrefabs
                        );
                    }
                }

                foreach (ConnectionPoint connectionPoint in newRoom.connectionPoints)
                {
                    if (!connectionPoint.isConnected && connectionPoint.GetComponent<BoxCollider>() == null)
                    {
                        BoxCollider collider = connectionPoint.gameObject.AddComponent<BoxCollider>();
                        collider.isTrigger = true;

                        collider.size = new Vector3(3f, 2f, 1f);
                        collider.center = new Vector3(0f, 1f, 0.5f);

                        connectionPoint.gameObject.AddComponent<ConnectionTrigger>();
                    }
                }

                if (progressionRule != null && progressionRule.generateOnce)
                {
                    usedOnceRules.Add(progressionRule);
                }

                return true;
            }
            else
            {
                Destroy(newRoom.gameObject);
                return false;
            }
        }
        else
        {
            Destroy(newRoom.gameObject);
            return false;
        }
    }

    bool IsPositionValid(Vector3 position, Room newRoom)
    {
        float minDistance = 5f;

        foreach (Room existingRoom in generatedRooms)
        {
            if (existingRoom != null && Vector3.Distance(position, existingRoom.transform.position) < minDistance)
            {
                return false;
            }
        }

        return true;
    }

    void PlaceEndRoom(ConnectionPoint entrancePoint)
    {
        if (endRoomPrefabs.Length == 0) return;

        int attempts = 0;
        bool roomPlaced = false;

        while (attempts < maxRoomAttempts && !roomPlaced)
        {
            Room endRoomPrefab = endRoomPrefabs[UnityEngine.Random.Range(0, endRoomPrefabs.Length)];
            Room endRoom = Instantiate(endRoomPrefab);

            List<ConnectionPoint> exitPoints = GetAllMatchingConnectionPoints(endRoom, entrancePoint);
            if (exitPoints.Count > 0)
            {
                ConnectionPoint exitPoint = exitPoints[UnityEngine.Random.Range(0, exitPoints.Count)];

                Vector3 offset = exitPoint.transform.position - endRoom.transform.position;

                Vector3 connectionDirection = GetDirectionFromConnectionType(entrancePoint.connectionType);
                Vector3 finalPosition = entrancePoint.transform.position - offset + (connectionDirection * roomDistance);

                endRoom.transform.position = finalPosition;
                endRoom.name = "EndRoom";

                exitPoint.isConnected = true;
                exitPoint.connectedTo = entrancePoint.transform;
                entrancePoint.isConnected = true;
                entrancePoint.connectedTo = exitPoint.transform;

                endRoom.isEndRoom = true;
                generatedRooms.Add(endRoom);
                roomPlaced = true;
            }
            else
            {
                Destroy(endRoom.gameObject);
            }
            attempts++;
        }
    }

    RoomSelectionResult GetRandomRoomData(RoomType previousRoomType, int currentRoomNumber, HashSet<Room> excludedPrefabs = null)
    {
        RoomProgressionRule[] currentRules = progressionRules ?? System.Array.Empty<RoomProgressionRule>();

        var mandatoryApplicableRules = currentRules
            .Where(r => r.isMandatory &&
                        currentRoomNumber >= r.minRoomNumber &&
                        currentRoomNumber <= r.maxRoomNumber &&
                        (!r.generateOnce || !usedOnceRules.Contains(r)))
            .ToList();

        if (mandatoryApplicableRules.Any())
        {
            var mandatoryRule = mandatoryApplicableRules.First();
            RoomType mandatoryType = mandatoryRule.GetRoomType();

            if (generationRuleDictionary.ContainsKey(previousRoomType) &&
                !generationRuleDictionary[previousRoomType].Contains(mandatoryType))
            {
                Debug.LogWarning($"Regla obligatoria para sala {currentRoomNumber} no es compatible con el tipo de sala anterior. Saltando.");
            }
            else
            {
                if (mandatoryRule.HasCustomPrefabs())
                {
                    Room customPrefab = mandatoryRule.GetRandomCustomPrefab();
                    if (customPrefab != null && (excludedPrefabs == null || !excludedPrefabs.Contains(customPrefab)))
                    {
                        return new RoomSelectionResult
                        {
                            RoomData = new RoomData { prefab = customPrefab, weight = 1.0f },
                            ProgressionRule = mandatoryRule
                        };
                    }
                }

                if (roomDataDictionary.ContainsKey(mandatoryType))
                {
                    var mandatoryRoomPrefabs = roomDataDictionary[mandatoryType]
                        .Where(rd => excludedPrefabs == null || !excludedPrefabs.Contains(rd.prefab))
                        .ToList();

                    if (mandatoryRoomPrefabs != null && mandatoryRoomPrefabs.Count > 0)
                    {
                        var preferredPrefabs = mandatoryRoomPrefabs
                            .Where(rd => !recentlyGeneratedPrefabs.Contains(rd.prefab))
                            .ToList();

                        if (preferredPrefabs.Count > 0)
                        {
                            return new RoomSelectionResult
                            {
                                RoomData = preferredPrefabs[UnityEngine.Random.Range(0, preferredPrefabs.Count)],
                                ProgressionRule = mandatoryRule
                            };
                        }
                        else
                        {
                            return new RoomSelectionResult
                            {
                                RoomData = mandatoryRoomPrefabs[UnityEngine.Random.Range(0, mandatoryRoomPrefabs.Count)],
                                ProgressionRule = mandatoryRule
                            };
                        }
                    }
                }
            }
        }

        var probableMandatoryRules = currentRules
            .Where(r => r.isProbableMandatory &&
                        currentRoomNumber >= r.minRoomNumber &&
                        currentRoomNumber <= r.maxRoomNumber &&
                        (!r.generateOnce || !usedOnceRules.Contains(r)))
            .ToList();

        foreach (var rule in probableMandatoryRules)
        {
            float escalatingProbability = GetEscalatingProbability(currentRoomNumber);
            if (UnityEngine.Random.Range(0f, 100f) < escalatingProbability)
            {
                RoomType probableType = rule.GetRoomType();

                if (generationRuleDictionary.ContainsKey(previousRoomType) &&
                    !generationRuleDictionary[previousRoomType].Contains(probableType))
                    continue;

                if (rule.HasCustomPrefabs())
                {
                    Room customPrefab = rule.GetRandomCustomPrefab();
                    if (customPrefab != null && (excludedPrefabs == null || !excludedPrefabs.Contains(customPrefab)))
                    {
                        return new RoomSelectionResult
                        {
                            RoomData = new RoomData { prefab = customPrefab, weight = 1.0f },
                            ProgressionRule = rule
                        };
                    }
                }

                var probableRoomPrefabs = roomDataDictionary[probableType]
                    .Where(rd => excludedPrefabs == null || !excludedPrefabs.Contains(rd.prefab))
                    .ToList();

                if (probableRoomPrefabs != null && probableRoomPrefabs.Count > 0)
                {
                    var preferredPrefabs = probableRoomPrefabs
                        .Where(rd => !recentlyGeneratedPrefabs.Contains(rd.prefab))
                        .ToList();

                    if (preferredPrefabs.Count > 0)
                    {
                        return new RoomSelectionResult
                        {
                            RoomData = preferredPrefabs[UnityEngine.Random.Range(0, preferredPrefabs.Count)],
                            ProgressionRule = rule
                        };
                    }
                    else
                    {
                        return new RoomSelectionResult
                        {
                            RoomData = probableRoomPrefabs[UnityEngine.Random.Range(0, probableRoomPrefabs.Count)],
                            ProgressionRule = rule
                        };
                    }
                }
            }
        }

        var applicableRules = currentRules
            .Where(rule =>
                !rule.isMandatory &&
                !rule.isProbableMandatory &&
                currentRoomNumber >= rule.minRoomNumber &&
                currentRoomNumber <= rule.maxRoomNumber &&
                (!rule.generateOnce || !usedOnceRules.Contains(rule)))
            .ToList();

        var progressionAllowedTypes = applicableRules.Select(p => p.GetRoomType()).Distinct().ToList();

        RoomType[] generationAllowedTypes;
        if (!generationRuleDictionary.TryGetValue(previousRoomType, out generationAllowedTypes))
        {
            generationAllowedTypes = roomDataDictionary.Keys.ToArray();
        }

        List<RoomType> validRoomTypes = new List<RoomType>();
        if (progressionAllowedTypes.Any())
            validRoomTypes = progressionAllowedTypes.Intersect(generationAllowedTypes).ToList();
        else
            validRoomTypes = generationAllowedTypes.ToList();

        validRoomTypes = ApplyPreviousRoomFilter(validRoomTypes, lastGeneratedRoomType);

        if (!validRoomTypes.Any())
        {
            Debug.LogError($"Error: No hay tipos de sala válidos para la sala {currentRoomNumber}.");
            return null;
        }

        var filteredProbabilities = roomTypeProbabilities.Where(p => validRoomTypes.Contains(p.roomType)).ToList();
        float totalProbability = filteredProbabilities.Sum(p => p.probability);

        if (totalProbability <= 0)
        {
            RoomType fallbackType = validRoomTypes[UnityEngine.Random.Range(0, validRoomTypes.Count)];
            if (!roomDataDictionary.ContainsKey(fallbackType) || roomDataDictionary[fallbackType].Count == 0)
            {
                Debug.LogError($"Error: El RoomType '{fallbackType}' no tiene prefabs.");
                return null;
            }

            var fallbackRoomDataList = roomDataDictionary[fallbackType]
                .Where(rd => excludedPrefabs == null || !excludedPrefabs.Contains(rd.prefab))
                .ToList();

            if (fallbackRoomDataList.Count == 0)
            {
                Debug.LogWarning($"Todos los prefabs del tipo '{fallbackType}' han sido excluidos.");
                return null;
            }

            var preferredFallback = fallbackRoomDataList
                .Where(rd => !recentlyGeneratedPrefabs.Contains(rd.prefab))
                .ToList();

            RoomData fallbackRoomData;
            if (preferredFallback.Count > 0)
            {
                fallbackRoomData = preferredFallback[UnityEngine.Random.Range(0, preferredFallback.Count)];
            }
            else
            {
                fallbackRoomData = fallbackRoomDataList[UnityEngine.Random.Range(0, fallbackRoomDataList.Count)];
            }

            var matchingRule = applicableRules.FirstOrDefault(r => r.GetRoomType() == fallbackType);

            return new RoomSelectionResult
            {
                RoomData = fallbackRoomData,
                ProgressionRule = matchingRule
            };
        }

        float randomValue = UnityEngine.Random.Range(0f, totalProbability);
        float currentSum = 0f;
        RoomType selectedType = RoomType.Normal;

        foreach (var roomProb in filteredProbabilities)
        {
            currentSum += roomProb.probability;
            if (randomValue <= currentSum)
            {
                selectedType = roomProb.roomType;
                break;
            }
        }

        if (!roomDataDictionary.ContainsKey(selectedType) || roomDataDictionary[selectedType].Count == 0)
        {
            Debug.LogError($"Error: El RoomType '{selectedType}' no tiene prefabs.");
            return null;
        }

        var roomDataList = roomDataDictionary[selectedType]
            .Where(rd => excludedPrefabs == null || !excludedPrefabs.Contains(rd.prefab))
            .ToList();

        if (roomDataList.Count == 0)
        {
            Debug.LogWarning($"Todos los prefabs del tipo '{selectedType}' han sido excluidos por intentos fallidos.");
            return null;
        }

        var preferredRoomData = roomDataList
            .Where(rd => !recentlyGeneratedPrefabs.Contains(rd.prefab))
            .ToList();

        var finalSelectionPool = preferredRoomData.Count > 0 ? preferredRoomData : roomDataList;

        float totalWeight = finalSelectionPool.Sum(data => data.weight);

        if (totalWeight <= 0)
        {
            ResetRoomWeights(selectedType);
            finalSelectionPool = preferredRoomData.Count > 0 ?
                roomDataDictionary[selectedType]
                    .Where(rd => excludedPrefabs == null || !excludedPrefabs.Contains(rd.prefab))
                    .Where(rd => !recentlyGeneratedPrefabs.Contains(rd.prefab))
                    .ToList()
                : roomDataList;

            totalWeight = finalSelectionPool.Sum(data => data.weight);
            if (totalWeight <= 0) return null;
        }

        float randomWeightValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeightSum = 0f;
        RoomData finalRoomData = null;

        foreach (var data in finalSelectionPool)
        {
            currentWeightSum += data.weight;
            if (randomWeightValue <= currentWeightSum)
            {
                finalRoomData = data;
                break;
            }
        }

        if (finalRoomData == null) return null;

        RoomProgressionRule finalRule = applicableRules.FirstOrDefault(r =>
            r.GetRoomType() == selectedType &&
            currentRoomNumber >= r.minRoomNumber &&
            currentRoomNumber <= r.maxRoomNumber);

        return new RoomSelectionResult
        {
            RoomData = finalRoomData,
            ProgressionRule = finalRule
        };
    }

    private CombatContents GenerateCombatContentFromProgression(RoomProgressionLevel progressiveLevel)
    {
        switch (progressiveLevel.GenerationMode)
        {
            case EnemyGenerationMode.ProceduralFromPool:
            default:
                {
                    Debug.Log($"Generando contenido SOLO PROCEDURAL (Pool) para la Sala {roomsGenerated}.");
                    int wavesToGenerate = UnityEngine.Random.Range(progressiveLevel.minWaves, progressiveLevel.maxWaves + 1);

                    CombatContents proceduralContents = new CombatContents
                    {
                        timeBetweenWaves = progressiveLevel.timeBetweenWaves,
                        waves = new List<EnemyWave>()
                    };

                    for (int i = 0; i < wavesToGenerate; i++)
                    {
                        proceduralContents.waves.Add(GenerateSingleProceduralWave(progressiveLevel));
                    }
                    return proceduralContents;
                }

            case EnemyGenerationMode.PredefinedCombination:
                return GetFullPredefinedContents(progressiveLevel);

            case EnemyGenerationMode.CombinationAndProcedural:
                {
                    if (progressiveLevel.predefinedCombinations == null || progressiveLevel.predefinedCombinations.Count == 0)
                    {
                        Debug.LogWarning($"El modo es CombinationAndProcedural, pero no hay combinaciones predefinidas. Recurriendo a ProceduralFromPool.");
                        goto case EnemyGenerationMode.ProceduralFromPool;
                    }

                    int comboIndex = UnityEngine.Random.Range(0, progressiveLevel.predefinedCombinations.Count);
                    PredefinedCombatCombination selectedCombo = progressiveLevel.predefinedCombinations[comboIndex];

                    if (selectedCombo.waves == null || selectedCombo.waves.Count == 0)
                    {
                        Debug.LogWarning("La combinación seleccionada no tiene oleadas. Recurriendo a Procedural puro.");
                        goto case EnemyGenerationMode.ProceduralFromPool;
                    }

                    int totalWaves = UnityEngine.Random.Range(progressiveLevel.minWaves, progressiveLevel.maxWaves + 1);
                    CombatContents mixedContents = new CombatContents
                    {
                        timeBetweenWaves = progressiveLevel.timeBetweenWaves,
                        waves = new List<EnemyWave>()
                    };

                    for (int i = 0; i < totalWaves; i++)
                    {
                        if (UnityEngine.Random.value < 0.5f)
                        {
                            PredefinedWave predefinedWave = selectedCombo.waves[UnityEngine.Random.Range(0, selectedCombo.waves.Count)];
                            mixedContents.waves.Add(ConvertPredefinedWaveToEnemyWave(predefinedWave));
                            Debug.Log($"Oleada {i + 1}: Predefinida (de '{selectedCombo.CombinationName}').");
                        }
                        else
                        {
                            mixedContents.waves.Add(GenerateSingleProceduralWave(progressiveLevel));
                            Debug.Log($"Oleada {i + 1}: Procedural (del Pool).");
                        }
                    }

                    return mixedContents;
                }
        }
    }

    public RoomProgressionRule SelectProgressionRule(RoomType roomType)
    {
        int roomNum = roomsGenerated + 1;

        var availableRules = progressionRules
            .Where(r => r != null)
            .Where(r => roomNum >= r.minRoomNumber && roomNum <= r.maxRoomNumber)
            .Where(r => r.roomType == roomType)
            .Where(r => !r.generateOnce || !usedOnceRules.Contains(r))
            .ToList();

        if (!availableRules.Any()) return null;

        RoomProgressionRule selectedRule = null;

        var mandatoryRules = availableRules.Where(r => r.isMandatory).ToList();
        if (mandatoryRules.Any())
        {
            selectedRule = mandatoryRules[UnityEngine.Random.Range(0, mandatoryRules.Count)];
        }
        else
        {
            float totalProbability = availableRules.Where(r => r.probability > 0).Sum(r => r.probability);
            if (totalProbability > 0)
            {
                float randomPoint = UnityEngine.Random.Range(0f, totalProbability);
                float currentSum = 0f;

                foreach (var rule in availableRules.Where(r => r.probability > 0))
                {
                    currentSum += rule.probability;
                    if (randomPoint < currentSum)
                    {
                        selectedRule = rule;
                        break;
                    }
                }
            }
        }

        if (selectedRule != null)
        {
            bool isCombatRoom = selectedRule.roomType == RoomType.Combat;

            if (isCombatRoom && (selectedRule.combatContent == null || !selectedRule.combatContent.waves.Any()))
            {
                return null;
            }

            if (selectedRule.generateOnce)
            {
                usedOnceRules.Add(selectedRule);
            }
        }

        return selectedRule;
    }

    private List<GameObject> SelectEnemiesByWeight(List<EnemyTierConfig> pool, int count)
    {
        List<GameObject> selectedEnemies = new List<GameObject>();
        if (pool == null || !pool.Any()) return selectedEnemies;

        float totalWeight = pool.Sum(e => e.SpawnWeight);

        for (int i = 0; i < count; i++)
        {
            float randomPoint = UnityEngine.Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var enemyTier in pool)
            {
                currentWeight += enemyTier.SpawnWeight;
                if (randomPoint < currentWeight)
                {
                    selectedEnemies.Add(enemyTier.EnemyPrefab);
                    break;
                }
            }
        }
        return selectedEnemies;
    }

    private EnemyWave GenerateSingleProceduralWave(RoomProgressionLevel level)
    {
        EnemyWave newWave = new EnemyWave();

        int roomsIntoLevel = currentRoomNumber - level.startRoomNumber;
        float progressionFactor = Mathf.Clamp01(roomsIntoLevel / 10f);

        int targetEnemyCount = Mathf.RoundToInt(
            Mathf.Lerp(level.minEnemyCountPerWave, level.maxEnemyCountPerWave, progressionFactor)
        );
        newWave.enemyCount = Mathf.Max(1, targetEnemyCount);

        if (level.availableEnemyPool != null && level.availableEnemyPool.Count > 0)
        {
            newWave.enemyPrefabs = SelectEnemiesByWeight(level.availableEnemyPool, newWave.enemyCount).ToArray();
        }
        else
        {
            Debug.LogWarning("El Pool de Enemigos para este nivel está vacío. No se generaron enemigos.");
            newWave.enemyPrefabs = System.Array.Empty<GameObject>();
        }

        return newWave;
    }

    private EnemyWave ConvertPredefinedWaveToEnemyWave(PredefinedWave predefinedWave)
    {
        EnemyWave newWave = new EnemyWave();
        List<GameObject> waveEnemies = new List<GameObject>();
        List<EnemySpawnMode> waveModes = new List<EnemySpawnMode>();
        int totalCount = 0;

        if (predefinedWave.enemiesInWave != null)
        {
            foreach (var detail in predefinedWave.enemiesInWave)
            {
                if (detail.EnemyPrefab != null && detail.Count > 0)
                {
                    for (int i = 0; i < detail.Count; i++)
                    {
                        waveEnemies.Add(detail.EnemyPrefab);
                        waveModes.Add(detail.SpawnMode);
                    }

                    totalCount += detail.Count;
                }
            }
        }

        newWave.enemyPrefabs = waveEnemies.ToArray();
        newWave.enemyCount = totalCount;
        newWave.spawnModes = waveModes.ToArray();

        return newWave;
    }

    private CombatContents GetFullPredefinedContents(RoomProgressionLevel level)
    {
        if (level.predefinedCombinations == null || level.predefinedCombinations.Count == 0)
        {
            Debug.LogWarning("El modo es PredefinedCombination, pero la lista de combinaciones está vacía.");
            return new CombatContents();
        }

        int randomIndex = UnityEngine.Random.Range(0, level.predefinedCombinations.Count);
        PredefinedCombatCombination selectedCombo = level.predefinedCombinations[randomIndex];

        Debug.Log($"Usando contenido PREDEFINIDO '{selectedCombo.CombinationName}' para la Sala {roomsGenerated}.");

        CombatContents contents = new CombatContents
        {
            timeBetweenWaves = selectedCombo.timeBetweenWaves,
            waves = new List<EnemyWave>()
        };

        if (selectedCombo.waves != null)
        {
            foreach (var predefinedWave in selectedCombo.waves)
            {
                contents.waves.Add(ConvertPredefinedWaveToEnemyWave(predefinedWave));
            }
        }

        return contents;
    }

    public CombatContents GetCombatContentsForRoom(RoomType roomType, RoomProgressionRule usedProgressionRule)
    {
        if (roomType != RoomType.Combat)
        {
            return new CombatContents();
        }

        int roomNum = roomsGenerated;

        if (defaultEnemyConfig != null)
        {
            RoomProgressionLevel progressiveLevel = defaultEnemyConfig.GetConfigForRoom(roomNum);

            if (progressiveLevel != null)
            {
                Debug.Log($"[DungeonGenerator] Sala {roomNum}: Usando sistema progresivo (Nivel {progressiveLevel.startRoomNumber}, Modo: {progressiveLevel.GenerationMode})");
                return GenerateCombatContentFromProgression(progressiveLevel);
            }
        }

        if (usedProgressionRule != null &&
            usedProgressionRule.combatContent != null &&
            usedProgressionRule.combatContent.waves.Any())
        {
            Debug.Log($"[DungeonGenerator] Sala {roomNum}: Usando contenido de combate de RoomProgressionRule (fallback).");
            return usedProgressionRule.combatContent;
        }

        Debug.LogWarning($"[DungeonGenerator] Sala {roomNum}: No se encontró contenido de combate. Devolviendo vacío.");
        return new CombatContents();
    }

    void UpdateRoomWeights(RoomData usedRoom)
    {
        usedRoom.repetitionCount++;
        usedRoom.weight = 1.0f / (1.0f + usedRoom.repetitionCount * repetitionPenalty);
        RoomType usedRoomType = usedRoom.prefab.roomType;

        if (roomDataDictionary.ContainsKey(usedRoomType))
        {
            foreach (var data in roomDataDictionary[usedRoomType])
            {
                if (data != usedRoom)
                {
                    data.weight = Mathf.Min(1.0f, data.weight * (1.0f + weightDecay * 0.1f));
                }
            }
        }
    }

    void ResetRoomWeights(RoomType type)
    {
        if (roomDataDictionary.ContainsKey(type))
        {
            foreach (var data in roomDataDictionary[type])
            {
                data.weight = 1.0f / (1.0f + data.repetitionCount * repetitionPenalty * 0.5f);
            }
        }
    }

    List<ConnectionPoint> GetAllMatchingConnectionPoints(Room room, ConnectionPoint targetPoint)
    {
        List<ConnectionPoint> matchingConnections = new List<ConnectionPoint>();
        ConnectionType requiredType = GetOppositeConnectionType(targetPoint.connectionType);

        foreach (ConnectionPoint conn in room.connectionPoints)
        {
            if (conn.connectionType == requiredType && !conn.isConnected)
            {
                matchingConnections.Add(conn);
            }
        }
        return matchingConnections;
    }

    ConnectionType GetOppositeConnectionType(ConnectionType type)
    {
        switch (type)
        {
            case ConnectionType.North: return ConnectionType.South;
            case ConnectionType.South: return ConnectionType.North;
            case ConnectionType.East: return ConnectionType.West;
            case ConnectionType.West: return ConnectionType.East;
            default: return ConnectionType.North;
        }
    }

    private List<RoomType> ApplyPreviousRoomFilter(List<RoomType> candidates, RoomType previousType)
    {
        if (!generationRuleDictionary.ContainsKey(previousType))
            return candidates;

        var allowed = generationRuleDictionary[previousType];
        var filtered = candidates.Where(t => allowed.Contains(t)).ToList();

        if (!filtered.Any())
        {
            Debug.LogWarning($"[DungeonGenerator] ApplyPreviousRoomFilter: Filtrar por '{previousType}' dejó 0 candidatos. Se ignora el filtro.");
            return candidates;
        }

        return filtered;
    }

    public IEnumerator TransitionToNextRoom(ConnectionPoint entrancePoint, Transform playerTransform, bool cameFromElevator = false)
    {
        IsTransitioning = true;

        if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen)
        {
            InventoryUIManager.Instance.CloseInventory();
        }

        PlayerShieldController shieldController = playerTransform.GetComponent<PlayerShieldController>();
        if (shieldController != null)
        {
            shieldController.ForceRecallShield();
        }

        const float PlayerOffsetDistance = 0.75f;

        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<PlayerMovement>();
        }

        CurrentRoomCount++;

        float timeToCleanRoom = EndRoomTimer();

        float originalPlayerY = playerTransform.position.y;

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        Room oldRoom = entrancePoint.GetComponentInParent<Room>();

        if (oldRoom != null)
        {
            DesignateMandatoryExit(oldRoom);
        }

        int oldRoomDoorIndex =
            System.Array.IndexOf(oldRoom.connectionPoints, entrancePoint);

        if (oldRoomDoorIndex != -1 &&
            oldRoom.connectionDoors.Length > oldRoomDoorIndex &&
            oldRoom.connectionDoors[oldRoomDoorIndex] != null)
        {
            oldRoom.connectionDoors[oldRoomDoorIndex].SetActive(true);
        }

        GenerateNextRoom(entrancePoint);

        if (entrancePoint.connectedTo == null)
        {
            if (oldRoom != null &&
                oldRoomDoorIndex != -1 &&
                oldRoom.connectionDoors.Length > oldRoomDoorIndex &&
                oldRoom.connectionDoors[oldRoomDoorIndex] != null)
            {
                oldRoom.connectionDoors[oldRoomDoorIndex].SetActive(false);
            }

            if (playerMovement != null)
            {
                playerMovement.SetCanMove(true);
            }

            if (combatActionManager != null)
            {
                combatActionManager.enabled = true;
            }

            IsTransitioning = false;
            yield break;
        }

        Room newRoom =
            entrancePoint.connectedTo.GetComponentInParent<Room>();

        ConnectionPoint exitPoint = null;

        if (newRoom != null)
        {
            exitPoint = newRoom.connectionPoints.FirstOrDefault(
                conn =>
                    conn.isConnected &&
                    conn.connectedTo == entrancePoint.transform);
        }

        if (newRoom == null || exitPoint == null)
        {
            if (playerMovement != null)
            {
                playerMovement.SetCanMove(true);
            }

            if (combatActionManager != null)
            {
                combatActionManager.enabled = true;
            }

            IsTransitioning = false;
            yield break;
        }

        for (int i = 0; i < newRoom.connectionPoints.Length; i++)
        {
            BoxCollider boxCollider =
                newRoom.connectionPoints[i]
                .GetComponent<BoxCollider>();

            if (boxCollider != null)
            {
                boxCollider.enabled = false;
            }
        }

        newRoom.LockAllDoors();

        int newRoomEntranceDoorIndex =
            System.Array.IndexOf(
                newRoom.connectionPoints,
                exitPoint);

        if (newRoomEntranceDoorIndex != -1 &&
            newRoom.connectionDoors.Length > newRoomEntranceDoorIndex &&
            newRoom.connectionDoors[newRoomEntranceDoorIndex] != null)
        {
            newRoom.connectionDoors[newRoomEntranceDoorIndex]
                .SetActive(false);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClearManipulationText();
        }

        if (!cameFromElevator &&
            roomTransitionController != null &&
            roomTransitionController.HasPreFadeSequence)
        {
            yield return StartCoroutine(
                roomTransitionController.RunPreFadeSequence(
                    playerTransform));
        }
        else if (!cameFromElevator)
        {
            yield return FadeController.Instance.FadeOut(
                onStart: () =>
                {
                    Vector3 directionOutOfOldRoom =
                        entrancePoint.transform.forward.normalized;

                    directionOutOfOldRoom.y = 0f;

                    Vector3 targetPosition =
                        playerTransform.position +
                        directionOutOfOldRoom *
                        playerMoveDistance;

                    targetPosition.y = originalPlayerY;

                    StartCoroutine(
                        roomTransitionController.MovePlayerSmooth(
                            playerTransform,
                            targetPosition,
                            playerMoveDuration,
                            directionOutOfOldRoom));
                },
                onComplete: null,
                respectPause: true
            );
        }

        if (exitPoint != null)
        {
            Vector3 directionIntoNewRoom =
                exitPoint.transform.forward.normalized;

            directionIntoNewRoom.y = 0f;

            Vector3 spawnPosition =
                exitPoint.transform.position +
                directionIntoNewRoom *
                PlayerOffsetDistance;

            spawnPosition.y = exitPoint.transform.position.y;

            if (playerMovement != null)
            {
                playerMovement.TeleportTo(spawnPosition);
            }
            else
            {
                playerTransform.position = spawnPosition;
            }

            roomTransitionController.SetAnimDirFromWorld(
                playerTransform, directionIntoNewRoom);
        }

        yield return FadeController.Instance.FadeIn(
            onStart: () =>
            {
                if (exitPoint == null)
                    return;

                Vector3 directionIntoNewRoom =
                    exitPoint.transform.forward.normalized;

                directionIntoNewRoom.y = 0f;

                Vector3 targetPosition =
                    playerTransform.position +
                    directionIntoNewRoom *
                    playerMoveDistance;

                targetPosition.y = playerTransform.position.y;

                StartCoroutine(
                    roomTransitionController.MovePlayerSmooth(
                        playerTransform,
                        targetPosition,
                        playerMoveDuration,
                        directionIntoNewRoom));
            },
            onComplete: () =>
            {
                StartRoomTimer();

                if (DevilManipulationManager.Instance != null)
                {
                    if (newRoom != null &&
                        newRoom.roomType == RoomType.Combat)
                    {
                        DevilManipulationManager.Instance
                            .TryActivatePendingManipulation();
                    }
                    else
                    {
                        DevilManipulationManager.Instance
                            .NotifyNonCombatRoom();
                    }
                }

                if (AsyncMusicController.Instance != null)
                {
                    if (newRoom.isEndRoom)
                    {
                        AsyncMusicController.Instance
                            .PlayMusic(MusicState.Boss);
                    }
                    else
                    {
                        switch (newRoom.roomType)
                        {
                            case RoomType.Combat:
                                AsyncMusicController.Instance
                                    .PlayMusic(MusicState.Calm);
                                break;

                            case RoomType.Shop:
                                AsyncMusicController.Instance
                                    .PlayMusic(MusicState.Shop);
                                break;

                            default:
                                AsyncMusicController.Instance
                                    .PlayMusic(MusicState.Calm);
                                break;
                        }
                    }
                }

                if (newRoom != null &&
                    newRoom.roomType == RoomType.Combat)
                {
                    var enemyManager =
                        newRoom.GetComponent<EnemyManager>();

                    if (enemyManager != null)
                    {
                        StartCoroutine(
                            enemyManager.StartCombatEncounter(
                                exitPoint));

                        if (AsyncMusicController.Instance != null)
                        {
                            AsyncMusicController.Instance
                                .PlayMusic(MusicState.Battle);
                        }
                    }

                    newRoom.SetEntrancePoint(exitPoint);

                    PreCalculateDoorTypes(newRoom);
                }
                else if (newRoom != null &&
                         newRoom.roomType == RoomType.Shop)
                {
                    if (DevilManipulationManager.Instance
                        .PendingDistortion ==
                        DevilDistortionType.SealedLuck)
                    {
                        ShopManager.Instance
                            .SetDistortionActive(
                                DevilDistortionType.SealedLuck,
                                true);
                    }
                    else
                    {
                        DevilManipulationManager.Instance
                            .TryActivatePendingManipulation();
                    }

                    newRoom.SetEntrancePoint(exitPoint);

                    newRoom.UnlockExitDoors(exitPoint);

                    PreCalculateDoorTypes(newRoom);

                    UpdateDoorPreviews(newRoom);
                }
                else
                {
                    newRoom.EventsOnFinsih();

                    newRoom.SetEntrancePoint(exitPoint);

                    newRoom.UnlockExitDoors(exitPoint);

                    PreCalculateDoorTypes(newRoom);

                    UpdateDoorPreviews(newRoom);
                }

                for (int i = 1;
                     i < newRoom.connectionPoints.Length;
                     i++)
                {
                    BoxCollider boxCollider =
                        newRoom.connectionPoints[i]
                        .GetComponent<BoxCollider>();

                    if (boxCollider != null)
                    {
                        boxCollider.enabled = true;
                    }
                }

                if (playerMovement != null)
                {
                    playerMovement.SetCanMove(true);

                    if (combatActionManager != null)
                    {
                        combatActionManager.enabled = true;
                    }

                    if (newRoom != null)
                    {
                        OnRoomEntered?.Invoke(
                            newRoom.roomType);
                    }

                    IsTransitioning = false;
                }
            },
            respectPause: true
        );
    }

    private void DesignateMandatoryExit(Room currentRoom)
    {
        int nextRoomNumber = roomsGenerated + 1;

        if (mandatoryRoomToPlace != null && nextRoomNumber == mandatoryRoomNumber && nextRoomNumber != designatedMandatoryRoomNumber)
        {
            var availableExits = currentRoom.connectionPoints.Where(cp => !cp.isConnected).ToList();

            if (availableExits.Any())
            {
                ConnectionPoint selectedExit = availableExits[UnityEngine.Random.Range(0, availableExits.Count)];
                designatedMandatoryExit = selectedExit.transform;
                designatedMandatoryRoomNumber = nextRoomNumber;
            }
        }
    }

    public void OnCombatRoomCleared(Room clearedRoom, ConnectionPoint entrancePoint)
    {
        float roomCompletionTime = 0f;

        if (clearedRoom.roomType == RoomType.Combat && UIManager.Instance != null)
        {
            roomCompletionTime = UIManager.Instance.StopAndReportTimer();
        }

        OnRoomCompleted?.Invoke(clearedRoom.roomType, roomCompletionTime);

        if (AsyncMusicController.Instance != null)
        {
            AsyncMusicController.Instance.PlayMusic(MusicState.Calm);
        }

        clearedRoom.EventsOnFinsih();

        clearedRoom.SetEntrancePoint(entrancePoint);

        UpdateDoorPreviews(clearedRoom);
    }

    public void UpdateDoorPreviews(Room room)
    {
        if (!enableDoorPreview || room == null) return;

        for (int i = 0; i < room.connectionPoints.Length; i++)
        {
            ConnectionPoint connectionPoint = room.connectionPoints[i];
            if (connectionPoint.isConnected) continue;

            if (!doorPreviewCache.TryGetValue(connectionPoint, out RoomType nextRoomType))
                continue;

            DoorPreviewIndicator indicator = connectionPoint.GetComponentInChildren<DoorPreviewIndicator>();

            if (indicator != null)
            {
                indicator.SetRoomType(nextRoomType);
            }
            else
            {
                GameObject doorObject = i < room.connectionDoors.Length ? room.connectionDoors[i] : null;
                if (doorObject != null)
                {
                    indicator = doorObject.GetComponent<DoorPreviewIndicator>();
                    if (indicator == null)
                        indicator = doorObject.AddComponent<DoorPreviewIndicator>();

                    indicator.SetRoomType(nextRoomType);
                }
            }
        }
    }

    private void PreCalculateDoorTypes(Room room)
    {
        RoomType currentRoomType = room.roomType;
        int nextRoomNum = roomsGenerated + 1;

        var exitDoors = room.connectionPoints
            .Where(cp => !cp.isConnected)
            .ToList();

        if (exitDoors.Count == 0) return;

        var mandatoryRules = progressionRules
            .Where(r => r.isMandatory &&
                        nextRoomNum >= r.minRoomNumber &&
                        nextRoomNum <= r.maxRoomNumber &&
                        (!r.generateOnce || !usedOnceRules.Contains(r)))
            .ToList();

        var probableRules = progressionRules
            .Where(r => r.isProbableMandatory &&
                        nextRoomNum >= r.minRoomNumber &&
                        nextRoomNum <= r.maxRoomNumber &&
                        (!r.generateOnce || !usedOnceRules.Contains(r)))
            .ToList();

        Dictionary<ConnectionPoint, RoomType?> assignments = new Dictionary<ConnectionPoint, RoomType?>();
        foreach (var cp in exitDoors)
            assignments[cp] = null;

        foreach (var rule in mandatoryRules)
        {
            var freeDoors = exitDoors.Where(cp => assignments[cp] == null).ToList();
            if (freeDoors.Count == 0) break;

            ConnectionPoint target = freeDoors[UnityEngine.Random.Range(0, freeDoors.Count)];
            assignments[target] = rule.GetRoomType();
        }

        foreach (var rule in probableRules)
        {
            RoomType probableType = rule.GetRoomType();

            if (generationRuleDictionary.ContainsKey(lastGeneratedRoomType) &&
                !generationRuleDictionary[lastGeneratedRoomType].Contains(probableType))
                continue;

            bool allowMultiple = rule.allowMultipleDoorsOfSameType;
            var freeDoors = exitDoors.Where(cp => assignments[cp] == null).ToList();
            if (freeDoors.Count == 0) break;

            foreach (var door in freeDoors)
            {
                if (!allowMultiple && assignments.Values.Contains(probableType))
                    break;

                float roll = UnityEngine.Random.Range(0f, 100f);
                if (roll < rule.probability)
                {
                    assignments[door] = probableType;
                    if (!allowMultiple) break;
                }
            }
        }

        foreach (var door in exitDoors)
        {
            if (assignments[door] == null)
            {
                assignments[door] = GetFallbackRoomType(currentRoomType, nextRoomNum);
            }

            doorPreviewCache[door] = assignments[door].Value;
        }
    }

    private RoomType GetFallbackRoomType(RoomType previousRoomType, int nextRoomNumber)
    {
        RoomType[] generationAllowedTypes;
        if (!generationRuleDictionary.TryGetValue(previousRoomType, out generationAllowedTypes))
            generationAllowedTypes = roomDataDictionary.Keys.ToArray();

        var filteredProbabilities = roomTypeProbabilities
            .Where(p => generationAllowedTypes.Contains(p.roomType) &&
                        roomDataDictionary.ContainsKey(p.roomType) &&
                        roomDataDictionary[p.roomType].Count > 0)
            .ToList();

        if (!filteredProbabilities.Any())
            return RoomType.Normal;

        float total = filteredProbabilities.Sum(p => p.probability);
        if (total <= 0)
            return filteredProbabilities[UnityEngine.Random.Range(0, filteredProbabilities.Count)].roomType;

        float roll = UnityEngine.Random.Range(0f, total);
        float sum = 0f;
        foreach (var p in filteredProbabilities)
        {
            sum += p.probability;
            if (roll <= sum) return p.roomType;
        }

        return filteredProbabilities[0].roomType;
    }

    private RoomType DetermineNextRoomType(RoomType previousRoomType, int nextRoomNumber)
    {
        if (mandatoryRoomToPlace != null && nextRoomNumber == mandatoryRoomNumber)
        {
            return mandatoryRoomToPlace.GetRoomType();
        }

        var probableMandatoryRules = progressionRules
            .Where(r => r.isProbableMandatory &&
                        nextRoomNumber >= r.minRoomNumber &&
                        nextRoomNumber <= r.maxRoomNumber &&
                        (!r.generateOnce || !hasProbableMandatoryBeenGenerated))
            .ToList();

        foreach (var rule in probableMandatoryRules)
        {
            float escalatingProbability = GetEscalatingProbability(nextRoomNumber);
            if (UnityEngine.Random.Range(0f, 100f) < escalatingProbability)
            {
                if (generationRuleDictionary.ContainsKey(previousRoomType) &&
                    !generationRuleDictionary[previousRoomType].Contains(rule.GetRoomType()))
                    continue;

                return rule.GetRoomType();
            }
        }

        var progressionAllowedTypes = progressionRules
            .Where(rule => nextRoomNumber >= rule.minRoomNumber &&
                          nextRoomNumber <= rule.maxRoomNumber)
            .Select(p => p.GetRoomType())
            .ToList();

        RoomType[] generationAllowedTypes;
        if (!generationRuleDictionary.TryGetValue(previousRoomType, out generationAllowedTypes))
        {
            generationAllowedTypes = roomDataDictionary.Keys.ToArray();
        }

        List<RoomType> validRoomTypes = new List<RoomType>();

        if (progressionAllowedTypes.Any())
        {
            validRoomTypes = progressionAllowedTypes.Intersect(generationAllowedTypes).ToList();
        }
        else
        {
            validRoomTypes = generationAllowedTypes.ToList();
        }

        if (!validRoomTypes.Any())
        {
            return RoomType.Normal;
        }

        var filteredProbabilities = roomTypeProbabilities
            .Where(p => validRoomTypes.Contains(p.roomType))
            .ToList();

        float totalProbability = filteredProbabilities.Sum(p => p.probability);

        if (totalProbability <= 0)
        {
            return validRoomTypes[UnityEngine.Random.Range(0, validRoomTypes.Count)];
        }

        float randomValue = UnityEngine.Random.Range(0f, totalProbability);
        float currentSum = 0f;

        foreach (var roomProb in filteredProbabilities)
        {
            currentSum += roomProb.probability;
            if (randomValue <= currentSum)
            {
                return roomProb.roomType;
            }
        }

        return validRoomTypes[0];
    }
}