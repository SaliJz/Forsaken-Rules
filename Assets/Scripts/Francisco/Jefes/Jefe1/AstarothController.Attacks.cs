using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AstarothController
{
    #region Attack 1 Logic

    private IEnumerator WhipAttackSequence()
    {
        if (_isDead) yield break;

        _isAttackingWithWhip = true;

        GameObject whipIndicator = null;

        if (_navMeshAgent != null)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
        }

        LookAtPlayer();

        whipIndicator = CreatePersistentWhipIndicator();

        if (_animCtrl != null) _animCtrl.isWalking = false;
        if (_animCtrl != null) _animCtrl.PlayPrepareBaseAttack();

        // Espera al final del wind-up antes del primer golpe.
        yield return WaitForAnimEvent(ANIM_EVENT_WHIP_WINDUP_DONE, _whipPreAttackDelay);

        // Golpe 1
        if (_animCtrl != null) _animCtrl.PlayAttack();
        yield return WaitForAnimEvent(ANIM_EVENT_WHIP_IMPACT, _whipDelay1);
        PlayWhipSoundCrisp();
        CheckWhipHitbox("Golpe 1");

        // Golpe 2
        yield return new WaitForSeconds(_whipDelay2);
        if (_animCtrl != null) _animCtrl.PlayAttack();
        yield return WaitForAnimEvent(ANIM_EVENT_WHIP_IMPACT, _whipDelay1);
        PlayWhipSoundCrisp();
        CheckWhipHitbox("Golpe 2");

        // Golpe 3
        yield return new WaitForSeconds(_whipDelay3);
        if (_animCtrl != null) _animCtrl.PlayAttack();
        yield return WaitForAnimEvent(ANIM_EVENT_WHIP_IMPACT, _whipDelay1);
        PlayWhipSoundCrisp();
        CheckWhipHitbox("Golpe 3");

        yield return new WaitForSeconds(0.6f);

        DestroyWhipIndicator(whipIndicator);

        _isAttackingWithWhip = false;
    }

    private GameObject CreatePersistentWhipIndicator()
    {
        if (_whipTelegraphPrefab == null || _whipDamageOrigin == null) return null;

        Vector3 groundPosition = GetGroundPosition(_whipDamageOrigin.position);
        groundPosition.y += 0.03f;

        GameObject indicator = Instantiate(_whipTelegraphPrefab, groundPosition, Quaternion.identity);
        indicator.transform.localScale = new Vector3(_whipHitRadius * 2f, 0.05f, _whipHitRadius * 2f);

        _activeWhipIndicator = indicator;
        _whipImpactRocks_VFX = indicator.GetComponentInChildren<ParticleSystem>();
        _instantiatedEffects.Add(indicator);

        StartCoroutine(FollowWhipIndicator(indicator.transform));

        return indicator;
    }

    private IEnumerator FollowWhipIndicator(Transform indicator)
    {
        while (indicator != null && _isAttackingWithWhip && _whipDamageOrigin != null)
        {
            Vector3 groundPosition = GetGroundPosition(_whipDamageOrigin.position);
            groundPosition.y += 0.03f;

            indicator.position = groundPosition;
            indicator.localScale = new Vector3(_whipHitRadius * 2f, 0.05f, _whipHitRadius * 2f);

            yield return null;
        }
    }

    private void DestroyWhipIndicator(GameObject indicator)
    {
        if (indicator == null) return;

        if (_activeWhipIndicator == indicator)
        {
            _activeWhipIndicator = null;
            _whipImpactRocks_VFX = null;
        }

        _instantiatedEffects.Remove(indicator);
        Destroy(indicator);
    }

    private void PlayWhipSoundCrisp()
    {
        if (audioSource != null && whipAttackSFX != null)
        {
            audioSource.Stop();
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(whipAttackSFX);
        }
    }

    private void CheckWhipHitbox(string debugHitName)
    {
        if (_whipDamageOrigin == null) return;

        if (_whipImpactRocks_VFX != null)
        {
            _whipImpactRocks_VFX.Stop();
            _whipImpactRocks_VFX.Play();
        }

        Collider[] hits = Physics.OverlapSphere(
            _whipDamageOrigin.position,
            _whipHitRadius,
            LayerMask.GetMask("Player")
        );

        bool playerHit = false;

        foreach (Collider hit in hits)
        {
            GameObject target = hit.transform.root != null
                ? hit.transform.root.gameObject
                : hit.gameObject;

            ExecuteAttack(target, _whipDamageOrigin.position, _Attack1Damage);

            playerHit = true;
        }

        if (playerHit)
        {
            _totalAttemptsLanded++;
        }
    }

    #endregion

    #region Attack 2 Logic

    private IEnumerator SmashAttackSequence()
    {
        if (_isDead) yield break;

        _isSmashing = true;
        _smashRockInFlight = false;
        _smashImpactCompleted = false;
        _showSmashOverlapGizmo = false;
        smashAnticipationEnded = false;

        if (_navMeshAgent != null)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
        }

        LookAtPlayer();

        _smashTargetPoint = _player != null ? _player.position : transform.position;

        BeginHeldSmashRock();

        if (_animCtrl != null) _animCtrl.isWalking = false;
        if (_animCtrl != null) _animCtrl.PlayCanon();

        Coroutine indicatorRoutine = StartCoroutine(TrackSmashGroundIndicatorDuringAnimation());

        float holdTimer = 0f;
        while (holdTimer < _smashDelay)
        {
            FollowHeldSmashRock();
            holdTimer += Time.deltaTime;
            yield return null;
        }

        float anticipationTimeout = canonAnticipationDuration + 2f;
        float anticipationSafetyTimer = 0f;
        while (!_isInAnticipation && anticipationSafetyTimer < anticipationTimeout)
        {
            anticipationSafetyTimer += Time.deltaTime;
            yield return null;
        }
        yield return new WaitUntil(() => !_isInAnticipation);

        smashAnticipationEnded = true;

        if (_animCtrl != null) _animCtrl.PlayCanonShot();

        yield return WaitForAnimEvent(ANIM_EVENT_CANON_RELEASE, _smashRockTravelDuration);

        if (!_smashImpactCompleted && !_smashRockInFlight)
        {
            LaunchSmashRockToPlayer();
        }

        float safetyTimer = 0f;

        while (_smashRockInFlight)
        {
            safetyTimer += Time.deltaTime;
            yield return null;
        }

        if (indicatorRoutine != null)
        {
            StopCoroutine(indicatorRoutine);
        }

        HideSmashGroundIndicator();

        yield return new WaitForSeconds(0.25f);

        if (_animCtrl != null) _animCtrl.ReturnToIdle();

        _isSmashing = false;
        ResetSmashVisuals();
    }

    private void FollowHeldSmashRock()
    {
        if (!_smashRockIsHeld) return;
        if (_smashRockObject == null) return;
        if (_smashRockHeldFollowTarget == null) return;

        Transform rockTransform = _smashRockObject.transform;

        rockTransform.position = _smashRockHeldFollowTarget.position;
        rockTransform.rotation = _smashRockHeldFollowTarget.rotation;
        rockTransform.localScale = _smashRockOriginalLocalScale * _smashRockScale;
    }

    private IEnumerator TrackSmashGroundIndicatorDuringAnimation()
    {
        Transform indicator = _smashGroundIndicator;
        GameObject createdIndicator = null;

        if (indicator == null && _smashGroundIndicatorPrefab != null)
        {
            Vector3 startPosition = GetGroundPosition(_smashTargetPoint);
            startPosition.y += _smashIndicatorGroundOffset;

            createdIndicator = Instantiate(_smashGroundIndicatorPrefab, startPosition, Quaternion.identity);
            _instantiatedEffects.Add(createdIndicator);
            indicator = createdIndicator.transform;
        }

        if (indicator == null)
        {
            yield break;
        }

        indicator.gameObject.SetActive(true);
        indicator.localScale = new Vector3(_smashRadius * 2f, 0.05f, _smashRadius * 2f);

        while (_isSmashing && !_smashRockInFlight! && !_smashImpactCompleted)
        {
            if (_player != null && !smashAnticipationEnded)
            {
                _smashTargetPoint = _player.position;
            }

            Vector3 ground = GetGroundPosition(_smashTargetPoint);
            ground.y += _smashIndicatorGroundOffset;
            indicator.position = ground;

            yield return null;
        }

        if (createdIndicator != null)
        {
            _instantiatedEffects.Remove(createdIndicator);
            Destroy(createdIndicator);
        }
        else if (indicator != null)
        {
            indicator.gameObject.SetActive(false);
        }
    }

    private void HideSmashGroundIndicator()
    {
        if (_smashGroundIndicator != null)
        {
            _smashGroundIndicator.gameObject.SetActive(false);
        }
    }

    private void LaunchSmashRockToPlayer()
    {
        if (_isDead) return;
        if (!_isSmashing) return;
        if (_smashRockInFlight) return;
        if (_smashImpactCompleted) return;

        EndHeldSmashRock();
        StartCoroutine(ThrowSmashRockToTarget());
    }

    private IEnumerator ThrowSmashRockToTarget()
    {
        _smashRockInFlight = true;

        if (audioSource != null && smashAttackSFX != null)
        {
            audioSource.PlayOneShot(smashAttackSFX);
        }

        if (_smashRockObject == null)
        {
            PerformSmashDamage(_smashTargetPoint, new HashSet<GameObject>());
            _smashRockInFlight = false;
            _smashImpactCompleted = true;
            yield break;
        }

        Transform rockTransform = _smashRockObject.transform;

        rockTransform.SetParent(null, true);
        rockTransform.localScale = _smashRockOriginalLocalScale * _smashRockScale;
        _smashRockObject.SetActive(true);

        Vector3 startPosition = rockTransform.position;
        Vector3 previousPosition = startPosition;

        Vector3 groundTarget = GetGroundPosition(_smashTargetPoint);
        Vector3 endPosition = groundTarget + Vector3.up * Mathf.Max(0.05f, _smashIndicatorGroundOffset);

        HashSet<GameObject> hitByDirectImpact = new HashSet<GameObject>();

        float elapsed = 0f;

        while (elapsed < _smashRockTravelDuration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _smashRockTravelDuration));
            float curveTime = _smashRockTravelCurve != null
                ? _smashRockTravelCurve.Evaluate(normalizedTime)
                : normalizedTime;

            Vector3 nextPosition = Vector3.Lerp(startPosition, endPosition, curveTime);

            Vector3 movement = nextPosition - previousPosition;
            float movementDistance = movement.magnitude;

            if (movementDistance > 0.001f)
            {
                CheckRockFlightSphereCast(previousPosition, movement.normalized, movementDistance, hitByDirectImpact);
            }
            else
            {
                CheckRockFlightOverlap(nextPosition, hitByDirectImpact);
            }

            rockTransform.position = nextPosition;

            Vector3 direction = endPosition - startPosition;
            if (direction.sqrMagnitude > 0.01f)
            {
                rockTransform.rotation = Quaternion.LookRotation(direction.normalized);
            }

            previousPosition = nextPosition;

            yield return null;
        }

        rockTransform.position = endPosition;
        CheckRockFlightOverlap(endPosition, hitByDirectImpact);

        PerformSmashDamage(endPosition, hitByDirectImpact);

        yield return new WaitForSeconds(0.05f);

        RestoreSmashRockTransform();
        SetSmashRockActive(false);

        _smashRockInFlight = false;
        _smashImpactCompleted = true;
    }

    private void ResetSmashVisuals()
    {
        EndHeldSmashRock();
        RestoreSmashRockTransform();
        SetSmashRockActive(false);

        if (_smashGroundIndicator != null)
        {
            _smashGroundIndicator.gameObject.SetActive(false);
        }

        _smashRockInFlight = false;
        _smashImpactCompleted = false;
        _showSmashOverlapGizmo = false;
    }

    private void CheckRockFlightSphereCast(
        Vector3 origin,
        Vector3 direction,
        float distance,
        HashSet<GameObject> alreadyHit)
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            _smashRockHitRadius,
            direction,
            distance,
            LayerMask.GetMask("Player")
        );

        foreach (RaycastHit hit in hits)
        {
            GameObject entity = hit.collider.transform.root != null
                ? hit.collider.transform.root.gameObject
                : hit.collider.gameObject;

            if (alreadyHit.Contains(entity)) continue;

            alreadyHit.Add(entity);
            ExecuteAttack(entity, hit.point, _Attack2Damage);
        }
    }

    private void CheckRockFlightOverlap(Vector3 center, HashSet<GameObject> alreadyHit)
    {
        Collider[] hits = Physics.OverlapSphere(
            center,
            _smashRockHitRadius,
            LayerMask.GetMask("Player")
        );

        foreach (Collider hit in hits)
        {
            GameObject entity = hit.transform.root != null
                ? hit.transform.root.gameObject
                : hit.gameObject;

            if (alreadyHit.Contains(entity)) continue;

            alreadyHit.Add(entity);
            ExecuteAttack(entity, center, _Attack2Damage);
        }
    }

    private void PerformSmashDamage(Vector3 damageCenter, HashSet<GameObject> alreadyHitByRock)
    {
        _totalAttemptsExecuted++;
        _lastSmashHitPlayer = false;
        _lastSmashOverlapCenter = damageCenter;
        _lastSmashOverlapRadius = _smashRadius;
        _showSmashOverlapGizmo = true;

        Vector3 smashGroundPosition = GetGroundPosition(damageCenter);
        smashGroundPosition.y += 0.1f;

        if (_smashRadiusPrefab != null)
        {
            GameObject visualEffect = Instantiate(_smashRadiusPrefab, smashGroundPosition, Quaternion.identity);
            _instantiatedEffects.Add(visualEffect);
            Destroy(visualEffect, 0.6f);

            StartCoroutine(ExpandSmashRadiusWithDamage(
                visualEffect.transform,
                _smashRadius,
                smashGroundPosition,
                alreadyHitByRock
            ));
        }

        if (!_lastSmashHitPlayer)
        {
            ShowDodgeIndicator();
        }

        Invoke(nameof(DisableSmashOverlapGizmo), 1f);
    }

    private void DisableSmashOverlapGizmo()
    {
        _showSmashOverlapGizmo = false;
    }

    private IEnumerator ExpandSmashRadiusWithDamage(
        Transform effectTransform,
        float targetRadius,
        Vector3 groundPosition,
        HashSet<GameObject> alreadyHitByRock)
    {
        float duration = 0.5f;
        float elapsedTime = 0f;

        float startRadius = Mathf.Max(0.25f, _smashRockScale * 0.5f);

        Vector3 initialScale = new Vector3(startRadius * 2f, 0.05f, startRadius * 2f);
        Vector3 targetScale = new Vector3(targetRadius * 2f, 0.05f, targetRadius * 2f);

        HashSet<GameObject> hitByShockwaveEntity = new HashSet<GameObject>();

        while (elapsedTime < duration && effectTransform != null)
        {
            elapsedTime += Time.deltaTime;

            float t = elapsedTime / duration;
            float currentRadius = Mathf.Lerp(startRadius, targetRadius, t);

            effectTransform.localScale = Vector3.Lerp(initialScale, targetScale, t);

            Collider[] hitColliders = Physics.OverlapSphere(
                groundPosition,
                currentRadius,
                LayerMask.GetMask("Player")
            );

            foreach (Collider hitCollider in hitColliders)
            {
                GameObject entity = hitCollider.transform.root != null
                    ? hitCollider.transform.root.gameObject
                    : hitCollider.gameObject;

                if (alreadyHitByRock.Contains(entity) || hitByShockwaveEntity.Contains(entity)) continue;

                float heightDifference = Mathf.Abs(entity.transform.position.y - groundPosition.y);
                if (heightDifference > 2f) continue;

                if (Vector3.Distance(entity.transform.position, groundPosition) <= currentRadius)
                {
                    hitByShockwaveEntity.Add(entity);

                    ExecuteAttack(entity, groundPosition, _Attack2Damage);
                    ApplyKnockback(entity.transform, 10f);

                    _lastSmashHitPlayer = true;
                    _totalAttemptsLanded++;
                }
            }

            yield return null;
        }

        if (effectTransform != null)
        {
            effectTransform.localScale = targetScale;
            Destroy(effectTransform.gameObject, 0.5f);
        }
    }

    #endregion

    #region Special Ability

    private IEnumerator PulsoCarnal()
    {
        if (_isDead) yield break;

        _isUsingSpecialAbility = true;

        if (_animCtrl != null) _animCtrl.isWalking = true;

        yield return MoveToCenter(_roomCenter);

        if (_navMeshAgent != null)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
        }

        if (_animCtrl != null) _animCtrl.isWalking = false;
        if (_animCtrl != null) _animCtrl.PlayPulpo();

        float pulpoAnticipationTimeout = pulsoCarnalAnticipationDuration + 2f;
        float pulpoAnticipationSafetyTimer = 0f;
        while (!_isInAnticipation && pulpoAnticipationSafetyTimer < pulpoAnticipationTimeout)
        {
            pulpoAnticipationSafetyTimer += Time.deltaTime;
            yield return null;
        }
        yield return new WaitUntil(() => !_isInAnticipation);

        yield return WaitForAnimEvent(ANIM_EVENT_PULSO_CARNAL_IMPACT, _pulseDelay);

        Vector3 groundPos = GetGroundPosition(transform.position);

        if (_headsTransform != null)
        {
            yield return AnimateHeadDown();
        }

        if (_nervesVisualizationPrefab != null)
        {
            GameObject pulseObj = Instantiate(_nervesVisualizationPrefab, groundPos, Quaternion.identity);
            FleshPulseController pulseController = pulseObj.GetComponent<FleshPulseController>();

            if (pulseController != null)
            {
                pulseController.Initialize(
                    _roomMaxRadius,
                    _pulseExpansionDuration,
                    _pulseDamage,
                    _pulseSlowPercentage,
                    _pulseSlowDuration
                );
            }

            _instantiatedEffects.Add(pulseObj);
        }

        yield return new WaitForSeconds(_pulseExpansionDuration + _pulseWaitDuration);

        if (_headsTransform != null)
        {
            StartCoroutine(AnimateHeadUp());
        }

        ShakeCamera(_shakeDuration, _amplitude, _frequency);

        if (pulseAttackSFX != null)
        {
            AudioSource.PlayClipAtPoint(pulseAttackSFX, transform.position);
        }

        if (_crackEffectPrefab != null)
        {
            GameObject crackEffect = Instantiate(_crackEffectPrefab, groundPos, Quaternion.identity, null);
            _instantiatedEffects.Add(crackEffect);
            Destroy(crackEffect, 2f);
        }

        if (_animCtrl != null) _animCtrl.ReturnToIdle();

        yield return new WaitForSeconds(1f);

        _isUsingSpecialAbility = false;
        _currentState = BossState.Moving;

        if (_isDead) yield break;

        StartCombatLoop();
    }

    private IEnumerator MoveToCenter(Vector3 targetCenter)
    {
        if (_navMeshAgent == null || !_navMeshAgent.isOnNavMesh) yield break;

        _navMeshAgent.isStopped = false;
        _navMeshAgent.speed = _movementSpeedForPulse;
        _navMeshAgent.SetDestination(targetCenter);

        float safetyTimer = 0f;

        while (_navMeshAgent.pathPending || _navMeshAgent.remainingDistance > _navMeshAgent.stoppingDistance)
        {
            if (_navMeshAgent.remainingDistance == float.PositiveInfinity) break;

            safetyTimer += Time.deltaTime;
            if (safetyTimer >= 5f) break;

            yield return null;
        }

        _navMeshAgent.speed = _originalSpeed;
    }

    private IEnumerator AnimateHeadDown()
    {
        if (_headsTransform == null) yield break;

        Quaternion start = _headsTransform.localRotation;
        Quaternion target = start * Quaternion.Euler(_headDownRotationAngle, 0, 0);

        float elapsed = 0f;

        while (elapsed < _headAnimationDuration)
        {
            elapsed += Time.deltaTime;
            _headsTransform.localRotation = Quaternion.Slerp(start, target, elapsed / _headAnimationDuration);
            yield return null;
        }

        _headsTransform.localRotation = target;
    }

    private IEnumerator AnimateHeadUp()
    {
        if (_headsTransform == null) yield break;

        Quaternion start = _headsTransform.localRotation;
        Quaternion target = Quaternion.identity;

        float elapsed = 0f;

        while (elapsed < _headAnimationDuration)
        {
            elapsed += Time.deltaTime;
            _headsTransform.localRotation = Quaternion.Slerp(start, target, elapsed / _headAnimationDuration);
            yield return null;
        }

        _headsTransform.localRotation = target;
    }

    private void CalculateRoomRadius()
    {
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 10f, LayerMask.GetMask("Ground")))
        {
            _roomCenter = hit.collider.bounds.center;
            _roomCenter.y = transform.position.y;

            float calculatedRadius = Mathf.Max(hit.collider.bounds.extents.x, hit.collider.bounds.extents.z);

            if (_calculateRoomRadiusOnStart)
            {
                _roomMaxRadius = Mathf.Max(5f, calculatedRadius - 2f);
            }
        }
        else
        {
            _roomCenter = transform.position;

            if (_calculateRoomRadiusOnStart)
            {
                _roomMaxRadius = 25f;
            }
        }
    }

    #endregion

    #region Defensive Stomp

    private void StartStompPullVFX()
    {
        if (_stompPullVFX == null) return;

        float verticalOffset = 0.05f;
        int groundLayer = LayerMask.GetMask("Ground");
        Vector3 pos = transform.position;
        Vector3 rayOrigin = new Vector3(pos.x, pos.y + 50f, pos.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f, groundLayer))
        {
            pos.y = hit.point.y + verticalOffset;
        }

        // _activeStompPullVFX = Instantiate(_stompPullVFX, pos, Quaternion.identity);
        _stompPullVFX.transform.position = pos;
        _stompPullVFX.Stop();
        _stompPullVFX.Play();
    }

    private void StopStompPullVFX()
    {
        if (_stompPullVFX == null) return;

        // ParticleSystem ps = _activeStompPullVFX.GetComponent<ParticleSystem>();
        // if (ps == null) ps = _activeStompPullVFX.GetComponentInChildren<ParticleSystem>();

        // if (ps != null)
        // {
        //     VFXHelper.StopAndDestroy(ps);
        // }
        // else
        // {
        //     Destroy(_activeStompPullVFX);
        // }

        // _activeStompPullVFX = null;

        _stompPullVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void InterruptAndPerformStomp()
    {
        _stompProximityTimer = 0f;

        PrepareCombatInterrupt();
        DestroyAllInstantiatedEffects();
        ResetSmashVisuals();

        _currentState = BossState.Attacking;
        _isStomping = true;
        _stompTimer = _stompCooldown;

        StartCoroutine(DefensiveStompSequence());
    }

    private IEnumerator DefensiveStompSequence()
    {
        if (_isDead) yield break;

        LookAtPlayer();

        if (_animCtrl != null) _animCtrl.isWalking = false;
        if (_animCtrl != null) _animCtrl.PlayApisonador();

        UpdateStompIndicators();

        //CAMBIO EN EL INICIO DEL PULL, ELIMINAR SI LORD DISEÑO NO ESTÁ DE ACUERDO ATT:Tu marido.com
        SetStompIndicatorsActive(true);
        Coroutine pullCoroutine = StartCoroutine(PullPlayersToStompCenter());
        //------------------------------------------------------

        float stompAnticipationTimeout = apisonadorAnticipationDuration + 2f;
        float stompAnticipationSafetyTimer = 0f;
        while (!_isInAnticipation && stompAnticipationSafetyTimer < stompAnticipationTimeout)
        {
            stompAnticipationSafetyTimer += Time.deltaTime;
            yield return null;
        }
        yield return new WaitUntil(() => !_isInAnticipation);

        ////Inicio original del pull-----------------------------------------
        // SetStompIndicatorsActive(true);
        // Coroutine pullCoroutine = StartCoroutine(PullPlayersToStompCenter());

        yield return WaitForAnimEvent(ANIM_EVENT_APISONADOR_IMPACT, 0.8f);

        PerformStompImpact();

        if (pullCoroutine != null)
        {
            StopCoroutine(pullCoroutine);
            if (_stompPullVFX != null)
            {
                StopStompPullVFX();
            }
        }
        SetStompIndicatorsActive(false);

        yield return new WaitForSeconds(0.5f);

        if (_animCtrl != null) _animCtrl.ReturnToIdle();

        yield return new WaitForSeconds(0.15f);

        _isStomping = false;
        _currentState = BossState.Moving;

        if (_navMeshAgent != null && _navMeshAgent.enabled)
        {
            _navMeshAgent.isStopped = false;
        }

        _combatPatternStep = CombatPatternStep.Whip;
        _skipNextCombatLoopDelay = true;

        StartCombatLoop();
    }

    private void SetStompIndicatorsActive(bool active)
    {
        if (_stompPullIndicatorObject != null)
        {
            _stompPullIndicatorObject.SetActive(active);
        }

        if (_stompImpactIndicatorObject != null)
        {
            _stompImpactIndicatorObject.SetActive(active);
        }
    }

    private void UpdateStompIndicators()
    {
        Vector3 groundPosition = GetGroundPosition(transform.position);
        groundPosition.y += 0.03f;

        if (_stompPullIndicatorObject != null)
        {
            _stompPullIndicatorObject.transform.position = groundPosition;
            _stompPullIndicatorObject.transform.localScale = GetIndicatorScaleFromRadius(_stompPullRadius);
        }

        if (_stompImpactIndicatorObject != null)
        {
            _stompImpactIndicatorObject.transform.position = groundPosition + Vector3.up * 0.01f;
            _stompImpactIndicatorObject.transform.localScale = GetIndicatorScaleFromRadius(_stompRadius);
        }

        // if (_activeStompPullVFX != null)
        // {
        //     _activeStompPullVFX.transform.position = groundPosition;
        // }

        if (_stompPullVFX != null)
        {
            _stompPullVFX.transform.position = groundPosition;
        }
    }

    private Vector3 GetIndicatorScaleFromRadius(float radius)
    {
        return new Vector3(radius * 2f, 0.05f, radius * 2f);
    }

    private IEnumerator PullPlayersToStompCenter()
    {
        float elapsed = 0f;

        StartStompPullVFX();

        while (elapsed < _stompPullDuration)
        {
            UpdateStompIndicators();

            Collider[] colliders = Physics.OverlapSphere(
                transform.position,
                _stompPullRadius,
                LayerMask.GetMask("Player")
            );

            foreach (Collider col in colliders)
            {
                GameObject target = col.transform.root != null
                    ? col.transform.root.gameObject
                    : col.gameObject;

                PullPlayerTowardStompCenter(target);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopStompPullVFX();
    }

    private void PullPlayerTowardStompCenter(GameObject target)
    {
        if (target == null) return;

        PlayerMovement playerMove = target.GetComponent<PlayerMovement>();
        if (playerMove != null && playerMove.IsDashing) return;

        Vector3 targetCenter = transform.position;
        targetCenter.y = target.transform.position.y;

        Vector3 direction = targetCenter - target.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f) return;

        Vector3 displacement = direction.normalized * (_stompPullSpeed * Time.deltaTime);

        if (displacement.magnitude > direction.magnitude)
        {
            displacement = direction;
        }

        if (playerMove != null)
        {
            playerMove.MoveCharacter(displacement);
            return;
        }

        CharacterController cc = target.GetComponent<CharacterController>();
        if (cc != null && cc.enabled)
        {
            cc.Move(displacement);
            return;
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.MovePosition(rb.position + displacement);
        }
    }

    private void PerformStompImpact()
    {
        if (audioSource != null && stompSFX != null)
        {
            audioSource.PlayOneShot(stompSFX);
        }

        Vector3 groundPosition = GetGroundPosition(transform.position);
        groundPosition.y += 0.03f;

        if (_stompVFXPrefab != null)
        {
            GameObject stompVFX = Instantiate(_stompVFXPrefab, groundPosition, Quaternion.identity);
            stompVFX.transform.localScale = GetIndicatorScaleFromRadius(_stompRadius);
            Destroy(stompVFX, 2f);
        }

        ShakeCamera(0.3f, 2f, 2f);

        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            _stompRadius,
            LayerMask.GetMask("Player")
        );

        foreach (Collider col in colliders)
        {
            GameObject target = col.transform.root != null
                ? col.transform.root.gameObject
                : col.gameObject;

            if (_enableStompDamage)
            {
                ExecuteAttack(target, transform.position, _stompDamage);
            }

            ApplyKnockback(target.transform, _stompKnockbackForce);
        }
    }

    #endregion
}