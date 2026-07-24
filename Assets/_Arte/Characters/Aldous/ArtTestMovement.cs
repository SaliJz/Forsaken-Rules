using UnityEngine;

public class ArtTestMovement : MonoBehaviour
{
    PlayerAnimCtrl anim;
    Rigidbody rb;

    float h;
    float v;
    public float speed = 10;

    void Start()
    {
        rb = gameObject.GetComponent<Rigidbody>();   
        anim = gameObject.GetComponent<PlayerAnimCtrl>();
    }

    void Update()
    {
        h = Input.GetAxisRaw("Horizontal");
        v = Input.GetAxisRaw("Vertical"); 


        Vector3 inputDir = new Vector3(h, 0, v);
        Vector3 rotatedDir;
        if (!anim.isBlocking)
        {
            rotatedDir = Quaternion.Euler(0, 45, 0) * inputDir;
        }
        else
        {
            rotatedDir = Quaternion.Euler(0, 0, 0) * Vector3.zero;
        }


        rb.linearVelocity = rotatedDir * speed * Time.deltaTime * 10;
    }
}
