using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class FindMissingComponents : EditorWindow
{
    [MenuItem("Tools/Find Missing Components")]
    static void Open()
    {
        GetWindow<FindMissingComponents>("Find Missing Components").Show();
    }

    void OnGUI()
    {
        if (GUILayout.Button("Buscar en escena activa"))
        {
            FindInScene();
        }
        if (GUILayout.Button("Buscar en todos los assets (prefabs)"))
        {
            FindInAssets();
        }
    }

    static void FindInScene()
    {
        var results = new List<string>();
        // Sustituir FindObjectsOfType por FindObjectsByType con el modo de ordenación None (más rápido)
        var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in all)
        {
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null)
                {
                    results.Add($"Missing component on GameObject '{go.name}' (Path: {GetGameObjectPath(go)})");
                }
            }
        }
        if (results.Count == 0) Debug.Log("No missing components found in scene.");
        else results.ForEach(s => Debug.LogWarning(s));
    }

    static void FindInAssets()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            var comps = prefab.GetComponentsInChildren<Component>(true);
            foreach (var c in comps)
            {
                if (c == null)
                {
                    Debug.LogWarning($"Missing component in prefab '{path}'");
                }
            }
        }
        Debug.Log("Search prefabs done.");
    }

    static string GetGameObjectPath(GameObject go)
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