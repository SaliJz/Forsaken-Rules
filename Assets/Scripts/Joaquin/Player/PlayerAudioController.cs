using UnityEngine;

public class PlayerAudioController : MonoBehaviour
{
    #region Inspector - References

    [Header("References")]
    [SerializeField] private AudioSource mainAudioSource;
    [SerializeField] private AudioSource footstepsAudioSource;

    #endregion

    #region Inspector - Damage SFX Configuration

    [Header("Damage SFX Configuration")]
    [SerializeField] private AudioClip damageClip;
    [Range(0f, 1f)][SerializeField] private float damageVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float damagePitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool useDamagePitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float damagePitchVariance = 0.1f;

    #endregion

    #region Inspector - Death SFX Configuration

    [Header("Death SFX Configuration")]
    [SerializeField] private AudioClip deathClip;
    [Range(0f, 1f)][SerializeField] private float deathVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float deathPitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool useDeathPitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float deathPitchVariance = 0.1f;

    #endregion

    #region Inspector - Step SFX Configuration

    [Header("Step SFX Configuration")]
    [SerializeField] private AudioClip footstepGenericClip;
    [SerializeField] private AudioClip footstepNivel1Clip;
    [SerializeField] private AudioClip footstepNivel2Clip;
    [SerializeField] private AudioClip footstepNivel3Clip;
    [Range(0f, 1f)][SerializeField] private float stepVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float stepPitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool useStepPitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float stepPitchVariance = 0.1f;

    #endregion

    #region Inspector - Dash SFX Configuration

    [Header("Dash SFX Configuration")]
    [SerializeField] private AudioClip dashClip;
    [Range(0f, 1f)][SerializeField] private float dashVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float dashPitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool usePitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float pitchVariance = 0.1f;

    #endregion

    #region Inspector - Melee SFX Configuration

    [Header("Melee SFX Configuration")]
    [SerializeField] private AudioClip basicSlashClip;
    [SerializeField] private AudioClip spinSlashClip;
    [SerializeField] private AudioClip heavySlashClip;
    [Range(0f, 1f)][SerializeField] private float meleeVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float meleePitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool useMeleePitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float meleePitchVariance = 0.1f;

    #endregion

    #region Inspector - Hit SFX Configuration

    [Header("Hit SFX Configuration")]
    [SerializeField] private AudioClip hitClip;
    [Range(0f, 1f)][SerializeField] private float hitVolume = 0.85f;
    [Range(0f, 2f)][SerializeField] private float hitPitch = 1.0f;
    [Header("Hit Variance")]
    [SerializeField] private bool useHitPitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float hitPitchVariance = 0.1f;

    #endregion

    #region Inspector - Range SFX Configuration

    [Header("Range SFX Configuration")]
    [SerializeField] private AudioClip throwShieldClip;
    [SerializeField] private AudioClip catchShieldClip;
    [SerializeField] private AudioClip throwShieldBerserkerClip;
    [SerializeField] private AudioClip catchShieldBerserkerClip;
    [Range(0f, 1f)][SerializeField] private float rangeVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float rangePitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool useRangePitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float rangePitchVariance = 0.1f;

    #endregion

    #region Inspector - Block SFX Configuration

    [Header("Block SFX Configuration")]
    [SerializeField] private AudioClip activeBlockClip;
    [SerializeField] private AudioClip blockHitClip;
    [SerializeField] private AudioClip blockBreakClip;
    [Range(0f, 1f)][SerializeField] private float blockVolume = 0.8f;
    [Range(0f, 1f)][SerializeField] private float blockPitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool useblockPitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float blockPitchVariance = 0.1f;

    #endregion

    #region Inspector - Berserker SFX Configuration

    [Header("Berserker SFX Configuration")]
    [SerializeField] private AudioClip activeAbilityClip;
    [SerializeField] private AudioClip desactiveAbilityClip;
    [SerializeField] private AudioClip lowHealthWarningSound;
    [SerializeField] private AudioClip lifeAbsorbClip;
    [Range(0f, 1f)][SerializeField] private float abilityVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float abilityPitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool useabilityPitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float abilityPitchVariance = 0.1f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (mainAudioSource == null)
        {
            mainAudioSource = GetComponentInChildren<AudioSource>();
            if (mainAudioSource == null) Debug.LogError($"{name}: No se encontro AudioSource hijo.");
        }

