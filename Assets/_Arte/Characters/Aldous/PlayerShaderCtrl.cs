using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerShaderCtrl : MonoBehaviour
{
    #region Variables

    [Header("Referencias")]
    [SerializeField] private Renderer targetRenderer; // Renderer del material que queremos controlar
    [SerializeField] private SpriteRenderer spriteRend; // Renderer del sprite para el flash de daño

    [Header("Berserker Settings")]
    [Tooltip("Valor del grosor del outline cuando está activo (en estado de berserker)")]
    [SerializeField] private float outlineActive = 0.001f;
    [Tooltip("Valor máximo del grosor del outline durante la transición (en estado de berserker)")]
    [SerializeField] private float outlineMax = 0.002f;
    [SerializeField] private GameObject BrokenVFX; // Prefab del VFX de ruptura del outline
    [SerializeField] private Material BrokenMat; // Material para el VFX de ruptura, que cambiará de color según el estado de stamina


    [Header("Shine Settings")]
    [Tooltip("Valor máximo del brillo durante el efecto de shine")]
    [SerializeField] private float shinePeak = 0.8f;
    [Tooltip("Valor máximo del brillo durante el efecto de shine")]
    [SerializeField] private float damagePeak = 0.5f;

    [Header("Hit/Heal Settings")]
    // [Tooltip("Duración total del flash de daño (ida y vuelta)")]
    // [SerializeField] private float damageFlashDuration = 0.15f;
    [Tooltip("Color que aparece al recibir daño")]
    [SerializeField] private Color DamageColor = Color.red;
    [SerializeField] private GameObject DamageVFX;
    [Tooltip("Color que aparece al recibir vida de enemigos o larvas")]
    [SerializeField] private Color HealColor = Color.green;
    [SerializeField] private GameObject HealVFX;

    [Header("State Change Settings")]
    [SerializeField] Color startColor;
    [SerializeField] Color endColor;
    [SerializeField] float AfterImageLifetime;
    [SerializeField] GameObject afterImagePrefab;
    [SerializeField] private GameObject StateChangeVFX;
    private MaterialPropertyBlock mpb;
    private Coroutine shineRoutine;
    private Coroutine outlineRoutine;
    // private Coroutine damageRoutine;
    private Coroutine currentFlashCoroutine;

    [Header("Propiedades del shader")]
    private static readonly int GrossorID = Shader.PropertyToID("_Grossor");
    private static readonly int ShineID = Shader.PropertyToID("_ShineAmount");
    private static readonly int HasStaminaID = Shader.PropertyToID("_HasStamina");
    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorID = Shader.PropertyToID("_FlashColor");

    private bool testBerserActive = false;
    private bool testStamina = true;


    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(mpb);

        //Estado base
        mpb.SetFloat(GrossorID, 0f);
        mpb.SetFloat(ShineID, 0f);
        mpb.SetFloat(HasStaminaID, 1f);

        targetRenderer.SetPropertyBlock(mpb);
    }

    private void Update()
    {
        #if UNITY_EDITOR
        if(SceneManager.GetActiveScene().name == "AndreiNew") TEST_IMPUTS();
        #endif
    }

    private void LateUpdate()
    {
        if (targetRenderer == null) return;

        Bounds b = targetRenderer.bounds;
        
        b.center = transform.position;
        
        b.Expand(0.1f);

        targetRenderer.bounds = b;
    }

    #endregion

    #region Testing

    private void TEST_IMPUTS()
    {
        if (Input.GetKeyDown(KeyCode.Keypad9))
        {
            ShineTrigger();
        }

        if (Input.GetKeyDown(KeyCode.Keypad8))
        {
            testBerserActive = !testBerserActive;
            BersekerOutline(testBerserActive);
        }
        if (Input.GetKeyDown(KeyCode.Keypad7))
        {
            testStamina = !testStamina;
            SetHasStamina(testStamina);
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            DamageTrigger();
        }
        if (Input.GetKeyDown(KeyCode.O))
        {
            HealingTrigger();
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            AgeChangeTrigger();
        }
    }
    #endregion

    #region Shine Functions

    public void ShineTrigger(float duration = 0.22f)
    {
        if(shineRoutine != null) StopCoroutine(shineRoutine);

        shineRoutine = StartCoroutine(ShineRoutine(duration));
    }

    private IEnumerator ShineRoutine(float duration)
    {
        float half = duration * 0.5f;
        float t = 0f;

        // while (t < half)
        // {
        //     t += Time.deltaTime;
        //     SetColorID(ShineID, Mathf.Lerp(0f, shinePeak, t / half));
        //     yield return null;
        // }
        SetColorID(ShineID, 1);
        yield return new WaitForSeconds(0.1f);

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            SetColorID(ShineID, Mathf.Lerp(shinePeak, 0f, t / half));
            yield return null;
        }

        SetColorID(ShineID, 0f);
        shineRoutine = null;
    }

    private void SetColorID(int ID, float value)
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();

        if (targetRenderer != null) targetRenderer.GetPropertyBlock(mpb);
        if (mpb != null) mpb.SetFloat(ID, value);

        if (targetRenderer != null) targetRenderer.SetPropertyBlock(mpb);
    }

    #endregion

    #region Berserker Functions

    public void BersekerOutline(bool Active)
    {
        if (outlineRoutine != null) StopCoroutine(outlineRoutine);

        outlineRoutine = StartCoroutine(Active? BerserkerOn():BerserkerOff());
    }

    private IEnumerator BerserkerOn()
    {
        ShineTrigger(0.5f);
        float t = 0f;
        const float time = 0.1f;

        while (t < time)
        {
            t += Time.deltaTime;
            SetOutline(Mathf.Lerp(0f, outlineMax, t / time));
            yield return null;
        }

        t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            SetOutline(Mathf.Lerp(outlineMax, outlineActive, t / time));
            yield return null;
        }

        SetOutline(outlineActive);
        outlineRoutine = null;
    }

    private IEnumerator BerserkerOff()
    {
        float t = 0f;
        float time = 0.15f;
        ShineTrigger();

        float start = GetCurrentOutline();

        while (t < time)
        {
            t += Time.deltaTime;
            SetOutline(Mathf.Lerp(start, 0f, t / time));
            yield return null;
        }

        GameObject obj = Instantiate(BrokenVFX);
        obj.transform.position = transform.position;

        OutlineBrokeColor(obj);
        
        SetOutline(0f);
        outlineRoutine = null;
    }


    private void SetOutline(float value)
    {
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(GrossorID, value);

        targetRenderer.SetPropertyBlock(mpb);
    }

    private float GetCurrentOutline()
    {
        targetRenderer.GetPropertyBlock(mpb);
        return mpb.GetFloat(GrossorID);
    }

    public void SetHasStamina(bool hasStamina)
    {
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(HasStaminaID, hasStamina? 1f: 0f);

        targetRenderer.SetPropertyBlock(mpb);
    }

    private void OutlineBrokeColor(GameObject RefVFX)
    {
        Material copy = Instantiate(BrokenMat);
        ParticleSystem party = RefVFX.GetComponent<ParticleSystem>();

        targetRenderer.GetPropertyBlock(mpb);
        float stamina = mpb.GetFloat(HasStaminaID);

        copy.color = stamina == 1? Color.yellow: Color.red;

        ParticleSystemRenderer psRender = RefVFX.GetComponent<ParticleSystemRenderer>();
        psRender.material = copy;
    }

    #endregion

    #region Hit/Heal Functions

    public void DamageTrigger()
    {
        if(currentFlashCoroutine != null) StopCoroutine(currentFlashCoroutine);

        currentFlashCoroutine = StartCoroutine(HitCoroutine());

        DamageVFX.SetActive(false);
        DamageVFX.SetActive(true);
    }

    public void HealingTrigger()
    {
        if(currentFlashCoroutine != null) StopCoroutine(currentFlashCoroutine);

        currentFlashCoroutine = StartCoroutine(HealCoroutine());

        HealVFX.SetActive(false);
        HealVFX.SetActive(true);
    }

    private IEnumerator HitCoroutine()
    {
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(FlashColorID, DamageColor);
        targetRenderer.SetPropertyBlock(mpb);

        SetColorID(FlashAmountID, 1);
        yield return new WaitForSeconds(0.08f);

        SetColorID(FlashAmountID, 0);
        yield return new WaitForSeconds (0.05f);

        SetColorID(FlashAmountID, 1);
        yield return new WaitForSeconds(0.08f);

        SetColorID(FlashAmountID, 0);
        currentFlashCoroutine = null;
    }

    private IEnumerator HealCoroutine(float duration = 0.5f)
    {
        float t = 0f;
        float half = duration* 0.5f;

        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(FlashColorID, HealColor);
        targetRenderer.SetPropertyBlock(mpb);

        SetColorID(FlashAmountID, 1);
        yield return new WaitForSeconds(0.1f);

        while(t < half)
        {
            t+= Time.deltaTime;
            SetColorID(FlashAmountID, Mathf.Lerp(1, 0, t/half));
            yield return null;
        }

        SetColorID(FlashAmountID, 0);


        currentFlashCoroutine = null;
    }

    #endregion

    #region AgeChange Functions
    void SpawnAfterImage()
    {
        GameObject obj = Instantiate(afterImagePrefab);
        obj.name = "PlayerAfterImage";
        obj.transform.position = transform.position;
        obj.transform.rotation = transform.rotation;
        obj.transform.localScale = transform.localScale;

        SpriteRenderer r = obj.GetComponent<SpriteRenderer>();

        r.sprite = spriteRend.sprite;
        r.flipX = spriteRend.flipX;
        r.flipY = spriteRend.flipY;
        r.sortingLayerID = spriteRend.sortingLayerID;
        r.sortingOrder = spriteRend.sortingOrder - 1;

        S_AfterImagePrefab afterImage = obj.GetComponent<S_AfterImagePrefab>();
        afterImage.Initialize(startColor, endColor, AfterImageLifetime);
    }

    public void AgeChangeTrigger()
    {
        ShineTrigger(0.5f);

        SpawnAfterImage();

        StateChangeVFX.SetActive(false);
        StateChangeVFX.SetActive(true);
    }

    #endregion

    public void ResetAllEffects()
    {
        StopAllCoroutines();
        
        SetColorID(ShineID, 0);
        SetColorID(FlashAmountID, 0);
        SetOutline(0);
        DamageVFX.SetActive(false);
        HealVFX.SetActive(false);
    }
}