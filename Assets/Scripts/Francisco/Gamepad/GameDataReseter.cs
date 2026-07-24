using UnityEngine;

public class GameDataReseter : MonoBehaviour
{
    private const string MerchantFirstVisitKey = "MerchantFirstVisit";

    private void Awake()
    {
        if (PlayerPrefs.HasKey(MerchantFirstVisitKey))
        {
            PlayerPrefs.DeleteKey(MerchantFirstVisitKey);
            PlayerPrefs.Save();
        }
    }
}