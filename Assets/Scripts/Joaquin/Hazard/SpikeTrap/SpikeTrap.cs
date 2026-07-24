using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary> 
/// Trampa de pinchos que avisa, ataca y entra en enfriamiento antes de rearmarse.
/// </summary>
public class SpikeTrap : MonoBehaviour
{
    private enum TrapState { Armed, Priming, Active, Cooldown }
    [SerializeField, Tooltip("Estado actual de la trampa (solo lectura en inspector).")]
    private TrapState currentState = TrapState.Armed;

    [Header("Configuración de Detección")]
    [SerializeField, Tooltip("Define qué capas activan la trampa (Ej: Player, Enemy).")]
    private LayerMask targetLayers;

    [Header("Parámetros")]
    [SerializeField] private float damage = 20f;
    [SerializeField, Tooltip("Tiempo entre el aviso y el ataque.")]
    private float primingDuration = 0.7f;
    [SerializeField, Tooltip("Tiempo que los pinchos permanecen arriba.")]
    private float activeDuration = 1.5f;
    [SerializeField, Tooltip("Tiempo de recarga de la trampa.")]
    private float cooldownDuration = 3f;

    [Header("Placa de presión")]
    [SerializeField, Tooltip("Transform de la base/plataforma que baja hacia el jugador.")]
    private Transform plateTransform;
    [SerializeField, Tooltip("posición local Y cuando la placa está levantada.")]
    private float plateRaisedLocalY = 1f;
    [SerializeField, Tooltip("posición local Y cuando la placa queda presionada.")]
    private float platePressedLocalY = 0.25f;
    [SerializeField, Tooltip("duración del movimiento de la placa al bajar o subir.")]
    private float plateMoveDuration = 0.25f;

    [Header("VFX")]
    [SerializeField, Tooltip("particle system que se reproduce durante el priming. Debe ser hijo de la placa.")]
    private ParticleSystem primingDustVFX;
    [SerializeField, Tooltip("particle system tipo burst que se reproduce en la activación.")]
    private ParticleSystem activateDustVFX;

    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip triggerSound;   
    [SerializeField] private AudioClip activateSound;
    [SerializeField] private AudioClip desactivateSound;
    [SerializeField] private AudioClip armedSound;

    [Header("Debug HUD")]
    [SerializeField] private bool canDebug = false;

    private readonly List<GameObject> targetsInRange = new List<GameObject>();
    private Dictionary<int, float> originalOffsets = new Dictionary<int, float>();

    private Coroutine trapRoutine;
    private Coroutine plateMoveRoutine;

    private void OnDisable()
    {
        StopPlateRoutineIfAny();
        StopPrimingVFX();
        StopActivateVFX();
        RestoreAllAgentsOffsets();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsInLayerMask(other.gameObject, targetLayers))
        {
            if (!targetsInRange.Contains(other.gameObject))
            {
                targetsInRange.Add(other.gameObject);
                NavMeshAgent agent = other.GetComponent<NavMeshAgent>();
                if (agent != null && !originalOffsets.ContainsKey(other.gameObject.GetInstanceID()))
                {
                    originalOffsets.Add(other.gameObject.GetInstanceID(), agent.baseOffset);
                }
            }

            if (currentState == TrapState.Armed)
            {
                if (trapRoutine == null) trapRoutine = StartCoroutine(ActivateTrapSequence());
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsInLayerMask(other.gameObject, targetLayers))
        {
            if (targetsInRange.Contains(other.gameObject))
            {
                RestoreAgentOffset(other.gameObject);
                targetsInRange.Remove(other.gameObject);
            }
        }
    }

    private bool IsInLayerMask(GameObject obj, LayerMask mask)
    {
        return (mask.value & (1 << obj.layer)) != 0;
    }

    private IEnumerator ActivateTrapSequence()
    {
        //1. Aviso (Priming)
        SetState(TrapState.Priming);
        PlaySound(triggerSound);

        // iniciar movimiento de placa y VFX de priming
        StartPlateLower();
        StartPrimingVFX();

        yield return new WaitForSecondsRealtime(primingDuration);

        // 2. Ataque (Active)
        StopPrimingVFX();
        SetState(TrapState.Active);
        TriggerAnimation("Activate");
        PlaySound(activateSound);
        PlayActivateVFX();

        for (int i = targetsInRange.Count - 1; i >= 0; i--)
        {
            GameObject target = targetsInRange[i];

            if (target != null && target.activeInHierarchy)
            {
                ApplyDamage(target);
            }
            else
            {
                targetsInRange.RemoveAt(i);
            }
        }

        yield return new WaitForSecondsRealtime(activeDuration);

        // 3. Enfriamiento (Cooldown)
        SetState(TrapState.Cooldown);
        TriggerAnimation("Deactivate");
        PlaySound(desactivateSound);

        yield return new WaitForSecondsRealtime(cooldownDuration);

        // 4. Rearmado (Armed)
        SetState(TrapState.Armed);
        PlaySound(armedSound);
        trapRoutine = null;
        StartPlateRaise();
        StopPrimingVFX();
        StopActivateVFX();

        targetsInRange.RemoveAll(x => x == null || !x.activeInHierarchy);

        if (targetsInRange.Count > 0)
        {
            trapRoutine = StartCoroutine(ActivateTrapSequence());
        }
    }

