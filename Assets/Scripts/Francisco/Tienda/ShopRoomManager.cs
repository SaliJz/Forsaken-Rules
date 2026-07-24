using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class ShopRoomManager : MonoBehaviour
{
    [Header("Shop Item Spawn Locations")]
    public List<Transform> spawnLocations;

    [Header("Merchant Settings")]
    public GameObject merchantPrefab;
    public Transform merchantSpawnLocation;
    public GameObject appearanceEffectPrefab;
    public float effectDuration = 1.0f;

    private ShopManager shopManager;
    private MerchantRoomManager merchantRoomManager;

    private void Awake()
    {
        shopManager = FindAnyObjectByType<ShopManager>();
        merchantRoomManager = GetComponentInParent<MerchantRoomManager>();

        if (shopManager == null)
        {
            Debug.LogError("ShopManager no encontrado. El cuarto de la tienda no funcionará.");
        }
    }

    private void Start()
    {
        if (shopManager != null)
        {
            StartCoroutine(shopManager.GenerateShopItems(spawnLocations, this.transform));
        }

        if (merchantPrefab != null && merchantSpawnLocation != null && merchantRoomManager != null)
        {
            StartCoroutine(SpawnMerchantSequence());
        }
    }

    private IEnumerator SpawnMerchantSequence()
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

        Instantiate(merchantPrefab, merchantSpawnLocation.position, merchantSpawnLocation.rotation, transform);

        merchantRoomManager.CompleteFirstVisit();
    }
}