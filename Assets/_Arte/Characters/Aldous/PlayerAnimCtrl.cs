using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

public class PlayerAnimCtrl : BaseAnimCtrl<PlayerAnimCtrl.PlayerState>
{
    #region Custom Classes

    public enum PlayerState
    {
        idle, run, dash, distance, melee, damage,
        block, blockfb, store, inventory, bind, death
    }

    public enum Age
    {
        young, adult, old
    }

    [Header("Referencias")]
    [SerializeField] PlayerShaderCtrl shaderCtrl;
    [SerializeField] PlayerVfxCtrl vfxCtrl;
    // [SerializeField] LayerMask groundMask;

    [Header("Player Custom Status")]
    public Age currentAge =Age.adult;
    public Age nextAge;
    public bool HasShield = true;
    private bool currentShield = true;
    public int meleeStep = 1;

    [Header("State Flags")]
    public bool damageActive;
    public bool isDashing;
    public bool isBlocking;

    [Header("Settings & VFX")]
    private float dashTimer;
    public float dashDuration = 0.5f;
    [SerializeField] GameObject[] VFX_meleeBegin;
    [SerializeField] GameObject[] VFX_meleeMid;
    [SerializeField] GameObject[] VFX_meleeLate;
    [SerializeField] ParticleSystem VFX_wallParticles;
    [SerializeField] ParticleSystem VFX_Dash;
    // [SerializeField] GameObject shadowPrefab;

    [Header("Debug")]
    [SerializeField] private bool debug = false;

    private float H, V;
    private bool IsWalking() => H != 0 || V != 0;

    private bool _externalInputThisFrame = false;

    public void SetInputAxes(float h, float v)
    {
        H = h;
        V = v;
        _externalInputThisFrame = true;
    }

    #endregion

    #region Unity Lifecycle

    protected override void Start()
    {
        base.Start();

        nextAge = currentAge;
        currentShield = HasShield;
    }

    private void Update()
    {
        if (DialogManager.Instance != null && DialogManager.Instance.IsActive)
        {
            PlayState(PlayerState.idle, AnimPriority.locomotion, false);
            return;
        }

        if (!_externalInputThisFrame)
        {
            if (PauseController.Instance != null && PauseController.IsGamePaused) return;
            if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;

            H = Input.GetAxisRaw("Horizontal");
            V = Input.GetAxisRaw("Vertical");
            
        }
        _externalInputThisFrame = false;

        UpdateDirection(H,V);
        UpdateAgeState();
        UpdateWalkParticles();
        
        #if UNITY_EDITOR
        if(SceneManager.GetActiveScene().name == "AndreiNew")
        {DebugInputs();}
        #endif

        if (UpdateBlockLogic()) return;
        if (UpdateDashLogic()) return;
        if (isForcedAnim) return;

        HandleLocomotion();
    }

    #endregion

    #region Logica de Estados

    private void HandleLocomotion()
    {
        PlayerState nextState = IsWalking()? PlayerState.run : PlayerState.idle;
        
        bool shieldChanged = HasShield != currentShield;
        bool ageChanged = currentAge != nextAge;

        bool forceRefresh = shieldChanged || ageChanged;

        if (forceRefresh)
        {
            currentShield = HasShield;
            currentAge = nextAge;
        }


        PlayState(nextState, AnimPriority.locomotion, forceRefresh);
    }

    private void UpdateWalkParticles()
    {
         if(VFX_wallParticles != null)
        {
            if (IsWalking() && currentPriority == AnimPriority.locomotion)
            {
                if (!VFX_wallParticles.isPlaying) VFXHelper.SafePlay(VFX_wallParticles);
            } 
            else
            {
                if (!VFX_wallParticles.isStopped) VFXHelper.SafeStop(VFX_wallParticles, false);
            } 
        }
    }

    private void UpdateAgeState()
    {
        if (currentAge == nextAge) return;
        
        currentAge = nextAge;

        AnimPriority refreshPriority; 
        refreshPriority = isForcedAnim? currentPriority : AnimPriority.locomotion;
        PlayState(currentState, refreshPriority);
    }

