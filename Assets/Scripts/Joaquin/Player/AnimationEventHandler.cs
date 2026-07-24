using UnityEngine;

public class AnimationEventHandler : MonoBehaviour
{
    [SerializeField] private PlayerMeleeAttack playerMeleeAttack;

    private void OnEnable()
    {
        if (playerMeleeAttack != null) playerMeleeAttack.OnAttacked += HandleAttackState;
    }

    private void OnDisable()
    {
        if (playerMeleeAttack != null) playerMeleeAttack.OnAttacked -= HandleAttackState;
    }

    private void OnDestroy()
    {
        if (playerMeleeAttack != null) playerMeleeAttack.OnAttacked -= HandleAttackState;
    }

    private void HandleAttackState(bool state)
    {
        //if (!state)
        //{
        //    if (playerMeleeAttack != null)
        //    {
        //        if (!playerMeleeAttack.IsAttacking)
        //        {
        //            //playerMeleeAttack.DesactiveAttack1Slash();
        //            //playerMeleeAttack.DesactiveAttack2Slash();
        //            //playerMeleeAttack.DesactiveAttack3Slash();
        //        }
        //    }
        //}
    }

    public void CallActiveAttack1Slash()
    {
        if (playerMeleeAttack != null)
        {
            playerMeleeAttack.ActiveAttack1Slash();
        }
    }

    public void CallDesactiveAttack1Slash()
    {
        //if (playerMeleeAttack != null)
        //{
        //    playerMeleeAttack.DesactiveAttack1Slash();
        //}
    }

    public void CallActiveAttack2Slash()
    {
        if (playerMeleeAttack != null)
        {
            playerMeleeAttack.ActiveAttack2Slash();
        }
    }

    public void CallDesactiveAttack2Slash()
    {
        //if (playerMeleeAttack != null)
        //{
        //    playerMeleeAttack.DesactiveAttack2Slash();
        //}
    }

    public void CallActiveAttack3Slash()
    {
        if (playerMeleeAttack != null)
        {
            playerMeleeAttack.ActiveAttack3Slash();
        }
    }

    public void CallDesactiveAttack3Slash()
    {
        //if (playerMeleeAttack != null)
        //{
        //    playerMeleeAttack.DesactiveAttack3Slash();
        //}
    }
}