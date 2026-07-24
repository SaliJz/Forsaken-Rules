using UnityEngine;
using DG.Tweening;
using UnityEngine.Events;

public class TweenMovementController : MonoBehaviour
{
    #region Configuration Fields
    [Header("Movement Settings")]
    [SerializeField] private Vector3 _endPosition = new Vector3(5f, 0f, 0f);
    [SerializeField] private float _duration = 2.0f;
    [SerializeField] private Ease _easeType = Ease.InOutQuad;
    [SerializeField] private bool _isLocal = true;
    [SerializeField] private bool _startOnAwake = false;
    [SerializeField] private bool _faceDirection = false;

    [Header("Prefab Support")]
    [SerializeField] private bool _relativeToStart = false;

    [Header("Events")]
    [SerializeField] private UnityEvent OnMovementStart;
    [SerializeField] private UnityEvent OnMovementFinished;

    [Header("Gizmos")]
    [SerializeField] private Color _gizmoColor = Color.yellow;
    [SerializeField] private float _arrowLength = 0.5f;
    [SerializeField] private float _gizmoLineThickness = 4f;
    #endregion

    #region Private Variables
    private Vector3 _startPosition;
    #endregion

    #region Unity Lifecycle Methods
    private void Awake()
    {
        _startPosition = _isLocal ? transform.localPosition : transform.position;

        if (_startOnAwake)
        {
            StartMovement();
        }
    }
    #endregion

    #region Public
    public void StartMovement()
    {
        transform.DOKill(true);

        OnMovementStart?.Invoke();

        Vector3 currentPosition = _isLocal ? transform.localPosition : transform.position;
        Vector3 targetPosition = _relativeToStart ? currentPosition + _endPosition : _endPosition;

        Tween myTween;

        if (_isLocal)
        {
            myTween = transform.DOLocalMove(targetPosition, _duration).SetEase(_easeType);
        }
        else
        {
            myTween = transform.DOMove(targetPosition, _duration).SetEase(_easeType);
        }

        if (_faceDirection)
        {
            Vector3 direction = (targetPosition - currentPosition).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.DORotate(new Vector3(0, 0, angle), _duration).SetEase(_easeType);
        }

        myTween.OnComplete(() =>
        {
            _startPosition = _isLocal ? transform.localPosition : transform.position;
            OnMovementFinished?.Invoke();
        });
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmos()
    {
        if (transform == null) return;

        Vector3 gizmoStartPosition = transform.position;
        Vector3 gizmoEndPosition;

        if (_relativeToStart)
        {
            if (_isLocal)
            {
                Vector3 localTarget = transform.localPosition + _endPosition;
                gizmoEndPosition = transform.parent != null
                    ? transform.parent.TransformPoint(localTarget)
                    : localTarget;
            }
            else
            {
                gizmoEndPosition = transform.position + _endPosition;
            }
        }
        else
        {
            if (_isLocal && transform.parent != null)
            {
                gizmoEndPosition = transform.parent.TransformPoint(_endPosition);
            }
            else
            {
                gizmoEndPosition = _endPosition;
            }
        }

        Gizmos.color = _gizmoColor;
        Vector3 direction = (gizmoEndPosition - gizmoStartPosition);

        for (int i = 0; i < _gizmoLineThickness; i++)
        {
            Vector3 offset = Vector3.Cross(direction, Vector3.forward).normalized * (i / 50f);
            Gizmos.DrawLine(gizmoStartPosition + offset, gizmoEndPosition + offset);
        }

        Gizmos.DrawSphere(gizmoEndPosition, 0.1f);
        DrawArrow2D(gizmoEndPosition, direction.normalized, _gizmoColor, _arrowLength);
        Gizmos.color = Color.white;
    }

    private void DrawArrow2D(Vector3 position, Vector3 direction, Color color, float length)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0, 0, angle);
        Vector3 rightWing = rotation * Quaternion.Euler(0, 0, -150) * Vector3.right * length;
        Vector3 leftWing = rotation * Quaternion.Euler(0, 0, 150) * Vector3.right * length;

        Gizmos.color = color;
        Gizmos.DrawLine(position, position + rightWing);
        Gizmos.DrawLine(position, position + leftWing);
    }
    #endregion
}