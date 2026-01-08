using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using AGI.Kapster.Desktop.Extensions;

namespace AGI.Kapster.Desktop;

class Program
{
    private const string MutexName = "AGI.Kapster.SingleInstance.Mutex";
    private static Mutex? _instanceMutex;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Check for single instance
        _instanceMutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running
            Console.WriteLine("AGI.Kapster is already running. Only one instance is allowed.");
            Log.Warning("Application startup blocked - another instance is already running");
            return;
        }

        try
        {
            // Check for command line arguments
            var isMinimizedStart = args.Any(arg => arg == "--minimized" || arg == "-m");

            if (isMinimizedStart)
            {
                Log.Debug("Application started in minimized mode from command line");
            }

            RunApp(args, isMinimizedStart);
        }
        finally
        {
            // Release mutex on exit
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
        }
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

        // Add User Secrets in Development environment (for local InstrumentationKey etc.)
        if (environment == "Development")
        {
            builder.Configuration.AddUserSecrets<Program>(optional: true);
        }

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
                    path: Path.Combine(logsDir, ".log"),
                    shared: true,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }
        else if (environment == "Production")
        {
            loggerConfiguration
                .MinimumLevel.Warning()
                .WriteTo.File(
                    path: Path.Combine(logsDir, ".log"),
                    shared: true,
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
                    path: Path.Combine(logsDir, ".log"),
                    shared: true,
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

        // Register all application services using unified extension method
        builder.Services.AddKapsterServices(builder.Configuration);

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
    NotifyBeforeInstall = true,
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
