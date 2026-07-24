using UnityEngine;
using UnityEngine.UI;

public class EntropyChargeUI : MonoBehaviour
{
    #region Inspector Fields
    [Header("Referencias UI")]
    public ChargeSlot[] chargeSlots = new ChargeSlot[EntropyChargeSystem.MaxCharges];

    [Header("Posición sobre el enemigo")]
    public float heightOffset = 2.5f;

    [Header("Colores")]
    public Color activeColor = new Color(1f, 0.45f, 0.1f, 1f);
    public Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
    public Color fillColor = new Color(1f, 0.8f, 0.1f, 1f);

    [SerializeField] private bool canForcedTransform = true;

    #endregion

    #region Private Fields
    private EntropyChargeSystem chargeSystem;
    private Transform enemyTransform;
    #endregion

    #region Nested Types
    [System.Serializable]
    public class ChargeSlot
    {
        public GameObject root;
        public Image background;
        public Image radialFill;
        public Image icon;
    }
    #endregion

    #region Unity
    private void Awake()
    {
        if (canForcedTransform)
        {
            enemyTransform = transform.parent;
        }
        chargeSystem = GetComponentInParent<EntropyChargeSystem>();
    }

    private void Start()
    {
        if (chargeSystem != null)
        {
            chargeSystem.OnChargesChanged += HandleChargesChanged;
            chargeSystem.OnChargesExpired += HandleChargesExpired;
        }

        SetAllSlotsVisible(false);
    }

    private void OnDestroy()
    {
        if (chargeSystem != null)
        {
            chargeSystem.OnChargesChanged -= HandleChargesChanged;
            chargeSystem.OnChargesExpired -= HandleChargesExpired;
        }
    }

    private void LateUpdate()
    {
        if (enemyTransform != null)
        {
            transform.position = enemyTransform.position + Vector3.up * heightOffset;
            transform.forward = Camera.main.transform.forward;
        }
    }
    #endregion

    #region Handlers
    private void HandleChargesChanged(int currentCharges, float timeRemaining, float totalTime)
    {
        if (currentCharges <= 0 || timeRemaining <= 0)
        {
            SetAllSlotsVisible(false);
            return;
        }

        SetAllSlotsVisible(true);

        float timePerCharge = chargeSystem.Config.chargeDuration;

        for (int i = 0; i < chargeSlots.Length; i++)
        {
            float lowerThreshold = i * timePerCharge;
            float timeInThisSlot = timeRemaining - lowerThreshold;

            float fillAmount = Mathf.Clamp01(timeInThisSlot / timePerCharge);
            bool isActive = fillAmount > 0;

            SetSlotFill(i, fillAmount, isActive);
        }
    }

    private void HandleChargesExpired()
    {
        SetAllSlotsVisible(false);
    }
    #endregion

    #region Helpers
    private void SetSlotFill(int index, float fill, bool active)
    {
        if (index >= chargeSlots.Length || chargeSlots[index] == null) return;
        var slot = chargeSlots[index];

        if (slot.radialFill != null)
        {
            slot.radialFill.fillAmount = fill;
            slot.radialFill.color = fillColor;
        }

        if (slot.background != null)
            slot.background.color = active ? activeColor : inactiveColor;

        if (slot.icon != null)
            slot.icon.color = active ? Color.white : inactiveColor;
    }

    private void SetAllSlotsVisible(bool visible)
    {
        for (int i = 0; i < chargeSlots.Length; i++)
        {
            if (chargeSlots[i] != null && chargeSlots[i].root != null)
                chargeSlots[i].root.SetActive(visible);
        }
    }
    #endregion
}