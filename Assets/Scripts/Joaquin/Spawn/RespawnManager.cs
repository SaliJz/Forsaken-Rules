using System.Collections;
using UnityEngine;
using UnityEngine.UI; // Si usas UI para el fade

public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance;

    [Header("Referencias")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image fadeImage;

    [Header("Configuración")]
    [SerializeField] private float respawnDelay = 0.5f;
    [SerializeField] private float fadeDuration = 0.3f;
    //[SerializeField] private int damageOnFall = 10;

    private SpawnPoint currentSpawnPoint;
    private SpawnPoint defaultSpawnPoint;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (playerMovement == null)
        {
            playerMovement = FindFirstObjectByType<PlayerMovement>();
        }

        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }
    }

    private void Start()
    {
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0;
            fadeImage.color = c;
        }

        //if (currentSpawnPoint == null)
        //{
        //    currentSpawnPoint = FindFirstObjectByType<SpawnPoint>();
        //    defaultSpawnPoint = currentSpawnPoint;
        //}

        //if (playerMovement != null && currentSpawnPoint != null)
        //{
        //    playerMovement.TeleportTo(currentSpawnPoint.GetSpawnPosition());
        //}
    }

    public void RegisterSpawnPoint(SpawnPoint newPoint)
    {
        // Solo actualizamos si es un punto diferente
        if (currentSpawnPoint != newPoint)
        {
            if (currentSpawnPoint != null) currentSpawnPoint.DeactivateVisuals();

            currentSpawnPoint = newPoint;
            currentSpawnPoint.ActivateVisuals();

            Debug.Log($"Nuevo punto de spawn establecido: {newPoint.name}");
        }
    }

    public void RespawnPlayer()
    {
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        playerMovement.SetCanMove(false);
        playerMovement.CancelDash();
        playerMovement.IsDashDisabled = true;

        if (playerHealth != null)
        {
            // playerHealth.TakeDamage(damageOnFall); 
        }

        if (fadeImage != null)
        {
            float t = 0;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                Color c = fadeImage.color;
                c.a = Mathf.Lerp(0, 1, t / fadeDuration);
                fadeImage.color = c;
                yield return null;
            }
            fadeImage.color = new Color(0, 0, 0, 1);
        }

        yield return new WaitForSeconds(respawnDelay);

        if (currentSpawnPoint != null)
        {
            playerMovement.TeleportTo(currentSpawnPoint.GetSpawnPosition());
        }
        else
        {
            Debug.LogError("No hay SpawnPoint asignado!");
        }

        if (fadeImage != null)
        {
            float t = 0;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                Color c = fadeImage.color;
                c.a = Mathf.Lerp(1, 0, t / fadeDuration);
                fadeImage.color = c;
                yield return null;
            }
        }

        playerMovement.IsDashDisabled = false;
        playerMovement.SetCanMove(true);
    }
}