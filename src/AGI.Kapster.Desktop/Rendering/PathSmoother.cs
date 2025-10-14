using System;
using System.Collections.Generic;
using Avalonia;

namespace AGI.Kapster.Desktop.Rendering;

/// <summary>
/// Smooths mouse trail into a curved path using Catmull-Rom splines
/// </summary>
internal static class PathSmoother
{
    internal static SmoothResult Generate(IReadOnlyList<Point> trail)
    {
        if (trail == null || trail.Count < 2)
        {
            return new SmoothResult(
                Array.Empty<PathSample>(),
                0.0,
                0.0);
        }

        if (trail.Count == 2)
        {
            // Straight line
            var start = trail[0];
            var end = trail[1];
            var length = Distance(start, end);
            var tangent = Normalize(new Vector(end.X - start.X, end.Y - start.Y));
            
            var samples = new List<PathSample>
            {
                new PathSample(start, tangent, 0.0, 0.0),
                new PathSample(end, tangent, length, 1.0)
            };
            
            return new SmoothResult(samples, length, 0.0);
        }

        // Build smooth curve from trail points
        var result = new List<PathSample>();
        double totalLength = 0.0;
        
        // Calculate bend (deviation from straight line)
        var straightLine = new Vector(trail[^1].X - trail[0].X, trail[^1].Y - trail[0].Y);
        var straightLength = Math.Sqrt(straightLine.X * straightLine.X + straightLine.Y * straightLine.Y);
        double maxDeviation = 0.0;
        
        if (straightLength > 1e-6)
        {
            var straightDir = new Vector(straightLine.X / straightLength, straightLine.Y / straightLength);
            foreach (var pt in trail)
            {
                var toPoint = new Vector(pt.X - trail[0].X, pt.Y - trail[0].Y);
                var alongLine = straightDir.X * toPoint.X + straightDir.Y * toPoint.Y;
                var onLine = new Point(
                    trail[0].X + straightDir.X * alongLine,
                    trail[0].Y + straightDir.Y * alongLine);
                var deviation = Distance(pt, onLine);
                maxDeviation = Math.Max(maxDeviation, deviation);
            }
        }
        
        // Sample the curve
        const int samplesPerSegment = 8;
        for (int i = 0; i < trail.Count - 1; i++)
        {
            var p0 = trail[Math.Max(0, i - 1)];
            var p1 = trail[i];
            var p2 = trail[i + 1];
            var p3 = trail[Math.Min(trail.Count - 1, i + 2)];
            
            int numSamples = (i == trail.Count - 2) ? samplesPerSegment + 1 : samplesPerSegment;
            for (int s = 0; s < numSamples; s++)
            {
                var t = s / (double)samplesPerSegment;
                var pos = CatmullRom(p0, p1, p2, p3, t);
                
                // Estimate tangent
                var epsilon = 0.01;
                var tPrev = Math.Max(0, t - epsilon);
                var tNext = Math.Min(1, t + epsilon);
                var posPrev = CatmullRom(p0, p1, p2, p3, tPrev);
                var posNext = CatmullRom(p0, p1, p2, p3, tNext);
                var tangent = Normalize(new Vector(posNext.X - posPrev.X, posNext.Y - posPrev.Y));
                
                // Calculate arc length
                if (result.Count > 0)
                {
                    totalLength += Distance(result[^1].Position, pos);
                }
                
                var progress = result.Count / (double)(trail.Count * samplesPerSegment);
                result.Add(new PathSample(pos, tangent, totalLength, progress));
            }
        }
        
        var signedBend = maxDeviation / Math.Max(straightLength, 1.0);
        return new SmoothResult(result, totalLength, signedBend);
    }

    private static Point CatmullRom(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        
        var x = 0.5 * ((2 * p1.X) +
                       (-p0.X + p2.X) * t +
                       (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
                       (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
        
        var y = 0.5 * ((2 * p1.Y) +
                       (-p0.Y + p2.Y) * t +
                       (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
                       (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);
        
        return new Point(x, y);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Vector Normalize(Vector v)
    {
        var len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        if (len < 1e-6) return new Vector(1, 0);
        return new Vector(v.X / len, v.Y / len);
    }

    internal readonly record struct PathSample(
        Point Position,
        Vector Tangent,
        double ArcLength,
        double Progress);

    internal readonly record struct SmoothResult(
        IReadOnlyList<PathSample> Samples,
        double TotalLength,
        double SignedBend);
}
