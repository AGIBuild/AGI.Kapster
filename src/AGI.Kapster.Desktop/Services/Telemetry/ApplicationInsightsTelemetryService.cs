using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Telemetry;

/// <summary>
/// Application Insights implementation of ITelemetryService
/// All operations are async/fire-and-forget to avoid blocking the UI
/// </summary>
public sealed class ApplicationInsightsTelemetryService : ITelemetryService, IDisposable
{
    private readonly TelemetryClient? _client;
    private readonly TelemetryConfiguration? _configuration;
    private readonly string _sessionId;
    private readonly string _installId;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsEnabled => _client != null;

    /// <summary>
    /// Creates a new Application Insights telemetry service
    /// </summary>
    /// <param name="connectionStringOrKey">The Application Insights connection string (preferred) or instrumentation key</param>
    public ApplicationInsightsTelemetryService(string? connectionStringOrKey)
    {
        _sessionId = Guid.NewGuid().ToString("N")[..8];
        _installId = GetOrCreateInstallId();

        if (string.IsNullOrWhiteSpace(connectionStringOrKey))
        {
            Log.Information("Telemetry disabled: No connection string or instrumentation key configured");
            return;
        }

        try
        {
            _configuration = TelemetryConfiguration.CreateDefault();

            // Support both ConnectionString (preferred) and InstrumentationKey (legacy)
            if (connectionStringOrKey.Contains("InstrumentationKey=", StringComparison.OrdinalIgnoreCase))
            {
                // It's a full connection string
                _configuration.ConnectionString = connectionStringOrKey;
            }
            else if (Guid.TryParse(connectionStringOrKey, out _))
            {
                // It's just an instrumentation key (GUID format), convert to connection string
                _configuration.ConnectionString = $"InstrumentationKey={connectionStringOrKey}";
            }
            else
            {
                // Assume it's a connection string
                _configuration.ConnectionString = connectionStringOrKey;
            }

            // Disable developer mode to ensure async behavior
            _configuration.TelemetryChannel.DeveloperMode = false;

            _client = new TelemetryClient(_configuration);

            // Set common context properties
            _client.Context.Session.Id = _sessionId;
            _client.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
            _client.Context.Component.Version = GetAppVersion();
            _client.Context.GlobalProperties["install_id"] = _installId;
            _client.Context.GlobalProperties["platform"] = GetPlatform();

            Log.Information("Application Insights telemetry initialized (session: {SessionId}, endpoint: {Endpoint})", 
                _sessionId, 
                _configuration.ConnectionString?.Contains("IngestionEndpoint") == true ? "configured" : "default");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize Application Insights telemetry");
            _client = null;
            _configuration?.Dispose();
            _configuration = null;
        }
    }

    /// <inheritdoc />
    public void TrackEvent(string eventName, IDictionary<string, string>? properties = null)
    {
        if (_client == null || _disposed) return;

        try
        {
            var telemetry = new EventTelemetry(eventName);

            if (properties != null)
            {
                foreach (var kvp in properties)
                {
                    telemetry.Properties[kvp.Key] = kvp.Value;
                }
            }

            _client.TrackEvent(telemetry);
            Log.Debug("Telemetry event queued: {EventName} (props: {PropCount})", eventName, properties?.Count ?? 0);
        }
        catch (Exception ex)
        {
            // Silently fail - telemetry should never break the app
            Log.Debug(ex, "Failed to track event: {EventName}", eventName);
        }
    }

    /// <inheritdoc />
    public void TrackException(Exception exception, IDictionary<string, string>? properties = null)
    {
        if (_client == null || _disposed) return;

        try
        {
            var telemetry = new ExceptionTelemetry(exception);

            if (properties != null)
            {
                foreach (var kvp in properties)
                {
                    telemetry.Properties[kvp.Key] = kvp.Value;
                }
            }

            _client.TrackException(telemetry);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to track exception");
        }
    }

    /// <inheritdoc />
    public void TrackMetric(string name, double value)
    {
        if (_client == null || _disposed) return;

        try
        {
            _client.TrackMetric(name, value);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to track metric: {MetricName}", name);
        }
    }

    /// <inheritdoc />
    public void Flush()
    {
        if (_client == null || _disposed) return;

        try
        {
            Log.Debug("Flushing telemetry data...");
            _client.Flush();
            // Give enough time for the flush to complete (network I/O)
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
            Log.Debug("Telemetry flush completed");
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to flush telemetry");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Flush();
        _configuration?.Dispose();
    }

    private static string GetAppVersion()
    {
        try
        {
            return Assembly.GetExecutingAssembly()
                .GetName()
                .Version?
                .ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string GetPlatform()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsMacOS()) return "macos";
        if (OperatingSystem.IsLinux()) return "linux";
        return "unknown";
    }

    private static string GetOrCreateInstallId()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var kapsterDir = System.IO.Path.Combine(appDataPath, "AGI.Kapster");
            var installIdPath = System.IO.Path.Combine(kapsterDir, ".install_id");

            if (System.IO.File.Exists(installIdPath))
            {
                var existingId = System.IO.File.ReadAllText(installIdPath).Trim();
                if (!string.IsNullOrEmpty(existingId))
                {
                    return existingId;
                }
            }

            // Generate new install ID
            var newId = Guid.NewGuid().ToString("N");
            System.IO.Directory.CreateDirectory(kapsterDir);
            System.IO.File.WriteAllText(installIdPath, newId);
            return newId;
        }
        catch
        {
            // Fallback to session-based ID if we can't persist
            return Guid.NewGuid().ToString("N");
        }
    }
}
