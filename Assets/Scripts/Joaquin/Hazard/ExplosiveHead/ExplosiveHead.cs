using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ExplosiveHead:
/// - Detecta proximidad con un contador (OnTriggerEnter/Exit).
/// - Reproduce grito y efectos visuales de priming.
/// - Tras priming explota, aplica da�o y empuje.
/// - Debug: OnGUI + Gizmos
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class ExplosiveHead : MonoBehaviour
{
    private enum HazardState { Armed, Priming, Exploded }
    private HazardState currentState = HazardState.Armed;

    [Header("Par�metros")]
    [Tooltip("Tiempo en segundos desde el grito hasta la explosi�n.")]
    [SerializeField] private float primingDuration = 1.2f;
    [Tooltip("Radio de la explosi�n para aplicar da�o y empuje.")]
    [SerializeField] private float explosionRadius = 5f;
    [Tooltip("Da�o m�ximo infligido por la explosi�n (en el centro).")]
    [SerializeField] private float explosionDamage = 40f;
    [Tooltip("Distancia de empuje aplicada a CharacterController (sin Rigidbody).")]
    [SerializeField] private float ccKnockbackDistance = 3f;

    [Header("Comportamiento")]
    [Tooltip("Si true, inicia la secuencia autom�ticamente al detectar objetivos en el trigger.")]
    [SerializeField] private bool enableProximityTrigger = true;
    [Tooltip("Si true, el da�o decrece con la distancia (falloff).")]
    [SerializeField] private bool useDamageFalloff = true;
    [Tooltip("Si true, la priming se cancelar� si el jugador sale del �rea antes de explotar.")]
    [SerializeField] private bool cancelIfPlayerLeaves = false;

    [Header("Filtrado")]
    [Tooltip("M�scara para limitar qu� capas reciben da�o/knockback.")]
    [SerializeField] private LayerMask affectLayers = ~0;

    [Header("Referencias")]
    [SerializeField] private GameObject visuals;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private ParticleSystem explosionVFXPrefab;
    [SerializeField] private AudioClip screamSound;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private GameObject explosionSpherePrefab;
    [SerializeField] private GameObject explosionAreaVFX;

    [Header("Glow / Priming visuals")]
    [SerializeField] private List<Renderer> renderersToFlash = new List<Renderer>();
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashMaxIntensity = 3f;
    [Tooltip("Curva que controla la intensidad del parpadeo en funci�n del progreso 0..1 (0 inicio, 1 momento de explosi�n).")]
    [SerializeField] private AnimationCurve intensityOverTime = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Curva que controla la frecuencia del parpadeo (Hz) en funci�n del progreso 0..1. Ej: 1 -> 1Hz, 6 -> 6Hz.")]
    [SerializeField] private AnimationCurve frequencyOverTime = AnimationCurve.Linear(0, 1f, 1, 8f);
    [Tooltip("Curve to shape the blink envelope (0..1 input -> 0..1 output). Use to ease the on/off of the blink.")]
    [SerializeField] private AnimationCurve blinkEnvelope = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Jaw (mand�bula)")]
    [Tooltip("Transform usado como mand�bula (un cubo rotado sobre X para pruebas).")]
    [SerializeField] private Transform jawTransform;
    [Tooltip("�ngulo (grados) de la mand�bula cerrada (normal).")]
    [SerializeField] private float jawClosedAngle = 0f;
    [Tooltip("�ngulo (grados) de la mand�bula abierta en el m�ximo grito (negativo para rotar hacia abajo).")]
    [SerializeField] private float jawOpenAngle = -45f;
    [Tooltip("Curve para la apertura de la mand�bula (0..1 input -> 0..1 output).")]
    [SerializeField] private AnimationCurve jawCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Overlap settings")]
    [Tooltip("Cantidad m�xima de colliders a procesar en la explosi�n (para OverlapSphereNonAlloc).")]
    [SerializeField] private int maxTargets = 32;

    [Header("Debug (OnGUI/Gizmos)")]
    [SerializeField] private bool showDebugHUD = true;
    [SerializeField] private bool verboseDebug = false;

    private Coroutine hazardRoutine;
    private SphereCollider triggerCollider;
    private Collider[] overlapResults;
    private float primingTimeLeft = 0f;
    private bool forceExplodeOnDamage = false;

    private Dictionary<Renderer, MaterialPropertyBlock> mpbs;
    private int emissionColorID;
    private float maxScale;

    private int targetsInTrigger = 0;
    
    private void Awake()
    {
        triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider == null)
        {
            Debug.LogError("[ExplosiveHead] Requiere SphereCollider.", this);
            enabled = false;
            return;
        }
        triggerCollider.isTrigger = true;

        maxScale = GetMaxLossyScale();
        overlapResults = new Collider[maxTargets];

        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

        mpbs = new Dictionary<Renderer, MaterialPropertyBlock>();
        emissionColorID = Shader.PropertyToID("_EmissionColor");

        if (renderersToFlash.Count == 0)
        {
            renderersToFlash.AddRange(GetComponentsInChildren<Renderer>(true));
        }

        foreach (var r in renderersToFlash)
        {
            if (r == null) continue;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpbs[r] = mpb;
        }

        if (intensityOverTime == null || intensityOverTime.keys.Length == 0)
            intensityOverTime = AnimationCurve.EaseInOut(0, 0, 1, 1);

        if (frequencyOverTime == null || frequencyOverTime.keys.Length == 0)
            frequencyOverTime = AnimationCurve.Linear(0, 1f, 1, 8f);

        if (jawCurve == null || jawCurve.keys.Length == 0)
            jawCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        if (blinkEnvelope == null || blinkEnvelope.keys.Length == 0)
            blinkEnvelope = AnimationCurve.EaseInOut(0, 0, 1, 1);

        if (verboseDebug) Debug.Log("[ExplosiveHead] Awake complete. Renderers controlled: " + mpbs.Count);
    }

    private void Update()
    {
        if (currentState == HazardState.Armed && enableProximityTrigger && targetsInTrigger > 0 && hazardRoutine == null)
        {
            if (verboseDebug) Debug.Log($"[ExplosiveHead] Objetivos detectados ({targetsInTrigger}), iniciando priming.");

            if (hazardRoutine == null)
            {
                hazardRoutine = StartCoroutine(ActivationSequence());
            }
        }
    }

    /// <summary>
    /// Se activa cuando un objeto entra en el SphereCollider (Trigger).
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsLayerInMask(other.gameObject.layer, affectLayers)) return;
        targetsInTrigger++;
        if (verboseDebug) Debug.Log($"[ExplosiveHead] OnTriggerEnter: {other.name}");
    }

    /// <summary>
    /// Se desactiva cuando un objeto entra en el SphereCollider (Trigger).
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (!IsLayerInMask(other.gameObject.layer, affectLayers)) return;
        targetsInTrigger = Mathf.Max(0, targetsInTrigger - 1);
        if (verboseDebug) Debug.Log($"[ExplosiveHead] OnTriggerExit: {other.name}");
    }

    public void StartPriming(bool forceExplode = true)
    {
        if (currentState != HazardState.Armed)
        {
            if (verboseDebug) Debug.Log("[ExplosiveHead] Intento de priming forzado, pero el estado no es Armed.", this);
            return;
        }

        this.forceExplodeOnDamage = forceExplode;

        if (verboseDebug) Debug.Log("[ExplosiveHead] Priming forzado por da�o externo (escudo).", this);

        if (hazardRoutine != null)
        {
            StopCoroutine(hazardRoutine);
        }
        hazardRoutine = StartCoroutine(ActivationSequence());
    }

    /// <summary>
    /// Permite activar/desactivar el sensor de proximidad desde otros scripts.
    /// </summary>
    public void SetProximityTrigger(bool enabled)
    {
        enableProximityTrigger = enabled;
    }

    /// <summary>
    /// Corrutina que maneja la secuencia de grito -> espera -> explosi�n.
    /// </summary>
    private IEnumerator ActivationSequence()
    {
        currentState = HazardState.Priming;
        primingTimeLeft = primingDuration;
        PlayAudio(screamSound, 0.85f);

        explosionAreaVFX.SetActive(true);

        float elapsed = 1.4f;

        bool canBeCanceled = cancelIfPlayerLeaves && !forceExplodeOnDamage;

        while (elapsed < primingDuration)
        {
            if (canBeCanceled)
            {
                if (targetsInTrigger == 0)
                {
                    if (verboseDebug) Debug.Log("[ExplosiveHead] Priming cancelado: ya no hay objetivos.");
                    ResetVisuals();
                    currentState = HazardState.Armed;
                    hazardRoutine = null;
                    forceExplodeOnDamage = false; 
                    yield break;
                }
            }

            elapsed += Time.deltaTime;
            primingTimeLeft = Mathf.Max(0f, primingDuration - elapsed);

            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, primingDuration));

            UpdateVisuals(t, elapsed);

            yield return null;
        }

        primingTimeLeft = 0f;
        Explode();

        currentState = HazardState.Exploded;
        forceExplodeOnDamage = false; 

        float vfxDuration = explosionVFXPrefab != null ? explosionVFXPrefab.main.duration : 0.1f;
        Destroy(gameObject, vfxDuration + 0.2f);

        hazardRoutine = null;
    }

    private void UpdateVisuals(float progress, float elapsedTime)
    {
        // Parpadeo
        float freq = frequencyOverTime.Evaluate(progress);
        float intensityFactor = intensityOverTime.Evaluate(progress);
        float envelope = blinkEnvelope.Evaluate(progress);

        float blinkWave = Mathf.Sin(elapsedTime * freq * Mathf.PI * 2f) * 0.5f + 0.5f;
        float finalIntensity = blinkWave * envelope * intensityFactor;

        ApplyFlashToRenderers(finalIntensity * flashMaxIntensity);

        // Mand�bula
        if (jawTransform != null)
        {
            float jawT = jawCurve.Evaluate(progress);
            float angle = Mathf.Lerp(jawClosedAngle, jawOpenAngle, jawT);

            jawTransform.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }

    private void ApplyFlashToRenderers(float emissionIntensity)
    {
        Color emissionColor = flashColor * Mathf.Clamp01(emissionIntensity);
        foreach (var kv in mpbs)
        {
            var renderer = kv.Key;
            var mpb = kv.Value;
            if (renderer == null) continue;
            renderer.GetPropertyBlock(mpb);
            mpb.SetColor(emissionColorID, emissionColor);
            renderer.SetPropertyBlock(mpb);
        }
    }

    private void ResetVisuals()
    {
        ApplyFlashToRenderers(0);
        if (jawTransform != null)
        {
            jawTransform.localRotation = Quaternion.Euler(jawClosedAngle, 0f, 0f);
        }
    }

    /// <summary>
    /// Gestiona la l�gica de la explosi�n: da�o, empuje y efectos.
    /// </summary>
    private void Explode()
    {
        Vector3 center = GetTriggerWorldCenter();
        float worldExplosionRadius = explosionRadius * GetMaxLossyScale();

        if (explosionSpherePrefab != null)
        {
            var sphere = Instantiate(explosionSpherePrefab, center, Quaternion.identity);

            if (sphere.TryGetComponent<ExplosionScaleOverTime>(out var scaler))
            {
                scaler.EndScale = Vector3.one * worldExplosionRadius * 2;
            }
        }

        if (explosionVFXPrefab != null)
        {
            var vfxInstance = Instantiate(explosionVFXPrefab, center, Quaternion.identity);
            if (vfxInstance != null) Destroy(vfxInstance.gameObject, 0.75f);
        }

        PlayAudio(explosionSound, 1f);
        ResetVisuals();
        if (visuals != null) visuals.SetActive(false);

        int found = Physics.OverlapSphereNonAlloc(center, worldExplosionRadius, overlapResults, affectLayers);

        for (int i = 0; i < found; i++)
        {
            Collider collider = overlapResults[i];
            if (collider.transform.IsChildOf(transform) || collider.transform == transform) continue;

            float distance = Vector3.Distance(center, collider.ClosestPoint(center));
            float normalizedDistance = Mathf.Clamp01(distance / worldExplosionRadius);
            float dmg = useDamageFalloff ? Mathf.Lerp(explosionDamage, 0f, normalizedDistance) : explosionDamage;

            if (collider.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(dmg);
            }
            else
            {
                PlayerHealth playerHealth = collider.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(dmg);
                }
            }

            if (collider.TryGetComponent<PlayerKnockbackReceiver>(out var knockbackReceiver))
            {
                Vector3 direction = (collider.transform.position - center).normalized;
                direction.y = Mathf.Max(direction.y, 0.1f);

                float pushStrength = ccKnockbackDistance * (1f - normalizedDistance);

                knockbackReceiver.ApplyKnockback(direction, pushStrength);
            }
        }
    }

    private void PlayAudio(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Devuelve el centro del SphereCollider en coordenadas mundo.
    /// </summary>
    private Vector3 GetTriggerWorldCenter() => transform.TransformPoint(triggerCollider.center);

    /// <summary>
    /// Obtiene el mayor factor de escala para convertir radios locales a world space.
    /// </summary>
    private float GetMaxLossyScale()
    {
        Vector3 s = transform.lossyScale;
        return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    #region Gizmos & Debug HUD

    private void OnDrawGizmos()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider != null)
        {
            Vector3 center = GetTriggerWorldCenter();
            float radiusWorld = triggerCollider.radius * GetMaxLossyScale();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, radiusWorld);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetTriggerWorldCenter(), explosionRadius * GetMaxLossyScale());
    }

    private void OnGUI()
    {
        if (!showDebugHUD) return;

        GUIStyle box = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            padding = new RectOffset(10, 10, 10, 10)
        };

        Rect rect = new Rect(10, 10, 360, 210);
        GUILayout.BeginArea(rect, box);

        GUILayout.Label($"ExplosiveHead Debug (obj: {gameObject.name})");
        GUILayout.Space(4);
        GUILayout.Label($"State: {currentState}");

        if (currentState == HazardState.Priming)
        {
            GUILayout.Label($"Time to explode (s): {primingTimeLeft:F2}");
        }
        GUILayout.Label($"Trigger center (world): {GetTriggerWorldCenter()}");
        GUILayout.Label($"Trigger radius (world): {triggerCollider.radius * GetMaxLossyScale():F2}");
        GUILayout.Label($"Explosion radius: {explosionRadius:F2}");
        GUILayout.Space(6);
        if (GUILayout.Button("Force Explode"))
        {
            if (hazardRoutine != null) StopCoroutine(hazardRoutine);
            Explode();
        }

        GUILayout.EndArea();
    }

    #endregion
}