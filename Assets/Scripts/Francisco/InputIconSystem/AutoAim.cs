using UnityEngine;
using UnityEngine.InputSystem;

public class AutoAim : MonoBehaviour
{
    #region Enums

    public enum FXMode { Arrows3D, FIFA }
    public enum FIFAMarkerMode { Actual, Sprite }

    #endregion

    #region Serialized Fields

    [Header("Auto-Aim")]
    [SerializeField] private bool enableAutoAim = true;
    [SerializeField] private bool onlyForGamepad = true;
    [SerializeField] private float autoAimRange = 25f;
    [SerializeField] private float autoAimAngle = 60f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private bool debugMode = false;

    [Header("Sticky Aim")]
    [SerializeField] private bool enableStickyTarget = true;
    [SerializeField] private float stickyTargetDuration = 0.5f;

    [Header("FX - General")]
    [SerializeField] private bool showTargetFX = true;
    [SerializeField] private bool forceShowWithoutGamepad = false;
    [SerializeField] private FXMode targetFXMode = FXMode.Arrows3D;

    [Header("FX - Mode 1: Arrows3D")]
    [SerializeField] private Color arrowColor = new Color(1f, 0.1f, 0.1f, 0.9f);
    [SerializeField] private float arrowSize = 0.2f;
    [SerializeField] private float arrowDistance = 1.0f;
    [SerializeField] private float arrowOffset = 1.0f;
    [SerializeField] private bool animateFX = true;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseScale = 1.2f;
    [SerializeField] private float rotationSpeed = 90f;

    [Header("FX - Mode 2: FIFA")]
    [SerializeField] private FIFAMarkerMode fifaMarkerMode = FIFAMarkerMode.Actual;
    [SerializeField] private Sprite fifaCustomSprite = null;
    [SerializeField] private bool fifaSpriteApplyColor = true;

    [Header("FX - Mode 2: FIFA Position")]
    [SerializeField] private float fifaHeightAboveEnemy = 2.2f;
    [SerializeField] private float fifaArrowSize = 0.35f;

