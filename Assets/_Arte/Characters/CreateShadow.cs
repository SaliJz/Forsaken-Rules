using UnityEngine;

public class CreateShadow : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] GameObject shadowPrefab;
    [SerializeField] LayerMask GroundLayer;
    [Tooltip("Si este field esta vacio, el script tomará la posicion central del objeto")]
    [SerializeField] Transform shadowPosition;
    [SerializeField] bool setParentInParent = false;

    [Header("Configuración")]
    public float shadowScale = 1f;
    [SerializeField] float spawnYoffset = 0.02f;
    [SerializeField] float checkInterval = 0.1f;
    [Tooltip("Hacer chequeo de si el personaje está en el suelo o pasando por un abismo (reservado para Aldous y aporia (quiza)")]
    [SerializeField] bool checkShadows = false;

    private GameObject shadowInstance;
    private float timer;
    private float currentShadowScale;

    private void Start()
    {
        SpawnShadow();
    }

    private void Update()
    {
        if (checkShadows)
        {
            timer += Time.deltaTime;

            if(timer > checkInterval)
            {
                timer = 0f;
                CheckGround();
            }
        }

        if (shadowInstance != null) shadowInstance.transform.rotation = Quaternion.identity;

        UpdateShadowSize();
    }

    private void SpawnShadow()
    {
        RaycastHit hit;
        Vector3 shadowSpawnPoint = shadowPosition? shadowPosition.position : transform.position;
        Vector3 rayOrigin = shadowSpawnPoint + Vector3.up * 0.5f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 50f, GroundLayer, QueryTriggerInteraction.Collide))
        {
            Vector3 spawnPos = hit.point + new Vector3(0, spawnYoffset, 0);
            Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

            shadowInstance = Instantiate(shadowPrefab, spawnPos, spawnRotation);
            shadowInstance.transform.localScale = Vector3.one * shadowScale * 0.1f;

            if(!setParentInParent)
            {
                if(shadowPosition != null) shadowInstance.transform.SetParent(shadowPosition.transform);
                else shadowInstance.transform.SetParent(this.transform);
            }
            else
            {
                shadowInstance.transform.SetParent(transform.parent);
            }
            Debug.Log("Sombra spawneada");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] No se encontró suelo para spawnear la sombra.");
        }
    }

    private void UpdateShadowSize()
    {
        if (shadowInstance == null || currentShadowScale == shadowScale) return;

        shadowInstance.transform.localScale = Vector3.one * shadowScale * 0.1f;
        currentShadowScale = shadowScale;
    }

    private void CheckGround()
    {
        if (shadowInstance == null) return;
        Vector3 shadowSpawnPoint = shadowPosition? shadowPosition.position : transform.position;

        Vector3 rayOrigin = shadowSpawnPoint + Vector3.up * 0.5f;
        bool hasGround = Physics.Raycast(rayOrigin, Vector3.down, 50f, GroundLayer, QueryTriggerInteraction.Collide);
        shadowInstance.SetActive(hasGround);
    }
}
