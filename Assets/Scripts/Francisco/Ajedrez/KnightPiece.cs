using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ChessPieceAttack))]
public class KnightPiece : BoardPiece
{
    #region Enums
    public enum AIState { Patrolling, Alerted, Committed, Moving }
    #endregion

    #region Variables
    [Header("Knight Settings")]
    [SerializeField] private float gizmoHeight = 1.5f;
    [SerializeField] private float gizmosAnchor = 0.7f;
    [SerializeField] private float gizmosLenght = 1.2f;
    [SerializeField] private Transform playerTransform;

    [Header("Movement")]
    [SerializeField] private float patrolMoveSpeed = 10f;
    [SerializeField] private float attackMoveSpeed = 16f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private float attackWindupTime = 1f;

    private AIState currentState = AIState.Patrolling;
    private Vector3 currentTargetWorldPos;
    private Vector2Int committedTargetCoord;
    private Coroutine brainCoroutine;
    private ChessPieceAttack attackComponent;

    private List<Vector2Int> patrolPath = new List<Vector2Int>();

    private readonly Vector2Int[] knightMoves = new Vector2Int[]
    {
        new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(-2, 1), new Vector2Int(-2, -1),
        new Vector2Int(1, 2), new Vector2Int(1, -2), new Vector2Int(-1, 2), new Vector2Int(-1, -2)
    };
    #endregion

    #region Unity Events
    private void Start()
    {
        attackComponent = GetComponent<ChessPieceAttack>();
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        AutoConnectToNearestBoard();
        if (boardManager != null)
            brainCoroutine = StartCoroutine(KnightBrain());
    }

    private void Update()
    {
        if (!isMoving || isStunned) return;
        LookAtTarget(currentTargetWorldPos, rotationSpeed);
    }
    #endregion

    #region Brain
    private IEnumerator KnightBrain()
    {
        yield return new WaitUntil(() => boardManager != null);
        while (true)
        {
            if (isStunned) { yield return new WaitForSeconds(0.2f); continue; }
            if (isMoving) { yield return new WaitForSeconds(0.1f); continue; }

            Vector2Int playerCoord = boardManager.WorldPosToCoord(playerTransform.position);
            bool playerIsOnBoard = boardManager.TileExists(playerCoord);

            if (!playerIsOnBoard)
            {
                currentState = AIState.Patrolling;
                yield return StartCoroutine(PatrolRoutine());
                continue;
            }

            bool canAttack = playerIsOnBoard && CanSeePlayer(playerCoord);
            bool isNearby = playerIsOnBoard && IsPlayerNearby(playerTransform);

            switch (currentState)
            {
                case AIState.Patrolling:
                    if (canAttack)
                    {
                        committedTargetCoord = playerCoord;
                        currentState = AIState.Committed;
                        yield return StartCoroutine(AttackWindup());
                    }
                    else if (isNearby) currentState = AIState.Alerted;
                    else yield return StartCoroutine(PatrolRoutine());
                    break;

                case AIState.Alerted:
                    if (canAttack)
                    {
                        committedTargetCoord = playerCoord;
                        currentState = AIState.Committed;
                        yield return StartCoroutine(AttackWindup());
                    }
                    else if (!isNearby) currentState = AIState.Patrolling;
                    else
                    {
                        Vector2Int nextMove = GetBestMoveTowards(playerCoord);
                        if (nextMove != currentCoord)
                            yield return StartCoroutine(MoveRoutine(nextMove, patrolMoveSpeed, true));
                    }
                    break;

                case AIState.Committed:
                    currentState = isNearby ? AIState.Alerted : AIState.Patrolling;
                    break;
            }
            yield return new WaitForSeconds(detectionInterval * 0.5f);
        }
    }
    #endregion

