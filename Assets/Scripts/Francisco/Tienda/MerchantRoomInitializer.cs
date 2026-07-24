using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MerchantRoomInitializer : MonoBehaviour
{
    public List<Transform> itemSpawnLocations;
    public GameObject merchantPrefab;
    public Transform merchantSpawnLocation;
    public GameObject appearanceEffectPrefab;
    public float effectDuration = 1.0f;

    private ShopManager shopManager;
    private MerchantRoomManager merchantRoomManager;
    private MerchantDialogHandler dialogHandler;
    private bool isInitialized = false;

    private void Awake()
    {
        shopManager = FindAnyObjectByType<ShopManager>();
        merchantRoomManager = FindAnyObjectByType<MerchantRoomManager>();
        dialogHandler = FindAnyObjectByType<MerchantDialogHandler>();

        if (shopManager == null)
        {
            Debug.LogError("ShopManager no encontrado. La sala del mercader no funcionará.");
        }

        if (dialogHandler != null)
        {
            dialogHandler.ChangeRoomMerchant(merchantRoomManager);
            dialogHandler.ResetMerchantState();
        }
    }

    public void InitializeMerchantRoom()
    {
        if (isInitialized)
        {
            return;
        }

        isInitialized = true;
        StartCoroutine(ExecuteInitializationSequence());
    }

    private IEnumerator ExecuteInitializationSequence()
    {
        if (appearanceEffectPrefab != null && merchantSpawnLocation != null)
        {
            GameObject effect = Instantiate(appearanceEffectPrefab, merchantSpawnLocation.position, Quaternion.identity, transform);

            yield return new WaitForSeconds(effectDuration);

            Destroy(effect);
        }
        else
        {
            yield return new WaitForSeconds(0.1f);
        }

        if (merchantPrefab != null && merchantSpawnLocation != null)
        {
            Instantiate(merchantPrefab, merchantSpawnLocation.position, Quaternion.identity, transform);
        }

        if (shopManager != null && merchantRoomManager != null)
        {
            merchantRoomManager.InitializeMerchantRoom(itemSpawnLocations, this.transform);
            merchantRoomManager.CompleteFirstVisit();
        }
    }
}