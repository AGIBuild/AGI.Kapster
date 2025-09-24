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
using AGI.Kapster.Desktop.Services.Hotkeys;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.Platforms;
using AGI.Kapster.Desktop.Services;
using AGI.Kapster.Desktop.Services.Annotation;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Views;

namespace AGI.Kapster.Desktop;

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

    private static void RunApp(string[] args, bool startMinimized)
    {
        var builder = Host.CreateApplicationBuilder(args);
        // Determine environment (default Development for local run; CI/CD sets to Production)
        var environment = builder.Environment.EnvironmentName ?? "Production";
        Log.Information("Starting AGI.Kapster in {Environment} environment", environment);

        // Configure with fallback protection
        var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(appsettingsPath))
        {
            // Create default configuration if file doesn't exist
            CreateDefaultConfiguration(appsettingsPath);
        }
        
        // Add base configuration with fallback protection
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        // Use user data directory for logs to avoid permission issues
        var userDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataDir = Path.Combine(userDataDir, "AGI.Kapster");
        var logsDir = Path.Combine(appDataDir, "logs");
        Directory.CreateDirectory(logsDir);

        // Configure Serilog with explicit assemblies for single-file deployment
        var loggerConfiguration = new LoggerConfiguration()
            .Enrich.WithProperty("Environment", environment)
            .Enrich.WithProperty("LogsDirectory", logsDir);

        // Configure based on environment
        if (environment == "Development")
        {
            loggerConfiguration
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: Path.Combine(logsDir, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }
        else if (environment == "Production")
        {
            loggerConfiguration
                .MinimumLevel.Warning()
                .WriteTo.File(
                    path: Path.Combine(logsDir, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }
        else
        {
            // Default configuration
            loggerConfiguration
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: Path.Combine(logsDir, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 15,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        Log.Logger = loggerConfiguration.CreateLogger();

        // Register startup arguments
        var startupArgs = new AGI.Kapster.Desktop.Models.AppStartupArgs
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
        builder.Services.AddTransient<ISettingsService>(provider =>
            new SettingsService(
                provider.GetRequiredService<IFileSystemService>(),
                provider.GetRequiredService<IConfiguration>()
            ));
        builder.Services.AddSingleton<IApplicationController, ApplicationController>();
        builder.Services.AddSingleton<IHotkeyManager, HotkeyManager>();
        builder.Services.AddTransient<SettingsWindow>();

        // Auto-update service (only enabled in production)
        builder.Services.AddSingleton<AGI.Kapster.Desktop.Services.Update.IUpdateService, AGI.Kapster.Desktop.Services.Update.UpdateService>();

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

    /// <summary>
    /// Creates a default configuration file if it doesn't exist
    /// </summary>
    /// <param name="filePath">Path where to create the configuration file</param>
    private static void CreateDefaultConfiguration(string filePath)
    {
        try
        {
            var defaultConfig = new
            {
                Application = new
                {
                    Name = "AGI.Kapster",
                    Version = "1.2.0",
                    Description = "Modern Cross-Platform Screenshot and Annotation Tool"
                },
                AutoUpdate = new
                {
                    Enabled = true,
                    CheckFrequencyHours = 24,
                    InstallAutomatically = true,
                    NotifyBeforeInstall = false,
                    UsePreReleases = false,
                    RepositoryOwner = "AGIBuild",
                    RepositoryName = "AGI.Kapster"
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(defaultConfig, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(filePath, json);
            Log.Information("Created default configuration file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to create default configuration file: {FilePath}", filePath);
        }
    }
}
