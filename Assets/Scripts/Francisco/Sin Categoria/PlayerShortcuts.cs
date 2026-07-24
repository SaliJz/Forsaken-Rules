using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShortcuts : MonoBehaviour
{
    [Header("Shortcut Configuration")]
    [SerializeField] private KeyCode healKey = KeyCode.H;
    [SerializeField] private KeyCode damageKey = KeyCode.J;
    [SerializeField] private KeyCode maxHealthKey = KeyCode.K;

    [Header("Modification Values")]
    [SerializeField] private float healAmount = 20f;
    [SerializeField] private float damageAmount = 10f;

    [Header("Gamepad Settings")]
    [SerializeField, Range(0.1f, 1f)] private float triggerThreshold = 0.5f;

    private PlayerHealth playerHealth;
    private Gamepad pad;

    private bool healComboWasPressed;
    private bool damageComboWasPressed;
    private bool maxHealthComboWasPressed;

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
        RefreshGamepad();
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            Debug.LogError("[PlayerShortcuts] No se encontró PlayerHealth en este GameObject.");
            enabled = false;
        }
    }

    private void Update()
    {
        HandleKeyboard();
        HandleGamepad();
    }

    private void HandleKeyboard()
    {
        if (Input.GetKeyDown(healKey)) ExecuteHeal();
        if (Input.GetKeyDown(damageKey)) ExecuteDamage();
        if (Input.GetKeyDown(maxHealthKey)) ExecuteMaxHealth();
    }

    private void HandleGamepad()
    {
        if (pad == null) return;

        bool r1 = pad.rightShoulder.isPressed;
        bool xBtn = pad.buttonSouth.isPressed;

        Vector2 dpad = pad.dpad.ReadValue();

        bool healCombo = dpad.y > 0.5f && r1 && xBtn;   // Flecha arriba + R1 + X
        bool damageCombo = dpad.y < -0.5f && r1 && xBtn;   // Flecha abajo + R1 + X
        bool maxHealthCombo = dpad.x < -0.5f && r1 && xBtn;   // Flecha izquierda + R1 + X

        EvaluateCombo(healCombo, ref healComboWasPressed, ExecuteHeal);
        EvaluateCombo(damageCombo, ref damageComboWasPressed, ExecuteDamage);
        EvaluateCombo(maxHealthCombo, ref maxHealthComboWasPressed, ExecuteMaxHealth);
    }

    /// Dispara <paramref name="action"/> solo en el primer frame en que el combo está activo.
    private static void EvaluateCombo(bool isPressed, ref bool wasPressed, System.Action action)
    {
        if (isPressed && !wasPressed) action?.Invoke();
        wasPressed = isPressed;
    }

    private void ExecuteHeal()
    {
        if (!IsHealthValid()) return;
        playerHealth.Heal(healAmount);
        Debug.Log($"<color=green>[DEBUG] Jugador curado +{healAmount} vida. " +
                  $"Vida actual: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}</color>");
    }

    private void ExecuteDamage()
    {
        if (!IsHealthValid()) return;
        playerHealth.TakeDamage(damageAmount, true);
        Debug.Log($"<color=yellow>[DEBUG] Jugador dañado -{damageAmount} vida. " +
                  $"Vida actual: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}</color>");
    }

    private void ExecuteMaxHealth()
    {
        if (!IsHealthValid()) return;

        float missing = playerHealth.MaxHealth - playerHealth.CurrentHealth;
        if (missing > 0)
        {
            playerHealth.Heal(missing);
            Debug.Log($"<color=cyan>[DEBUG] Vida restaurada al máximo: {playerHealth.MaxHealth}</color>");
        }
        else
        {
            Debug.Log($"<color=cyan>[DEBUG] La vida ya está al máximo: {playerHealth.MaxHealth}</color>");
        }
    }

    private bool IsHealthValid()
    {
        if (playerHealth != null) return true;
        Debug.LogWarning("[PlayerShortcuts] PlayerHealth es null. Acción ignorada.");
        return false;
    }

    private void RefreshGamepad() => pad = Gamepad.current;

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad) return;

        if (change is InputDeviceChange.Added or InputDeviceChange.Reconnected)
        {
            pad = device as Gamepad;
            Debug.Log("[PlayerShortcuts] Mando conectado.");
        }
        else if (change is InputDeviceChange.Removed or InputDeviceChange.Disconnected)
        {
            if (pad == device) pad = Gamepad.current;
            Debug.Log("[PlayerShortcuts] Mando desconectado.");
        }
    }
}