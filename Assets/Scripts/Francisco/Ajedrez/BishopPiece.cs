using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ChessPieceAttack))]
public class BishopPiece : BoardPiece
{
    #region Enums
    public enum AIState { Patrolling, Alerted, Committed, Moving }
    #endregion

    #region Variables
    [Header("Bishop Settings")]
    [SerializeField] private float gizmoHeight = 1.5f;
    [SerializeField] private float gizmosAnchor = 0.7f;
    [SerializeField] private float gizmosLenght = 1.2f;
    [SerializeField] private Transform playerTransform;

    [Header("Movement")]
    [SerializeField] private float patrolMoveSpeed = 8f;
    [SerializeField] private float attackMoveSpeed = 16f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private int patrolStepDist = 2;
    [SerializeField] private float attackWindupTime = 1f;

    private AIState currentState = AIState.Patrolling;
    private Vector3 currentTargetWorldPos;
    private Vector2Int committedTargetCoord;
    private Coroutine brainCoroutine;
    private ChessPieceAttack attackComponent;

    private List<Vector2Int> patrolPath = new List<Vector2Int>();

    private readonly Vector2Int[] moveDirs = new Vector2Int[]
    {
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1)
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
            brainCoroutine = StartCoroutine(BishopBrain());
    }

    private void Update()
    {
        if (!isMoving || isStunned) return;
        LookAtTarget(currentTargetWorldPos, rotationSpeed);
    }
    #endregion

    #region Brain
    private IEnumerator BishopBrain()
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
                        Vector2Int approach = GetSmartApproach(playerCoord);
                        if (approach != currentCoord)
                            yield return StartCoroutine(MoveRoutine(approach, patrolMoveSpeed, true));
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

    #region Attack Flow
    private IEnumerator AttackWindup()
    {
        ShowPathLine(currentCoord, committedTargetCoord); 

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
    #endregion

    #region Movement
    private IEnumerator MoveRoutine(Vector2Int targetCoord, float speed, bool interruptForAttack = false)
    {
        if (!boardManager.TileExists(targetCoord)) yield break;
        if (boardManager.IsTileOccupied(targetCoord)) yield break;

        Vector2Int diff = targetCoord - currentCoord;
        Vector2Int dir = new Vector2Int(
            diff.x == 0 ? 0 : (int)Mathf.Sign(diff.x),
            diff.y == 0 ? 0 : (int)Mathf.Sign(diff.y));

        if (dir == Vector2Int.zero) yield break;

        while (currentCoord != targetCoord)
        {
            Vector2Int nextCoord = currentCoord + dir;

            if (!boardManager.TileExists(nextCoord)) yield break;
            if (nextCoord != targetCoord && boardManager.IsTileOccupied(nextCoord)) yield break;

            isMoving = true;
            currentTargetWorldPos = boardManager.GetWorldPosFromCoord(nextCoord);
            currentTargetWorldPos.y = transform.position.y;
            UpdateBoardPosition(nextCoord);

            while (Vector3.Distance(transform.position, currentTargetWorldPos) > 0.05f)
            {
                if (isStunned) { isMoving = false; yield break; }

                transform.position = Vector3.MoveTowards(
                    transform.position,
                    currentTargetWorldPos,
                    speed * Time.deltaTime
                );

                yield return null;
            }

            transform.position = currentTargetWorldPos;
            isMoving = false;

            if (interruptForAttack && ShouldInterruptForAttack())
                yield break;
        }
    }

    private bool ShouldInterruptForAttack()
    {
        if (playerTransform == null || boardManager == null) return false;

        Vector2Int playerCoord = boardManager.WorldPosToCoord(playerTransform.position);
        return boardManager.TileExists(playerCoord) && CanSeePlayer(playerCoord);
    }

    private IEnumerator PatrolRoutine()
    {
        if (patrolPath.Count == 0)
            GenerateLongPatrolPath();

        while (patrolPath.Count > 0)
        {
            Vector2Int next = patrolPath[0];
            patrolPath.RemoveAt(0);

            if (!boardManager.TileExists(next) || boardManager.IsTileOccupied(next))
            {
                patrolPath.Clear();
                break;
            }

            yield return StartCoroutine(MoveRoutine(next, patrolMoveSpeed, true));
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
        int segments = Random.Range(3, 7);
        List<Vector2Int> availableDirs = new List<Vector2Int>(moveDirs);
        Vector2Int lastDir = Vector2Int.zero;

        for (int s = 0; s < segments; s++)
        {
            for (int i = availableDirs.Count - 1; i > 0; i--)
            {
                int k = Random.Range(0, i + 1);
                Vector2Int tmp = availableDirs[k];
                availableDirs[k] = availableDirs[i];
                availableDirs[i] = tmp;
            }

            Vector2Int chosenDir = Vector2Int.zero;
            int chosenDist = 0;

            foreach (var dir in availableDirs)
            {
                if (dir == lastDir || dir == -lastDir) continue;

                int maxDist = 0;
                for (int i = 1; i <= 30; i++)
                {
                    Vector2Int test = current + dir * i;
                    if (boardManager.TileExists(test) && !boardManager.IsTileOccupied(test))
                        maxDist = i;
                    else
                        break;
                }

                if (maxDist < 2) continue;

                chosenDir = dir;
                chosenDist = Random.Range(maxDist / 2, maxDist + 1);
                break;
            }

            if (chosenDir == Vector2Int.zero) break;

            Vector2Int waypoint = current + chosenDir * chosenDist;
            patrolPath.Add(waypoint);
            lastDir = chosenDir;
            current = waypoint;
        }
    }
    #endregion

    #region Detection
    public override bool CanSeePlayer(Vector2Int playerCoord)
    {
        Vector2Int diff = playerCoord - currentCoord;
        if (Mathf.Abs(diff.x) != Mathf.Abs(diff.y)) return false;
        Vector2Int dir = new Vector2Int((int)Mathf.Sign(diff.x), (int)Mathf.Sign(diff.y));
        Vector2Int check = currentCoord + dir;
        while (check != playerCoord)
        {
            if (!boardManager.TileExists(check) || boardManager.IsTileOccupied(check)) return false;
            check += dir;
        }
        return true;
    }

    private Vector2Int GetSmartApproach(Vector2Int target)
    {
        var candidates = new List<Vector2Int>();
        float minDist = float.MaxValue;
        foreach (var dir in moveDirs)
        {
            for (int i = 1; i <= 4; i++)
            {
                Vector2Int test = currentCoord + (dir * i);
                if (!boardManager.TileExists(test) || boardManager.IsTileOccupied(test)) break;
                float dist = Vector2Int.Distance(test, target);
                if (Mathf.Abs(test.x - target.x) == Mathf.Abs(test.y - target.y)) dist -= 8f;
                if (dist < minDist) { minDist = dist; candidates.Clear(); candidates.Add(test); }
            }
        }
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : currentCoord;
    }
    #endregion

    #region Trail
    private void ShowPathLine(Vector2Int from, Vector2Int to)
    {
        var points = new List<Vector3>();
        Vector2Int diff = to - from;
        Vector2Int dir = new Vector2Int((int)Mathf.Sign(diff.x), (int)Mathf.Sign(diff.y));
        Vector2Int cur = from;
        while (cur != to)
        {
            points.Add(boardManager.GetWorldPosFromCoord(cur) + Vector3.up * trailHeight);
            cur += dir;
        }
        points.Add(boardManager.GetWorldPosFromCoord(to) + Vector3.up * trailHeight);
        SetTrailPoints(points.ToArray());
    }
    #endregion

    #region Overrides
    public override void OnStunEnd()
    {
        isStunned = false;
        isMoving = false;
        HideTrail();
        patrolPath.Clear();
        if (brainCoroutine != null) StopCoroutine(brainCoroutine);
        brainCoroutine = StartCoroutine(BishopBrain());
    }
    #endregion

    #region Gizmos
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