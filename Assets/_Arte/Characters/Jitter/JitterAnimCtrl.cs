using UnityEngine;

public class JitterAnimCtrl : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator anim;
    [SerializeField] private SkinnedMeshRenderer mesh;
    private MaterialPropertyBlock propBlock;

    [Header("State Bools")]
    public bool isWalking; //CONECTAR AL BOOL EQUIVALENTE DEL ENEMIGO
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private bool isOnFuryMode = false;

    [Header("HeartBeat Settings")]
    [SerializeField] private float normalFrecuency = 1.5f;
    [SerializeField] private float furyFrecuency = 4f;
    [SerializeField] private float pulseValue = 8f;

    [Header("Anticipation Shake")]
    [SerializeField] private float shakeIntensity = 0.2f;
    [SerializeField] private float shakeFrequency = 2f;

    [Header("VFX Settings")]
    [SerializeField] ParticleSystem[] HandSlashes;
    [SerializeField] ParticleSystem AttackImpactVFX;

    private Coroutine shakeCoroutine;
    private Vector3 originalLocalPosition;
    private bool originalPositionCaptured = false;

    #region Unity Lifecycle
    private void Awake()
    {
        anim = GetComponent<Animator>();
        propBlock = new MaterialPropertyBlock();

        originalLocalPosition = transform.localPosition;
        originalPositionCaptured = true;
    }

    private void Update()
    {
        HandleMovement();
        BeatRythim();

        if(isOnFuryMode) ElectricPulse();
        else ElectricityReset();

        #if UNITY_EDITOR
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "AndreiNew") testImputs();
        #endif
    }

    #endregion

    private void HandleMovement()
    {
        anim.SetBool(IsWalkingHash, isWalking);
    }

    private void BeatRythim()
    {
        float frecuency = isOnFuryMode? furyFrecuency: normalFrecuency;
        float baseWave = Mathf.Sin(Time.time * frecuency * Mathf.PI);
        float pulse = Mathf.Pow(baseWave, pulseValue);

        mesh.SetBlendShapeWeight(0, pulse * 100);
    }

    private void ElectricPulse()
    {
        float pulse = Mathf.PingPong(Time.time * pulseValue, 1f);

        mesh.GetPropertyBlock(propBlock, 1);
        propBlock.SetFloat("_Amount", pulse);
        mesh.SetPropertyBlock(propBlock, 1);
    }

    private void ElectricityReset()
    {
        mesh.GetPropertyBlock(propBlock, 1);
        if (propBlock.GetFloat("_Amount") > 0f)
        {
            propBlock.SetFloat("_Amount", 0);
            mesh.SetPropertyBlock(propBlock, 1);
        }

    }

    #region Public actions

    public void SetFuryMode (bool active)
    {
        if (isOnFuryMode == active) return;
        
        isOnFuryMode = active;
        anim.SetBool("RageMode", isOnFuryMode);

        Debug.Log(isOnFuryMode);

        if (isOnFuryMode)
        {
            anim.Play("Rage Transform");
        }
    }

    public void PlayAttack()
    {
        anim.SetTrigger("Attack");
    }

    public void PlayDamage()
    {
        anim.Play("Damage", 0, 0);
    }

    public void PlayDeath()
    {
        anim.SetTrigger("Death");
    }

    public void PauseAnimation()
    {
        if (anim != null) anim.speed = 0f;
    }

    public void ResumeAnimation()
    {
        if (anim != null) anim.speed = 1f;
    }

    public void PlayAnticipationShake(float duration)
    {
        StopAnticipationShake();
        shakeCoroutine = StartCoroutine(ShakeRoutine(duration));
    }

    public void StopAnticipationShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        if (originalPositionCaptured)
        {
            transform.localPosition = originalLocalPosition;
        }
    }

    private System.Collections.IEnumerator ShakeRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float offsetX = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f) * shakeIntensity;
            float offsetZ = Mathf.Cos(elapsed * shakeFrequency * Mathf.PI * 2.3f) * shakeIntensity * 0.6f;
            transform.localPosition = originalLocalPosition + new Vector3(offsetX, 0f, offsetZ);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = originalLocalPosition;
        shakeCoroutine = null;
    }

    #endregion    

    #region VFX Handlers

    public void ActivateNormalAttackVFX()
    {
        // if (!HandSlashes[0].isPlaying) 
        HandSlashes[0].Stop();
        HandSlashes[0].Play();
        // Debug.Log($"Playing slash: {HandSlashes[0]}");
    }

    public void ActivateFuryAttackVFX()
    {
        for(int i = 0; i < HandSlashes.Length; i++)
        {
            // if(!HandSlashes[i].isPlaying) 
            HandSlashes[i].Stop();
            HandSlashes[i].Play();
            // Debug.Log($"Playing slash: {HandSlashes[i]}");
        }
    }

    public void DeactivateSlashes()
    {
        for(int i = 0; i < HandSlashes.Length; i++)
        {
            if(!HandSlashes[i].isStopped) HandSlashes[i].Stop();
            // Debug.Log("Ana Stop");
        }
    }

    public void PlayImpactVFX()
    {
        if(AttackImpactVFX == null) return;
        AttackImpactVFX.Stop();
        AttackImpactVFX.Play();
    }

    #endregion

    #if UNITY_EDITOR
    #region testing

    private void testImputs()
    {
        if (Input.GetKeyDown(KeyCode.W)) isWalking = true;
        if (Input.GetKeyUp(KeyCode.W)) isWalking = false;

        if (Input.GetKeyDown(KeyCode.Q) && isOnFuryMode == false) SetFuryMode(true);
        if (Input.GetKeyDown(KeyCode.E) && isOnFuryMode == true) SetFuryMode(false);

        if (Input.GetKeyDown(KeyCode.M)) PlayAttack();
        if (Input.GetKeyDown(KeyCode.J)) PlayDamage();

        if (Input.GetKeyDown(KeyCode.K)) PlayDeath();
    }

    #endregion
    #endif
}
