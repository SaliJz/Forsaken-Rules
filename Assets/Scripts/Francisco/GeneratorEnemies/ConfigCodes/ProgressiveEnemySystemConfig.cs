using UnityEngine;
using System.Collections.Generic;
using System.Linq; 

[CreateAssetMenu(fileName = "NewProgressiveEnemyConfig", menuName = "Dungeon/Progressive Enemy Config", order = 1)]
public class ProgressiveEnemySystemConfig : ScriptableObject
{
    [Header("Definición de la Progresión")]
    public List<RoomProgressionLevel> progressionLevels = new List<RoomProgressionLevel>();

    public RoomProgressionLevel GetConfigForRoom(int roomNumber)
    {
        var sortedLevels = progressionLevels.OrderBy(l => l.startRoomNumber).ToList();

        RoomProgressionLevel bestMatch = null;

        foreach (var level in sortedLevels)
        {
            if (roomNumber >= level.startRoomNumber)
            {
                bestMatch = level;
            }
            else
            {
                break;
            }
        }

        return bestMatch;
    }
}