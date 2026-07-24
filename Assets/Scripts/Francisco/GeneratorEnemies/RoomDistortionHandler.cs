using System.Linq;
using UnityEngine;

public class RoomDistortionHandler : MonoBehaviour
{
    [SerializeField] private PhysicsMaterial slipperyMaterial;

    private Renderer[] roomFloorRenderers;

    private void Start()
    {
        roomFloorRenderers = GetComponentsInChildren<Renderer>().Where(r => r.tag == "Floor").ToArray();
    }

    public void ApplyFloorOfTheDamned()
    {
        if (slipperyMaterial == null)
        {
            Debug.LogError("[Distorsión] slipperyMaterial no asignado para FloorOfTheDamned.");
            return;
        }

        foreach (var renderer in roomFloorRenderers)
        {
            if (renderer.TryGetComponent<Collider>(out var collider))
            {
                collider.sharedMaterial = slipperyMaterial;
            }
        }
        Debug.Log("[Distorsión] Suelo maldito aplicado: Fricción baja.");
    }

    public void ClearFloorOfTheDamned()
    {
        // restaurar el material de física original
    }
}