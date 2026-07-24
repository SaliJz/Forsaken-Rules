// PentagramBelcebu.cs
// Coloca este script en el GameObject VISUAL del pentagrama (el mismo GameObject debe tener un Collider isTrigger).
// Asegúrate que el GameObject tiene un Renderer y que el Player usa el tag "Player".
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Renderer))]
public class PentagramBelcebu : MonoBehaviour
{
    [Header("Referencias visuales")]
    [Tooltip("Renderer del mismo GameObject (se tomará automáticamente si no lo asignas).")]
    public Renderer pentagramRenderer;

    [Header("Colores HDR para parpadeo (idle)")]
    [ColorUsage(false, true)]
    public Color idleColorA = Color.white;
    [ColorUsage(false, true)]
    public Color idleColorB = Color.yellow;

    [Header("Intensidades HDR (idle)")]
    [Tooltip("Intensidad mínima (puede ser <1).")]
    public float idleMinIntensity = 0.2f;
    [Tooltip("Intensidad máxima (puede ser >1 para HDR).")]
    public float idleMaxIntensity = 2.0f;
    [Tooltip("Velocidad del pulso idle (1 = normal).")]
    public float idlePulseSpeed = 1f;

    [Header("Al entrar (Player) - flash principal")]
    [Tooltip("Intensidad objetivo al entrar (valor HDR, p.ej. 6-20 para flash fuerte).")]
    public float enterIntensity = 8f;
    [Tooltip("Tiempo que mantiene la intensidad al entrar antes de continuar (s).")]
    public float enterBrightDuration = 0.35f;
    [Tooltip("Duración del ramp (s) para subir a la intensidad de entrada. 0 = instantáneo")]
    public float enterRampTime = 0.08f;
    [Tooltip("Ocultar el renderer del pentagrama después de la activación.")]
    public bool hideOnActivate = false;

    [Header("Flash secundario potente (amarillo HDR)")]
    [ColorUsage(false, true)]
    [Tooltip("Color del flash secundario (por defecto amarillo HDR).")]
    public Color enterSecondaryColor = Color.yellow;
    [Tooltip("Intensidad HDR muy alta para el flash secundario (p.ej. 20-80).")]
    public float enterSecondaryIntensity = 40f;
    [Tooltip("Duración del flash secundario en segundos.")]
    public float enterSecondaryDuration = 0.12f;
    [Tooltip("Delay entre el flash principal y el secundario (s).")]
    public float secondaryDelayAfterPrimary = 0.05f;

    [Header("Enjambre")]
    public GameObject swarmPrefab;

    // runtime
    private Material _instancedMaterial;
    private bool _activated = false;
    private Coroutine _idleCoroutine;
    private Collider _myTrigger;

    private void Reset()
    {
        if (pentagramRenderer == null) pentagramRenderer = GetComponent<Renderer>();
    }

    private void Awake()
    {
        _myTrigger = GetComponent<Collider>();
        if (_myTrigger == null)
        {
            Debug.LogError("[PentagramBelcebu] Requiere un Collider en este GameObject (marcar isTrigger).");
        }
        else if (!_myTrigger.isTrigger)
        {
            Debug.LogWarning("[PentagramBelcebu] El Collider no está marcado como isTrigger. Se recomienda marcarlo.");
        }

        if (pentagramRenderer == null) pentagramRenderer = GetComponent<Renderer>();
        if (pentagramRenderer == null)
        {
            Debug.LogError("[PentagramBelcebu] No se encontró Renderer en este GameObject.");
            return;
        }

        // Instanciar material para no modificar el shared asset
        _instancedMaterial = pentagramRenderer.material;
        if (_instancedMaterial == null)
        {
            Debug.LogError("[PentagramBelcebu] No se pudo obtener material instanciado del Renderer.");
            return;
        }

        // Forzar color base al color A por defecto
        if (_instancedMaterial.HasProperty("_Color"))
            _instancedMaterial.SetColor("_Color", idleColorA);

        // Asegurar keyword de emisión
        if (_instancedMaterial.HasProperty("_EmissionColor"))
            _instancedMaterial.EnableKeyword("_EMISSION");

        // Valor inicial de emisión (idleMinIntensity)
        SetEmissionImmediate(idleColorA, idleMinIntensity);
    }

    private void Start()
    {
        if (_instancedMaterial != null)
            _idleCoroutine = StartCoroutine(IdlePulse());
    }

