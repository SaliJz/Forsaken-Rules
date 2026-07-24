using UnityEngine;
public class S_VFX_AfterImagesSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] SpriteRenderer sourceRenderer;

    [Header("Spawn Settings")]   
    [SerializeField] public GameObject afterImagePrefab;
    [SerializeField] public float spawnInterval = 0.05f;
    [SerializeField] public float lifetime = 0.2f;

    [Header("Color")]
    [SerializeField] Color startColor = new Color(1f, 0.8f, 0.2f, 0.7f);
    [SerializeField] Color endColor = new Color(1f, 0.2f, 0.8f, 0.3f);

    float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if(timer >= spawnInterval)
        {
            SpawnAfterImage();
            timer = 0;
        }
    }

    private void OnEnable()
    {
        timer = spawnInterval;
    }

    void SpawnAfterImage()
    {
        if (afterImagePrefab == null) return;
        // GameObject obj = Instantiate(afterImagePrefab, transform.position, transform.rotation);
        GameObject obj = Instantiate(afterImagePrefab);
        obj.name = "AfterImage";
        obj.transform.position = transform.position;
        obj.transform.rotation = transform.rotation;
        obj.transform.localScale = transform.localScale;

        SpriteRenderer r = obj.GetComponent<SpriteRenderer>();

        r.sprite = sourceRenderer.sprite;
        r.flipX = sourceRenderer.flipX;
        r.flipY = sourceRenderer.flipY;
        r.sortingLayerID = sourceRenderer.sortingLayerID;
        r.sortingOrder = sourceRenderer.sortingOrder - 1;

        S_AfterImagePrefab afterImage = obj.GetComponent<S_AfterImagePrefab>();
        if (afterImage != null) afterImage.Initialize(startColor, endColor, lifetime);
        else Destroy(obj, lifetime);
        
    }
}