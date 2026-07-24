using UnityEngine;

public class PlayerEffectDistortion : MonoBehaviour
{
    public static PlayerEffectDistortion Instance { get; private set; }

    private PlayerMovement playerMovement;

    private void Awake()
    {
        Instance = this;
        playerMovement = GetComponent<PlayerMovement>();
    }

    public void ApplyAbyssalConfusion()
    {
        if (playerMovement != null)
        {
            //playerMovement.InvertControls(true); USARE EL INPUTSYSTEMS PARA ESTO
            Debug.Log("[Distorsión] Controles Abisales Invertidos.");
        }
    }

    public void ClearAbyssalConfusion()
    {
        if (playerMovement != null)
        {
            //playerMovement.InvertControls(false); USARE EL INPUTSYSTEMS PARA ESTO
            Debug.Log("[Distorsión] Controles Abisales Restaurados.");
        }
    }
}