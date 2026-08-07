using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

public static class JsonAnimAssetBuilder
{
    [MenuItem("Tools/JsonAnimation/Rebuild JsonAnims")]

    public static void BuildSelected()
    {
        var selected = Selection.objects;
        int build = 0;

        foreach(var obj in selected)
        {
            JsonAnimAsset asset = obj as JsonAnimAsset;
            if(asset == null) continue;

                Build(asset);
                build++;

            // Debug.Log($"[ANIM BUILD] Build {build} assets");
        }
    }
    public static void Build(JsonAnimAsset asset)
    {
        // JsonAnimAsset asset = Selection.activeObject as JsonAnimAsset;
        // if(asset == null) {Debug.LogWarning("[ANIM BUILDER] Select a JsonAnimAsset first"); return;}
        if(asset.spriteSheet == null || asset.atlasJson == null || asset.animJson == null) return;

        AssetDatabase.StartAssetEditing();

        try
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset));
            foreach (var sub in subAssets)
            {
                if (sub is Sprite) Object.DestroyImmediate(sub, true);
            }

            Dictionary<string, Sprite> atlas = AtlasParser.Parse(asset.spriteSheet, asset.atlasJson);
            Dictionary<string, AnimParser.AnimData> anims = AnimParser.Parse(asset.animJson);

            foreach (var sprite in atlas.Values)
            {
                AssetDatabase.AddObjectToAsset(sprite, asset);
                sprite.hideFlags = HideFlags.HideInHierarchy;
            }

            asset.SetData(atlas, anims);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("CONTEXT/JsonAnimAsset/Rebuild")]
    static void RebuildAsset(MenuCommand command)
    {
        JsonAnimAsset asset = (JsonAnimAsset)command.context;

        Build(asset);
        AssetDatabase.SaveAssets();

    }

    public static void SaveAnimJson(JsonAnimAsset asset)
    {
        if (asset.animData == null || asset.animJson == null) return;

        if (asset.animData.directions == null || asset.animData.directions.Length == 0)
        {
            Debug.LogError($"[JsonAnimAssetBuilder] Se abortó el guardado de {asset.id}: AnimData vacio o corrupto. No se sobreescribió el archivo");
        }

        string json = JsonUtility.ToJson(asset.animData, true);
        string path = AssetDatabase.GetAssetPath(asset.animJson);
        System.IO.File.WriteAllText(path, json);
        AssetDatabase.Refresh();

        EditorUtility.ClearDirty(asset);
    }
}
