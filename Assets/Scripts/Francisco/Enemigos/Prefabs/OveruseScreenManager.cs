using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class OveruseScreenManager : MonoBehaviour //Quizas deba cambiar su nombre algo mas general. Quizas Jamil creara mas enemigos con esta funcioanlidad
{
    #region Singleton
    public static OveruseScreenManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    #endregion

    #region Inspector Fields
    [Header("UI Components")]
    [SerializeField] private Image whiteOverlay;
    [SerializeField] private GameObject faceSilhouettesGroup;
    [SerializeField] private CanvasGroup silhouettesCanvasGroup;

    [Header("Settings")]
    [SerializeField] private float maxValue = 1.0f;
    [SerializeField] private float decayRate = 0.1f;
    [SerializeField] private float silhouetteThreshold = 0.4f;
    #endregion

    #region Private Fields
    private float currentScreenValue;
    private bool isBeingAffected;
    #endregion

    #region Unity Events
    private void Update()
    {
        HandleValueDecay();
        UpdateVisuals();

        isBeingAffected = false;
    }
    #endregion

    #region Public Methods
    public void AddValue(float amount)
    {
        isBeingAffected = true;
        currentScreenValue += amount * Time.deltaTime;
        currentScreenValue = Mathf.Clamp(currentScreenValue, 0, maxValue);
    }

    public void ResetDecayTimer()
    {
        isBeingAffected = true;
    }
    #endregion

    #region Private Methods
    private void HandleValueDecay()
    {
        if (isBeingAffected) return;

        if (currentScreenValue > 0)
        {
            currentScreenValue -= decayRate * Time.deltaTime;
            currentScreenValue = Mathf.Max(currentScreenValue, 0);
        }
    }

    private void UpdateVisuals()
    {
        if (whiteOverlay != null)
        {
            Color tempColor = whiteOverlay.color;
            tempColor.a = currentScreenValue / maxValue;
            whiteOverlay.color = tempColor;
        }

        if (silhouettesCanvasGroup != null)
        {
            float alpha = currentScreenValue >= silhouetteThreshold
                ? (currentScreenValue - silhouetteThreshold) / (maxValue - silhouetteThreshold)
                : 0f;

            silhouettesCanvasGroup.alpha = alpha;

            if (faceSilhouettesGroup != null)
            {
                faceSilhouettesGroup.SetActive(alpha > 0);
            }
        }
    }
    #endregion
}