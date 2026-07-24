using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Clase que maneja los efectos visuales del enemigo
/// </summary>
public class EnemyVisualEffects : MonoBehaviour
{
    #region Inspector - References

    [Header("Renderers Configuration")]
    [Tooltip("Si se deja vacio, buscara Renderers en los hijos.")]
    [SerializeField] private Renderer[] renderers;

    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Transform damageNumberParent;

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitStunSFX;
    [SerializeField] private AudioClip toughnessBlockSFX;
    [SerializeField] private AudioClip normalHitSFX;
    [SerializeField] private AudioClip criticalHitSFX;

    #endregion

    #region Inspector - Flash And Blink Settings

    [Header("Flash And Blink Amount Override")]
    [Tooltip("Si es true, se anima el valor '_Amount' en los renderers referenciados.")]
    [SerializeField] private bool useAmountFlash = false;
    [Tooltip("Nombre de la propiedad float del shader que se animara.")]
    [SerializeField] private string amountFlashProperty = "_Amount";
    [Tooltip("Valor del _Amount durante el pico del flash.")]
    [SerializeField] private float amountFlashPeakValue = 1f;
    [Tooltip("Valor del _Amount en reposo (antes y despues del flash).")]
    [SerializeField] private float amountFlashRestValue = 0f;
    [SerializeField] private float hitFlashDuration = 0.1f;

    #endregion

    #region Inspector - Effect Settings

    [Header("Stun Effect")]
    [SerializeField] private GameObject stunVFXPrefab;
    [SerializeField] private Transform stunVFXSpawnPoint;
    [SerializeField] private float stunVFXHeightFallback = 2f;

    [Header("Armor Glow Effect")]
    [SerializeField] private Material glowMaterial;
    [SerializeField] private float glowIntensity = 2f;

    [Header("Toughness Block Effect")]
    [SerializeField] private Color toughnessColor = Color.cyan;

    [Header("Anticipation Telegraph Blink")]
    //[SerializeField] private float anticipationBlinkInterval = 0.04f;
    [SerializeField] private Color anticipationBlinkColor = Color.red;

    [Header("Damage Visual Settings")]
    [SerializeField] private Color normalColor = Color.red;
    [SerializeField] private Color criticalColor = new Color(0.5f, 0f, 0f);

    #endregion

    #region Internal State

    private bool isStunned = false;

    private Coroutine stunEffectCoroutine = null;
    private GameObject activeStunVFX;

    private Coroutine hitFlashCoroutine = null;
    private Coroutine glowCoroutine = null;
    private Coroutine anticipationBlinkCoroutine = null;

