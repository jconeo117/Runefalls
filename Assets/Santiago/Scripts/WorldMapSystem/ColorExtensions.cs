using UnityEngine;

/// <summary>
/// Extension methods for Unity's Color struct.
/// </summary>
public static class ColorExtensions
{
    /// <summary>Returns a copy of the color with the alpha channel replaced.</summary>
    public static Color WithAlpha(this Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
