using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class AporiaAnimCtrl : BaseAnimCtrl<AporiaAnimCtrl.ActionState>
{
    public enum ActionState
    {
        idle, run, dash, attack, death, damage
    }

    [Header("State Flags")]
    public bool damageActive;
    public bool isDashing;

    [Header("Settings & VFX")]
    private float dashTimer;
    public float dashDuration = 0.5f;
    [SerializeField] private GameObject AttackVFX;
    [SerializeField] private GameObject ImpactVFX;
    public float h, v; //CONECTAR A VELOCIDAD DEL ENEMIGO
    // Vector2 nextdirecion;

    [Header("Anticipation Shake")]
    [SerializeField] private float shakeIntensity = 0.2f;
    [SerializeField] private float shakeFrequency = 2f;

    private SpriteAnimator spriteAnimator;
    private Coroutine shakeCoroutine;
    private Vector3 originalLocalPosition;
    private bool originalPositionCaptured = false;

    protected override void Start()
    {
        base.Start();

        spriteAnimator = GetComponent<SpriteAnimator>();
        originalLocalPosition = transform.localPosition;
        originalPositionCaptured = true;
        PlayState(ActionState.run, AnimPriority.locomotion);
    }

    private void Update()
    {
        string directionBeforeUpdate = currentDirection;

        #if UNITY_EDITOR
        if(SceneManager.GetActiveScene().name == "AndreiNew") handleTESTimputs();
        #endif

        UpdateDirection(h,v);

        UpdateDashLogic();

        if(currentPriority <= AnimPriority.locomotion)
        {
            bool hasInput = (h != 0 || v != 0);
            bool directionChanged = currentDirection != directionBeforeUpdate;

            if (hasInput)
            {
                if (directionChanged || currentState != ActionState.run)
                {
                    PlayState(ActionState.run, AnimPriority.locomotion, false);
                }
            }
            else
            {
                PlayState(ActionState.idle, AnimPriority.locomotion, false);
            }
        }
        
    }

    #region Public Actions

    private void PlayAttack()
    {
        PlayState(ActionState.attack, AnimPriority.attack);
    }

    public void PlayDash()
    {
        if(isDashing) return;
        isDashing = true;
        dashTimer = dashDuration;
        PlayState(ActionState.dash, AnimPriority.dash);
    }

    private void EndDash()
    {
        isDashing = false;
        damageActive = false;
        currentPriority = AnimPriority.none;
        PlayState(ActionState.run, AnimPriority.locomotion);
    }

    public void PlayDeath()
    {
        PlayState(ActionState.death, AnimPriority.bind);
    }

    public void PlayDamage()
    {
        PlayState(ActionState.damage, AnimPriority.damage);
    }

    #endregion

    protected override string ResolveFullID(string baseID, string direction)
    {
        return baseID;
    }

    #region Event Void

    protected override void OnAnimationEvent(string ev)
    {
        switch (ev)
        {
            case "SpawnVFX": 
                if (AttackVFX != null) AttackVFX.SetActive(true); break;
            case "ImpactVFX": 
            {
                if(ImpactVFX != null)
                {
                    ImpactVFX.SetActive(false);
                    ImpactVFX.SetActive(true); 
                }
                break;
            }
        }
    }

    #endregion

    protected override void OnFinishedAnimation()
    {
        if(currentPriority == AnimPriority.dash) return;
        PlayState(ActionState.run, AnimPriority.locomotion);
    }

    private bool UpdateDashLogic()
    {
        if(!isDashing) return false;

        dashTimer -= Time.deltaTime;
        if(dashTimer <= 0) EndDash();
        return true;
    }

    public void PauseAnimation()
    {
        if (spriteAnimator != null) spriteAnimator.paused = true;
    }

    public void ResumeAnimation()
    {
        if (spriteAnimator != null) spriteAnimator.paused = false;
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

    private void handleTESTimputs()
    {
        h = Input.GetAxisRaw("Horizontal");
        v = Input.GetAxisRaw("Vertical");

        if(Input.GetKeyDown(KeyCode.Space)) PlayDash();
        if(Input.GetKeyDown(KeyCode.G)) PlayAttack();
        if(Input.GetKeyDown(KeyCode.L)) PlayDamage();
    }

    public override void PlayState(ActionState state, AnimPriority priority, bool reset = true)
    {
        if(state == ActionState.attack && currentPriority == AnimPriority.dash)
        {
            isDashing = false;
            dashTimer = 0;
            currentPriority = AnimPriority.none;
        }
        if(state == ActionState.damage && currentPriority != AnimPriority.damage)
        {
            currentPriority = AnimPriority.none;
        }

        base.PlayState(state, priority, reset);
    }
}