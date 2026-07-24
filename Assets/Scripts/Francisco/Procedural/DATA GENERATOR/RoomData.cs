using UnityEngine;

[System.Serializable]
public class RoomData
{
    public Room prefab;
    [HideInInspector]
    public int repetitionCount = 0;
    [HideInInspector]
    public float weight = 1.0f;
}