using System;
using System.IO;
using System.Linq;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using AGI.Captor.App.Services.Hotkeys;
using AGI.Captor.App.Services.Overlay;
using AGI.Captor.App.Services;
using AGI.Captor.App.Overlays;
using AGI.Captor.App.Views;

namespace AGI.Captor.App;

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
        
        // Register startup arguments
        var startupArgs = new AGI.Captor.App.Models.AppStartupArgs
        {
            StartMinimized = startMinimized,
            IsAutoStart = startMinimized, // Assume minimized start is auto start
            Args = args
        };
        builder.Services.AddSingleton(startupArgs);

        var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDir);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(logsDir, "app-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);

        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddSingleton<AGI.Captor.App.ViewModels.MainWindowViewModel>();
        builder.Services.AddSingleton<ISystemTrayService, SystemTrayService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IApplicationController, ApplicationController>();
        builder.Services.AddSingleton<IHotkeyManager, HotkeyManager>();
        builder.Services.AddTransient<SettingsWindow>();
        if (OperatingSystem.IsWindows())
        {
            builder.Services.AddSingleton<IHotkeyProvider, WindowsHotkeyProvider>();
            builder.Services.AddSingleton<IElementDetector, WindowsElementDetector>();
        }
        else if (OperatingSystem.IsMacOS())
        {
            builder.Services.AddSingleton<IHotkeyProvider, MacHotkeyProvider>();
            // TODO: Add MacElementDetector when implementing T-0202
            builder.Services.AddSingleton<IElementDetector, WindowsElementDetector>(); // Placeholder
        }

        builder.Services.AddSingleton<IOverlayController>(provider =>
        {
            var hotkeyProvider = provider.GetService<IHotkeyProvider>();
            var elementDetector = provider.GetService<IElementDetector>();
            return new OverlayWindowManager(hotkeyProvider, elementDetector);
        });

        var host = builder.Build();

        Log.Debug("AGI.Captor starting. Logs at {LogsDir}", logsDir);

        App.Services = host.Services;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        host.Dispose();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
