using UnityEngine;

public class AudioClipChanger : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private AudioSource audioSource;

    private AudioClip defaultClip;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            defaultClip = audioSource.clip;
        }
        else
        {
            Debug.LogError("AudioSource no encontrado en el objeto de juego. Asegúrate de adjuntar uno.");
        }
    }

    public void ReplaceAudioClip(AudioClip newClip)
    {
        if (audioSource == null) return;

        if (newClip != null)
        {
            audioSource.clip = newClip;
        }
        else
        {
            Debug.LogWarning("El nuevo AudioClip proporcionado es nulo.");
        }
    }

    public void ReplaceAndPlayAudioClip(AudioClip newClip)
    {
        if (audioSource == null) return;

        ReplaceAudioClip(newClip);

        if (audioSource.clip != null)
        {
            audioSource.Play();
        }
    }

    public void ReturnToDefaultClip()
    {
        if (audioSource == null) return;

        audioSource.clip = defaultClip;
    }

    public void ReturnToDefaultAndPlay()
    {
        if (audioSource == null) return;

        ReturnToDefaultClip();

        if (audioSource.clip != null)
        {
            audioSource.Play();
        }
    }

    public void PlayAudio()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Play();
        }
    }

    public void StopAudio()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }
}