    [Header("FX - Mode 2: FIFA Bob")]
    [SerializeField] private bool fifaBobEnabled = true;
    [SerializeField] private float fifaBobSpeed = 2.5f;
    [SerializeField] private float fifaBobAmount = 0.18f;
    [SerializeField] private AnimationCurve fifaBobCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("FX - Mode 2: FIFA Color")]
    [SerializeField] private bool fifaColorCycleEnabled = true;
    [SerializeField] private float fifaColorCycleSpeed = 1.2f;
    [SerializeField] private Gradient fifaColorGradient;
    [SerializeField] private Color fifaStaticColor = new Color(1f, 0.85f, 0f, 1f);

    [Header("FX - Mode 2: FIFA Opacity")]
    [SerializeField] private bool fifaOpacityPulseEnabled = true;
    [SerializeField] private float fifaOpacityPulseSpeed = 3f;
    [SerializeField] private float fifaOpacityMin = 0.4f;
    [SerializeField] private float fifaOpacityMax = 1.0f;

    [Header("FX - Mode 2: FIFA Transform")]
    [SerializeField] private float fifaSelfRotationSpeed = 0f;
    [SerializeField] private bool fifaBillboardToCamera = true;

    #endregion

    #region Private Fields

    private Transform currentTarget;
    private float lastTargetTime;
    private bool fxInitialized = false;
    private Camera mainCamera;
    private GamepadPointer gamepadPointer;
    private bool originalShowTargetFX;
    private FXMode lastBuiltMode;
    private FIFAMarkerMode lastBuiltFIFAMode;

    private GameObject[] arrows3D = new GameObject[4];

    private GameObject fifaArrowObj;
    private Material fifaMaterial;
    private float fifaColorTime = 0f;

    private EnemyHealth currentTargetHealth;

    #endregion

    #region Properties

    public bool EnableAutoAim
    {
        get => enableAutoAim;
        set => enableAutoAim = value;
    }

    public bool OnlyForGamepad
    {
        get => onlyForGamepad;
        set => onlyForGamepad = value;
    }

    public bool ForceShowWithoutGamepad
    {
        get => forceShowWithoutGamepad;
        set => forceShowWithoutGamepad = value;
    }

    public FXMode TargetFXMode
    {
        get => targetFXMode;
        set { targetFXMode = value; RebuildFX(); }
    }

    public FIFAMarkerMode FifaMarkerMode
    {
        get => fifaMarkerMode;
        set { fifaMarkerMode = value; RebuildFX(); }
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        mainCamera = Camera.main;
        gamepadPointer = GamepadPointer.Instance;
        originalShowTargetFX = showTargetFX;

        if (fifaColorGradient == null)
            fifaColorGradient = BuildDefaultGradient();

        if (showTargetFX)
            InitializeFX();
    }

    private void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        bool modeChanged = targetFXMode != lastBuiltMode;
        bool fifaSubModeChanged = targetFXMode == FXMode.FIFA && fifaMarkerMode != lastBuiltFIFAMode;

        if ((modeChanged || fifaSubModeChanged) && fxInitialized)
            RebuildFX();

        bool isGamepadActive = IsGamepadActiveDevice();
        bool shouldShowFX = originalShowTargetFX && enableAutoAim
                            && (!onlyForGamepad || isGamepadActive || forceShowWithoutGamepad);

        SetFXEnabled(shouldShowFX);

        Vector3 referenceForward = transform.forward;
        Vector3? gamepadAim = GetGamepadAimDirection();
        if (gamepadAim.HasValue)
            referenceForward = gamepadAim.Value;

        if (enableAutoAim)
            FindBestTarget(transform.position, referenceForward, null);

        if (showTargetFX && fxInitialized)
            UpdateFX();
    }

    private void OnDestroy()
    {
        DestroyAllFX();
    }

    #endregion

    #region Device Helpers

    private bool IsGamepadActiveDevice()
    {
        if (gamepadPointer == null) return false;
        InputDevice activeDevice = gamepadPointer.GetCurrentActiveDevice();
        Gamepad gamepad = gamepadPointer.GetCurrentGamepad();
        return activeDevice != null && activeDevice == gamepad;
    }

    private Vector3? GetGamepadAimDirection()
    {
        if (gamepadPointer == null) return null;
        if (!onlyForGamepad || IsGamepadActiveDevice())
        {
            Vector2 v = gamepadPointer.GetAimDirectionValue();
            if (v.magnitude > 0)
                return new Vector3(v.x, 0f, v.y).normalized;
        }
        return null;
    }

    #endregion

    #region Target Finding

    public Transform FindBestTarget(Vector3 playerPosition, Vector3 playerForward, Vector3? aimDirection = null)
    {
        if (!enableAutoAim) return null;

        Collider[] cols = Physics.OverlapSphere(playerPosition, autoAimRange, enemyLayer);
        if (cols.Length == 0)
        {
            currentTarget = null;
            currentTargetHealth = null;
            return null;
        }

        Transform best = null;
        float bestScore = float.MaxValue;
        Vector3 refDir = GetGamepadAimDirection() ?? (aimDirection ?? playerForward);

        foreach (Collider col in cols)
        {
            if (col == null || !col.gameObject.activeInHierarchy) continue;

            Vector3 toTarget = (col.transform.position - playerPosition).normalized;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) continue;

            float angle = Vector3.Angle(refDir, toTarget);
            if (angle > autoAimAngle) continue;
            if (!IsEnemyValid(col)) continue;

            float dist = Vector3.Distance(playerPosition, col.transform.position);
            float normalizedDist = dist / autoAimRange;
            float normalizedAngle = angle / autoAimAngle;
            float proximityBonus = normalizedDist < 0.15f ? -1f : 0f;
            float score = proximityBonus + normalizedAngle * 0.8f + normalizedDist * 0.2f;

            if (score < bestScore)
            {
                bestScore = score;
                best = col.transform;
            }
        }

        if (best != null)
        {
            if (currentTarget != best)
            {
                currentTarget = best;
                currentTargetHealth = best.GetComponent<EnemyHealth>();
            }

            lastTargetTime = Time.time;
        }
        else
        {
            currentTarget = null;
            currentTargetHealth = null;
        }

        return best;
    }

    private bool IsTargetValid(Transform target, Vector3 playerPosition, Vector3 playerForward, Vector3? aimDirection)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        if (!IsEnemyValid(target.GetComponent<Collider>())) return false;

        float dist = Vector3.Distance(playerPosition, target.position);
        if (dist > autoAimRange * 1.2f) return false;

        Vector3 toTarget = (target.position - playerPosition).normalized;
        toTarget.y = 0f;
        Vector3 refDir = GetGamepadAimDirection() ?? (aimDirection ?? playerForward);
        return Vector3.Angle(refDir, toTarget) <= autoAimAngle * 1.5f;
    }

    private bool IsEnemyValid(Collider col)
    {
        if (col == null) return false;
        IDamageable damageable = col.GetComponent<IDamageable>();
        if (damageable == null) return false;
        EnemyHealth health = col.GetComponent<EnemyHealth>();
        if (health != null && health.IsDead) return false;
        return true;
    }

    public Vector3 GetAimDirection(Vector3 playerPosition, Vector3 playerForward, Vector3? manualAimDirection, out bool foundTarget)
    {
        Transform target = currentTarget;
        Vector3? gamepadAim = GetGamepadAimDirection();
        Vector3 effectiveAim = gamepadAim ?? (manualAimDirection ?? playerForward);

        if (enableStickyTarget && target != null && Time.time - lastTargetTime < stickyTargetDuration)
        {
            if (IsTargetValid(target, playerPosition, playerForward, effectiveAim))
            {
                Vector3 dir = (target.position - playerPosition).normalized;
                dir.y = 0f;
                foundTarget = true;
                return dir;
            }
        }

        target = FindBestTarget(playerPosition, playerForward, effectiveAim);

        if (target != null)
        {
            Vector3 dir = (target.position - playerPosition).normalized;
            dir.y = 0f;
            foundTarget = true;
            return dir;
        }

        foundTarget = false;
        return effectiveAim;
    }

    public void ClearTarget()
    {
        currentTarget = null;
        currentTargetHealth = null;
        HideAllFX();
    }

    public Transform GetCurrentTarget()
    {
        return (currentTarget != null && currentTarget.gameObject.activeInHierarchy) ? currentTarget : null;
    }

    #endregion

    #region FX - Init & Rebuild

    private void InitializeFX()
    {
        if (fxInitialized) return;

        if (targetFXMode == FXMode.Arrows3D)
            InitArrows3D();
        else
            InitFIFA();

        lastBuiltMode = targetFXMode;
        lastBuiltFIFAMode = fifaMarkerMode;
        fxInitialized = true;
    }

    private void RebuildFX()
    {
        DestroyAllFX();
        fxInitialized = false;
        InitializeFX();
    }

    private void DestroyAllFX()
    {
        for (int i = 0; i < arrows3D.Length; i++)
        {
            if (arrows3D[i] != null) { Destroy(arrows3D[i]); arrows3D[i] = null; }
        }

        if (fifaArrowObj != null) { Destroy(fifaArrowObj); fifaArrowObj = null; }
    }

    private void HideAllFX()
    {
        foreach (var a in arrows3D) if (a != null) a.SetActive(false);
        if (fifaArrowObj != null) fifaArrowObj.SetActive(false);
    }

    #endregion

    #region FX - Mode 1: Arrows3D

    private void InitArrows3D()
    {
        for (int i = 0; i < 4; i++)
        {
            arrows3D[i] = BuildArrow3D(i);
            arrows3D[i].SetActive(false);
        }
    }

    private GameObject BuildArrow3D(int index)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(obj.GetComponent<Collider>());
        obj.name = $"TargetArrow3D_{index}";
        obj.transform.SetParent(transform);
        obj.transform.localScale = Vector3.one * arrowSize;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = arrowColor;
        mat.mainTexture = BuildArrowTexture(arrowColor);
        obj.GetComponent<Renderer>().material = mat;

        return obj;
    }

    private Texture2D BuildArrowTexture(Color col)
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = size / 2f;
                float ny = y / (float)size;
                float half = (1f - ny) * cx * 0.8f;
                if (Mathf.Abs(x - cx) <= half)
                {
                    bool edge = Mathf.Abs(x - cx) > half - 2f || y < 2 || y > size - 3;
                    pixels[y * size + x] = edge ? new Color(col.r * 0.5f, col.g * 0.5f, col.b * 0.5f, 1f) : col;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return tex;
    }

    private void UpdateArrows3D()
    {
        Transform target = GetCurrentTarget();
        bool show = target != null && showTargetFX;
        for (int i = 0; i < arrows3D.Length; i++)
            if (arrows3D[i] != null) arrows3D[i].SetActive(show);

        if (!show || mainCamera == null) return;

        Vector3 center = target.position + Vector3.up * arrowOffset;
        float distToCam = Vector3.Distance(mainCamera.transform.position, center);
        float screenScale = distToCam * 0.05f;
        float effectiveDist = arrowDistance * screenScale;

        Vector3 cr = mainCamera.transform.right.normalized;
        Vector3 cu = mainCamera.transform.up.normalized;

        Vector3[] offsets = { cu * effectiveDist, cr * effectiveDist, -cu * effectiveDist, -cr * effectiveDist };

        float pulse = animateFX ? 1f + Mathf.Sin(Time.time * pulseSpeed) * (pulseScale - 1f) : 1f;

        for (int i = 0; i < arrows3D.Length; i++)
        {
            if (arrows3D[i] == null) continue;

            Vector3 pos = center + offsets[i] * pulse;
            arrows3D[i].transform.position = pos;
            arrows3D[i].transform.rotation = Quaternion.LookRotation(-(mainCamera.transform.position - pos));

            if (animateFX)
                arrows3D[i].transform.Rotate(0f, 0f, Time.time * rotationSpeed, Space.Self);

            arrows3D[i].transform.localScale = Vector3.one * (arrowSize * pulse * screenScale);
        }
    }

    public void SetArrow3DColor(Color color)
    {
        arrowColor = color;
        if (!fxInitialized) return;
        foreach (var a in arrows3D)
        {
            if (a == null) continue;
            Renderer r = a.GetComponent<Renderer>();
            if (r != null && r.material != null) r.material.color = color;
        }
    }

    #endregion

    #region FX - Mode 2: FIFA

    private void InitFIFA()
    {
        fifaArrowObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(fifaArrowObj.GetComponent<Collider>());
        fifaArrowObj.name = "TargetArrow_FIFA";
        fifaArrowObj.transform.SetParent(null);

        fifaMaterial = new Material(Shader.Find("Sprites/Default"));

        if (fifaMarkerMode == FIFAMarkerMode.Sprite && fifaCustomSprite != null)
        {
            fifaMaterial.mainTexture = fifaCustomSprite.texture;
            fifaMaterial.color = fifaSpriteApplyColor ? fifaStaticColor : Color.white;
        }
        else
        {
            fifaMaterial.color = fifaStaticColor;
            fifaMaterial.mainTexture = BuildFIFATexture();
        }

        fifaArrowObj.GetComponent<Renderer>().material = fifaMaterial;
        fifaArrowObj.SetActive(false);
        fifaColorTime = 0f;
    }

    private Texture2D BuildFIFATexture()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = size / 2f;
                float ny = 1f - (y / (float)size);
                float half = ny * (size * 0.45f);

                if (Mathf.Abs(x - cx) <= half)
                {
                    bool edge = Mathf.Abs(x - cx) > half - 2.5f || y < 2 || y > size - 3;
                    pixels[y * size + x] = edge ? new Color(0f, 0f, 0f, 0.85f) : Color.white;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    private void UpdateFIFA()
    {
        Transform target = GetCurrentTarget();
        bool show = target != null && showTargetFX;
        if (fifaArrowObj == null) return;
        fifaArrowObj.SetActive(show);
        if (!show || mainCamera == null) return;

        float bob = 0f;
        if (fifaBobEnabled)
        {
            float t = (Mathf.Sin(Time.time * fifaBobSpeed) + 1f) * 0.5f;
            bob = fifaBobCurve.Evaluate(t) * fifaBobAmount;
        }

        Vector3 worldPos;

        if (currentTargetHealth != null && currentTargetHealth.HealthBarAnchor != null)
        {
            worldPos = currentTargetHealth.HealthBarAnchor.position + Vector3.up * (fifaHeightAboveEnemy + bob);
        }
        else
        {
            worldPos = target.position + Vector3.up * (fifaHeightAboveEnemy + bob);
        }

        fifaArrowObj.transform.position = worldPos;

        if (fifaBillboardToCamera)
            fifaArrowObj.transform.rotation = Quaternion.LookRotation(-(mainCamera.transform.position - worldPos));

        if (Mathf.Abs(fifaSelfRotationSpeed) > 0.001f)
            fifaArrowObj.transform.Rotate(0f, 0f, fifaSelfRotationSpeed * Time.deltaTime, Space.Self);

        bool applyColor = fifaMarkerMode == FIFAMarkerMode.Actual || fifaSpriteApplyColor;

        if (applyColor)
        {
            fifaColorTime += Time.deltaTime * fifaColorCycleSpeed;
            if (fifaColorTime > 1f) fifaColorTime -= 1f;

            Color baseColor = fifaColorCycleEnabled ? fifaColorGradient.Evaluate(fifaColorTime) : fifaStaticColor;

            float alpha = baseColor.a;
            if (fifaOpacityPulseEnabled)
            {
                float t = (Mathf.Sin(Time.time * fifaOpacityPulseSpeed) + 1f) * 0.5f;
                alpha = Mathf.Lerp(fifaOpacityMin, fifaOpacityMax, t);
            }

            fifaMaterial.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
        else if (fifaOpacityPulseEnabled)
        {
            float t = (Mathf.Sin(Time.time * fifaOpacityPulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(fifaOpacityMin, fifaOpacityMax, t);
            Color c = fifaMaterial.color;
            fifaMaterial.color = new Color(c.r, c.g, c.b, alpha);
        }

        float distToCam = Vector3.Distance(mainCamera.transform.position, worldPos);
        fifaArrowObj.transform.localScale = Vector3.one * (fifaArrowSize * distToCam * 0.04f);
    }

    #endregion

    #region FX - Dispatcher

    private void UpdateFX()
    {
        if (targetFXMode == FXMode.Arrows3D)
        {
            if (fifaArrowObj != null) fifaArrowObj.SetActive(false);
            UpdateArrows3D();
        }
        else
        {
            foreach (var a in arrows3D) if (a != null) a.SetActive(false);
            UpdateFIFA();
        }
    }

    public void SetFXEnabled(bool enabled)
    {
        if (enabled != showTargetFX) showTargetFX = enabled;
        if (!enabled) HideAllFX();
    }

    #endregion

    #region Gradient Helper

    private Gradient BuildDefaultGradient()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.yellow,  0.00f),
                new GradientColorKey(Color.red,     0.25f),
                new GradientColorKey(Color.magenta, 0.50f),
                new GradientColorKey(Color.cyan,    0.75f),
                new GradientColorKey(Color.yellow,  1.00f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );
        return g;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (!debugMode) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, autoAimRange);

        Vector3 fwd = transform.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + Quaternion.Euler(0, autoAimAngle, 0) * fwd * autoAimRange);
        Gizmos.DrawLine(transform.position, transform.position + Quaternion.Euler(0, -autoAimAngle, 0) * fwd * autoAimRange);

        if (currentTarget == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, currentTarget.position);
        Gizmos.DrawWireSphere(currentTarget.position, 0.5f);

        if (!showTargetFX) return;

        if (targetFXMode == FXMode.FIFA)
        {
            Gizmos.color = Color.yellow;
            Vector3 fifaPos = currentTarget.position + Vector3.up * fifaHeightAboveEnemy;
            Gizmos.DrawWireSphere(fifaPos, 0.15f);
            Gizmos.DrawLine(currentTarget.position, fifaPos);
        }

        if (targetFXMode == FXMode.Arrows3D)
        {
            Gizmos.color = Color.red;
            Vector3 c = currentTarget.position + Vector3.up * arrowOffset;
            Gizmos.DrawWireSphere(c + Vector3.forward * arrowDistance, 0.1f);
            Gizmos.DrawWireSphere(c + Vector3.right * arrowDistance, 0.1f);
            Gizmos.DrawWireSphere(c + Vector3.back * arrowDistance, 0.1f);
            Gizmos.DrawWireSphere(c + Vector3.left * arrowDistance, 0.1f);
        }
    }

    #endregion
}