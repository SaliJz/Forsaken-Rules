
// SueloFragil.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SueloFragil (3D): colocado sobre la grieta (Collider con IsTrigger).
/// - Al entrar un Player inicia rutina para romper tras 'delayParaRomper'.
/// - Si 'romperAlSalir' == false y el jugador sale antes, se cancela.
/// - Al romper: EN LUGAR DE INSTANTIAR, BUSCA un hijo con AreaAcido y lo ACTIVA,
///   aplica escala X,Y preservando Z del child, alinea posición/rotación y reproduce efectos.
/// - Si no existe child con AreaAcido, hace fallback a instanciar 'prefabHueco' (comportamiento anterior).
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class SueloFragil : MonoBehaviour
{
    [Header("Referencias visuales")]
    [Tooltip("MeshRenderer que representa la grieta visible antes de romper.")]
    public MeshRenderer meshGrieta; // visual inicial
    [Tooltip("Prefab 3D del hueco que se instanciará al romper si no hay child AreaAcido (fallback).")]
    public GameObject prefabHueco;  // prefab 3D del hueco (contiene AreaAcido)

    [Header("Escalas (Vector2: x -> X, y -> Y)")]
    [Tooltip("Escala que se aplicará al prefab/child del hueco (X,Y). Z del hueco se preserva.")]
    public Vector2 escalaHueco = Vector2.one;
    [Tooltip("Escala que se aplicará a la malla de la grieta (X,Y). Z de la malla se preserva.")]
    public Vector2 escalaGrieta = Vector2.one;

    [Header("Parametros")]
    [Tooltip("Tiempo en segundos desde que el jugador entra hasta que la losa se rompe.")]
    public float delayParaRomper = 0.4f;
    [Tooltip("Si true, la losa se romperá aunque el jugador salga antes del delay.")]
    public bool romperAlSalir = false;

    [Header("Efectos")]
    [Tooltip("SFX que suena al activarse la grieta (entrada) y también al romper.")]
    public AudioClip sfxActivar;
    [Tooltip("ParticleSystem que se reproduce al activarse/romper.")]
    public ParticleSystem efectoActivacion;

    // Estado interno
    private bool yaRoto = false;
    private AudioSource audioSource;
    private HashSet<GameObject> objetosDentro = new HashSet<GameObject>();

    // guardamos Z original del mesh para preservarlo al aplicar X,Y desde Vector2
    private float meshGrietaOriginalZ = 1f;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (meshGrieta != null)
        {
            // conservar Z actual y aplicar X,Y desde Vector2
            meshGrietaOriginalZ = meshGrieta.transform.localScale.z;
            meshGrieta.transform.localScale = new Vector3(escalaGrieta.x, escalaGrieta.y, meshGrietaOriginalZ);
        }

        // Asegurarse de que el collider esté en modo trigger (se espera así por diseño).
        Collider c = GetComponent<Collider>();
        if (c != null && !c.isTrigger)
        {
            Debug.LogWarning($"[SueloFragil] Collider en '{gameObject.name}' no está como IsTrigger. Se recomienda marcarlo como trigger.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (yaRoto) return;

        if (other.gameObject.CompareTag("Player"))
        {
            // Registrar y arrancar rutina solo si es la primera vez que entra este objeto
            if (!objetosDentro.Contains(other.gameObject))
            {
                objetosDentro.Add(other.gameObject);
                StartCoroutine(RutinaRomper(other.gameObject));
            }

            // Feedback de activación (sonido/partículas)
            if (efectoActivacion != null)
            {
                efectoActivacion.Play();
            }
            if (audioSource != null && sfxActivar != null)
            {
                audioSource.PlayOneShot(sfxActivar);
            }
            else if (audioSource == null && sfxActivar != null)
            {
                AudioSource.PlayClipAtPoint(sfxActivar, transform.position);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            objetosDentro.Remove(other.gameObject);
        }
    }

    private IEnumerator RutinaRomper(GameObject jugador)
    {
        float t = 0f;
        while (t < delayParaRomper)
        {
            t += Time.deltaTime;

            // si el jugador salió antes y no queremos romper, cancelamos
            if (!romperAlSalir && !objetosDentro.Contains(jugador))
            {
                yield break;
            }

            // Si ya se rompió por otro motivo, cancelar
            if (yaRoto) yield break;

            yield return null;
        }

        RomperAhora();
    }

    private void RomperAhora()
    {
        if (yaRoto) return;
        yaRoto = true;

        // PRIMERO: intentar buscar un hijo (incluso inactivo) que tenga AreaAcido
        AreaAcido areaComp = GetComponentInChildren<AreaAcido>(true); // true = incluir inactivos
        if (areaComp != null)
        {
            GameObject huecoGO = areaComp.gameObject;

            // Activar el child que contiene AreaAcido
            huecoGO.SetActive(true);

            // Alineado: si el AreaAcido implementa ForceAlignNow(), úsalo para respetar offsets definidos allí
            try
            {
                areaComp.ForceAlignNow();
            }
            catch
            {
                // Si por alguna razón no existe o falla, ignoramos (para compatibilidad).
            }

            // Aplicar escala X,Y preservando Z original del child (comportamiento antiguo)
            Vector3 childLocalScale = huecoGO.transform.localScale;
            float childZ = childLocalScale.z;
            huecoGO.transform.localScale = new Vector3(escalaHueco.x, escalaHueco.y, childZ);

            // Opcional: si quieres forzar un escalado desde el propio AreaAcido (si implementa StartScaleTo),
            // podríamos llamarlo aquí; dejo comentado por ahora para evitar interferir con su Start().
            // areaComp.StartScaleTo(new Vector3(escalaHueco.x, escalaHueco.y, childZ), areaComp.escalaDuration, 0f);

        }
        else if (prefabHueco != null)
        {
            // FALLBACK: si no hay child con AreaAcido, instanciamos el prefab como antes.
            GameObject hueco = Instantiate(prefabHueco, transform.position, transform.rotation, transform.parent);

            // Si el prefab tiene escala distinta, preservamos su Z y aplicamos X,Y desde escalaHueco
            float huecoZ = hueco.transform.localScale.z;
            hueco.transform.localScale = new Vector3(escalaHueco.x, escalaHueco.y, huecoZ);
        }
        else
        {
            // No hay child ni prefab asignado: avisar en consola para facilitar debug
            Debug.LogWarning($"[SueloFragil] RomperAhora() no encontró child AreaAcido ni prefabHueco en '{gameObject.name}'.", this);
        }

        // Desactivar la malla de la grieta para ocultarla
        if (meshGrieta != null)
        {
            meshGrieta.enabled = false;
        }

        // Desactivar el collider para que no vuelva a detectar entradas
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Reproducir efectos finales (mismo sfx que al activar)
        if (efectoActivacion != null)
        {
            efectoActivacion.Play();
        }
        if (audioSource != null && sfxActivar != null)
        {
            audioSource.PlayOneShot(sfxActivar);
        }
        else if (sfxActivar != null)
        {
            AudioSource.PlayClipAtPoint(sfxActivar, transform.position);
        }
    }

    // Método público para forzar la ruptura desde otros scripts (útil en tests o eventos externos)
    public void ForceBreakNow()
    {
        if (!yaRoto) RomperAhora();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Mantener valores no nulos para escalas por conveniencia de editor
        if (escalaHueco.x <= 0f) escalaHueco.x = 0.01f;
        if (escalaHueco.y <= 0f) escalaHueco.y = 0.01f;
        if (escalaGrieta.x <= 0f) escalaGrieta.x = 0.01f;
        if (escalaGrieta.y <= 0f) escalaGrieta.y = 0.01f;
    }
#endif
}
