using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

public class witnessAnimCtrl : MonoBehaviour
{
    Animator anim;

    static readonly string ANIM_HIT_PREFIX = "A_hit";
    static readonly string ANIM_IDLE = "A_idle";

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    #if UNITY_EDITOR
    void Update()
    {
        if(SceneManager.GetActiveScene().name == "AndreiNew")
        testinput();
    }
    #endif

    public void PlayHit()
    {
        int r = Random.Range(1, 3);
        // if (anim.Play(ANIM_HIT_PREFIX+ r.ToString()) != )
        anim.Play(ANIM_HIT_PREFIX+ r.ToString());
    }

    void testinput()
    {
        if (Input.GetKeyDown(KeyCode.K)) PlayHit();
    }
}
