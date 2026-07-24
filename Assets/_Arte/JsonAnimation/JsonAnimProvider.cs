using UnityEngine;

public interface JsonAnimProvider
{
    JsonAnimAsset GetAnim(string ID);
    bool AnimExist(string ID);
}
