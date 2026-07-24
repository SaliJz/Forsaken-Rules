using UnityEngine;
using System.Collections;

public class CrackFadeOut : MonoBehaviour
{
    [SerializeField] private float fadeDelay = 0.5f;
    [SerializeField] private float fadeDuration = 1.5f;

    private Renderer[] renderers;
    private MaterialPropertyBlock mpb;

    private void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        yield return new WaitForSeconds(fadeDelay);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeDuration);

            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(mpb);
                mpb.SetFloat("_Alpha", alpha);
                renderer.SetPropertyBlock(mpb);
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}