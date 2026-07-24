using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Este script controla el indicador direccional al usar el mando, mostrandolo debajo del personaje o desactivandolo cuando se usa el
/// teclado + mouse
/// </summary>
public class JoystickDirectionalSpriteCtrl : MonoBehaviour
{
    #region References

    [Header ("References")]
    [SerializeField] GameObject directionalObject;
    [SerializeField] Transform directionalTransform;

    #endregion

    #region Inspector - Settings

    [Header("Directional Sprite Settings")]
    [SerializeField] private float deadzone = 0.2f;
    [SerializeField] private float rotationSmoothSpeed = 20f;
    [SerializeField] private float angleOffset = 0f;
    [SerializeField] private float timeToVanish = 1f;

    #endregion

    #region Internal Variables

    SpriteRenderer r;
    private bool lastInputWasGamepad;
    private float currentAngle;
    private float targetAngle;
    private bool hasValidDirection;
    private float baseEulerX;
    private float baseEulerY;
    private Coroutine delayToVanishRoutine;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (directionalTransform == null) directionalTransform = directionalObject.transform;

        baseEulerX = directionalTransform.localEulerAngles.x;
        baseEulerY = directionalTransform.localEulerAngles.y;

        r = directionalObject.GetComponent<SpriteRenderer>();

    }

    void Update()
    {
        DetectLastInput();

        Vector2 stick = Gamepad.current != null? Gamepad.current.leftStick.ReadValue() : Vector2.zero;
        hasValidDirection = stick.magnitude >= deadzone;

        bool shouldShow = lastInputWasGamepad && hasValidDirection;
        HandleVisiblity(shouldShow);

        if (!shouldShow) return;

        targetAngle = Mathf.Atan2(stick.x, stick. y) * Mathf.Rad2Deg;
        targetAngle += angleOffset;

        currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime));

        directionalTransform.rotation = Quaternion.Euler(baseEulerX, baseEulerY, currentAngle);
    }

    #endregion

    #region Internal Methods

    private void DetectLastInput()
    {
        if (Gamepad.current != null && GamepadHasInput(Gamepad.current))
        {
            lastInputWasGamepad = true;
        }
        else if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) || 
            (Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f || Mouse.current.leftButton.wasPressedThisFrame ||
            Mouse.current.rightButton.wasPressedThisFrame)))
        {
            lastInputWasGamepad = false;
        }
    }

    private void HandleVisiblity(bool shouldShow)
    {
        if (shouldShow)
        {
            if (delayToVanishRoutine != null)
            {
                StopCoroutine(delayToVanishRoutine);
                delayToVanishRoutine = null;
            }
            SetDirectionalVisible(true);
            r.color = Color.white;
        }
        else if (directionalObject != null && directionalObject.activeSelf && delayToVanishRoutine == null)
        {
            delayToVanishRoutine = StartCoroutine(DelayDeactivateDirectional(timeToVanish));
        }
    }

    private bool GamepadHasInput(Gamepad gamepad)
    {
        if (gamepad.leftStick.ReadValue().magnitude >= deadzone) return true;

        foreach (var control in gamepad.allControls)
        {
            if (control is ButtonControl button && button.wasPressedThisFrame) return true;
        }
        return false;
    }

    private void SetDirectionalVisible(bool value)
    {
        if (directionalObject != null && directionalObject.activeSelf != value)
        {
            directionalObject.SetActive(value);
        }
    }

    private IEnumerator DelayDeactivateDirectional(float duration)
    {
        float elapse = 0;

        while (elapse < duration)
        {
            elapse += Time.deltaTime;
            float t = elapse/duration;
            float alpha = Mathf.Lerp(1, 0, t);
            r.color = new Color(r.color.r, r.color.g, r.color.b, alpha);

            yield return null;
        }

        r.color = new Color(r.color.r, r.color.g, r.color.b, 0);
        SetDirectionalVisible(false);
        delayToVanishRoutine = null;
    }

    #endregion
}
