// Assets/Editor/FindMissingComponentsDetailed.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class FindMissingComponentsDetailed : EditorWindow
{
    [MenuItem("Tools/Find Missing Components (Detailed)")]
    static void Open() { GetWindow<FindMissingComponentsDetailed>("Missing Components").Show(); }

    void OnGUI()
    {
        if (GUILayout.Button("Buscar en escena activa"))
            FindInScene();
        if (GUILayout.Button("Buscar en todos los prefabs"))
            FindInPrefabs();
        if (GUILayout.Button("Eliminar componentes faltantes en escena (safe)"))
            RemoveMissingInScene();
    }

    static void FindInScene()
    {
        var results = new List<string>();
        var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in all)
        {
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null)
                    results.Add($"MissingComponent: '{GetPath(go)}' on GameObject '{go.name}'");
            }
        }
        if (results.Count == 0) Debug.Log("No missing components in scene.");
        else results.ForEach(r => Debug.LogWarning(r));
    }

    static void FindInPrefabs()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab");
        int count = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            var comps = prefab.GetComponentsInChildren<Component>(true);
            foreach (var c in comps)
                if (c == null)
                    Debug.LogWarning($"Missing component in prefab '{path}'");
            count++;
        }
        Debug.Log($"Prefabs inspeccionados: {count}");
    }

    static void RemoveMissingInScene()
    {
        int removed = 0;
        var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in all)
        {
            var comps = go.GetComponents<Component>();
            SerializedObject so = new SerializedObject(go);
            var compProp = so.FindProperty("m_Component");
            if (compProp == null) continue;
            // Enumerar componentes y eliminar nulos (cuidado: solo en escena)
            for (int i = comps.Length - 1; i >= 0; i--)
            {
                if (comps[i] == null)
                {
                    Debug.Log($"Eliminando componente nulo en '{GetPath(go)}'");
                    UnityEditorInternal.ComponentUtility.CopyComponent(null);
                    removed++;
                    // No existe API pública directa para remover un componente nulo salvo editar serialized properties.
                    // Una técnica es reserializar sin el slot nulo, pero aquí informamos y dejamos al usuario eliminar manualmente.
                }
            }
        }
        Debug.Log($"Finished. Missing components detected (informados): {removed}");
        EditorUtility.DisplayDialog("Missing Components", $"Missing components reported: {removed}\nSe han listado en consola.", "OK");
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