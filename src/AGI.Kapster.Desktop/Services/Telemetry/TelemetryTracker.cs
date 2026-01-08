using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Telemetry;

/// <summary>
/// Static helper class for tracking telemetry events throughout the application.
/// Provides both predefined events and custom event support with a fluent API.
/// </summary>
/// <example>
/// // Track predefined events
/// TelemetryTracker.TrackAppStarted();
/// TelemetryTracker.TrackScreenshotCaptured(CaptureMode.Region, hasAnnotations: true);
/// 
/// // Track custom events with fluent API
/// TelemetryTracker.Event("CustomAction")
///     .WithProperty("key", "value")
///     .WithDuration(stopwatch.Elapsed)
///     .Send();
/// 
/// // Track exceptions
/// TelemetryTracker.TrackError(exception, "ContextInfo");
/// </example>
public static class TelemetryTracker
{
    private static ITelemetryService? _service;
    private static readonly object _lock = new();

    #region Initialization

    /// <summary>
    /// Initializes the tracker with a service provider.
    /// Called automatically during app startup.
    /// </summary>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        lock (_lock)
        {
            _service = serviceProvider.GetService<ITelemetryService>();
            if (_service?.IsEnabled == true)
            {
                Log.Debug("TelemetryTracker initialized");
            }
        }
    }

    /// <summary>
    /// Gets whether telemetry is enabled and properly configured
    /// </summary>
    public static bool IsEnabled => _service?.IsEnabled ?? false;

    #endregion

    #region Predefined Events - Application Lifecycle

    /// <summary>
    /// Track application startup with environment information
    /// </summary>
    public static void TrackAppStarted()
    {
        if (!IsEnabled) return;

        var props = EnvironmentInfo.GetProperties();
        _service?.TrackEvent(EventNames.AppStarted, props);
        Log.Debug("Telemetry: {Event} ({Summary})", EventNames.AppStarted, EnvironmentInfo.GetSummary());
    }

    /// <summary>
    /// Track application exit
    /// </summary>
    /// <param name="reason">Exit reason</param>
    public static void TrackAppExited(ExitReason reason = ExitReason.UserRequested)
    {
        if (!IsEnabled) return;

        _service?.TrackEvent(EventNames.AppExited, new Dictionary<string, string>
        {
            [PropertyNames.ExitReason] = reason.ToString()
        });
        // Note: Flush is handled automatically by ITelemetryService.Dispose() during shutdown
    }

    #endregion

    #region Predefined Events - Screenshot & Capture

    /// <summary>
    /// Track screenshot captured event
    /// </summary>
    /// <param name="mode">Capture mode used</param>
    /// <param name="hasAnnotations">Whether annotations were added</param>
    /// <param name="annotationCount">Number of annotations</param>
    /// <param name="durationMs">Time taken to capture (optional)</param>
    public static void TrackScreenshotCaptured(
        CaptureMode mode,
        bool hasAnnotations = false,
        int annotationCount = 0,
        long? durationMs = null)
    {
        if (!IsEnabled) return;

        var props = new Dictionary<string, string>
        {
            [PropertyNames.CaptureMode] = mode.ToString(),
            [PropertyNames.HasAnnotations] = hasAnnotations.ToString(),
            [PropertyNames.AnnotationCount] = annotationCount.ToString()
        };

        if (durationMs.HasValue)
        {
            props[PropertyNames.DurationMs] = durationMs.Value.ToString();
        }

        _service?.TrackEvent(EventNames.ScreenshotCaptured, props);
    }

    /// <summary>
    /// Track screenshot cancelled event
    /// </summary>
    /// <param name="reason">Cancellation reason</param>
    public static void TrackScreenshotCancelled(string? reason = null)
    {
        if (!IsEnabled) return;

        var props = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(reason))
        {
            props[PropertyNames.CancelReason] = reason;
        }

        _service?.TrackEvent(EventNames.ScreenshotCancelled, props);
    }

    /// <summary>
    /// Track screenshot saved event
    /// </summary>
    /// <param name="format">Image format (PNG, JPEG, etc.)</param>
    /// <param name="destination">Where it was saved (File, Clipboard, Both)</param>
    public static void TrackScreenshotSaved(string format, SaveDestination destination)
    {
        if (!IsEnabled) return;

        _service?.TrackEvent(EventNames.ScreenshotSaved, new Dictionary<string, string>
        {
            [PropertyNames.ImageFormat] = format,
            [PropertyNames.SaveDestination] = destination.ToString()
        });
    }

    #endregion

    #region Predefined Events - Annotations

    /// <summary>
    /// Track annotation tool used
    /// </summary>
    /// <param name="toolType">Type of annotation tool</param>
    public static void TrackAnnotationUsed(AnnotationToolType toolType)
    {
        if (!IsEnabled) return;

        _service?.TrackEvent(EventNames.AnnotationUsed, new Dictionary<string, string>
        {
            [PropertyNames.ToolType] = toolType.ToString()
        });
    }

    #endregion

    #region Predefined Events - Features

    /// <summary>
    /// Track settings opened
    /// </summary>
    public static void TrackSettingsOpened()
    {
        if (!IsEnabled) return;
        _service?.TrackEvent(EventNames.SettingsOpened);
    }

    /// <summary>
    /// Track settings changed
    /// </summary>
    /// <param name="settingName">Name of the setting changed</param>
    public static void TrackSettingChanged(string settingName)
    {
        if (!IsEnabled) return;

        _service?.TrackEvent(EventNames.SettingChanged, new Dictionary<string, string>
        {
            [PropertyNames.SettingName] = settingName
        });
    }

    /// <summary>
    /// Track hotkey triggered
    /// </summary>
    /// <param name="action">Action triggered by hotkey</param>
    public static void TrackHotkeyTriggered(string action)
    {
        if (!IsEnabled) return;

        _service?.TrackEvent(EventNames.HotkeyTriggered, new Dictionary<string, string>
        {
            [PropertyNames.Action] = action
        });
    }

    /// <summary>
    /// Track update check
    /// </summary>
    /// <param name="updateAvailable">Whether an update was found</param>
    /// <param name="newVersion">New version if available</param>
    public static void TrackUpdateChecked(bool updateAvailable, string? newVersion = null)
    {
        if (!IsEnabled) return;

        var props = new Dictionary<string, string>
        {
            [PropertyNames.UpdateAvailable] = updateAvailable.ToString()
        };

        if (!string.IsNullOrEmpty(newVersion))
        {
            props[PropertyNames.NewVersion] = newVersion;
        }

        _service?.TrackEvent(EventNames.UpdateChecked, props);
    }

    #endregion

    #region Error Tracking

    /// <summary>
    /// Track an exception/error
    /// </summary>
    /// <param name="exception">The exception to track</param>
    /// <param name="context">Additional context about where the error occurred</param>
    /// <param name="isFatal">Whether this error is fatal</param>
    public static void TrackError(Exception exception, string? context = null, bool isFatal = false)
    {
        if (!IsEnabled || exception == null) return;

        var props = new Dictionary<string, string>
        {
            [PropertyNames.IsFatal] = isFatal.ToString()
        };

        if (!string.IsNullOrEmpty(context))
        {
            props[PropertyNames.ErrorContext] = context;
        }

        _service?.TrackException(exception, props);

        if (isFatal)
        {
            FlushImmediate();
        }
    }

    /// <summary>
    /// Track an unhandled exception (called from global exception handler)
    /// </summary>
    public static void TrackUnhandledException(Exception exception, bool isTerminating)
    {
        if (exception == null) return;

        // Always try to track unhandled exceptions, even if service reports disabled
        try
        {
            _service?.TrackException(exception, new Dictionary<string, string>
            {
                [PropertyNames.ExceptionType] = "UnhandledException",
                [PropertyNames.IsTerminating] = isTerminating.ToString(),
                [PropertyNames.IsFatal] = isTerminating.ToString()
            });

            if (isTerminating)
            {
                FlushImmediate();
            }
        }
        catch
        {
            // Silently ignore telemetry errors
        }
    }

    #endregion

    #region Metrics

    /// <summary>
    /// Track a metric value
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Metric value</param>
    public static void TrackMetric(string name, double value)
    {
        if (!IsEnabled) return;
        _service?.TrackMetric(name, value);
    }

    /// <summary>
    /// Track a duration metric
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="duration">Duration value</param>
    public static void TrackDuration(string name, TimeSpan duration)
    {
        if (!IsEnabled) return;
        _service?.TrackMetric(name, duration.TotalMilliseconds);
    }

    #endregion

    #region Custom Events - Fluent API

    /// <summary>
    /// Create a custom event builder for fluent API
    /// </summary>
    /// <param name="eventName">Name of the event</param>
    /// <returns>Event builder for chaining</returns>
    /// <example>
    /// TelemetryTracker.Event("CustomAction")
    ///     .WithProperty("key", "value")
    ///     .WithDuration(stopwatch.Elapsed)
    ///     .Send();
    /// </example>
    public static EventBuilder Event(string eventName)
    {
        return new EventBuilder(eventName, _service);
    }

    /// <summary>
    /// Create a timed operation that automatically tracks duration
    /// </summary>
    /// <param name="eventName">Name of the event to track when disposed</param>
    /// <returns>Disposable operation tracker</returns>
    /// <example>
    /// using (TelemetryTracker.TimedOperation("LongRunningTask"))
    /// {
    ///     // ... operation code ...
    /// } // Duration automatically tracked on dispose
    /// </example>
    public static TimedOperation TimedOperation(string eventName)
    {
        return new TimedOperation(eventName, _service);
    }

    #endregion

    #region Internal

    /// <summary>
    /// Flush pending telemetry data immediately (used for fatal errors only)
    /// Normal cleanup is handled automatically by ITelemetryService.Dispose()
    /// </summary>
    private static void FlushImmediate()
    {
        try
        {
            _service?.Flush();
        }
        catch
        {
            // Silently ignore
        }
    }

    #endregion
}

