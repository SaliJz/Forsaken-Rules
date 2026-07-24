using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// HealthCommentDisplay (ajustada para repetir pasivos cada idleTime si no hay actividad)
/// - Si no hay actividad durante `idleTimeToShowPassive`, se considera inactivo.
/// - Mientras siga sin actividad, se muestra un pasivo cada `idleTimeToShowPassive`.
/// - Mostrar un pasivo NO reinicia la marca de actividad, por eso se repetirán los pasivos
///   cada periodo si no pasa nada (tal como pediste).
/// - Recibir daño o curación actualiza `lastHealthActivityTime` y desarma el "armed" estado.
/// - El primer cambio de salud cuando estamos armados muestra el comentario inmediatamente y reinicia.
/// - Respeta cooldowns para positivos y globalMessageCooldown para otros casos; el auto-pasivo ignora globalMessageCooldown
///   para permitir repetición cada intervalo mientras no haya acción.
/// </summary>
public class HealthCommentDisplay : MonoBehaviour
{
    // Defaults inmutables (no serializar)
    private static readonly string[] defaultPositiveMessages = new string[]
    {
        "Oh, curado... pensé que morirías antes. Qué esperaba, ¿drama?",
        "Bien, otra vez con vida. No me hagas esperar el espectáculo.",
        "Te arreglan y vuelves. Qué persistente... o desesperado.",
        "Has sanado. Me alegra seguir viéndote fallar en directo.",
        "Perfecto, vuelves a brillar — por ahora me entretienes.",
        "Otra curación. Qué adorable tu negación ante la muerte.",
        "Te dejo vivir otra vez. Devuélveme algo de emoción.",
        "Regresaste — buen movimiento para mantener mi apuesta viva.",
        "Salvaste un latido más. Demuéstrame que valió la pena.",
        "Sanas y sigues; me gusta tu tenacidad (y mi entretenimiento)."
    };

    private static readonly string[] defaultNegativeMessages = new string[]
    {
        "Jaja, tu familia nunca volverá a verte. Qué lástima, ¿eh?",
        "¿Eso es todo? Pensé que durarías más, ridículo.",
        "¿Te duele? Qué adorablemente patético.",
        "Me estás aburriendo con esa debilidad, muévete.",
        "¿Creías que sería fácil? ja, te equivocas.",
        "Ves cómo caes; yo apuesto y gano. Tú pierdes.",
        "No sabes ni levantarte bien. ¿Tu orgullo dónde está?",
        "¿Quieres que me ría un poco más? Porque es divertido.",
        "Apenas un rasguño y ya te quejas. Patético.",
        "Te dije que no confiaras en nadie; sobre todo en ti.",
        "¿Tu madre te dijo que fueras así de inútil? Qué vergüenza.",
        "Sangras como si fuera arte. Yo aplaudo desde aquí.",
        "Se te nota el miedo en la voz. Me encanta oírlo.",
        "Tienes dos opciones: mejorar o ser mi entretenimiento.",
        "Ni siquiera te esfuerzas. ¿Qué esperabas?",
        "Un golpe más y me llevo todo. Me gusta esa sensación.",
        "Te doy oportunidades y las desperdicias. Triste.",
        "¿Vas a dejarme sin espectáculo? Sigue fallando.",
        "Tu final será bonito — para mis historias.",
        "No tengo piedad contigo; me diviertes demasiado.",
        "Esa caída fue preciosa. Gracias por el show.",
        "¿Dónde está tu orgullo ahora que te tengo contra la pared?",
        "Apenas sobrevives y ya me aburres. Haz algo digno.",
        "No llores, que yo disfruto del drama más que nadie.",
        "Si te rindes, mi apuesta se cumple. ¿No te importa?",
        "Te siento débil. Eso me pone de buen humor."
    };

    private static readonly string[] defaultPassiveMessages = new string[]
    {
        "¿Qué haces? ¿Contemplando tu destino?",
        "Muévete rápido, ¿o prefieres ser un adorno?",
        "¿Quieto? Me aburres. Haz algo interesante.",
        "No vas a avanzar rápido? ¿No quieres ver a tu madre ya?",
        "Levántate, que la diversión no espera.",
        "Digo… ¿vas a quedarte parado toda la partida?",
        "No me hagas intervenir — muévete.",
        "Qué paciencia la tuya… ¿o es miedo?",
        "Anda, apúrate. Tengo apuestas que cerrar.",
        "¿Sin reacción? Eso no me gusta.",
        "Mueve esos pies, que me paguen por el espectáculo.",
        "No te quedes ahí parado, que la vida no se gana sola."
    };

