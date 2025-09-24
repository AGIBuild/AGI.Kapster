using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.VisualTree;
using SkiaSharp;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Overlay.Platforms;

/// <summary>
/// Windows-specific screen capture implementation
/// </summary>
public class WindowsScreenCaptureStrategy : IScreenCaptureStrategy
{
    public bool SupportsWindowCapture => true;
    public bool SupportsElementCapture => true;
    public bool IsHardwareAccelerated => false; // Can be enhanced with DXGI later

    public async Task<SKBitmap?> CaptureFullScreenAsync(Screen screen)
    {
        return await Task.Run(() =>
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

    public async Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Get window bounds
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

    public async Task<SKBitmap?> CaptureRegionAsync(PixelRect region)
    {
        return await Task.Run(() => CaptureRegion(region));
    }

    public async Task<SKBitmap?> CaptureElementAsync(IElementInfo element)
    {
        // For now, just capture the element bounds
        // Can be enhanced to use UI Automation for better element capture
        return await CaptureRegionAsync(element.Bounds);
    }

    public async Task<SKBitmap?> CaptureWindowRegionAsync(Avalonia.Rect windowRect, Avalonia.Visual window)
    {
        return await Task.Run(() =>
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

                    return CaptureRegion(screenRect);
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
            var hdcSrc = GetDC(IntPtr.Zero);
            var hdcDest = CreateCompatibleDC(hdcSrc);
            var hBitmap = CreateCompatibleBitmap(hdcSrc, region.Width, region.Height);
            var hOld = SelectObject(hdcDest, hBitmap);

            BitBlt(hdcDest, 0, 0, region.Width, region.Height, hdcSrc, region.X, region.Y, SRCCOPY);

            SelectObject(hdcDest, hOld);
            DeleteDC(hdcDest);
            ReleaseDC(IntPtr.Zero, hdcSrc);

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

    private SKBitmap? ConvertHBitmapToSKBitmap(IntPtr hBitmap)
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

            var hdcScreen = GetDC(IntPtr.Zero);
            GetDIBits(hdcScreen, hBitmap, 0, (uint)bm.bmHeight, pixmap.GetPixels(), ref bmi, DIB_RGB_COLORS);
            ReleaseDC(IntPtr.Zero, hdcScreen);
        }

        return bitmap;
    }

    #region P/Invoke declarations

    private const int SRCCOPY = 0x00CC0020;
    private const int BI_RGB = 0;
    private const int DIB_RGB_COLORS = 0;

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        IntPtr lpvBits, ref BITMAPINFO lpbi, uint uUsage);

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
        public IntPtr bmBits;
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


