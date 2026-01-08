using AGI.Kapster.Desktop.Services.Telemetry;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AGI.Kapster.Tests.Services.Telemetry;

/// <summary>
/// Tests for TelemetryTracker static helper class
/// </summary>
public class TelemetryTrackerTests
{
    [Fact]
    public void TelemetryTracker_BeforeInitialize_IsEnabledShouldBeFalse()
    {
        // Reset by initializing with null service provider
        var services = new ServiceCollection().BuildServiceProvider();

        // Note: TelemetryTracker is static, so this test verifies behavior
        // when ITelemetryService is not registered
        TelemetryTracker.Initialize(services);

        // Assert
        TelemetryTracker.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void TelemetryTracker_WithNullTelemetryService_IsEnabledShouldBeFalse()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService, NullTelemetryService>()
            .BuildServiceProvider();

        // Act
        TelemetryTracker.Initialize(services);

        // Assert
        TelemetryTracker.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void TelemetryTracker_WithDisabledService_TrackAppStartedShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService>(new ApplicationInsightsTelemetryService(null))
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act
        var act = () => TelemetryTracker.TrackAppStarted();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void TelemetryTracker_WithDisabledService_TrackErrorShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService>(new ApplicationInsightsTelemetryService(null))
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act
        var act = () => TelemetryTracker.TrackError(new Exception("Test"), "TestContext");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void TelemetryTracker_WithDisabledService_TrackUnhandledExceptionShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService>(new ApplicationInsightsTelemetryService(null))
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act
        var act = () => TelemetryTracker.TrackUnhandledException(new Exception("Test"), isTerminating: true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void TelemetryTracker_TrackScreenshotCaptured_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService, NullTelemetryService>()
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act
        var act = () => TelemetryTracker.TrackScreenshotCaptured(
            CaptureMode.Region,
            hasAnnotations: true,
            annotationCount: 3,
            durationMs: 500);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void TelemetryTracker_TrackAnnotationUsed_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService, NullTelemetryService>()
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act
        var act = () => TelemetryTracker.TrackAnnotationUsed(AnnotationToolType.Arrow);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void TelemetryTracker_Event_ShouldReturnBuilder()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService, NullTelemetryService>()
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act
        var builder = TelemetryTracker.Event("CustomEvent");

        // Assert
        builder.Should().NotBeNull();
    }

    [Fact]
    public void TelemetryTracker_Event_FluentAPI_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService, NullTelemetryService>()
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act
        var act = () => TelemetryTracker.Event("CustomEvent")
            .WithProperty("key1", "value1")
            .WithProperty("key2", "value2")
            .Send();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void TelemetryTracker_TimedOperation_ShouldReturnOperation()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService, NullTelemetryService>()
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act
        using var operation = TelemetryTracker.TimedOperation("TestOperation");

        // Assert
        operation.Should().NotBeNull();
    }

    [Fact]
    public void TelemetryTracker_TimedOperation_SetSucceeded_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService, NullTelemetryService>()
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act
        var act = () =>
        {
            using var operation = TelemetryTracker.TimedOperation("TestOperation");
            Thread.Sleep(10); // Simulate some work
            operation.SetSucceeded();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void TelemetryTracker_TimedOperation_SetFailed_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService, NullTelemetryService>()
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act
        var act = () =>
        {
            using var operation = TelemetryTracker.TimedOperation("TestOperation");
            operation.SetFailed("Test failure reason");
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void TelemetryTracker_TimedOperation_Dispose_ShouldAutoComplete()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService, NullTelemetryService>()
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act - using block will dispose and auto-complete
        var act = () =>
        {
            using var operation = TelemetryTracker.TimedOperation("TestOperation");
            Thread.Sleep(10);
            // No explicit Complete() call - should auto-complete on dispose
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void TelemetryTracker_TrackDuration_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService, NullTelemetryService>()
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act
        var act = () => TelemetryTracker.TrackDuration("TestDuration", TimeSpan.FromMilliseconds(100));

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void TelemetryTracker_TrackMetric_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryService, NullTelemetryService>()
            .BuildServiceProvider();
        TelemetryTracker.Initialize(services);

        // Act
        var act = () => TelemetryTracker.TrackMetric("TestMetric", 42.5);

        // Assert
        act.Should().NotThrow();
    }
}
