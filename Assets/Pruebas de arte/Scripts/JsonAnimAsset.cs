using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AnimAsset", menuName = "Json Animations/JsonAnim Asset")]
public class JsonAnimAsset : ScriptableObject
{
    public string id;

    [Header("Source Files")]
    public Texture2D spriteSheet;
    public TextAsset atlasJson;
    public TextAsset animJson;

    [Serializable]
    public class FrameSprite
    {
        public string name;
        public Sprite sprite;
    }
    [Serializable]
    public class DirectionAnim
    {
        public string direction;
        public AnimParser.AnimData anim;
    }

    [Header("Generated Data (Persistant)")]
    [SerializeField] List<FrameSprite> framesList = new();
    [SerializeField] List<DirectionAnim> animList = new();

    [NonSerialized] public Dictionary<string,Sprite> spritesLookup;
    [NonSerialized] public Dictionary<string, AnimParser.AnimData> animLookup;

    public int spriteCount;
    public int animationCount;

    public bool isBuilt => spriteCount > 0 && animationCount > 0;

    public void onBeforeSerialize() {}
    public void OnAfterDeserialize()
    {
        BuildLookup();
    }

    void BuildLookup()
    {
        // Debug.Log($"[JsonAnimAsset] Building lookup for {id} \nFrames stored: {framesList.Count} \nAnim Stored: {animList.Count}");

        spritesLookup = new Dictionary<string, Sprite>();
        animLookup = new Dictionary<string, AnimParser.AnimData>();

        foreach (var f in framesList)
        {
            if(f.sprite != null){
            // Debug.Log($"[JsonAnimAsset] Register Sprite -> {f.name}");
            spritesLookup[f.name] = f.sprite;}
        }
        foreach (var a in animList)
        {
            if(a.anim != null){
            // Debug.Log($"[JsonAnimAsset] Register Direction -> {a.direction}");
            animLookup[a.direction] = a.anim;}
        }

        // Debug.Log($"[JsonAnimAsset] Lookup built -> sprites:{spritesLookup.Count} | anims:{animLookup.Count}");
    }

    public AnimParser.AnimData GetDirection(string dir)
    {
        if(animLookup == null || animLookup.Count == 0) BuildLookup();

        // Debug.Log($"[SpriteAnimator] SEARCHING direction '{dir}' in asset '{id}'");

        if(animLookup.TryGetValue(dir, out var data)) 
        {
            // Debug.Log($"[SpriteAnimator] Direction FOUND -> {dir}");
            return data;
        }

        // Debug.LogError($"[SpriteAnimator] Direction '{dir}' NOT FOUND in Asset {id}");
        return null;
    }

    public Sprite GetSprite(string frameName)
    {
        if(spritesLookup == null || spritesLookup.Count == 0) BuildLookup();
        // return spritesLookup[frameName];

        if(spritesLookup.TryGetValue(frameName, out var sprite)) return sprite;

        Debug.Log($"Sprite {frameName} not found in {id}");
        return null;
    }

    public void SetData(Dictionary<string, Sprite> sprites, Dictionary<string, AnimParser.AnimData> animations)
    {
        framesList.Clear();
        animList.Clear();

        foreach(var s in sprites)
        {
            framesList.Add(new FrameSprite
                { name = s.Key,
                sprite = s.Value });
        }
        foreach(var a in animations)
        {
            AnimParser.AnimData data = a.Value;
            foreach(var f in data.frames)
            {
                if(sprites.TryGetValue(f.frame, out Sprite foundSprite))
                {
                    f.sprite = foundSprite;
                }
                else
                {
                    // Debug.LogWarning($"[Build] Sprite '{f.frame}' no encontrado en Atlas");
                }
            }

            animList.Add(new DirectionAnim
                { direction = a.Key,
                anim = data });
        }

        spriteCount = sprites.Count;
        animationCount = animations.Count;

        // spritesLookup = null;
        // animLookup = null;

        // BuildLookup();
    }
}
