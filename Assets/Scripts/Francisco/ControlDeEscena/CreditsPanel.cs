using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class CreditsPanel : MonoBehaviour
{
    public enum PanelDisplayType { AnimatedScale, CanvasFade, Animator, Static }
    public enum EntryAlignment { Left, Center, Right }
    public enum EntryType { Title, Subtitle, Role, Name, RoleWithName, ImageWithText, Spacer }

    #region [ References ]

    [Header("Panel Display Settings")]
    public PanelDisplayType displayType = PanelDisplayType.AnimatedScale;

    [Header("Canvas Group Reference")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animator Reference")]
    [SerializeField] private Animator panelAnimator;
    [SerializeField] private string openTrigger = "Open";
    [SerializeField] private string closeTrigger = "Close";

    [Header("DOTween Settings")]
    [SerializeField] private float openCloseDuration = 0.3f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    [Header("Scale Animation Vectors")]
    [SerializeField] private Vector3 startScale = Vector3.zero;
    [SerializeField] private Vector3 endScale = Vector3.one;

    [Header("Focus Control")]
    [SerializeField] private GameObject firstSelectedButton;

    [Header("Credits Container")]
    [SerializeField] private RectTransform creditsViewport;
    [SerializeField] private RectTransform creditsContainer;
    [SerializeField] private float scrollSpeed = 50f;
    [SerializeField] private float autoScrollDelay = 1f;
    [SerializeField] private bool loopCredits = true;

    [Header("Alignment Offsets")]
    [SerializeField] private float rightAlignmentOffset = 0f;

    [Header("Credits Content")]
    [SerializeField] private CreditsEntry[] creditsEntries;

    [Header("Prefab References")]
    [SerializeField] private GameObject titlePrefab;
    [SerializeField] private GameObject subtitlePrefab;
    [SerializeField] private GameObject rolePrefab;
    [SerializeField] private GameObject namePrefab;
    [SerializeField] private GameObject roleWithNamePrefab;
    [SerializeField] private GameObject imageWithTextPrefab;
    [SerializeField] private GameObject spacerPrefab;

    [Header("Style Settings")]
    [SerializeField] private float defaultSpacing = 20f;
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color subtitleColor = Color.gray;
    [SerializeField] private Color roleColor = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private Color nameColor = Color.white;


    [SerializeField] private Material estadoDeVidaMaterial;

    private Tweener scrollTween;
    private List<GameObject> instantiatedObjects = new List<GameObject>();
    private float contentHeight;
    private float currentYPosition = 0f;
    private float currentLineMaxHeight = 0f;

    private const string PROP_CONDICION_DAÑO = "_Condicion";
    private const string PROP_TRANSPARENCIA = "_Transparencia";
    private const string PROP_INTENSIDAD_POSTERIZADO = "_IntensidadPosterizado";
    private const string PROP_CONDICION_VIDA = "_Condici_n";
    private const string PROP_TIEMPO_RESPIRACION = "_TiempoRespiraci_n";
    private const string PROP_TAMAÑO_DITHER = "_Tama_oDither";
    private const string PROP_LIMITE_DITHER = "_LimiteDither";
    private const string PROP_CONDICION_PIXEL = "_Condici_nPixel";
    private const string PROP_SIZE_PIXEL = "_SizePixel";

    #endregion

    #region [ Data Structures ]

    [System.Serializable]
    public class CreditsEntry
    {
        public EntryType entryType;
        public string textContent;
        public string secondaryText;
        public Sprite image;
        public EntryAlignment alignment = EntryAlignment.Center;
        public float customSpacing = 0f;
        public bool useCustomColor = false;
        public Color customColor = Color.white;
        public int fontSize = 0;
        public bool continueOnSameLine = false;
    }

    #endregion

    #region [ Unity Methods ]

    private void Start()
    {
        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.localScale = startScale;
        }
        else if (displayType == PanelDisplayType.CanvasFade && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        if (SceneManager.GetActiveScene().name != "Scene_Credits")
        {
            gameObject.SetActive(false);
        }

        SetFloat(PROP_CONDICION_DAÑO, 0f);
        SetFloat(PROP_TRANSPARENCIA, 72f);
        SetFloat(PROP_INTENSIDAD_POSTERIZADO, 0f);
        SetFloat(PROP_CONDICION_VIDA, 0f);
        SetFloat(PROP_TIEMPO_RESPIRACION, 4f);
        SetFloat(PROP_TAMAÑO_DITHER, 24.5f);
        SetFloat(PROP_LIMITE_DITHER, 0.3f);
        SetFloat(PROP_CONDICION_PIXEL, 0f);
        SetFloat(PROP_SIZE_PIXEL, 150f);
    }

    private void OnDestroy()
    {
        StopAutoScroll();
    }

    #endregion

    #region [ Panel Control ]

    public void OpenPanel()
    {
        gameObject.SetActive(true);

        StopAutoScroll();

        if (creditsContainer != null)
        {
            creditsContainer.DOKill();
            creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, 0f);
        }

        ClearCredits();

        BuildCredits();

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.localScale = startScale;
            transform.DOScale(endScale, openCloseDuration)
                .SetEase(openEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    SetInitialFocus();
                    StartAutoScroll();
                });
        }
        else if (displayType == PanelDisplayType.CanvasFade && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;

            canvasGroup.DOFade(1f, openCloseDuration)
                .SetEase(Ease.OutSine)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    SetInitialFocus();
                    StartAutoScroll();
                });
        }
        else if (displayType == PanelDisplayType.Animator && panelAnimator != null)
        {
            panelAnimator.SetTrigger(openTrigger);
            SetInitialFocus();
            StartAutoScroll();
        }
        else
        {
            SetInitialFocus();
            StartAutoScroll();
        }
    }

    public void ClosePanel()
    {
        StopAutoScroll();

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.DOScale(startScale, openCloseDuration)
                .SetEase(closeEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    ClearCredits();
                    if (creditsContainer != null)
                    {
                        creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, 0f);
                    }
                });
        }
        else if (displayType == PanelDisplayType.CanvasFade && canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;

            canvasGroup.DOFade(0f, openCloseDuration)
                .SetEase(Ease.InSine)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    ClearCredits();
                    if (creditsContainer != null)
                    {
                        creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, 0f);
                    }
                });
        }
        else if (displayType == PanelDisplayType.Animator && panelAnimator != null)
        {
            panelAnimator.SetTrigger(closeTrigger);
            StartCoroutine(DelayedDeactivate());
        }
        else
        {
            gameObject.SetActive(false);
            ClearCredits();
            if (creditsContainer != null)
            {
                creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, 0f);
            }
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private IEnumerator DelayedDeactivate()
    {
        yield return new WaitForSecondsRealtime(openCloseDuration);
        gameObject.SetActive(false);
        ClearCredits();
        if (creditsContainer != null)
        {
            creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, 0f);
        }
    }

    private void SetFloat(string property, float value)
    {
        estadoDeVidaMaterial.SetFloat(property, value);
    }

    #endregion

    #region [ Credits Building ]

    private void BuildCredits()
    {
        if (creditsContainer == null)
        {
            Debug.LogError("CreditsPanel: creditsContainer es nulo!");
            return;
        }

        currentYPosition = 0f;
        currentLineMaxHeight = 0f;

        bool isFirstInLine = true;
        float lineStartY = 0f;
        float spacingForThisLine = defaultSpacing;

        foreach (CreditsEntry entry in creditsEntries)
        {
            if (isFirstInLine)
            {
                lineStartY = currentYPosition;
                currentLineMaxHeight = 0f;
            }

            spacingForThisLine = entry.customSpacing > 0f ? entry.customSpacing : defaultSpacing;

            GameObject instance = SpawnEntry(entry);
            if (instance == null) continue;

            SetAlignment(instance, entry);

            float entryHeight = MeasureHeight(instance, entry, spacingForThisLine);
            PositionInstance(instance, lineStartY, entryHeight);

            if (entryHeight > currentLineMaxHeight)
                currentLineMaxHeight = entryHeight;

            instantiatedObjects.Add(instance);

            if (entry.continueOnSameLine)
            {
                isFirstInLine = false;
            }
            else
            {
                currentYPosition = lineStartY + currentLineMaxHeight + spacingForThisLine;
                isFirstInLine = true;
            }
        }

        Canvas.ForceUpdateCanvases();
        contentHeight = currentYPosition;
    }

    private GameObject SpawnEntry(CreditsEntry entry)
    {
        switch (entry.entryType)
        {
            case EntryType.Title:
            case EntryType.Subtitle:
            case EntryType.Role:
            case EntryType.Name:
                return SpawnText(GetPrefabForType(entry.entryType), entry.textContent, entry);

            case EntryType.RoleWithName:
                return SpawnRoleWithName(entry);

            case EntryType.ImageWithText:
                return SpawnImageWithText(entry);

            case EntryType.Spacer:
                return InstantiatePrefab(spacerPrefab);

            default:
                return null;
        }
    }

    private GameObject GetPrefabForType(EntryType type)
    {
        switch (type)
        {
            case EntryType.Title: return titlePrefab;
            case EntryType.Subtitle: return subtitlePrefab;
            case EntryType.Role: return rolePrefab;
            case EntryType.Name: return namePrefab;
            default: return null;
        }
    }

    private float MeasureHeight(GameObject instance, CreditsEntry entry, float spacing)
    {
        if (entry.entryType == EntryType.Spacer)
            return spacing * 2f;

        Canvas.ForceUpdateCanvases();
        return instance.GetComponent<RectTransform>().rect.height;
    }

    private void PositionInstance(GameObject instance, float lineStartY, float entryHeight)
    {
        RectTransform rt = instance.GetComponent<RectTransform>();
        float posY = -lineStartY - entryHeight * rt.pivot.y;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, posY);
    }

    private void ClearCredits()
    {
        foreach (GameObject obj in instantiatedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        instantiatedObjects.Clear();
    }

    #endregion

    #region [ Entry Spawners ]

    private GameObject SpawnText(GameObject prefab, string text, CreditsEntry entry)
    {
        GameObject instance = InstantiatePrefab(prefab);
        if (instance == null) return null;

        TextMeshProUGUI tmp = instance.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            Debug.LogError($"CreditsPanel: '{instance.name}' ({entry.entryType}) no tiene TextMeshProUGUI.");
            return instance;
        }

        tmp.text = text;
        tmp.color = entry.useCustomColor ? entry.customColor : GetDefaultColor(entry.entryType);
        if (entry.fontSize > 0) tmp.fontSize = entry.fontSize;

        return instance;
    }

    private GameObject SpawnRoleWithName(CreditsEntry entry)
    {
        GameObject instance = InstantiatePrefab(roleWithNamePrefab);
        if (instance == null) return null;

        TextMeshProUGUI[] texts = instance.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length < 2)
        {
            Debug.LogError($"CreditsPanel: '{instance.name}' (RoleWithName) necesita al menos 2 TextMeshProUGUI.");
            return instance;
        }

        texts[0].text = entry.textContent;
        texts[1].text = entry.secondaryText;

        texts[0].color = entry.useCustomColor ? entry.customColor : roleColor;
        texts[1].color = entry.useCustomColor ? entry.customColor : nameColor;

        if (entry.fontSize > 0)
        {
            texts[0].fontSize = entry.fontSize;
            texts[1].fontSize = entry.fontSize;
        }

        return instance;
    }

    private GameObject SpawnImageWithText(CreditsEntry entry)
    {
        GameObject instance = InstantiatePrefab(imageWithTextPrefab);
        if (instance == null) return null;

        Image img = instance.GetComponentInChildren<Image>();
        if (img != null && entry.image != null)
            img.sprite = entry.image;

        TextMeshProUGUI tmp = instance.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null)
        {
            Debug.LogError($"CreditsPanel: '{instance.name}' (ImageWithText) no tiene TextMeshProUGUI.");
            return instance;
        }

        tmp.text = entry.textContent;
        tmp.color = entry.useCustomColor ? entry.customColor : Color.white;
        if (entry.fontSize > 0) tmp.fontSize = entry.fontSize;

        return instance;
    }

    private GameObject InstantiatePrefab(GameObject prefab)
    {
        if (prefab == null) return null;
        return Instantiate(prefab, creditsContainer);
    }

    #endregion

    #region [ Alignment & Style ]

    private float GetTargetX(EntryAlignment alignment)
    {
        if (creditsViewport == null) return 0f;

        Vector3[] viewportCorners = new Vector3[4];
        creditsViewport.GetWorldCorners(viewportCorners);

        float viewportLeft = viewportCorners[0].x;
        float viewportRight = viewportCorners[2].x;
        float viewportWidth = viewportRight - viewportLeft;
        float boxWidth = viewportWidth / 3f;

        switch (alignment)
        {
            case EntryAlignment.Left:
                return viewportLeft + (boxWidth * 0.5f); 
            case EntryAlignment.Center:
                return viewportLeft + (boxWidth * 1.5f); 
            case EntryAlignment.Right:
                return viewportRight - (boxWidth * 0.5f); 
            default:
                return 0f;
        }
    }

    private void SetAlignment(GameObject instance, CreditsEntry entry)
    {
        RectTransform rt = instance.GetComponent<RectTransform>();
        if (rt == null) return;

        float worldX = GetTargetX(entry.alignment);

        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector3 currentPos = rt.position;
        rt.position = new Vector3(worldX, currentPos.y, currentPos.z);
    }

    private Color GetDefaultColor(EntryType type)
    {
        switch (type)
        {
            case EntryType.Title: return titleColor;
            case EntryType.Subtitle: return subtitleColor;
            case EntryType.Role: return roleColor;
            case EntryType.Name: return nameColor;
            default: return Color.white;
        }
    }

    #endregion

    #region [ Gamepad Focus Control ]

  private void SetInitialFocus()
{
    if (firstSelectedButton == null) return;

    bool isGamepadModeActive = GamepadPointer.Instance != null && GamepadPointer.Instance.IsGamepadMode();

    if (!isGamepadModeActive) return;

    if (EventSystem.current != null)
    {
        EventSystem.current.SetSelectedGameObject(null);
    }
    ResetInputDevices();
    EventSystem.current.SetSelectedGameObject(firstSelectedButton);
}

    private void ResetInputDevices()
    {
        if (Gamepad.current != null)
        {
            InputSystem.ResetDevice(Gamepad.current);
        }
        if (Keyboard.current != null)
        {
            InputSystem.ResetDevice(Keyboard.current);
        }
        if (Mouse.current != null)
        {
            InputSystem.ResetDevice(Mouse.current);
        }
    }

    #endregion

    #region [ Auto Scroll ]

    private void StartAutoScroll()
    {
        StopAutoScroll();

        if (creditsContainer == null || creditsViewport == null)
        {
            Debug.LogError("CreditsPanel: creditsContainer o creditsViewport es nulo. No se puede iniciar scroll.");
            return;
        }

        DOVirtual.DelayedCall(autoScrollDelay, () =>
        {
            if (this != null && gameObject.activeInHierarchy)
            {
                PerformScroll();
            }
        }, true);
    }

    private void PerformScroll()
    {
        if (contentHeight <= 0f)
        {
            Debug.LogWarning("CreditsPanel: La altura del contenido es 0. Asegúrate de que el Content Size Fitter esté configurado.");
            return;
        }

        float viewportHeight = creditsViewport.rect.height;
        float totalDistance = contentHeight + viewportHeight;

        float duration = totalDistance / scrollSpeed;

        if (creditsContainer != null)
        {
            creditsContainer.DOKill();
            creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, 0f);
        }

        float targetY = contentHeight + viewportHeight;

        scrollTween = creditsContainer.DOAnchorPosY(targetY, duration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (loopCredits && this != null && gameObject.activeInHierarchy)
                {
                    PerformScroll();
                }
            });
    }

    private void StopAutoScroll()
    {
        if (scrollTween != null && scrollTween.IsActive())
        {
            scrollTween.Kill(false);
            scrollTween = null;
        }
    }

    public void PauseScroll()
    {
        if (scrollTween != null && scrollTween.IsActive())
        {
            scrollTween.Pause();
        }
    }

    public void ResumeScroll()
    {
        if (scrollTween != null && scrollTween.IsActive())
        {
            scrollTween.Play();
        }
        else
        {
            StartAutoScroll();
        }
    }

    public void ResetScroll()
    {
        StopAutoScroll();
        if (creditsContainer != null)
        {
            creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, 0f);
        }
        StartAutoScroll();
    }

    #endregion

    #region [ Gizmos ]

    private void OnDrawGizmos()
    {
        if (creditsViewport == null) return;

        Vector3[] viewportCorners = new Vector3[4];
        creditsViewport.GetWorldCorners(viewportCorners);

        float viewportZ = viewportCorners[0].z;
        float viewportLeft = viewportCorners[0].x;
        float viewportRight = viewportCorners[2].x;
        float viewportWidth = viewportRight - viewportLeft;
        float topY = viewportCorners[1].y;
        float bottomY = viewportCorners[0].y;
        float viewportHeight = topY - bottomY;

        float boxWidth = viewportWidth / 3f;
        float boxHeight = viewportHeight;
        float boxCenterY = bottomY + viewportHeight * 0.5f;

        float leftX = viewportLeft + (boxWidth * 0.5f);
        float centerX = viewportLeft + (boxWidth * 1.5f);
        float rightX = viewportRight - (boxWidth * 0.5f);

        void DrawBox(float cx, float cy, float w, float h)
        {
            float hw = w * 0.5f;
            float hh = h * 0.5f;
            Vector3 bl = new Vector3(cx - hw, cy - hh, viewportZ);
            Vector3 tl = new Vector3(cx - hw, cy + hh, viewportZ);
            Vector3 tr = new Vector3(cx + hw, cy + hh, viewportZ);
            Vector3 br = new Vector3(cx + hw, cy - hh, viewportZ);
            Gizmos.DrawLine(bl, tl);
            Gizmos.DrawLine(tl, tr);
            Gizmos.DrawLine(tr, br);
            Gizmos.DrawLine(br, bl);
        }

        void DrawCenterLine(float cx)
        {
            Gizmos.DrawLine(
                new Vector3(cx, bottomY, viewportZ),
                new Vector3(cx, topY, viewportZ));
        }

        Gizmos.color = new Color(0f, 1f, 0f, 0.8f);
        DrawBox(leftX, boxCenterY, boxWidth, boxHeight);
        DrawBox(centerX, boxCenterY, boxWidth, boxHeight);
        DrawBox(rightX, boxCenterY, boxWidth, boxHeight);

        Gizmos.color = new Color(1f, 0f, 0f, 0.9f);
        DrawCenterLine(leftX);
        DrawCenterLine(centerX);
        DrawCenterLine(rightX);

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.red;
        float labelY = topY + 12f;
        UnityEditor.Handles.Label(new Vector3(leftX, labelY, viewportZ), "L");
        UnityEditor.Handles.Label(new Vector3(centerX, labelY, viewportZ), "C");
        UnityEditor.Handles.Label(new Vector3(rightX, labelY, viewportZ), "R");
#endif
    }

    #endregion

    #region [ Public Methods ]

    public void SetScrollSpeed(float speed)
    {
        scrollSpeed = Mathf.Max(0f, speed);
    }

    public void AddCreditsEntry(CreditsEntry entry)
    {
        var list = new List<CreditsEntry>(creditsEntries);
        list.Add(entry);
        creditsEntries = list.ToArray();
    }

    public void ClearAllEntries()
    {
        creditsEntries = new CreditsEntry[0];
        ClearCredits();
    }

    #endregion
}