using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AstarothController
{
    #region Audio System

    private void HandleAudioLoop()
    {
        if (_navMeshAgent == null || !_navMeshAgent.enabled) return;

        bool isMoving = !_navMeshAgent.isStopped && _navMeshAgent.velocity.sqrMagnitude > 0.5f;

        if (isMoving)
        {
            _audioStepTimer += Time.deltaTime;
            if (_audioStepTimer >= 1f)
            {
                if (audioSource != null && walkSFX != null) audioSource.PlayOneShot(walkSFX, 0.5f);
                _audioStepTimer = 0f;
            }

            _audioIdleTimer = 0f;
        }
        else
        {
            _audioIdleTimer += Time.deltaTime;
            if (_audioIdleTimer >= _audioIdleInterval)
            {
                if (audioSource != null && presenceSFX != null) audioSource.PlayOneShot(presenceSFX);
                _audioIdleTimer = 0f;
                _audioIdleInterval = Random.Range(5f, 9f);
            }
        }
    }

    #endregion

    #region Movement & Orientation Helpers

    private void LookAtPlayer()
    {
        if (_player == null || _navMeshAgent == null) return;

        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * _navMeshAgent.angularSpeed);
        }
    }

    private Vector3 GetGroundPosition(Vector3 rayOrigin)
    {
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin + Vector3.up * 2f, Vector3.down, out hit, 30f, LayerMask.GetMask("Ground")))
        {
            return hit.point + Vector3.up * 0.01f;
        }

        return new Vector3(rayOrigin.x, 0.01f, rayOrigin.z);
    }

    #endregion

    #region Combat Systems

    private void CacheBossRenderers()
    {
        _bossRenderers = GetComponentsInChildren<Renderer>(true);
        _bossOriginalColors = new Color[_bossRenderers.Length];

        for (int i = 0; i < _bossRenderers.Length; i++)
        {
            if (_bossRenderers[i] == null) continue;

            Material mat = _bossRenderers[i].material;
            _bossOriginalColors[i] = mat.HasProperty("_Color") ? mat.color : Color.white;
        }
    }

    private void ExecuteAttack(GameObject target, Vector3 position, float damageAmount)
    {
        if (target.TryGetComponent<PlayerHealth>(out var health))
        {
            if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) 
                && blockSystem.IsBlocking 
                && blockSystem.CanBlockAttack(position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount);
                if (remainingDamage > 0f) health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
        else if (target.TryGetComponent<PlayerHealth>(out var healthOnly))
        {
            healthOnly.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
    }

    private void DealAreaDamage(Vector3 center, float radius, float damage, float knockbackForce)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, LayerMask.GetMask("Player"));

        HashSet<GameObject> damagedTargets = new HashSet<GameObject>();
        foreach (var hit in hits)
        {
            GameObject target = hit.transform.root != null ? hit.transform.root.gameObject : hit.gameObject;
            if (damagedTargets.Contains(target)) continue;

            damagedTargets.Add(target);
            ExecuteAttack(target, center, damage);
            ApplyKnockback(target.transform, knockbackForce);
        }
    }

    private void DealAreaDamageOnce(Vector3 center, float radius, float damage, float knockbackForce, HashSet<GameObject> damagedTargets)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, LayerMask.GetMask("Player"));

        foreach (var hit in hits)
        {
            GameObject target = hit.transform.root != null ? hit.transform.root.gameObject : hit.gameObject;
            if (damagedTargets.Contains(target)) continue;

            damagedTargets.Add(target);
            ExecuteAttack(target, center, damage);
            ApplyKnockback(target.transform, knockbackForce);
        }
    }

    private void ApplyKnockback(Transform target, float force)
    {
        if (target.TryGetComponent<PlayerKnockbackReceiver>(out var knockbackReceiver))
        {
            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0f;

            knockbackReceiver.ApplyKnockback(direction, force);
        }
    }

    #endregion

    #region Visuals & Feedback Helpers

    private void SpawnGroundTelegraph(GameObject prefab, Vector3 centerPosition, float radius, float duration)
    {
        if (_isDead) return;
        if (prefab == null) return;

        Vector3 origin = centerPosition + Vector3.up * (radius + 0.5f);
        Vector3 groundCenter;

        if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, radius + 2f, LayerMask.GetMask("Ground")))
        {
            groundCenter = hit.point + Vector3.up * 0.02f;
        }
        else
        {
            groundCenter = new Vector3(centerPosition.x, 0.02f, centerPosition.z);
        }

        GameObject instance = Instantiate(prefab, groundCenter, Quaternion.identity);
        _instantiatedEffects.Add(instance);
        instance.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
        Destroy(instance, duration);
    }

    private void ShowDodgeIndicator()
    {
        if (_isDead) return;
        if (_dodgeIndicatorPrefab == null || _player == null) return;

        GameObject indicator = Instantiate(_dodgeIndicatorPrefab, _player.position + Vector3.up * 2f, Quaternion.identity);
        Destroy(indicator, _dodgeIndicatorDuration);
    }

    public void ShakeCamera(float duration, float amplitude, float frequency)
    {
        if (_isDead) return;
        if (_noise == null) return;
        StartCoroutine(ShakeRoutine(duration, amplitude, frequency));
    }

    private IEnumerator ShakeRoutine(float duration, float amplitude, float frequency)
    {
        _noise.AmplitudeGain = amplitude;
        _noise.FrequencyGain = frequency;
        yield return new WaitForSeconds(duration);
        _noise.AmplitudeGain = 0f;
        _noise.FrequencyGain = 0f;
    }

    private void DestroyAllInstantiatedEffects()
    {
        foreach (GameObject effect in _instantiatedEffects)
        {
            if (effect != null) Destroy(effect);
        }

        _instantiatedEffects.Clear();
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (_whipDamageOrigin != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawWireSphere(_whipDamageOrigin.position, _whipHitRadius);
        }

        if (_showSmashOverlapGizmo)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_lastSmashOverlapCenter, _lastSmashOverlapRadius);
        }

        if (_showRoomGizmos)
        {
            Gizmos.color = new Color(1, 0, 1, 0.3f);
            Gizmos.DrawSphere(_roomCenter, 0.5f);
            Gizmos.DrawWireSphere(_roomCenter, _roomMaxRadius);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, _smashDetectionRadius);

            Gizmos.color = new Color(0.45f, 0.22f, 0.08f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _mudWaveTriggerDistance);

            Gizmos.color = new Color(0.8f, 0.55f, 0.2f, 0.5f);
            Gizmos.DrawRay(transform.position, transform.forward * _mudWaveMinChargeDistance);
        }
    }

    #endregion
}