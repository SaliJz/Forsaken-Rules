// AcidPillarPrefab.cs (versión corregida - segura ante referencias no asignadas)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AcidPillarPrefab : MonoBehaviour
{
    [Header("Movimiento (segundos)")]
    public float riseHeight = 3f;
    public float riseTime = 0.4f;
    public float holdTime = 0.25f;
    public float fallTime = 0.4f;
    [Tooltip("Si true usa localPosition en lugar de world position (útil si es child).")]
    public bool useLocalPosition = false;

    [Header("Daño y veneno")]
    public float instantDamage = 20f;
    public float poisonDuration = 3f;
    public float poisonDPS = 2f;

    [Header("Daño continuo (opcional)")]
    public bool damageContinuous = false;
    public float continuousDPS = 5f;
    public float tickInterval = 0.5f;

    [Header("Capas / comportamiento")]
    public LayerMask damageLayers = ~0;
    public bool autoDestroyOnFinish = true;
    public float destroyDelayAfterFinish = 0.05f;

    [Header("VFX / SFX")]
    [Tooltip("Opcional: Partículas que se reproducen en el pico de la erupción.")]
    public ParticleSystem eruptParticles;
    [Tooltip("Opcional: AudioSource para reproducir SFX.")]
    public AudioSource audioSource;
    public AudioClip sfxEruptStart;
    public AudioClip sfxEruptEnd;

    // Interno
    private Vector3 initialPosition;
    private Vector3 peakPosition;
    private Coroutine motionCoroutine;
    private Collider myCollider;
    private Rigidbody myRb;

    private HashSet<GameObject> hitThisEruption = new HashSet<GameObject>();
    private Dictionary<GameObject, float> stayAccumulators = new Dictionary<GameObject, float>();

    private void Awake()
    {
        myCollider = GetComponent<Collider>();
        if (myCollider == null)
        {
            Debug.LogError("[AcidPillarPrefab] Se requiere Collider en el prefab.");
            return;
        }

        myCollider.isTrigger = true;

        myRb = GetComponent<Rigidbody>();
        if (myRb == null)
        {
            myRb = gameObject.AddComponent<Rigidbody>();
        }
        myRb.isKinematic = true;
        myRb.useGravity = false;

        if (useLocalPosition) initialPosition = transform.localPosition;
        else initialPosition = transform.position;
        peakPosition = initialPosition + Vector3.up * riseHeight;

        // Intentar auto-assign de partículas si no fueron asignadas en inspector
        if (eruptParticles == null)
        {
            eruptParticles = GetComponentInChildren<ParticleSystem>();
            if (eruptParticles == null)
            {
                Debug.LogWarning("[AcidPillarPrefab] eruptParticles no asignado y no se encontró ParticleSystem en hijos. Se omitirá VFX.");
            }
            else
            {
                Debug.Log("[AcidPillarPrefab] eruptParticles auto-asignado desde hijos.");
            }
        }

        // Intentar auto-assign de AudioSource si no fue puesto
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                // no es crítico, solo advertimos
                Debug.Log("[AcidPillarPrefab] audioSource no asignado (SFX omitidos).");
            }
        }
    }

    private void OnEnable()
    {
        hitThisEruption.Clear();
        stayAccumulators.Clear();

        if (motionCoroutine != null) StopCoroutine(motionCoroutine);
        motionCoroutine = StartCoroutine(MotionRoutine());
    }

    private void OnDisable()
    {
        if (motionCoroutine != null) StopCoroutine(motionCoroutine);
        motionCoroutine = null;
    }

    private IEnumerator MotionRoutine()
    {
        // Subida
        yield return StartCoroutine(InterpolatePosition(initialPosition, peakPosition, riseTime));

        // Reproducir VFX y SFX (solo si existen)
        if (eruptParticles != null)
        {
            eruptParticles.Play();
        }
        if (audioSource != null && sfxEruptStart != null)
        {
            audioSource.PlayOneShot(sfxEruptStart);
        }

        // Mantener arriba durante holdTime
        float tHold = 0f;
        while (tHold < holdTime)
        {
            tHold += Time.deltaTime;
            yield return null;
        }

        // Bajar
        yield return StartCoroutine(InterpolatePosition(peakPosition, initialPosition, fallTime));

        // Sonido fin y stop VFX - solo si existen
        if (audioSource != null && sfxEruptEnd != null)
        {
            audioSource.PlayOneShot(sfxEruptEnd);
        }
        if (eruptParticles != null)
        {
            eruptParticles.Stop();
        }

        // Fin de ciclo: destruir o resetear
        if (autoDestroyOnFinish)
        {
            if (destroyDelayAfterFinish > 0f) yield return new WaitForSeconds(destroyDelayAfterFinish);
            Destroy(gameObject);
        }
        else
        {
            hitThisEruption.Clear();
            stayAccumulators.Clear();
        }
    }

    private IEnumerator InterpolatePosition(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            SetPosition(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            Vector3 pos = Vector3.Lerp(from, to, t);
            SetPosition(pos);
            yield return null;
        }
        SetPosition(to);
    }

    private void SetPosition(Vector3 pos)
    {
        if (useLocalPosition) transform.localPosition = pos;
        else transform.position = pos;
    }

    private void ApplyImpactToRoot(GameObject root)
    {
        if (root == null) return;
        if (hitThisEruption.Contains(root)) return;
        hitThisEruption.Add(root);

        PlayerHealth ph = root.GetComponentInChildren<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(instantDamage);
            ph.ApplyPoison(poisonDuration, poisonDPS);
            return;
        }

        var idmg = root.GetComponentInChildren<IDamageable>();
        if (idmg != null)
        {
            try { idmg.TakeDamage(instantDamage); } catch { }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (((1 << other.gameObject.layer) & damageLayers) == 0) return;

        GameObject root = other.transform.root.gameObject;
        ApplyImpactToRoot(root);

        if (damageContinuous)
        {
            if (!stayAccumulators.ContainsKey(root)) stayAccumulators[root] = 0f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        if (((1 << other.gameObject.layer) & damageLayers) == 0) return;

        GameObject root = other.transform.root.gameObject;
        if (stayAccumulators.ContainsKey(root)) stayAccumulators.Remove(root);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!damageContinuous) return;
        if (other == null) return;
        if (((1 << other.gameObject.layer) & damageLayers) == 0) return;

        GameObject root = other.transform.root.gameObject;
        if (!stayAccumulators.ContainsKey(root)) stayAccumulators[root] = 0f;

        stayAccumulators[root] += Time.deltaTime;
        if (stayAccumulators[root] >= tickInterval)
        {
            float ticks = Mathf.Floor(stayAccumulators[root] / tickInterval);
            float damageToApply = continuousDPS * tickInterval * ticks;

            PlayerHealth ph = root.GetComponentInChildren<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(damageToApply);
            }
            else
            {
                var idmg = root.GetComponentInChildren<IDamageable>();
                if (idmg != null) { try { idmg.TakeDamage(damageToApply); } catch { } }
            }

            stayAccumulators[root] = stayAccumulators[root] - ticks * tickInterval;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 start = useLocalPosition ? transform.TransformPoint(initialPosition) : initialPosition;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(start, start + Vector3.up * riseHeight);
        Gizmos.DrawWireSphere(start, 0.1f);
    }
}
