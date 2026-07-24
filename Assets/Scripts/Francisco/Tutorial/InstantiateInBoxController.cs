using UnityEngine;
using System.Collections.Generic;

public class InstantiateInBoxController : MonoBehaviour
{
    public enum SpawnMode { Simple, Specific }

    [System.Serializable]
    public class SpecificEntry
    {
        public GameObject prefab;
        public KeyCode key = KeyCode.None;
    }

    [Header("Configuration")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.Simple;

    [Header("Simple Mode")]
    [SerializeField] private List<GameObject> prefabList = new List<GameObject>();
    [SerializeField] private KeyCode spawnKey = KeyCode.R;

    [Header("Specific Mode")]
    [SerializeField] private List<SpecificEntry> specificEntries = new List<SpecificEntry>();

    private BoxCollider spawnArea;
    private int simpleCurrentIndex = 0;
    private int specificActivePrefabIndex = -1;
    private List<GameObject> specificSpawnedObjects = new List<GameObject>();

    public SpawnMode CurrentSpawnMode => spawnMode;
    public List<GameObject> PrefabList => prefabList;
    public KeyCode SpawnKey => spawnKey;
    public List<SpecificEntry> SpecificEntries => specificEntries;

    private void Awake()
    {
        spawnArea = GetComponent<BoxCollider>();

        if (spawnArea == null)
        {
            Debug.LogError("[InstantiateInBoxController] A BoxCollider is required on this GameObject to define the spawn area.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (spawnMode == SpawnMode.Simple && Input.GetKeyDown(spawnKey))
        {
            SpawnNextSimpleItem();
        }

        if (spawnMode == SpawnMode.Specific)
        {
            for (int i = 0; i < specificEntries.Count; i++)
            {
                var entry = specificEntries[i];
                if (entry.key != KeyCode.None && Input.GetKeyDown(entry.key))
                    SpawnSpecificEntry(i);
            }
        }
    }

    public void SpawnNextSimpleItem()
    {
        if (!ValidatePrefabList(prefabList, "Simple")) return;

        GameObject prefab = prefabList[simpleCurrentIndex];
        simpleCurrentIndex = (simpleCurrentIndex + 1) % prefabList.Count;

        SpawnPrefab(prefab);
    }

    public void SpawnSpecificEntry(int index)
    {
        if (specificEntries == null || specificEntries.Count == 0)
        {
            Debug.LogError("[InstantiateInBoxController] Specific entries list is empty.");
            return;
        }

        if (index < 0 || index >= specificEntries.Count)
        {
            Debug.LogError($"[InstantiateInBoxController] Index {index} is out of range.");
            return;
        }

        var entry = specificEntries[index];

        if (entry.prefab == null)
        {
            Debug.LogError($"[InstantiateInBoxController] Entry {index} has no prefab assigned.");
            return;
        }

        bool isSamePrefab = index == specificActivePrefabIndex;

        if (!isSamePrefab)
        {
            DestroyAllSpecificObjects();
            specificActivePrefabIndex = index;
        }

        SpawnPrefab(entry.prefab);
        specificSpawnedObjects.RemoveAll(obj => obj == null);
    }

    private void DestroyAllSpecificObjects()
    {
        foreach (GameObject obj in specificSpawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        specificSpawnedObjects.Clear();
    }

    private GameObject SpawnPrefab(GameObject prefab)
    {
        if (spawnArea == null)
        {
            Debug.LogError("[InstantiateInBoxController] BoxCollider not found. Cannot spawn.");
            return null;
        }

        Vector3 randomPosition = GetRandomPositionInBounds(spawnArea.bounds);
        GameObject spawned = Instantiate(prefab, randomPosition, Quaternion.identity);
        spawned.transform.SetParent(transform);

        if (spawnMode == SpawnMode.Specific)
            specificSpawnedObjects.Add(spawned);

        Debug.Log($"[InstantiateInBoxController] '{prefab.name}' spawned at: {randomPosition}");
        return spawned;
    }

    private Vector3 GetRandomPositionInBounds(Bounds bounds)
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }

    private bool ValidatePrefabList(List<GameObject> list, string modeName)
    {
        if (list == null || list.Count == 0)
        {
            Debug.LogError($"[InstantiateInBoxController] The prefab list for {modeName} mode is empty.");
            return false;
        }
        return true;
    }
}