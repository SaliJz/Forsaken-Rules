using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class HojasEstancadasHazard : MonoBehaviour
{
    private sealed class PlayerZoneState
    {
        public PlayerStatsManager StatsManager;
        public PlayerMovement PlayerMovement;
        public Coroutine PendingActivation;
        public string ModifierKey;
        public int OverlapCount;
        public bool IsSlowApplied;
    }

    [Header("Configuracion de la trampa")]
    [SerializeField, Range(0.05f, 1f), Tooltip("Fraccion final de velocidad que conserva el jugador. 0.4 = 40% de su velocidad actual.")]
    private float movementMultiplier = 0.4f;

    [SerializeField, Min(0f), Tooltip("Tiempo de espera antes de aplicar la ralentizacion tras pisar la zona.")]
    private float DelayActivation = 0.5f;

    [SerializeField, Range(0.1f, 1f), Tooltip("Velocidad de la animacion de correr mientras el jugador esta dentro del parche.")]
    private float slowedRunAnimationSpeed = 0.5f;

    [SerializeField, Tooltip("Si esta activo, solo respondera a objetos etiquetados como Player.")]
    private bool requirePlayerTag = true;

    [Header("Sombras Emergentes")]
    [SerializeField, Tooltip("Objeto visual a activar cuando las siluetas emergen y la ralentizacion entra en efecto.")]
    private GameObject emergenceVisualRoot;

    private readonly Dictionary<GameObject, PlayerZoneState> trackedPlayers = new Dictionary<GameObject, PlayerZoneState>();

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null && !trigger.isTrigger)
        {
            Debug.LogWarning($"[{nameof(HojasEstancadasHazard)}] El collider de '{name}' deberia tener IsTrigger activado.");
        }

        UpdateVisualState();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerStatsManager statsManager = other.GetComponentInParent<PlayerStatsManager>();
        if (statsManager == null)
        {
            return;
        }

        GameObject playerObject = statsManager.gameObject;
        if (requirePlayerTag && !playerObject.CompareTag("Player"))
        {
            return;
        }

        if (!trackedPlayers.TryGetValue(playerObject, out PlayerZoneState state))
        {
            state = new PlayerZoneState
            {
                StatsManager = statsManager,
                PlayerMovement = playerObject.GetComponent<PlayerMovement>(),
                ModifierKey = BuildModifierKey(playerObject)
            };

            trackedPlayers.Add(playerObject, state);
        }

        state.OverlapCount++;
        if (state.OverlapCount > 1 || state.IsSlowApplied || state.PendingActivation != null)
        {
            return;
        }

        state.PendingActivation = StartCoroutine(ActivateSlowAfterDelay(playerObject, state));
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerStatsManager statsManager = other.GetComponentInParent<PlayerStatsManager>();
        if (statsManager == null)
        {
            return;
        }

        GameObject playerObject = statsManager.gameObject;
        if (!trackedPlayers.TryGetValue(playerObject, out PlayerZoneState state))
        {
            return;
        }

        state.OverlapCount = Mathf.Max(0, state.OverlapCount - 1);
        if (state.OverlapCount > 0)
        {
            return;
        }

        CancelPendingActivation(state);
        RemoveSlow(state);
        trackedPlayers.Remove(playerObject);
        UpdateVisualState();
    }

    private IEnumerator ActivateSlowAfterDelay(GameObject playerObject, PlayerZoneState state)
    {
        if (DelayActivation > 0f)
        {
            yield return new WaitForSeconds(DelayActivation);
        }

        state.PendingActivation = null;

        if (playerObject == null || state.StatsManager == null || state.OverlapCount <= 0 || state.IsSlowApplied)
        {
            yield break;
        }

        state.StatsManager.ApplyMultiplierModifier(state.ModifierKey, StatType.MoveSpeed, movementMultiplier);
        state.IsSlowApplied = true;

        //if (state.PlayerMovement != null)
        //{
        //    state.PlayerMovement.SetRunAnimationSpeedOverride(state.ModifierKey, slowedRunAnimationSpeed);
        //}

        UpdateVisualState();
    }

    private void CancelPendingActivation(PlayerZoneState state)
    {
        if (state.PendingActivation == null)
        {
            return;
        }

        StopCoroutine(state.PendingActivation);
        state.PendingActivation = null;
    }

    private void RemoveSlow(PlayerZoneState state)
    {
        if (!state.IsSlowApplied || state.StatsManager == null)
        {
            state.IsSlowApplied = false;
            return;
        }

        state.StatsManager.RemoveNamedModifier(state.ModifierKey);
        state.IsSlowApplied = false;

        //if (state.PlayerMovement != null)
        //{
        //    state.PlayerMovement.ClearRunAnimationSpeedOverride(state.ModifierKey);
        //}
    }

    private string BuildModifierKey(GameObject playerObject)
    {
        return $"HojasEstancadas_{GetInstanceID()}_{playerObject.GetInstanceID()}";
    }

    private void UpdateVisualState()
    {
        if (emergenceVisualRoot == null)
        {
            return;
        }

        emergenceVisualRoot.SetActive(HasAnyPlayerSlowed());
    }

    private bool HasAnyPlayerSlowed()
    {
        foreach (PlayerZoneState state in trackedPlayers.Values)
        {
            if (state.IsSlowApplied)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDisable()
    {
        CleanupAllStates();
    }

    private void OnDestroy()
    {
        CleanupAllStates();
    }

    private void CleanupAllStates()
    {
        foreach (PlayerZoneState state in trackedPlayers.Values)
        {
            CancelPendingActivation(state);
            RemoveSlow(state);
        }

        trackedPlayers.Clear();
        UpdateVisualState();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        movementMultiplier = Mathf.Clamp(movementMultiplier, 0.05f, 1f);
        DelayActivation = Mathf.Max(0f, DelayActivation);
        slowedRunAnimationSpeed = Mathf.Clamp(slowedRunAnimationSpeed, 0.1f, 1f);
    }
#endif
}
