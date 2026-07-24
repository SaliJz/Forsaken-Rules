using UnityEngine;

public class RoomTrigger : MonoBehaviour
{
    public string roomId;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HUDMapManager.Instance.UpdateActiveRoom(roomId);
        }
    }
}