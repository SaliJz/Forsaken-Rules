using System.Collections;
using UnityEngine;

public class SetRandomSprite : MonoBehaviour
{
    [SerializeField] Sprite[] sprites;
    GlassShardDamage glassDamageScript;
    SpriteRenderer targetRenderer;
    MaterialPropertyBlock mpb;

    void Start()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
        glassDamageScript = GetComponentInParent<GlassShardDamage>();

        mpb = new MaterialPropertyBlock();
        
        StartCoroutine(SpawnShine());

        int randomNum = Random.Range(0, sprites.Length);
        targetRenderer.sprite = sprites[randomNum];
    }

    IEnumerator SpawnShine()
    {
        float t = 0;
        // float duration = glassDamageScript.shardDeathDuration/4;
        float duration = 0.15f;

        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat("_Amount", 1);
        targetRenderer.SetPropertyBlock(mpb);

        yield return new WaitForSeconds(0.2f);

        while(t < duration)
        {
            t+= Time.deltaTime;

            targetRenderer.GetPropertyBlock(mpb);

            mpb.SetFloat("_Amount", Mathf.Lerp(1, 0, t / duration));

            targetRenderer.SetPropertyBlock(mpb);
            yield return null;
        }

        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat("_Amount", 0);
        targetRenderer.SetPropertyBlock(mpb);
    }
}
