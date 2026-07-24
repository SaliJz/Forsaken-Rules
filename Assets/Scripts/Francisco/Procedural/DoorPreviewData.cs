using UnityEngine;

[CreateAssetMenu(fileName = "DoorPreviewData", menuName = "Dungeon/Door Preview Data")]
public class DoorPreviewData : ScriptableObject
{
    [System.Serializable]
    public class RoomTypeEntry
    {
        public RoomType roomType;
        public Color color;
        public string displayName;
        public GameObject icon;
    }

    [Header("Room Type Configurations")]
    public RoomTypeEntry[] entries;

    [Header("Settings")]
    public float emissionIntensity = 2f;
    public bool showText = true;
    public bool showIcon = true;
    public Color baseColor = new Color(0.3f, 0.3f, 0.3f); 
    public string baseDisplayName = "???";

    public RoomTypeEntry GetEntry(RoomType roomType)
    {
        foreach (var entry in entries)
            if (entry.roomType == roomType) return entry;
        return null;
    }
}