using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoomTypeProbability
{
    public RoomType roomType;
    [Range(0f, 100f)]
    public float probability = 0;
    public Room[] roomPrefabs;
    [HideInInspector]
    public List<RoomData> roomDataList = new List<RoomData>();
}