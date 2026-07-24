// AreaAcido.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AreaAcido (3D): colocado en el prefab del hueco (Collider con IsTrigger).
/// Escala inicial y final completas (X,Y,Z) y transici�n fluida entre ellas.
/// A�ade opciones para alinear la posici�n/rotaci�n local al iniciarse (�til cuando
/// el prefab tiene children con offsets y lo instancias desde otro objeto).
/// </summary>
public class AreaAcido : MonoBehaviour
{
    [Header("Parametros acido")]
    public float dps = 6f;
    public float tickInterval = 1f;
    public float duracionVeneno = 3f;
    public float danoVenenoPorSeg = 2f;
    [Range(0f, 1f)] public float porcentajeRalentizacion = 0.5f;

    [Header("Efectos")]
    public AudioClip sfxEntrar;
    public AudioClip sfxDanoTick;
    public ParticleSystem psEnter;

    [Header("Escalado (X,Y,Z)")]
    [Tooltip("Si est� activado, se aplicar� un escalado al Start desde 'escalaInicial' hasta 'escalaFinal'.")]
    public bool scaleOnStart = true;

    [Tooltip("Si true, la escala inicial se tomar� de 'escalaInicial'. Si false, la escala inicial ser� la escala actual del transform.")]
    public bool usarEscalaInicialExplicita = true;

    [Tooltip("Escala inicial (X,Y,Z).")]
    public Vector3 escalaInicial = new Vector3(0.1f, 0.1f, 0.1f);

    [Tooltip("Escala final (X,Y,Z).")]
    public Vector3 escalaFinal = new Vector3(1f, 1f, 1f);

    [Tooltip("Duraci�n del escalado en segundos. Si <= 0 se aplica instant�neamente.")]
    public float escalaDuration = 0.5f;

    [Tooltip("Curva de easing para el escalado (0..1). Si es null se usar� ease in/out por defecto.")]
    public AnimationCurve escalaCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Retardo antes de comenzar el escalado en segundos.")]
    public float scaleDelay = 0f;

    [Header("Posicionamiento al instanciar")]
    [Tooltip("Si est� activado y el objeto tiene padre al iniciar en runtime, se ajustar� la posici�n/rotaci�n local.")]
    public bool alignToParentOnStart = true;

    [Tooltip("Offset local de posici�n que se aplicar� cuando se alinee (localPosition = offset).")]
    public Vector3 localPositionOffset = Vector3.zero;

    [Tooltip("Offset local de rotaci�n (Euler) que se aplicar� cuando se alinee (localEulerAngles = offset).")]
    public Vector3 localEulerOffset = Vector3.zero;

    // Mapa de coroutines por jugador (para aplicar da�o peri�dico)
    private Dictionary<GameObject, Coroutine> coroutinesPorJugador = new Dictionary<GameObject, Coroutine>();

    // Guarda la referencia de la coroutine de escalado para poder detenerla sin afectar otras coroutines
    private Coroutine scaleCoroutine;

    // AudioSource localizado en el mismo GameObject (si existe)
    private AudioSource localAudioSource;

    private void Start()
    {
        // Intentar obtener AudioSource en el mismo GameObject
        localAudioSource = GetComponent<AudioSource>();
        if (localAudioSource == null)
        {
            Debug.LogWarning($"[AreaAcido] No se encontr� AudioSource en '{gameObject.name}'. Usar� PlayClipAtPoint como fallback.");
        }

        // Alineado de posici�n/rotaci�n local si fue instanciado como hijo en runtime
        if (Application.isPlaying && alignToParentOnStart && transform.parent != null)
        {
            transform.localPosition = localPositionOffset;
            // transform.localEulerAngles = localEulerOffset;
        }
        

        Vector3 current = transform.localScale;

        Vector3 initial = usarEscalaInicialExplicita ? escalaInicial : current;
        Vector3 target = escalaFinal;

        if (scaleOnStart)
        {
            if (escalaDuration <= 0f)
            {
                transform.localScale = target;
            }
            else
            {
                transform.localScale = initial;
                scaleCoroutine = StartCoroutine(RutinaEscalar(initial, target, escalaDuration, scaleDelay, escalaCurve));
            }
        }
    }

    /// <summary>
    /// Forzar alineado inmediatamente (�til si instancias y quieres ajustar desde el creador).
    /// </summary>
    public void ForceAlignNow()
    {
        if (transform.parent == null) return;
        transform.localPosition = localPositionOffset;
        // transform.localEulerAngles = localEulerOffset;
    }

    private IEnumerator RutinaEscalar(Vector3 from, Vector3 to, float duration, float delay, AnimationCurve curve)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        AnimationCurve activeCurve = curve != null ? curve : escalaCurve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);

        if (duration <= 0f)
        {
            transform.localScale = to;
            scaleCoroutine = null;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            float eased = activeCurve.Evaluate(normalized);
            transform.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }
        transform.localScale = to;
        scaleCoroutine = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.CompareTag("Player")) return;

        GameObject go = other.gameObject;
        if (coroutinesPorJugador.ContainsKey(go)) return;
        Coroutine c = StartCoroutine(RutinaAplicarEfectos(go));
        coroutinesPorJugador.Add(go, c);

        // Reproducir SFX de entrada usando el AudioSource local si existe, si no fallback a PlayClipAtPoint
        if (sfxEntrar != null)
        {
            if (localAudioSource != null)
            {
                localAudioSource.PlayOneShot(sfxEntrar);
            }
            else
            {
                AudioSource.PlayClipAtPoint(sfxEntrar, transform.position);
            }
        }

        if (psEnter != null) psEnter.Play();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.gameObject.CompareTag("Player")) return;

        GameObject go = other.gameObject;
        if (coroutinesPorJugador.TryGetValue(go, out Coroutine c))
        {
            StopCoroutine(c);
            coroutinesPorJugador.Remove(go);
            PlayerHealth ph = go.GetComponent<PlayerHealth>();
            if (ph != null) ph.RemoveSlow();
        }
    }

    private IEnumerator RutinaAplicarEfectos(GameObject jugador)
    {
        PlayerHealth ph = jugador.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.ApplySlow(porcentajeRalentizacion, duracionVeneno);
            ph.ApplyPoison(duracionVeneno, danoVenenoPorSeg, tickInterval);
        }

        while (true)
        {
            if (ph != null)
            {
                ph.TakeDamage(dps * tickInterval);
            }

            // Reproducir SFX de da�o por tick usando AudioSource local si existe, si no fallback a PlayClipAtPoint
            if (sfxDanoTick != null)
            {
                if (localAudioSource != null)
                {
                    localAudioSource.PlayOneShot(sfxDanoTick);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(sfxDanoTick, transform.position);
                }
            }

            yield return new WaitForSeconds(tickInterval);
        }
    }

    /// <summary>
    /// Inicia un escalado manual hacia los valores X,Y,Z indicados.
    /// </summary>
    public void StartScaleTo(float targetX, float targetY, float targetZ, float duration, float delay = 0f, AnimationCurve curve = null)
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }

        Vector3 current = transform.localScale;
        Vector3 target = new Vector3(targetX, targetY, targetZ);

        if (duration <= 0f)
        {
            transform.localScale = target;
            return;
        }

        scaleCoroutine = StartCoroutine(RutinaEscalar(current, target, duration, delay, curve));
    }

    /// <summary>
    /// Sobrecarga: StartScaleTo con Vector3.
    /// </summary>
    public void StartScaleTo(Vector3 target, float duration, float delay = 0f, AnimationCurve curve = null)
    {
        StartScaleTo(target.x, target.y, target.z, duration, delay, curve);
    }
}
