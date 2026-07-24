using System.Collections.Generic;
using UnityEngine;

public static class AnimParser
{
    [System.Serializable]
    public class AnimFrame
    {
        public string key;
        public string frame;
        public Sprite sprite;
        public string[] evnt;
    }

    [System.Serializable]
    public class AnimData
    {
        public string key;
        public string type;
        public int repeat;
        public int frameRate;
        public AnimFrame[] frames;
    }

    [System.Serializable]
    public class AnimFile
    {
        public AnimData[] anims;
    }

    public static Dictionary<string, AnimData> Parse(TextAsset animJson)
    {
        AnimFile file = JsonUtility.FromJson<AnimFile>(animJson.text);

        var dirDict = new Dictionary<string, AnimData>();

        foreach (var anim in file.anims)
        {
            dirDict.Add(anim.key, anim);
        }

        return dirDict;
    }
}
