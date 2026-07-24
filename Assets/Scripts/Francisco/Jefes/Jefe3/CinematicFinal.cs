using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using static BaseAnimCtrl<PlayerAnimCtrl.PlayerState>;
using static PlayerAnimCtrl;

public class CinematicFinal : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera _camera;

    [Header("Settings")]
    [SerializeField] private float _targetFOV = 5f;
    [SerializeField] private float _zoomSpeed = 10f;
    [SerializeField] private float _postZoomDelay = 1f;
    [SerializeField] private UnityEvent _onZoom;

    private bool m_CinematicStart = false;

    private void Awake()
    {
        if (_camera == null)
        {
            _camera = FindAnyObjectByType<CinemachineCamera>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_CinematicStart) return;

        if (other.CompareTag("Player"))
        {
            StartCoroutine(PreviewCinematic(other.transform));
        }
    }

    private IEnumerator PreviewCinematic(Transform playerTransform)
    {
        m_CinematicStart = true;

        GameObject.Find("BarContainer").SetActive(false);
        GameObject.Find("InventoryManager").SetActive(false);

        PlayerAnimCtrl playerAnimator = playerTransform.GetComponentInChildren<PlayerAnimCtrl>();

        if (playerAnimator != null)
        {
            playerAnimator.SetInputAxes(0f, 0f);

            playerAnimator.PlayState(PlayerState.idle, AnimPriority.dash, true);
        }

        DisablePlayerControls(playerTransform, false);

        yield return new WaitForSeconds(_postZoomDelay);

        _onZoom?.Invoke();

        if (_camera != null)
        {
            float currentFOV = _camera.Lens.FieldOfView;

            while (Mathf.Abs(currentFOV - _targetFOV) > 0.05f)
            {
                currentFOV = Mathf.MoveTowards(currentFOV, _targetFOV, _zoomSpeed * Time.deltaTime);
                _camera.Lens.FieldOfView = currentFOV;

                yield return null;
            }

            _camera.Lens.FieldOfView = _targetFOV;
        }
    }

    private void DisablePlayerControls(Transform playerTransform, bool state)
    {
        if (playerTransform == null) return;

        MonoBehaviour[] scripts = playerTransform.GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script == null || script == this)
                continue;
            if (script is PlayerAnimCtrl || script.GetType().Namespace?.StartsWith("UnityEngine") == true)
                continue;

            string scriptName = script.GetType().Name;
            if (scriptName.Contains("Cinemachine") || scriptName.Contains("Feedback") || scriptName.Contains("Distortion"))
                continue;

            script.enabled = state;
        }

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = state;
        }
    }
}