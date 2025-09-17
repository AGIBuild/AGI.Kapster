using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using AGI.Captor.Desktop.Services.Hotkeys;
using AGI.Captor.Desktop.Services.Overlay;
using AGI.Captor.Desktop.Services.Overlay.Platforms;
using AGI.Captor.Desktop.Services;
using AGI.Captor.Desktop.Overlays;
using AGI.Captor.Desktop.Services.ElementDetection;
using AGI.Captor.Desktop.Views;

namespace AGI.Captor.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Check for command line arguments
        var isMinimizedStart = args.Any(arg => arg == "--minimized" || arg == "-m");

        if (isMinimizedStart)
        {
            Log.Debug("Application started in minimized mode from command line");
        }
        
        RunApp(args, isMinimizedStart);
    }
    
    private static void RunApp(string[] args, bool startMinimized )
    {
        var builder = Host.CreateApplicationBuilder(args);
        // Determine environment (default Development for local run; CI/CD sets to Production)
        var environment = builder.Environment.EnvironmentName ?? "Development";
        Log.Information("Starting AGI.Captor in {Environment} environment", environment);

        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        
        // Ensure logs directory exists for file sink
        var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDir);
        
        // Configure Serilog strictly from configuration
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.WithProperty("Environment", environment)
            .CreateLogger();
        
        // Register startup arguments
        var startupArgs = new AGI.Captor.Desktop.Models.AppStartupArgs
        {
            StartMinimized = startMinimized,
            IsAutoStart = startMinimized, // Assume minimized start is auto start
            Args = args
        };
        builder.Services.AddSingleton(startupArgs);

        // Logging is already configured in InitializeLogging
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);

        builder.Services.AddSingleton<ISystemTrayService, SystemTrayService>();
        builder.Services.AddSingleton<IFileSystemService, FileSystemService>();
        builder.Services.AddTransient<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IApplicationController, ApplicationController>();
        builder.Services.AddSingleton<IHotkeyManager, HotkeyManager>();
        builder.Services.AddTransient<SettingsWindow>();
        
        // Register platform-specific hotkey providers
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            builder.Services.AddSingleton<IHotkeyProvider, WindowsHotkeyProvider>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            builder.Services.AddSingleton<IHotkeyProvider, MacHotkeyProvider>();
        }
        else
        {
            builder.Services.AddSingleton<IHotkeyProvider, UnsupportedHotkeyProvider>();
        }
        
        // Register platform-specific services
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            builder.Services.AddTransient<IOverlayWindow, WindowsOverlayWindow>();
            builder.Services.AddTransient<IElementDetector, WindowsElementDetector>();
            builder.Services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>();
            builder.Services.AddSingleton<IOverlayRenderer, WindowsOverlayRenderer>();
            builder.Services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            builder.Services.AddTransient<IOverlayWindow, MacOverlayWindow>();
            builder.Services.AddTransient<IElementDetector, NullElementDetector>();
            builder.Services.AddSingleton<IScreenCaptureStrategy, MacScreenCaptureStrategy>();
            builder.Services.AddSingleton<IOverlayRenderer, WindowsOverlayRenderer>(); // Reuse Windows renderer
            builder.Services.AddSingleton<IClipboardStrategy, MacClipboardStrategy>();
        }
        else
        {
            // Default to Windows implementations for other platforms
            builder.Services.AddTransient<IOverlayWindow, WindowsOverlayWindow>();
            builder.Services.AddTransient<IElementDetector, WindowsElementDetector>();
            builder.Services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>();
            builder.Services.AddSingleton<IOverlayRenderer, WindowsOverlayRenderer>();
            builder.Services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>();
        }
        
        // Register the overlay manager
        builder.Services.AddSingleton<IOverlayController, SimplifiedOverlayManager>();

        var host = builder.Build();

        App.Services = host.Services;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        host.Dispose();
    }
    
    
    // Removed InitializeLogging; Serilog is configured in RunApp strictly from configuration files

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
