using System.Collections;
using UnityEngine;
public class EstadoDeVida : FullScreenEffectsBase
{
    [Header("Referencias")]
    [SerializeField] ShieldSkill berserkerScript;

    #region Variables Shader

    private const string PROP_CONDICION_DAÑO = "_Condicion";
    private const string PROP_TRANSPARENCIA = "_Transparencia";
    private const string PROP_INTENSIDAD_POSTERIZADO = "_IntensidadPosterizado";
    private const string PROP_CONDICION_VIDA = "_Condici_n";
    private const string PROP_TIEMPO_RESPIRACION = "_TiempoRespiraci_n";
    private const string PROP_TAMAÑO_DITHER = "_Tama_oDither";
    private const string PROP_LIMITE_DITHER = "_LimiteDither";
    private const string PROP_CONDICION_PIXEL = "_Condici_nPixel";
    private const string PROP_SIZE_PIXEL = "_SizePixel";
    private const string CONDICION_BERSERKER = "_CondicionBerserker";
    private const string COLOR_BERSERKER = "_ColorBerseker";
    private const string COLOR_VENAS = "_ColorVenas";

    #endregion

    #region Inspector Feedback de Daño

    [Header("Recibir Daño")]
    [Tooltip("Activa efecto de primera herida (Capa1). Se auto-desactiva al terminar.")]
    public bool PrimeraHerida;

    [Tooltip("Activa efecto de heridas posteriores (Capa2). Se auto-desactiva al terminar.")]
    public bool DemasHeridas;

    [Tooltip("Segundos que tardan Transparencia e IntensidadPosterizado en bajar a 0.")]
    [Min(0.01f)]
    public float TiempoHerida = 2f;

    [Tooltip("Valor maximo de Transparencia en el shader.")]
    [Range(0f, 100f)]
    public float Transparencia = 72f;

    [Tooltip("Valor maximo de IntensidadPosterizado en el shader.")]
    [Range(0f, 100f)]
    public float Posterizado = 0f;

    private bool _prevPrimeraHerida;
    private bool _prevDemasHeridas;
    private bool _dañoProcesando;
    private float _dañoElapsed;

    #endregion

    #region Feedback Baja Vida

    [Header("Vida Baja")]
    [Tooltip("Activa o desactiva el efecto de vida baja.")]
    public bool VidaBajaActiva;

    [Tooltip("Sincronizado con TiempoRespiracion del shader.")]
    [Range(1f, 6f)]
    public float TiempoRespiracion = 4f;

    [Tooltip("Sincronizado con TamanoDither del shader.")]
    [Range(0f, 100f)]
    public float TamañoDither = 24.5f;

    [Tooltip("Sincronizado con LimiteDither del shader.")]
    [Range(0f, 0.4f)]
    public float LimiteDither = 0.3f;

    #endregion

    #region Feedback Cambio de Etapa

    private bool _prevVidaBajaActiva;

    [Header("Pixeleado")]
    [Tooltip("Si esta activo el juego esta pixeleado, si no, no.")]
    public bool PixeladoActivo;
    [SerializeField] float minPixelSize = 100f;
    [SerializeField] float normalPixelSize = 1000f;
    [SerializeField] float PixelizeDuration = 0.3f;
    [SerializeField] AnimationCurve pixelEase;

    [Tooltip("Sincronizado con SizePixel del shader.")]
    [Min(1f)]
    // public float SizePixel = 150f;

    private bool _prevPixeladoActivo;
    private Coroutine currentPixelRutine;

    #endregion

    #region Feedback Berserker

    [Header("Berserker")]
    [Tooltip("Activa o desactiva el efecto Berserker. El shader sube de 0 a 1 o baja de 1 a 0.")]
    public bool CondicionBerserker;

    [Tooltip("Activa la transicion gradual de ColorBerserker1 hacia ColorBerserker2.")]
    public bool TransicionColorBerserker;

    [Tooltip("Tiempo que tarda el efecto Berserker en subir/bajar y el color en pasar de Color 1 a Color 2.")]
    [Min(0.01f)]
    public float TiempoBerserker = 1.5f;

    [Tooltip("Color inicial del efecto Berserker.")]
    public Color ColorBerserker1 = Color.red;

    [Tooltip("Color final del efecto Berserker.")]
    public Color ColorBerserker2 = new Color(0.6f, 0f, 0f, 1f);

