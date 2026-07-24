using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.VisualScripting.Member;

public enum MusicState
{
    Calm,
    Battle,
    Shop,
    Boss
}

public class AsyncMusicController : MonoBehaviour
{
    public static AsyncMusicController Instance { get; private set; }

    [Header("Fuentes de Audio")]
    public AudioSource calmSource;
    public AudioSource battleSource;
    public AudioSource shopSource;
    public AudioSource bossSource;

    [Header("Configuración")]
    [Range(0.1f, 5f)]
    public float fadeDuration = 2.0f;

    private MusicState currentState = MusicState.Calm;
    private Coroutine currentTransition;
    private Dictionary<MusicState, AudioSource> audioSources;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        audioSources = new Dictionary<MusicState, AudioSource>
        {
            { MusicState.Calm, calmSource },
            { MusicState.Battle, battleSource },
            { MusicState.Shop, shopSource },
            { MusicState.Boss, bossSource }
        };
    }

    private void Start()
    {
        foreach (var source in audioSources.Values)
        {
            if (source != null)
            {
                source.loop = true;
                source.playOnAwake = false;
                source.volume = 0f;
                source.Stop();
            }
        }

        if (calmSource != null)
        {
            calmSource.volume = 1f;
            calmSource.Play();
        }
    }

    public void PlayMusic(MusicState newState)
    {
        if (currentState == newState) return;

        currentState = newState;

        if (currentTransition != null) StopCoroutine(currentTransition);
        currentTransition = StartCoroutine(CrossfadeMusic(newState));
    }

    private IEnumerator CrossfadeMusic(MusicState targetState)
    {
        float timer = 0f;

        AudioSource targetSource = audioSources.ContainsKey(targetState) ? audioSources[targetState] : null;

        if (targetSource != null)
        {
            targetSource.volume = 0f;
            targetSource.time = 0f;
            targetSource.Play();
        }

        Dictionary<AudioSource, float> startVolumes = new Dictionary<AudioSource, float>();

        foreach (var kvp in audioSources)
        {
            if (kvp.Value != null && kvp.Key != targetState && kvp.Value.isPlaying)
            {
                startVolumes[kvp.Value] = kvp.Value.volume;
            }
        }

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fadeDuration;

            if (targetSource != null)
            {
                targetSource.volume = Mathf.Lerp(0f, 1f, t);
            }

            foreach (var kvp in startVolumes)
            {
                AudioSource sourceOut = kvp.Key;
                float startVol = kvp.Value;
                sourceOut.volume = Mathf.Lerp(startVol, 0f, t);
            }

            yield return null;
        }

        if (targetSource != null) targetSource.volume = 1f;

        foreach (var kvp in audioSources)
        {
            if (kvp.Key != targetState && kvp.Value != null)
            {
                kvp.Value.volume = 0f;
                kvp.Value.Stop();
            }
        }
    }
}