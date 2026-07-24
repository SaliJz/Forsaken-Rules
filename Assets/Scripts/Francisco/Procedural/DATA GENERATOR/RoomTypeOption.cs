using UnityEngine;

[System.Serializable]
public class RoomTypeOption
{
    public RoomType roomType;
    [Range(0f, 100f)]
    public float probability = 50f;
}