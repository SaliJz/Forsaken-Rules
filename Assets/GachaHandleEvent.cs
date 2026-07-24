using UnityEngine;

public class GachaHandleEvent : MonoBehaviour
{
    [SerializeField] GachaAnimCtrl animCtrl;

    public void ThrowEyeEvent()
    {
        animCtrl.LaunchEyeEvent();
        animCtrl.isAnimating = false;
    }

    public void Endnimation()
    {
        animCtrl.isAnimating = false;
    }
}
