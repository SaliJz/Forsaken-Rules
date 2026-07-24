using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class MorlockProjectileWordTrail : MonoBehaviour
{
    [Header("Letter Visuals")]
    [SerializeField] private GameObject letterPrefab;

    [Tooltip("Scale multiplier applied to each spawned letter prefab.")]
    [SerializeField, Min(0.1f)] private float letterScaleMultiplier = 0.7f;

    [Tooltip("Time between one visible letter and the next.")]
    [FormerlySerializedAs("revealDelay")]
    [SerializeField, Min(0f)] private float letterRevealDelay = 0.03f;

    [Tooltip("Initial offset of the first letter relative to the projectile.")]
    [FormerlySerializedAs("firstLetterLocalOffset")]
    [SerializeField] private Vector3 firstLetterOffset = new Vector3(0f, 0.12f, 0.04f);

    [Tooltip("Minimum spacing kept between letters, even for narrow glyphs.")]
    [SerializeField, Min(0f)] private float baseLetterSpacing = 0.05f;

    [Tooltip("How much of each letter's real width is used to separate the trail.")]
    [SerializeField, Min(0f)] private float widthSpacingMultiplier = 1.0f;

    [Tooltip("Minimum local width used when calculating dynamic spacing.")]
    [SerializeField, Min(0f)] private float minimumLetterWidth = 20f;

    [Tooltip("Vertical falloff per letter (keep small, e.g. -0.01).")]
    [SerializeField] private float verticalFalloffPerLetter = -0.01f;

    [Tooltip("Depth lag per letter (keep small, e.g. 0.04).")]
    [SerializeField] private float depthLagPerLetter = 0.04f;

    [Header("Billboard")]
    [SerializeField] private bool alwaysFaceMainCamera = true;

    // ── runtime state ──────────────────────────────────────────────
    private readonly List<Transform>  activeLetters      = new();
    private readonly List<TMP_Text>   activeTexts        = new();
    // trailOffset[i] = world-space horizontal distance from the first letter
    private readonly List<float>      activeTrailOffsets = new();

    private Camera    targetCamera;
    private Coroutine spawnRoutine;
    private string    currentWord = string.Empty;

    // ── public API ─────────────────────────────────────────────────
    public void InitializeWord(string word)
    {
        currentWord = string.IsNullOrWhiteSpace(word) ? string.Empty : word.Trim();
        if (isActiveAndEnabled) RestartTrail();
    }

    public string GetWord() => currentWord;

    // ── Unity messages ─────────────────────────────────────────────
    private void Awake() => targetCamera = Camera.main;

    private void OnEnable()
    {
        if (!string.IsNullOrEmpty(currentWord) && spawnRoutine == null && activeLetters.Count == 0)
            spawnRoutine = StartCoroutine(SpawnLetters());
    }

    private void OnDisable()
    {
        if (spawnRoutine != null) { StopCoroutine(spawnRoutine); spawnRoutine = null; }
    }

    private void LateUpdate()
    {
        if (!alwaysFaceMainCamera || activeLetters.Count == 0) return;
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        for (int i = 0; i < activeLetters.Count; i++)
        {
            if (activeLetters[i] == null) continue;
            UpdateLetterPosition(activeLetters[i], i, activeTrailOffsets[i], targetCamera.transform);
            FaceCamera(activeLetters[i], targetCamera.transform);
        }
    }

    // ── internals ──────────────────────────────────────────────────
    private void RestartTrail()
    {
        if (spawnRoutine != null) { StopCoroutine(spawnRoutine); spawnRoutine = null; }
        ClearLetters();
        if (!string.IsNullOrEmpty(currentWord))
            spawnRoutine = StartCoroutine(SpawnLetters());
    }

    private IEnumerator SpawnLetters()
    {
        if (letterPrefab == null)
        {
            Debug.LogWarning($"[{nameof(MorlockProjectileWordTrail)}] Missing letterPrefab on {name}.", this);
            yield break;
        }

        if (targetCamera == null) targetCamera = Camera.main;

        // ── PASS 1: instantiate all letters (positions are temporary) ──
        foreach (char c in currentWord)
        {
            if (char.IsWhiteSpace(c)) continue;

            GameObject obj = Instantiate(letterPrefab, transform);
            obj.name = $"Letter_{activeLetters.Count}_{c}";

            Transform t = obj.transform;
            t.localScale *= letterScaleMultiplier;

            TMP_Text tmp = obj.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = c.ToString();

            activeLetters.Add(t);
            activeTexts.Add(tmp);
            activeTrailOffsets.Add(0f); // placeholder

            if (letterRevealDelay > 0f)
                yield return new WaitForSeconds(letterRevealDelay);
            else
                yield return null;
        }

        // ── PASS 2: wait one frame so TMP has built all meshes ─────────
        yield return null;

        // ── PASS 3: measure real widths and assign trail offsets ────────
        float accumulated = 0f;
        for (int i = 0; i < activeLetters.Count; i++)
        {
            activeTrailOffsets[i] = accumulated;
            accumulated += MeasureLetterSpacing(activeLetters[i], activeTexts[i]);
        }

        // ── PASS 4: set final positions ─────────────────────────────────
        if (targetCamera != null)
        {
            for (int i = 0; i < activeLetters.Count; i++)
            {
                if (activeLetters[i] == null) continue;
                UpdateLetterPosition(activeLetters[i], i, activeTrailOffsets[i], targetCamera.transform);
                FaceCamera(activeLetters[i], targetCamera.transform);
            }
        }

        spawnRoutine = null;
    }

    /// <summary>Returns the world-space width to advance after placing this letter.</summary>
    private float MeasureLetterSpacing(Transform letterTransform, TMP_Text tmp)
    {
        float measured = minimumLetterWidth;

        if (tmp != null)
        {
            // preferredWidth is reliable after ForceMeshUpdate (called one frame after spawn)
            tmp.ForceMeshUpdate();
            measured = Mathf.Max(minimumLetterWidth, tmp.preferredWidth);
        }

        float worldWidth = measured * letterTransform.lossyScale.x;
        return baseLetterSpacing + worldWidth * widthSpacingMultiplier;
    }

    private void UpdateLetterPosition(
        Transform letterTransform,
        int index,
        float trailOffset,       // already in world-space units
        Transform cameraTransform)
    {
        // Horizontal: trail only (no extra per-index multiplier → no double-offset)
        float horizontal = firstLetterOffset.x + trailOffset;
        float vertical   = firstLetterOffset.y + verticalFalloffPerLetter * index;
        float depth      = firstLetterOffset.z + depthLagPerLetter        * index;

        // Project the projectile's backward direction onto the camera plane
        // so the trail always reads left→right from the camera's perspective.
        Vector3 trailDir = Vector3.ProjectOnPlane(-transform.forward, cameraTransform.forward);
        if (trailDir.sqrMagnitude <= 0.0001f) trailDir = cameraTransform.right;
        trailDir.Normalize();
        if (Vector3.Dot(trailDir, cameraTransform.right) < 0f) trailDir = -trailDir;

        Vector3 upDir = Vector3.ProjectOnPlane(cameraTransform.up, cameraTransform.forward);
        if (upDir.sqrMagnitude <= 0.0001f) upDir = cameraTransform.up;
        upDir.Normalize();

        letterTransform.position =
            transform.position
            + trailDir  * horizontal
            + upDir     * vertical
            - transform.forward * depth;
    }

    private static void FaceCamera(Transform t, Transform cam) =>
        t.rotation = Quaternion.LookRotation(cam.forward, cam.up);

    private void ClearLetters()
    {
        foreach (Transform t in activeLetters)
            if (t != null) Destroy(t.gameObject);

        activeLetters.Clear();
        activeTexts.Clear();
        activeTrailOffsets.Clear();
    }
}