using UnityEngine;

public class AtlasJson : MonoBehaviour
{
    [System.Serializable]
    public class AtlasFrameRect
    {
        public int x;
        public int y;
        public int w;
        public int h;
    }

    [System.Serializable]
    public class AtlasAnchor
    {
        public float x;
        public float y;
    }

    [System.Serializable]
    public class AtlasFrame
    {
        public string filename;
        public AtlasFrameRect frame;
        public AtlasAnchor anchor;
    }

    [System.Serializable]
    public class AtlasData
    {
        public AtlasFrame[] frames;
    }
}