    [Header("Listas de mensajes (Editable en Inspector)")]
    [TextArea(2, 6)]
    [SerializeField] private List<string> positiveMessages;
    [TextArea(2, 6)]
    [SerializeField] private List<string> negativeMessages;
    [TextArea(2, 6)]
    [SerializeField] private List<string> passiveMessages;

    [Header("Referencias UI (preferible asignar las existentes en escena)")]
    public TextMeshProUGUI existingTextInstance;
    public TextMeshProUGUI textPrefab;
    public RectTransform spawnParent;

    [Header("Icono (devil) al lado del texto")]
    public Image existingIconInstance;
    public Image iconPrefab;
    public RectTransform iconParent;
    public Sprite devilSprite;

    [Header("Sonidos")]
    public AudioClip negativeMockingClip;
    public AudioSource audioSource;

    [Header("Comportamiento y duraciones")]
    public float displayDuration = 1.1f;
    public bool ignoreFirstHealthUpdate = true;

    [Tooltip("¿Mostrar mensajes pasivos cuando no hay cambios de vida?")]
    public bool enablePassiveMessages = true;

    [Tooltip("Segundos sin cambios de vida para ARMAR/mostrar el pasivo (y para repetir si sigue inactivo).")]
    public float idleTimeToShowPassive = 6f;

    [Tooltip("Cooldown entre mensajes pasivos (útil si quieres limitar, pero repetición usa idleTime).")]
    public float passiveMessageCooldown = 6f;

    [Header("Control de frecuencia para mensajes POSITIVOS")]
    public float positiveMessageCooldown = 6f;

    [Header("Agrupación de cambios de vida (evita spam)")]
    public float aggregationWindow = 0.6f;
    public float minChangeToConsider = 1f;

    [Header("Cooldown global entre mensajes (el primero mostrado gana durante este periodo)")]
    public float globalMessageCooldown = 6f;

    [Header("Modo: armar en idle")]
    [Tooltip("Si true, al pasar idleTimeToShowPassive el sistema se 'arma'. El próximo cambio de salud mostrará mensaje inmediato.")]
    public bool armOnIdle = true;

    // Internals
    private float previousHealth = -1f;
    private bool initialized = false;

    private TextMeshProUGUI currentInstance = null;
    private Image currentIconInstance = null;
    private Coroutine currentCoroutine = null;

    // Última actividad relacionada con la salud (daño o curación).
    private float lastHealthActivityTime = -999f;
    private float lastPassiveShownTime = -999f;
    private float lastPositiveShownTime = -999f;
    private float lastMessageShownTime = -999f;

    // Cycling orders
    private List<int> positiveOrder;
    private int positiveOrderIndex = 0;
    private List<int> negativeOrder;
    private int negativeOrderIndex = 0;
    private List<int> passiveOrder;
    private int passiveOrderIndex = 0;

    private enum MessageCategory { None, Positive, Negative, Passive }
    private MessageCategory currentCategory = MessageCategory.None;

    // Agrupación
    private float pendingNetChange = 0f;
    private float lastHealthChangeTime = -999f;
    private Coroutine aggregateCoroutine = null;

    // Estado armado: si true, el próximo cambio de salud disparará mensaje inmediato.
    private bool armedForNextHealthChange = false;

    private void OnValidate()
    {
        EnsureDefaultList(ref positiveMessages, defaultPositiveMessages);
        EnsureDefaultList(ref negativeMessages, defaultNegativeMessages);
        EnsureDefaultList(ref passiveMessages, defaultPassiveMessages);

        if (displayDuration < 0f) displayDuration = 1.1f;
        if (idleTimeToShowPassive < 0f) idleTimeToShowPassive = 6f;
        if (passiveMessageCooldown < 0f) passiveMessageCooldown = 6f;
        if (positiveMessageCooldown < 0f) positiveMessageCooldown = 6f;
        if (aggregationWindow < 0f) aggregationWindow = 0.6f;
        if (minChangeToConsider < 0f) minChangeToConsider = 1f;
        if (globalMessageCooldown < 0f) globalMessageCooldown = 6f;

        RebuildOrdersIfNeeded();
    }

