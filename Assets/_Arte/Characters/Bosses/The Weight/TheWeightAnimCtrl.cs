using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TheWeightAnimCtrl : MonoBehaviour
{
    public enum SwapForms
    {
        formaBase, apisonador, pulpo, canon,
    }

    SwapForms currentForm;
    SkinnedMeshRenderer[] currentMesh;

    [Header("Modelos")]
    [SerializeField] GameObject formaBaseModel;
    [SerializeField] SkinnedMeshRenderer[] formaBaseMesh;
    [SerializeField] GameObject apisonadorModel;
    [SerializeField] SkinnedMeshRenderer[] apisonadorMesh;
    [SerializeField] GameObject canonModel;
    [SerializeField] SkinnedMeshRenderer[] canonMesh;
    [SerializeField] GameObject pulpoModel;
    [SerializeField] SkinnedMeshRenderer[] pulpoMesh;

    [Header("Swap Model VFX")]
    [SerializeField] Material swapMaterial;
    [SerializeField] float ghostVanishTime = 0.2f;
    [SerializeField] GameObject FleshPrefab;
    [SerializeField] int fleshCount;
    [SerializeField] float fleshLifetime;

    [Header("General VFX")]
    [SerializeField] private GameObject canonShotSmoke;
    [SerializeField] private Transform canonShotSmokePos;

    Animator formaBase_anim;
    Animator apisonador_anim;
    Animator canon_anim;
    Animator pulpo_anim;

    [Header("Anticipation Shake")]
    [SerializeField] private float shakeIntensity = 0.2f;
    [SerializeField] private float shakeFrequency = 2f;

    private Coroutine shakeCoroutine;
    private Vector3 originalLocalPosition;
    private bool originalPositionCaptured = false;

    private bool isAnimationPaused = false;
    private float speedBeforePause = 1f;

    private bool internalWalking;
    public bool isWalking
    {
        get => internalWalking;
        set
        {
            if (internalWalking == value) return;
            internalWalking = value;
            formaBase_anim.SetBool("Walk", isWalking);
        }
    }

#if UNITY_EDITOR
    void Update()
    {
        if (SceneManager.GetActiveScene().name == "AndreiNew")
            TestImputs();
    }
#endif

    private const string ANIM_BASE_ATTACK = "A_Base_Attack";
    private const string ANIM_CANON_SHOT = "A_Canon_Disparo";
    private void Awake()
    {
        formaBase_anim = formaBaseModel.GetComponent<Animator>();
        apisonador_anim = apisonadorModel.GetComponent<Animator>();
        canon_anim = canonModel.GetComponent<Animator>();
        pulpo_anim = pulpoModel.GetComponent<Animator>();

        originalLocalPosition = transform.localPosition;
        originalPositionCaptured = true;

        ReturnToIdle();
    }

    /// <summary>
    /// Animator de la forma actualmente activa (la única que realmente
    /// se está reproduciendo, ya que las otras 3 están desactivadas).
    /// </summary>
    private Animator GetActiveAnimator()
    {
        switch (currentForm)
        {
            case SwapForms.apisonador: return apisonador_anim;
            case SwapForms.canon: return canon_anim;
            case SwapForms.pulpo: return pulpo_anim;
            default: return formaBase_anim;
        }
    }

    private void SwapTo(SwapForms newForm)
    {
        QuickSwapVFX();

        formaBaseModel.SetActive(false);
        apisonadorModel.SetActive(false);
        canonModel.SetActive(false);
        pulpoModel.SetActive(false);

        currentForm = newForm;

        switch (newForm)
        {
            case SwapForms.formaBase:
                {
                    formaBaseModel.SetActive(true);
                    currentMesh = formaBaseMesh;
                    break;
                }
            case SwapForms.apisonador:
                {
                    apisonadorModel.SetActive(true);
                    currentMesh = apisonadorMesh;
                    break;
                }
            case SwapForms.canon:
                {
                    canonModel.SetActive(true);
                    currentMesh = canonMesh;
                    break;
                }
            case SwapForms.pulpo:
                {
                    pulpoModel.SetActive(true);
                    currentMesh = pulpoMesh;
                    break;
                }
        }
    }

    private void QuickSwapVFX()
    {
        if (currentMesh == null) return;

        foreach (var mesh in currentMesh)
        {
            Mesh bakedMesh = new Mesh();
            mesh.BakeMesh(bakedMesh);

            GameObject ghost = new GameObject("SwapGhost");
            ghost.transform.position = mesh.transform.position;
            ghost.transform.rotation = mesh.transform.rotation;
            ghost.transform.localScale = mesh.transform.localScale;

            MeshFilter mf = ghost.AddComponent<MeshFilter>();
            MeshRenderer mr = ghost.AddComponent<MeshRenderer>();
            EscudoController e = ghost.AddComponent<EscudoController>();
            mf.mesh = bakedMesh;
            mr.material = swapMaterial;
            e.Escudo = true;
            e.tiempoEscudo = ghostVanishTime;

            SpawnFleshVFX(bakedMesh);

            Destroy(ghost, ghostVanishTime);
        }
    }

    void SpawnFleshVFX(Mesh bakedMesh)
    {
        Vector3[] vertices = bakedMesh.vertices;

        for (int i = 0; i < fleshCount; i++)
        {
            Vector3 localPos = vertices[Random.Range(0, vertices.Length)];
            Vector3 worldPos = currentMesh[0].transform.TransformPoint(localPos);

            GameObject currentFlesh = Instantiate(FleshPrefab, worldPos, Random.rotation);

            ParticleSystem fleshPS = currentFlesh.GetComponent<ParticleSystem>();
            if (fleshPS == null) fleshPS = currentFlesh.GetComponentInChildren<ParticleSystem>();

            if (fleshPS != null)
            {
                StartCoroutine(DestroyFleshAfterLifetime(fleshPS));
            }
            else
            {
                Destroy(currentFlesh, fleshLifetime);
            }
        }
    }

    private IEnumerator DestroyFleshAfterLifetime(ParticleSystem fleshPS)
    {
        yield return new WaitForSeconds(fleshLifetime);

        VFXHelper.StopAndDestroy(fleshPS);
    }


    #region Funcionas publicas
    public void PlayApisonador() => SwapTo(SwapForms.apisonador);
    public void PlayCanon() => SwapTo(SwapForms.canon);
    public void PlayPulpo() => SwapTo(SwapForms.pulpo);
    public void ReturnToIdle() => SwapTo(SwapForms.formaBase);
    public void PlayPrepareBaseAttack() => formaBase_anim.SetTrigger("PrepareAtk");
    public void PlayAttack() => formaBase_anim.Play(ANIM_BASE_ATTACK);
    public void PlayCanonShot() 
    {
        canon_anim.SetTrigger("Shot");
        InstantiateCanonShotVFX();
    }

    public void SetAnimatorSpeed(float speed)
    {
        formaBase_anim.speed = speed;
        apisonador_anim.speed = speed;
        canon_anim.speed = speed;
        pulpo_anim.speed = speed;
    }

    public float GetAnimatorSpeed()
    {
        return formaBase_anim.speed;
    }

    public void PlayDeath()
    {
        formaBase_anim.ResetTrigger("DeathTrigger");
        formaBase_anim.SetTrigger("DeathTrigger");
    }

    /// <summary>
    /// Congela el animator de la forma activa en su pose actual
    /// (usado durante la pausa de anticipación de ataque).
    /// </summary>
    public void PauseAnimation()
    {
        Animator anim = GetActiveAnimator();
        if (anim == null || isAnimationPaused) return;

        speedBeforePause = anim.speed;
        anim.speed = 0f;
        isAnimationPaused = true;
    }

    /// <summary>
    /// Restaura la velocidad que tenía el animator de la forma activa
    /// antes de PauseAnimation (respeta multiplicadores de Enrage/MudWave).
    /// </summary>
    public void ResumeAnimation()
    {
        Animator anim = GetActiveAnimator();
        if (anim == null || !isAnimationPaused) return;

        anim.speed = speedBeforePause;
        isAnimationPaused = false;
    }

    /// <summary>
    /// Tiembla la raíz del modelo (la forma activa) durante la anticipación
    /// de un ataque, sin alterar la pose congelada por PauseAnimation.
    /// </summary>
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

    private IEnumerator ShakeRoutine(float duration)
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

    void TestImputs()
    {
        if (Input.GetKeyDown(KeyCode.J)) PlayApisonador();
        if (Input.GetKeyDown(KeyCode.K)) PlayCanon();
        if (Input.GetKeyDown(KeyCode.L)) PlayPulpo();
        if (Input.GetKeyDown(KeyCode.O)) ReturnToIdle();
        if (Input.GetKeyDown(KeyCode.Mouse0)) PlayPrepareBaseAttack();
        if (Input.GetKeyDown(KeyCode.Mouse1)) PlayAttack();
        if (Input.GetKeyDown(KeyCode.N)) PlayCanonShot();

        float h = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        isWalking = h != 0 || y != 0;
    }

    private void InstantiateCanonShotVFX()
    {
        Instantiate(canonShotSmoke, canonShotSmokePos.position, canonShotSmokePos.localRotation);
    }
}