using UnityEngine;
using System.Collections;

public class VeynarAnimCtrl : MonoBehaviour
{
    [SerializeField] private Animator anim;
    [SerializeField] private SkinnedMeshRenderer mesh;
    [SerializeField] private EscudoController shieldCtrl;
    private MaterialPropertyBlock propBlock;

    #region  private variables

    private const string CamouProperty = "_Camou_Amount";
    private readonly string ANIM_SPAWNING = "A_Spawning";

    private bool _isInvulnerable;
    private bool _isSpawning;

    #endregion

    #region public variables

    public bool isInvulnerable
    {
        get => _isInvulnerable;
        set
        {
            _isInvulnerable = value;
            anim.SetBool("Protected", _isInvulnerable);
        }
    }

    public bool isSpawning
    {
        get => _isSpawning;
        set
        {
            _isSpawning = value;
            anim.SetBool("Spawning",_isSpawning);
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        propBlock = new MaterialPropertyBlock();
    }

    #if UNITY_EDITOR
    private void Update()
    {
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "AndreiNew")
        testInput();
    }
    #endif

    #endregion

    #region Public Methods

    public void UpdateCamou(float value)
    {
        mesh.GetPropertyBlock(propBlock);
        propBlock.SetFloat(CamouProperty, value);
        mesh.SetPropertyBlock(propBlock);
    }

    public void PlayDamage()
    {
        anim.SetTrigger("Damage");
    }

    public void PlayMoveOut()
    {
        anim.Play("Move Out");
        anim.SetBool("IsTraveling", true);
    }

    public void PlayMoveIn()
    {
        anim.Play("Move In");
    }

    public void TravelingFalseEvent()
    {
        anim.SetBool("IsTraveling", false);
    }

    public void PlayDeath()
    {
        anim.SetTrigger("Death");
    }

    public void PlaySpawning()
    {
        anim.Play(ANIM_SPAWNING);
        anim.SetBool("Spawning",true);
    }

    private void StopSpawningBool()
    {
        anim.SetBool("Spawning",false);
    }

    public IEnumerator WaitForMoveOut()
    {
        yield return null;

        AnimatorStateInfo stateInfo;
        do
        {
            stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            yield return null;
        }
        while (stateInfo.IsName("Move Out") && stateInfo.normalizedTime < 1f);
    }

    #endregion

    #region Invulnerability

    public void PlayInvulnerabilityVFX()
    {
        if (shieldCtrl != null) shieldCtrl.Escudo = true;
    }

    #endregion

    #if UNITY_EDITOR
    #region Testing

    private void testInput()
    {
        if (Input.GetKeyDown(KeyCode.K)) PlayDamage();
        if (Input.GetKeyDown(KeyCode.L)) PlayMoveOut();
        if (Input.GetKeyDown(KeyCode.P)) PlayMoveIn();

        if (Input.GetKeyDown(KeyCode.Keypad1)) UpdateCamou(0f);
        if (Input.GetKeyDown(KeyCode.Keypad2)) UpdateCamou(0.5f);
        if (Input.GetKeyDown(KeyCode.Keypad3)) UpdateCamou(1f);

        if(Input.GetKeyDown(KeyCode.D)) PlayDeath();
        if (Input.GetKeyDown(KeyCode.H)) PlaySpawning();
        if(Input.GetKeyDown(KeyCode.O)) isInvulnerable = true;
        else if (Input.GetKeyUp(KeyCode.O)) isInvulnerable = false;

        if(Input.GetKeyDown(KeyCode.Y)) PlayInvulnerabilityVFX();
    }

    #endregion
    #endif
}