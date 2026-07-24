using System.Collections;
using UnityEngine;

public class PetraTrailFader : MonoBehaviour
{
    #region Private Fields

    private LineRenderer lr;
    private float duration;

    #endregion

    #region Public Methods

    public void Init(LineRenderer lineRenderer, float fadeDuration)
    {
        lr = lineRenderer;
        duration = fadeDuration;
        StartCoroutine(FadeOut());
    }

    #endregion

    #region Private Methods

    private IEnumerator FadeOut()
    {
        if (lr == null) { Destroy(gameObject); yield break; }

        Color startColor = lr.startColor;
        Color endColor = lr.endColor;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            lr.startColor = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
            lr.endColor = new Color(endColor.r, endColor.g, endColor.b, Mathf.Lerp(endColor.a, 0f, t));
            yield return null;
        }

        Destroy(gameObject);
    }

    #endregion
}