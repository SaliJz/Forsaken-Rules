using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class RoomProgressionRule
{
    [Header("Room Type Selection")]
    public bool useMultipleRoomTypes = false;
    public RoomType roomType;
    public List<RoomTypeOption> multipleRoomTypes = new List<RoomTypeOption>();

    [Header("Custom Room Prefabs")]
    public bool useCustomPrefabs = false;
    public Room[] customRoomPrefabs;

    [Header("Progression Range")]
    public int minRoomNumber = 1;
    public int maxRoomNumber = 10;

    [Header("Mandatory/Probability")]
    public bool isMandatory;
    public bool isProbableMandatory;
    public bool generateOnce;
    public bool allowMultipleDoorsOfSameType = false;
    [Range(0f, 100f)]
    public float probability = 0;

    [Header("Content Rule")]
    public CombatContents combatContent;

    public RoomType GetRoomType()
    {
        if (!useMultipleRoomTypes || multipleRoomTypes == null || multipleRoomTypes.Count == 0)
        {
            return roomType;
        }

        float totalProbability = 0f;
        foreach (var option in multipleRoomTypes)
        {
            totalProbability += option.probability;
        }

        if (totalProbability <= 0)
        {
            return multipleRoomTypes[Random.Range(0, multipleRoomTypes.Count)].roomType;
        }

        float randomValue = Random.Range(0f, totalProbability);
        float currentSum = 0f;

        foreach (var option in multipleRoomTypes)
        {
            currentSum += option.probability;
            if (randomValue <= currentSum)
            {
                return option.roomType;
            }
        }

        return multipleRoomTypes[0].roomType;
    }

    public Room GetRandomCustomPrefab()
    {
        if (!useCustomPrefabs || customRoomPrefabs == null || customRoomPrefabs.Length == 0)
        {
            return null;
        }

        var validPrefabs = customRoomPrefabs.Where(p => p != null).ToArray();

        if (validPrefabs.Length == 0)
        {
            return null;
        }

        return validPrefabs[Random.Range(0, validPrefabs.Length)];
    }

    public bool HasCustomPrefabs()
    {
        return useCustomPrefabs && customRoomPrefabs != null && customRoomPrefabs.Length > 0;
    }
}