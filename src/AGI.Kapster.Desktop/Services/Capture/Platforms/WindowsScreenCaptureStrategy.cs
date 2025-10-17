using System;
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
/// Windows-specific screen capture implementation
/// </summary>
public class WindowsScreenCaptureStrategy : IScreenCaptureStrategy
{
    public bool SupportsWindowCapture => true;
    public bool SupportsElementCapture => true;
    public bool IsHardwareAccelerated => false; // Can be enhanced with DXGI later

    public Task<SKBitmap?> CaptureFullScreenAsync(Screen screen)
    {
        return Task.Run(() =>
        {
            try
            {
                var bounds = screen.Bounds;
                return CaptureRegion(new PixelRect(
                    bounds.Position.X,
                    bounds.Position.Y,
                    bounds.Size.Width,
                    bounds.Size.Height));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to capture full screen");
                return null;
            }
        });
    }

    public Task<SKBitmap?> CaptureWindowAsync(nint windowHandle)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!GetWindowRect(windowHandle, out RECT rect))
                {
                    Log.Warning("Failed to get window rect for handle {Handle}", windowHandle);
                    return null;
                }

                var bounds = new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
                return CaptureRegion(bounds);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to capture window");
                return null;
            }
        });
    }

    public Task<SKBitmap?> CaptureRegionAsync(PixelRect region)
    {
        return Task.Run(() => CaptureRegion(region));
    }

    public async Task<SKBitmap?> CaptureElementAsync(IElementInfo element)
    {
        // For now, just capture the element bounds
        // Can be enhanced to use UI Automation for better element capture
        return await CaptureRegionAsync(element.Bounds);
    }

    public Task<SKBitmap?> CaptureWindowRegionAsync(Rect windowRect, Visual window)
    {
        return Task.Run(() =>
        {
            try
            {
                // Convert window coordinates to screen coordinates
                if (window is Window w)
                {
                    // Get the screen this window is on to determine DPI scaling
                    var screen = w.Screens.ScreenFromWindow(w);
                    if (screen == null)
                    {
                        Log.Warning("Could not determine screen for window");
                        return null;
                    }

                    // Get DPI scaling factor
                    var scaling = screen.Scaling;

                    // PointToScreen in Avalonia should return physical pixel coordinates
                    // But we need to ensure we're working with the correct coordinate system
                    var topLeft = w.PointToScreen(new Point(windowRect.X, windowRect.Y));
                    var bottomRight = w.PointToScreen(new Point(windowRect.Right, windowRect.Bottom));

                    var screenRect = new PixelRect(
                        Math.Min(topLeft.X, bottomRight.X),
                        Math.Min(topLeft.Y, bottomRight.Y),
                        Math.Max(1, Math.Abs(bottomRight.X - topLeft.X)),
                        Math.Max(1, Math.Abs(bottomRight.Y - topLeft.Y)));

                    Log.Debug("CaptureWindowRegion: DIP rect {WindowRect}, Screen scaling {Scaling}, Physical rect {ScreenRect}", 
                        windowRect, scaling, screenRect);

                    var bitmap = CaptureRegion(screenRect);
                    
                    if (bitmap != null)
                    {
                        Log.Debug("Captured bitmap: {W}x{H} pixels", bitmap.Width, bitmap.Height);
                    }
                    
                    return bitmap;
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
        });
    }

    private SKBitmap? CaptureRegion(PixelRect region)
    {
        try
        {
            var hdcSrc = GetDC(nint.Zero);
            var hdcDest = CreateCompatibleDC(hdcSrc);
            var hBitmap = CreateCompatibleBitmap(hdcSrc, region.Width, region.Height);
            var hOld = SelectObject(hdcDest, hBitmap);

            BitBlt(hdcDest, 0, 0, region.Width, region.Height, hdcSrc, region.X, region.Y, SRCCOPY);

            SelectObject(hdcDest, hOld);
            DeleteDC(hdcDest);
            ReleaseDC(nint.Zero, hdcSrc);

            var bitmap = ConvertHBitmapToSKBitmap(hBitmap);
            DeleteObject(hBitmap);

            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture region {Region}", region);
            return null;
        }
    }

    private SKBitmap? ConvertHBitmapToSKBitmap(nint hBitmap)
    {
        var bmp = GetObject(hBitmap, Marshal.SizeOf(typeof(BITMAP)), out BITMAP bm);
        if (bmp == 0) return null;

        var info = new SKImageInfo(bm.bmWidth, bm.bmHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);

        using (var pixmap = bitmap.PeekPixels())
        {
            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                    biWidth = bm.bmWidth,
                    biHeight = -bm.bmHeight, // Top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB
                }
            };

            var hdcScreen = GetDC(nint.Zero);
            GetDIBits(hdcScreen, hBitmap, 0, (uint)bm.bmHeight, pixmap.GetPixels(), ref bmi, DIB_RGB_COLORS);
            ReleaseDC(nint.Zero, hdcScreen);
        }

        return bitmap;
    }

    #region P/Invoke declarations

    private const int SRCCOPY = 0x00CC0020;
    private const int BI_RGB = 0;
    private const int DIB_RGB_COLORS = 0;

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDC);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleBitmap(nint hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(nint hdcDest, int xDest, int yDest, int width, int height,
        nint hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(nint hgdiobj, int cbBuffer, out BITMAP lpvObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(nint hdc, nint hbmp, uint uStartScan, uint cScanLines,
        nint lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public nint bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public uint[] bmiColors;
    }

    #endregion
}