        if (footstepsAudioSource == null)
        {
            footstepsAudioSource = GetComponentInChildren<AudioSource>();
            if (footstepsAudioSource == null) Debug.LogError($"{name}: No se encontro AudioSource hijo.");
        }
    }

    #endregion

    #region Status & Combat Audio Methods

    public void PlayDamageSound()
    {
        PlayOneShotInternal(damageClip, damageVolume, damagePitch, useDamagePitchVariance, damagePitchVariance);
    }

    public void PlayDeathSound()
    {
        PlayOneShotInternal(deathClip, deathVolume, deathPitch, useDeathPitchVariance, deathPitchVariance);
    }

    public void PlayHitSound()
    {
        PlayOneShotInternal(hitClip, hitVolume, hitPitch, useHitPitchVariance, hitPitchVariance);
    }

    #endregion

    #region Movement Audio Methods

    public void PlayStepSound(int nivel)
    {
        AudioClip clipToPlay = null;
        switch (nivel)
        {
            case 0:
                clipToPlay = footstepGenericClip;
                break;
            case 1:
                clipToPlay = footstepNivel1Clip;
                break;
            case 2:
                clipToPlay = footstepNivel2Clip;
                break;
            case 3:
                clipToPlay = footstepNivel3Clip;
                break;
            default:
                Debug.LogWarning($"{name}: Nivel de paso '{nivel}' no valido para sonido de paso.");
                return;
        }

        if (clipToPlay != null && footstepsAudioSource != null)
        {
            if (useStepPitchVariance) footstepsAudioSource.pitch = stepPitch + Random.Range(-stepPitchVariance, stepPitchVariance);
            else footstepsAudioSource.pitch = stepPitch;

            footstepsAudioSource.PlayOneShot(clipToPlay, stepVolume);
        }
    }

    public void StopFootsteps()
    {
        if (footstepsAudioSource != null && footstepsAudioSource.isPlaying)
        {
            footstepsAudioSource.Stop();
        }
    }

    public void PlayDashSound()
    {
        PlayOneShotInternal(dashClip, dashVolume, dashPitch, usePitchVariance, pitchVariance);
    }

    #endregion

    #region Offense & Defense Audio Methods

    public void PlayMeleeSound(string attackType)
    {
        AudioClip clipToPlay = null;
        switch (attackType)
        {
            case "BasicSlash":
                clipToPlay = basicSlashClip;
                break;
            case "SpinSlash":
                clipToPlay = spinSlashClip;
                break;
            case "HeavySlash":
                clipToPlay = heavySlashClip;
                break;
            default:
                Debug.LogWarning($"{name}: Tipo de ataque '{attackType}' para sonido melee.");
                return;
        }

        PlayOneShotInternal(clipToPlay, meleeVolume, meleePitch, useMeleePitchVariance, meleePitchVariance);
    }

    public void PlayThrowShieldSound(bool isBerserker)
    {
        AudioClip clip = isBerserker ? throwShieldBerserkerClip : throwShieldClip;
        PlayOneShotInternal(clip, rangeVolume, rangePitch, useRangePitchVariance, rangePitchVariance);
    }

    public void PlayCatchShieldSound(bool isBerserker)
    {
        AudioClip clip = isBerserker ? catchShieldBerserkerClip : catchShieldClip;
        PlayOneShotInternal(clip, rangeVolume, rangePitch, useRangePitchVariance, rangePitchVariance);
    }

    public void PlayActiveBlockSound()
    {
        PlayOneShotInternal(activeBlockClip, blockVolume, blockPitch, useblockPitchVariance, blockPitchVariance);
    }

    public void PlayBlockHitSound()
    {
        PlayOneShotInternal(blockHitClip, blockVolume, blockPitch, useblockPitchVariance, blockPitchVariance);
    }

    public void PlayBlockBreakSound()
    {
        PlayOneShotInternal(blockBreakClip, blockVolume, blockPitch, useblockPitchVariance, blockPitchVariance);
    }

    #endregion

    #region Berserker Specific Audio Methods

    public void PlayBerserkerAbility(bool isActive)
    {
        if (isActive)
        {
            PlayOneShotInternal(activeAbilityClip, abilityVolume, abilityPitch, useabilityPitchVariance, abilityPitchVariance);
        }
        else
        {
            PlayOneShotInternal(desactiveAbilityClip, abilityVolume, abilityPitch, useabilityPitchVariance, abilityPitchVariance);
        }
    }

    public void PlayBerserkerLowWarningAbility()
    {
        PlayOneShotInternal(lowHealthWarningSound, abilityVolume, abilityPitch, useabilityPitchVariance, abilityPitchVariance);
    }

    public void PlayLifeAbsorbSound()
    {
        PlayOneShotInternal(lifeAbsorbClip, abilityVolume, abilityPitch, useabilityPitchVariance, abilityPitchVariance);
    }

    #endregion

    #region Internal Helper Methods

    // Un metodo generico privado para reutilizar la logica de varianza
    private void PlayOneShotInternal(AudioClip clip, float volume, float basePitch, bool useVariance, float varianceAmount)
    {
        if (clip == null || mainAudioSource == null) return;

        if (useVariance)
        {
            mainAudioSource.pitch = basePitch + Random.Range(-varianceAmount, varianceAmount);
        }
        else
        {
            mainAudioSource.pitch = basePitch;
        }

        mainAudioSource.PlayOneShot(clip, volume);
    }

    #endregion
}