    private void Awake()
    {
        if (spawnParent == null && existingTextInstance == null)
        {
            Canvas c = FindFirstObjectByType<Canvas>();
            if (c != null) spawnParent = c.GetComponent<RectTransform>();
        }

        if (iconParent == null) iconParent = spawnParent;

        if (existingTextInstance != null)
        {
            existingTextInstance.gameObject.SetActive(false);
            Color c = existingTextInstance.color; c.a = 0f; existingTextInstance.color = c;
        }

        if (existingIconInstance != null)
        {
            existingIconInstance.gameObject.SetActive(false);
            Color ic = existingIconInstance.color; ic.a = 0f; existingIconInstance.color = ic;
        }

        EnsureDefaultList(ref positiveMessages, defaultPositiveMessages);
        EnsureDefaultList(ref negativeMessages, defaultNegativeMessages);
        EnsureDefaultList(ref passiveMessages, defaultPassiveMessages);

        RebuildOrdersIfNeeded();

        EnsureAudioSource();
        if (audioSource != null && audioSource.clip == null && negativeMockingClip != null) audioSource.clip = negativeMockingClip;
        if (audioSource != null && audioSource.clip == null)
        {
            AudioClip rc = Resources.Load<AudioClip>("HealthCommentDefault");
            if (rc != null) audioSource.clip = rc;
        }

        if (devilSprite == null)
        {
            Sprite s = Resources.Load<Sprite>("DevilIcon");
            if (s != null) devilSprite = s;
        }
    }

    private void Start()
    {
        if (PlayerHealth.Instance != null)
        {
            InitializeFromPlayer(PlayerHealth.Instance);
        }
        else
        {
            PlayerHealth.OnPlayerInstantiated += OnPlayerInstantiated;
        }
    }

    private void OnPlayerInstantiated(PlayerHealth ph)
    {
        InitializeFromPlayer(ph);
        PlayerHealth.OnPlayerInstantiated -= OnPlayerInstantiated;
    }

    private void InitializeFromPlayer(PlayerHealth ph)
    {
        if (ph == null) return;

        previousHealth = ph.CurrentHealth;
        PlayerHealth.OnHealthChanged += HandleOnHealthChanged;
        initialized = true;
        lastHealthActivityTime = Time.time;
        lastPassiveShownTime = Time.time - idleTimeToShowPassive;
        lastPositiveShownTime = Time.time - positiveMessageCooldown;
        lastMessageShownTime = Time.time - globalMessageCooldown;
        armedForNextHealthChange = false;
    }

    private void OnDestroy()
    {
        PlayerHealth.OnHealthChanged -= HandleOnHealthChanged;
        PlayerHealth.OnPlayerInstantiated -= OnPlayerInstantiated;
    }

    private void Update()
    {
        if (!initialized) return;

        // Si estamos inactivos desde hace idleTimeToShowPassive o más, mostramos pasivo periódicamente.
        // NO reiniciamos lastHealthActivityTime al mostrar pasivo para que siga contando inactividad.
        if (enablePassiveMessages && Time.time - lastHealthActivityTime >= idleTimeToShowPassive
            && Time.time - lastPassiveShownTime >= idleTimeToShowPassive
            && !(currentCoroutine != null && currentCategory == MessageCategory.Passive))
        {
            // Si armOnIdle está activado, marcamos armado para el próximo cambio de salud.
            if (armOnIdle)
            {
                armedForNextHealthChange = true;
            }

            // Mostrar pasivo AUTOMÁTICO desde inactividad; permitimos que esto ignore globalMessageCooldown
            ShowRandomPassiveComment(bypassGlobal: true);
            lastPassiveShownTime = Time.time;

            // IMPORTANT: NO actualizamos lastHealthActivityTime aquí, para que la inactividad continúe y
            // los pasivos sigan repitiéndose cada idleTimeToShowPassive mientras no haya actividad.
        }
    }

