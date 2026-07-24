using UnityEngine;
using UnityEngine.EventSystems;

public class UIAudioFeedback : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    [SerializeField] private AudioSource uiAudioSource;
    [Header("Sonido")]
    [SerializeField] private AudioClip selectionClip;

    private void Start()
    {
        if (uiAudioSource == null)
        {
            uiAudioSource = FindAnyObjectByType<AudioSource>();
            if (uiAudioSource == null)
            {
                Debug.LogWarning("No se encontró AudioSource para UIAudioFeedback. El feedback de audio no funcionará.");
            }
        }
    }

    private void PlaySelectionSound()
    {
        if (uiAudioSource != null && selectionClip != null)
        {
            uiAudioSource.PlayOneShot(selectionClip);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (GamepadPointer.Instance != null)
        {
            var currentDevice = GamepadPointer.Instance.GetCurrentActiveDevice();
            bool isGamepad = (currentDevice == GamepadPointer.Instance.GetCurrentGamepad());

            if (isGamepad)
            {
                return;
            }
        }

        if (EventSystem.current.currentSelectedGameObject != gameObject)
        {
            PlaySelectionSound();
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (GamepadPointer.Instance != null)
        {
            var currentDevice = GamepadPointer.Instance.GetCurrentActiveDevice();
            bool isGamepad = (currentDevice == GamepadPointer.Instance.GetCurrentGamepad());

            if (!isGamepad)
            {
                return; 
            }
        }

        PlaySelectionSound();
    }
}