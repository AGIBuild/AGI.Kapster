using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace AGI.Kapster.Desktop.Rendering;

/// <summary>
/// Builds tactical arrows using a single cubic Bezier curve for the body
/// </summary>
internal static class TacticalArrowBuilder
{
    private const double ShadowOffset = 2.0;
    
    private enum ArrowLengthClass
    {
        Micro,   // <60px
        Short,   // 60-120px
        Medium,  // 120-250px
        Long,    // 250-450px
        XLong    // >450px
    }

    internal static TacticalArrowResult Build(TacticalArrowRequest request)
    {
        // Step 1: Find the maximum deviation point from the trail
        var (controlPoint, maxDeviation) = FindMaxDeviationPoint(
            request.Trail ?? new List<Point> { request.Start, request.End }, 
            request.Start, 
            request.End);
        
        // Step 2: Calculate base length and classify arrow
        var straightDistance = Distance(request.Start, request.End);
        var baseLength = straightDistance;
        var lengthClass = ClassifyLength(baseLength);
        
        // Step 3: Compute size parameters
        var sizeParams = ComputeSizeParameters(baseLength, request.StrokeThickness, lengthClass);
        
        // Step 4: Build the arrow using a single cubic bezier curve
        var result = BuildSimpleTacticalArrow(
            request.Start,
            request.End,
            controlPoint,
            request.Color,
            sizeParams,
            baseLength);

        return result;
    }

    /// <summary>
    /// Find the point in the trail with maximum perpendicular deviation from the start-end line
    /// </summary>
    private static (Point ControlPoint, double MaxDeviation) FindMaxDeviationPoint(
        IReadOnlyList<Point> trail,
        Point start,
        Point end)
    {
        if (trail.Count < 3)
        {
            // No curvature, return midpoint
            return (new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2), 0);
        }

