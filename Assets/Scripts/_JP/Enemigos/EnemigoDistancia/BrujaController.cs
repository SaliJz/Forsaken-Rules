// Bruja_Controller.cs (versión completa usando los helpers para no sobrescribir destinos)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Coordina movimiento, disparo y reaccion a golpes (modificado para NavMesh y linea de vista)
public class Bruja_Controller : MonoBehaviour
{
    public Bruja_Movement movimiento;
    public Bruja_Shooter shooter;
    public HealthController salud; // <- ahora usa el script unificado HealthController

    [Header("Parametros de IA")]
    public float maxAttackRange = 12f;         // distancia maxima desde donde la bruja quiere disparar
    public float keepDistance = 7f;            // distancia que intenta mantener (si el player se acerca, se aleja)
    public float retreatOnInterruptTime = 0.9f; // 
    public float postRecoveredRetreatTime = 1.2f;

    [Header("Busqueda de posicion de flanqueo")]
    public int flankAngleSteps = 12;
    public float flankDistance = 9f;           // distancia objetivo alrededor del player para flanquear
    public float flankSampleRadius = 2f;       // variacion para buscar posiciones navegables

    // Nueva variable para el retardo
    [Header("Retardo de reaccion")]
    public float retreatReactionDelay = 0.5f;

    bool isOverwhelmed = false;
    Coroutine aiRoutine;

    private float timePlayerBecameTooClose = -1f;

    void Start()
    {
        if (movimiento == null) movimiento = GetComponent<Bruja_Movement>();
        if (shooter == null) shooter = GetComponent<Bruja_Shooter>();
        if (salud == null) salud = GetComponent<HealthController>(); // <- obtén HealthController

        // registrar callback (comprobando null)
        if (salud != null)
        {
            salud.OnDamaged += HandleDamaged;
            salud.OnInterrupted += HandleInterrupted;
            salud.OnOverwhelmed += HandleOverwhelmed;
            salud.OnRecovered += HandleRecovered;
        }
        else
        {
            Debug.LogWarning($"{name}: HealthController (salud) no encontrado en el GameObject.");
        }

        // pasar offset de firepoint al movimiento si existe (para simulaciones de LOS futuras)
        if (shooter != null && movimiento != null && shooter.firePoint != null)
        {
            Vector3 offset = shooter.firePoint.position - transform.position;
            movimiento.SetFirePointOffset(offset);
        }

        aiRoutine = StartCoroutine(AILoop());
    }

    void OnDestroy()
    {
        if (salud != null)
        {
            salud.OnDamaged -= HandleDamaged;
            salud.OnInterrupted -= HandleInterrupted;
            salud.OnOverwhelmed -= HandleOverwhelmed;
            salud.OnRecovered -= HandleRecovered;
        }

        if (aiRoutine != null) StopCoroutine(aiRoutine);
    }

