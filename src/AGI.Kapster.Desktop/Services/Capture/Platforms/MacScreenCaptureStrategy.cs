using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.VisualTree;
using SkiaSharp;
using Serilog;
using AGI.Kapster.Desktop.Services.Capture;

namespace AGI.Kapster.Desktop.Services.Capture.Platforms;

/// <summary>
/// macOS-specific screen capture implementation
/// </summary>
public class MacScreenCaptureStrategy : IScreenCaptureStrategy
{
    public bool SupportsWindowCapture => true;
    public bool SupportsElementCapture => false; // Not implemented yet
    public bool IsHardwareAccelerated => true; // macOS uses Core Graphics

    public async Task<SKBitmap?> CaptureFullScreenAsync(Screen screen)
    {
        try
        {
            if (!OperatingSystem.IsMacOS())
            {
                Log.Warning("[MacScreenCapture] Not running on macOS");
                return null;
            }

            var displays = GetActiveDisplays();
            if (displays.Count == 0)
            {
                Log.Warning("[MacScreenCapture] No active displays found");
                return null;
            }

            // Prefer matching by pixel size vs logical size * scaling to avoid global-origin ambiguity
            var expectedPixelWidth = (int)Math.Round(screen.Bounds.Width * screen.Scaling);
            var expectedPixelHeight = (int)Math.Round(screen.Bounds.Height * screen.Scaling);

            DisplayInfo? best = null;
            double bestScore = double.MaxValue;
            foreach (var d in displays)
            {
                var dw = d.PixelBounds.Width;
                var dh = d.PixelBounds.Height;
                // Score based on relative error in both dimensions
                double errW = Math.Abs(dw - expectedPixelWidth) / Math.Max(1.0, expectedPixelWidth);
                double errH = Math.Abs(dh - expectedPixelHeight) / Math.Max(1.0, expectedPixelHeight);
                double score = errW + errH;
                Log.Debug("[MacScreenCapture] Display {Id} size {Dw}x{Dh}, expected {W}x{H}, score {Score}",
                    d.DisplayId, dw, dh, expectedPixelWidth, expectedPixelHeight, score);
                if (score < bestScore)
                {
                    best = d;
                    bestScore = score;
                }
            }

            if (best == null)
            {
                var mainId = CGMainDisplayID();
                Log.Warning("[MacScreenCapture] Could not match display, using main display {Id}", mainId);
                return await CaptureDisplayAsync(mainId, null);
            }

            Log.Debug("[MacScreenCapture] Matched Screen to Display {Id} with pixel size {W}x{H}",
                best.Value.DisplayId, best.Value.PixelBounds.Width, best.Value.PixelBounds.Height);
            return await CaptureDisplayAsync(best.Value.DisplayId, null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MacScreenCapture] CaptureFullScreenAsync failed");
            return null;
        }
    }

    public async Task<SKBitmap?> CaptureWindowAsync(nint windowHandle)
    {
        // TODO: Implement using CGWindowListCreateImage with specific window ID
        Log.Warning("macOS window capture not yet implemented");
        return await Task.FromResult<SKBitmap?>(null);
    }

    public async Task<SKBitmap?> CaptureRegionAsync(PixelRect region)
    {
        try
        {
            if (!OperatingSystem.IsMacOS())
            {
                Log.Warning("[MacScreenCapture] Not running on macOS");
                return null;
            }

            // Identify display covering the region center
            var displays = GetActiveDisplays();
            if (displays.Count == 0)
            {
                Log.Warning("[MacScreenCapture] No active displays found");
                return null;
            }

            var display = FindBestDisplayForRegion(region, displays);
            if (display == null)
            {
                Log.Warning("[MacScreenCapture] Could not map region to a display");
                return null;
            }

            // Capture full display, then crop with Skia for simplicity and correctness
            var full = await CaptureDisplayAsync(display.Value.DisplayId, null);
            if (full == null)
            {
                return null;
            }

            try
            {
                var relativeX = region.X - display.Value.PixelBounds.X;
                var relativeY = region.Y - display.Value.PixelBounds.Y;
                var cropRect = new SKRectI(
                    Math.Max(0, relativeX),
                    Math.Max(0, relativeY),
                    Math.Max(0, relativeX + region.Width),
                    Math.Max(0, relativeY + region.Height));

                // Clamp to full image bounds
                cropRect.Right = Math.Min(cropRect.Right, full.Width);
                cropRect.Bottom = Math.Min(cropRect.Bottom, full.Height);

                if (cropRect.Width <= 0 || cropRect.Height <= 0)
                {
                    Log.Warning("[MacScreenCapture] Crop rect out of bounds: {Rect}", cropRect);
                    full.Dispose();
                    return null;
                }

                var subset = new SKBitmap(cropRect.Width, cropRect.Height, full.ColorType, full.AlphaType);
                using (var canvas = new SKCanvas(subset))
                {
                    canvas.DrawBitmap(full, cropRect, new SKRect(0, 0, subset.Width, subset.Height));
                }

                full.Dispose();
                return subset;
            }
            catch (Exception ex)
            {
                full.Dispose();
                Log.Error(ex, "[MacScreenCapture] Failed to crop region");
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MacScreenCapture] Failed to capture region");
            return null;
        }
    }

