using UnityEngine;
using System.Collections;

public class HUDModifier : MonoBehaviour
{
    public static HUDModifier Instance { get; private set; }

    [Header("Configuración de Distorsión")]
    [SerializeField] private RectTransform healthBarRect;

    private Vector2 originalBarPosition;
    private Coroutine distortionCoroutine;
    private float effectTimer = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (healthBarRect != null)
            originalBarPosition = healthBarRect.anchoredPosition;
    }

    public void ApplyUIDistortion(float duration, float intensity)
    {
        effectTimer = duration;

        if (distortionCoroutine != null) return;

        distortionCoroutine = StartCoroutine(DistortionRoutine(intensity));
    }

    private IEnumerator DistortionRoutine(float intensity)
    {
        float shakeForce = intensity * 20f;

        while (effectTimer > 0)
        {
            effectTimer -= Time.deltaTime;

            if (healthBarRect != null)
            {
                float offsetX = Random.Range(-1f, 1f) * shakeForce;
                float offsetY = Random.Range(-1f, 1f) * shakeForce;
                healthBarRect.anchoredPosition = originalBarPosition + new Vector2(offsetX, offsetY);

                float scaleX = 1f + Random.Range(-0.1f, 0.1f) * intensity;
                float scaleY = 1f + Random.Range(-0.1f, 0.1f) * intensity;
                healthBarRect.localScale = new Vector3(scaleX, scaleY, 1f);
            }

            yield return null;
        }

        ResetHUD();
    }

    private void ResetHUD()
    {
        if (healthBarRect != null)
        {
            healthBarRect.anchoredPosition = originalBarPosition;
            healthBarRect.localScale = Vector3.one;
        }
        distortionCoroutine = null;
    }
}