    #region Movement & L-Logic
    private IEnumerator MoveRoutine(Vector2Int targetCoord, float speed, bool interruptForAttack = false)
    {
        if (!boardManager.TileExists(targetCoord) || boardManager.IsTileOccupied(targetCoord)) yield break;

        Vector2Int intermediateCoord = GetIntermediateLPoint(currentCoord, targetCoord);
        if (!boardManager.TileExists(intermediateCoord)) yield break;

        isMoving = true;

        Vector3 intermediatePos = boardManager.GetWorldPosFromCoord(intermediateCoord);
        Vector3 finalPos = boardManager.GetWorldPosFromCoord(targetCoord);
        intermediatePos.y = transform.position.y;
        finalPos.y = transform.position.y;

        currentTargetWorldPos = intermediatePos;

        while (Vector3.Distance(transform.position, intermediatePos) > 0.05f)
        {
            if (isStunned) { isMoving = false; yield break; }

            transform.position = Vector3.MoveTowards(
                transform.position,
                intermediatePos,
                speed * Time.deltaTime
            );

            yield return null;
        }

        transform.position = intermediatePos;
        UpdateBoardPosition(intermediateCoord);
        isMoving = false;

        if (interruptForAttack && ShouldInterruptForAttack())
            yield break;

        isMoving = true;
        currentTargetWorldPos = finalPos;

        while (Vector3.Distance(transform.position, finalPos) > 0.05f)
        {
            if (isStunned) { isMoving = false; yield break; }

            transform.position = Vector3.MoveTowards(
                transform.position,
                finalPos,
                speed * Time.deltaTime
            );

            yield return null;
        }

        transform.position = finalPos;
        UpdateBoardPosition(targetCoord);
        isMoving = false;

        if (interruptForAttack && ShouldInterruptForAttack())
            yield break;
    }

    private bool ShouldInterruptForAttack()
    {
        if (playerTransform == null || boardManager == null) return false;

        Vector2Int playerCoord = boardManager.WorldPosToCoord(playerTransform.position);
        return boardManager.TileExists(playerCoord) && CanSeePlayer(playerCoord);
    }

