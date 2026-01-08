using System;
using System.Runtime.InteropServices;
using System.Threading;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

/// <summary>
/// macOS keyboard layout monitor using TIS notification
/// </summary>
public class MacKeyboardLayoutMonitor : IKeyboardLayoutMonitor
{
    private bool _monitoring = false;
    private bool _disposed = false;
    private Timer? _debounceTimer;
    private DateTime _lastChangeTime = DateTime.MinValue;
    private const int DebounceMs = 500;
    private int _consecutiveErrors = 0;
    private const int MaxConsecutiveErrors = 3; // Disable monitoring after 3 consecutive errors

    public event EventHandler? LayoutChanged;

    // TIS API is in Carbon framework, but may need dynamic loading on some macOS versions
    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon", CallingConvention = CallingConvention.Cdecl, EntryPoint = "TISCopyCurrentKeyboardInputSource", SetLastError = true)]
    private static extern IntPtr TISCopyCurrentKeyboardInputSource();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", CallingConvention = CallingConvention.Cdecl)]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool CFStringGetCString(IntPtr theString, System.Text.StringBuilder buffer, long bufferSize, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", CallingConvention = CallingConvention.Cdecl)]
    private static extern long CFStringGetLength(IntPtr theString);

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon", CallingConvention = CallingConvention.Cdecl, EntryPoint = "TISGetInputSourceProperty", SetLastError = true)]
    private static extern IntPtr TISGetInputSourceProperty(IntPtr inputSource, IntPtr propertyKey);

    private const uint kCFStringEncodingUTF8 = 0x08000100;
    private static readonly IntPtr kTISPropertyInputSourceID = CFStringCreateWithCString(IntPtr.Zero, "TISPropertyInputSourceID", kCFStringEncodingUTF8);

    private static bool _tisApiAvailable = true;

    public bool IsSupported => OperatingSystem.IsMacOS();
    public bool IsMonitoring => _monitoring;

    public void StartMonitoring()
    {
        if (!IsSupported || _monitoring || !_tisApiAvailable)
            return;

        try
        {
            // Test if TIS API is available
            var testLayout = TISCopyCurrentKeyboardInputSource();
            if (testLayout == IntPtr.Zero)
            {
                Log.Warning("TIS API not available, keyboard layout monitoring disabled");
                _tisApiAvailable = false;
                return;
            }
            CFRelease(testLayout);

            // macOS doesn't have a direct notification API for layout changes
            // We'll poll periodically (debounced) as a fallback
            // In practice, layout changes are infrequent, so polling every 30 seconds is acceptable
            // Longer interval significantly reduces IMKCFRunLoopWakeUpReliable warnings from TIS API calls
            // Start with a longer initial delay to avoid interfering with startup
            _debounceTimer = new Timer(CheckLayoutChange, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            _monitoring = true;
            Log.Debug("macOS keyboard layout monitoring started (polling mode)");
        }
        catch (EntryPointNotFoundException)
        {
            Log.Warning("TIS API not found, keyboard layout monitoring disabled");
            _tisApiAvailable = false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start macOS keyboard layout monitoring");
        }
    }

    public void StopMonitoring()
    {
        if (!_monitoring)
            return;

        try
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
            _monitoring = false;
            _consecutiveErrors = 0; // Reset error counter
            Log.Debug("macOS keyboard layout monitoring stopped");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error stopping macOS keyboard layout monitoring");
        }
    }

    private string? _lastLayoutId = null;

    /// <summary>
    /// Converts a CFString to a C# string.
    /// Returns null if conversion fails.
    /// </summary>
    private static string? CFStringToString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero)
            return null;

        try
        {
            var length = CFStringGetLength(cfString);
            if (length <= 0)
                return null;

            // Allocate buffer (CFString uses UTF-16, so length * 2 bytes + null terminator)
            var buffer = new System.Text.StringBuilder((int)length + 1);
            if (CFStringGetCString(cfString, buffer, buffer.Capacity, kCFStringEncodingUTF8))
            {
                return buffer.ToString();
            }
        }
        catch
        {
            // If conversion fails, fall back to pointer-based identifier
        }

        // Fallback: use pointer as identifier (not ideal but works)
        return cfString.ToString();
    }

    private void CheckLayoutChange(object? state)
    {
        // Skip if we've had too many consecutive errors
        if (_consecutiveErrors >= MaxConsecutiveErrors)
        {
            Log.Debug("Keyboard layout monitoring temporarily disabled due to repeated errors");
            return;
        }

        try
        {
            var currentLayout = TISCopyCurrentKeyboardInputSource();
            if (currentLayout == IntPtr.Zero)
            {
                // If we can't get layout, skip this check (may happen on some macOS versions)
                _consecutiveErrors++;
                return;
            }

            try
            {
                // Get layout ID to compare (not the pointer, which changes each time)
                var layoutIdPtr = TISGetInputSourceProperty(currentLayout, kTISPropertyInputSourceID);
                string? currentLayoutId = null;
                
                if (layoutIdPtr != IntPtr.Zero)
                {
                    // Convert CFString to C# string properly
                    currentLayoutId = CFStringToString(layoutIdPtr);
                }

                // Compare layout IDs instead of pointers
                if (_lastLayoutId != null && currentLayoutId != null && _lastLayoutId != currentLayoutId)
                {
                    // Debounce: only fire if enough time has passed since last change
                    var now = DateTime.UtcNow;
                    if ((now - _lastChangeTime).TotalMilliseconds > DebounceMs)
                    {
                        Log.Debug("Keyboard layout changed detected: {Old} -> {New}", _lastLayoutId, currentLayoutId);
                        _lastChangeTime = now;
                        LayoutChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                else if (_lastLayoutId == null)
                {
                    // First check - just store the ID
                    _lastLayoutId = currentLayoutId;
                }

                // Release current layout reference
                CFRelease(currentLayout);
                
                // Reset error counter on successful check
                _consecutiveErrors = 0;
            }
            catch
            {
                // If we can't get layout ID, release and skip
                CFRelease(currentLayout);
                _consecutiveErrors++;
            }
        }
        catch (EntryPointNotFoundException ex)
        {
            // TIS API may not be available on all macOS versions
            // Log once and disable monitoring
            Log.Warning("TIS API not available, keyboard layout monitoring disabled: {Message}", ex.Message);
            StopMonitoring();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking keyboard layout change");
            _consecutiveErrors++;
            
            // If we've had too many errors, disable monitoring to avoid spam
            if (_consecutiveErrors >= MaxConsecutiveErrors)
            {
                Log.Warning("Keyboard layout monitoring disabled due to repeated errors. Character-stable hotkeys may not update when keyboard layout changes.");
                StopMonitoring();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopMonitoring();
        _lastLayoutId = null;
        _disposed = true;
    }
}

