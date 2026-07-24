using UnityEngine;

public class GachaponPlayerDetector : MonoBehaviour
{
    #region Properties

    // Expone el CharacterController solo si el jugador esta en la zona
    public CharacterController PlayerInZone { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerInZone = other.GetComponent<CharacterController>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerInZone = null;
        }
    }

    #endregion
}