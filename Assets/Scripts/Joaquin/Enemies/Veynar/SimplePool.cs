using System.Collections.Generic;
using UnityEngine;

public class SimplePool : MonoBehaviour
{
    [Tooltip("El Prefab que este pool va a gestionar.")]
    [SerializeField] private GameObject prefab;
    [SerializeField] private int initialSize = 10;

    private Queue<GameObject> queue = new Queue<GameObject>();

    private void Awake()
    {
        Warm(initialSize);
    }

    /// <summary>
    /// Pre-instancia un número de objetos para llenar el pool.
    /// </summary>
    public void Warm(int count)
    {
        if (prefab == null)
        {
            Debug.LogError($"El prefab no está asignado en el pool '{gameObject.name}'.", this);
            return;
        }
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(prefab, transform);
            go.SetActive(false);
            queue.Enqueue(go);
        }
    }

    /// <summary>
    /// Obtiene un objeto del pool, lo activa y lo devuelve.
    /// </summary>
    public GameObject Get()
    {
        if (prefab == null) return null;

        GameObject result;
        if (queue.Count == 0)
        {
            result = Instantiate(prefab, transform);
        }
        else
        {
            result = queue.Dequeue();
        }

        result.transform.SetParent(null);
        result.SetActive(true);
        return result;
    }

    /// <summary>
    /// Devuelve un objeto al pool, desactivándolo.
    /// </summary>
    public void Return(GameObject gameObject)
    {
        if (gameObject == null) return;

        gameObject.SetActive(false);
        gameObject.transform.SetParent(transform);
        queue.Enqueue(gameObject);
    }
}