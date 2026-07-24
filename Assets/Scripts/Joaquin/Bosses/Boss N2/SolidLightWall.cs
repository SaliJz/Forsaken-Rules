using System.Collections;
using UnityEngine;

/// <summary>
/// Barrera de luz sólida creada por "Falla Divisoria".
/// </summary>
public class SolidLightWall : MonoBehaviour
{
    #region Inspector - Daño

    [Header("Daño de contacto")]
    [SerializeField] private float contactDamagePerSecond = 2.5f;

    [Header("Daño instantáneo al crear encima del jugador")]
    [SerializeField] private float instantDamageOnSpawn = 12.5f;

    #endregion

    #region Inspector - Configuración

    [Header("Tiempo de vida")]
    [SerializeField] private float wallLifetime = 5f;

    [Header("Detección")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Empuje de contacto")]
    [Tooltip("Velocidad del empuje que aparta al jugador al tocar la pared.")]
    [SerializeField] private float pushSpeed = 4f;
    [Tooltip("Duración del empuje en segundos.")]
    [SerializeField] private float pushDuration = 0.2f;
    [Tooltip("Intervalo mínimo entre zaps (daño + empuje) en segundos.")]
    [SerializeField] private float zapInterval = 0.65f;

    #endregion

    #region Public Properties

    public float ContactDamagePerSecond
    {
        get => contactDamagePerSecond;
        set => contactDamagePerSecond = value;
    }

    public float InstantDamageOnSpawn
    {
        get => instantDamageOnSpawn;
        set => instantDamageOnSpawn = value;
    }

    public float WallLifetime
    {
        get => wallLifetime;
        set => wallLifetime = value;
    }

    #endregion

    #region Estado interno

    private float zapTimer;
    private bool zapOnCooldown;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (CheckSpawnOverlapPlayer()) return;
        StartCoroutine(LifetimeRoutine());
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Player")) return;
        TryZap(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.collider.CompareTag("Player") || zapOnCooldown) return;

        zapTimer += Time.deltaTime;
        if (zapTimer < zapInterval) return;

        zapTimer = 0f;
        TryZap(collision.collider);
    }

    #endregion

    #region Core Logic

    private void TryZap(Collider playerCollider)
    {
        if (zapOnCooldown) return;

        playerCollider.GetComponent<PlayerHealth>()?.TakeDamage(contactDamagePerSecond);

        Vector3 pushDir = playerCollider.transform.position - transform.position;
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude < 0.001f)
            pushDir = -transform.forward;
        pushDir.Normalize();

        CharacterController cc = playerCollider.GetComponent<CharacterController>();
        if (cc != null && cc.enabled)
        {
            StartCoroutine(PushRoutine(cc, pushDir));
        }
        else
        {
            Rigidbody rb = playerCollider.GetComponent<Rigidbody>();
            rb?.AddForce(pushDir * pushSpeed, ForceMode.VelocityChange);
        }

        StartCoroutine(ZapCooldownRoutine());
    }

    /// <summary>
    /// Desplaza el CharacterController del jugador durante pushDuration segundos
    /// para simular el empuje sin necesitar Rigidbody.
    /// </summary>
    private IEnumerator PushRoutine(CharacterController cc, Vector3 dir)
    {
        float elapsed = 0f;
        while (elapsed < pushDuration)
        {
            if (cc == null || !cc.enabled) yield break;
            cc.Move(dir * pushSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ZapCooldownRoutine()
    {
        zapOnCooldown = true;
        zapTimer = 0f;
        yield return new WaitForSeconds(zapInterval);
        zapOnCooldown = false;
    }

    /// <returns>
    /// true si había jugador encima y la pared se destruyó sin mantenerse.
    /// </returns>
    private bool CheckSpawnOverlapPlayer()
    {
        Collider[] hits = Physics.OverlapBox(
            transform.position,
            transform.localScale * 0.5f,
            transform.rotation,
            playerLayer
        );

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;
            hit.GetComponent<PlayerHealth>()?.TakeDamage(instantDamageOnSpawn);
            Destroy(gameObject);
            return true;
        }

        return false;
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(wallLifetime);
        Destroy(gameObject);
    }

    #endregion

    #region Debug

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.7f, 1f, 0.2f, 0.3f);
        Gizmos.DrawCube(transform.position, transform.localScale);
    }

    #endregion
}