    private IEnumerator IdlePulse()
    {
        float t = 0f;
        while (!_activated)
        {
            t += Time.deltaTime * idlePulseSpeed;
            float s = (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f; // 0..1
            float intensity = Mathf.Lerp(idleMinIntensity, idleMaxIntensity, s);
            Color baseColor = Color.Lerp(idleColorA, idleColorB, s);
            ApplyEmission(baseColor, intensity);
            yield return null;
        }
    }

    // Aplica emisión usando el color base y la intensidad HDR (no clamp)
    private void ApplyEmission(Color baseColor, float intensity)
    {
        if (_instancedMaterial == null) return;

        if (_instancedMaterial.HasProperty("_EmissionColor"))
        {
            Color linear = baseColor.linear * Mathf.Max(0f, intensity);
            _instancedMaterial.SetColor("_EmissionColor", linear);
            _instancedMaterial.EnableKeyword("_EMISSION");
            DynamicGI.SetEmissive(pentagramRenderer, linear);
        }
        else if (_instancedMaterial.HasProperty("_Color"))
        {
            float clamped = Mathf.Clamp01(intensity);
            _instancedMaterial.SetColor("_Color", baseColor * clamped);
        }
    }

    // Set instantáneo (sin tween) — útil al inicializar o al volver del hide
    private void SetEmissionImmediate(Color baseColor, float intensity)
    {
        ApplyEmission(baseColor, intensity);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_activated) return;
        if (!other.CompareTag("Player")) return;

        _activated = true;

        if (_idleCoroutine != null) StopCoroutine(_idleCoroutine);

        StartCoroutine(EnterSequenceWithSecondaryFlash());

        // desactivar trigger para evitar reactivaciones
        if (_myTrigger != null) _myTrigger.enabled = false;
    }

    // Secuencia: ramp -> flash principal -> (opcional delay) -> flash secundario potente -> finalizar
    private IEnumerator EnterSequenceWithSecondaryFlash()
    {
        // 1) Ramp / flash principal (mantiene enterBrightDuration)
        yield return StartCoroutine(EnterBrightThenWait(enterIntensity, enterBrightDuration, enterRampTime));

        // 2) short delay before secondary (if any)
        if (secondaryDelayAfterPrimary > 0f)
            yield return new WaitForSeconds(secondaryDelayAfterPrimary);

        // 3) Secondary super-bright yellow HDR flash (instantáneo)
        ApplyEmission(enterSecondaryColor, enterSecondaryIntensity);
        yield return new WaitForSeconds(enterSecondaryDuration);

        // 4) Finalizar: ocultar o volver a idle
        if (hideOnActivate && pentagramRenderer != null)
        {
            pentagramRenderer.enabled = false;
        }
        else
        {
            SetEmissionImmediate(idleColorA, idleMinIntensity);
        }

        SpawnSwarmAndHide();
    }

    // Reutilizable: rampa hasta targetIntensity y espera 'duration' (flash principal)
    private IEnumerator EnterBrightThenWait(float targetIntensity, float duration, float ramp)
    {
        Color rampColor = idleColorB; // preferimos el flash amarillo en la entrada

        if (ramp > 0f)
        {
            // Intentamos leer un color de inicio razonable (si existe)
            Color startColor = idleColorA;
            if (_instancedMaterial != null && _instancedMaterial.HasProperty("_EmissionColor"))
            {
                // GetColor devuelve el color ya multiplicado por intensidad (en linear space),
                // así que usamos solo su color aproximado (aceptable para un ramp corto).
                Color current = _instancedMaterial.GetColor("_EmissionColor");
                // Evitamos valores extremadamente altos dividiendo por average if needed:
                float avg = (current.r + current.g + current.b) / 3f;
                if (avg > 0.0001f)
                    startColor = current / avg;
            }

            float elapsed = 0f;
            while (elapsed < ramp)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / ramp)); // easing
                Color c = Color.Lerp(startColor, rampColor, k);
                float it = Mathf.Lerp(idleMaxIntensity, targetIntensity, k);
                ApplyEmission(c, it);
                yield return null;
            }
        }
        else
        {
            ApplyEmission(rampColor, targetIntensity);
        }

        yield return new WaitForSeconds(duration);
    }

    private void SpawnSwarmAndHide()
    {
        if (swarmPrefab != null)
        {
            GameObject instance = Instantiate(swarmPrefab, transform.position, Quaternion.identity);
            var swarmController = instance.GetComponent<SwarmController>();
            if (swarmController != null)
            {
                swarmController.InitializeFromPentagram(transform.position);
            }
            else
            {
                Debug.LogWarning("[PentagramBelcebu] swarmPrefab instanciado pero no tiene SwarmController en root.");
            }
        }
        else
        {
            Debug.LogWarning("[PentagramBelcebu] swarmPrefab no asignado en el Inspector.");
        }
    }

    // Método público para forzar la activación desde código
    public void ForceActivate()
    {
        if (!_activated)
        {
            _activated = true;
            if (_idleCoroutine != null) StopCoroutine(_idleCoroutine);
            StartCoroutine(EnterSequenceWithSecondaryFlash());
            if (_myTrigger != null) _myTrigger.enabled = false;
        }
    }

    private void OnDisable()
    {
        // Liberar la instancia del material para evitar leaks (en build y editor)
        if (_instancedMaterial != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(_instancedMaterial);
#else
            Destroy(_instancedMaterial);
#endif
            _instancedMaterial = null;
        }
    }
}
