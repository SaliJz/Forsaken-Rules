using System.Collections;
using UnityEngine;

/// <summary>
/// Utilidad centralizada para detener y destruir ParticleSystems.
/// </summary>
public static class VFXHelper
{
    private static VFXHelperRunner runner;

    private static VFXHelperRunner Runner
    {
        get
        {
            if (runner != null) return runner;

            var go = new GameObject("[VFXHelperRunner]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<VFXHelperRunner>();
            return runner;
        }
    }

    /// <summary>
    /// Reproduce un ParticleSystem de forma segura.
    /// Detiene y limpia primero para evitar que partículas de una emisión anterior
    /// queden "huérfanas" en el Job Scheduler.
    /// </summary>
    public static void SafePlay(ParticleSystem ps)
    {
        if (ps == null || !ps.gameObject.activeInHierarchy) return;

        // Detener con StopEmittingAndClear garantiza que los NativeArrays del frame
        // anterior se invalidan antes de iniciar una nueva simulación.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Clear(true);
        ps.Play();
    }

    /// <summary>
    /// Detiene un ParticleSystem de forma segura.
    /// </summary>
    /// <param name="ps">ParticleSystem objetivo.</param>
    /// <param name="clear">Si true, limpia las partículas existentes inmediatamente.</param>
    public static void SafeStop(ParticleSystem ps, bool clear = false)
    {
        if (ps == null) return;

        var stopBehavior = clear
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;

        // withChildren = true para asegurar que los sub-emitters también se detienen
        // y liberan sus NativeArrays correctamente.
        ps.Stop(true, stopBehavior);

        if (clear) ps.Clear(true);
    }

    /// <summary>
    /// Activa o desactiva la emisión de un TrailRenderer de forma segura.
    /// </summary>
    public static void SafeSetEmitting(TrailRenderer trail, bool emitting)
    {
        if (trail == null) return;
        trail.emitting = emitting;
        if (!emitting) trail.Clear();
    }

    /// <summary>
    /// Detiene el ParticleSystem y destruye su GameObject de forma segura.
    /// Espera al menos 2 frames para que el Job Scheduler libere sus NativeArrays
    /// internos antes del Destroy.
    /// La corrutina corre en el runner persistente: nunca se invalida aunque el
    /// objeto padre sea destruido antes de completarse.
    /// </summary>
    /// <param name="ps">ParticleSystem a detener y destruir.</param>
    /// <param name="extraDelay">Segundos adicionales de espera antes del Destroy.</param>
    public static void StopAndDestroy(ParticleSystem ps, float extraDelay = 0f)
    {
        if (ps == null) return;
        Runner.Run(StopAndDestroyRoutine(ps, extraDelay));
    }

    /// <summary>
    /// Desvincula el PS de su padre, lo reproduce (efecto one-shot al destruir),
    /// luego lo detiene y destruye de forma segura.
    /// Usar cuando el objeto padre se va a destruir inmediatamente después.
    /// </summary>
    /// <param name="ps">ParticleSystem a desvincular y destruir.</param>
    /// <param name="extraDelay">Segundos adicionales antes del Destroy.</param>
    public static void DetachStopAndDestroy(ParticleSystem ps, float extraDelay = 0f)
    {
        if (ps == null) return;
        ps.transform.SetParent(null);
        ps.Play();
        Runner.Run(StopAndDestroyRoutine(ps, extraDelay));
    }

    /// <summary>
    /// Secuencia segura de Stop → espera 2 frames → Destroy.
    ///
    /// Por qué 2 frames:
    ///   El sistema de partículas de Unity usa NativeArrays (Job System) que se
    ///   planifican en el frame actual y se liberan en el siguiente LateUpdate.
    ///   Si Destroy() se llama antes de que esos jobs terminen, el allocator
    ///   detecta el puntero inválido. Dos frames es el margen seguro documentado
    ///   en las release notes de Unity 2022+ y Unity 6.
    /// </summary>
    private static IEnumerator StopAndDestroyRoutine(ParticleSystem ps, float extraDelay)
    {
        if (ps == null) yield break;

        // 1. Detener con withChildren=true para incluir sub-emitters.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 2. Frame 1: el job del PS actual termina su Update.
        yield return null;

        // 3. Frame 2: el LateUpdate libera los NativeArrays asociados.
        yield return null;

        // 4. Delay extra opcional (ej: dejar que las partículas existentes
        //    completen su ciclo de vida antes de destruir el GameObject).
        if (extraDelay > 0f) yield return new WaitForSeconds(extraDelay);

        // 5. Destroy seguro – la comprobación null evita MissingReferenceException
        //    si algo externo ya destruyó el objeto.
        if (ps != null) Object.Destroy(ps.gameObject);
    }
}

/// <summary>
/// MonoBehaviour auxiliar del VFXHelper.
/// Creado y gestionado automáticamente; no añadir manualmente a la escena.
/// </summary>
internal class VFXHelperRunner : MonoBehaviour
{
    public void Run(IEnumerator routine) => StartCoroutine(routine);
}