using UnityEngine;

public class DevilDistortionEffectManager : MonoBehaviour
{
    private void OnEnable()
    {
        if (DevilManipulationManager.Instance != null)
        {
            DevilManipulationManager.Instance.OnDistortionActivated += ExecuteDistortionLogic;
        }
        else
        {
            Debug.LogError("[DevilDistortionEffectManager] DevilManipulationManager.Instance no encontrado.");
        }
    }

    private void OnDisable()
    {
        if (DevilManipulationManager.Instance != null)
        {
            DevilManipulationManager.Instance.OnDistortionActivated -= ExecuteDistortionLogic;
        }
    }

    private string GetDistortionName(DevilDistortionType distortion)
    {
        switch (distortion)
        {
            case DevilDistortionType.AbyssalConfusion:
                return "Confusión Abisal";
            case DevilDistortionType.FloorOfTheDamned:
                return "Suelo de los Condenados";
            case DevilDistortionType.DeceptiveDarkness:
                return "Oscuridad Engañosa";
            case DevilDistortionType.SealedLuck:
                return "Suerte Sellada";
            case DevilDistortionType.WitheredBloodthirst:
                return "Sed de Sangre Agostada";
            case DevilDistortionType.InfernalJudgement:
                return "Juicio Infernal";
            default:
                return "Distorsión Desconocida";
        }
    }

    private void ExecuteDistortionLogic(DevilDistortionType distortion)
    {
        if (UIManager.Instance != null)
        {
            string distortionName = GetDistortionName(distortion);
            UIManager.Instance.ShowManipulationText($"¡Distorsión Activada: {distortionName}!");
        }

        switch (distortion)
        {
            case DevilDistortionType.AbyssalConfusion:
                PlayerEffectDistortion.Instance?.ApplyAbyssalConfusion();
                break;

            case DevilDistortionType.FloorOfTheDamned:
                DungeonGenerator.Instance?.CurrentRoom?.GetComponent<RoomDistortionHandler>()?.ApplyFloorOfTheDamned();
                break;

            case DevilDistortionType.DeceptiveDarkness:
                //VisibilityController.Instance?.SetVisibility(12f);
                break;

            case DevilDistortionType.SealedLuck:
                ShopManager.Instance?.SetDistortionActive(DevilDistortionType.SealedLuck, true);
                break;

            case DevilDistortionType.WitheredBloodthirst:
                PlayerHealth.Instance?.BlockKillHeal(true);
                break;

            case DevilDistortionType.InfernalJudgement:
                int extraWaves = UnityEngine.Random.Range(2, 4);
                DungeonGenerator.Instance?.CurrentRoom?.GetComponent<EnemyManager>()?.AddExtraWaves(extraWaves);
                break;
        }
    }
}