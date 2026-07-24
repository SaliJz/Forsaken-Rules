using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class EspejoController : MonoBehaviour
{
    public enum EstadoEspejo
    {
        Base,
        Nivel1,
        Nivel2,
        Nivel3
    }

    [Header("Estado actual")]
    public EstadoEspejo estadoActual = EstadoEspejo.Base;

    [Header("Materiales")]
    public Material textBase;
    public Material EspejoMaterial;

    [Header("Objetos por nivel")]
    public GameObject nivel1;
    public GameObject nivel2;
    public GameObject nivel3;

    [Header("Referencias de cámara")]
    public GameObject MainCamera;
    public GameObject CameraEspejo;

    [Header("Seguimiento de cámara espejo")]
    public bool copiarRotacion = true;
    public bool copiarMovimiento = true;

    [Tooltip("Distancia máxima que CameraEspejo puede alejarse de su posición inicial.")]
    public float limiteMovimiento = 10f;

    [Tooltip("Suavizado del movimiento de CameraEspejo.")]
    public float suavizadoMovimiento = 8f;

    [Tooltip("Suavizado de la rotación de CameraEspejo.")]
    public float suavizadoRotacion = 8f;

    private MeshRenderer meshRenderer;

    private Vector3 posicionInicialMainCamera;
    private Vector3 posicionInicialCameraEspejo;

    private EstadoEspejo ultimoEstado;

    [Header("Mask Alpha Settings")]
    [SerializeField] private float maskAlphaSpeed = 1f;
    private Coroutine maskAlphaCoroutine;
    private static readonly int MaskAplha_PROP = Shader.PropertyToID("_Mask_Alpha");
    private Material materialInstance;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoLevantarse;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (MainCamera == null)
        {
            MainCamera = GameObject.Find("Main Camera");
        }

        if (MainCamera != null)
        {
            posicionInicialMainCamera = MainCamera.transform.position;
        }
        else
        {
            Debug.LogWarning("No se encontró un GameObject llamado 'Main Camera'.", this);
        }

        if (CameraEspejo != null)
        {
            posicionInicialCameraEspejo = CameraEspejo.transform.position;
        }

        ultimoEstado = estadoActual;
        AplicarEstado();
    }

    private void Update()
    {
        if (estadoActual != ultimoEstado)
        {
            AplicarEstado();
            ultimoEstado = estadoActual;
        }

        SincronizarCamaraEspejo();

        testInputs();
    }

    private void AplicarEstado()
    {
        AplicarMaterial();
        ActivarNivel();
        ActualizarMaskAplhaCoroutine();
        ReproducirSonidoLevantarse();
    }

    private void AplicarMaterial()
    {
        if (meshRenderer == null) return;

        Material[] materiales = meshRenderer.materials;

        if (materiales.Length <= 2)
        {
            Debug.LogWarning("El MeshRenderer no tiene Element 2 en Materials.", this);
            return;
        }

        if (estadoActual == EstadoEspejo.Base)
        {
            materiales[2] = textBase;
        }
        else
        {
            materiales[2] = EspejoMaterial;
        }


        materialInstance = new Material(materiales[2]);
        materiales[2] = materialInstance;
        meshRenderer.materials = materiales;
        // materialInstance = materiales[2];
    }

    private void ActivarNivel()
    {
        if (nivel1 != null)
            nivel1.SetActive(estadoActual == EstadoEspejo.Nivel1);

        if (nivel2 != null)
            nivel2.SetActive(estadoActual == EstadoEspejo.Nivel2);

        if (nivel3 != null)
            nivel3.SetActive(estadoActual == EstadoEspejo.Nivel3);
    }

    private void SincronizarCamaraEspejo()
    {
        if (MainCamera == null || CameraEspejo == null) return;

        if (copiarMovimiento)
        {
            Vector3 desplazamientoMain = MainCamera.transform.position - posicionInicialMainCamera;

            if (desplazamientoMain.magnitude > limiteMovimiento)
            {
                desplazamientoMain = desplazamientoMain.normalized * limiteMovimiento;
            }

            Vector3 posicionObjetivo = posicionInicialCameraEspejo + desplazamientoMain;

            CameraEspejo.transform.position = Vector3.Lerp(
                CameraEspejo.transform.position,
                posicionObjetivo,
                Time.deltaTime * suavizadoMovimiento
            );
        }

        if (copiarRotacion)
        {
            Quaternion rotacionObjetivo = MainCamera.transform.rotation;

            CameraEspejo.transform.rotation = Quaternion.Lerp(
                CameraEspejo.transform.rotation,
                rotacionObjetivo,
                Time.deltaTime * suavizadoRotacion
            );
        }
    }

    private void ActualizarMaskAplhaCoroutine()
    {
        bool activo = estadoActual != EstadoEspejo.Base;

        if (maskAlphaCoroutine != null)
        {
            StopCoroutine(maskAlphaCoroutine);
            maskAlphaCoroutine = null;
        }

        if (activo)
        {
            maskAlphaCoroutine = StartCoroutine(MaskAlphaRoutine());
        }
        else SetMaskAlpha(0f);
    }

    private IEnumerator MaskAlphaRoutine()
    {
        while (true)
        {
            yield return LerpMaskAlpha(0, 1, maskAlphaSpeed);
            yield return LerpMaskAlpha(1, 0, maskAlphaSpeed);
            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator LerpMaskAlpha(float Start, float End, float duration = 1f)
    {
        float elapse = 0f;

        while (elapse < duration)
        {
            elapse += Time.deltaTime;
            float t = Mathf.Clamp01(elapse/duration);

            SetMaskAlpha(Mathf.Lerp(Start, End, t));
            yield return null;
        }

        SetMaskAlpha(End);
    }

    private void SetMaskAlpha (float value)
    {
        if (materialInstance == null) return;
        if (materialInstance.HasProperty(MaskAplha_PROP))
        {
            materialInstance.SetFloat(MaskAplha_PROP, value);
        }
    } 

    private void ReproducirSonidoLevantarse()
    {
        if (audioSource == null || sonidoLevantarse == null) return;

        audioSource.PlayOneShot(sonidoLevantarse);
    }

    public void ActivarNivel1() => estadoActual = EstadoEspejo.Nivel1;
    public void ActivarNivel2() => estadoActual = EstadoEspejo.Nivel2;
    public void ActivarNivel3() => estadoActual = EstadoEspejo.Nivel3;
    public void Resetear() => estadoActual = EstadoEspejo.Base;


    void testInputs()
    {
        if(Input.GetKeyDown(KeyCode.Keypad1)) estadoActual = EstadoEspejo.Nivel1;
        if(Input.GetKeyDown(KeyCode.Keypad2)) estadoActual = EstadoEspejo.Nivel2;
        if(Input.GetKeyDown(KeyCode.Keypad3)) estadoActual = EstadoEspejo.Nivel3;
        if(Input.GetKeyDown(KeyCode.Keypad4)) estadoActual = EstadoEspejo.Base;
    }
}