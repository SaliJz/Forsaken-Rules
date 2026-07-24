using System.Collections;
using UnityEngine;

public class AporiaAestheticsManager : MonoBehaviour
{
    #region Enums
    [SerializeField] private enum EffectMode { None, Glitch, Pulse }
    #endregion

    #region Headers
    [Header("Configuracion de Modo")]
    [SerializeField] private EffectMode currentMode = EffectMode.None;

    [Header("Parametros de Glitch")]
    [SerializeField] private float glitchDuration = 0.35f;

    [Header("Parametros de Pulso")]
    [SerializeField] private float pulseSpeed = 4f;
    [SerializeField] private float pulseIntensity = 0.08f;
    #endregion

    #region Unity Events
    private void Start()
    {
        InitializeEffect();
    }
    #endregion

    #region Logic

    public void InitializeEffect()
    {
        StopAllCoroutines();

        switch (currentMode)
        {
            case EffectMode.Glitch:
                StartCoroutine(GlitchRoutine(gameObject, glitchDuration));
                break;
            case EffectMode.Pulse:
                StartCoroutine(PulseRoutine(gameObject));
                break;
        }
    }

    private IEnumerator GlitchRoutine(GameObject target, float dur)
    {
        float t = 0;
        MeshRenderer mr = target.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            Debug.LogWarning($"[AporiaAestheticsManager] '{name}': no se encontró MeshRenderer en '{target.name}'. El efecto Glitch no se reproducirá (¿usa SkinnedMeshRenderer u otro renderer?).");
            yield break;
        }

        while (t < dur)
        {
            mr.enabled = !mr.enabled;
            float wait = Random.Range(0.02f, 0.08f);
            yield return new WaitForSeconds(wait);
            t += wait;
        }
        mr.enabled = true;
    }

    private IEnumerator PulseRoutine(GameObject target)
    {
        Vector3 baseScale = target.transform.localScale;
        while (target != null)
        {
            float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
            target.transform.localScale = baseScale * scale;
            yield return null;
        }
    }
    #endregion
}