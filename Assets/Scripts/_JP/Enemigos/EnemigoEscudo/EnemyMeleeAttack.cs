// EnemyMeleeAttack.cs
using System.Collections;
using UnityEngine;

/// <summary>
/// Spawnea el prefab melee; si se asigna spawnParent, el prefab se hace hijo y el hitbox seguirá la posición.
/// </summary>
public class EnemyMeleeAttack : MonoBehaviour
{
    [Header("Prefab / Spawn")]
    [Tooltip("Prefab que contiene el collider (isTrigger) y MeleeHitbox.cs")]
    public GameObject meleePrefab;

    [Tooltip("Transform padre opcional: si se asigna, el hitbox será creado como hijo y seguirá su posición")]
    public Transform spawnParent;

    [Tooltip("Distancia desde el centro del enemigo donde se instancia el hitbox")]
    public float spawnDistance = 1.3f;

    [Header("Timing / Rango")]
    public float attackInterval = 1.5f;
    public float attackRange = 2f;

    [Header("Propiedades por defecto (se pasan al hitbox si no se sobrescriben)")]
    public float defaultDamage = 10f;
    public float defaultLifetime = 0.45f;

    private float lastAttackTime = -999f;

    public bool TryAttack(Transform target)
    {
        if (target == null) return false;
        if (Time.time - lastAttackTime < attackInterval) return false;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > attackRange) return false;

        // si no hay prefab no aplicamos cooldown (antes el cooldown bloqueaba futuros intentos)
        if (meleePrefab == null)
        {
            Debug.LogWarning($"[{name}] EnemyMeleeAttack: meleePrefab no asignado.");
            return false;
        }

        // spawn position delante del enemigo
        Vector3 forward = transform.forward;
        Vector3 spawnPos = transform.position + forward * spawnDistance + Vector3.up * 0.5f;

        // calcular rotación plana hacia el objetivo
        Vector3 dir = (target.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        Quaternion spawnRot = Quaternion.LookRotation(dir);

        GameObject go = Instantiate(meleePrefab, spawnPos, spawnRot);

        // si se asignó spawnParent, hacerlo padre (mantener posición world actual) y pasar followTarget
        if (spawnParent != null)
        {
            go.transform.SetParent(spawnParent, worldPositionStays: true);
        }

        var hitbox = go.GetComponent<MeleeHitbox>();
        if (hitbox != null)
        {
            hitbox.SetOwner(gameObject);
            if (hitbox.damage <= 0f) hitbox.damage = defaultDamage;
            if (hitbox.lifetime <= 0f) hitbox.lifetime = defaultLifetime;

            // si hay spawnParent, pedir al hitbox que copie su posición cada frame
            if (spawnParent != null)
            {
                hitbox.followTarget = spawnParent;
            }
        }

        // si el prefab no contiene MeleeHitbox, lo destruimos tras defaultLifetime
        if (hitbox == null)
        {
            Destroy(go, defaultLifetime);
        }

        lastAttackTime = Time.time;
        return true;
    }

    // helper opcional
    public bool TryAttackAtPosition(Vector3 point)
    {
        if (Time.time - lastAttackTime < attackInterval) return false;
        if (meleePrefab == null) return false;

        GameObject go = Instantiate(meleePrefab, point, Quaternion.identity);
        if (spawnParent != null) go.transform.SetParent(spawnParent, worldPositionStays: true);

        var hitbox = go.GetComponent<MeleeHitbox>();
        if (hitbox != null)
        {
            hitbox.SetOwner(gameObject);
            if (hitbox.damage <= 0f) hitbox.damage = defaultDamage;
            if (hitbox.lifetime <= 0f) hitbox.lifetime = defaultLifetime;
            if (spawnParent != null) hitbox.followTarget = spawnParent;
        }

        lastAttackTime = Time.time;
        return true;
    }
}



//// EnemyMeleeAttack.cs
//using System.Collections;
//using UnityEngine;