    [Tooltip("Valor V en HSV para oscurecer las venas. Menor valor = venas mas oscuras.")]
    [Range(0f, 1f)]
    public float ValorVenas = 0.45f;

    private bool _prevCondicionBerserker;
    private bool _prevTransicionColorBerserker;

    private float _berserkerValorActual;
    private float _colorBerserkerElapsed;
    private Color _colorBerserkerActual;

    #endregion

    #region Unity Lifecycle

    protected override void Start()
    {
        // if (estadoDeVidaMaterial == null))
        // {
        //     Debug.LogError("[EstadoDeVida] No hay Material asignado en estadoDeVidaMaterial.", this);
        //     enabled = false;
        //     return;
        // }

        base.Start();

        if(!enabled) return;

        PixeladoActivo = true;

        SetFloat(PROP_CONDICION_DAÑO, 0f);
        SetFloat(PROP_TRANSPARENCIA, Transparencia);
        SetFloat(PROP_INTENSIDAD_POSTERIZADO, Posterizado);

        SetFloat(PROP_CONDICION_VIDA, 0f);
        SetFloat(PROP_TIEMPO_RESPIRACION, TiempoRespiracion);
        SetFloat(PROP_TAMAÑO_DITHER, TamañoDither);
        SetFloat(PROP_LIMITE_DITHER, LimiteDither);

        SetFloat(PROP_CONDICION_PIXEL, 100f);
        // SetFloat(PROP_SIZE_PIXEL, SizePixel);

        _colorBerserkerActual = ColorBerserker1;
        _berserkerValorActual = 0f;
        _colorBerserkerElapsed = 0f;

        SetFloat(CONDICION_BERSERKER, 0f);
        SetColor(COLOR_BERSERKER, _colorBerserkerActual);
        SetColor(COLOR_VENAS, CalcularColorVenas(_colorBerserkerActual));

        _prevPrimeraHerida = false;
        _prevDemasHeridas = false;
        _prevVidaBajaActiva = false;
        // _prevPixeladoActivo = false;
        _prevCondicionBerserker = false;
        _prevTransicionColorBerserker = false;

        // PixeladoActivo = false;
        CondicionBerserker = false;
        TransicionColorBerserker = false;
    }

    

    private void Update()
    {
        HandleDaño();
        HandleVidaBaja();
        // HandlePixelado();
        HandleBerserker();

        SetFloat(PROP_TIEMPO_RESPIRACION, TiempoRespiracion);
        SetFloat(PROP_TAMAÑO_DITHER, TamañoDither);
        SetFloat(PROP_LIMITE_DITHER, LimiteDither);
        // SetFloat(PROP_SIZE_PIXEL, SizePixel);
    }

    private void OnEnable()
    {
        berserkerScript.OnAbilityActivated += ActiveBerserker;
        berserkerScript.OnAbilityDeactivated += DeactiveBerserker;
        ShieldSkill.OnStaminaChanged += BerserkerColorTransition;
        PlayerHealth.OnLifeStageChanged += Pixelizado;
        HUDManager.OnLowHealthChanged += HandleLowHealthChange;
    }

    private void OnDisable()
    {
        berserkerScript.OnAbilityActivated -= ActiveBerserker;
        berserkerScript.OnAbilityDeactivated -= DeactiveBerserker;
        ShieldSkill.OnStaminaChanged -= BerserkerColorTransition;
        PlayerHealth.OnLifeStageChanged -= Pixelizado;
        HUDManager.OnLowHealthChanged -= HandleLowHealthChange;
    }

    #endregion


    #region Metodos FB Daño

    private void HandleDaño()
    {
        bool nuevaPrimera = PrimeraHerida && !_prevPrimeraHerida;
        bool nuevaDemas = DemasHeridas && !_prevDemasHeridas;

        if (nuevaPrimera || nuevaDemas)
        {
            int condicion = (nuevaDemas || DemasHeridas) ? 2 : 1;
            ActivarDaño(condicion);
        }

        if (_dañoProcesando)
        {
            _dañoElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_dañoElapsed / TiempoHerida);

            SetFloat(PROP_TRANSPARENCIA, Mathf.Lerp(Transparencia, 0f, t));
            SetFloat(PROP_INTENSIDAD_POSTERIZADO, Mathf.Lerp(Posterizado, 0f, t));

            if (t >= 1f)
                DesactivarDaño();
        }

