// GeyserSpawner3D.cs
// Script para el cráter/spawner (Unity 3D).
// - Cada eruptionIntervals hace preaviso (pinta el relleno), luego instancia el prefab de columna ácida.
// - El prefab implementa OnTrigger para aplicar daño/veneno.
// - Ajusta capas, colores, SFX y prefab desde el inspector.

using System.Collections;
using UnityEngine;

public class GeyserSpawner3D : MonoBehaviour
{
    [Header("Temporización (segundos)")]
    [Tooltip("Intervalo total entre erupciones.")]
    public float eruptionInterval = 5f;
    [Tooltip("Tiempo de aviso previo en el que el cráter se pone verde antes de erupcionar.")]
    public float preEruptionTime = 1f;

    [Header("Prefab columna (must have AcidPillarPrefab)")]
    [Tooltip("Prefab de la columna ácida (debe tener AcidPillarPrefab).")]
    public GameObject acidPillarPrefab;
    [Tooltip("Escala local al instanciar el prefab.")]
    public Vector3 pillarSpawnScale = Vector3.one;
    [Tooltip("Offset vertical para instanciar el prefab (por si quieres que salga ligeramente desde abajo).")]
    public Vector3 spawnOffset = Vector3.zero;

    [Header("Visuals")]
    [Tooltip("Renderer (MeshRenderer) del relleno del cráter que se pondrá verde en el preaviso.")]
    public Renderer craterFillRenderer;
    [Tooltip("Color idle del relleno (cráter oscuro).")]
    public Color idleColor = new Color(0.05f, 0.05f, 0.05f, 1f);
    [Tooltip("Color pre-erupción (verde ácido).")]
    public Color preEruptColor = new Color(0.3f, 1f, 0.2f, 1f);
    [Tooltip("Tiempo de interpolación del color al volver a idle.")]
    public float colorResetTime = 0.4f;

    [Header("Sonido y animador (opcional)")]
    public AudioSource audioSource;
    public AudioClip preEruptSfx;
    public AudioClip eruptSfx;
    public Animator animator; // Triggers: "PreErupt", "Erupt"

    [Header("Opciones")]
    [Tooltip("Si true, instancia un prefab incluso si acidPillarPrefab es null (no aplicará daño).")]
    public bool allowNullPrefab = false;

    // Interno
    private Coroutine loopCoroutine;
    private Material instancedMaterial;
    private Color originalColor;
    private Coroutine colorCoroutine;

    private void Awake()
    {
        if (craterFillRenderer == null)
        {
            craterFillRenderer = GetComponentInChildren<Renderer>();
        }

        if (craterFillRenderer != null)
        {
            // Creamos instancia de material para no modificar shared material
            instancedMaterial = craterFillRenderer.material;
            if (instancedMaterial.HasProperty("_Color"))
            {
                originalColor = instancedMaterial.color;
            }
            else originalColor = idleColor;

            instancedMaterial.color = idleColor;
        }
        else
        {
            originalColor = idleColor;
        }
    }

    private void OnEnable()
    {
        StartLoop();
    }

    private void OnDisable()
    {
        StopLoop();
    }

    private void StartLoop()
    {
        if (loopCoroutine != null) StopCoroutine(loopCoroutine);
        loopCoroutine = StartCoroutine(EruptionLoop());
    }

    private void StopLoop()
    {
        if (loopCoroutine != null) StopCoroutine(loopCoroutine);
        loopCoroutine = null;

        if (colorCoroutine != null) StopCoroutine(colorCoroutine);
        colorCoroutine = null;

        if (instancedMaterial != null) instancedMaterial.color = originalColor;
    }

    private IEnumerator EruptionLoop()
    {
        while (true)
        {
            float waitBefore = Mathf.Max(0f, eruptionInterval - preEruptionTime);
            yield return new WaitForSeconds(waitBefore);

            // Preaviso visual + animador + sfx
            PreEruption();

            yield return new WaitForSeconds(preEruptionTime);

            // Ejecutar erupción: instancia prefab y sfx/anim
            DoEruption();

            yield return null;
        }
    }

    private void PreEruption()
    {
        if (instancedMaterial != null)
        {
            if (colorCoroutine != null) StopCoroutine(colorCoroutine);
            colorCoroutine = StartCoroutine(InterpMaterialColor(instancedMaterial, instancedMaterial.color, preEruptColor, Mathf.Max(0.05f, preEruptionTime * 0.9f)));
        }

        if (audioSource != null && preEruptSfx != null) audioSource.PlayOneShot(preEruptSfx);
        if (animator != null) animator.SetTrigger("PreErupt");
    }

    private void DoEruption()
    {
        if (audioSource != null && eruptSfx != null) audioSource.PlayOneShot(eruptSfx);
        if (animator != null) animator.SetTrigger("Erupt");

        // Instanciar prefab
        if (acidPillarPrefab != null)
        {
            Vector3 spawnPos = transform.position + spawnOffset;
            GameObject inst = Instantiate(acidPillarPrefab, spawnPos, Quaternion.identity);
            inst.transform.localScale = pillarSpawnScale;
        }
        else
        {
            if (!allowNullPrefab)
            {
                Debug.LogWarning("[GeyserSpawner3D] acidPillarPrefab no asignado. Asigna el prefab para que la columna funcione.");
            }
        }

        // Restaurar color al idle (suave)
        if (instancedMaterial != null)
        {
            if (colorCoroutine != null) StopCoroutine(colorCoroutine);
            colorCoroutine = StartCoroutine(InterpMaterialColor(instancedMaterial, instancedMaterial.color, originalColor, colorResetTime));
        }
    }

    private IEnumerator InterpMaterialColor(Material mat, Color from, Color to, float duration)
    {
        if (mat == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
            if (mat.HasProperty("_Color")) mat.color = Color.Lerp(from, to, t);
            yield return null;
        }
        if (mat.HasProperty("_Color")) mat.color = to;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}
