using UnityEngine;

public class TutoAldousMaterialOverride : MonoBehaviour
{
    [SerializeField] Renderer targetRenderer;
    [SerializeField] AnimationCurve curve;
    [SerializeField] float duration = 1f;

    Material mat;
    float timer = 0f;
    bool playing = false;

    void Start()
    {
        mat = targetRenderer.material;
        playing = true;
    }

    void Update()
    {
        if (!playing) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer/duration);
        mat.SetFloat("_Amount", curve.Evaluate(t));

        if (t >= 1f) playing = false;
    }
}
