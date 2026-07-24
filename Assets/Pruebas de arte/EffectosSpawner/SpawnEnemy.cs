using UnityEngine;

public class SpawnEnemy : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] public GameObject prefabEnemigo;

    private void OnDestroy()
    {
        if (prefabEnemigo == null) return;
        if (!gameObject.scene.isLoaded) return;

        Instantiate(prefabEnemigo, transform.position, Quaternion.identity);
    }


}
