using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            RespawnManager.Instance.RespawnPlayer();
        }

        //else if (other.CompareTag("Enemy"))
        //{
        //    Destroy(other.gameObject);
        //}
    }
}