using UnityEngine;
using System.Collections;
using TMPro;

public class DummyTarget : MonoBehaviour, IDamageable
{
    #region IDAMAGEABLE IMPLEMENTATION
    public virtual float CurrentHealth { get { return 100f; } }
    public virtual float MaxHealth { get { return 100f; } }
    #endregion

    #region REFERENCES AND CONFIGURATION
    protected Renderer dummyRenderer;
    protected Color baseColor;
    protected Coroutine colorChangeCoroutine;

    [Header("Dialogue Configuration")]
    public string[] dialogueLines;
    protected int dialogueIndex = 0;

    [Header("Invulnerability Settings")]
    public float invulnerabilityDuration = 0.5f;
    protected bool canBeHit = true;

    [Header("Visual Effects Settings")]
    public Color hitColor = Color.red;
    public float hitColorDuration = 0.2f;

    [Header("UI Configuration")]
    public GameObject logPanel;
    public TextMeshProUGUI logText;
    public float logPanelDisplayTime = 4f;

    protected Coroutine panelToggleCoroutine;

    [Header("Animation")]
    [SerializeField] protected Animator dummyAnimator;
    [SerializeField] protected witnessAnimCtrl dummyAnimCtrl;
    [SerializeField] protected string hitTriggerName = "OnHit";

    [Header("Rotation Logic")]
    [SerializeField] protected Transform transformToRotate;
    [SerializeField] protected float rotationSpeed = 10f;
    [SerializeField] protected float rotationYOffset = 0f;
    protected Coroutine rotationCoroutine;
    protected Transform playerTransform;
    protected Quaternion initialRotation;

    [Header("Audio")] // Nuevo Header para organizar los campos de audio
    [SerializeField] private AudioClip hitSound; // AudioClip para el sonido de golpe
    private AudioSource audioSource; // Componente AudioSource

    #endregion

    #region INITIALIZATION
    protected virtual void Awake()
    {
        dummyRenderer = GetComponent<Renderer>();

        if (dummyRenderer != null)
        {
            baseColor = dummyRenderer.material.GetColor("_Color");
        }
        else
        {
            Debug.LogError("Renderer component not found on DummyTarget.");
        }

        if (logPanel != null) logPanel.SetActive(false);

        if (dummyAnimator == null)
        {
            dummyAnimator = GetComponent<Animator>();
        }

        if (transformToRotate == null)
        {
            transformToRotate = transform.parent != null ? transform.parent : transform;
        }

        initialRotation = transformToRotate.rotation;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        // Inicializar AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        }
    }
    #endregion

    #region CORE FUNCTIONALITY
    public virtual void TakeDamage(float damageAmount, bool isCritical = false, AttackDamageType attackDamageType = AttackDamageType.Melee)
    {
        if (!canBeHit)
        {
            return;
        }

        canBeHit = false;
        StartCoroutine(HitCooldown());

        if (dummyAnimator != null)
        {
            // dummyAnimator.SetTrigger(hitTriggerName);
            dummyAnimCtrl?.PlayHit();
        }

        // Reproducir sonido de golpe
        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }

        if (colorChangeCoroutine != null)
        {
            StopCoroutine(colorChangeCoroutine);
        }
        colorChangeCoroutine = StartCoroutine(FlashColor());

        if (playerTransform != null)
        {
            if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
            rotationCoroutine = StartCoroutine(RotateTowardsPlayer());
        }

        ShowNextLineCyclic();

        Debug.Log($"Maniquí golpeado. Daño: {damageAmount} (Crítico: {isCritical}). Nueva línea de diálogo.");
    }

    protected IEnumerator HitCooldown()
    {
        yield return new WaitForSeconds(invulnerabilityDuration);
        canBeHit = true;
    }

    public void ShowNextLineCyclic()
    {
        if (dialogueLines == null || dialogueLines.Length == 0)
        {
            if (logText != null) logText.text = "No hay líneas de diálogo configuradas.";
            return;
        }

        dialogueIndex++;

        if (dialogueIndex >= dialogueLines.Length)
        {
            dialogueIndex = 0;
        }

        ShowCurrentLine();
    }

    protected void ShowCurrentLine()
    {
        UpdateLogUI();

        if (panelToggleCoroutine != null)
        {
            StopCoroutine(panelToggleCoroutine);
        }
        panelToggleCoroutine = StartCoroutine(TogglePanelAfterDelay());
    }

    #endregion

    #region LOGIC AND UI
    protected void UpdateLogUI()
    {
        if (logText == null || dialogueLines == null || dialogueLines.Length == 0) return;

        logText.text = dialogueLines[dialogueIndex];
    }

    protected IEnumerator TogglePanelAfterDelay()
    {
        if (logPanel != null)
        {
            logPanel.SetActive(true);
        }

        yield return new WaitForSeconds(logPanelDisplayTime);

        if (logPanel != null)
        {
            logPanel.SetActive(false);
        }
    }

    #endregion

    #region ROTATION
    protected IEnumerator RotateTowardsPlayer()
    {
        if (transformToRotate == null || playerTransform == null) yield break;

        Vector3 direction = (playerTransform.position - transformToRotate.position).normalized;
        direction.y = 0;

        if (direction == Vector3.zero) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        targetRotation *= Quaternion.Euler(0, rotationYOffset, 0);

        float elapsedTime = 0f;
        float maxRotationTime = 2f;

        while (Quaternion.Angle(transformToRotate.rotation, targetRotation) > 0.1f && elapsedTime < maxRotationTime)
        {
            transformToRotate.rotation = Quaternion.Slerp(
                transformToRotate.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transformToRotate.rotation = targetRotation;
        rotationCoroutine = null;
    }
    #endregion

    #region VISUAL EFFECTS
    protected IEnumerator FlashColor()
    {
        if (dummyRenderer != null)
        {
            dummyRenderer.material.color = hitColor;

            yield return new WaitForSeconds(hitColorDuration);

            dummyRenderer.material.color = baseColor;
        }
    }
    #endregion

    #region CLEANUP
    protected virtual void OnDestroy()
    {
        if (dummyRenderer != null && dummyRenderer.material != null)
        {
            Destroy(dummyRenderer.material);
        }
    }
    #endregion
}
