using UnityEngine;
using System.Collections;

/// <summary>
/// Gestor de audio del sistema de inventario.
/// </summary>
public class InventoryAudioManager : MonoBehaviour
{
    #region Public Properties & Events

    public static InventoryAudioManager Instance { get; private set; }

    #endregion

    #region Inspector - References

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("Source dedicada a la musica de inventario")]
    [SerializeField] private AudioSource musicSource;
    [Tooltip("Source para sonidos de ambiente superpuestos sobre la musica de inventario")]
    [SerializeField] private AudioSource ambientSource;
    [Tooltip("Source dedicada a la musica principal del nivel")]
    [SerializeField] private AudioSource levelMusicSource;

    #endregion

    #region Inspector - Sound Effects (Clips)

    [Header("Apertura / Cierre")]
    [SerializeField] private AudioClip inventoryOpenSound;
    [SerializeField] private AudioClip inventoryCloseSound;

    [Header("Sonidos de Animacion de Barras")]
    [SerializeField] private AudioClip barsExpandSound;
    [SerializeField] private AudioClip barsRetractSound;

    [Header("Hover de Slots")]
    [SerializeField] private AudioClip goldenSlotHoverSound;
    [SerializeField] private AudioClip commonSlotHoverSound;

    [Header("Interaccion")]
    [SerializeField] private AudioClip itemClickSound;
    [SerializeField] private AudioClip itemAddedSound;
    [Tooltip("Sonido al cambiar seleccion entre slots dorados")]
    [SerializeField] private AudioClip switchGoldenSlotSound;

    [Header("Sonidos por Rareza")]
    [SerializeField] private AudioClip normalSound;
    [SerializeField] private AudioClip rareSound;
    [SerializeField] private AudioClip superRareSound;

    #endregion

    #region Inspector - Music Settings

    [Header("Musica de Inventario")]
    [SerializeField] private AudioClip inventoryMusic;
    [SerializeField][Range(0f, 1f)] private float musicVolume = 0.4f;

    [Header("Musica del Nivel")]
    [Tooltip("Velocidad del fade de ducking en segundos")]
    [SerializeField] private float duckFadeSpeed = 0.5f;
    [Tooltip("Volumen al que baja la musica del nivel cuando el inventario esta abierto")]
    [SerializeField][Range(0f, 1f)] private float levelMusicDuckVolume = 0.2f;

    [Header("Musica de Ambiente")]
    [SerializeField] private AudioClip inventoryAmbientLoop;
    [SerializeField][Range(0f, 1f)] private float ambientVolume = 0.3f;

    #endregion

    #region Inspector - Volume Settings

    [Header("Volumenes SFX")]
    [SerializeField][Range(0f, 1f)] private float hoverVolume = 0.5f;
    [SerializeField][Range(0f, 1f)] private float clickVolume = 0.7f;
    [SerializeField][Range(0f, 1f)] private float rarityVolume = 0.8f;
    [SerializeField][Range(0f, 1f)] private float addedVolume = 0.9f;

    #endregion

    #region Internal State

    private float originalLevelMusicVolume;
    private Coroutine duckCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    #endregion

    #region Audio Playback API

    public void DuckLevelMusic()
    {
        if (levelMusicSource == null) return;
        originalLevelMusicVolume = levelMusicSource.volume;
        if (duckCoroutine != null) StopCoroutine(duckCoroutine);
        duckCoroutine = StartCoroutine(FadeLevelMusic(targetVolume: levelMusicDuckVolume));
    }

    public void RestoreLevelMusic()
    {
        if (levelMusicSource == null) return;
        if (duckCoroutine != null) StopCoroutine(duckCoroutine);
        duckCoroutine = StartCoroutine(FadeLevelMusic(targetVolume: originalLevelMusicVolume));
    }

    private IEnumerator FadeLevelMusic(float targetVolume)
    {
        float startVolume = levelMusicSource.volume;
        float elapsed = 0f;
        while (elapsed < duckFadeSpeed)
        {
            elapsed += Time.unscaledDeltaTime;
            levelMusicSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duckFadeSpeed);
            yield return null;
        }
        levelMusicSource.volume = targetVolume;
        duckCoroutine = null;
    }

    public void PlayOpenSound()
    {
        PlaySfx(inventoryOpenSound, 1f);
        PlayMusic();
        DuckLevelMusic();
        PlayAmbientLoop();
    }

    public void PlayCloseSound()
    {
        PlaySfx(inventoryCloseSound, 1f);
        StopMusic();
        RestoreLevelMusic();
        StopAmbientLoop();
    }

    public void PlayBarsExpandSound() => PlaySfx(barsExpandSound, 1f);
    public void PlayBarsRetractSound() => PlaySfx(barsRetractSound, 1f);
    public void PlayGoldenSlotHoverSound() => PlaySfx(goldenSlotHoverSound, hoverVolume);
    public void PlayCommonSlotHoverSound() => PlaySfx(commonSlotHoverSound, hoverVolume);
    public void PlaySwitchGoldenSlotSound() => PlaySfx(switchGoldenSlotSound, hoverVolume);

    public void PlayClickSound() => PlaySfx(itemClickSound, clickVolume);
    public void PlayItemAddedSound() => PlaySfx(itemAddedSound, addedVolume);

    public void PlayRaritySound(ItemRarity rarity)
    {
        AudioClip clip = rarity switch
        {
            ItemRarity.Raro => rareSound,
            ItemRarity.SuperRaro => superRareSound,
            _ => normalSound
        };
        PlaySfx(clip, rarityVolume);
    }

    #endregion

    #region Internal Audio Logic

    private void PlayMusic()
    {
        if (musicSource == null || inventoryMusic == null) return;
        musicSource.clip = inventoryMusic;
        musicSource.volume = musicVolume;
        ambientSource.loop = true;
        musicSource.Play();
    }

    private void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying) musicSource.Stop();
    }

    public void PlayAmbientLoop()
    {
        if (ambientSource == null || inventoryAmbientLoop == null) return;
        ambientSource.clip = inventoryAmbientLoop;
        ambientSource.volume = ambientVolume;
        ambientSource.loop = true;
        ambientSource.Play();
    }

    public void StopAmbientLoop()
    {
        if (ambientSource != null && ambientSource.isPlaying) ambientSource.Stop();
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    #endregion
}