    private void ApplyDamage(GameObject target)
    {
        PlayerHealth pHealth = target.GetComponent<PlayerHealth>();
        if (pHealth != null)
        {
            pHealth.TakeDamage(damage);
            return;
        }

        EnemyHealth eHealth = target.GetComponent<EnemyHealth>();
        if (eHealth != null)
        {
            eHealth.TakeDamage(damage, false, AttackDamageType.Nothing);
            return;
        }

        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage, false, AttackDamageType.Nothing);
        }
    }

    private void SetState(TrapState newState)
    {
        currentState = newState;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void TriggerAnimation(string trigger)
    {
        if (animator != null)
        {
            animator.SetTrigger(trigger);
        }
    }

    private void StartPlateLower()
    {
        StopPlateRoutineIfAny();
        if (plateTransform != null)
        {
            plateMoveRoutine = StartCoroutine(MovePlateLocalY(plateRaisedLocalY, platePressedLocalY, plateMoveDuration));
        }
    }

    private void StartPlateRaise()
    {
        StopPlateRoutineIfAny();
        if (plateTransform != null)
        {
            float raiseDuration = Mathf.Max(0.08f, plateMoveDuration * 0.45f);
            plateMoveRoutine = StartCoroutine(MovePlateLocalY(platePressedLocalY, plateRaisedLocalY, raiseDuration));
        }
    }

    private void StopPlateRoutineIfAny()
    {
        if (plateMoveRoutine != null)
        {
            StopCoroutine(plateMoveRoutine);
            plateMoveRoutine = null;
        }
    }

    private IEnumerator MovePlateLocalY(float fromY, float toY, float duration)
    {
        Vector3 startPos = plateTransform.localPosition;
        float startTime = Time.realtimeSinceStartup;

        float totalDropDistance = plateRaisedLocalY - platePressedLocalY;

        while (Time.realtimeSinceStartup < startTime + duration)
        {
            float elapsed = Time.realtimeSinceStartup - startTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            float currentY = Mathf.Lerp(fromY, toY, eased);
            plateTransform.localPosition = new Vector3(startPos.x, currentY, startPos.z);

            UpdateAgentsVerticalOffset(currentY);

            yield return null;
        }

        plateTransform.localPosition = new Vector3(startPos.x, toY, startPos.z);
        UpdateAgentsVerticalOffset(toY);

        plateMoveRoutine = null;
    }

    private void UpdateAgentsVerticalOffset(float currentPlateY)
    {
        float offsetModifier = currentPlateY - plateRaisedLocalY;

        for (int i = 0; i < targetsInRange.Count; i++)
        {
            GameObject target = targetsInRange[i];
            if (target == null) continue;

            NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                int id = target.GetInstanceID();
                float baseOffset = originalOffsets.ContainsKey(id) ? originalOffsets[id] : 0f;

                agent.baseOffset = baseOffset + offsetModifier;
            }
        }
    }

    private void RestoreAgentOffset(GameObject target)
    {
        if (target == null) return;
        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            int id = target.GetInstanceID();
            if (originalOffsets.ContainsKey(id))
            {
                agent.baseOffset = originalOffsets[id];
            }
        }
    }

    private void RestoreAllAgentsOffsets()
    {
        foreach (var target in targetsInRange)
        {
            RestoreAgentOffset(target);
        }
        originalOffsets.Clear();
    }

    private void StartPrimingVFX()
    {
        if (primingDustVFX != null)
        {
            primingDustVFX.transform.position = plateTransform != null ? plateTransform.position : transform.position;
            if (!primingDustVFX.isPlaying) primingDustVFX.Play();
        }
    }

    private void StopPrimingVFX()
    {
        if (primingDustVFX != null && primingDustVFX.isPlaying)
        {
            primingDustVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void PlayActivateVFX()
    {
        if (activateDustVFX != null)
        {
            activateDustVFX.transform.position = plateTransform != null ? plateTransform.position : transform.position;
            activateDustVFX.Play();
        }
    }

    private void StopActivateVFX()
    {
        if (activateDustVFX != null && activateDustVFX.isPlaying)
        {
            activateDustVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void OnDrawGizmos()
    {
        Color color = Color.gray;
        switch (currentState)
        {
            case TrapState.Armed: color = Color.green; break;
            case TrapState.Priming: color = Color.yellow; break;
            case TrapState.Active: color = Color.red; break;
            case TrapState.Cooldown: color = Color.cyan; break;
        }
        Gizmos.color = color;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(1, 0.5f, 1));
    }

    private void OnGUI()
    {
        if (!canDebug) return;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(10, 10, 10, 10)
        };

        Rect rect = new Rect(10, 10, 250, 150);
        GUILayout.BeginArea(rect, boxStyle);
        GUILayout.Label("<b>SpikeTrap Debug</b>", new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        });
        GUILayout.Space(5);
        GUILayout.Label($"Estado: {currentState}");
        GUILayout.Label($"Jugador en rango: {targetsInRange.Count}");
        GUILayout.Label($"Rutina activa: {(trapRoutine != null ? "Sí" : "No")}");
        GUILayout.EndArea();
    }
}