    private bool UpdateBlockLogic()
    {
        if (!isBlocking) return false;

        // if(lastDirection != currentDirection)

        PlayState(PlayerState.block, AnimPriority.action, false);

        return true;
    }

    private bool UpdateDashLogic()
    {
        if(!isDashing) return false;

        dashTimer -= Time.deltaTime;
        if(dashTimer <= 0) EndDash();
        return true;
    }

    private bool IsLoopingState(PlayerState state)
    {
        return state == PlayerState.idle || state == PlayerState.run;
    }

    public void ResetToGameplayState()
    {
        isForcedAnim = false;
        currentPriority = AnimPriority.none;

        damageActive = false;
        isDashing = false;
        isBlocking = false;

        PlayState(PlayerState.idle, AnimPriority.locomotion, true);
    }

    #endregion

    #region PlayAnimations Voids

    public void SetAgeStage(int age)
    {
        switch (age)
        {
            case 3: nextAge = Age.young; break;
            case 2: nextAge = Age.adult; break;
            case 1: nextAge = Age.old; break;
        }
    }

    public void PlayDamage() //DAMAGE-------------------------------
    {
        // if (damageActive) return;

        if (isBlocking)
        {
            PlayBlockFeedback();
            return;
        }

        damageActive = true;
        PlayState(PlayerState.damage, AnimPriority.damage);
    }

    public void PlayDistanceAttack()
    {
        PlayState(PlayerState.distance, AnimPriority.attack);
    }

    public void PlayMelee(int index) //MELEE -------------------------------------
    {
        meleeStep = index;

        if (provider.AnimExist(ResolveFullID("melee", lastDirection)))
        {
            PlayState(PlayerState.melee, AnimPriority.attack);
        }
    }

    public void StartDash() //DASH---------------------------------------
    {
        if(isDashing) return;
        isDashing = true;
        dashTimer = dashDuration;
        
        shaderCtrl?.ShineTrigger();
        if(VFX_Dash != null)
        {
            VFX_Dash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            VFX_Dash.Play(true);
        }

        // SpawnGoldenShadow();

        PlayState(PlayerState.dash, AnimPriority.dash);
    }

    public void EndDash()
    {
        isDashing = false;
        damageActive = false;
        currentPriority = AnimPriority.none;

        if(VFX_Dash != null && !VFX_Dash.isStopped) VFX_Dash.Stop();

        PlayState(PlayerState.idle, AnimPriority.locomotion);
    }

    public void PlayBlock() // BLOCK ------------------------------------------------------
    {
        isBlocking = true;
        SA.holdOnLastFrame = true;
        PlayState(PlayerState.block, AnimPriority.action);
    }

    public void EndBlock()
    {
        isBlocking = false;
        SA.holdOnLastFrame = false;
        currentPriority = AnimPriority.none;
        PlayState(PlayerState.idle, AnimPriority.locomotion);
    }

    private void PlayBlockFeedback()
    {
        SA.holdOnLastFrame = false;
        PlayState(PlayerState.blockfb, AnimPriority.action);
    }

    public void PlayDeath()
    {
        isDashing = false;
        SA.useUnscaledTime = true;
        lastDirection = "downleft";
        PlayState(PlayerState.death, AnimPriority.bind);
    }

    #endregion

    #region Resolver & Callbacks

    protected override string ResolveFullID(string baseID, string direction)
    {
        //Forzar animacion de muerte
        if (baseID == "death") return "death";  

        //Contruccion de Prefix de edad
        string prefix = currentAge switch
        {
            Age.adult => "mid:",
            Age.old => "late:",
            _ => "begin:"
        };

        bool isGroup1 = direction == "down" ||
                            direction == "downleft" ||
                            direction == "downright" ||
                            direction == "left";

        string groupSuffix = isGroup1? "1": "2";

        string baseResolved = baseID == "melee"? $"{prefix}melee{meleeStep}": prefix + baseID;

        string id;
        id = provider.AnimExist(baseResolved + groupSuffix) ? baseResolved + groupSuffix : baseResolved;

        if(!HasShield && provider.AnimExist(id + "_ns"))
        {
            id += "_ns";
        }

        if (debug) Debug.Log(id);
        return id;
    }
    protected override void OnFinishedAnimation()
    {
        if(currentPriority == AnimPriority.dash) return;
        if(currentState == PlayerState.blockfb)
        {
            isBlocking = true;
            SA.holdOnLastFrame = true;
            PlayState(PlayerState.block, AnimPriority.action, false);
            return;
        }

        damageActive = false;
        PlayState(PlayerState.idle, AnimPriority.locomotion);
    }
    #endregion

