using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clase que maneja el pool de escudos para optimizar el rendimiento.
/// </summary> 
public class ShieldPooler : MonoBehaviour
{
    public static ShieldPooler Instance;

    [Header("Pool Configuration")]
    [SerializeField] private GameObject shieldToPool;
    [SerializeField] private int amountToPool;

    private List<GameObject> pooledObjects;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (shieldToPool == null)
        {
            Debug.LogError("[ShieldPooler] shieldToPool no asignado.");
            pooledObjects = new List<GameObject>();
            return;
        }

        pooledObjects = new List<GameObject>();
        for (int i = 0; i < amountToPool; i++)
        {
            GameObject obj = Instantiate(shieldToPool);
            obj.SetActive(false);
            pooledObjects.Add(obj);
        }
    }

    /// <summary>
    /// Función que devuelve un escudo inactivo del pool
    /// </summary>
    /// <returns></returns>
    public GameObject GetPooledObject()
    {
        for (int i = pooledObjects.Count - 1; i >= 0; i--)
        {
            if (pooledObjects[i] == null) pooledObjects.RemoveAt(i);
        }

        for (int i = 0; i < pooledObjects.Count; i++)
        {
            var obj = pooledObjects[i];
            if (!obj.activeInHierarchy)
            {
                return obj;
            }
        }

        Debug.LogWarning("[ShieldPooler] Pool de escudos agotado. Considera aumentar 'amountToPool'.");
        return null;
    }
}