using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BossIntroTrigger : MonoBehaviour
{
    #region Inspector

    [Header("Sequence References")]
    [SerializeField] private BossIntroDirector bossIntroDirector;

    [Header("Settings")]
    [SerializeField] private string playerTag = "Player";

    #endregion

    #region Private State

    private bool hasTriggered = false;

    #endregion

    #region Trigger Detection

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        if (other.CompareTag(playerTag))
        {
            hasTriggered = true;
            StartCoroutine(ExecuteFullSequenceRoutine(other.transform));
        }
    }

    #endregion

    #region Main Routine

    private IEnumerator ExecuteFullSequenceRoutine(Transform playerTransform)
    {
        if (bossIntroDirector != null)
        {
            yield return StartCoroutine(bossIntroDirector.BossIntroRoutine(playerTransform));
        }

        Destroy(gameObject);
    }

    #endregion
}