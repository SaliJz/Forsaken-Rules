using UnityEngine;
using UnityEditor;
using System.Linq;

public class JsonAnimSaveGuard : UnityEditor.AssetModificationProcessor
{
    static string[] OnWillSaveAssets (string[] paths)
    {
        var dirtyAssets = AssetDatabase.FindAssets("t:JsonAnimAsset").
            Select(guid => AssetDatabase.LoadAssetAtPath<JsonAnimAsset>(AssetDatabase.GUIDToAssetPath(guid))).
            Where(a => a != null && EditorUtility.IsDirty(a)).ToList();

        foreach (var asset in dirtyAssets)
        {
            JsonAnimAssetBuilder.SaveAnimJson(asset);
            JsonAnimAssetBuilder.Build(asset);
        }

        return paths;
    }

    public static bool ResolveAllDirtyAssets()
    {
        var dirtyAssets = AssetDatabase.FindAssets("t:JsonAnimAsset").
            Select(guid => AssetDatabase.LoadAssetAtPath<JsonAnimAsset>(AssetDatabase.GUIDToAssetPath(guid))).
            Where(a => a != null && EditorUtility.IsDirty(a)).ToList();

        foreach (var asset in dirtyAssets)
        {
            int option = EditorUtility.DisplayDialogComplex("Unsaved Animation Changes",
                $"'{asset.id}' Has unsaved changes. \n\nWhat do you want to do?", "Save", "Discard", "Cancel");

            switch (option)
            {
                case 0:
                    {
                        JsonAnimAssetBuilder.SaveAnimJson(asset);
                        JsonAnimAssetBuilder.Build(asset);
                        break;
                    }
                case 1:
                    {
                        asset.animData = null;
                        EditorUtility.ClearDirty(asset);
                        break;
                    }
                case 2:
                    {
                        return false;
                    }
            }
        }

        return true;
    }
}

[InitializeOnLoad]
public static class JsonAnimQuitGuard
{
    static JsonAnimQuitGuard()
    {
        EditorApplication.wantsToQuit += OnWantsToQuit;
        EditorApplication.playModeStateChanged += OnPlayStateChanged;
    }

    static bool OnWantsToQuit()
    {
        bool anyDirty = AssetDatabase.FindAssets("t:JsonAnimAsset").
            Select(guid => AssetDatabase.LoadAssetAtPath<JsonAnimAsset>(AssetDatabase.GUIDToAssetPath(guid))).
            Any(a => a != null && EditorUtility.IsDirty(a));

        if (!anyDirty) return true;

        return JsonAnimSaveGuard.ResolveAllDirtyAssets();
    }

    static void OnPlayStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        if (!JsonAnimSaveGuard.ResolveAllDirtyAssets())
        {
            EditorApplication.isPlaying = false;
        }
    }
}