    IEnumerator AILoop()
    {
        while (true)
        {
            if (isOverwhelmed)
            {
                // no ataca, se queda quieta (segun pedido)
                if (movimiento != null) movimiento.StopMoving();
                if (shooter != null) shooter.StopAttackCycle();
                if (movimiento != null) movimiento.FacePlayer();
                yield return null;
                continue;
            }

            float dist = (movimiento != null) ? movimiento.DistanceToPlayer() : Mathf.Infinity;

            // Obtener referencia segura al transform del player (preferir movimiento.player, si no shooter.player)
            Transform playerT = null;
            if (movimiento != null && movimiento.player != null) playerT = movimiento.player;
            else if (shooter != null && shooter.player != null) playerT = shooter.player;

            // si el player esta muy cerca -> alejarse
            if (dist < keepDistance)
            {
                if (shooter != null) shooter.StopAttackCycle();

                if (timePlayerBecameTooClose < 0f)
                {
                    timePlayerBecameTooClose = Time.time;
                }

                if (Time.time >= timePlayerBecameTooClose + retreatReactionDelay)
                {
                    // Solo pedir alejamiento si no estamos ya navegando hacia una posición que nos aleje.
                    Vector3 desiredAway = transform.position;
                    if (playerT != null)
                    {
                        Vector3 awayDir = (transform.position - playerT.position).normalized;
                        if (awayDir.sqrMagnitude < 0.001f) awayDir = transform.forward;
                        desiredAway = transform.position + awayDir * (movimiento != null ? movimiento.retreatDistance : 1f);
                    }
                    // Si no sabemos la posicion del player, igual pedimos el movimiento normal de alejamiento
                    if (movimiento != null && !movimiento.IsNavigatingTo(desiredAway, 0.7f))
                    {
                        movimiento.MoveAwayFromPlayer();
                    }
                    else if (movimiento != null && playerT == null)
                    {
                        movimiento.MoveAwayFromPlayer();
                    }
                }
            }
            else
            {
                // si el player se aleja, reseteamos el tiempo
                timePlayerBecameTooClose = -1f;

                // fuera de keepDistance
                if (dist > maxAttackRange)
                {
                    if (shooter != null) shooter.StopAttackCycle();

                    // Evitar reasignar destino si ya vamos hacia el player (si tenemos transform del player)
                    if (playerT != null)
                    {
                        if (movimiento != null && !movimiento.IsNavigatingTo(playerT.position, 0.6f))
                        {
                            movimiento.MoveTowardsPlayer();
                        }
                    }
                    else
                    {
                        // si no sabemos player transform, intentar de todos modos
                        if (movimiento != null) movimiento.MoveTowardsPlayer();
                    }
                }
                else
                {
                    // dentro de rango: preferimos atacar si hay linea de vista
                    if (shooter != null && shooter.HasLineOfSight())
                    {
                        if (movimiento != null) movimiento.FacePlayer();
                        shooter.StartAttackCycle();
                    }
                    else
                    {
                        if (shooter != null) shooter.StopAttackCycle();
                        Vector3 flankPos;
                        if (FindFlankPosition(out flankPos))
                        {
                            // Evitar reasignar destino si ya vamos a ese flankPos
                            if (movimiento != null && !movimiento.IsNavigatingTo(flankPos, 0.5f))
                            {
                                movimiento.MoveToPosition(flankPos);
                            }
                        }
                        else
                        {
                            // si no conseguimos posicion de flanqueo, acercarnos para intentar obtener LOS
                            if (playerT != null)
                            {
                                if (movimiento != null && !movimiento.IsNavigatingTo(playerT.position, 0.6f))
                                {
                                    movimiento.MoveTowardsPlayer();
                                }
                            }
                            else
                            {
                                if (movimiento != null) movimiento.MoveTowardsPlayer();
                            }
                        }
                    }
                }
            }
            yield return new WaitForFixedUpdate();
        }
    }

    // Buscar una posicion alrededor del player que este en NavMesh y desde la cual la bruja tendria linea de vista
    bool FindFlankPosition(out Vector3 result)
    {
        result = transform.position;
        if (movimiento == null || shooter == null) return false;
        Transform playerTransform = (shooter.player != null) ? shooter.player : movimiento.player;
        if (playerTransform == null) return false;

        Vector3 fireOffset = (shooter.firePoint != null) ? (shooter.firePoint.position - transform.position) : Vector3.zero;

        // Búsqueda de posiciones de flanqueo en un círculo alrededor del player
        for (int i = 0; i < flankAngleSteps; i++)
        {
            float angle = (360f / flankAngleSteps) * i;
            Vector3 desiredFlankDir = Quaternion.Euler(0, angle, 0) * (playerTransform.position - transform.position).normalized;
            Vector3 desiredFlankPos = playerTransform.position + desiredFlankDir * flankDistance;

            Vector3 navFlankPos;
            if (movimiento.IsNavPositionReachable(desiredFlankPos, out navFlankPos))
            {
                // simular la posicion con el offset del firepoint
                Vector3 simulatedFirePos = navFlankPos + fireOffset;
                // si desde alli tendria LOS
                if (shooter.HasLineOfSightFrom(simulatedFirePos))
                {
                    result = navFlankPos;
                    return true;
                }
            }
        }

        return false;
    }

    // si recibe cualquier golpe
    void HandleDamaged()
    {
        // placeholder para FX o sonido
    }

    // cuando se interrumpe (por ejemplo durante un ataque)
    void HandleInterrupted()
    {
        if (shooter != null)
        {
            shooter.CancelCurrentAttack();
        }
        if (movimiento != null)
        {
            movimiento.RetreatForSeconds(retreatOnInterruptTime, 1.2f);
        }
    }

