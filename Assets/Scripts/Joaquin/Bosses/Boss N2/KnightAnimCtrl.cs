using UnityEngine;
using System.Collections;

public class KnightAnimCtrl : MonoBehaviour
{
    #region Inspector - References

    [SerializeField] private Animator anim;
    [SerializeField] private EscudoController shieldCtrl;

    #endregion

    #region Inspector - Anticipation Shake

    [Header("Anticipation Shake")]
    [SerializeField] private float shakeIntensity = 0.2f;
    [SerializeField] private float shakeFrequency = 2f;

    #endregion

    #region Internal State

    private static readonly int HashWalking = Animator.StringToHash("Walking");
    private static readonly int HashSwordStack = Animator.StringToHash("SwordStack");
    private static readonly int HashAttackDash = Animator.StringToHash("AttackDash");
    private static readonly int HashAttackHand = Animator.StringToHash("AttackHand");
    private static readonly int HashSoloDash = Animator.StringToHash("SoloDash");
    private static readonly int HashDeath = Animator.StringToHash("Death");
    private static readonly int HashAttackEnded = Animator.StringToHash("AttackEnded");

    private Coroutine shakeCoroutine;
    private Vector3 originalLocalPosition;
    private bool originalPositionCaptured = false;

    private IAnimEventHandler ownerEventHandler;

    #endregion

    private void Awake()
    {
        if (anim == null) anim = GetComponent<Animator>();

        ownerEventHandler = GetComponentInParent<IAnimEventHandler>();

        originalLocalPosition = transform.localPosition;
        originalPositionCaptured = true;
    }

    #region Playback

    public void SetWalking(bool value)
    {
        if (anim != null) anim.SetBool(HashWalking, value);
    }

    public void PlayStaticFailure()
    {
        if (anim != null) anim.SetBool(HashAttackEnded, false);
        if (anim != null) anim.SetTrigger(HashSwordStack);
    }

    public void PlayBrokenCharge()
    {
        if (anim != null) anim.SetBool(HashAttackEnded, false);
        if (anim != null) anim.SetTrigger(HashAttackDash);
    }

    public void PlayScrapRam()
    {
        if (anim != null) anim.SetBool(HashAttackEnded, false);
        if (anim != null) anim.SetTrigger(HashSoloDash);
    }

    public void PlayHandAttack()
    {
        if (anim != null) anim.SetBool(HashAttackEnded, false);
        if (anim != null) anim.SetTrigger(HashAttackHand);
    }

    public void PlayDeath()
    {
        if (anim != null) anim.SetTrigger(HashDeath);
    }

    public void SetAttackEnded(bool value)
    {
        if (anim != null) anim.SetBool(HashAttackEnded, value);
    }

    #endregion

    #region Animation Control

    public void PlayInvulnerabilityVFX()
    {
        if (shieldCtrl != null) shieldCtrl.Escudo = true;
    }

    public void PauseAnimation()
    {
        if (anim != null) anim.speed = 0f;
    }

    public void ResumeAnimation()
    {
        if (anim != null) anim.speed = 1f;
    }

    #endregion

    #region Animation Events Relay

    public void AnimEvent_AnticipationPause()
    {
        ownerEventHandler?.HandleAnimEvents("AnimEvent_AnticipationPause");
    }

    public void AnimEvent_AttackExecute()
    {
        ownerEventHandler?.HandleAnimEvents("AnimEvent_AttackExecute");
    }

    #endregion

    #region Anticipation Shake

    public void PlayAnticipationShake(float duration)
    {
        StopAnticipationShake();
        shakeCoroutine = StartCoroutine(ShakeRoutine(duration));
    }

    public void StopAnticipationShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        if (originalPositionCaptured)
        {
            transform.localPosition = originalLocalPosition;
        }
    }

    private IEnumerator ShakeRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float ox = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f) * shakeIntensity;
            float oz = Mathf.Cos(elapsed * shakeFrequency * Mathf.PI * 2.3f) * shakeIntensity * 0.6f;
            transform.localPosition = originalLocalPosition + new Vector3(ox, 0f, oz);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = originalLocalPosition;
        shakeCoroutine = null;
    }

    #endregion
}