    public async Task<SKBitmap?> CaptureElementAsync(IElementInfo element)
    {
        // Element capture not supported on macOS yet
        Log.Warning("macOS element capture not supported");
        return await Task.FromResult<SKBitmap?>(null);
    }

    public async Task<SKBitmap?> CaptureWindowRegionAsync(Rect windowRect, Visual window)
    {
        try
        {
            // Convert window coordinates to screen coordinates
            if (window is Window w)
            {
                var p1 = w.PointToScreen(new Point(windowRect.X, windowRect.Y));
                var p2 = w.PointToScreen(new Point(windowRect.Right, windowRect.Bottom));

                var screenRect = new PixelRect(
                    Math.Min(p1.X, p2.X),
                    Math.Min(p1.Y, p2.Y),
                    Math.Max(1, Math.Abs(p2.X - p1.X)),
                    Math.Max(1, Math.Abs(p2.Y - p1.Y)));

                return await CaptureRegionAsync(screenRect);
            }
            else
            {
                Log.Warning("CaptureWindowRegionAsync requires a Window instance");
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture window region");
            return null;
        }
    }

    // =====================
    // CoreGraphics bindings
    // =====================

    private const string CoreGraphicsPath = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string ImageIOPath = "/System/Library/Frameworks/ImageIO.framework/ImageIO";
    private const string CoreFoundationPath = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(CoreGraphicsPath)]
    private static extern IntPtr CGDisplayCreateImage(uint displayId);

    [DllImport(CoreGraphicsPath)]
    private static extern uint CGMainDisplayID();

    [DllImport(CoreGraphicsPath)]
    private static extern int CGGetActiveDisplayList(uint maxDisplays, [Out] uint[] activeDisplays, out uint displayCount);

    [DllImport(CoreGraphicsPath)]
    private static extern CGRect CGDisplayBounds(uint display);

    [DllImport(ImageIOPath)]
    private static extern IntPtr CGImageDestinationCreateWithData(IntPtr data, IntPtr type, nint count, IntPtr options);

    [DllImport(ImageIOPath)]
    private static extern void CGImageDestinationAddImage(IntPtr dest, IntPtr image, IntPtr properties);