    void HandleOverwhelmed()
    {
        isOverwhelmed = true;
        if (shooter != null) shooter.StopAttackCycle();
        if (movimiento != null) movimiento.StopMoving();
    }

    void HandleRecovered()
    {
        isOverwhelmed = false;
        if (movimiento != null)
        {
            movimiento.RetreatForSeconds(postRecoveredRetreatTime, 1f);
        }
        // si la IA esta en ciclo de ataque, se reanuda
        if (shooter != null) shooter.StartAttackCycle();
    }

    void OnDrawGizmos()
    {
        if (movimiento != null && movimiento.player != null)
        {
            // dibujar linea de KeepDistance
            Color c = Color.yellow;
            c.a = 0.5f;
            Gizmos.color = c;
            Gizmos.DrawWireSphere(movimiento.player.position, keepDistance);

            // dibujar linea de MaxAttackRange
            c = Color.red;
            c.a = 0.5f;
            Gizmos.color = c;
            Gizmos.DrawWireSphere(movimiento.player.position, maxAttackRange);

            // dibujar linea de FlankDistance
            c = Color.cyan;
            c.a = 0.5f;
            Gizmos.color = c;
            Gizmos.DrawWireSphere(movimiento.player.position, flankDistance);
        }
    }
}



//// Bruja_Controller.cs
//using System.Collections;
//using UnityEngine;
//using UnityEngine.AI;

//// Coordina movimiento, disparo y reaccion a golpes (modificado para NavMesh y linea de vista)
//public class Bruja_Controller : MonoBehaviour
//{
//    public Bruja_Movement movimiento;
//    public Bruja_Shooter shooter;
//    public HealthController salud; // <- ahora usa el script unificado HealthController

//    [Header("Parametros de IA")]
//    public float maxAttackRange = 12f;         // distancia maxima desde donde la bruja quiere disparar
//    public float keepDistance = 7f;            // distancia que intenta mantener (si el player se acerca, se aleja)
//    public float retreatOnInterruptTime = 0.9f;
//    public float postRecoveredRetreatTime = 1.2f;

//    [Header("Busqueda de posicion de flanqueo")]
//    public int flankAngleSteps = 12;
//    public float flankDistance = 9f;           // distancia objetivo alrededor del player para flanquear
//    public float flankSampleRadius = 2f;       // variacion para buscar posiciones navegables

//    bool isOverwhelmed = false;
//    Coroutine aiRoutine;

//    void Start()
//    {
//        if (movimiento == null) movimiento = GetComponent<Bruja_Movement>();
//        if (shooter == null) shooter = GetComponent<Bruja_Shooter>();
//        if (salud == null) salud = GetComponent<HealthController>(); // <- obtén HealthController

//        // registrar callback (comprobando null)
//        if (salud != null)
//        {
//            salud.OnDamaged += HandleDamaged;
//            salud.OnInterrupted += HandleInterrupted;
//            salud.OnOverwhelmed += HandleOverwhelmed;
//            salud.OnRecovered += HandleRecovered;
//        }
//        else
//        {
//            Debug.LogWarning($"{name}: HealthController (salud) no encontrado en el GameObject.");
//        }

//        // pasar offset de firepoint al movimiento si existe (para simulaciones de LOS futuras)
//        if (shooter != null && movimiento != null && shooter.firePoint != null)
//        {
//            Vector3 offset = shooter.firePoint.position - transform.position;
//            movimiento.SetFirePointOffset(offset);
//        }

//        aiRoutine = StartCoroutine(AILoop());
//    }

//    void OnDestroy()
//    {
//        if (salud != null)
//        {
//            salud.OnDamaged -= HandleDamaged;
//            salud.OnInterrupted -= HandleInterrupted;
//            salud.OnOverwhelmed -= HandleOverwhelmed;
//            salud.OnRecovered -= HandleRecovered;
//        }
//    }

//    IEnumerator AILoop()
//    {
//        while (true)
//        {
//            if (isOverwhelmed)
//            {
//                // no ataca, se queda quieta (segun pedido)
//                if (movimiento != null) movimiento.StopMoving();
//                if (shooter != null) shooter.StopAttackCycle();
//                if (movimiento != null) movimiento.FacePlayer();
//                yield return null;
//                continue;
//            }

