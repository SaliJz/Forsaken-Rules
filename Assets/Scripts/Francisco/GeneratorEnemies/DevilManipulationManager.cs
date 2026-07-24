using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using Unity.Mathematics;

public class DevilManipulationManager : MonoBehaviour
{
    public static DevilManipulationManager Instance { get; private set; }

    [SerializeField] private DevilConfiguration config;

    private PlayerHealth playerHealthManager;

    private int roomsSinceLastManipulation = 999;
    private int cleanRoomsCounter = 0;

    private List<DevilDistortionType> availableDistortions;
    private List<DevilDistortionType> allDistortionTypes;

    public event Action<DevilAuraType, ResurrectionLevel, float> OnAuraManipulationActivated;
    public event Action<DevilDistortionType> OnDistortionActivated;

    private bool isManipulationActive = false;
    private string activeManipulationName = "Ninguna";

    private float damageReceivedInCurrentRoom = 0f;
    private float maxDamageForCondition = 10f; 
    private int roomsWithLowDamage = 0; 
    private float maxTimeForCondition4 = 180f;

    private bool isManipulationPending = false;
    private bool isAuraPending = false;
    private DevilAuraType pendingAuraType = DevilAuraType.None;
    private ResurrectionLevel pendingResurrectionLevel = ResurrectionLevel.None;
    private float pendingAuraCoverage = 0f;
    private DevilDistortionType pendingDistortionType = DevilDistortionType.None;
    public DevilDistortionType PendingDistortion => pendingDistortionType;

    public float EnemySpeedMultiplier { get; private set; } = 1.0f;
    public float EnemyDamageMultiplier { get; private set; } = 1.0f;
    private void Start()
    {
        if (playerHealthManager != null)
        {
            playerHealthManager.OnDamageReceived += OnPlayerDamaged;
        }

        DungeonGenerator.OnRoomCompleted += RoomCompleted;
    }

