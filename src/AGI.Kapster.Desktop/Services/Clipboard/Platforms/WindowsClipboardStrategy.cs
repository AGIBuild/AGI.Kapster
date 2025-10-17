using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Serilog;
using AGI.Kapster.Desktop.Services.Clipboard;

namespace AGI.Kapster.Desktop.Services.Clipboard.Platforms;

/// <summary>
/// Windows-specific clipboard implementation using Win32 API
/// </summary>
public class WindowsClipboardStrategy : IClipboardStrategy
{
    public bool SupportsMultipleFormats => true;
    public bool SupportsImages => true;

    public Task<bool> SetImageAsync(SKBitmap bitmap)
    {
        return Task.Run(() =>
        {
            try
            {
                // Convert SKBitmap to Windows HBITMAP
                var hBitmap = CreateHBitmapFromSKBitmap(bitmap);
                if (hBitmap == nint.Zero)
                {
                    Log.Warning("Failed to create HBITMAP from SKBitmap");
                    return false;
                }

                // Set to clipboard
                bool result = SetHBitmapToClipboard(hBitmap);

                // If clipboard didn't take ownership, clean up
                if (!result)
                {
                    DeleteObject(hBitmap);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set image to clipboard");
                return false;
            }
        });
    }

    public Task<bool> SetTextAsync(string text)
    {
        return Task.Run(() =>
        {
            try
            {
                // Allocate global memory for text
                var hGlobal = Marshal.StringToHGlobalUni(text);
                if (hGlobal == nint.Zero) return false;

                bool result = false;

                // Try to open clipboard with retries
                for (int i = 0; i < 10 && !result; i++)
                {
                    if (OpenClipboard(nint.Zero))
                    {
                        try
                        {
                            EmptyClipboard();
                            var set = SetClipboardData(CF_UNICODETEXT, hGlobal);
                            result = set != nint.Zero;
                            if (result)
                            {
                                hGlobal = nint.Zero; // Clipboard takes ownership
                            }
                        }
                        finally
                        {
                            CloseClipboard();
                        }
                    }

                    if (!result)
                    {
                        Thread.Sleep(25);
                    }
                }

                // Clean up if clipboard didn't take ownership
                if (hGlobal != nint.Zero)
                {
                    Marshal.FreeHGlobal(hGlobal);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set text to clipboard");
                return false;
            }
        });
    }

    public Task<SKBitmap?> GetImageAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (!OpenClipboard(nint.Zero))
                    return null;

                try
                {
                    var hBitmap = GetClipboardData(CF_BITMAP);
                    if (hBitmap == nint.Zero)
                        return null;

                    return ConvertHBitmapToSKBitmap(hBitmap);
                }
                finally
                {
                    CloseClipboard();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get image from clipboard");
                return null;
            }
        });
    }

    public Task<string?> GetTextAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (!OpenClipboard(nint.Zero))
                    return null;

                try
                {
                    var hGlobal = GetClipboardData(CF_UNICODETEXT);
                    if (hGlobal == nint.Zero)
                        return null;

                    return Marshal.PtrToStringUni(hGlobal);
                }
                finally
                {
                    CloseClipboard();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get text from clipboard");
                return null;
            }
        });
    }

    public Task<bool> ClearAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (!OpenClipboard(nint.Zero))
                    return false;

                try
                {
                    return EmptyClipboard();
                }
                finally
                {
                    CloseClipboard();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clear clipboard");
                return false;
            }
        });
    }

    private nint CreateHBitmapFromSKBitmap(SKBitmap skBitmap)
    {
        var info = skBitmap.Info;
        var hdc = GetDC(nint.Zero);
        var hBitmap = CreateCompatibleBitmap(hdc, info.Width, info.Height);
        ReleaseDC(nint.Zero, hdc);

        if (hBitmap == nint.Zero)
            return nint.Zero;

        // Copy pixel data
        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                biWidth = info.Width,
                biHeight = -info.Height, // Top-down
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB
            }
        };

        using (var pixmap = skBitmap.PeekPixels())
        {
            hdc = GetDC(nint.Zero);
            SetDIBits(hdc, hBitmap, 0, (uint)info.Height, pixmap.GetPixels(), ref bmi, DIB_RGB_COLORS);
            ReleaseDC(nint.Zero, hdc);
        }

        return hBitmap;
    }

    private SKBitmap? ConvertHBitmapToSKBitmap(nint hBitmap)
    {
        if (GetObject(hBitmap, Marshal.SizeOf(typeof(BITMAP)), out BITMAP bm) == 0)
            return null;

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
                    biHeight = -bm.bmHeight,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB
                }
            };

            var hdc = GetDC(nint.Zero);
            GetDIBits(hdc, hBitmap, 0, (uint)bm.bmHeight, pixmap.GetPixels(), ref bmi, DIB_RGB_COLORS);
            ReleaseDC(nint.Zero, hdc);
        }

        return bitmap;
    }

    private bool SetHBitmapToClipboard(nint hBitmap)
    {
        bool result = false;

        // Robust clipboard open with retries
        for (int i = 0; i < 10 && !result; i++)
        {
            if (OpenClipboard(nint.Zero))
            {
                try
                {
                    EmptyClipboard();
                    var set = SetClipboardData(CF_BITMAP, hBitmap);
                    result = set != nint.Zero;
                    if (result)
                    {
                        // Clipboard takes ownership
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }

            if (!result)
            {
                Thread.Sleep(25);
            }
        }

        return result;
    }

    #region P/Invoke declarations

    private const uint CF_BITMAP = 2;
    private const uint CF_UNICODETEXT = 13;
    private const int BI_RGB = 0;
    private const int DIB_RGB_COLORS = 0;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetClipboardData(uint uFormat, nint hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetClipboardData(uint uFormat);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleBitmap(nint hdc, int cx, int cy);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDC);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(nint hgdiobj, int cbBuffer, out BITMAP lpvObject);

    [DllImport("gdi32.dll")]
    private static extern int SetDIBits(nint hdc, nint hbmp, uint uStartScan, uint cScanLines,
        nint lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(nint hdc, nint hbmp, uint uStartScan, uint cScanLines,
        nint lpvBits, ref BITMAPINFO lpbi, uint uUsage);

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


