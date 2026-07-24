using UnityEngine;

public class AnimationBridge : MonoBehaviour
{
    #region Inspector

    [Header("Target Animation Settings")]
    [SerializeField] private Animator targetAnimator;

    #endregion

    #region Public Methods for Animation Events

    public void PlayTargetAnimation(string clipName)
    {
        if (targetAnimator == null)
        {
            Debug.LogWarning("AnimationBridge: No hay un Target Animator asignado.");
            return;
        }

        targetAnimator.Play(clipName);
    }

    public void TriggerTargetParameter(string parameterName)
    {
        if (targetAnimator != null)
        {
            targetAnimator.SetTrigger(parameterName);
        }
    }

    #endregion
}