    [DllImport(ImageIOPath)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGImageDestinationFinalize(IntPtr dest);

    [DllImport(CoreFoundationPath)]
    private static extern IntPtr CFDataCreateMutable(IntPtr allocator, nint capacity);

    [DllImport(CoreFoundationPath)]
    private static extern IntPtr CFDataGetBytePtr(IntPtr theData);

    [DllImport(CoreFoundationPath)]
    private static extern nint CFDataGetLength(IntPtr theData);

    [DllImport(CoreFoundationPath)]
    private static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundationPath)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);

    private const uint kCFStringEncodingUTF8 = 0x08000100;

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;
    }

    private static IntPtr CFString(string s)
    {
        if (string.IsNullOrEmpty(s))
            return IntPtr.Zero;
        try
        {
            return CFStringCreateWithCString(IntPtr.Zero, s, kCFStringEncodingUTF8);
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }
    }

    private static void CFReleaseSafe(IntPtr p)
    {
        if (p != IntPtr.Zero)
        {
            try { CFRelease(p); }
            catch { }
        }
    }

    private struct DisplayInfo
    {
        public uint DisplayId { get; init; }
        public PixelRect PixelBounds { get; init; }
    }

    private static List<DisplayInfo> GetActiveDisplays()
    {
        const int MaxDisplays = 16;
        var ids = new uint[MaxDisplays];
        var result = new List<DisplayInfo>();
        try
        {
            var status = CGGetActiveDisplayList((uint)MaxDisplays, ids, out uint count);
            if (status != 0 || count == 0)
            {
                ids[0] = CGMainDisplayID();
                count = 1;
            }

            for (var i = 0u; i < count; i++)
            {
                var id = ids[i];
                var rect = CGDisplayBounds(id);
                // Assume CoreGraphics returns pixel-based bounds in global coordinates
                var pixelRect = new PixelRect(
                    (int)rect.Origin.X,
                    (int)rect.Origin.Y,
                    (int)rect.Size.Width,
                    (int)rect.Size.Height);

                result.Add(new DisplayInfo
                {
                    DisplayId = id,
                    PixelBounds = pixelRect
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MacScreenCapture] Failed to enumerate displays");
        }
        return result;
    }

    private static DisplayInfo? FindBestDisplayForRegion(PixelRect region, List<DisplayInfo> displays)
    {
        DisplayInfo? best = null;
        var bestArea = -1;
        foreach (var d in displays)
        {
            var overlap = IntersectArea(d.PixelBounds, region);
            if (overlap > bestArea)
            {
                best = d;
                bestArea = overlap;
            }
        }
        return best;
    }

    private static int IntersectArea(PixelRect a, PixelRect b)
    {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
        var w = x2 - x1;
        var h = y2 - y1;
        if (w <= 0 || h <= 0) return 0;
        return w * h;
    }

    private static async Task<SKBitmap?> CaptureDisplayAsync(uint displayId, PixelRect? crop)
    {
        return await Task.Run(() =>
        {
            IntPtr cgImage = IntPtr.Zero;
            IntPtr kUTTypePNG = IntPtr.Zero;
            IntPtr data = IntPtr.Zero;
            IntPtr dest = IntPtr.Zero;
            try
            {
                cgImage = CGDisplayCreateImage(displayId);
                if (cgImage == IntPtr.Zero)
                {
                    Log.Warning("[MacScreenCapture] CGDisplayCreateImage returned null for display {DisplayId}", displayId);
                    return null;
                }

                kUTTypePNG = CFString("public.png");
                if (kUTTypePNG == IntPtr.Zero)
                {
                    Log.Warning("[MacScreenCapture] CFString(public.png) returned null");
                    return null;
                }

                data = CFDataCreateMutable(IntPtr.Zero, 0);
                if (data == IntPtr.Zero)
                {
                    Log.Warning("[MacScreenCapture] CFDataCreateMutable returned null");
                    return null;
                }

                dest = CGImageDestinationCreateWithData(data, kUTTypePNG, 1, IntPtr.Zero);
                if (dest == IntPtr.Zero)
                {
                    Log.Warning("[MacScreenCapture] CGImageDestinationCreateWithData returned null");
                    return null;
                }

                CGImageDestinationAddImage(dest, cgImage, IntPtr.Zero);
                var finalized = CGImageDestinationFinalize(dest);
                if (!finalized)
                {
                    Log.Warning("[MacScreenCapture] CGImageDestinationFinalize failed");
                    return null;
                }

                var lengthN = CFDataGetLength(data);
                var length = (long)lengthN;
                if (length <= 0 || length > int.MaxValue)
                {
                    Log.Warning("[MacScreenCapture] Invalid PNG data length: {Length}", length);
                    return null;
                }

                var bytesPtr = CFDataGetBytePtr(data);
                if (bytesPtr == IntPtr.Zero)
                {
                    Log.Warning("[MacScreenCapture] CFDataGetBytePtr returned null");
                    return null;
                }

                var managedBytes = new byte[(int)length];
                Marshal.Copy(bytesPtr, managedBytes, 0, (int)length);

                var bmp = SKBitmap.Decode(managedBytes);
                if (bmp == null)
                {
                    Log.Warning("[MacScreenCapture] SKBitmap.Decode returned null");
                    return null;
                }

                if (crop.HasValue)
                {
                    var r = crop.Value;
                    var subset = new SKBitmap(r.Width, r.Height, bmp.ColorType, bmp.AlphaType);
                    using (var canvas = new SKCanvas(subset))
                    {
                        canvas.DrawBitmap(bmp, new SKRectI(r.X, r.Y, r.X + r.Width, r.Y + r.Height), new SKRect(0, 0, r.Width, r.Height));
                    }
                    bmp.Dispose();
                    return subset;
                }

                return bmp;
            }
            catch (DllNotFoundException ex)
            {
                Log.Error(ex, "[MacScreenCapture] DllNotFoundException");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacScreenCapture] Exception during capture");
                return null;
            }
            finally
            {
                CFReleaseSafe(dest);
                CFReleaseSafe(data);
                CFReleaseSafe(kUTTypePNG);
                CFReleaseSafe(cgImage);
            }
        });
    }
}


