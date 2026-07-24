using UnityEngine;

/// <summary>
/// Helper estático para gestionar offsets de pequeños HUDs de debug por esquina,
/// y resetear contadores cada frame para evitar solapamientos.
/// </summary>
public static class DebugHUDManager
{
    public enum Corner { TopLeft = 0, TopRight = 1, BottomLeft = 2, BottomRight = 3 }

    private static int lastFrame = -1;
    private static int[] counters = new int[4];

    // Resetea contadores una vez por frame
    private static void EnsureFrameReset()
    {
        if (Time.frameCount != lastFrame)
        {
            lastFrame = Time.frameCount;
            for (int i = 0; i < counters.Length; i++) counters[i] = 0;
        }
    }

    // Devuelve el índice de offset (0,1,2...) para la esquina pedida
    public static int GetNextIndex(Corner corner)
    {
        EnsureFrameReset();
        int idx = counters[(int)corner];
        counters[(int)corner] = idx + 1;
        return idx;
    }

    // Calcula la posición en pantalla para un rect en la esquina pedida
    public static Vector2 GetCornerPosition(Corner corner, float width, float height, int index, float padding)
    {
        float x = 0f, y = 0f;
        switch (corner)
        {
            case Corner.TopLeft:
                x = padding;
                y = padding + (height + padding) * index;
                break;
            case Corner.TopRight:
                x = Screen.width - width - padding;
                y = padding + (height + padding) * index;
                break;
            case Corner.BottomLeft:
                x = padding;
                y = Screen.height - height - padding - (height + padding) * index;
                break;
            case Corner.BottomRight:
                x = Screen.width - width - padding;
                y = Screen.height - height - padding - (height + padding) * index;
                break;
        }
        return new Vector2(x, y);
    }
}