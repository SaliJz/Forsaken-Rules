using UnityEngine;

public class FleshPulseNerve : MonoBehaviour
{
    [SerializeField] private LayerMask _obstacleLayer;
    [SerializeField] private LayerMask _playerLayer;

    [SerializeField] private bool _simulateBasePivot = true;

    private FleshPulseController _controller;

    private float _currentLength = 0f;
    private bool _hitWall = false;
    private Vector3 _initialScale;
    private Vector3 _initialLocalPosition;

    public void Initialize(FleshPulseController controller, LayerMask obstacleMask, LayerMask playerMask)
    {
        _controller = controller;
        _obstacleLayer = obstacleMask;
        _playerLayer = playerMask;

        _initialScale = transform.localScale;
        _initialLocalPosition = transform.localPosition;

        transform.localScale = new Vector3(_initialScale.x, 0f, _initialScale.z);
    }

    public void Expand(float growthAmount, float maxRadius)
    {
        if (_hitWall) return;

        float nextLength = _currentLength + growthAmount;

        Vector3 rayOrigin = _controller.transform.position + (Vector3.up * 1.0f);

        Vector3 rayDirection = transform.up;

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, nextLength, _obstacleLayer))
        {
            if (hit.distance < 1.0f)
            {
                _currentLength = Mathf.Min(nextLength, maxRadius);
            }
            else
            {
                _currentLength = hit.distance;
                _hitWall = true;
            }
        }
        else
        {
            _currentLength = Mathf.Min(nextLength, maxRadius);
            if (_currentLength >= maxRadius) _hitWall = true;
        }

        transform.localScale = new Vector3(_initialScale.x, _currentLength, _initialScale.z);

        if (_simulateBasePivot)
        {
            float offset = _currentLength * 0.5f;
            transform.localPosition = _initialLocalPosition + (Vector3.up * offset);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & _playerLayer) == 0) return;

        if (_controller == null) return;

        GameObject target = other.gameObject;

        if (IsBlockedByWall(target.transform.position))
        {
            Debug.Log("[FleshPulseNerve] Golpe bloqueado por pared.");
            return;
        }

        _controller.TryHitPlayer(target);
    }

    private bool IsBlockedByWall(Vector3 targetPosition)
    {
        Vector3 origin = _controller.transform.position + Vector3.up * 1.0f;
        Vector3 targetCenter = targetPosition + Vector3.up * 1.0f;

        if (Physics.Linecast(origin, targetCenter, out RaycastHit hit, _obstacleLayer))
        {
            return true;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        if (_controller == null && transform.parent == null) return;

        Transform centerT = _controller != null ? _controller.transform : transform.parent;
        if (centerT == null) return;

        Vector3 start = centerT.position + Vector3.up * 1.0f;
        Vector3 dir = transform.up;
        float dist = transform.localScale.y;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(start, dir * dist);
        Gizmos.DrawWireSphere(start + (dir * dist), 0.2f);
    }
}