// HealthController.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente unificado de vida (Player / Enemy).
/// - Soporta 2 capas de vida (primaria = azul, secundaria = roja)
/// - Implementa IDamageable (si existe en tu proyecto)
/// - Callbacks (OnDamaged, OnInterrupted, OnOverwhelmed, OnRecovered)
/// - Opciones para notificar controller (SendMessage o invocar TakeDamage si existe)
/// - Manejo seguro de referencias UI (no lanza NullReference si faltan)
/// - Escudo por tag "Escudo" (OnTriggerEnter)
/// </summary>
public class HealthController : MonoBehaviour /*, IDamageable*/ // Descomenta ", IDamageable" si tu proyecto define esa interfaz.
{
    public enum Role { Generic, Player, Enemy }

    [Header("Rol / Comportamiento")]
    public Role role = Role.Generic;
    [Tooltip("Si true -> SetActive(false) en la muerte; si false -> Destroy(gameObject).")]
    public bool disableOnDeath = false;

    [Header("Vidas por capas")]
    public int firstMaxLife = 20;   // primera vida (azul)
    public int secondMaxLife = 20;  // segunda vida (roja)

    // valores internos
    int firstCurrent;
    int secondCurrent;

    [Header("Detección de golpes rápidos (opcional)")]
    public bool enableHitRecovery = true;
    public int hitThreshold = 3;
    public float hitCountWindow = 1.2f;
    public float recoveryNoHitTime = 2.0f;

    [Header("Configuración de Escudo")]
    public int escudoDamage = 25;

    [Header("Referencias (opcional)")]
    public MonoBehaviour controller; // puede ser MeleeEnemyController u otro. Se usa con SendMessage o reflexión.
    [Tooltip("Si activado, intentará invocar controller.TakeDamage(amount). Si false, sólo SendMessage(\"OnEnemyDamaged\", amount).")]
    public bool notifyControllerOnDamage = false;

    [Header("UI - Sliders (opcional)")]
    public Slider firstLifeSlider;    // asignar slider azul (primera vida)
    public Slider secondLifeSlider;   // asignar slider rojo (segunda vida)
    public Image firstFillImage;      // opcional: imagen del fill para colorear (azul)
    public Image secondFillImage;     // opcional: imagen del fill para colorear (roja)

    [Header("Robo de vida al jugador (opcional)")]
    [SerializeField] private float healthStealAmount = 0f;

    // Callbacks
    public Action OnDamaged;
    public Action OnInterrupted;
    public Action OnOverwhelmed;
    public Action OnRecovered;

    // estado de "overwhelmed"
    int recentHits = 0;
    float lastHitTime = -999f;
    bool isOverwhelmed = false;
    Coroutine recoveryCoroutine;

    // helpers
    public int MaxHealth { get { return Mathf.Max(0, firstMaxLife) + Mathf.Max(0, secondMaxLife); } }
    public int CurrentHealth { get { return Mathf.Max(0, firstCurrent) + Mathf.Max(0, secondCurrent); } }

    private PlayerHealth playerHealth;

