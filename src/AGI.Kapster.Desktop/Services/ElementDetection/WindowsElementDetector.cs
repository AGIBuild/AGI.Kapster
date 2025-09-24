using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Serilog;

namespace AGI.Kapster.Desktop.Services;

/// <summary>
/// Windows implementation of element detector using UIAutomation and Win32 APIs
/// </summary>
public class WindowsElementDetector : IElementDetector
{
    private bool _isDetectionActive;
    private bool _isWindowMode = true; // Start with window detection mode

    public event Action<bool>? DetectionModeChanged;

    public bool IsDetectionActive
    {
        get => _isDetectionActive;
        set
        {
            if (_isDetectionActive != value)
            {
                _isDetectionActive = value;
                Log.Information("Element detection active: {Active}", value);
            }
        }
    }

    public bool IsWindowMode => _isWindowMode;

    public void ToggleDetectionMode()
    {
        _isWindowMode = !_isWindowMode;
        DetectionModeChanged?.Invoke(_isWindowMode);
        Log.Information("Detection mode changed to: {Mode}", _isWindowMode ? "Window" : "Element");
    }

    public DetectedElement? DetectElementAt(int x, int y, IntPtr ignoreWindow = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            Log.Warning("WindowsElementDetector called on non-Windows platform");
            return null;
        }

        try
        {
            if (_isWindowMode)
            {
                return DetectWindowAt(x, y, ignoreWindow);
            }
            else
            {
                return DetectUIElementAt(x, y, ignoreWindow);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error detecting element at ({X}, {Y})", x, y);
            return null;
        }
    }

    private DetectedElement? DetectWindowAt(int x, int y, IntPtr ignoreWindow = default)
    {
        var point = new POINT { X = x, Y = y };

        // First attempt: Get window directly
        IntPtr hwnd = WindowFromPoint(point);

        if (hwnd == IntPtr.Zero)
            return null;

        // If we hit the ignore window (overlay), try to find windows beneath it
        if (ignoreWindow != IntPtr.Zero && (hwnd == ignoreWindow || IsChildWindow(hwnd, ignoreWindow)))
        {
            hwnd = FindWindowBeneathIgnored(x, y, ignoreWindow);
            if (hwnd == IntPtr.Zero)
                return null;
        }

        // Check if this is a valid window that we want to select
        IntPtr targetWindow = FindSelectableWindow(hwnd);

        return GetWindowInfo(targetWindow, true);
    }

    private bool IsChildWindow(IntPtr hwnd, IntPtr parentHwnd)
    {
        IntPtr parent = hwnd;
        while (parent != IntPtr.Zero)
        {
            parent = GetParent(parent);
            if (parent == parentHwnd)
                return true;
        }
        return false;
    }

    private IntPtr FindWindowBeneathIgnored(int x, int y, IntPtr ignoreWindow)
    {
        // Temporarily hide the ignore window to detect what's beneath
        bool wasVisible = IsWindowVisible(ignoreWindow);
        if (wasVisible)
        {
            ShowWindow(ignoreWindow, SW_HIDE);
        }

        try
        {
            var point = new POINT { X = x, Y = y };
            return WindowFromPoint(point);
        }
        finally
        {
            // Restore the window visibility
            if (wasVisible)
            {
                ShowWindow(ignoreWindow, SW_SHOW);
            }
        }
    }

    private IntPtr FindSelectableWindow(IntPtr hwnd)
    {
        // First, try the immediate window
        if (IsSelectableWindow(hwnd))
            return hwnd;

        // Then try parent windows until we find a selectable one
        IntPtr parent = hwnd;
        while (parent != IntPtr.Zero)
        {
            parent = GetParent(parent);
            if (parent != IntPtr.Zero && IsSelectableWindow(parent))
                return parent;
        }

        // Fallback to root window
        IntPtr rootWindow = GetAncestor(hwnd, GA_ROOT);
        return rootWindow != IntPtr.Zero ? rootWindow : hwnd;
    }

    private bool IsSelectableWindow(IntPtr hwnd)
    {
        // Check if window is visible
        if (!IsWindowVisible(hwnd))
            return false;

        // Get window rect to check if it's a reasonable size
        if (!GetWindowRect(hwnd, out RECT rect))
            return false;

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        // Skip very small windows (likely UI controls)
        if (width < 50 || height < 50)
            return false;

        // Get window class to filter out system windows
        var classBuilder = new StringBuilder(256);
        GetClassName(hwnd, classBuilder, classBuilder.Capacity);
        string className = classBuilder.ToString();

        // Skip desktop and shell windows
        if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd")
            return false;

        return true;
    }

