using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private GameObject activeVFX;
    [SerializeField] private GameObject inactiveModel;
    [SerializeField] private GameObject activeModel;

    private void Start()
    {
        DeactivateVisuals();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            RespawnManager.Instance?.RegisterSpawnPoint(this);
            Debug.Log("[SpawnPoint] Player reached spawn point: " + gameObject.name);
        }
    }

    public Vector3 GetSpawnPosition()
    {
        return transform.position;
    }

    public void ActivateVisuals()
    {
        if (activeVFX != null) activeVFX.SetActive(true);
        if (activeModel != null) activeModel.SetActive(true);
        if (inactiveModel != null) inactiveModel.SetActive(false);
    }

    public void DeactivateVisuals()
    {
        if (activeVFX != null) activeVFX.SetActive(false);
        if (activeModel != null) activeModel.SetActive(false);
        if (inactiveModel != null) inactiveModel.SetActive(true);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 2);
    }
}