    #region Eventos
    protected override void OnAnimationEvent(string ev)
    {
        switch (ev)
        {
            case "MeleeSlash_1": vfxCtrl?.SpawnSlash(0); break;
            case "MeleeSlash_2": vfxCtrl?.SpawnSlash(1); break;
            case "MeleeSlash_3": vfxCtrl?.SpawnSlash(2); break;
            case "DeactivateSprite": gameObject.SetActive(false); break;
        }
    }

    
    #endregion


    // #region Spawn Golden Shadow in Dash [OPTIMIZAR]
    // public void SpawnGoldenShadow()
    // {
    //     RaycastHit hit;
    //     Vector3 shadowSpawnPoint = transform.position;
    //     Vector3 rayOrigin = shadowSpawnPoint + Vector3.up * 0.5f;

    //     if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 50f, groundMask))
    //     {
    //         Vector3 spawnPos = hit.point + new Vector3(0, 0.1f, 0);
    //         Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

    //         GameObject goldenInstance = Instantiate(shadowPrefab, spawnPos, spawnRotation);
    //         goldenInstance.transform.localScale = Vector3.one * 0.2f;
            
    //         SpriteRenderer sr = goldenInstance.transform.GetChild(0).GetComponent<SpriteRenderer>();
    //         if (sr != null) StartCoroutine(GoldenShadowFade(sr, 0.5f));
            
    //         // goldenInstance.transform.SetParent(this.transform);
    //         Debug.Log("Sombra spawneada");

    //         Destroy(goldenInstance, 1.2f);
    //     }
    //     else
    //     {
    //         Debug.LogWarning($"[{gameObject.name}] No se encontró suelo para spawnear la sombra.");
    //     }
    // }

    // IEnumerator GoldenShadowFade(SpriteRenderer sr, float duration)
    // {
    //     Material mat = sr.material;

    //     Color colorOriginal = mat.GetColor("_Color");
    //     Color colorFinal = colorOriginal;
    //     colorFinal.a = 0f;

    //     float t = 0;
    //     while (t < duration)
    //     {
    //         t += Time.deltaTime;

    //         mat.SetColor("_Color", Color.Lerp(colorOriginal, colorFinal, t/duration));
    //         yield return null;
    //     }

    //     mat.SetColor("_Color", colorFinal);
    // }
    // #endregion

    #if UNITY_EDITOR
    #region Testing
    private void DebugInputs()
    {
        if (Input.GetKeyDown(KeyCode.H)) HasShield = !HasShield;

        if (Input.GetKeyDown(KeyCode.J) && !damageActive) PlayDamage();
        if (Input.GetKeyDown(KeyCode.Space) && !isDashing) StartDash();
        if (Input.GetKeyDown(KeyCode.B)) PlayBlock();
        if (Input.GetKeyUp(KeyCode.B)) EndBlock();
        if (Input.GetKeyDown(KeyCode.Keypad0)) PlayDistanceAttack();

        if (Input.GetKeyDown(KeyCode.Keypad1))
        {
            PlayMelee(1);
            Debug.Log("MELEE1");
        }
        if (Input.GetKeyDown(KeyCode.Keypad2))
        {
            PlayMelee(2);
            Debug.Log("MELEE2");
        }
        if (Input.GetKeyDown(KeyCode.Keypad3))
        {
            PlayMelee(3);
            Debug.Log("MELEE3");
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) nextAge = Age.young;
        if (Input.GetKeyDown(KeyCode.Alpha2)) nextAge = Age.adult;
        if (Input.GetKeyDown(KeyCode.Alpha3)) nextAge = Age.old;

        if (Input.GetKeyDown(KeyCode.F1)) PlayDeath();
    }
    #endregion 
    #endif
}