using UnityEngine;
using System.Collections.Generic;

public class HUDMapManager : MonoBehaviour
{
    public static HUDMapManager Instance;

    [Header("Configuración de Colores")]
    public Color colorVivo = Color.white;
    public Color colorApagado = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Lista de Cuartos en HUD")]
    public List<RoomUIEntry> mapRooms = new List<RoomUIEntry>();

    private string currentRoomId = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitialState();
    }

    public void UpdateActiveRoom(string id)
    {
        if (currentRoomId == id) return;
        currentRoomId = id;

        foreach (var entry in mapRooms)
        {
            bool isActive = (entry.roomId == id);

            if (entry.roomIcon != null)
            {
                entry.roomIcon.color = isActive ? colorVivo : colorApagado;
            }

            if (entry.roomText != null)
            {
                if (isActive)
                {
                    entry.roomText.text = entry.roomId; 
                    entry.roomText.gameObject.SetActive(true);
                }
                else
                {
                    entry.roomText.gameObject.SetActive(false);
                }
            }
        }
    }

    private void InitialState()
    {
        foreach (var entry in mapRooms)
        {
            if (entry.roomIcon != null) entry.roomIcon.color = colorApagado;
            if (entry.roomText != null) entry.roomText.gameObject.SetActive(false);
        }
    }
}