        _prevPrimeraHerida = PrimeraHerida;
        _prevDemasHeridas = DemasHeridas;
    }

    private void ActivarDaño(int condicion)
    {
        PrimeraHerida = (condicion == 1);
        DemasHeridas = (condicion == 2);

        _dañoElapsed = 0f;
        _dañoProcesando = true;

        SetFloat(PROP_CONDICION_DAÑO, condicion);
        SetFloat(PROP_TRANSPARENCIA, Transparencia);
        SetFloat(PROP_INTENSIDAD_POSTERIZADO, Posterizado);

        PrimeraHerida = false;
    }

    private void DesactivarDaño()
    {
        _dañoProcesando = false;
        PrimeraHerida = false;
        DemasHeridas = false;
        _prevPrimeraHerida = false;
        _prevDemasHeridas = false;

        SetFloat(PROP_CONDICION_DAÑO, 0f);
        SetFloat(PROP_TRANSPARENCIA, Transparencia);
        SetFloat(PROP_INTENSIDAD_POSTERIZADO, Posterizado);
    }

    #endregion

    #region Metodos Vida Baja

    private void HandleLowHealthChange(bool value)
    {
        VidaBajaActiva = value;
    }

    private void HandleVidaBaja()
    {
        if (VidaBajaActiva == _prevVidaBajaActiva) return;

        SetFloat(PROP_CONDICION_VIDA, VidaBajaActiva ? 1f : 0f);
        _prevVidaBajaActiva = VidaBajaActiva;
    }

    public void LowHealthVignette(bool active)
    {
        VidaBajaActiva = active;
    }

    #endregion

    #region Metodos Pixelizado

    private void Pixelizado(PlayerHealth.LifeStage lifeStage)
    {
        // if (PixeladoActivo == _prevPixeladoActivo) return;

        // // SetFloat(PROP_CONDICION_PIXEL, PixeladoActivo ? 100f : 0f);
        // _prevPixeladoActivo = PixeladoActivo;

        if (currentPixelRutine != null)
        {
            StopCoroutine(currentPixelRutine);
            currentPixelRutine = null;
        }
        
        currentPixelRutine = StartCoroutine(StageChange(PixelizeDuration));
    }

    private IEnumerator StageChange(float duration)
    {
        // Debug.LogError("Corrutine de cambio de vida vfx");
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = pixelEase.Evaluate(elapsed/duration);
            SetFloat(PROP_SIZE_PIXEL, Mathf.Lerp(minPixelSize, normalPixelSize, t));
            yield return null;
        }

        SetFloat(PROP_SIZE_PIXEL, normalPixelSize);
        
        currentPixelRutine = null;
    }

    #endregion

    #region Metodos Berserker

    private void HandleBerserker()
    {
        float targetBerserker = CondicionBerserker ? 1f : 0f;

        _berserkerValorActual = Mathf.MoveTowards(
            _berserkerValorActual,
            targetBerserker,
            Time.deltaTime / TiempoBerserker
        );

        SetFloat(CONDICION_BERSERKER, _berserkerValorActual);

        if (TransicionColorBerserker && !_prevTransicionColorBerserker)
        {
            _colorBerserkerElapsed = 0f;
        }

        if (TransicionColorBerserker)
        {
            _colorBerserkerElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_colorBerserkerElapsed / TiempoBerserker);

            _colorBerserkerActual = Color.Lerp(ColorBerserker1, ColorBerserker2, t);
        }
        else
        {
            _colorBerserkerActual = ColorBerserker1;
            _colorBerserkerElapsed = 0f;
        }

        SetColor(COLOR_BERSERKER, _colorBerserkerActual);
        SetColor(COLOR_VENAS, CalcularColorVenas(_colorBerserkerActual));

        _prevCondicionBerserker = CondicionBerserker;
        _prevTransicionColorBerserker = TransicionColorBerserker;
    }

    public void ActiveBerserker()
    {
        TransicionColorBerserker = false;
        CondicionBerserker = true;
    }

    public void DeactiveBerserker()
    {
        CondicionBerserker = false;
    }

    public void BerserkerColorTransition(float current, float max)
    {
        TransicionColorBerserker = current <= 0? true: false;
    }

    private Color CalcularColorVenas(Color baseColor)
    {
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);

        Color colorVenas = Color.HSVToRGB(h, s, ValorVenas);
        colorVenas.a = baseColor.a;

        return colorVenas;
    }

    #endregion
}