//            float dist = (movimiento != null) ? movimiento.DistanceToPlayer() : Mathf.Infinity;

//            // si el player esta muy cerca -> alejarse
//            if (dist < keepDistance)
//            {
//                if (shooter != null) shooter.StopAttackCycle();
//                if (movimiento != null) movimiento.MoveAwayFromPlayer();
//            }
//            else
//            {
//                // si esta fuera de rango de ataque, acercarse lo suficiente para tener direccion
//                if (dist > maxAttackRange)
//                {
//                    if (shooter != null) shooter.StopAttackCycle();
//                    if (movimiento != null) movimiento.MoveTowardsPlayer();
//                }
//                else
//                {
//                    // dentro de rango: preferimos atacar si hay linea de vista
//                    if (shooter != null && shooter.HasLineOfSight())
//                    {
//                        // orientarse y empezar ciclo de ataque
//                        if (movimiento != null) movimiento.FacePlayer();
//                        shooter.StartAttackCycle();
//                    }
//                    else
//                    {
//                        // si no hay linea de vista, intentar flanquear buscando una posicion navegable que tenga LOS
//                        if (shooter != null) shooter.StopAttackCycle();

//                        Vector3 flankPos;
//                        if (FindFlankPosition(out flankPos))
//                        {
//                            if (movimiento != null) movimiento.MoveToPosition(flankPos);
//                        }
//                        else
//                        {
//                            // si no conseguimos posicion de flanqueo, acercarnos para intentar obtener LOS
//                            if (movimiento != null) movimiento.MoveTowardsPlayer();
//                        }
//                    }
//                }
//            }

//            yield return new WaitForFixedUpdate();
//        }
//    }

//    // Buscar una posicion alrededor del player que este en NavMesh y desde la cual la bruja tendria linea de vista
//    bool FindFlankPosition(out Vector3 result)
//    {
//        result = transform.position;
//        if (movimiento == null || shooter == null || shooter.player == null) return false;
//        Transform player = shooter.player;
//        Vector3 fireOffset = (shooter.firePoint != null) ? (shooter.firePoint.position - transform.position) : Vector3.zero;

//        for (int i = 0; i < flankAngleSteps; i++)
//        {
//            float angle = (360f / flankAngleSteps) * i;
//            Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
//            Vector3 candidate = player.position + dir * flankDistance;

//            // variar la altura/offset para samplear mejor la navmesh
//            Vector3 sampleTarget = candidate;

//            // samplear navmesh cerca del candidato
//            NavMeshHit hit;
//            if (NavMesh.SamplePosition(sampleTarget, out hit, flankSampleRadius, NavMesh.AllAreas))
//            {
//                Vector3 navPos = hit.position;

//                // simular desde esa posicion si la bruja (mas el offset del firepoint) tendria LOS
//                Vector3 simulatedFirePos = navPos + fireOffset;

//                if (Vector3.Distance(simulatedFirePos, player.position) > maxAttackRange * 1.2f)
//                {
//                    // si desde alli esta demasiado lejos, ignorar
//                    continue;
//                }

//                if (shooter.HasLineOfSightFrom(simulatedFirePos))
//                {
//                    result = navPos;
//                    return true;
//                }
//            }
//        }

//        return false;
//    }

//    // si recibe cualquier golpe
//    void HandleDamaged()
//    {
//        // placeholder para FX o sonido
//    }

//    // cuando se interrumpe (por ejemplo durante un ataque)
//    void HandleInterrupted()
//    {
//        if (shooter != null)
//        {
//            shooter.CancelCurrentAttack();
//        }
//        if (movimiento != null)
//        {
//            movimiento.RetreatForSeconds(retreatOnInterruptTime, 1.2f);
//        }
//    }

//    void HandleOverwhelmed()
//    {
//        isOverwhelmed = true;
//        if (shooter != null) shooter.StopAttackCycle();
//        if (movimiento != null) movimiento.StopMoving();
//    }

//    void HandleRecovered()
//    {
//        isOverwhelmed = false;
//        if (movimiento != null) movimiento.RetreatForSeconds(postRecoveredRetreatTime, 1.1f);
//    }
//}













