using System.Collections;
using UnityEngine;

public class SequenceTransition : TransitionInteractive
{
    #region Estado

    private bool isSequenceRunning = false;
    private AudioSource sequenceAudioSource;

    #endregion

    #region Audio

    [Header("Audio")]
    [SerializeField] private AudioClip sequenceClip;
    [SerializeField] private float sequenceVolume = 1f;
    [SerializeField] private bool loopSequenceClip = true;

    #endregion

    #region API P�blica

    public bool IsSequenceRunning => isSequenceRunning;

    public IEnumerator ExecuteSequence(Transform playerTransform)
    {
        isSequenceRunning = true;
        PlaySequenceAudio();

        yield return StartCoroutine(RunNodes(playerTransform));

        StopSequenceAudio();
        isSequenceRunning = false;
    }

    #endregion

    #region Audio Methods

    private void PlaySequenceAudio()
    {
        if (sequenceClip == null)
        {
            return;
        }

        if (sequenceAudioSource == null)
        {
            sequenceAudioSource = GetComponent<AudioSource>();
            if (sequenceAudioSource == null)
            {
                sequenceAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        sequenceAudioSource.playOnAwake = false;
        sequenceAudioSource.spatialBlend = 0f;
        sequenceAudioSource.loop = loopSequenceClip;
        sequenceAudioSource.clip = sequenceClip;
        sequenceAudioSource.volume = Mathf.Clamp01(sequenceVolume);
        sequenceAudioSource.Play();
    }

    private void StopSequenceAudio()
    {
        if (sequenceAudioSource != null && sequenceAudioSource.isPlaying)
        {
            sequenceAudioSource.Stop();
        }
    }

    #endregion
}