using System;
using Avalonia.Media;

namespace AGI.Kapster.Desktop.Rendering;

/// <summary>
/// Tactical arrow width and color profiles along the path
/// </summary>
internal static class PathProfiles
{
    private const int SampleCount = 64;

    internal static ProfileResult BuildProfiles(
        double neckWidth, 
        double totalLength, 
        double neckRatio, 
        double tailNeckRatio,
        double fadeRatioMin,
        double fadeRatioMax,
        Color baseColor)
    {
        var tailWidth = neckWidth * tailNeckRatio;
        var decay = Math.Log(tailWidth / neckWidth) / Math.Max(neckRatio, 1e-3);
        
        var widths = new double[SampleCount];
        var colors = new Color[SampleCount];

        for (int i = 0; i < SampleCount; i++)
        {
            var t = i / (double)(SampleCount - 1);
            
            // Width: exponential decay from tail to neck
            var width = tailWidth * Math.Exp(-decay * t);
            width = Math.Max(width, neckWidth);
            widths[i] = width;

            // Alpha: 1 - exp(-m*t) from 0 to 1
            // m controls the fade speed, higher m = faster fade
            const double m = 4.0;
            var alpha = 1.0 - Math.Exp(-m * t);
            alpha = Math.Clamp(alpha, 0.0, 1.0);

            // Apply premultiplied alpha for smooth appearance
            colors[i] = PremultiplyAlpha(baseColor, alpha);
        }

        return new ProfileResult(widths, colors, tailWidth, neckWidth);
    }

    private static Color PremultiplyAlpha(Color color, double alpha)
    {
        var a = (byte)Math.Round(alpha * 255);
        return Color.FromArgb(
            a,
            (byte)(color.R * a / 255),
            (byte)(color.G * a / 255),
            (byte)(color.B * a / 255));
    }

    internal readonly record struct ProfileResult(
        double[] Widths, 
        Color[] Colors, 
        double TailWidth, 
        double NeckWidth);
}

