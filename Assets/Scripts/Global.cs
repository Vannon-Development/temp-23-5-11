using UnityEngine;

public static class Extensions
{
    public static bool NearZero(this float f)
    {
        return Mathf.Abs(f) < 0.001;
    }
}
