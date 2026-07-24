using TMPro;
using UnityEngine;
using System;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI manipulationIndicatorText;
    [SerializeField] private TextMeshProUGUI roomStatusText;
    [SerializeField] private TextMeshProUGUI roomTimerText;

    private float roomTimer = 0f;
    private bool isTimerRunning = false;

    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        manipulationIndicatorText.enabled = false;

        if (roomTimerText != null)
        {
            roomTimerText.gameObject.SetActive(false);
            roomTimerText.text = "Tiempo: 00:00";
        }
    }

    private void Start()
    {
        DungeonGenerator.OnRoomEntered += OnRoomEntered;
    }

    private void OnDestroy()
    {
        DungeonGenerator.OnRoomEntered -= OnRoomEntered;
    }

    private void OnRoomEntered(RoomType roomType)
    {
        if (roomType == RoomType.Combat)
        {
            roomTimer = 0f;
            isTimerRunning = true;
        }
        else
        {
            isTimerRunning = false;
        }
    }

    private void Update()
    {
        if (DevilManipulationManager.Instance != null && roomStatusText != null)
        {
            roomStatusText.text = DevilManipulationManager.Instance.GetCurrentManipulationStatus();
        }

        if (isTimerRunning)
        {
            roomTimer += Time.deltaTime;

            if (roomTimerText != null)
            {
                TimeSpan time = TimeSpan.FromSeconds(roomTimer);
                roomTimerText.text = string.Format("Tiempo: {0:D2}:{1:D2}", time.Minutes, time.Seconds);
                roomTimerText.gameObject.SetActive(true);
            }
        }
        else if (roomTimerText != null)
        {
            roomTimerText.gameObject.SetActive(false);
        }
    }

    public void ShowManipulationText(string message)
    {
        manipulationIndicatorText.text = message;
        manipulationIndicatorText.enabled = true;
    }

    public void ClearManipulationText()
    {
        manipulationIndicatorText.enabled = false;
        manipulationIndicatorText.text = string.Empty;
    }

    public float StopAndReportTimer()
    {
        if (isTimerRunning)
        {
            isTimerRunning = false;
            return roomTimer;
        }
        return 0f;
    }
}