using UnityEngine;

public class StaticAnimCtrl : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator anim;
    [SerializeField] private Material[] originalMaterials;
    [SerializeField] private Material tpMaterial;
    [SerializeField] private SkinnedMeshRenderer mesh;

    [Header("Anticipation Shake")]
    [SerializeField] private float shakeIntensity = 0.2f;
    [SerializeField] private float shakeFrequency = 2f;

    private EnemyVisualEffects visualEffects;

    private Coroutine shakeCoroutine;
    private Vector3 originalLocalPosition;
    private bool originalPositionCaptured = false;

    private void Awake()
    {
        anim = gameObject.GetComponent<Animator>();
        visualEffects = GetComponentInParent<EnemyVisualEffects>();

        originalLocalPosition = transform.localPosition;
        originalPositionCaptured = true;

        if (mesh != null)
        {
            originalMaterials = mesh.sharedMaterials;
        }
        else
        {
            Debug.LogError("No hay SkinnedMeshRenderer asignado en: " + gameObject.name + "!!!");
        }
    }

    private void Update()
    {
        // TESTinputs();
    }

    public void PlayShoot()
    {
        if (anim != null) anim.SetTrigger("Shoot");
    }

    public void PlayDeath()
    {
        if (anim != null) anim.SetTrigger("Death");
    }

    public void PlayTPout()
    {
        Material[] tpArray = new Material[originalMaterials.Length];
        for (int i = 0; i < tpArray.Length; i++)
        {
            tpArray[i] = tpMaterial;
        }

        if (mesh != null) mesh.materials = tpArray;

        if (anim != null) anim.Play("TP out");
    }

    public void PlayTPin()
    {
        if (anim != null) anim.Play("TP in");
    }

    public void PlayDamage()
    {
        if (anim != null) anim.SetTrigger("Damage");
    }

    public void restoreOriginalMaterials()
    {
        mesh.sharedMaterials = originalMaterials;

        if (visualEffects != null) visualEffects.ReapplyAmountFlashMaterials();
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

    private void TESTinputs()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0)) PlayShoot();
        if (Input.GetKeyDown(KeyCode.Space)) PlayTPout();
        if (Input.GetKeyDown(KeyCode.M)) PlayTPin();
        if (Input.GetKeyDown(KeyCode.B)) PlayDeath();
        if (Input.GetKeyDown(KeyCode.L)) PlayDamage();
    }
}