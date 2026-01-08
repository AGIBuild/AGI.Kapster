using AGI.Kapster.Desktop.Services.Telemetry;
using FluentAssertions;
using Xunit;

namespace AGI.Kapster.Tests.Services.Telemetry;

/// <summary>
/// Tests for ITelemetryService implementations
/// </summary>
public class TelemetryServiceTests
{
    #region NullTelemetryService Tests

    [Fact]
    public void NullTelemetryService_IsEnabled_ShouldBeFalse()
    {
        // Arrange
        var service = new NullTelemetryService();

        // Act & Assert
        service.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void NullTelemetryService_TrackEvent_ShouldNotThrow()
    {
        // Arrange
        var service = new NullTelemetryService();

        // Act
        var act = () => service.TrackEvent("TestEvent", new Dictionary<string, string> { ["key"] = "value" });

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void NullTelemetryService_TrackException_ShouldNotThrow()
    {
        // Arrange
        var service = new NullTelemetryService();

        // Act
        var act = () => service.TrackException(new Exception("Test"), new Dictionary<string, string> { ["key"] = "value" });

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void NullTelemetryService_TrackMetric_ShouldNotThrow()
    {
        // Arrange
        var service = new NullTelemetryService();

        // Act
        var act = () => service.TrackMetric("TestMetric", 42.0);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void NullTelemetryService_Flush_ShouldNotThrow()
    {
        // Arrange
        var service = new NullTelemetryService();

        // Act
        var act = () => service.Flush();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region ApplicationInsightsTelemetryService Tests

    [Fact]
    public void ApplicationInsightsTelemetryService_NullConnectionString_ShouldBeDisabled()
    {
        // Arrange & Act
        var service = new ApplicationInsightsTelemetryService(null);

        // Assert
        service.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void ApplicationInsightsTelemetryService_EmptyConnectionString_ShouldBeDisabled()
    {
        // Arrange & Act
        var service = new ApplicationInsightsTelemetryService("");

        // Assert
        service.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void ApplicationInsightsTelemetryService_WhitespaceConnectionString_ShouldBeDisabled()
    {
        // Arrange & Act
        var service = new ApplicationInsightsTelemetryService("   ");

        // Assert
        service.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void ApplicationInsightsTelemetryService_WhenDisabled_TrackEventShouldNotThrow()
    {
        // Arrange
        var service = new ApplicationInsightsTelemetryService(null);

        // Act
        var act = () => service.TrackEvent("TestEvent");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ApplicationInsightsTelemetryService_WhenDisabled_TrackExceptionShouldNotThrow()
    {
        // Arrange
        var service = new ApplicationInsightsTelemetryService(null);

        // Act
        var act = () => service.TrackException(new Exception("Test"));

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ApplicationInsightsTelemetryService_WhenDisabled_FlushShouldNotThrow()
    {
        // Arrange
        var service = new ApplicationInsightsTelemetryService(null);

        // Act
        var act = () => service.Flush();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ApplicationInsightsTelemetryService_Dispose_ShouldNotThrow()
    {
        // Arrange
        var service = new ApplicationInsightsTelemetryService(null);

        // Act
        var act = () => service.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ApplicationInsightsTelemetryService_Dispose_MultipleTimesShouldNotThrow()
    {
        // Arrange
        var service = new ApplicationInsightsTelemetryService(null);

        // Act
        var act = () =>
        {
            service.Dispose();
            service.Dispose();
            service.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ApplicationInsightsTelemetryService_AfterDispose_TrackEventShouldNotThrow()
    {
        // Arrange
        var service = new ApplicationInsightsTelemetryService(null);
        service.Dispose();

        // Act
        var act = () => service.TrackEvent("TestEvent");

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}