    private Vector2Int GetIntermediateLPoint(Vector2Int start, Vector2Int end)
    {
        Vector2Int diff = end - start;
        if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y))
            return new Vector2Int(start.x + diff.x, start.y);
        else
            return new Vector2Int(start.x, start.y + diff.y);
    }
    #endregion

    #region Trail Logic
    private void ShowKnightLPath(Vector2Int from, Vector2Int to)
    {
        Vector2Int intermediate = GetIntermediateLPoint(from, to);

        Vector3 startPos = boardManager.GetWorldPosFromCoord(from) + Vector3.up * trailHeight;
        Vector3 midPos = boardManager.GetWorldPosFromCoord(intermediate) + Vector3.up * trailHeight;
        Vector3 endPos = boardManager.GetWorldPosFromCoord(to) + Vector3.up * trailHeight;

        SetTrailPoints(new Vector3[] { startPos, midPos, endPos });
    }
    #endregion

    #region Attack & Patrol
    private IEnumerator AttackWindup()
    {
        ShowKnightLPath(currentCoord, committedTargetCoord); 

        Vector3 targetWorldPos = boardManager.GetWorldPosFromCoord(committedTargetCoord);
        targetWorldPos.y = transform.position.y;

        float elapsed = 0f;
        while (elapsed < attackWindupTime)
        {
            if (isStunned) { HideTrail(); yield break; }
            LookAtTarget(targetWorldPos, rotationSpeed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 attackDirection = (targetWorldPos - transform.position).normalized;

        currentState = AIState.Moving;
        attackComponent.SetAttacking(true);
        yield return StartCoroutine(MoveRoutine(committedTargetCoord, attackMoveSpeed));
        attackComponent.SetAttacking(false);

        HideTrail();
        attackComponent.ForceHitCheck(attackDirection);
        currentState = AIState.Patrolling;
    }

    private IEnumerator PatrolRoutine()
    {
        if (patrolPath.Count == 0)
            GenerateLongPatrolPath();

        while (patrolPath.Count > 0)
        {
            Vector2Int nextCoord = patrolPath[0];

            if (!boardManager.TileExists(nextCoord) || boardManager.IsTileOccupied(nextCoord))
            {
                patrolPath.Clear();
                break;
            }

            Vector2Int intermediate = GetIntermediateLPoint(currentCoord, nextCoord);
            if (!boardManager.TileExists(intermediate))
            {
                patrolPath.Clear();
                break;
            }

            patrolPath.RemoveAt(0);
            yield return StartCoroutine(MoveRoutine(nextCoord, patrolMoveSpeed, true));
            yield return new WaitForSeconds(detectionInterval);

            Vector2Int playerCoord = boardManager.WorldPosToCoord(playerTransform.position);
            if (boardManager.TileExists(playerCoord) && (CanSeePlayer(playerCoord) || IsPlayerNearby(playerTransform)))
                yield break;
        }
    }

    private void GenerateLongPatrolPath()
    {
        patrolPath.Clear();
        Vector2Int current = currentCoord;
        int pathLength = Random.Range(6, 12);

        for (int i = 0; i < pathLength; i++)
        {
            var validMoves = new List<Vector2Int>();
            foreach (var m in knightMoves)
            {
                Vector2Int next = current + m;
                if (boardManager.TileExists(next) && !boardManager.IsTileOccupied(next))
                {
                    Vector2Int intermediate = GetIntermediateLPoint(current, next);
                    if (boardManager.TileExists(intermediate))
                        validMoves.Add(next);
                }
            }

            if (validMoves.Count == 0) break;

            Vector2Int chosen = validMoves[Random.Range(0, validMoves.Count)];
            patrolPath.Add(chosen);
            current = chosen;
        }
    }
    #endregion

    #region Detection
    public override bool CanSeePlayer(Vector2Int playerCoord)
    {
        Vector2Int diff = playerCoord - currentCoord;
        bool isKnightMove = false;
        foreach (var m in knightMoves) if (m == diff) isKnightMove = true;

        if (!isKnightMove) return false;

        Vector2Int intermediate = GetIntermediateLPoint(currentCoord, playerCoord);
        return boardManager.TileExists(intermediate);
    }

    private Vector2Int GetBestMoveTowards(Vector2Int target)
    {
        var best = new List<Vector2Int>();
        float minDist = float.MaxValue;

        foreach (var m in knightMoves)
        {
            Vector2Int test = currentCoord + m;

            if (!boardManager.TileExists(test) || boardManager.IsTileOccupied(test)) continue;

            Vector2Int intermediate = GetIntermediateLPoint(currentCoord, test);
            if (!boardManager.TileExists(intermediate)) continue;

            float dist = Vector2Int.Distance(test, target);
            if (dist < minDist) { minDist = dist; best.Clear(); best.Add(test); }
        }
        return best.Count > 0 ? best[Random.Range(0, best.Count)] : currentCoord;
    }
    #endregion

    #region Overrides & Gizmos
    public override void OnStunEnd()
    {
        isStunned = false;
        isMoving = false;
        HideTrail();
        patrolPath.Clear();
        if (brainCoroutine != null) StopCoroutine(brainCoroutine);
        brainCoroutine = StartCoroutine(KnightBrain());
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (!showGizmos || boardManager == null) return;
        Gizmos.color = new Color(1, 1, 1, 0.1f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Color stateColor = isStunned ? new Color(1f, 0.9f, 0f, 0.4f) :
            currentState == AIState.Moving ? new Color(1f, 0f, 0f, 0.6f) :
            currentState == AIState.Committed ? new Color(1f, 0.4f, 0f, 0.5f) :
            currentState == AIState.Alerted ? new Color(1f, 0.8f, 0f, 0.4f) : new Color(0f, 1f, 0f, 0.3f);
        Gizmos.color = stateColor;
        Gizmos.DrawCube(transform.position + Vector3.up * gizmoHeight, new Vector3(gizmosAnchor, gizmosLenght, gizmosAnchor));
    }
    #endregion
}