using UnityEngine;
using DG.Tweening;

public class ObjectAnimationController : MonoBehaviour
{
    #region Configuration
    [Header("Visibility State")]
    [SerializeField] private bool isSelectedOrConnected = false;

    [Header("Rotation Activation")]
    [SerializeField] private bool rotateX = false;
    [SerializeField] private bool rotateY = true;
    [SerializeField] private bool rotateZ = false;

    [Header("Rotation Settings")]
    [HideInInspector, SerializeField] private float speedX = 30f;
    [HideInInspector, SerializeField] private float speedY = 30f;
    [HideInInspector, SerializeField] private float speedZ = 30f;
    [HideInInspector, SerializeField] private bool directionX = true;
    [HideInInspector, SerializeField] private bool directionY = true;
    [HideInInspector, SerializeField] private bool directionZ = true;
    #endregion

    #region Internal State
    private Vector3 _initialScale;
    private bool _currentVisibilityState;
    private Renderer _objectRenderer;
    private Collider _objectCollider;
    #endregion

    #region Core Logic
    void Awake()
    {
        _initialScale = transform.localScale;
        _objectRenderer = GetComponent<Renderer>();
        _objectCollider = GetComponent<Collider>();
        SetVisibilityAndScale(isSelectedOrConnected);
    }

    void Update()
    {
        ApplyRotation();

        if (_currentVisibilityState != isSelectedOrConnected)
        {
            SetVisibilityAndScale(isSelectedOrConnected);
        }
    }

    private void ApplyRotation()
    {
        float currentSpeedX = rotateX ? speedX * (directionX ? 1f : -1f) : 0f;
        float currentSpeedY = rotateY ? speedY * (directionY ? 1f : -1f) : 0f;
        float currentSpeedZ = rotateZ ? speedZ * (directionZ ? 1f : -1f) : 0f;

        Vector3 rotationSpeed = new Vector3(currentSpeedX, currentSpeedY, currentSpeedZ);

        transform.Rotate(rotationSpeed * Time.deltaTime);
    }

    private void SetVisibilityAndScale(bool shouldBeVisible)
    {
        _currentVisibilityState = shouldBeVisible;

        if (_objectRenderer != null) _objectRenderer.enabled = shouldBeVisible;
        if (_objectCollider != null) _objectCollider.enabled = shouldBeVisible;

        transform.DOKill(true);
    }

    public void ToggleConnectionState(bool newState)
    {
        isSelectedOrConnected = newState;
    }
    #endregion
}