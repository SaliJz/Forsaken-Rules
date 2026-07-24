using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransparencyController : MonoBehaviour
{
    [SerializeField]
    private LayerMask wallLayer;
    [SerializeField]
    private float minOpacity = 0.3f;
    [SerializeField]
    private float transitionSpeed = 2.0f;

    private Dictionary<GameObject, Material> originalMaterials = new Dictionary<GameObject, Material>();
    private Dictionary<GameObject, Coroutine> activeFades = new Dictionary<GameObject, Coroutine>();

    private void OnTriggerEnter(Collider other)
    {
        if ((wallLayer.value & (1 << other.gameObject.layer)) > 0)
        {
            GameObject wallObject = other.gameObject;

            if (activeFades.ContainsKey(wallObject))
            {
                StopCoroutine(activeFades[wallObject]);
                activeFades.Remove(wallObject);
            }

            Renderer wallRenderer = wallObject.GetComponent<Renderer>();
            if (wallRenderer)
            {
                if (!originalMaterials.ContainsKey(wallObject))
                {
                    originalMaterials.Add(wallObject, wallRenderer.material);
                    wallRenderer.material = MakeMaterialTransparent(wallRenderer.material);
                }

                Coroutine fadeCoroutine = StartCoroutine(FadeWall(wallRenderer, minOpacity));
                activeFades.Add(wallObject, fadeCoroutine);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if ((wallLayer.value & (1 << other.gameObject.layer)) > 0)
        {
            GameObject wallObject = other.gameObject;

            if (activeFades.ContainsKey(wallObject))
            {
                StopCoroutine(activeFades[wallObject]);
                activeFades.Remove(wallObject);
            }

            Renderer wallRenderer = wallObject.GetComponent<Renderer>();
            if (wallRenderer)
            {
                Coroutine fadeCoroutine = StartCoroutine(FadeWall(wallRenderer, 1.0f, () => RestoreWallMaterial(wallObject)));
                activeFades.Add(wallObject, fadeCoroutine);
            }
        }
    }

    private IEnumerator FadeWall(Renderer wallRenderer, float targetOpacity, System.Action onComplete = null)
    {
        if (!wallRenderer) yield break;

        Color startColor = wallRenderer.material.color;
        float startAlpha = startColor.a;
        float elapsedTime = 0;

        float alphaChange = Mathf.Abs(startAlpha - targetOpacity);
        float duration = alphaChange / transitionSpeed;
        duration = Mathf.Max(duration, 0.01f); 

        while (elapsedTime < duration)
        {
            float alpha = Mathf.Lerp(startAlpha, targetOpacity, elapsedTime / duration);
            wallRenderer.material.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        wallRenderer.material.color = new Color(startColor.r, startColor.g, startColor.b, targetOpacity);

        onComplete?.Invoke();
    }

    private void RestoreWallMaterial(GameObject wallObject)
    {
        if (originalMaterials.ContainsKey(wallObject) && wallObject)
        {
            Renderer wallRenderer = wallObject.GetComponent<Renderer>();
            if (wallRenderer)
            {
                wallRenderer.material = originalMaterials[wallObject];
                originalMaterials.Remove(wallObject);
                activeFades.Remove(wallObject);
            }
        }
    }

    private Material MakeMaterialTransparent(Material originalMaterial)
    {
        Material newMaterial = new Material(originalMaterial);

        newMaterial.SetFloat("_Mode", 2);
        newMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        newMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        newMaterial.SetInt("_ZWrite", 0);
        newMaterial.DisableKeyword("_ALPHATEST_ON");
        newMaterial.EnableKeyword("_ALPHABLEND_ON");
        newMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        newMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return newMaterial;
    }
}