using UnityEngine;

public class SoulDitchHazard : MonoBehaviour
{
    [Header("Ajustes del Castigo")]
    [SerializeField] private float lagAmount = 150f;
    [SerializeField] private float driftAmount = 0.6f;
    [SerializeField] private float distortionIntensity = 0.75f;
    [SerializeField] private float duration = 3.5f;

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var modifier = other.GetComponent<PlayerInputModifier>();
            if (modifier != null) modifier.ApplyDebuff(lagAmount, driftAmount, duration);

            if (HUDModifier.Instance != null)
                HUDModifier.Instance.ApplyUIDistortion(duration, distortionIntensity);
        }
    }
}