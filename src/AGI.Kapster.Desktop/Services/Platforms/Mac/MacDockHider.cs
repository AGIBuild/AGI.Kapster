using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Platforms.Mac;

internal static class MacDockHider
{
    [SupportedOSPlatform("macos")]
    public static void TryHideFromDock()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            var nsApplication = objc_getClass("NSApplication");
            if (nsApplication == IntPtr.Zero)
            {
                return;
            }

            var sharedSel = sel_registerName("sharedApplication");
            var app = objc_msgSend_IntPtr(nsApplication, sharedSel);
            if (app == IntPtr.Zero)
            {
                return;
            }

            // 1 == NSApplicationActivationPolicyAccessory
            var setPolicySel = sel_registerName("setActivationPolicy:");
            var ok = objc_msgSend_bool_int(app, setPolicySel, 1);
            Log.Debug("SetActivationPolicy(Accessory) result: {Result}", ok);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to set macOS ActivationPolicy to Accessory");
        }
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool_int(IntPtr receiver, IntPtr selector, int intArg);
}


