using UnityEngine;

public class TextureScroll : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = -2.0f;
    private LineRenderer lr;
    private Material mat;

    private void Start()
    {
        lr = GetComponent<LineRenderer>();
        if (lr != null) mat = lr.material;
    }

    private void Update()
    {
        if (mat != null)
        {
            // Mueve la textura en el eje X
            float offset = Time.time * scrollSpeed;
            mat.mainTextureOffset = new Vector2(offset, 0);
        }
    }
}