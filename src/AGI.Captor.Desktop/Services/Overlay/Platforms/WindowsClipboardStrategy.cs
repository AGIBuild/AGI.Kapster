using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Serilog;

namespace AGI.Captor.Desktop.Services.Overlay.Platforms;

/// <summary>
/// Windows-specific clipboard implementation using Win32 API
/// </summary>
public class WindowsClipboardStrategy : IClipboardStrategy
{
    public bool SupportsMultipleFormats => true;
    public bool SupportsImages => true;
    
    public async Task<bool> SetImageAsync(SKBitmap bitmap)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Convert SKBitmap to Windows HBITMAP
                var hBitmap = CreateHBitmapFromSKBitmap(bitmap);
                if (hBitmap == IntPtr.Zero)
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
    
    public async Task<bool> SetTextAsync(string text)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Allocate global memory for text
                var hGlobal = Marshal.StringToHGlobalUni(text);
                if (hGlobal == IntPtr.Zero) return false;
                
                bool result = false;
                
                // Try to open clipboard with retries
                for (int i = 0; i < 10 && !result; i++)
                {
                    if (OpenClipboard(IntPtr.Zero))
                    {
                        try
                        {
                            EmptyClipboard();
                            var set = SetClipboardData(CF_UNICODETEXT, hGlobal);
                            result = set != IntPtr.Zero;
                            if (result)
                            {
                                hGlobal = IntPtr.Zero; // Clipboard takes ownership
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
                if (hGlobal != IntPtr.Zero)
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
    
    public async Task<SKBitmap?> GetImageAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!OpenClipboard(IntPtr.Zero))
                    return null;
                
                try
                {
                    var hBitmap = GetClipboardData(CF_BITMAP);
                    if (hBitmap == IntPtr.Zero)
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
    
    public async Task<string?> GetTextAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!OpenClipboard(IntPtr.Zero))
                    return null;
                
                try
                {
                    var hGlobal = GetClipboardData(CF_UNICODETEXT);
                    if (hGlobal == IntPtr.Zero)
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
    
    public async Task<bool> ClearAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!OpenClipboard(IntPtr.Zero))
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
    
    private IntPtr CreateHBitmapFromSKBitmap(SKBitmap skBitmap)
    {
        var info = skBitmap.Info;
        var hdc = GetDC(IntPtr.Zero);
        var hBitmap = CreateCompatibleBitmap(hdc, info.Width, info.Height);
        ReleaseDC(IntPtr.Zero, hdc);
        
        if (hBitmap == IntPtr.Zero)
            return IntPtr.Zero;
        
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
            hdc = GetDC(IntPtr.Zero);
            SetDIBits(hdc, hBitmap, 0, (uint)info.Height, pixmap.GetPixels(), ref bmi, DIB_RGB_COLORS);
            ReleaseDC(IntPtr.Zero, hdc);
        }
        
        return hBitmap;
    }
    
    private SKBitmap? ConvertHBitmapToSKBitmap(IntPtr hBitmap)
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
            
            var hdc = GetDC(IntPtr.Zero);
            GetDIBits(hdc, hBitmap, 0, (uint)bm.bmHeight, pixmap.GetPixels(), ref bmi, DIB_RGB_COLORS);
            ReleaseDC(IntPtr.Zero, hdc);
        }
        
        return bitmap;
    }
    
    private bool SetHBitmapToClipboard(IntPtr hBitmap)
    {
        bool result = false;
        
        // Robust clipboard open with retries
        for (int i = 0; i < 10 && !result; i++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    EmptyClipboard();
                    var set = SetClipboardData(CF_BITMAP, hBitmap);
                    result = set != IntPtr.Zero;
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
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);
    
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);
    
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    
    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);
    
    [DllImport("gdi32.dll")]
    private static extern int SetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        IntPtr lpvBits, ref BITMAPINFO lpbi, uint uUsage);
    
    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        IntPtr lpvBits, ref BITMAPINFO lpbi, uint uUsage);
    
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
