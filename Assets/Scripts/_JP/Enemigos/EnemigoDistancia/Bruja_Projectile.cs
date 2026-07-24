using UnityEngine;

public class Bruja_Projectile : MonoBehaviour
{
    public float speed = 12f;
    public int damage = 1;
    public float lifeTime = 5f;
    public LayerMask collisionLayers;
    Vector3 direction;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    public void SetDirection(Vector3 dir)
    {
        direction = dir.normalized;
    }

    void FixedUpdate()
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.position += direction * speed * Time.fixedDeltaTime;
            transform.forward = direction;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if ((collisionLayers.value & (1 << other.gameObject.layer)) > 0)
        {
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Player"))
        {
            // Intentamos obtener PlayerHealth directamente o en los padres
            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (health == null)
            {
                health = other.GetComponentInParent<PlayerHealth>();
            }

            if (health != null)
            {
                // TakeDamage recibe float, casteamos el int damage a float
                health.TakeDamage((float)damage);
            }
            else
            {
                Debug.LogWarning("Bruja_Projectile: no se encontró PlayerHealth en el objeto con tag Player (" + other.name + ").");
            }

            Destroy(gameObject);
            return;
        }

        // Si golpea algo sólido (no trigger), destruir la bala
        if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}




//using UnityEngine;

//public class Bruja_Projectile : MonoBehaviour
//{
//    public float speed = 12f;
//    public int damage = 1;
//    public float lifeTime = 5f;
//    Vector3 direction;

//    void Start()
//    {
//        Destroy(gameObject, lifeTime);
//    }

//    public void SetDirection(Vector3 dir)
//    {
//        direction = dir.normalized;
//    }

//    void FixedUpdate()
//    {
//        if (direction.sqrMagnitude > 0.001f)
//        {
//            transform.position += direction * speed * Time.fixedDeltaTime;
//            transform.forward = direction;
//        }
//    }

//    void OnTriggerEnter(Collider other)
//    {
//        // suponer que el player tiene tag "Player" y un script que maneja recibo de danio
//        if (other.CompareTag("Player"))
//        {
//            //// intentar mandar mensaje al player
//            //other.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
//            Destroy(gameObject);
//        }
//        //else
//        //{
//        //    // si golpea algo solido, destruir
//        //    if (!other.isTrigger)
//        //    {
//        //        Destroy(gameObject);
//        //    }
//        //}
//    }
//}