    private void OnDestroy()
    {
        if (playerHealthManager != null)
        {
            playerHealthManager.OnDamageReceived -= OnPlayerDamaged;
        }

        DungeonGenerator.OnRoomCompleted -= RoomCompleted;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            playerHealthManager = FindAnyObjectByType<PlayerHealth>();
            InitializeDistortions();
        }
    }

    public void NotifyNonCombatRoom()
    {
        if (roomsSinceLastManipulation != 999)
        {
            roomsSinceLastManipulation++;
        }

        ReportDebug($"Sala no-combate detectada. roomsSinceLastManipulation={roomsSinceLastManipulation}.", 2);
    }

    public string GetCurrentManipulationStatus()
    {
        if (!isManipulationActive)
        {
            return "Estado: Normal";
        }

        if (activeManipulationName.Contains("Aura") || activeManipulationName.Contains("Frenesí") || activeManipulationName.Contains("Endurecimiento"))
        {
            if (pendingAuraType == DevilAuraType.PartialResurrection) 
            {
                return $"MANIPULACIÓN: {activeManipulationName} (Nivel {(int)pendingResurrectionLevel})";
            }

            return $"MANIPULACIÓN: Aura de {activeManipulationName}";
        }
        else 
        {
            return $"MANIPULACIÓN: Distorsión '{activeManipulationName}'";
        }

    }

    public string GetDistortionName(DevilDistortionType distortion)
    {
        switch (distortion)
        {
            case DevilDistortionType.AbyssalConfusion:
                return "Confusión Abisal";
            case DevilDistortionType.FloorOfTheDamned:
                return "Suelo de los Condenados";
            case DevilDistortionType.DeceptiveDarkness:
                return "Oscuridad Engañosa";
            case DevilDistortionType.SealedLuck:
                return "Suerte Sellada";
            case DevilDistortionType.WitheredBloodthirst:
                return "Sed de Sangre Agostada";
            case DevilDistortionType.InfernalJudgement:
                return "Juicio Infernal";
            default:
                return "Desconocida";
        }
    }

    public string GetAuraName(DevilAuraType aura)
    {
        switch (aura)
        {
            case DevilAuraType.Frenzy:
                return "Frenesí";
            case DevilAuraType.Hardening:
                return "Endurecimiento";
            case DevilAuraType.Stunning:
                return "Aturdimiento";
            case DevilAuraType.Explosive: 
                return "Explosiva";
            case DevilAuraType.PartialResurrection:
                return "Resurrección Parcial";
            default:
                return "Desconocida";
        }
    }

    public void NotifyRoomCleaned(float timeToCleanRoom)
    {
        bool isCleanRoom = timeToCleanRoom <= 180f;

        if (isCleanRoom)
        {
            cleanRoomsCounter++;
            ReportDebug($"Sala limpiada en {timeToCleanRoom:F1}s. cleanRoomsCounter={cleanRoomsCounter}.", 1);
        }
        else
        {
            ReportDebug($"Tiempo de sala ({timeToCleanRoom:F1}s) > 180s. Contador de salas limpias NO se incrementa.", 2);
        }

        if (roomsSinceLastManipulation != 999)
        {
            roomsSinceLastManipulation++;
        }

        CheckManipulationConditions();
    }

    public void OnPlayerDamaged(float damage)
    {
        damageReceivedInCurrentRoom += damage;
        ReportDebug($"Daño recibido en sala actual: {damageReceivedInCurrentRoom:F1}", 1);
    }

    public void RelicAcquired()
    {
        if (CheckRelicAndHealthCondition())
        {
            TryActivateManipulation();
            ReportDebug("Condición 1 (Reliquia + Vida >= 25%) cumplida. Intentando Manipulación.", 2);
        }
        else
        {
            ReportDebug($"Condición 1 fallida (Vida: {playerHealthManager.CurrentHealthPercent:P0} < 25% o tirada 50% fallida).", 2);
        }
    }

    public void PactMade()
    {
        ReportDebug("Condición 3 (Pacto) cumplida. Intentando Manipulación.", 1);
        TryActivateManipulation();
    }

    public void RoomCompleted(RoomType roomType, float timeSpentInRoom)
    {
        roomsSinceLastManipulation++;
        if (isManipulationActive)
        {
            if (roomsSinceLastManipulation >= 2)
            {
                isManipulationActive = false;
                roomsSinceLastManipulation = 0;
                ReportDebug("Cooldown de Manipulación del Diablo finalizado.", 1);
            }
            return; 
        }

        bool isCombatRoom = roomType == RoomType.Combat;

        if (isCombatRoom)
        {
            bool lowDamageInRoom = damageReceivedInCurrentRoom <= maxDamageForCondition;

            if (lowDamageInRoom)
            {
                roomsWithLowDamage++;
            }
            else
            {
                roomsWithLowDamage = 0;
                cleanRoomsCounter = 0;
                ReportDebug("Daño alto (>10) recibido. Reiniciando contador 4 (Tiempo).", 1);
            }

            if (roomsWithLowDamage >= 2)
            {
                ReportDebug("Condición 2 (Daño < 10 en 2 salas) cumplida. Intentando Manipulación.", 1);
                TryActivateManipulation();
                roomsWithLowDamage = 0; 
            }

            if (timeSpentInRoom <= maxTimeForCondition4)
            {
                cleanRoomsCounter++;
            }
            else
            {
                cleanRoomsCounter = 0;
                ReportDebug($"Sala completada tarde. Reiniciando contador 4 (Tiempo).", 1);
            }

            if (cleanRoomsCounter >= 2)
            {
                ReportDebug("Condición 4 (2 salas seguidas en <= 3 min) cumplida. Intentando Manipulación.", 1);
                TryActivateManipulation();
            }
        }
        else
        {
            roomsWithLowDamage = 0;
            cleanRoomsCounter = 0;
        }

        damageReceivedInCurrentRoom = 0f;
    }

    private void TryActivateManipulation()
    {
        if (isManipulationPending) return;

        if (UnityEngine.Random.value < 0.5f)
        {
            isManipulationPending = true;

            ChooseManipulationType();

            cleanRoomsCounter = 0;
            roomsWithLowDamage = 0;

            ReportDebug($"¡ACTIVACIÓN DEL DIABLO! Condición cumplida. Manipulación pendiente para la siguiente sala.", 2);
        }
        else
        {
            ReportDebug($"Condición cumplida, pero la tirada del Diablo falló (50%).", 1);
        }
    }

    private void ChooseManipulationType()
    {
        if (UnityEngine.Random.Range(0f, 1f) < config.AuraProbability)
        {
            pendingAuraType = ChooseRandomAura();
            pendingResurrectionLevel = ChooseRandomResurrectionLevel();
            pendingAuraCoverage = config.AuraEnemyCoveragePercent; 

            isAuraPending = true;
            ReportDebug($"Manipulación pendiente: Aura '{pendingAuraType}' (Nivel {pendingResurrectionLevel}) guardada.", 1);
        }
        else
        {
            pendingDistortionType = ChooseRandomDistortion();

            isAuraPending = false;
            ReportDebug($"Manipulación pendiente: Distorsión '{pendingDistortionType}' guardada.", 1);
        }
    }

    public void CheckAndRerollSealedLuck(bool isEnteringShopRoom)
    {
        if (isManipulationPending && pendingDistortionType == DevilDistortionType.SealedLuck)
        {
            if (isEnteringShopRoom)
            {
                ReportDebug("Suerte Sellada: Sala de Tienda detectada. Se activa localmente.", 1);
            }
            else
            {
                ReportDebug("Suerte Sellada: Sala de Combate/Otro tipo detectada. Se descarta la manipulación y se elige una nueva.", 2);

                isManipulationPending = false;
                pendingDistortionType = DevilDistortionType.None;

                ChooseManipulationType();

                TryActivatePendingManipulation();
            }
        }
    }

    public void ApplyAura(DevilAuraType type)
    {
        pendingAuraType = type;
    }

    private DevilAuraType ChooseRandomAura()
    {
        var allAuras = Enum.GetValues(typeof(DevilAuraType))
                                 .Cast<DevilAuraType>()
                                 .Where(a => a != DevilAuraType.None && a != DevilAuraType.PartialResurrection)
                                 .ToList();

        if (allAuras.Count == 0) return DevilAuraType.None;

        return allAuras[UnityEngine.Random.Range(0, allAuras.Count)];
    }

    private ResurrectionLevel ChooseRandomResurrectionLevel()
    {
        if (UnityEngine.Random.Range(0f, 1f) < config.ResurrectionChance)
        {
            return (ResurrectionLevel)UnityEngine.Random.Range(1, 4);
        }
        return ResurrectionLevel.None;
    }

    private DevilDistortionType ChooseRandomDistortion()
    {
        if (availableDistortions.Count == 0)
        {
            availableDistortions = allDistortionTypes.Where(d => d != DevilDistortionType.None).ToList();
            ReportDebug("Todas las distorsiones han ocurrido. La lista ha sido reestablecida.", 1);
        }

        float totalWeight = availableDistortions.Sum(type => GetDistortionChance(type));

        if (totalWeight <= 0) return DevilDistortionType.None;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;
        DevilDistortionType chosenDistortion = DevilDistortionType.None;

        foreach (var type in availableDistortions)
        {
            float chance = GetDistortionChance(type);
            cumulativeWeight += chance;
            if (roll < cumulativeWeight)
            {
                chosenDistortion = type;
                break;
            }
        }

        if (chosenDistortion != DevilDistortionType.None)
        {
            availableDistortions.Remove(chosenDistortion);
        }

        return chosenDistortion;
    }

    public Color GetCurrentLevelColor()
    {
        return Color.white;
    }
    private void OnEnable()
    {
        PlayerHealth.OnPlayerInstantiated += HandlePlayerRegistration;
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerInstantiated -= HandlePlayerRegistration;
    }

    private void InitializeDistortions()
    {
        allDistortionTypes = Enum.GetValues(typeof(DevilDistortionType))
                                 .Cast<DevilDistortionType>()
                                 .Where(d => d != DevilDistortionType.None)
                                 .ToList();

        availableDistortions = new List<DevilDistortionType>(allDistortionTypes);
    }

    private void HandlePlayerRegistration(PlayerHealth player)
    {
        playerHealthManager = player;
        ReportDebug("Referencia de PlayerHealth obtenida por registro de evento.", 1);
    }

    public void OnRoomEntered()
    {
        isManipulationActive = false;
        activeManipulationName = "Ninguna";
        if (UIManager.Instance != null) UIManager.Instance.ClearManipulationText();

        if (roomsSinceLastManipulation < config.CooldownInRooms)
        {
            roomsSinceLastManipulation++;
            ReportDebug($"Cooldown del Diablo: {roomsSinceLastManipulation} / {config.CooldownInRooms} salas completadas.", 1);
        }

        TryActivatePendingManipulation();
    }

    public void CheckManipulationConditions()
    {
        if (roomsSinceLastManipulation < config.CooldownInRooms)
        {
            return;
        }

        bool condition1 = CheckRelicAndHealthCondition();
        bool condition2 = CheckDamageCondition();
        bool condition3 = CheckPactCondition();
        bool condition4 = CheckCleanRoomsCondition();

        if (condition1 || condition2 || condition3 || condition4)
        {
            ApplyDevilManipulation();
        }
    }

    public void ResetCleanRoomsCounter()
    {
        cleanRoomsCounter = 0;
        ReportDebug("Contador de Salas Limpias reseteado.", 1);
    }

    private bool CheckRelicAndHealthCondition()
    {
        if (playerHealthManager != null && playerHealthManager.CurrentHealthPercent >= 0.25f)
        {
            return UnityEngine.Random.value < 0.5f;
        }
        return false;
    }

    private bool CheckDamageCondition()
    {
        return UnityEngine.Random.value < 0.5f;
    }

    private bool CheckPactCondition()
    {
        return UnityEngine.Random.value < 0.5f;
    }

    private bool CheckCleanRoomsCondition()
    {
        cleanRoomsCounter++;
        return cleanRoomsCounter >= config.MaxCleanRoomsCondition;
    }

    private void ApplyDevilManipulation()
    {
        ResetCleanRoomsCounter();
        roomsSinceLastManipulation = 0;

        float roll = UnityEngine.Random.value;

        if (roll <= config.AuraProbability)
        {
            ActivateEnemyAuraManipulation();
        }
        else
        {
            ActivateDistortionManipulation();
        }
    }

    private void ActivateEnemyAuraManipulation()
    {
        var auras = Enum.GetValues(typeof(DevilAuraType))
                        .Cast<DevilAuraType>()
                        .Where(a => a != DevilAuraType.None)
                        .ToArray();

        DevilAuraType chosenAura = auras[UnityEngine.Random.Range(0, auras.Length)];

        ResurrectionLevel level = ResurrectionLevel.None;
        if (chosenAura == DevilAuraType.PartialResurrection)
        {
            level = (ResurrectionLevel)UnityEngine.Random.Range(1, 4);
        }

        OnAuraManipulationActivated?.Invoke(
            chosenAura,
            level,
            config.AuraEnemyCoveragePercent);

        ReportDebug($"Manipulación del Diablo: Aura Enemiga '{chosenAura}' (Nivel {level}) activada para el {config.AuraEnemyCoveragePercent * 100}% de enemigos.", 2);
    }

    private void ActivateDistortionManipulation()
    {
        if (availableDistortions.Count == 0)
        {
            availableDistortions = new List<DevilDistortionType>(allDistortionTypes);
            ReportDebug("Todas las Distorsiones han ocurrido. La lista se ha reiniciado.", 1);
        }

        DevilDistortionType chosenDistortion = ChooseDistortionByWeight();

        if (chosenDistortion != DevilDistortionType.None)
        {
            availableDistortions.Remove(chosenDistortion);
            OnDistortionActivated?.Invoke(chosenDistortion);
            ReportDebug($"Manipulación del Diablo: Distorsión '{chosenDistortion}' activada.", 2);
        }
    }

    private DevilDistortionType ChooseDistortionByWeight()
    {
        List<(DevilDistortionType type, float chance)> candidates = new List<(DevilDistortionType, float)>();
        float totalWeight = 0f;

        foreach (var type in availableDistortions)
        {
            float chance = GetDistortionChance(type);
            candidates.Add((type, chance));
            totalWeight += chance;
        }

        if (totalWeight <= 0) return DevilDistortionType.None;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;

        foreach (var candidate in candidates)
        {
            cumulativeWeight += candidate.chance;
            if (roll < cumulativeWeight)
            {
                return candidate.type;
            }
        }

        return DevilDistortionType.None;
    }

    private float GetDistortionChance(DevilDistortionType type)
    {
        return type switch
        {
            DevilDistortionType.AbyssalConfusion => config.AbyssalConfusion_Chance,
            DevilDistortionType.FloorOfTheDamned => config.FloorOfTheDamned_Chance,
            DevilDistortionType.DeceptiveDarkness => config.DeceptiveDarkness_Chance,
            DevilDistortionType.SealedLuck => config.SealedLuck_Chance,
            DevilDistortionType.WitheredBloodthirst => config.WitheredBloodthirst_Chance,
            DevilDistortionType.InfernalJudgement => config.InfernalJudgement_Chance,
            _ => 0f
        };
    }

    public void TryActivatePendingManipulation()
    {
        if (!isManipulationPending) return;

        if (!isAuraPending && pendingDistortionType == DevilDistortionType.SealedLuck)
        {
            ReportDebug($"Manipulación 'Suerte Sellada' detectada. Se mantendrá PENDIENTE hasta la Tienda.", 1);
            return; 
        }

        if (isAuraPending)
        {
            OnAuraManipulationActivated?.Invoke(pendingAuraType, pendingResurrectionLevel, pendingAuraCoverage);

            isManipulationActive = true;
            activeManipulationName = GetAuraName(pendingAuraType);

            pendingDistortionType = DevilDistortionType.None;
        }
        else
        {
            OnDistortionActivated?.Invoke(pendingDistortionType);

            isManipulationActive = true;
            activeManipulationName = GetDistortionName(pendingDistortionType);

            ReportDebug($"Manipulación del Diablo: Distorsión '{pendingDistortionType}' activada.", 1);
        }

        isManipulationPending = false;
        isAuraPending = false;
        pendingAuraType = DevilAuraType.None;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1: Debug.Log($"[DevilManager] {message}"); break;
            case 2: Debug.LogWarning($"[DevilManager] {message}"); break;
            case 3: Debug.LogError($"[DevilManager] {message}"); break;
        }
    }
}