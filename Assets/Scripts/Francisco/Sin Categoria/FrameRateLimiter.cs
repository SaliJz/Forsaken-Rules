using System.Threading;
using UnityEngine;

public class FrameRateLimiter : MonoBehaviour
{
    [Tooltip("FPS máximo que permitirá la aplicación")]
    [SerializeField] private int targetFPS = 36;

    private float frameInterval;
    private float lastFrameTime;

    private static FrameRateLimiter instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFPS;
        frameInterval = 1f / targetFPS;
        lastFrameTime = Time.unscaledTime;

        Debug.Log($"[FrameRateLimiter] Objetivo: {targetFPS} FPS (intervalo {frameInterval:F4}s)");
    }

    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        float elapsed = now - lastFrameTime;
        float toWait = frameInterval - elapsed;

        if (toWait > 0f)
        {
            int ms = Mathf.CeilToInt(toWait * 1000f);
            Thread.Sleep(ms);
        }

        lastFrameTime = Time.unscaledTime;
    }
}