    /// <summary>
    /// Manejo de cambios de salud.
    /// - Reinicia la marca de actividad (lastHealthActivityTime) en cada cambio de salud.
    /// - Si estamos ARMADOS: el primer cambio dispara comentario inmediatamente y desarma.
    /// - Si no armados: acumula cambios durante aggregationWindow (comportamiento previo).
    /// </summary>
    private void HandleOnHealthChanged(float currentHealth, float maxHealth)
    {
        // Registrar actividad (esto reinicia el contador de pasivos y cancela armado)
        lastHealthActivityTime = Time.time;

        if (!initialized)
        {
            previousHealth = currentHealth;
            initialized = true;
            return;
        }

        if (ignoreFirstHealthUpdate && Mathf.Approximately(previousHealth, currentHealth))
        {
            previousHealth = currentHealth;
            return;
        }

        float delta = currentHealth - previousHealth;

        // Si estamos ARMADOS: el primer cambio dispara y desarma.
        if (armOnIdle && armedForNextHealthChange)
        {
            armedForNextHealthChange = false;

            // Si cooldown global está activo, no mostramos; limpiar acumulador.
            if (Time.time - lastMessageShownTime < globalMessageCooldown)
            {
                pendingNetChange = 0f;
                previousHealth = currentHealth;
                return;
            }

            if (delta >= minChangeToConsider)
            {
                if (Time.time - lastPositiveShownTime >= positiveMessageCooldown && !(currentCoroutine != null && currentCategory == MessageCategory.Positive))
                {
                    ShowRandomComment(true, delta);
                    lastPositiveShownTime = Time.time;
                }
            }
            else if (delta <= -minChangeToConsider)
            {
                if (!(currentCoroutine != null && currentCategory == MessageCategory.Negative))
                {
                    ShowRandomComment(false, Mathf.Abs(delta));
                }
            }
            else
            {
                // cambio muy pequeño -> tratamos como passive opcional
                if (enablePassiveMessages && Time.time - lastPassiveShownTime >= passiveMessageCooldown && !(currentCoroutine != null && currentCategory == MessageCategory.Passive))
                {
                    ShowRandomPassiveComment();
                    lastPassiveShownTime = Time.time;
                }
            }

            pendingNetChange = 0f;
            previousHealth = currentHealth;
            return;
        }

        // No estamos armados: comportamiento de agregación
        pendingNetChange += delta;
        lastHealthChangeTime = Time.time;

        if (aggregateCoroutine == null)
            aggregateCoroutine = StartCoroutine(AggregateAndDecideRoutine());

        // Asegurar que no quedamos armados por error
        armedForNextHealthChange = false;
        previousHealth = currentHealth;
    }

    private IEnumerator AggregateAndDecideRoutine()
    {
        while (Time.time - lastHealthChangeTime < aggregationWindow)
        {
            yield return null;
        }

        float net = pendingNetChange;
        pendingNetChange = 0f;
        aggregateCoroutine = null;

        if (Time.time - lastMessageShownTime < globalMessageCooldown) yield break;

        if (net >= minChangeToConsider)
        {
            if (Time.time - lastPositiveShownTime >= positiveMessageCooldown && !(currentCoroutine != null && currentCategory == MessageCategory.Positive))
            {
                ShowRandomComment(true, net);
                lastPositiveShownTime = Time.time;
            }
        }
        else if (net <= -minChangeToConsider)
        {
            if (!(currentCoroutine != null && currentCategory == MessageCategory.Negative))
            {
                ShowRandomComment(false, Mathf.Abs(net));
            }
        }
        else
        {
            if (enablePassiveMessages && Time.time - lastPassiveShownTime >= passiveMessageCooldown && !(currentCoroutine != null && currentCategory == MessageCategory.Passive))
            {
                ShowRandomPassiveComment(); // aquí respeta globalMessageCooldown por la propia función
                lastPassiveShownTime = Time.time;
            }
        }
    }

    private void ShowRandomComment(bool isPositive, float amountChanged)
    {
        string message = isPositive
            ? GetNextMessageFromCycle(positiveMessages, ref positiveOrder, ref positiveOrderIndex)
            : GetNextMessageFromCycle(negativeMessages, ref negativeOrder, ref negativeOrderIndex);

        if (string.IsNullOrEmpty(message)) return;

        MessageCategory category = isPositive ? MessageCategory.Positive : MessageCategory.Negative;

        if (currentCoroutine != null && currentCategory == category) return;
        if (Time.time - lastMessageShownTime < globalMessageCooldown) return;

        ShowComment(message, category);
    }

    /// <summary>
    /// ShowRandomPassiveComment ahora acepta bypassGlobal:
    /// - bypassGlobal == true : ignorar globalMessageCooldown (usado por auto-pasivo cada idle interval).
    /// - bypassGlobal == false: respeta globalMessageCooldown (usado en agregación/neutrales).
    /// </summary>
    private void ShowRandomPassiveComment(bool bypassGlobal = false)
    {
        string message = GetNextMessageFromCycle(passiveMessages, ref passiveOrder, ref passiveOrderIndex);
        if (string.IsNullOrEmpty(message)) return;

        if (currentCoroutine != null && currentCategory == MessageCategory.Passive) return;

        if (!bypassGlobal && Time.time - lastMessageShownTime < globalMessageCooldown) return;

        ShowComment(message, MessageCategory.Passive);
    }

