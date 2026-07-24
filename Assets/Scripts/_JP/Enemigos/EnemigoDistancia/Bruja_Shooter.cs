using System.Collections;
using System.Reflection;
using UnityEngine;

public class Bruja_Shooter : MonoBehaviour
{
    public Transform player;
    public Transform firePoint;              // punto desde donde salen los proyectiles (child)
    public GameObject projectilePrefab;
    public float projectileSpeed = 12f;

    [Header("Ciclo de ataque")]
    public float windupTime = 0.6f;          // tiempo de carga antes de disparar (se puede interrumpir)
    public float timeBetweenShots = 0.25f;
    public float attackCooldown = 1.2f;      // tiempo entre ataques completos
    public float maxAttackRange = 12f;       // rango maximo para considerar ataque (horizontal)

    [Header("Ráfaga")]
    public int burstCount = 3;               // número de proyectiles por ráfaga (ahora configurable)

    [Header("Busqueda de direccion al obviar paredes")]
    public int sampleAngleSteps = 12;        // cuantas direcciones probar alrededor del jugador
    public float sampleRadiusMin = 0.8f;
    public float sampleRadiusMax = 2.2f;
    public int sampleRadiusSteps = 2;

    // Referencia opcional al componente de movimiento para pausar/reanudar
    public Bruja_Movement movement;

    Coroutine attackRoutine;
    bool isAttacking = false;
    bool cancelAttack = false;

    // Distancia para "sacar" el spawn del proyectil fuera del collider del shooter
    [Header("Ajustes de spawn")]
    public float spawnOffset = 0.6f;

    void Awake()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (firePoint == null) firePoint = transform;

