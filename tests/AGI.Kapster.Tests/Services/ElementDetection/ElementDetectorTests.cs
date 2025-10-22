using System;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Overlay.Platforms;
using FluentAssertions;
using Xunit;

namespace AGI.Kapster.Tests.Services.ElementDetection;

/// <summary>
/// Tests for element detector implementations
/// Note: WindowsElementDetector tests are limited to basic state management
/// as full functionality requires Win32 API integration testing
/// </summary>
public class ElementDetectorTests
{
    #region NullElementDetector Tests

    [Fact]
    public void NullDetector_IsSupported_ShouldBeFalse()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act & Assert
        detector.IsSupported.Should().BeFalse();
    }

    [Fact]
    public void NullDetector_HasPermissions_ShouldBeFalse()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act & Assert
        detector.HasPermissions.Should().BeFalse();
    }

    [Fact]
    public void NullDetector_IsWindowMode_ShouldBeFalse()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act & Assert
        detector.IsWindowMode.Should().BeFalse();
    }

    [Fact]
    public void NullDetector_IsDetectionActive_CanSetAndGet()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act
        detector.IsDetectionActive = true;

        // Assert
        detector.IsDetectionActive.Should().BeTrue();
    }

    [Fact]
    public void NullDetector_DetectElementAt_ShouldReturnNull()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act
        var result = detector.DetectElementAt(100, 200);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NullDetector_DetectElementAt_WithIgnoreWindow_ShouldReturnNull()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act
        var result = detector.DetectElementAt(100, 200, new nint(12345));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NullDetector_ToggleDetectionMode_ShouldNotThrow()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act
        Action act = () => detector.ToggleDetectionMode();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void NullDetector_DetectionModeChanged_CanAddAndRemoveHandlers()
    {
        // Arrange
        var detector = new NullElementDetector();
        var handlerCalled = false;
        Action<bool> handler = (isWindowMode) => { handlerCalled = true; };

        // Act
        detector.DetectionModeChanged += handler;
        detector.ToggleDetectionMode();
        detector.DetectionModeChanged -= handler;

        // Assert
        handlerCalled.Should().BeFalse(); // NullElementDetector doesn't fire events
    }

    [Fact]
    public void NullDetector_Dispose_ShouldNotThrow()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act & Assert
        Action act = () => detector.Dispose();
        act.Should().NotThrow();
    }

    #endregion

    #region WindowsElementDetector Tests

    [Fact]
    public void WindowsDetector_IsSupported_OnWindows_ShouldReflectPlatform()
    {
        // Arrange
        var detector = new WindowsElementDetector();

        // Act & Assert
        if (OperatingSystem.IsWindows())
        {
            detector.IsSupported.Should().BeTrue();
        }
        else
        {
            detector.IsSupported.Should().BeFalse();
        }
    }

    [Fact]
    public void WindowsDetector_HasPermissions_OnWindows_ShouldReflectPlatform()
    {
        // Arrange
        var detector = new WindowsElementDetector();

        // Act & Assert
        if (OperatingSystem.IsWindows())
        {
            detector.HasPermissions.Should().BeTrue();
        }
        else
        {
            detector.HasPermissions.Should().BeFalse();
        }
    }

    [Fact]
    public void WindowsDetector_IsWindowMode_InitiallyTrue()
    {
        // Arrange
        var detector = new WindowsElementDetector();

        // Act & Assert
        detector.IsWindowMode.Should().BeTrue();
    }

    [Fact]
    public void WindowsDetector_IsDetectionActive_CanSetAndGet()
    {
        // Arrange
        var detector = new WindowsElementDetector();

        // Act
        detector.IsDetectionActive = true;

        // Assert
        detector.IsDetectionActive.Should().BeTrue();
    }

    [Fact]
    public void WindowsDetector_IsDetectionActive_ChangingValueMultipleTimes()
    {
        // Arrange
        var detector = new WindowsElementDetector();

        // Act & Assert
        detector.IsDetectionActive = true;
        detector.IsDetectionActive.Should().BeTrue();

        detector.IsDetectionActive = false;
        detector.IsDetectionActive.Should().BeFalse();

        detector.IsDetectionActive = true;
        detector.IsDetectionActive.Should().BeTrue();
    }

    [Fact]
    public void WindowsDetector_ToggleDetectionMode_ShouldChangeMode()
    {
        // Arrange
        var detector = new WindowsElementDetector();
        var initialMode = detector.IsWindowMode;

        // Act
        detector.ToggleDetectionMode();

        // Assert
        detector.IsWindowMode.Should().Be(!initialMode);
    }

    [Fact]
    public void WindowsDetector_ToggleDetectionMode_MultipleTimes_ShouldAlternate()
    {
        // Arrange
        var detector = new WindowsElementDetector();
        var initialMode = detector.IsWindowMode;

        // Act & Assert
        detector.ToggleDetectionMode();
        detector.IsWindowMode.Should().Be(!initialMode);

        detector.ToggleDetectionMode();
        detector.IsWindowMode.Should().Be(initialMode);

        detector.ToggleDetectionMode();
        detector.IsWindowMode.Should().Be(!initialMode);
    }

    [Fact]
    public void WindowsDetector_ToggleDetectionMode_ShouldRaiseEvent()
    {
        // Arrange
        var detector = new WindowsElementDetector();
        var eventRaised = false;
        var capturedMode = false;

        detector.DetectionModeChanged += (isWindowMode) =>
        {
            eventRaised = true;
            capturedMode = isWindowMode;
        };

        // Act
        detector.ToggleDetectionMode();

        // Assert
        eventRaised.Should().BeTrue();
        capturedMode.Should().Be(detector.IsWindowMode);
    }

    [Fact]
    public void WindowsDetector_DetectElementAt_OnNonWindows_ShouldReturnNull()
    {
        // Arrange
        var detector = new WindowsElementDetector();

        // Act & Assert
        if (!OperatingSystem.IsWindows())
        {
            var result = detector.DetectElementAt(100, 200);
            result.Should().BeNull();
        }
    }

    [Fact]
    public void WindowsDetector_DetectElementAt_WithInvalidCoordinates_ShouldHandleGracefully()
    {
        // Arrange
        var detector = new WindowsElementDetector();

        // Act
        var result1 = detector.DetectElementAt(-1, -1);
        var result2 = detector.DetectElementAt(int.MaxValue, int.MaxValue);

        // Assert - Should not throw, may return null or a valid window
        // (Actual behavior depends on platform and window configuration)
        Action act1 = () => detector.DetectElementAt(-1, -1);
        Action act2 = () => detector.DetectElementAt(int.MaxValue, int.MaxValue);
        
        act1.Should().NotThrow();
        act2.Should().NotThrow();
    }

    [Fact]
    public void WindowsDetector_Dispose_ShouldNotThrow()
    {
        // Arrange
        var detector = new WindowsElementDetector();

        // Act & Assert
        Action act = () => detector.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void WindowsDetector_MultipleInstances_ShouldBeIndependent()
    {
        // Arrange
        var detector1 = new WindowsElementDetector();
        var detector2 = new WindowsElementDetector();

        // Act
        detector1.IsDetectionActive = true;
        detector1.ToggleDetectionMode(); // Change from window to element mode

        // Assert
        detector1.IsDetectionActive.Should().BeTrue();
        detector2.IsDetectionActive.Should().BeFalse();
        
        detector1.IsWindowMode.Should().BeFalse();
        detector2.IsWindowMode.Should().BeTrue();
    }

    #endregion

    #region DetectedElement Tests

    [Fact]
    public void DetectedElement_Construction_ShouldSetProperties()
    {
        // Arrange
        var bounds = new Avalonia.Rect(100, 200, 300, 400);
        var name = "Test Window";
        var className = "TestClass";
        var processName = "TestProcess";
        var handle = new nint(12345);

        // Act
        var element = new DetectedElement(bounds, name, className, processName, handle, true);

        // Assert
        element.Bounds.Should().Be(bounds);
        element.Name.Should().Be(name);
        element.ClassName.Should().Be(className);
        element.ProcessName.Should().Be(processName);
        element.WindowHandle.Should().Be(handle);
        element.IsWindow.Should().BeTrue();
    }

    [Fact]
    public void DetectedElement_Construction_WithEmptyStrings_ShouldWork()
    {
        // Arrange & Act
        var element = new DetectedElement(
            new Avalonia.Rect(0, 0, 100, 100),
            string.Empty,
            string.Empty,
            string.Empty,
            nint.Zero,
            false);

        // Assert
        element.Name.Should().BeEmpty();
        element.ClassName.Should().BeEmpty();
        element.ProcessName.Should().BeEmpty();
        element.WindowHandle.Should().Be(nint.Zero);
        element.IsWindow.Should().BeFalse();
    }

    #endregion
}