    private Color ColorForPositive() => new Color(0.18f, 0.95f, 0.25f, 1f);
    private Color ColorForNegative() => new Color(1f, 0.35f, 0.35f, 1f);
    private Color ColorForPassive() => new Color(1f, 0.8f, 0.2f, 1f);

    private string GetNextMessageFromCycle(List<string> list, ref List<int> order, ref int orderIndex)
    {
        if (list == null || list.Count == 0) return string.Empty;

        if (order == null || order.Count != list.Count)
        {
            order = CreateShuffledOrder(list.Count);
            orderIndex = 0;
        }

        if (orderIndex >= order.Count)
        {
            order = CreateShuffledOrder(list.Count);
            orderIndex = 0;
        }

        int idx = order[orderIndex];
        orderIndex++;
        if (idx < 0 || idx >= list.Count) return string.Empty;
        return list[idx];
    }

    private List<int> CreateShuffledOrder(int n)
    {
        List<int> o = new List<int>(n);
        for (int i = 0; i < n; i++) o.Add(i);
        for (int i = n - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int tmp = o[i];
            o[i] = o[j];
            o[j] = tmp;
        }
        return o;
    }

    private void ShowComment(string message, MessageCategory category)
    {
        if (currentCoroutine != null && currentCategory == category) return;

        if (currentCoroutine != null && currentCategory != category)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;

            if (currentInstance != null)
            {
                if (existingTextInstance != null && currentInstance == existingTextInstance)
                {
                    currentInstance.gameObject.SetActive(false);
                    Color c = currentInstance.color; c.a = 0f; currentInstance.color = c;
                }
                else
                {
                    if (currentInstance.gameObject != null) Destroy(currentInstance.gameObject);
                }
                currentInstance = null;
            }

            if (currentIconInstance != null)
            {
                if (existingIconInstance != null && currentIconInstance == existingIconInstance)
                {
                    currentIconInstance.gameObject.SetActive(false);
                    Color ic = currentIconInstance.color; ic.a = 0f; currentIconInstance.color = ic;
                }
                else
                {
                    if (currentIconInstance.gameObject != null) Destroy(currentIconInstance.gameObject);
                }
                currentIconInstance = null;
            }

            currentCategory = MessageCategory.None;
        }

        EnsureAudioSource();
        AudioClip clipToPlay = negativeMockingClip != null ? negativeMockingClip : (audioSource != null ? audioSource.clip : null);
        if (clipToPlay != null && audioSource != null) audioSource.PlayOneShot(clipToPlay);

        Color color = ColorForPassive();
        if (category == MessageCategory.Positive) color = ColorForPositive();
        else if (category == MessageCategory.Negative) color = ColorForNegative();