        // Auto-resuelva la referencia a Bruja_Movement si está en el mismo GameObject
        if (movement == null)
        {
            movement = GetComponent<Bruja_Movement>();
        }
    }

    void Update()
    {
        // orientacion pasiva: mantenemos una rotacion suave hacia el player en el plano horizontal
        if (player != null)
        {
            Vector3 dir = new Vector3(player.position.x - transform.position.x, 0f, player.position.z - transform.position.z);
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion target = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 8f * Time.deltaTime);
            }
        }
    }

    public void StartAttackCycle()
    {
        if (attackRoutine == null)
        {
            attackRoutine = StartCoroutine(AttackCycle());
        }
    }

    public void StopAttackCycle()
    {
        cancelAttack = true;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttacking = false;
        cancelAttack = false;

        // Asegurar que el movimiento se restaure si lo pausamos
        movement?.ResumeMovement();
    }

    public void CancelCurrentAttack()
    {
        // llamada externa para interrumpir ataque (por ejemplo al recibir danio)
        cancelAttack = true;
        // reanudar movimiento inmediatamente
        movement?.ResumeMovement();
    }

    IEnumerator AttackCycle()
    {
        while (true)
        {
            isAttacking = true;
            cancelAttack = false;

            // comprobar que hay una direccion valida antes del windup (horizontal)
            Vector3 chosenDir = FindBestAimDirection();
            if (chosenDir == Vector3.zero)
            {
                // no hay direccion valida (pared bloqueando) -> no atacar ahora
                isAttacking = false;
                attackRoutine = null;
                movement?.ResumeMovement();
                yield break;
            }

            // ----- PAUSAR MOVIMIENTO ANTES DEL WINDUP/DISPAROS -----
            movement?.PauseMovement();

            // windup (se puede cancelar)
            float t = 0f;
            while (t < windupTime)
            {
                if (cancelAttack)
                {
                    isAttacking = false;
                    cancelAttack = false;
                    attackRoutine = null;
                    movement?.ResumeMovement();
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }

            // Ejecutar UNA ráfaga usando la variable burstCount y esperar a que termine (puede ser interrumpida)
            yield return StartCoroutine(ShootBurst(chosenDir, Mathf.Max(1, burstCount)));

            // ----- TERMINÓ DE DISPARAR -> reanudar movimiento -----
            movement?.ResumeMovement();

            isAttacking = false;

            // cooldown antes del siguiente ataque
            float cd = 0f;
            while (cd < attackCooldown)
            {
                if (cancelAttack)
                {
                    cancelAttack = false;
                    attackRoutine = null;
                    movement?.ResumeMovement();
                    yield break;
                }
                cd += Time.deltaTime;
                yield return null;
            }
            // mantener attackRoutine activo para continuar el ciclo
        }
    }

    // Corrutina que dispara una ráfaga de `burstCount` proyectiles en la misma dirección horizontal.
    IEnumerator ShootBurst(Vector3 direction, int burstCountLocal)
    {
        if (projectilePrefab == null || firePoint == null || player == null) yield break;
        if (direction.sqrMagnitude < 0.0001f) yield break;

        for (int i = 0; i < burstCountLocal; i++)
        {
            if (cancelAttack)
            {
                // si se cancela, salir inmediatamente
                cancelAttack = false;
                yield break;
            }

            SpawnProjectile(direction);

            // si es el último de la ráfaga, no esperar
            if (i < burstCountLocal - 1)
            {
                // esperar entre disparos de la ráfaga
                float wait = 0f;
                while (wait < timeBetweenShots)
                {
                    if (cancelAttack)
                    {
                        cancelAttack = false;
                        yield break;
                    }
                    wait += Time.deltaTime;
                    yield return null;
                }
            }
        }
    }

    // Crea un proyectil único y le aplica velocidad; también ignora colisiones contra el shooter para evitar autodestrucción.
    void SpawnProjectile(Vector3 direction)
    {
        // direction ya es horizontal (y == 0). Asegurar spawn con la misma Y del firePoint.
        Vector3 horizontalDir = direction.normalized;
        Vector3 spawnPos = new Vector3(firePoint.position.x, firePoint.position.y, firePoint.position.z) + horizontalDir * spawnOffset;

        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(horizontalDir));
        // buscar Rigidbody tanto en root como en hijos (más robusto)
        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb == null) rb = proj.GetComponentInChildren<Rigidbody>();

        if (rb != null)
        {
            // evitar que la gravedad haga caer la bala (si quieres gravedad, cambia a true)
            rb.useGravity = false;
            // velocidad solo en plano horizontal
            Vector3 vel = new Vector3(horizontalDir.x, 0f, horizontalDir.z) * projectileSpeed;
            // <-- Uso seguro compatible con distintas versiones de Unity:
            SetRigidbodyVelocity(rb, vel);
        }

        Bruja_Projectile pscript = proj.GetComponent<Bruja_Projectile>();
        if (pscript != null)
        {
            // mantener dirección horizontal
            pscript.SetDirection(new Vector3(horizontalDir.x, 0f, horizontalDir.z));
        }

        // IGNORAR colisiones entre la bala y el shooter (evita que la bala choque al nacer si tiene isTrigger/colisiones)
        Collider[] myCols = GetComponentsInChildren<Collider>();
        Collider[] projCols = proj.GetComponentsInChildren<Collider>();
        foreach (var pc in projCols)
        {
            if (pc == null) continue;
            foreach (var mc in myCols)
            {
                if (mc == null) continue;
                Physics.IgnoreCollision(pc, mc, true);
            }
        }

        // Nota: si usas pooling, reemplaza Instantiate por tu sistema de pool y re-aplica la ignoración de colisión según convenga.
    }

    // Asigna la "velocidad lineal" del Rigidbody de forma compatible:
    // - intenta usar la propiedad/field `linearVelocity` si existe (evita el warning en Unity nuevo)
    // - si no existe, usa la propiedad tradicional `velocity`
    void SetRigidbodyVelocity(Rigidbody rb, Vector3 velocity)
    {
        if (rb == null) return;

        var type = rb.GetType();
        // intentar propiedad
        var prop = type.GetProperty("linearVelocity", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            // si el tipo coincide con Vector3
            if (prop.PropertyType == typeof(Vector3))
            {
                prop.SetValue(rb, velocity, null);
                return;
            }
        }

        // intentar campo
        var field = type.GetField("linearVelocity", BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            if (field.FieldType == typeof(Vector3))
            {
                field.SetValue(rb, velocity);
                return;
            }
        }

        // fallback clásico (funcionará en versiones donde velocity aún existe)
        // rb.velocity = velocity; // <-- LÍNEA OBSOLETA
        rb.linearVelocity = velocity; // <-- USAR LA NUEVA PROPIEDAD
    }

    // Comprueba si desde el firePoint actual hay linea de vista directa al player (en plano horizontal)
    public bool HasLineOfSight()
    {
        if (player == null || firePoint == null) return false;
        return HasLineOfSightFrom(firePoint.position);
    }

    // Comprueba linea de vista desde una posicion arbitraria (usa Y del origin para mantener plano)
    public bool HasLineOfSightFrom(Vector3 origin)
    {
        if (player == null) return false;

        Vector3 originFlat = new Vector3(origin.x, origin.y, origin.z);
        Vector3 playerFlat = new Vector3(player.position.x, origin.y, player.position.z); // misma Y del origin
        Vector3 dir = playerFlat - originFlat;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;

        RaycastHit hit;
        if (Physics.Raycast(originFlat, dir.normalized, out hit, dist))
        {
            if (hit.transform == player || hit.transform.IsChildOf(player))
            {
                return true;
            }
            return false; // algo bloquea antes del player (pared u otro obstaculo)
        }

        return true;
    }

    // Intenta encontrar una direccion alternativa hacia el jugador "obviando" la pared:
    // busca puntos alrededor del player y elige el primero que sea visible desde el firePoint
    public Vector3 FindBestAimDirection()
    {
        if (player == null || firePoint == null) return Vector3.zero;

        // primer intento: direccion directa en plano horizontal
        Vector3 directDir = new Vector3(player.position.x - firePoint.position.x, 0f, player.position.z - firePoint.position.z);
        if (directDir.magnitude <= maxAttackRange && HasLineOfSightFrom(firePoint.position))
        {
            return directDir.normalized;
        }

        // samplear puntos alrededor del player para encontrar un punto de su area visible (todos con la misma Y del firePoint)
        for (int r = 0; r < sampleRadiusSteps; r++)
        {
            float radius = Mathf.Lerp(sampleRadiusMin, sampleRadiusMax, (float)r / Mathf.Max(1, sampleRadiusSteps - 1));
            for (int i = 0; i < sampleAngleSteps; i++)
            {
                float angle = (360f / sampleAngleSteps) * i;
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
                Vector3 candidatePoint = new Vector3(player.position.x + offset.x, firePoint.position.y, player.position.z + offset.z);

                // limitar rango: solo aceptar si el candidato esta razonablemente cerca (horizontalmente)
                float candDist = Vector3.Distance(new Vector3(firePoint.position.x, firePoint.position.y, firePoint.position.z), candidatePoint);
                if (candDist > maxAttackRange * 1.2f) continue;

                Vector3 dirToCandidate = candidatePoint - new Vector3(firePoint.position.x, firePoint.position.y, firePoint.position.z);
                float dist = dirToCandidate.magnitude;
                if (dist < 0.01f) continue;

                RaycastHit hit;
                if (Physics.Raycast(new Vector3(firePoint.position.x, firePoint.position.y, firePoint.position.z), dirToCandidate.normalized, out hit, dist))
                {
                    if (hit.transform == player || hit.transform.IsChildOf(player))
                    {
                        return dirToCandidate.normalized;
                    }
                    // bloqueado -> seguir probando
                }
                else
                {
                    // ningun hit entre firePoint y candidato -> aceptamos
                    return dirToCandidate.normalized;
                }
            }
        }

        // si no se encontro ninguna direccion valida, devolver Vector3.zero
        return Vector3.zero;
    }

    public bool IsAttacking()
    {
        return isAttacking;
    }
}