#region Event Builder (Fluent API)

/// <summary>
/// Fluent builder for custom telemetry events
/// </summary>
public sealed class EventBuilder
{
    private readonly string _eventName;
    private readonly ITelemetryService? _service;
    private readonly Dictionary<string, string> _properties = new();

    internal EventBuilder(string eventName, ITelemetryService? service)
    {
        _eventName = eventName;
        _service = service;
    }

    /// <summary>
    /// Add a property to the event
    /// </summary>
    public EventBuilder WithProperty(string key, string value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// Add a property to the event (with object value)
    /// </summary>
    public EventBuilder WithProperty(string key, object? value)
    {
        _properties[key] = value?.ToString() ?? "null";
        return this;
    }

    /// <summary>
    /// Add multiple properties
    /// </summary>
    public EventBuilder WithProperties(IDictionary<string, string> properties)
    {
        foreach (var kvp in properties)
        {
            _properties[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    /// Add duration property
    /// </summary>
    public EventBuilder WithDuration(TimeSpan duration)
    {
        _properties[PropertyNames.DurationMs] = duration.TotalMilliseconds.ToString("F0");
        return this;
    }

    /// <summary>
    /// Add duration property from stopwatch
    /// </summary>
    public EventBuilder WithDuration(Stopwatch stopwatch)
    {
        return WithDuration(stopwatch.Elapsed);
    }

    /// <summary>
    /// Add success/failure status
    /// </summary>
    public EventBuilder WithSuccess(bool success)
    {
        _properties[PropertyNames.Success] = success.ToString();
        return this;
    }

    /// <summary>
    /// Send the event
    /// </summary>
    public void Send()
    {
        if (_service == null || !_service.IsEnabled) return;
        _service.TrackEvent(_eventName, _properties.Count > 0 ? _properties : null);
    }
}

#endregion

#region Timed Operation

/// <summary>
/// Tracks the duration of an operation and sends telemetry when disposed
/// </summary>
public sealed class TimedOperation : IDisposable
{
    private readonly string _eventName;
    private readonly ITelemetryService? _service;
    private readonly Stopwatch _stopwatch;
    private readonly Dictionary<string, string> _properties = new();
    private bool _disposed;

    internal TimedOperation(string eventName, ITelemetryService? service)
    {
        _eventName = eventName;
        _service = service;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Add a property to be included when the operation completes
    /// </summary>
    public TimedOperation WithProperty(string key, string value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// Mark the operation as failed
    /// </summary>
    public void SetFailed(string? reason = null)
    {
        _properties[PropertyNames.Success] = "False";
        if (!string.IsNullOrEmpty(reason))
        {
            _properties[PropertyNames.FailureReason] = reason;
        }
    }

    /// <summary>
    /// Mark the operation as successful (default)
    /// </summary>
    public void SetSucceeded()
    {
        _properties[PropertyNames.Success] = "True";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stopwatch.Stop();

        if (_service == null || !_service.IsEnabled) return;

        _properties[PropertyNames.DurationMs] = _stopwatch.ElapsedMilliseconds.ToString();

        if (!_properties.ContainsKey(PropertyNames.Success))
        {
            _properties[PropertyNames.Success] = "True";
        }

        _service.TrackEvent(_eventName, _properties);
    }
}

#endregion

#region Enums and Constants

/// <summary>
/// Predefined event names
/// </summary>
public static class EventNames
{
    // Application lifecycle
    public const string AppStarted = "App_Started";
    public const string AppExited = "App_Exited";

    // Screenshot & Capture
    public const string ScreenshotCaptured = "Screenshot_Captured";
    public const string ScreenshotCancelled = "Screenshot_Cancelled";
    public const string ScreenshotSaved = "Screenshot_Saved";

    // Annotations
    public const string AnnotationUsed = "Annotation_Used";

    // Features
    public const string SettingsOpened = "Settings_Opened";
    public const string SettingChanged = "Setting_Changed";
    public const string HotkeyTriggered = "Hotkey_Triggered";
    public const string UpdateChecked = "Update_Checked";
}

/// <summary>
/// Predefined property names
/// </summary>
public static class PropertyNames
{
    // General
    public const string Success = "success";
    public const string DurationMs = "duration_ms";
    public const string FailureReason = "failure_reason";

    // App lifecycle
    public const string ExitReason = "exit_reason";

    // Capture
    public const string CaptureMode = "capture_mode";
    public const string HasAnnotations = "has_annotations";
    public const string AnnotationCount = "annotation_count";
    public const string CancelReason = "cancel_reason";
    public const string ImageFormat = "image_format";
    public const string SaveDestination = "save_destination";

    // Annotations
    public const string ToolType = "tool_type";

    // Settings
    public const string SettingName = "setting_name";
    public const string Action = "action";

    // Updates
    public const string UpdateAvailable = "update_available";
    public const string NewVersion = "new_version";

    // Errors
    public const string IsFatal = "is_fatal";
    public const string ErrorContext = "error_context";
    public const string ExceptionType = "exception_type";
    public const string IsTerminating = "is_terminating";
}

/// <summary>
/// Application exit reasons
/// </summary>
public enum ExitReason
{
    UserRequested,
    SystemShutdown,
    Update,
    Error
}

/// <summary>
/// Screenshot capture modes
/// </summary>
public enum CaptureMode
{
    Region,
    FullScreen,
    Window,
    ActiveWindow
}

/// <summary>
/// Where screenshots are saved
/// </summary>
public enum SaveDestination
{
    File,
    Clipboard,
    Both
}

/// <summary>
/// Annotation tool types
/// </summary>
public enum AnnotationToolType
{
    Arrow,
    Rectangle,
    Ellipse,
    Line,
    Freehand,
    Text,
    Highlighter,
    Blur,
    Numbering
}

#endregion
