using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class SueloFragilFeedback : MonoBehaviour
{
    [Header("Referencias (intentar autoconfigurar si es posible)")]
    [Tooltip("Referencia al SueloFragil (si está en el mismo GameObject se asigna automáticamente).")]
    public SueloFragil sueloFragil;
    [Tooltip("Renderer visible de la grieta. Si está vacío, intentará usar sueloFragil.meshGrieta.")]
    public MeshRenderer crackRenderer;

    [Header("Feedback visual")]
    [Tooltip("Color con el que 'parpadeará' la grieta para avisar al jugador.")]
    public Color pulseColor = Color.red;
    [Tooltip("Velocidad del pulso (más alto = parpadea más rápido).")]
    public float pulseSpeed = 3f;
    [Tooltip("Intensidad máxima de emisión / mezcla (0..5 en la mayoría de shaders).")]
    [Range(0f, 5f)]
    public float pulseIntensity = 2f;

    [Header("Comportamiento")]
    [Tooltip("Si true, solo responde a objetos con tag 'Player'.")]
    public bool requirePlayerTag = true;

    // Estado
    private HashSet<GameObject> playersInside = new HashSet<GameObject>();
    private Coroutine pulseCoroutine;
    private MaterialPropertyBlock mpb;
    private Color baseColor = Color.white;
    private float baseEmission = 0f;
    private bool alreadyPlayedBreak = false;

    private void Reset()
    {
        // Intentar asignar referencias por defecto cuando se añade el componente
        sueloFragil = GetComponent<SueloFragil>();
        if (sueloFragil != null && crackRenderer == null) crackRenderer = sueloFragil.meshGrieta;
    }

    private void Awake()
    {
        if (sueloFragil == null)
        {
            sueloFragil = GetComponent<SueloFragil>();
        }

        if (crackRenderer == null && sueloFragil != null)
        {
            crackRenderer = sueloFragil.meshGrieta;
        }

        mpb = new MaterialPropertyBlock();

        // Guardar valores base si hay renderer
        if (crackRenderer != null && crackRenderer.sharedMaterial != null)
        {
            var mat = crackRenderer.sharedMaterial;
            if (mat.HasProperty("_BaseColor"))
            {
                baseColor = mat.GetColor("_BaseColor");
                baseEmission = baseColor.maxColorComponent;
            }
            else if (mat.HasProperty("_Color"))
            {
                baseColor = mat.GetColor("_Color");
                baseEmission = baseColor.maxColorComponent;
            }
            else
            {
                baseColor = Color.white;
                baseEmission = 0f;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (requirePlayerTag && !other.gameObject.CompareTag("Player")) return;

        // Registrar jugador y si es el primero, iniciar feedback
        if (!playersInside.Contains(other.gameObject))
        {
            playersInside.Add(other.gameObject);

            if (playersInside.Count == 1)
            {
                StartPreBreakFeedback();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (requirePlayerTag && !other.gameObject.CompareTag("Player")) return;

        if (playersInside.Contains(other.gameObject))
        {
            playersInside.Remove(other.gameObject);

            // Si ya no hay jugadores dentro y la losa no se rompió, detener feedback
            if (playersInside.Count == 0 && !IsAlreadyBroken())
            {
                StopPreBreakFeedback();
            }
        }
    }

    private void StartPreBreakFeedback()
    {
        if (alreadyPlayedBreak) return;

        // Iniciar pulso visual
        if (crackRenderer != null)
        {
            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
            pulseCoroutine = StartCoroutine(PulseRoutine());
        }

        // Comenzar a vigilar la ruptura (si SueloFragil existe)
        if (sueloFragil != null)
        {
            StartCoroutine(WatchForBreak());
        }
    }

    private void StopPreBreakFeedback()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        RestoreRendererBase();
    }

    private IEnumerator PulseRoutine()
    {
        // Pulso hasta que algo lo detenga
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * pulseSpeed;
            float ping = Mathf.PingPong(t, 1f); // 0..1
            // Interpolar color/emission
            Color col = Color.Lerp(baseColor, pulseColor, ping);
            float emission = Mathf.Lerp(baseEmission, pulseIntensity, ping);

            mpb.Clear();
            if (crackRenderer != null)
            {
                // Intentar setear BaseColor y _EmissionColor si existen
                var mat = crackRenderer.sharedMaterial;
                if (mat != null && mat.HasProperty("_BaseColor"))
                {
                    mpb.SetColor("_BaseColor", col);
                    mpb.SetColor("_EmissionColor", col * emission);
                }
                else if (mat != null && mat.HasProperty("_Color"))
                {
                    mpb.SetColor("_Color", col);
                    mpb.SetColor("_EmissionColor", col * emission);
                }
                else
                {
                    // Fallback: usar _EmissionColor solo
                    mpb.SetColor("_EmissionColor", col * emission);
                }
                crackRenderer.SetPropertyBlock(mpb);
            }

            yield return null;
        }
    }

    // Vigila el SueloFragil para detectar el momento exacto de ruptura
    private IEnumerator WatchForBreak()
    {
        // Si no hay SueloFragil no podemos vigilar
        if (sueloFragil == null) yield break;

        MeshRenderer watchRenderer = sueloFragil.meshGrieta;
        Collider watchCollider = sueloFragil.GetComponent<Collider>();

        while (true)
        {
            // Si el mesh se desactiva -> ruptura
            if (watchRenderer != null && !watchRenderer.enabled)
            {
                HandleBroke();
                yield break;
            }

            // Si el collider se desactiva -> ruptura
            if (watchCollider != null && !watchCollider.enabled)
            {
                HandleBroke();
                yield break;
            }

            // Por seguridad, si el GameObject deja de existir
            if (sueloFragil == null)
            {
                HandleBroke();
                yield break;
            }

            yield return null;
        }
    }

    private void HandleBroke()
    {
        if (alreadyPlayedBreak) return;
        alreadyPlayedBreak = true;

        // Parar pulso
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        // Restaurar renderer o dejarlo oculto si la grieta fue desactivada por SueloFragil.
        RestoreRendererBase();
    }

    private void RestoreRendererBase()
    {
        if (crackRenderer == null) return;

        mpb.Clear();
        if (crackRenderer.sharedMaterial.HasProperty("_BaseColor"))
        {
            mpb.SetColor("_BaseColor", baseColor);
            mpb.SetColor("_EmissionColor", baseColor * baseEmission);
        }
        else if (crackRenderer.sharedMaterial.HasProperty("_Color"))
        {
            mpb.SetColor("_Color", baseColor);
            mpb.SetColor("_EmissionColor", baseColor * baseEmission);
        }
        crackRenderer.SetPropertyBlock(mpb);
    }

    private bool IsAlreadyBroken()
    {
        if (sueloFragil == null) return false;
        if (sueloFragil.meshGrieta != null && !sueloFragil.meshGrieta.enabled) return true;
        Collider col = sueloFragil.GetComponent<Collider>();
        if (col != null && !col.enabled) return true;
        return false;
    }

    private void OnDestroy()
    {
        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Intentar autoconfigurar referencias mínimas
        if (sueloFragil == null) sueloFragil = GetComponent<SueloFragil>();
        if (crackRenderer == null && sueloFragil != null) crackRenderer = sueloFragil.meshGrieta;
        pulseSpeed = Mathf.Max(0.01f, pulseSpeed);
        pulseIntensity = Mathf.Max(0f, pulseIntensity);
    }
#endif
}
