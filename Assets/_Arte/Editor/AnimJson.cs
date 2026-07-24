using System.Collections.Generic;
using UnityEngine;

public class AnimJson : MonoBehaviour
{
    [System.Serializable]
    public class AnimJsonData
    {
        public AnimClipData[] anims;
    }


    [System.Serializable]
    public class AnimClipData
    {
        public string key;
        public string type;
        public int repeat;
        public int frameRate;
        public List<AnimFrameData> frames;
    }

    [System.Serializable]
    public class AnimFrameData
    {
        public string key;
        public string frame;
        public string[] evnt;
    }
}
