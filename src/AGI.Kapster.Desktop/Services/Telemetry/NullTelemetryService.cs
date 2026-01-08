using System;
using System.Collections.Generic;

namespace AGI.Kapster.Desktop.Services.Telemetry;

/// <summary>
/// Null implementation of ITelemetryService
/// Used when telemetry is disabled or not configured
/// </summary>
public sealed class NullTelemetryService : ITelemetryService
{
    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public void TrackEvent(string eventName, IDictionary<string, string>? properties = null)
    {
        // No-op
    }

    /// <inheritdoc />
    public void TrackException(Exception exception, IDictionary<string, string>? properties = null)
    {
        // No-op
    }

    /// <inheritdoc />
    public void TrackMetric(string name, double value)
    {
        // No-op
    }

    /// <inheritdoc />
    public void Flush()
    {
        // No-op
    }
}
