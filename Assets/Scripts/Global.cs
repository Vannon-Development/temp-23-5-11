using UnityEngine;

public static class Extensions
{
    public static bool NearZero(this float f)
    {
        return Mathf.Abs(f) < 0.001;
    }

    public static float Lerp(this float t, float t0, float t1, float x0, float x1)
    {
        return x0 + (x1 - x0) * (t - t0) / (t1 - t0); 
    }
}
