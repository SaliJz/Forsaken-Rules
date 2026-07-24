// Assets/Editor/SelectionValidator.cs
using UnityEditor;
using UnityEngine;

public class SelectionValidator
{
    [MenuItem("Tools/Validate Selection")]
    static void ValidateSelection()
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            Debug.Log("No hay objetos seleccionados.");
            return;
        }

        for (int i = 0; i < objs.Length; i++)
        {
            var o = objs[i];
            if (o == null)
            {
                Debug.LogError($"Selección contiene un objeto NULL en el índice {i}.");
                continue;
            }
            var go = o as GameObject;
            if (go != null)
            {
                Debug.Log($"Seleccionado GameObject: '{go.name}' (InstanceID: {go.GetInstanceID()}) Path: {GetPath(go)}");
            }
            else
            {
                Debug.Log($"Seleccionado Asset/Other: {o.name} ({o.GetType().Name})");
            }
        }
    }

    static string GetPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}