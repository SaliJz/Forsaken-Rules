using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mano que emerge del suelo para el ataque "Manos de los Ahogados".
/// Controlador principal lógico de la mano.
/// Delega el apartado visual a Boss2_HandAttackCtrl.
/// </summary>
public class SoulHand : MonoBehaviour
{
    #region Inspector - References

    [Header("Referencias")]
    [Tooltip("El script que controla la animación visual, situado en el objeto hijo.")]
    [SerializeField] private Boss2_HandAttackCtrl visualController;

    [Header("VFX")]
    [Tooltip("El prefab del efecto de explosion")]
    [SerializeField] private GameObject explosionVFXPrefab;

    #endregion

    #region Inspector - Daño

    [Header("Dano")]
    [Tooltip("El daño que inflige la mano al jugador.")]
    [SerializeField] private float damage = 15f;
    [Tooltip("El radio de detección para infligir daño al jugador.")]
    [SerializeField] private float radius = 1.5f;

    #endregion

    #region Internal State

    private bool hasExploded;
    private readonly List<GameObject> vfxInstances = new();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (visualController == null)
        {
            visualController = GetComponentInChildren<Boss2_HandAttackCtrl>();
        }
    }

    private void OnEnable()
    {
        if (visualController != null)
        {
            visualController.OnAttack.AddListener(EvaluateDamage);
            visualController.OnSequenceEnd.AddListener(HandleSequenceEnd);
        }
    }

    private void OnDisable()
    {
        if (visualController != null)
        {
            visualController.OnAttack.RemoveListener(EvaluateDamage);
            visualController.OnSequenceEnd.RemoveListener(HandleSequenceEnd);
        }
    }

    private void OnDestroy()
    {
        foreach (var vfx in vfxInstances)
        {
            if (vfx != null) Destroy(vfx);
        }
        vfxInstances.Clear();
    }

    #endregion

    #region Initialization

    public void Initialize(float damageAmount, float grabRadius)
    {
        damage = damageAmount;
        radius = grabRadius;

        if (visualController != null)
        {
            visualController.TriggerAttackSequence();
        }
        else
        {
            Debug.LogError("SoulHand no tiene referenciado su Boss2_HandAttackCtrl.");
        }
    }

    #endregion

    #region Event Handlers

    private void EvaluateDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;

            TriggerExplosion(hit.gameObject);
            break;
        }
    }

    private void HandleSequenceEnd()
    {
        if (!hasExploded)
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Core Logic & VFX

    private void TriggerExplosion(GameObject player)
    {
        if (hasExploded) return;
        hasExploded = true;

        player.GetComponent<PlayerHealth>()?.TakeDamage(damage);

        SpawnExplosionVFX();

        Destroy(gameObject, 0.5f);
    }

    private void SpawnExplosionVFX()
    {
        if (explosionVFXPrefab == null) return;

        GameObject vfx = Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
        vfx.transform.localScale = Vector3.one * radius * 2f;
        vfxInstances.Add(vfx);
        Destroy(vfx, 1f);
    }

    #endregion
}