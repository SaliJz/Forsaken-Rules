using UnityEngine;

public interface ITeleportableDoor
{
    void OpenDoor();
    void CloseDoor();
    Transform GetTransform();
}