    private DetectedElement? DetectUIElementAt(int x, int y, IntPtr ignoreWindow = default)
    {
        try
        {
            // First get the window
            var windowElement = DetectWindowAt(x, y, ignoreWindow);
            if (windowElement == null)
            {
                Log.Debug("No window found at {X}, {Y}", x, y);
                return null;
            }

            Log.Debug("Found window: {Name} ({ClassName}) at {X}, {Y}", windowElement.Name, windowElement.ClassName, x, y);

            // Try to find a more specific UI element within the window
            var uiElement = FindUIElementAt(x, y, windowElement.WindowHandle);
            if (uiElement != null)
            {
                Log.Information("Found UI element: {Name} ({ClassName}) at {X}, {Y}", uiElement.Name, uiElement.ClassName, x, y);
                return uiElement;
            }
            else
            {
                Log.Debug("No UI element found within window, returning window: {Name}", windowElement.Name);
                return windowElement;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in UIAutomation element detection, falling back to window detection");
            return DetectWindowAt(x, y, ignoreWindow);
        }
    }

    private DetectedElement? FindUIElementAt(int x, int y, IntPtr windowHandle)
    {
        try
        {
            // Enhanced multi-strategy element detection

            // Strategy 1: Try UI Automation for modern apps
            var automationElement = FindUIAutomationElementAt(x, y);
            if (automationElement != null)
            {
                Log.Debug("Found UI Automation element: {Name} ({ClassName})", automationElement.Name, automationElement.ClassName);
                return automationElement;
            }

            // Strategy 2: Enhanced recursive child detection with multiple attempts
            var deepestChild = FindDeepestChildAtMultipass(x, y, windowHandle);
            if (deepestChild != IntPtr.Zero && deepestChild != windowHandle)
            {
                var childInfo = GetWindowInfo(deepestChild, false);
                if (childInfo != null)
                {
                    Log.Debug("Found child window: {Name} ({ClassName}) - IsUIElement: {IsUI}",
                        childInfo.Name, childInfo.ClassName, IsUIElement(childInfo));

                    if (IsUIElement(childInfo))
                    {
                        Log.Information("Found deep child element: {Name} ({ClassName})", childInfo.Name, childInfo.ClassName);
                        return childInfo;
                    }
                }
                else
                {
                    Log.Debug("Failed to get window info for child handle: {Handle:X}", deepestChild.ToInt64());
                }
            }
            else
            {
                Log.Debug("No child window found or child same as parent");
            }

            // Strategy 3: Use RealChildWindowFromPoint for precise detection
            var point = new POINT { X = x, Y = y };
            if (ScreenToClient(windowHandle, ref point))
            {
                var realChild = RealChildWindowFromPoint(windowHandle, point);
                Log.Debug("RealChildWindowFromPoint result: {Handle:X} (parent: {Parent:X})",
                    realChild.ToInt64(), windowHandle.ToInt64());

                if (realChild != IntPtr.Zero && realChild != windowHandle)
                {
                    var realChildInfo = GetWindowInfo(realChild, false);
                    if (realChildInfo != null)
                    {
                        Log.Debug("RealChild info: {Name} ({ClassName}) - IsUIElement: {IsUI}",
                            realChildInfo.Name, realChildInfo.ClassName, IsUIElement(realChildInfo));

                        if (IsUIElement(realChildInfo))
                        {
                            Log.Information("Found real child element: {Name} ({ClassName})", realChildInfo.Name, realChildInfo.ClassName);
                            return realChildInfo;
                        }
                    }
                }
            }

            // Strategy 4: Try enumerate child windows approach
            Log.Debug("Trying child enumeration for window {Handle:X}", windowHandle.ToInt64());
            var enumeratedChild = FindChildByEnumeration(x, y, windowHandle);
            if (enumeratedChild != null)
            {
                Log.Information("Found enumerated child element: {Name} ({ClassName})", enumeratedChild.Name, enumeratedChild.ClassName);
                return enumeratedChild;
            }

            Log.Debug("No UI element found within window, element detection failed");
            return null;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error finding UI element at {X}, {Y}", x, y);
            return null;
        }
    }

    private DetectedElement? FindUIAutomationElementAt(int x, int y)
    {
        // Temporarily disable UI Automation due to COM issues
        // Focus on improving Win32-based detection instead
        return null;
    }

    private IntPtr FindDeepestChildAtMultipass(int x, int y, IntPtr parentWindow)
    {
        // Multi-pass approach for better element detection

        // Pass 1: Standard ChildWindowFromPoint
        var child1 = FindDeepestChildAt(x, y, parentWindow);
        if (child1 != IntPtr.Zero && child1 != parentWindow && IsInteractiveElement(child1))
            return child1;

        // Pass 2: Try with slight coordinate offsets (helps with border cases)
        var offsets = new[] { (-1, -1), (1, 1), (0, -1), (0, 1), (-1, 0), (1, 0) };
        foreach (var (dx, dy) in offsets)
        {
            var child2 = FindDeepestChildAt(x + dx, y + dy, parentWindow);
            if (child2 != IntPtr.Zero && child2 != parentWindow && child2 != child1 && IsInteractiveElement(child2))
                return child2;
        }

        return child1;
    }

    private IntPtr FindDeepestChildAt(int x, int y, IntPtr parentWindow)
    {
        var point = new POINT { X = x, Y = y };

        // Convert to client coordinates of the parent window
        if (!ScreenToClient(parentWindow, ref point))
            return IntPtr.Zero;

        // Get immediate child
        var child = ChildWindowFromPoint(parentWindow, point);
        if (child == IntPtr.Zero || child == parentWindow)
            return parentWindow;

        // Recursively find deeper children
        var screenPoint = new POINT { X = x, Y = y };
        if (ScreenToClient(child, ref screenPoint))
        {
            var deeperChild = FindDeepestChildAt(x, y, child);
            return deeperChild != IntPtr.Zero ? deeperChild : child;
        }

        return child;
    }

    private bool IsInteractiveElement(IntPtr hwnd)
    {
        if (!IsWindowVisible(hwnd))
            return false;

        // Get window rect to check size
        if (!GetWindowRect(hwnd, out RECT rect))
            return false;

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        // Must be reasonable size for interaction
        if (width < 5 || height < 5 || width > 2000 || height > 2000)
            return false;

        // Get class name for filtering
        var classBuilder = new StringBuilder(256);
        GetClassName(hwnd, classBuilder, classBuilder.Capacity);
        string className = classBuilder.ToString().ToLower();

        // Extended list of interactive element class names
        var interactiveClasses = new[]
        {
            "button", "edit", "static", "listbox", "combobox", "msctls_trackbar32",
            "msctls_updown32", "scrollbar", "richedit", "sys", "afx:", "atl:",
            "chrome_", "firefox", "webkit", "qt_", "tk", "tree", "list", "tab",
            "internet explorer_", "msctls_", "toolbarwindow32", "tooltips_class32"
        };

        bool hasInteractiveClass = interactiveClasses.Any(cls => className.Contains(cls));

        // If it has an interactive class name, it's likely interactive
        if (hasInteractiveClass)
            return true;

        // Additional heuristics: check if it has text content (indicates UI element)
        var titleBuilder = new StringBuilder(256);
        GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
        string title = titleBuilder.ToString();

        // Small windows with text are likely UI elements
        return !string.IsNullOrEmpty(title) && width < 500 && height < 200;
    }

    private DetectedElement? FindChildByEnumeration(int x, int y, IntPtr parentWindow)
    {
        try
        {
            var foundChild = IntPtr.Zero;
            var targetPoint = new POINT { X = x, Y = y };

            // Convert to parent window coordinates
            if (!ScreenToClient(parentWindow, ref targetPoint))
                return null;

            // Enumerate child windows
            EnumChildWindows(parentWindow, (hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd))
                    return true; // Continue enumeration

                // Get child window rect in parent coordinates
                if (GetWindowRect(hwnd, out RECT childRect))
                {
                    // Convert child rect to parent client coordinates
                    var topLeft = new POINT { X = childRect.Left, Y = childRect.Top };
                    var bottomRight = new POINT { X = childRect.Right, Y = childRect.Bottom };

                    ScreenToClient(parentWindow, ref topLeft);
                    ScreenToClient(parentWindow, ref bottomRight);

                    // Check if point is within this child
                    if (targetPoint.X >= topLeft.X && targetPoint.X <= bottomRight.X &&
                        targetPoint.Y >= topLeft.Y && targetPoint.Y <= bottomRight.Y)
                    {
                        if (IsInteractiveElement(hwnd))
                        {
                            foundChild = hwnd;
                            return false; // Stop enumeration
                        }
                    }
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);

            if (foundChild != IntPtr.Zero)
            {
                return GetWindowInfo(foundChild, false);
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error in child enumeration");
            return null;
        }
    }

    private bool IsUIElement(DetectedElement element)
    {
        var className = element.ClassName.ToLower();
        var width = element.Bounds.Width;
        var height = element.Bounds.Height;

        // Skip very small elements (likely decorative or internal)
        if (width < 10 || height < 10)
            return false;

        // Check for common UI element class names (more comprehensive list)
        var uiElementClasses = new[]
        {
            "button", "edit", "static", "listbox", "combobox", "scrollbar", "toolbar", "statusbar",
            "msctls_", "richedit", "internet explorer_", "chrome_", "firefox", "webkit",
            "sys", "afx:", "atl:", "wtl:", "qt", "gtk", "tk",
            "tree", "list", "tab", "progress", "slider", "spin", "header"
        };

        bool hasUIClassName = uiElementClasses.Any(ui => className.Contains(ui));

        // If it has a clear UI class name, it's likely a UI element
        if (hasUIClassName)
            return true;

        // For elements without obvious class names, use size heuristics
        // UI elements are typically smaller than full windows but large enough to be interactive
        bool reasonableSize = width < 800 && height < 600 && width >= 20 && height >= 15;

        // Check if it has meaningful text content (good indicator of UI element)
        bool hasText = !string.IsNullOrEmpty(element.Name) && element.Name.Length > 0;

        return reasonableSize && hasText;
    }

    private DetectedElement? GetWindowInfo(IntPtr hwnd, bool isWindow)
    {
        try
        {
            // Get window rect
            if (!GetWindowRect(hwnd, out RECT rect))
                return null;

            var bounds = new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

            // Get window title
            var titleBuilder = new StringBuilder(256);
            GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
            string title = titleBuilder.ToString();

            // Get class name
            var classBuilder = new StringBuilder(256);
            GetClassName(hwnd, classBuilder, classBuilder.Capacity);
            string className = classBuilder.ToString();

            // Get process info
            GetWindowThreadProcessId(hwnd, out uint processId);
            string processName = "Unknown";
            try
            {
                using var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to get process name for PID {ProcessId}", processId);
            }

            // Use title as name, fallback to class name
            string name = !string.IsNullOrEmpty(title) ? title : className;
            if (string.IsNullOrEmpty(name))
                name = $"{processName} Window";

            return new DetectedElement(bounds, name, className, processName, hwnd, isWindow);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting window info for handle {Handle:X}", hwnd.ToInt64());
            return null;
        }
    }

    #region UI Automation Support

    private object? _automation;

    private object? GetUIAutomation()
    {
        if (_automation == null)
        {
            try
            {
                var automationType = OperatingSystem.IsWindows() ? Type.GetTypeFromProgID("UIAutomation.CUIAutomation") : null;
                if (automationType != null)
                {
                    _automation = Activator.CreateInstance(automationType);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize UI Automation");
            }
        }
        return _automation;
    }

    private string? GetElementProperty(object element, int propertyId)
    {
        try
        {
            var method = element.GetType().GetMethod("GetCurrentPropertyValue");
            var result = method?.Invoke(element, new object[] { propertyId });
            return result?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private bool GetElementBoolProperty(object element, int propertyId)
    {
        try
        {
            var method = element.GetType().GetMethod("GetCurrentPropertyValue");
            var result = method?.Invoke(element, new object[] { propertyId });
            return result is bool boolValue && boolValue;
        }
        catch
        {
            return false;
        }
    }

    private Rect GetElementBounds(object element)
    {
        try
        {
            var method = element.GetType().GetMethod("GetCurrentPropertyValue");
            var result = method?.Invoke(element, new object[] { UIA_BoundingRectanglePropertyId });
            if (result is double[] rect && rect.Length >= 4)
            {
                return new Rect(rect[0], rect[1], rect[2], rect[3]);
            }
        }
        catch { }
        return new Rect(0, 0, 0, 0);
    }

    private int GetElementControlType(object element)
    {
        try
        {
            var method = element.GetType().GetMethod("GetCurrentPropertyValue");
            var result = method?.Invoke(element, new object[] { UIA_ControlTypePropertyId });
            return result is int intValue ? intValue : 0;
        }
        catch
        {
            return 0;
        }
    }

    private IntPtr GetElementWindowHandle(object element)
    {
        try
        {
            var method = element.GetType().GetMethod("GetCurrentPropertyValue");
            var result = method?.Invoke(element, new object[] { UIA_NativeWindowHandlePropertyId });
            return result is int intValue ? new IntPtr(intValue) : IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    // UI Automation Property IDs
    private const int UIA_NamePropertyId = 30005;
    private const int UIA_ClassNamePropertyId = 30012;
    private const int UIA_BoundingRectanglePropertyId = 30001;
    private const int UIA_ControlTypePropertyId = 30003;
    private const int UIA_IsContentElementPropertyId = 30017;
    private const int UIA_IsControlElementPropertyId = 30016;
    private const int UIA_NativeWindowHandlePropertyId = 30020;

    #endregion

    #region Win32 API Declarations

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const uint GA_ROOT = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr ChildWindowFromPoint(IntPtr hWndParent, POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr RealChildWindowFromPoint(IntPtr hWndParent, POINT point);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    public bool IsSupported => OperatingSystem.IsWindows();
    public bool HasPermissions => OperatingSystem.IsWindows(); // Assume we have permissions on Windows

    public void Dispose()
    {
        // No resources to dispose
    }

    #endregion
}