        if (existingTextInstance != null)
        {
            currentInstance = existingTextInstance;
            currentInstance.text = message;
            currentInstance.color = color;
            currentInstance.gameObject.SetActive(true);
            Color start = currentInstance.color; start.a = 1f; currentInstance.color = start;

            if (existingIconInstance != null)
            {
                currentIconInstance = existingIconInstance;
                if (devilSprite != null) currentIconInstance.sprite = devilSprite;
                currentIconInstance.gameObject.SetActive(true);
                Color icStart = currentIconInstance.color; icStart.a = 1f; currentIconInstance.color = icStart;
            }

            currentCategory = category;
            lastMessageShownTime = Time.time;
            currentCoroutine = StartCoroutine(FadeAndHideRoutine(currentInstance, currentIconInstance, displayDuration));
        }
        else
        {
            if (textPrefab != null)
            {
                if (spawnParent == null)
                {
                    Canvas c = FindFirstObjectByType<Canvas>();
                    if (c != null) spawnParent = c.GetComponent<RectTransform>();
                    if (spawnParent == null)
                    {
                        Debug.LogWarning("[HealthCommentDisplay] No Canvas para instanciar prefab. Abortando ShowComment.");
                        return;
                    }
                }

                TextMeshProUGUI inst = Instantiate(textPrefab, spawnParent);
                inst.gameObject.SetActive(true);
                inst.text = message;
                inst.color = color;
                currentInstance = inst;

                if (iconPrefab != null)
                {
                    RectTransform parentForIcon = iconParent != null ? iconParent : spawnParent;
                    Image iconInst = Instantiate(iconPrefab, parentForIcon);
                    iconInst.gameObject.SetActive(true);
                    if (devilSprite != null) iconInst.sprite = devilSprite;
                    Color ic = iconInst.color; ic.a = 1f; iconInst.color = ic;
                    currentIconInstance = iconInst;
                }
                else if (existingIconInstance != null)
                {
                    currentIconInstance = existingIconInstance;
                    if (devilSprite != null) currentIconInstance.sprite = devilSprite;
                    currentIconInstance.gameObject.SetActive(true);
                    Color icStart = currentIconInstance.color; icStart.a = 1f; currentIconInstance.color = icStart;
                }

                currentCategory = category;
                lastMessageShownTime = Time.time;
                currentCoroutine = StartCoroutine(FadeAndDestroyRoutine(inst, currentIconInstance, displayDuration));
            }
            else
            {
                Debug.LogWarning("[HealthCommentDisplay] No existingTextInstance ni textPrefab asignado.");
            }
        }
    }

    private IEnumerator FadeAndHideRoutine(TextMeshProUGUI tmp, Image icon, float duration)
    {
        if (tmp == null)
        {
            if (icon != null) icon.gameObject.SetActive(false);
            currentCategory = MessageCategory.None;
            yield break;
        }

        float elapsed = 0f;
        Color startText = tmp.color; startText.a = 1f; tmp.color = startText;

        Color startIcon = Color.clear;
        if (icon != null) { startIcon = icon.color; startIcon.a = 1f; icon.color = startIcon; }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Color c = tmp.color; c.a = Mathf.Lerp(1f, 0f, t); tmp.color = c;
            if (icon != null) { Color ic = icon.color; ic.a = Mathf.Lerp(1f, 0f, t); icon.color = ic; }
            yield return null;
        }

        Color final = tmp.color; final.a = 0f; tmp.color = final;
        tmp.gameObject.SetActive(false);

        if (icon != null)
        {
            Color ifinal = icon.color; ifinal.a = 0f; icon.color = ifinal;
            icon.gameObject.SetActive(false);
        }

        currentCoroutine = null;
        currentInstance = null;
        currentIconInstance = null;
        currentCategory = MessageCategory.None;
    }

    private IEnumerator FadeAndDestroyRoutine(TextMeshProUGUI tmp, Image icon, float duration)
    {
        if (tmp == null)
        {
            if (icon != null && icon.gameObject != null) Destroy(icon.gameObject);
            currentCategory = MessageCategory.None;
            yield break;
        }

        float elapsed = 0f;
        Color startText = tmp.color; startText.a = 1f; tmp.color = startText;

        Color startIcon = Color.clear;
        if (icon != null) { startIcon = icon.color; startIcon.a = 1f; icon.color = startIcon; }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Color c = tmp.color; c.a = Mathf.Lerp(1f, 0f, t); tmp.color = c;
            if (icon != null) { Color ic = icon.color; ic.a = Mathf.Lerp(1f, 0f, t); icon.color = ic; }
            yield return null;
        }

        if (tmp != null && tmp.gameObject != null) Destroy(tmp.gameObject);
        if (icon != null && icon.gameObject != null) Destroy(icon.gameObject);

        currentCoroutine = null;
        currentInstance = null;
        currentIconInstance = null;
        currentCategory = MessageCategory.None;
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null) return;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // UI sound
        }
    }

    private void EnsureDefaultList(ref List<string> list, string[] defaults)
    {
        if (defaults == null || defaults.Length == 0) return;
        if (list == null || list.Count == 0)
        {
            list = new List<string>(defaults);
        }
    }

    private void RebuildOrdersIfNeeded()
    {
        if (positiveMessages != null)
        {
            if (positiveOrder == null || positiveOrder.Count != positiveMessages.Count)
            {
                positiveOrder = CreateShuffledOrder(positiveMessages.Count);
                positiveOrderIndex = 0;
            }
        }
        if (negativeMessages != null)
        {
            if (negativeOrder == null || negativeOrder.Count != negativeMessages.Count)
            {
                negativeOrder = CreateShuffledOrder(negativeMessages.Count);
                negativeOrderIndex = 0;
            }
        }
        if (passiveMessages != null)
        {
            if (passiveOrder == null || passiveOrder.Count != passiveMessages.Count)
            {
                passiveOrder = CreateShuffledOrder(passiveMessages.Count);
                passiveOrderIndex = 0;
            }
        }
    }
}
