using System;
using System.IO;
using System.Linq;
using Avalonia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using AGI.Captor.Desktop.Services.Hotkeys;
using AGI.Captor.Desktop.Services.Overlay;
using AGI.Captor.Desktop.Services;
using AGI.Captor.Desktop.Overlays;
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

        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddSingleton<AGI.Captor.Desktop.ViewModels.MainWindowViewModel>();
        builder.Services.AddSingleton<ISystemTrayService, SystemTrayService>();
        builder.Services.AddTransient<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IApplicationController, ApplicationController>();
        builder.Services.AddSingleton<IHotkeyManager, HotkeyManager>();
        builder.Services.AddTransient<SettingsWindow>();
        
        // Element detector (platform-specific)
        if (OperatingSystem.IsWindows())
        {
            builder.Services.AddSingleton<IElementDetector, WindowsElementDetector>();
        }
        else if (OperatingSystem.IsMacOS())
        {
            // TODO: Add MacElementDetector when implementing T-0202
            builder.Services.AddSingleton<IElementDetector, WindowsElementDetector>(); // Placeholder
        }

        builder.Services.AddSingleton<IOverlayController>(provider =>
        {
            var elementDetector = provider.GetService<IElementDetector>();
            return new OverlayWindowManager(elementDetector);
        });

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