    private Dictionary<Renderer, Material> originalMeshMats = new Dictionary<Renderer, Material>();
    private Dictionary<Renderer, Material> amountFlashMatInstances = new Dictionary<Renderer, Material>();
    private Dictionary<Renderer, Color> originalFlashColors = new Dictionary<Renderer, Color>();
    private List<GameObject> activePersistentEffects = new List<GameObject>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ValidateRenderers();
        CacheOriginalMaterials();
        CacheAmountFlashMaterials();
    }

    private void OnEnable()
    {
        ResetVisualState();
    }

    private void OnDisable()
    {
        CleanupAllEffects(true);
    }

    private void OnDestroy()
    {
        CleanupAllEffects(true);

        foreach (var mat in amountFlashMatInstances.Values)
        {
            if (mat != null) Destroy(mat);
        }
        amountFlashMatInstances.Clear();
    }

    #endregion

    #region Initialization And Data Sync

    private void ValidateRenderers()
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>();
        }

        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"[EnemyVisualEffects] No se encontraron Renderers en {gameObject.name}.");
        }
    }

    private void CacheOriginalMaterials()
    {
        originalMeshMats.Clear();

        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r != null) originalMeshMats[r] = r.sharedMaterial;
            }
        }
    }

    private void CacheAmountFlashMaterials()
    {
        amountFlashMatInstances.Clear();
        originalFlashColors.Clear();

        if (!useAmountFlash || renderers == null || renderers.Length == 0) return;

        foreach (var r in renderers)
        {
            if (r != null && r.sharedMaterial != null)
            {
                Material matInstance = new Material(r.sharedMaterial);
                amountFlashMatInstances[r] = matInstance;
                r.material = matInstance;

                if (matInstance.HasProperty(amountFlashProperty))
                {
                    matInstance.SetFloat(amountFlashProperty, amountFlashRestValue);
                }

                if (matInstance.HasProperty("_Color"))
                {
                    originalFlashColors[r] = matInstance.GetColor("_Color");
                }
            }
        }
    }

    #endregion

    #region Core Effect Management

    private void CleanupAllEffects(bool forceImmediate = false)
    {
        StopAllCoroutines();

        stunEffectCoroutine = null;
        hitFlashCoroutine = null;
        glowCoroutine = null;
        anticipationBlinkCoroutine = null;
        ResetAnticipationBlink();
        ResetAmountFlashValue();

        ParticleSystem psAS = activeStunVFX != null ? activeStunVFX.GetComponent<ParticleSystem>() : null;

        if (activeStunVFX != null)
        {
            if (forceImmediate) VFXHelper.StopAndDestroy(psAS);
            else DetachAndStopStunVFX(activeStunVFX);

            activeStunVFX = null;
        }

        for (int i = activePersistentEffects.Count - 1; i >= 0; i--)
        {
            GameObject fx = activePersistentEffects[i];
            if (fx == null) continue;

            ParticleSystem psFX = fx.GetComponent<ParticleSystem>();
            if (psFX != null) VFXHelper.StopAndDestroy(psFX);
            else Destroy(fx);
        }
        activePersistentEffects.Clear();

        ResetVisualState();
    }

    private void ResetVisualState()
    {
        RestoreAllOriginalMaterials();
        SetRenderersActive(true);
    }

    private void RestoreAllOriginalMaterials()
    {
        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;

                if (useAmountFlash && amountFlashMatInstances.ContainsKey(r))
                {
                    r.material = amountFlashMatInstances[r];
                }
                else if (originalMeshMats.ContainsKey(r))
                {
                    r.material = originalMeshMats[r];
                }
            }
        }

        if (useAmountFlash)
        {
            ResetAmountFlashValue();
        }
    }

    #endregion

    #region Damage And Hit Feedback

    public void PlayToughnessHitFeedback(Vector3 position, float damageAmount = 0f)
    {
        ShowDamageNumber(position, damageAmount, isCritical: false, isToughness: true);
        PlayToughnessBlockSound();
    }

    public void PlayHealthHitFeedback(Vector3 damagePosition, float damage, bool isCritical)
    {
        ShowDamageNumber(damagePosition, damage, isCritical);
        PlayHealthHitSound(isCritical);

        if (useAmountFlash && amountFlashMatInstances.Count > 0)
        {
            ReapplyAmountFlashMaterials();

            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                SetAmountFlashValue(amountFlashRestValue);
            }
            hitFlashCoroutine = StartCoroutine(HitFlashCoroutine(hitFlashDuration));
        }

        if (isStunned) return;

        if (anticipationBlinkCoroutine != null)
        {
            StopCoroutine(anticipationBlinkCoroutine);
            anticipationBlinkCoroutine = null;
        }
    }

    private void PlayHealthHitSound(bool isCritical)
    {
        if (audioSource == null) return;
        AudioClip clip = isCritical ? criticalHitSFX : normalHitSFX;
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    private void PlayToughnessBlockSound()
    {
        if (audioSource == null || toughnessBlockSFX == null) return;
        audioSource.PlayOneShot(toughnessBlockSFX);
    }

    public void ShowDamageNumber(Vector3 position, float damage, bool isCritical = false, bool isToughness = false)
    {
        if (damageNumberPrefab == null) return;

        GameObject damageNumber = Instantiate(damageNumberPrefab, position, Quaternion.identity, damageNumberParent);

        DamageNumber dnScript = damageNumber.GetComponent<DamageNumber>();
        if (dnScript != null)
        {
            dnScript.SetHealthColor(normalColor, criticalColor);
            dnScript.SetToughnessColor(toughnessColor);
            dnScript.Initialize(damage, isCritical, isToughness);
        }
    }

    #endregion

    #region Hit Blink System

    private IEnumerator HitFlashCoroutine(float duration)
    {
        if (!useAmountFlash || amountFlashMatInstances.Count == 0)
        {
            hitFlashCoroutine = null;
            yield break;
        }

        ReapplyAmountFlashMaterials();
        SetAmountFlashValue(amountFlashPeakValue);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float value = Mathf.Lerp(amountFlashPeakValue, amountFlashRestValue, t);
            SetAmountFlashValue(value);
            yield return null;
        }

        ForceRestoreAmountFlashVisualState();
        hitFlashCoroutine = null;
    }

    private void SetAmountFlashValue(float value)
    {
        if (!useAmountFlash) return;
        foreach (var mat in amountFlashMatInstances.Values)
        {
            if (mat != null && mat.HasProperty(amountFlashProperty))
            {
                mat.SetFloat(amountFlashProperty, value);
            }
        }
    }

    private void ResetAmountFlashValue()
    {
        SetAmountFlashValue(amountFlashRestValue);
    }

    #endregion

    #region Anticipation Telegraph Blink

    public void PlayAnticipationBlink(float duration)
    {
        if (anticipationBlinkCoroutine != null)
        {
            StopCoroutine(anticipationBlinkCoroutine);
            anticipationBlinkCoroutine = null;
        }
        anticipationBlinkCoroutine = StartCoroutine(AnticipationBlinkCoroutine(duration));
    }

    public void CancelAnticipationBlink()
    {
        if (anticipationBlinkCoroutine != null)
        {
            StopCoroutine(anticipationBlinkCoroutine);
            anticipationBlinkCoroutine = null;
        }
        ResetAnticipationBlink();
    }

    private IEnumerator AnticipationBlinkCoroutine(float duration)
    {
        if (!useAmountFlash || amountFlashMatInstances.Count == 0)
        {
            anticipationBlinkCoroutine = null;
            yield break;
        }

        ReapplyAmountFlashMaterials();

        foreach (var mat in amountFlashMatInstances.Values)
        {
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", anticipationBlinkColor);
            }
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float value = Mathf.Lerp(amountFlashRestValue, amountFlashPeakValue, t);
            SetAmountFlashValue(value);
            yield return null;
        }

        ResetAnticipationBlink();
        anticipationBlinkCoroutine = null;
    }

    private void ResetAnticipationBlink()
    {
        ForceRestoreAmountFlashVisualState();
    }

    #endregion

    #region Stun And Armor Effects

    public void StartStunEffect(float duration)
    {
        ResetVisualState();

        if (stunEffectCoroutine != null)
        {
            StopCoroutine(stunEffectCoroutine);
        }

        stunEffectCoroutine = StartCoroutine(StunEffectCoroutine(duration));
    }

    public void StopStunEffect()
    {
        if (stunEffectCoroutine != null)
        {
            StopCoroutine(stunEffectCoroutine);
            stunEffectCoroutine = null;
        }
        CleanupStunEffect();
    }

    private IEnumerator StunEffectCoroutine(float duration)
    {
        isStunned = true;

        if (audioSource != null && hitStunSFX != null) audioSource.PlayOneShot(hitStunSFX);

        if (stunVFXPrefab != null && activeStunVFX == null)
        {
            Vector3 stunSpawnPos = stunVFXSpawnPoint != null
                ? stunVFXSpawnPoint.position
                : transform.position + Vector3.up * stunVFXHeightFallback;

            activeStunVFX = Instantiate(stunVFXPrefab, stunSpawnPos, Quaternion.identity, transform);
        }

        yield return new WaitForSeconds(duration);

        CleanupStunEffect();
    }

    private void CleanupStunEffect()
    {
        isStunned = false;
        SetRenderersActive(true);

        if (activeStunVFX != null)
        {
            DetachAndStopStunVFX(activeStunVFX);
            activeStunVFX = null;
        }
    }

    private void DetachAndStopStunVFX(GameObject vfxToStop)
    {
        if (vfxToStop == null) return;

        if (vfxToStop.transform.parent == transform)
        {
            vfxToStop.transform.SetParent(null);
        }

        ParticleSystem vfxPS = vfxToStop.GetComponent<ParticleSystem>();
        VFXHelper.StopAndDestroy(vfxPS);
    }

    public void StartArmorGlow()
    {
        if (glowMaterial == null) return;
        if (glowCoroutine != null) StopCoroutine(glowCoroutine);

        ApplyMaterialToAll(glowMaterial);

        glowCoroutine = StartCoroutine(AnimateGlowCoroutine());
    }

    public void StopArmorGlow()
    {
        if (glowCoroutine != null)
        {
            StopCoroutine(glowCoroutine);
            glowCoroutine = null;
        }
        RestoreAllOriginalMaterials();
    }

    private IEnumerator AnimateGlowCoroutine()
    {
        float baseIntensity = 1f;
        while (true)
        {
            float intensity = baseIntensity + Mathf.Sin(Time.time * 3f) * glowIntensity;
            UpdateGlowIntensity(intensity);
            yield return null;
        }
    }

    #endregion

    #region External Material Control

    public void ReapplyAmountFlashMaterials()
    {
        if (!useAmountFlash) return;

        foreach (var kvp in amountFlashMatInstances)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                kvp.Key.material = kvp.Value;
            }
        }
    }

    public void UpdateBaseMaterial(Renderer targetRenderer, Material newMaterial)
    {
        if (targetRenderer == null || newMaterial == null) return;

        if (originalMeshMats.ContainsKey(targetRenderer))
        {
            originalMeshMats[targetRenderer] = newMaterial;
        }
        else
        {
            originalMeshMats.Add(targetRenderer, newMaterial);
        }

        if (glowCoroutine == null)
        {
            targetRenderer.material = newMaterial;
        }
    }

    #endregion

    #region Helper Methods

    private void ApplyMaterialToAll(Material mat)
    {
        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r != null) r.material = mat;
            }
        }
    }

    private void UpdateGlowIntensity(float intensity)
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r != null && r.material.HasProperty("_EmissionIntensity"))
            {
                r.material.SetFloat("_EmissionIntensity", intensity);
            }
        }
    }

    private void SetRenderersActive(bool active)
    {
        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = active;
            }
        }
    }

    private void ForceRestoreAmountFlashVisualState()
    {
        if (!useAmountFlash) return;

        ReapplyAmountFlashMaterials();
        SetAmountFlashValue(amountFlashRestValue);

        foreach (var kvp in amountFlashMatInstances)
        {
            Renderer r = kvp.Key;
            Material mat = kvp.Value;

            if (r != null && mat != null && mat.HasProperty("_Color") && originalFlashColors.ContainsKey(r))
            {
                mat.SetColor("_Color", originalFlashColors[r]);
            }
        }
    }

    #endregion

    #region Debugging

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3.up * stunVFXHeightFallback));
    }

    #endregion
}