///// <summary>
///// Spawnea el prefab melee; si se asigna spawnParent, el prefab se hace hijo y el hitbox seguirá la posición.
///// </summary>
//public class EnemyMeleeAttack : MonoBehaviour
//{
//    [Header("Prefab / Spawn")]
//    [Tooltip("Prefab que contiene el collider (isTrigger) y MeleeHitbox.cs")]
//    public GameObject meleePrefab;

//    [Tooltip("Transform padre opcional: si se asigna, el hitbox será creado como hijo y seguirá su posición")]
//    public Transform spawnParent;

//    [Tooltip("Distancia desde el centro del enemigo donde se instancia el hitbox")]
//    public float spawnDistance = 1.3f;

//    [Header("Timing / Rango")]
//    public float attackInterval = 1.5f;
//    public float attackRange = 2f;

//    [Header("Propiedades por defecto (se pasan al hitbox si no se sobrescriben)")]
//    public float defaultDamage = 10f;
//    public float defaultLifetime = 0.45f;

//    private float lastAttackTime = -999f;

//    public bool TryAttack(Transform target)
//    {
//        if (target == null) return false;
//        if (Time.time - lastAttackTime < attackInterval) return false;

//        float dist = Vector3.Distance(transform.position, target.position);
//        if (dist > attackRange) return false;

//        // spawn position delante del enemigo
//        Vector3 forward = transform.forward;
//        Vector3 spawnPos = transform.position + forward * spawnDistance + Vector3.up * 0.5f;

//        // calcular rotación plana hacia el objetivo (evitar dependencia de extensiones)
//        Vector3 dir = (target.position - transform.position);
//        dir.y = 0f;
//        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
//        Quaternion spawnRot = Quaternion.LookRotation(dir);

//        if (meleePrefab == null)
//        {
//            Debug.LogWarning($"[{name}] EnemyMeleeAttack: meleePrefab no asignado.");
//            lastAttackTime = Time.time;
//            return false;
//        }

//        GameObject go = Instantiate(meleePrefab, spawnPos, spawnRot);

//        // si se asignó spawnParent, hacerlo padre (mantener posición world actual) y pasar followTarget
//        if (spawnParent != null)
//        {
//            go.transform.SetParent(spawnParent, worldPositionStays: true);
//        }

//        var hitbox = go.GetComponent<MeleeHitbox>();
//        if (hitbox != null)
//        {
//            hitbox.SetOwner(gameObject);
//            if (hitbox.damage <= 0f) hitbox.damage = defaultDamage;
//            if (hitbox.lifetime <= 0f) hitbox.lifetime = defaultLifetime;

//            // si hay spawnParent, pedir al hitbox que copie su posición cada frame
//            if (spawnParent != null)
//            {
//                hitbox.followTarget = spawnParent;
//            }
//        }

//        // si el prefab no contiene MeleeHitbox, lo destruimos tras defaultLifetime
//        if (hitbox == null)
//        {
//            Destroy(go, defaultLifetime);
//        }

//        lastAttackTime = Time.time;
//        return true;
//    }

//    // helper opcional
//    public bool TryAttackAtPosition(Vector3 point)
//    {
//        if (Time.time - lastAttackTime < attackInterval) return false;
//        if (meleePrefab == null) return false;

//        GameObject go = Instantiate(meleePrefab, point, Quaternion.identity);
//        if (spawnParent != null) go.transform.SetParent(spawnParent, worldPositionStays: true);

//        var hitbox = go.GetComponent<MeleeHitbox>();
//        if (hitbox != null)
//        {
//            hitbox.SetOwner(gameObject);
//            if (hitbox.damage <= 0f) hitbox.damage = defaultDamage;
//            if (hitbox.lifetime <= 0f) hitbox.lifetime = defaultLifetime;
//            if (spawnParent != null) hitbox.followTarget = spawnParent;
//        }

//        lastAttackTime = Time.time;
//        return true;
//    }
//}
