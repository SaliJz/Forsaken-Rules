using UnityEngine;
using System.Collections;

public class CombatZoneDetector : MonoBehaviour
{
    /*
    [Header("Configuración de Detección")]
    [Tooltip("Radio alrededor del jugador para considerar que esta en combate")]
    public float detectionRadius = 15f;

    [Tooltip("Selecciona la capa 'Enemy' aquí")]
    public LayerMask enemyLayer;

    [Tooltip("Cada cuánto tiempo escanea el área")]
    public float checkRate = 0.5f;

    [Header("Referencias")]
    public AsyncMusicController musicController;
    public GameObject player;
    public Transform detectionCenter;

    private readonly Collider[] hitBuffer = new Collider[1];

    private void Awake()
    {
        if (musicController == null) musicController = GetComponent<AsyncMusicController>();
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");
        if (detectionCenter == null ) detectionCenter = player.transform;
    }

    private void Start()
    {
        StartCoroutine(CheckForEnemiesRoutine());
    }

    private IEnumerator CheckForEnemiesRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(checkRate);

        while (true)
        {
            int count = Physics.OverlapSphereNonAlloc(
                detectionCenter.position,
                detectionRadius,
                hitBuffer,
                enemyLayer
            );

            bool enemiesNearby = count > 0;

            musicController.SetBattleState(enemiesNearby);

            yield return wait;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 center = detectionCenter != null ? detectionCenter.position : transform.position;
        Gizmos.DrawWireSphere(center, detectionRadius);
    }
    */
}