using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class AnimParser
{
    [System.Serializable]
    public class AnimFrame
    {
        // public string key;
        public string frame;
        public Sprite sprite;
        public string[] evnt;
    }

    [System.Serializable]
    public class AnimData
    {
        public string key;
        // public string type;
        public int repeat;
        public int frameRate;
        public List<AnimFrame> frames;
    }

    // [System.Serializable]
    // public class AnimFile
    // {
    //     public AnimData[] anims;
    // }

    //Formato de AnimJson normalizado para el inspector
    [System.Serializable] public class NormEvent {public int frameIndex; public string[] keys;}
    [System.Serializable] public class NormDirection {public string direction; public string[] frames;}
    [System.Serializable]
    public class NormAnimFile
    {
        public string schema = "norm_v1";
        public int frameRate;
        public int repeat;
        public int framecount;
        public NormEvent[] events = new NormEvent[0];
        public NormDirection[] directions;
    }

    [System.Serializable] public class RawFrame {public string key; public string frame; public string[] evnt;}
    [System.Serializable] public class RawAnimData {
        public string key; public string type; public int repeat;
        public int frameRate; public RawFrame[] frames;}
    [System.Serializable] public class RawAnimFile {public RawAnimData[] anims;}

    public static Dictionary<string, AnimData> Parse(TextAsset animJson)
    {
        string text = animJson.text;
        NormAnimFile normalized = IsNormalize(animJson) ? JsonUtility.FromJson<NormAnimFile>(text): NormalizeAndWriteBack(animJson);
        return Denormalize (normalized);
    }

    public static bool IsNormalize (TextAsset json)
    {
        var probe = JsonUtility.FromJson<SchemaProbe>(json.text);
        return probe != null && probe.schema == "norm_v1";
    }

    [System.Serializable] class SchemaProbe {public string schema;}


    public static NormAnimFile NormalizeAndWriteBack(TextAsset json)
    {
        RawAnimFile raw = JsonUtility.FromJson<RawAnimFile>(json.text);
        RawAnimData first = raw.anims[0];

        var events = new List<NormEvent>();
        for (int i = 0; i < first.frames.Length; i++)
        {
            var evnt = first.frames[i].evnt;
            if (evnt != null && evnt.Length > 0)
            {
                events.Add(new NormEvent{ frameIndex = i, keys = evnt });
            }
        }

        var directions = new NormDirection[raw.anims.Length];
        for (int i = 0; i < raw.anims.Length; i++)
        {
            var a = raw.anims[i];
            var frameName = new string[a.frames.Length];
            for (int j = 0; j < a.frames.Length; j++)
            {
                frameName[j] = a.frames[j].frame;
            }
            directions[i] = new NormDirection{ direction = a.key, frames = frameName };
        }

        var normalized = new NormAnimFile
        {
            frameRate = first.frameRate,
            repeat = first.repeat,
            framecount = first.frames.Length,
            events = events.ToArray(),
            directions = directions 
        };

        #if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(json);
        System.IO.File.WriteAllText(path, JsonUtility.ToJson(normalized, true));
        AssetDatabase.ImportAsset(path);
        #endif

        return normalized;
    }

    static Dictionary<string, AnimData> Denormalize(NormAnimFile file)
    {
        var dict = new Dictionary<string, AnimData>();
        foreach(var dir in file.directions)
        {
            var frames = new List<AnimFrame>(dir.frames.Length);
            for (int i = 0; i < dir.frames.Length; i++)
            {
                string[] evnt = new string[0];
                foreach (var e in file.events)
                {
                    if (e.frameIndex == i) {evnt = e.keys; break;}
                }
                frames.Add(new AnimFrame {frame = dir.frames[i], evnt = evnt});
            }

            dict[dir.direction] = new AnimData
            {
                key = dir.direction,
                repeat = file.repeat,
                frameRate = file.frameRate,
                frames = frames
            };
        }

        return dict;
    }
}