    void Awake()
    {
        playerHealth = GameObject.FindWithTag("Player")?.GetComponent<PlayerHealth>(); 

        // Inicializa valores
        firstCurrent = Mathf.Clamp(firstMaxLife, 0, int.MaxValue);
        secondCurrent = Mathf.Clamp(secondMaxLife, 0, int.MaxValue);

        // Inicializa UI de forma segura
        if (firstLifeSlider != null)
        {
            firstLifeSlider.maxValue = Mathf.Max(1, firstMaxLife);
            firstLifeSlider.minValue = 0;
            firstLifeSlider.value = Mathf.Clamp(firstCurrent, 0, firstMaxLife);
            if (!firstLifeSlider.gameObject.activeSelf) firstLifeSlider.gameObject.SetActive(true);
        }
        if (secondLifeSlider != null)
        {
            secondLifeSlider.maxValue = Mathf.Max(1, secondMaxLife);
            secondLifeSlider.minValue = 0;
            secondLifeSlider.value = Mathf.Clamp(secondCurrent, 0, secondMaxLife);
            // segunda vida empieza desactivada hasta que la primaria se agote
            if (secondLifeSlider.gameObject.activeSelf) secondLifeSlider.gameObject.SetActive(false);
        }

        if (firstFillImage != null)
        {
            firstFillImage.color = Color.blue;
            if (!firstFillImage.gameObject.activeSelf) firstFillImage.gameObject.SetActive(true);
        }
        if (secondFillImage != null)
        {
            secondFillImage.color = Color.red;
            if (secondFillImage.gameObject.activeSelf) secondFillImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Interfaz pública para aplicar daño (compatible con ambos scripts originales).
    /// </summary>
    /// <param name="amount"></param>
    public void TakeDamage(int amount, bool isCritical = false)
    {
        if (amount <= 0) return;
        if (GetTotalLife() <= 0) return; // ya muerto -> ignorar

        int remaining = amount;

        // aplicar a primera vida
        if (firstCurrent > 0)
        {
            int taken = Mathf.Min(firstCurrent, remaining);
            firstCurrent -= taken;
            remaining -= taken;
        }

        // overflow a segunda vida
        if (remaining > 0 && secondCurrent > 0)
        {
            int taken2 = Mathf.Min(secondCurrent, remaining);
            secondCurrent -= taken2;
            remaining -= taken2;
        }

        // actualizar UI
        UpdateSlidersSafely();

        // callback
        OnDamaged?.Invoke();

        // lógica de conteo de golpes rápidos (si está activada)
        if (enableHitRecovery)
        {
            if (Time.time - lastHitTime <= hitCountWindow)
            {
                recentHits++;
            }
            else
            {
                recentHits = 1;
            }
            lastHitTime = Time.time;

            if (!isOverwhelmed && recentHits >= hitThreshold)
            {
                isOverwhelmed = true;
                OnOverwhelmed?.Invoke();
                if (recoveryCoroutine != null) StopCoroutine(recoveryCoroutine);
                recoveryCoroutine = StartCoroutine(WaitForNoHitsThenRecover());
            }
        }

        OnInterrupted?.Invoke();

        // Notificar controller según opciones (mantiene compatibilidad con el EnemyHealth original)
        if (controller != null)
        {
            if (notifyControllerOnDamage)
            {
                // intenta invocar método TakeDamage(int) si existe
                var miTipo = controller.GetType();
                var metodo = miTipo.GetMethod("TakeDamage", new Type[] { typeof(int) });
                if (metodo != null)
                {
                    try
                    {
                        metodo.Invoke(controller, new object[] { amount });
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"HealthController: al invocar controller.TakeDamage ocurrió: {e.Message}");
                    }
                }
                else
                {
                    // fallback a SendMessage si no existe el método
                    controller.SendMessage("OnEnemyDamaged", amount, SendMessageOptions.DontRequireReceiver);
                }
            }
            else
            {
                // notificar sin aplicar daño extra, manteniendo compatibilidad
                controller.SendMessage("OnEnemyDamaged", amount, SendMessageOptions.DontRequireReceiver);
            }
        }

        // muerte si corresponde
        if (GetTotalLife() <= 0)
        {
            Die();
        }

        Debug.Log($"{name} recibió {amount} de daño. Vida1={firstCurrent}/{firstMaxLife} Vida2={secondCurrent}/{secondMaxLife} Total={CurrentHealth}/{MaxHealth}");
    }

    IEnumerator WaitForNoHitsThenRecover()
    {
        while (Time.time - lastHitTime < recoveryNoHitTime)
        {
            yield return null;
        }

        isOverwhelmed = false;
        recentHits = 0;
        OnRecovered?.Invoke();
    }

    void Die()
    {
        // Mantener compatibilidad: algunos prefieren desactivar (player), otros destruir (enemy).
        if (disableOnDeath)
        {
            gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null && healthStealAmount > 0f)
        {
            playerHealth.Heal(healthStealAmount);
            Debug.Log($"El jugador ha robado {healthStealAmount} de vida al enemigo.");
        }
    }

    public bool IsOverwhelmed()
    {
        return isOverwhelmed;
    }

    public int GetTotalLife()
    {
        return Mathf.Max(0, firstCurrent) + Mathf.Max(0, secondCurrent);
    }

    void UpdateSlidersSafely()
    {
        if (firstLifeSlider != null)
        {
            firstLifeSlider.maxValue = Mathf.Max(1, firstMaxLife);
            firstLifeSlider.value = Mathf.Clamp(firstCurrent, 0, firstMaxLife);
            if (!firstLifeSlider.gameObject.activeSelf) firstLifeSlider.gameObject.SetActive(true);
        }
        if (firstFillImage != null)
        {
            if (!firstFillImage.gameObject.activeSelf) firstFillImage.gameObject.SetActive(true);
            firstFillImage.color = Color.blue;
        }

        bool shouldActivateSecond = (firstCurrent <= 0) && (secondMaxLife > 0);
        if (secondLifeSlider != null)
        {
            if (shouldActivateSecond)
            {
                if (!secondLifeSlider.gameObject.activeSelf) secondLifeSlider.gameObject.SetActive(true);
                secondLifeSlider.maxValue = Mathf.Max(1, secondMaxLife);
                secondLifeSlider.value = Mathf.Clamp(secondCurrent, 0, secondMaxLife);
            }
            else
            {
                if (secondLifeSlider.gameObject.activeSelf) secondLifeSlider.gameObject.SetActive(false);
            }
        }

        if (secondFillImage != null)
        {
            if (shouldActivateSecond)
            {
                if (!secondFillImage.gameObject.activeSelf) secondFillImage.gameObject.SetActive(true);
                secondFillImage.color = Color.red;
            }
            else
            {
                if (secondFillImage.gameObject.activeSelf) secondFillImage.gameObject.SetActive(false);
            }
        }
    }

    //// Trigger para detectar objetos con tag "Escudo" (igual que antes)
    //void OnTriggerEnter(Collider other)
    //{
    //    if (other == null) return;
    //    if (other.CompareTag("Escudo"))
    //    {
    //        TakeDamage(escudoDamage);
    //        Debug.Log($"{name} recibió {escudoDamage} de daño por Escudo (3D). Vida total ahora: {GetTotalLife()}");
    //    }
    //}

    // Métodos públicos de utilidad (si necesitas exponerlos a otros scripts)
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        // intenta rellenar firstCurrent primero, luego second
        if (firstCurrent < firstMaxLife)
        {
            int need = firstMaxLife - firstCurrent;
            int toAdd = Mathf.Min(need, amount);
            firstCurrent += toAdd;
            amount -= toAdd;
        }
        if (amount > 0 && secondCurrent < secondMaxLife)
        {
            int need2 = secondMaxLife - secondCurrent;
            int toAdd2 = Mathf.Min(need2, amount);
            secondCurrent += toAdd2;
            amount -= toAdd2;
        }
        UpdateSlidersSafely();
    }

    // Resetea la vida a los máximos
    public void ResetToFull()
    {
        firstCurrent = firstMaxLife;
        secondCurrent = secondMaxLife;
        recentHits = 0;
        isOverwhelmed = false;
        if (recoveryCoroutine != null) { StopCoroutine(recoveryCoroutine); recoveryCoroutine = null; }
        UpdateSlidersSafely();
    }
}
