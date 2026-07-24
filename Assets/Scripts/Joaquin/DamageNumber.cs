using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DamageNumber : MonoBehaviour
{
    #region Inspector - References

    [Header("References")]
    [SerializeField] private TMP_Text damageText;

    #endregion

    #region Inspector - Configuration

    [Header("Configuration")]
    [SerializeField] private float floatSpeed = 2f;
    //[SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private float gravity = 9.8f;
    [SerializeField] private float lateralSpeed = 0.5f;
    [SerializeField] private float gravityDelay = 0.5f;

    #endregion

    #region Inspector - Colors

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color criticalColor = Color.red;
    [SerializeField] private Color toughnessColor = Color.cyan;

    #endregion

    #region Inspector - Scale Punch

    [Header("Scale Punch")]
    [SerializeField] private float punchScale = 1.5f;
    [SerializeField] private float punchDuration = 0.12f;

    #endregion

    #region Inspector - Timing

    [Header("Timing")]
    [SerializeField, Range(0f, 1f)] private float holdFraction = 0.3f;

    #endregion

    #region Inspector - Rotation

    [Header("Rotation")]
    [SerializeField] private float maxRotationAngle = 12f;

    #endregion

    #region Inspector - Critical Scale

    [Header("Critical Scale")]
    [SerializeField] private float criticalScale = 1.6f;
    [SerializeField] private float normalScale = 1f;

    #endregion

    #region Internal State

    private CanvasGroup canvasGroup;
    private Vector3 velocity;
    private float targetScale;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    #endregion

    #region Core Logic

    public void Initialize(float damage, bool isCritical, bool isToughness = false)
    {
        if (damageText == null) return;

        float visualDamage = Mathf.Max(1f, damage);

        string prefix = isCritical ? "!" : string.Empty;
        damageText.text = $"{prefix}{Mathf.RoundToInt(visualDamage)}";

        if (isToughness)
        {
            damageText.color = toughnessColor;
        }
        else
        {
            damageText.color = isCritical ? criticalColor : normalColor;
        }

        targetScale = isCritical ? criticalScale : normalScale;
        transform.localScale = Vector3.zero;

        float lateral = Random.Range(-lateralSpeed, lateralSpeed);
        velocity = new Vector3(lateral, floatSpeed, 0f);

        float angle = Random.Range(-maxRotationAngle, maxRotationAngle);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        StartCoroutine(AnimateAndDeactivate());
    }

    public void SetToughnessColor(Color color)
    {
        toughnessColor = color;
    }

    public void SetHealthColor(Color normalColor, Color criticalColor)
    {
        this.normalColor = normalColor;
        this.criticalColor = criticalColor;
    }

    public void Deactivate()
    {
        StopAllCoroutines();
        Destroy(gameObject);
    }

    private IEnumerator AnimateAndDeactivate()
    {
        float punchElapsed = 0f;
        while (punchElapsed < punchDuration)
        {
            punchElapsed += Time.deltaTime;
            float t = punchElapsed / punchDuration;

            float scale = t < 0.5f
                ? Mathf.Lerp(0f, punchScale, t * 2f)
                : Mathf.Lerp(punchScale, targetScale, (t - 0.5f) * 2f);

            transform.localScale = Vector3.one * scale;
            yield return null;
        }
        transform.localScale = Vector3.one * targetScale;

        float elapsed = 0f;
        float holdTime = lifetime * holdFraction;
        float fadeDuration = lifetime - holdTime;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;

            if (elapsed >= gravityDelay)
            {
                velocity.y -= gravity * Time.deltaTime;
            }

            transform.position += velocity * Time.deltaTime;

            if (elapsed > holdTime)
            {
                float fadeT = (elapsed - holdTime) / fadeDuration;
                canvasGroup.alpha = 1f - (fadeT * fadeT);
            }
            else
            {
                canvasGroup.alpha = 1f;
            }

            yield return null;
        }

        Deactivate();
    }

    #endregion
}