

namespace AntiFraud.Core.Shared.Utils;

public static class Utils
{
    public static float Clamp(float value, float min = 0.0f, float max = 1.0f)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static float VectorizeRound(this float value) => MathF.Round(value, 4);
}