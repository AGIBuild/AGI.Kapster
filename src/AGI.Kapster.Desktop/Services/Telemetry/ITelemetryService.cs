using System;
using System.Collections.Generic;

namespace AGI.Kapster.Desktop.Services.Telemetry;

/// <summary>
/// Interface for application telemetry service
/// Provides methods for tracking events, exceptions, and metrics
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Gets whether telemetry is enabled and properly configured
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Track a custom event with optional properties
    /// </summary>
    /// <param name="eventName">Name of the event</param>
    /// <param name="properties">Optional properties to attach to the event</param>
    void TrackEvent(string eventName, IDictionary<string, string>? properties = null);

    /// <summary>
    /// Track an exception with optional properties
    /// </summary>
    /// <param name="exception">The exception to track</param>
    /// <param name="properties">Optional properties to attach</param>
    void TrackException(Exception exception, IDictionary<string, string>? properties = null);

    /// <summary>
    /// Track a metric value
    /// </summary>
    /// <param name="name">Name of the metric</param>
    /// <param name="value">Value of the metric</param>
    void TrackMetric(string name, double value);

    /// <summary>
    /// Flush any pending telemetry data
    /// Should be called before application exit
    /// </summary>
    void Flush();
}
