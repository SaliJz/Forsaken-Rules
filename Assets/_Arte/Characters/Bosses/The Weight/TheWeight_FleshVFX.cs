using System.Collections;
using UnityEngine;

public class TheWeight_FleshVFX : MonoBehaviour
{
    public float lifetime = 0.5f;
    [SerializeField] AnimationCurve curve;
    Transform t;
    Vector3 initialScale;

    void Awake()
    {
        t = transform;
        initialScale = t.localScale;
        StartCoroutine (lifeCycle());
    }

    IEnumerator lifeCycle()
    {
        float elapse = 0f;

        while(elapse < lifetime)
        {
            elapse += Time.deltaTime;
            float normalize = elapse /lifetime;

            t.localScale = initialScale * curve.Evaluate(normalize);

            yield return null;
            
        }

        Destroy(gameObject);
    }
}
