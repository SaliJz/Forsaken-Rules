using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif
using UnityEngine.VFX;

public class TheHostAnimCtrl : MonoBehaviour
{
    #region Referencias

    [Header("Referencias")]
    Animator anim;

    #endregion

    #region Inspector - Buffer Settings

    [Tooltip("Estos valor tambien puede ser llamado desde otro script para calzar con la anticipación real del boss")]
    [Header("Buffer Attack Settings")]
    [SerializeField] float _bufferAnticipationTime = 1f;
    [SerializeField] float _bufferDuration = 3f;
    [SerializeField] float bufferShakeIntensity = 3f;
    [SerializeField] VisualEffect VFX_GroundBreak;

    #endregion

    #region  Inspector - Teleport Settings

    [Header("TP Settings")]
    [Tooltip("Este valor tambien puede ser llamado desde otro script para calzar con la anticipación real del boss")]
    [SerializeField] float _tpAnticipationTime = 1f;
    [SerializeField] AnimationCurve tpCurve;

    #endregion

    #region Internal Properties

    bool _isWalking;
    Vector3 originalScale;
    static readonly string ANIM_BUFFER_PRE = "A_Buffer_Pre";
    static readonly string ANIM_TP_PRE = "A_Tp_Pre";
    static readonly string ANIM_TP_IMPACT = "A_Tp_Impact";
    static readonly string ANIM_BUFFER_ATTACK = "A_Buffer";
    static readonly int SHOT_TRIGGER = Animator.StringToHash("ShotAtk");
    static readonly int ATK_BLOQ = Animator.StringToHash("AtkBloq");

    private Coroutine tpReescalationCoroutine;
    private bool isAnimationPaused = false;
    private float speedBeforePause = 1f;

    #endregion

    #region public Actions
    public bool IsWalking
    {
        get => _isWalking;
        set
        {
            _isWalking = value;
            anim.SetBool("Walking", _isWalking);
        }
    }

    public float TPanitipacionTime
    {
        get => _tpAnticipationTime;
        set => _tpAnticipationTime = value;
    }

    public float BufferAnticipationTime
    {
        get => _bufferAnticipationTime;
        set => _bufferAnticipationTime = value;
    }

    public float BufferDuration
    {
        get => _bufferDuration;
        set => _bufferDuration = value;
    }

    #endregion

    #region Unity Lifetime

    private void Awake()
    {
        anim = GetComponent<Animator>();
        originalScale = transform.localScale;
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (SceneManager.GetActiveScene().name == "AndreiNew")
            testInputs();
    }
#endif

    #endregion

    #region Public Methods

    public void PlayBufferPre()
    {
        ReestoreScale();
        anim.Play(ANIM_BUFFER_PRE);
    }

    public void PlayBufferAttack()
    {
        ReestoreScale();
        anim.Play(ANIM_BUFFER_ATTACK);
        StartCoroutine(UnBloqBufferAttack());
    }

    public void PlayShotAttack()
    {
        ReestoreScale();
        anim.SetTrigger(SHOT_TRIGGER);
    }

    public void PlayTP_pre()
    {
        anim.Play(ANIM_TP_PRE);
        tpReescalationCoroutine = StartCoroutine(TP_Rescalation());
    }

    public void PlayTP_Impact()
    {
        ReestoreScale();
        anim.Play(ANIM_TP_IMPACT);
    }

    /// <summary>
    /// Congela el animator en su pose actual.
    /// </summary>
    public void PauseAnimation()
    {
        if (anim == null || isAnimationPaused) return;
        speedBeforePause = anim.speed;
        anim.speed = 0f;
        isAnimationPaused = true;
    }

    /// <summary>
    /// Restaura la velocidad del animator tras la pausa de anticipación.
    /// </summary>
    public void ResumeAnimation()
    {
        if (anim == null || !isAnimationPaused) return;
        anim.speed = speedBeforePause;
        isAnimationPaused = false;
    }

    #endregion

    private IEnumerator TP_Rescalation()
    {
        float t = 0;

        while (t < _tpAnticipationTime)
        {
            t += Time.deltaTime;

            float easedT = tpCurve.Evaluate(t / _tpAnticipationTime);

            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, easedT);
            yield return null;
        }

    }

    private void ReestoreScale()
    {
        if (tpReescalationCoroutine == null) return;

        StopCoroutine(tpReescalationCoroutine);
        tpReescalationCoroutine = null;

        transform.localScale = originalScale;
    }

    private IEnumerator UnBloqBufferAttack()
    {
        anim.SetBool(ATK_BLOQ, true);

        float t = 0;

        Vector3 originalPos = transform.localPosition;

        while (t < _bufferAnticipationTime)
        {
            t += Time.deltaTime;
            float randomX = Random.Range(-bufferShakeIntensity, bufferShakeIntensity);
            float randomY = Random.Range(-bufferShakeIntensity, bufferShakeIntensity);
            float RandomZ = Random.Range(-bufferShakeIntensity, bufferShakeIntensity);

            transform.localPosition = originalPos + new Vector3(randomX, randomY, RandomZ);
            yield return null;
        }

        transform.localPosition = originalPos;

        anim.SetBool(ATK_BLOQ, false);
    }

    //EVENTOS PARA EL EFFECTO DE ROCAS DEL BUFFER/EMBESTIDA
    public void OnPlayBufferVFX()
    {
        VFX_GroundBreak.Play();
    }

    public void OnStopBufferVFX()
    {
        VFX_GroundBreak.Stop();
    }

    #region Testing
    private void testInputs()
    {
        if (Input.GetKeyDown(KeyCode.J)) PlayBufferPre();
        if (Input.GetKeyDown(KeyCode.K)) PlayBufferAttack();
        if (Input.GetKeyDown(KeyCode.L)) PlayShotAttack();
        if (Input.GetKeyDown(KeyCode.O)) PlayTP_pre();
        if (Input.GetKeyDown(KeyCode.I)) PlayTP_Impact();
        if (Input.GetKeyDown(KeyCode.Space)) IsWalking = true;
        else if (Input.GetKeyUp(KeyCode.Space)) IsWalking = false;
    }

    #endregion
}