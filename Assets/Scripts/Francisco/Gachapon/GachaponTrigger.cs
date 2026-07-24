using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[System.Serializable]
public struct RarityColorMapping
{
    public EffectRarity rarity;
    [ColorUsage(true, true)]
    public Color color;
}

[RequireComponent(typeof(Collider))]
public class GachaponTrigger : MonoBehaviour, PlayerControlls.IInteractionsActions
{
    [Header("Dependencies")]
    [SerializeField] private GachaAnimCtrl animController;
    [SerializeField] private GachaponSystem gachaponSystem;
    [SerializeField] private MeshRenderer cubeRenderer;
    [SerializeField] private Material activatedMaterial;
    [SerializeField] private GachaponPlayerDetector playerDetector;

    [Header("UI Dependencies")]
    [SerializeField] private GameObject resultUIPanel;
    [SerializeField] private TextMeshProUGUI nameTMP;
    [SerializeField] private TextMeshProUGUI effectsTMP;
    [SerializeField] private TMP_FontAsset effectsFont;

    [Header("Purchase Cooldown")]
    [SerializeField] private UnityEvent onFinish;

    [Header("SFX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip OpenSFX;
    [SerializeField] private AudioClip riseSFX;

    [Header("Configuración")]
    private bool playerIsNear = false;
    private bool isActivated = false;
    public bool allowMultiplePulls = false;
    private bool isAnimating = false;

    [Header("Animation Configuration")]
    public float animationDuration = 1.5f;
    [Range(0.05f, 1f)]
    public float cycleSpeed = 0.15f;
    public AnimationCurve colorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float intensityLerpDuration = 0.5f;
    [Range(1.0f, 10.0f)]
    public float finalIntensityMultiplier = 3.0f;
    public float pulseDuration = 3.0f;

    [Header("Sink Animation")]
    public float sinkDuration = 1.0f;
    public Transform sinkTargetTransform;
    public Transform riseTargetTransform;
    public AnimationCurve sinkCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Rarity Colors")]
    private List<RarityColorMapping> rarityColorMappings = new List<RarityColorMapping>
{
    new RarityColorMapping { rarity = EffectRarity.Comun, color = new Color(0.85f, 0.85f, 0.85f, 1f) },     
    new RarityColorMapping { rarity = EffectRarity.Raro, color = new Color(0.55f, 0.8f, 1f, 1f) },         
    new RarityColorMapping { rarity = EffectRarity.Epico, color = new Color(0.9f, 0.55f, 1f, 1f) },         
    new RarityColorMapping { rarity = EffectRarity.Legendario, color = new Color(1f, 0.85f, 0.4f, 1f) }      
};
    private Dictionary<object, Color> rarityColorsMap = new Dictionary<object, Color>();

    private Material originalMaterialInstance;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private Vector3 initialPosition;

    private PlayerControlls playerControls;
    private PlayerBlockSystem cachedBlockSystem;
    private CharacterController playerCharacterController;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

        if (gachaponSystem == null)
            gachaponSystem = FindAnyObjectByType<GachaponSystem>();

        if (playerDetector == null)
            playerDetector = GetComponentInChildren<GachaponPlayerDetector>();

        if (animController == null)
            animController = GetComponentInChildren<GachaAnimCtrl>();

        if (cubeRenderer == null)
            cubeRenderer = GetComponent<MeshRenderer>();

        if (gachaponSystem == null)
        {
            Debug.LogError("GachaponSystem no encontrado. Desactivando GachaponTrigger.");
            enabled = false;
            return;
        }

        playerControls = new PlayerControlls();
        playerControls.Interactions.SetCallbacks(this);
    }

    private void OnEnable()
    {
        playerControls?.Interactions.Enable();
    }

    private void OnDisable()
    {
        playerControls?.Interactions.Disable();
        if (resultUIPanel != null) resultUIPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        playerControls?.Dispose();
    }

    private void Start()
    {
        if (sinkTargetTransform == null || riseTargetTransform == null)
        {
            Debug.LogError("Las referencias 'sinkTargetTransform' y 'riseTargetTransform' deben ser asignadas en el Inspector. Desactivando GachaponTrigger.");
            enabled = false;
            return;
        }

        if (cubeRenderer != null) originalMaterialInstance = cubeRenderer.material;
        initialPosition = transform.position;

        if (effectsTMP != null && effectsFont != null)
        {
            effectsTMP.font = effectsFont;
        }

        foreach (var mapping in rarityColorMappings)
        {
            rarityColorsMap[mapping.rarity] = mapping.color;
        }

        if (resultUIPanel != null)
        {
            resultUIPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (SteamInputManager.Instance != null && SteamInputManager.Instance.GetInteractPressed())
        {
            if (playerIsNear && !isActivated && !isAnimating)
            {
                StartCoroutine(AnimateAndPull());
            }
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        if (playerIsNear && !isActivated && !isAnimating)
        {
            StartCoroutine(AnimateAndPull());
        }
    }

    public void OnAdvanceDialogue(InputAction.CallbackContext context) { }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            cachedBlockSystem = other.GetComponent<PlayerBlockSystem>();
            if (cachedBlockSystem != null)
            {
                cachedBlockSystem.SetBlockingEnabled(false);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && !isAnimating)
        {
            playerIsNear = true;

            if (cachedBlockSystem == null)
            {
                cachedBlockSystem = other.GetComponent<PlayerBlockSystem>();
                if (cachedBlockSystem != null) cachedBlockSystem.SetBlockingEnabled(false);
            }

            if (!isActivated && HUDManager.Instance != null)
            {
                HUDManager.Instance.SetInteractionPromptGACHAPON(true, "Interact", "TIRAR");
            }
            else
            {
                HUDManager.Instance.SetInteractionPromptGACHAPON(false, "Interact", "TIRAR");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsNear = false;

            if (cachedBlockSystem != null)
            {
                cachedBlockSystem.SetBlockingEnabled(true);
                cachedBlockSystem = null;
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.SetInteractionPromptGACHAPON(false, "Interact", "TIRAR");
            }
        }
    }

    private IEnumerator AnimateAndPull()
    {
        isAnimating = true;
        isActivated = true;
        ShowResultUI(false, "", "");

        if (audioSource != null && OpenSFX != null)
        {
            audioSource.PlayOneShot(OpenSFX);
        }

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.SetInteractionPromptGACHAPON(false, "Interact", "TIRAR");
        }

        if (animController != null)
        {
            animController.ActivateGacha();
            yield return new WaitUntil(() => animController.IsAnimating == false);
        }

        //Coroutine animationCoroutine = StartCoroutine(AnimateGachaponLight(animationDuration));
        //yield return animationCoroutine;

        GachaponResult result = gachaponSystem.PullGachapon();

        bool isEyeCollected = false;
        System.Action onCollectedCallback = () => isEyeCollected = true;

        animController.EyeScript.OnEyeCollected += onCollectedCallback;

        yield return new WaitUntil(() => isEyeCollected);

        animController.EyeScript.OnEyeCollected -= onCollectedCallback;

        onFinish?.Invoke();
        Debug.Log("Evento activando");

        if (rarityColorsMap.ContainsKey(result.rarity))
        {
            if (result.effectPair != null)
            {
                gachaponSystem.ApplyEffect(result);
                // Pasar la rareza obtenida al formatear los modificadores
                ShowResultUI(true, result.effectPair.effectName, FormatModifiers(result.effectPair, result.rarity));
            }
            else
            {
                ShowResultUI(true, "¡Error en la tirada!", "No se pudo obtener un efecto válido.");
            }

            //yield return StartCoroutine(SetFinalRarityColor(result.rarity));

            //if (rarityColorsMap.TryGetValue(result.rarity, out Color baseColor))
            //{
            //    yield return StartCoroutine(PulseFinalRarityColorWithDuration(baseColor, pulseDuration));
            //}
        }
        else
        {
            //cubeRenderer.material.SetColor(EmissionColor, Color.black);
            Debug.LogWarning("La gachapon no devolvió una rareza válida. Sistema bloqueado y apagado.");
        }

        yield return new WaitForSeconds(3f);
        ShowResultUI(false, "", "");

        if (allowMultiplePulls)
        {
            //cubeRenderer.material = originalMaterialInstance;
            isActivated = false;
            isAnimating = false;
            if (playerIsNear && HUDManager.Instance != null)
            {
                HUDManager.Instance.SetInteractionPromptGACHAPON(false, "Interact", "TIRAR");
            }
        }
        else
        {
            yield return StartCoroutine(SinkGachapon());
        }
    }

    private IEnumerator AnimateGachaponLight(float duration)
    {
        float startTime = Time.time;
        Material animMaterial = cubeRenderer.material;

        if (activatedMaterial != null && animMaterial != activatedMaterial)
        {
            cubeRenderer.material = activatedMaterial;
            animMaterial = cubeRenderer.material;
        }

        List<Color> cycleColors = rarityColorMappings.Select(m => m.color).ToList();

        if (!animMaterial.HasProperty(EmissionColor) || cycleColors.Count < 2)
        {
            yield break;
        }

        int colorIndex = 0;
        float segmentTime = 0f;

        while (Time.time < startTime + duration)
        {
            Color startColor = cycleColors[colorIndex];
            Color endColor = cycleColors[(colorIndex + 1) % cycleColors.Count];

            segmentTime += Time.deltaTime;
            float t = segmentTime / cycleSpeed;
            t = colorCurve.Evaluate(Mathf.Clamp01(t));

            Color currentColor = Color.Lerp(startColor, endColor, t);
            animMaterial.SetColor(EmissionColor, currentColor);

            if (t >= 1.0f)
            {
                colorIndex = (colorIndex + 1) % cycleColors.Count;
                segmentTime = 0f;
            }

            yield return null;
        }
    }

    private IEnumerator SetFinalRarityColor(EffectRarity rarity)
    {
        if (rarityColorsMap.TryGetValue(rarity, out Color baseColor))
        {
            if (activatedMaterial != null)
            {
                if (cubeRenderer.material != activatedMaterial)
                {
                    cubeRenderer.material = activatedMaterial;
                }
            }

            Material finalMaterial = cubeRenderer.material;
            Color startColor = finalMaterial.GetColor(EmissionColor);
            Color targetColor = baseColor * finalIntensityMultiplier;
            float elapsedTime = 0f;

            while (elapsedTime < intensityLerpDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / intensityLerpDuration;
                t = Mathf.SmoothStep(0f, 1f, t);
                Color currentColor = Color.Lerp(startColor, targetColor, t);
                finalMaterial.SetColor(EmissionColor, currentColor);
                yield return null;
            }

            finalMaterial.SetColor(EmissionColor, targetColor);
        }
    }

    private IEnumerator PulseFinalRarityColorWithDuration(Color baseColor, float duration)
    {
        Material finalMaterial = cubeRenderer.material;
        Color highIntensityColor = baseColor * finalIntensityMultiplier;
        Color lowIntensityColor = baseColor;
        float startTime = Time.time;

        while (Time.time < startTime + duration)
        {
            float t = Mathf.PingPong(Time.time / intensityLerpDuration, 1f);
            t = colorCurve.Evaluate(t);
            Color currentColor = Color.Lerp(lowIntensityColor, highIntensityColor, t);
            finalMaterial.SetColor(EmissionColor, currentColor);
            yield return null;
        }

        finalMaterial.SetColor(EmissionColor, highIntensityColor);
    }

    private IEnumerator SinkGachapon()
    {
        isAnimating = true;
        GetComponent<Collider>().enabled = false;
        playerIsNear = false;

        Vector3 startPos = transform.position;
        Vector3 endPos = sinkTargetTransform.position;
        float elapsedTime = 0f;

        while (elapsedTime < sinkDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / sinkDuration;
            t = sinkCurve.Evaluate(t);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        transform.position = endPos;
        isAnimating = false;
    }

    public void StartAnimationRise()
    {
        Debug.Log("Inicia");
        StartCoroutine(RiseGachapon());
    }

    private IEnumerator RiseGachapon()
    {
        isAnimating = true;

        GetComponent<Collider>().enabled = false;

        if (audioSource != null && riseSFX != null)
        {
            audioSource.clip = riseSFX;
            audioSource.Play();
        }

        Vector3 startPos = transform.position;
        Vector3 endPos = riseTargetTransform.position;
        float elapsedTime = 0f;

        float pushOutSpeed = 3.5f;

        while (elapsedTime < sinkDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / sinkDuration;
            t = sinkCurve.Evaluate(t);
            transform.position = Vector3.Lerp(startPos, endPos, t);

            if (playerDetector != null && playerDetector.PlayerInZone != null)
            {
                CharacterController cc = playerDetector.PlayerInZone;

                Vector3 pushDirection = cc.transform.position - transform.position;
                pushDirection.y = 0;

                if (pushDirection.sqrMagnitude < 0.01f)
                {
                    pushDirection = Vector3.forward;
                }

                cc.Move(pushDirection.normalized * (pushOutSpeed * Time.deltaTime));
                Debug.Log("<color=cyan>Empujando al jugador</color>");
            }

            yield return null;
        }

        transform.position = endPos;
        GetComponent<Collider>().enabled = true;
        isActivated = false;
        isAnimating = false;

        if (audioSource != null && audioSource.isPlaying && audioSource.clip == riseSFX)
        {
            audioSource.Stop();
        }

        if (playerIsNear && HUDManager.Instance != null)
        {
            HUDManager.Instance.SetInteractionPromptGACHAPON(true, "Interact", "TIRAR");
        }
    }

    private void ShowResultUI(bool show, string name, string effects)
    {
        if (resultUIPanel != null)
        {
            resultUIPanel.SetActive(show);
            if (show)
            {
                if (effectsTMP != null)
                {
                    effectsTMP.text = effects;
                }
            }
        }
    }
    
    private string FormatModifiers(GachaponEffectData effectData, EffectRarity obtainedRarity)
    {
        string rarityHex = rarityColorsMap.TryGetValue(obtainedRarity, out Color rarityColor)
            ? UnityEngine.ColorUtility.ToHtmlStringRGB(rarityColor)
            : "FFFFFF";

        string translatedRarity = TranslateRarity(obtainedRarity);

        string description = $"<size=160%><color=#{rarityHex}>{translatedRarity}</color></size>\n\n";

        if (effectData.HasAdvantage)
        {
            var advantages = effectData.GetAdvantageModifiersForRarity(obtainedRarity);
            foreach (var (statType, value, duration, isPercentage, durationType) in advantages)
            {
                string statName = TranslateStatType(statType);
                string durationText = TranslateDurationType(durationType, duration);
                string durationSuffix = string.IsNullOrEmpty(durationText) ? "" : $" ({durationText})";
                string sign = value > 0 ? "+" : "";
                description += $"<color=green><size=100%>{statName}</size><size=40%>\n\n</size><size=140%>{sign}{value}{(isPercentage ? "%" : "")}</size>{durationSuffix}</color>\n\n";
            }
        }

        if (effectData.HasDisadvantage)
        {
            var disadvantages = effectData.GetDisadvantageModifiersForRarity(obtainedRarity);
            foreach (var (statType, value, duration, isPercentage, durationType) in disadvantages)
            {
                string statName = TranslateStatType(statType);
                string durationText = TranslateDurationType(durationType, duration);
                string durationSuffix = string.IsNullOrEmpty(durationText) ? "" : $" ({durationText})";
                description += $"<color=red><size=100%>{statName}</size><size=40%>\n\n</size><size=140%>{value}{(isPercentage ? "%" : "")}</size>{durationSuffix}</color>\n\n";
            }
        }

        return description.TrimEnd('\n');
    }

    private string TranslateStatType(StatType statType)
    {
        switch (statType)
        {
            case StatType.MaxHealth:
                return "Salud Máxima";
            case StatType.Endurance:
                return "Resistencia";
            case StatType.HealthDrainAmount:
                return "Drenaje de Vida";

            case StatType.MoveSpeed:
                return "Velocidad de Movimiento";
            case StatType.Gravity:
                return "Gravedad";
            case StatType.DashRangeFlatBonus:
                return "Alcance del Impulso";
            case StatType.DashCooldownPost:
                return "Enfriamiento del Impulso";
            case StatType.KnockbackReceived:
                return "Empuje Recibido";
            case StatType.StaminaConsumption:
                return "Consumo de Energia";

            case StatType.AttackDamage:
                return "Daño a Melé y Distancia";
            case StatType.AttackSpeed:
                return "Velocidad de Ataque a Melé y Distancia";
            case StatType.MeleeAttackDamage:
                return "Daño a Melé";
            case StatType.MeleeAttackSpeed:
                return "Velocidad de Ataque a Melé";
            case StatType.MeleeRadius:
                return "Alcance del Ataque a Melé";
            case StatType.MeleeComboDisplacement:
                return "Desplazamiento al Golpear";
            case StatType.CriticalChance:
                return "Probabilidad de Crítico";
            case StatType.CriticalDamageMultiplier:
                return "Multiplicador de Daño Crítico";
            case StatType.LifestealOnKill:
                return "Robo de Vida por Eliminación";

            case StatType.ShieldAttackDamage:
                return "Daño de Ataque a Distancia";
            case StatType.ShieldSpeed:
                return "Velocidad de Ataque a Distancia";
            case StatType.ShieldMaxDistance:
                return "Alcance del Ataque a Distancia";
            case StatType.ShieldMaxRebounds:
                return "Rebote del Ataque a Distancia";
            case StatType.ShieldReboundRadius:
                return "Alcance del Rebote del Escudo";
            case StatType.ShieldPushForce:
                return "Empuje del Ataque a Distancia";
            case StatType.ShieldReturnSpeed:
                return "Velocidad de Retorno del Ataque a Distancia";

            case StatType.LuckStack:
                return "Suerte Acumulada";
            case StatType.EssenceCostReduction:
                return "Reducción de Costo de Esencia";
            case StatType.ShopPriceReduction:
                return "Reducción de Precio en Tienda";
            case StatType.HealthPerRoomRegen:
                return "Regeneración por Sala";
            default: return statType.ToString();
        }
    }

    private string TranslateRarity(EffectRarity rarity)
    {
        switch (rarity)
        {
            case EffectRarity.Comun:
                return "Común";
            case EffectRarity.Epico:
                return "Épico";
            case EffectRarity.Raro:
                return "Raro";
            case EffectRarity.Legendario:
                return "Legendario";
            default:
                return rarity.ToString();
        }
    }

    private string TranslateDurationType(EffectDurationType durationType, float durationValue)
    {
        switch (durationType)
        {
            case EffectDurationType.Permanent:
                return "";
            case EffectDurationType.Rounds:
                return $"{Mathf.CeilToInt(durationValue)} Sala(s)";
            case EffectDurationType.Time:
                return $"{durationValue:F1}s";
            default:
                return durationType.ToString();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            initialPosition = transform.position;
        }

        Vector3 upPosition = initialPosition;

        if (sinkTargetTransform != null)
        {
            Vector3 downPosition = sinkTargetTransform.position;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(upPosition, downPosition);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(upPosition, 0.05f);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(downPosition, 0.05f);
        }

        if (riseTargetTransform != null)
        {
            Vector3 riseTargetPosition = riseTargetTransform.position;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(upPosition, riseTargetPosition);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(riseTargetPosition, 0.05f);
        }
    }
}