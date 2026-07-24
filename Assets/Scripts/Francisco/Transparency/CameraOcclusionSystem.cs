using System.Collections.Generic;
using UnityEngine;

public class CameraOcclusionSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera mainCamera;

    [Header("Detection Settings")]
    [SerializeField] private LayerMask occlusionLayers;
    [SerializeField] private float raycastRadius = 0.5f;
    [SerializeField] private float detectionDistance = 50f;

    [Header("Visual Configuration")]
    [SerializeField] private float fadedAlpha = 0.3f;
    [SerializeField] private float fadeSpeed = 5f;

    private class MaterialData
    {
        public Material[] originalMaterials;
        public Material[] fadedMaterials;
        public float currentAlpha = 1.0f;
        public Renderer renderer;

        public MaterialData(Renderer r, Material[] originalMats)
        {
            renderer = r;
            originalMaterials = originalMats;
            fadedMaterials = new Material[originalMaterials.Length];

            for (int i = 0; i < originalMaterials.Length; i++)
            {
                if (originalMaterials[i] == null)
                {
                    fadedMaterials[i] = null;
                    continue;
                }

                fadedMaterials[i] = new Material(originalMaterials[i]);
                SetupFadeMaterial(fadedMaterials[i]);
            }
        }

        public void Cleanup()
        {
            if (fadedMaterials != null)
            {
                foreach (var mat in fadedMaterials)
                {
                    if (mat != null) Object.Destroy(mat);
                }
            }
        }

        private void SetupFadeMaterial(Material mat)
        {
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            else if (mat.shader.name.Contains("Sprite"))
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }
        }
    }

    private Dictionary<Renderer, MaterialData> occludedObjects = new Dictionary<Renderer, MaterialData>();
    private HashSet<Renderer> currentlyOccluding = new HashSet<Renderer>();
    private List<Renderer> toRemove = new List<Renderer>();

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (playerTransform == null)
        {
            Debug.LogError("[CameraOcclusionSystem] playerTransform no está asignado.");
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (playerTransform == null || mainCamera == null) return;

        currentlyOccluding.Clear();
        Vector3 direction = playerTransform.position - mainCamera.transform.position;
        float distance = direction.magnitude;
        direction.Normalize();

        RaycastHit[] hits = Physics.SphereCastAll(
            mainCamera.transform.position,
            raycastRadius,
            direction,
            distance,
            occlusionLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer != null && hit.transform != playerTransform)
            {
                currentlyOccluding.Add(renderer);
            }
        }

        foreach (var renderer in currentlyOccluding)
        {
            if (renderer == null) continue;

            if (!occludedObjects.ContainsKey(renderer))
            {
                MaterialData data = new MaterialData(renderer, renderer.sharedMaterials);
                occludedObjects.Add(renderer, data);
                renderer.sharedMaterials = data.fadedMaterials;
                data.currentAlpha = 1.0f;
            }

            MaterialData currentData = occludedObjects[renderer];
            currentData.currentAlpha = Mathf.MoveTowards(
                currentData.currentAlpha,
                fadedAlpha,
                Time.deltaTime * fadeSpeed
            );
            ApplyAlphaToMaterials(currentData, currentData.currentAlpha);
        }

        toRemove.Clear();
        foreach (var kvp in occludedObjects)
        {
            Renderer renderer = kvp.Key;
            MaterialData data = kvp.Value;

            if (renderer == null)
            {
                data.Cleanup();
                toRemove.Add(renderer);
                continue;
            }

            if (!currentlyOccluding.Contains(renderer))
            {
                data.currentAlpha = Mathf.MoveTowards(
                    data.currentAlpha,
                    1.0f,
                    Time.deltaTime * fadeSpeed
                );
                ApplyAlphaToMaterials(data, data.currentAlpha);

                if (data.currentAlpha >= 1.0f - 0.01f)
                {
                    renderer.sharedMaterials = data.originalMaterials;
                    toRemove.Add(renderer);
                }
            }
        }

        foreach (var renderer in toRemove)
        {
            if (occludedObjects.ContainsKey(renderer))
            {
                if (renderer != null) occludedObjects[renderer].Cleanup();
                occludedObjects.Remove(renderer);
            }
        }
    }

    private void ApplyAlphaToMaterials(MaterialData data, float alpha)
    {
        foreach (var mat in data.fadedMaterials)
        {
            if (mat == null) continue;

            if (mat.HasProperty("_Color"))
            {
                Color color = mat.color;
                color.a = alpha;
                mat.color = color;
            }

            if (mat.HasProperty("_TintColor"))
            {
                Color tintColor = mat.GetColor("_TintColor");
                tintColor.a = alpha;
                mat.SetColor("_TintColor", tintColor);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (var kvp in occludedObjects)
        {
            if (kvp.Key != null)
            {
                kvp.Key.sharedMaterials = kvp.Value.originalMaterials;
            }
            kvp.Value.Cleanup();
        }
        occludedObjects.Clear();
    }
}