        var lineVector = new Vector(end.X - start.X, end.Y - start.Y);
        var lineLength = Math.Sqrt(lineVector.X * lineVector.X + lineVector.Y * lineVector.Y);
        if (lineLength < 1e-6)
        {
            return (new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2), 0);
        }

        var lineDir = new Vector(lineVector.X / lineLength, lineVector.Y / lineLength);
        var lineNormal = new Vector(-lineDir.Y, lineDir.X);

        double maxAbsDeviation = 0;
        Point bestPoint = start;
        double bestSignedDeviation = 0;

        foreach (var point in trail)
        {
            // Skip start and end points
            if (Distance(point, start) < 1e-3 || Distance(point, end) < 1e-3)
                continue;

            var toPoint = new Vector(point.X - start.X, point.Y - start.Y);
            var deviation = Vector.Dot(toPoint, lineNormal);
            
            if (Math.Abs(deviation) > maxAbsDeviation)
            {
                maxAbsDeviation = Math.Abs(deviation);
                bestPoint = point;
                bestSignedDeviation = deviation;
            }
        }

        // If no significant deviation, use midpoint
        if (maxAbsDeviation < 5.0)
        {
            return (new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2), 0);
        }

        return (bestPoint, bestSignedDeviation);
    }

    /// <summary>
    /// Build a tactical arrow using a single cubic bezier curve for the body
    /// </summary>
    private static TacticalArrowResult BuildSimpleTacticalArrow(
        Point start,
        Point end,
        Point deviationPoint,
        Color baseColor,
        ArrowSizeParameters sizeParams,
        double totalLength)
    {
        // Calculate cubic bezier control points
        var (cp1, cp2) = CalculateCubicBezierControlPoints(start, end, deviationPoint);

        // Calculate the target direction (start to end)
        var targetDirection = Normalize(new Vector(end.X - start.X, end.Y - start.Y));

        // Sample the bezier curve to build the arrow body
        // Use more samples for smoother curves, with denser sampling near the end
        const int baseSamples = 48;
        const int endSamples = 16; // Additional samples in the last 20% for accurate end tangent
        var bodyPoints = new List<(Point pos, Vector tangent, double t)>();
        
        // Sample the main body (0 to 80%)
        for (int i = 0; i < baseSamples; i++)
        {
            var t = i / (double)(baseSamples - 1) * 0.8;
            var pos = CubicBezier(start, cp1, cp2, end, t);
            var curveTangent = CubicBezierTangent(start, cp1, cp2, end, t);
            bodyPoints.Add((pos, Normalize(curveTangent), t));
        }
        
        // Densely sample the end region (80% to 100%) for accurate tangent
        for (int i = 1; i <= endSamples; i++)
        {
            var t = 0.8 + (i / (double)endSamples) * 0.2;
            var pos = CubicBezier(start, cp1, cp2, end, t);
            var curveTangent = CubicBezierTangent(start, cp1, cp2, end, t);
            bodyPoints.Add((pos, Normalize(curveTangent), t));
        }

        // Build arrow geometry
        var neckT = sizeParams.NeckRatio;
        var neckIndex = (int)(neckT * (bodyPoints.Count - 1));
        
        var tailWidth = sizeParams.NeckWidth * sizeParams.TailNeckRatio;
        
        // CRITICAL: Use the actual curve tangent at start for tail alignment
        // This prevents tail distortion when the curve has large curvature
        var tailTangent = bodyPoints[0].tangent;
        var tailNormal = new Vector(-tailTangent.Y, tailTangent.X);
        var halfTailWidth = tailWidth * 0.5;
        var tailLeft = new Point(
            start.X + tailNormal.X * halfTailWidth,
            start.Y + tailNormal.Y * halfTailWidth);
        var tailRight = new Point(
            start.X - tailNormal.X * halfTailWidth,
            start.Y - tailNormal.Y * halfTailWidth);
        
        // Build body edges with width profile - extend all the way to the curve end
        var leftEdge = new List<Point>();
        var rightEdge = new List<Point>();
        
        for (int i = 0; i < bodyPoints.Count; i++)
        {
            var (pos, tangent, t) = bodyPoints[i];
            
            if (i == 0)
            {
                // CRITICAL: Use pre-calculated tail points for first edge points
                // This ensures perfect alignment with swallow tail
                leftEdge.Add(tailLeft);
                rightEdge.Add(tailRight);
            }
            else
            {
            var normal = new Vector(-tangent.Y, tangent.X);
                
                // Width decays exponentially from tail to neck, then stays constant
                double width;
                if (t <= sizeParams.NeckRatio)
                {
                    var decay = Math.Log(tailWidth / sizeParams.NeckWidth) / Math.Max(sizeParams.NeckRatio, 1e-3);
                    width = tailWidth * Math.Exp(-decay * t);
                    width = Math.Max(width, sizeParams.NeckWidth);
                }
                else
                {
                    // After neck, maintain constant neck width to the end
                    width = sizeParams.NeckWidth;
                }
                
                var halfWidth = width * 0.5;
                leftEdge.Add(pos + normal * halfWidth);
                rightEdge.Add(pos - normal * halfWidth);
            }
        }

        // Build arrowhead at the curve end with smooth integration
        // CRITICAL: Use the actual curve tangent at the end for arrowhead direction
        // The arrowhead base should align with and embed into the curve's final edge points
        var curveEndPos = bodyPoints[^1].pos;
        var curveEndTangent = bodyPoints[^1].tangent;
        var curveEndNormal = new Vector(-curveEndTangent.Y, curveEndTangent.X);
        
        var headBaseWidth = sizeParams.NeckWidth * sizeParams.HeadBaseMultiplier;
        var headLength = headBaseWidth * sizeParams.HeadLengthMultiplier;
        
        // Enforce minimum arrowhead size for visibility
        const double minHeadBaseWidth = 12.0;
        const double minHeadLength = 16.0;
        if (headBaseWidth < minHeadBaseWidth || headLength < minHeadLength)
        {
            headBaseWidth = Math.Max(headBaseWidth, minHeadBaseWidth);
            headLength = Math.Max(headLength, minHeadLength);
        }
        
        // CRITICAL: Arrowhead base should be embedded slightly behind the curve end
        // This creates a smooth transition without a visible step
        const double embedDepth = 2.0; // Embed 2px into the curve
        var headBaseCenter = curveEndPos - curveEndTangent * embedDepth;
        
        var headLeft = headBaseCenter + curveEndNormal * headBaseWidth * 0.5;
        var headRight = headBaseCenter - curveEndNormal * headBaseWidth * 0.5;
        var headTip = curveEndPos + curveEndTangent * headLength;

        // Build geometry with dynamic tail depth
        var tailDepth = Math.Min(tailWidth * 0.5, totalLength * 0.08); // Limit tail depth to 8% of arrow length
        var geometry = BuildGeometry(leftEdge, rightEdge, headLeft, headRight, headTip, tailLeft, tailRight, start, tailTangent, tailDepth);
        
        // Build gradient with alpha fade along the actual curve path
        // Use all body points to create a smooth gradient that follows the curve
        var fill = BuildCurveAlphaGradient(bodyPoints, baseColor);
        
        var shadowBrush = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0));
        var shadowTransform = new TranslateTransform(ShadowOffset, ShadowOffset);

        // Convert body samples for external consumers (renderer)
        var samples = new List<ArrowSample>(bodyPoints.Count);
        foreach (var bp in bodyPoints)
        {
            samples.Add(new ArrowSample(bp.pos, bp.tangent, bp.t));
        }

        return new TacticalArrowResult(
            geometry,
            fill,
            shadowBrush,
            shadowTransform,
            0,
            sizeParams.NeckRatio,
            samples,
            sizeParams.NeckWidth,
            sizeParams.NeckWidth * sizeParams.TailNeckRatio);
    }

    private static (Point cp1, Point cp2) CalculateCubicBezierControlPoints(Point start, Point end, Point through)
    {
        // Calculate control points so the bezier curve passes near the 'through' point
        // For a smoother curve, we place control points to attract the curve toward 'through'
        
        var cp1 = new Point(
            start.X + (through.X - start.X) * 0.5,
            start.Y + (through.Y - start.Y) * 0.5);
        
        var cp2 = new Point(
            end.X + (through.X - end.X) * 0.5,
            end.Y + (through.Y - end.Y) * 0.5);
        
        return (cp1, cp2);
    }

    private static Point CubicBezier(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var omt = 1 - t;
        var omt2 = omt * omt;
        var omt3 = omt2 * omt;
        var t2 = t * t;
        var t3 = t2 * t;
        
        return new Point(
            omt3 * p0.X + 3 * omt2 * t * p1.X + 3 * omt * t2 * p2.X + t3 * p3.X,
            omt3 * p0.Y + 3 * omt2 * t * p1.Y + 3 * omt * t2 * p2.Y + t3 * p3.Y);
    }

    private static Vector CubicBezierTangent(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var omt = 1 - t;
        var omt2 = omt * omt;
        var t2 = t * t;
        
        return new Vector(
            -3 * omt2 * p0.X + 3 * omt2 * p1.X - 6 * omt * t * p1.X + 6 * omt * t * p2.X - 3 * t2 * p2.X + 3 * t2 * p3.X,
            -3 * omt2 * p0.Y + 3 * omt2 * p1.Y - 6 * omt * t * p1.Y + 6 * omt * t * p2.Y - 3 * t2 * p2.Y + 3 * t2 * p3.Y);
    }

    private static Geometry BuildGeometry(
        List<Point> left,
        List<Point> right,
        Point headLeft,
        Point headRight,
        Point headTip,
        Point tailLeft,
        Point tailRight,
        Point tailStart,
        Vector tailDirection,
        double tailDepth)
    {
        // Build swallow tail V-notch from provided symmetric tail points
        var tailNotch = new Point(
            tailStart.X + tailDirection.X * tailDepth,
            tailStart.Y + tailDirection.Y * tailDepth);
        
        var figure = new PathFigure { StartPoint = tailLeft, IsClosed = true };
        var segments = new PathSegments();

        // Smooth connection from tail to body left edge
        var tailToBodyControl = new Point(
            (tailLeft.X + left[0].X) * 0.5,
            (tailLeft.Y + left[0].Y) * 0.5);
        segments.Add(new QuadraticBezierSegment { Point1 = tailToBodyControl, Point2 = left[0] });
        
        // Left edge with smooth bezier curves
        for (int i = 1; i < left.Count; i++)
        {
            if (i == 1 || i == left.Count - 1)
            {
                // First and last segments use direct connection
                segments.Add(new LineSegment { Point = left[i] });
            }
            else
            {
                // Use quadratic bezier for smooth curves
                var control = new Point(
                    (left[i - 1].X + left[i].X) * 0.5,
                    (left[i - 1].Y + left[i].Y) * 0.5);
                segments.Add(new QuadraticBezierSegment { Point1 = control, Point2 = left[i] });
            }
        }

        // CRITICAL: Smooth transition from body edge to arrowhead base
        // Use the last few body edge points to create a smooth Bezier curve into the arrowhead
        // This eliminates the "step" appearance at the arrow base
        
        if (left.Count >= 3)
        {
            // Use second-to-last point as control for smooth curve into arrowhead
            var leftControl = left[^2];
            segments.Add(new QuadraticBezierSegment { Point1 = leftControl, Point2 = headLeft });
        }
        else
        {
            segments.Add(new LineSegment { Point = headLeft });
        }
        
        // Arrow head triangle (sharp edges)
        segments.Add(new LineSegment { Point = headTip });
        segments.Add(new LineSegment { Point = headRight });
        
        // CRITICAL: Smooth transition from arrowhead base back to body edge
        if (right.Count >= 3)
        {
            var rightControl = right[^2];
            segments.Add(new QuadraticBezierSegment { Point1 = rightControl, Point2 = right[^1] });
        }
        else
        {
            segments.Add(new LineSegment { Point = right[^1] });
        }

        // Right edge with smooth bezier curves (reverse order)
        for (int i = right.Count - 2; i >= 0; i--)
        {
            if (i == 0 || i == right.Count - 2)
            {
                // First and last segments use direct connection
                segments.Add(new LineSegment { Point = right[i] });
            }
            else
            {
                // Use quadratic bezier for smooth curves
                var control = new Point(
                    (right[i + 1].X + right[i].X) * 0.5,
                    (right[i + 1].Y + right[i].Y) * 0.5);
                segments.Add(new QuadraticBezierSegment { Point1 = control, Point2 = right[i] });
            }
        }

        // Smooth connection from body right edge to tail
        var bodyToTailControl = new Point(
            (right[0].X + tailRight.X) * 0.5,
            (right[0].Y + tailRight.Y) * 0.5);
        segments.Add(new QuadraticBezierSegment { Point1 = bodyToTailControl, Point2 = tailRight });
        
        // Swallow tail V-notch (keep sharp for characteristic look)
        segments.Add(new LineSegment { Point = tailNotch });

        figure.Segments = segments;
        return new PathGeometry { Figures = new PathFigures { figure } };
    }

    private static IBrush BuildCurveAlphaGradient(List<(Point pos, Vector tangent, double t)> bodyPoints, Color baseColor)
    {
        if (bodyPoints.Count < 2)
        {
            return new SolidColorBrush(baseColor);
        }

        var stops = new GradientStops();
        
        // CRITICAL: Extend transparent fade to 70% of arrow length for longer, smoother gradient
        const double fadeEndRatio = 0.70;
        const int stopCount = 64; // Much more stops for ultra-smooth gradient without banding
        
        // Add initial fully transparent stop at 0%
        stops.Add(new GradientStop(Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B), 0.0));

        for (int i = 1; i < stopCount; i++)
        {
            var ratio = i / (double)(stopCount - 1);
            
            if (ratio <= fadeEndRatio)
            {
                // Exponential fade formula: alpha(t) = 1 - exp(-m * t)
                // Use m=1.5 for very gradual, natural fade
                var normalizedT = ratio / fadeEndRatio; // Normalize to 0-1 within fade region
                const double m = 1.5;
                var alpha = 1.0 - Math.Exp(-m * normalizedT);
                
                // Apply perceptual gamma correction for smooth visual transition
                alpha = Math.Pow(alpha, 1.0 / 2.2);
                alpha = Math.Clamp(alpha, 0.0, 1.0);
                
                // Create color with calculated alpha
                var color = Color.FromArgb(
                    (byte)(alpha * baseColor.A),
                    baseColor.R,
                    baseColor.G,
                    baseColor.B);
                
                stops.Add(new GradientStop(color, ratio));
            }
            else
            {
                // Fully opaque from fadeEndRatio to 100%
                stops.Add(new GradientStop(baseColor, ratio));
            }
        }
        
        // Ensure fully opaque at the end
        stops.Add(new GradientStop(baseColor, 1.0));

        // Use the curve path for gradient direction
        var start = bodyPoints[0].pos;
        var end = bodyPoints[^1].pos;

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(start, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(end, RelativeUnit.Absolute),
            GradientStops = stops,
            SpreadMethod = GradientSpreadMethod.Pad,
            Opacity = 1.0
        };
    }

    private static ArrowLengthClass ClassifyLength(double baseLength)
    {
        if (baseLength < 60) return ArrowLengthClass.Micro;
        if (baseLength < 120) return ArrowLengthClass.Short;
        if (baseLength < 250) return ArrowLengthClass.Medium;
        if (baseLength < 450) return ArrowLengthClass.Long;
        return ArrowLengthClass.XLong;
    }
    
    private static ArrowSizeParameters ComputeSizeParameters(double baseLength, double userSize, ArrowLengthClass lengthClass)
    {
        // New saturation function with better distribution: y(x) = min + (max - min) * (1 - e^(-k*x))
        // This provides better differentiation across size 1-20 range
        var x = Math.Clamp(userSize, 1.0, 20.0);
        
        // Improved saturation function with explicit min/max range
        double SaturationFunc(double input, double min, double max, double k)
        {
            var range = max - min;
            return Math.Round(min + range * (1.0 - Math.Exp(-k * input)));
        }
        
        // Neck width: range 1-16px, k=0.18 for good spread with smaller minimum
        // size=1: ~1.16px, size=7: ~8.7px, size=20: ~14.5px
        var neckWidth = SaturationFunc(x, 1.0, 16.0, 0.18);
        
        // Ensure minimum width based on length class (reduced minimum values)
        double minNeckWidth = lengthClass switch
        {
            ArrowLengthClass.Micro => 1.0,
            ArrowLengthClass.Short => 1.5,
            ArrowLengthClass.Medium => 2.0,
            ArrowLengthClass.Long => 2.5,
            ArrowLengthClass.XLong => 3.0,
            _ => 2.0
        };
        neckWidth = Math.Max(neckWidth, minNeckWidth);
        
        // Cap maximum neck width to prevent extreme sizes
        neckWidth = Math.Min(neckWidth, 28.0);
        
        // Tail width: range 3-40px, k=0.14 for wider range with smaller minimum
        // size=1: ~3.4px, size=7: ~19.8px, size=20: ~35.3px
        var tailWidth = SaturationFunc(x, 3.0, 40.0, 0.14);
        var tailNeckRatio = tailWidth / Math.Max(neckWidth, 1.0);
        
        // Head base width: range 4-32px, k=0.15 for moderate growth with smaller minimum
        // size=1: ~4.57px, size=7: ~20.5px, size=20: ~29.8px
        var headBaseWidth = SaturationFunc(x, 4.0, 32.0, 0.15);
        var headBaseMultiplier = headBaseWidth / Math.Max(neckWidth, 1.0);
        
        // Head length: proportional to head base width
        var headLengthFactor = lengthClass switch
        {
            ArrowLengthClass.Micro => 1.0,
            ArrowLengthClass.Short => 1.2,
            ArrowLengthClass.Medium => 1.4,
            ArrowLengthClass.Long => 1.6,
            ArrowLengthClass.XLong => 1.8,
            _ => 1.4
        };
        var headLengthMultiplier = headLengthFactor;
        
        // Neck ratio (position in path)
        var neckRatio = lengthClass switch
        {
            ArrowLengthClass.Micro => 0.55,
            ArrowLengthClass.Short => 0.65,
            ArrowLengthClass.Medium => 0.75,
            ArrowLengthClass.Long => 0.82,
            ArrowLengthClass.XLong => 0.87,
            _ => 0.75
        };
        
        // Fade ratios (not used in new implementation, but kept for compatibility)
        var (fadeMin, fadeMax) = (0.0, 1.0);
        
        // Straighten start ratio (not used in new implementation)
        var straightenStartRatio = 0.0;
        
        return new ArrowSizeParameters(
            neckWidth,
            tailNeckRatio,
            headBaseMultiplier,
            headLengthMultiplier,
            neckRatio,
            fadeMin,
            fadeMax,
            straightenStartRatio);
    }
    
    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * Math.Clamp(t, 0.0, 1.0);
    }

    private static Vector Normalize(Vector v)
    {
        var length = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        if (length < 1e-6) return new Vector(1, 0);
        return new Vector(v.X / length, v.Y / length);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private readonly record struct ArrowSizeParameters(
        double NeckWidth,
        double TailNeckRatio,
        double HeadBaseMultiplier,
        double HeadLengthMultiplier,
        double NeckRatio,
        double FadeRatioMin,
        double FadeRatioMax,
        double StraightenStartRatio);

internal readonly record struct TacticalArrowResult(
        Geometry Geometry, 
        IBrush Fill, 
        IBrush ShadowFill, 
        Transform ShadowTransform, 
        double Bend, 
        double NeckRatio,
        IReadOnlyList<ArrowSample> Samples,
        double NeckWidth,
        double TailWidth);

internal readonly record struct ArrowSample(Point Position, Vector Tangent, double T);
}

internal readonly record struct TacticalArrowRequest(
    Point Start,
    Point End,
    IReadOnlyList<Point>? Trail,
    double StrokeThickness,
    Color Color,
    double? BendHint);
