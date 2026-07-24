using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCharacter_AnimLibrary", menuName = "Json Animations/Character Library")]
public class JsonAnimLibrarySO : ScriptableObject, JsonAnimProvider
{
    [Tooltip("Lista de todas las animaciones (JsonAnimAssets) de este personaje")]
    public List<JsonAnimAsset> animations = new List<JsonAnimAsset>();

    private Dictionary<string, JsonAnimAsset> _cache;

    private void OnEnable()
    {
        InitializeCache();
    }

    void InitializeCache()
    {
        _cache = new Dictionary<string, JsonAnimAsset>();
        foreach (var anim in animations)
        {
            if(anim != null && !string.IsNullOrEmpty(anim.id))
            {
                _cache[anim.id] = anim;
            }
        }
    }

    public JsonAnimAsset GetAnim(string ID)
    {
        if(_cache == null) InitializeCache();
        return _cache.TryGetValue(ID, out var asset) ? asset : null;
    }

    public bool AnimExist(string ID)
    {
        if(_cache == null) InitializeCache();
        return _cache.ContainsKey(ID);
    }
}
