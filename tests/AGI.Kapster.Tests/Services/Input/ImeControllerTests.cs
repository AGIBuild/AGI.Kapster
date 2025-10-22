using AGI.Kapster.Desktop.Services.Input;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using AGI.Kapster.Tests.TestHelpers;

namespace AGI.Kapster.Tests.Services.Input;

/// <summary>
/// Tests for IME controller implementations
/// </summary>
public class ImeControllerTests : TestBase
{
    public ImeControllerTests(ITestOutputHelper output) : base(output)
    {
    }

    #region NoOpImeController Tests

    [Fact]
    public void NoOpImeController_IsSupported_ShouldBeFalse()
    {
        // Arrange
        var controller = new NoOpImeController();

        // Act & Assert
        controller.IsSupported.Should().BeFalse();
    }

    [Fact]
    public void NoOpImeController_DisableIme_ShouldNotThrow()
    {
        // Arrange
        var controller = new NoOpImeController();

        // Act
        var action = () => controller.DisableIme(new nint(12345));

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void NoOpImeController_EnableIme_ShouldNotThrow()
    {
        // Arrange
        var controller = new NoOpImeController();

        // Act
        var action = () => controller.EnableIme(new nint(12345));

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void NoOpImeController_DisableIme_WithZeroHandle_ShouldNotThrow()
    {
        // Arrange
        var controller = new NoOpImeController();

        // Act
        var action = () => controller.DisableIme(nint.Zero);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void NoOpImeController_EnableIme_WithZeroHandle_ShouldNotThrow()
    {
        // Arrange
        var controller = new NoOpImeController();

        // Act
        var action = () => controller.EnableIme(nint.Zero);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void NoOpImeController_MultipleCallsShouldBeIdempotent()
    {
        // Arrange
        var controller = new NoOpImeController();
        var handle = new nint(12345);

        // Act
        controller.DisableIme(handle);
        controller.DisableIme(handle);
        controller.EnableIme(handle);
        controller.EnableIme(handle);

        // Assert - no exceptions means idempotent
        controller.IsSupported.Should().BeFalse();
    }

    #endregion

    #region WindowsImeController Tests (Windows-specific)

    [Fact]
    public void WindowsImeController_IsSupported_ShouldBeTrue()
    {
        // Arrange & Act
        var controller = CreateWindowsControllerSafely();
        if (controller == null) return; // Skip on non-Windows

        // Assert
#pragma warning disable CA1416 // Validate platform compatibility
        controller.IsSupported.Should().BeTrue();
#pragma warning restore CA1416
    }

    [Fact]
    public void WindowsImeController_DisableIme_WithZeroHandle_ShouldNotThrow()
    {
        // Arrange
        var controller = CreateWindowsControllerSafely();
        if (controller == null) return; // Skip on non-Windows

        // Act
#pragma warning disable CA1416 // Validate platform compatibility
        var action = () => controller.DisableIme(nint.Zero);
#pragma warning restore CA1416

        // Assert - should log warning but not throw
        action.Should().NotThrow();
    }

    [Fact]
    public void WindowsImeController_EnableIme_WithZeroHandle_ShouldNotThrow()
    {
        // Arrange
        var controller = CreateWindowsControllerSafely();
        if (controller == null) return; // Skip on non-Windows

        // Act
#pragma warning disable CA1416 // Validate platform compatibility
        var action = () => controller.EnableIme(nint.Zero);
#pragma warning restore CA1416

        // Assert - should log warning but not throw
        action.Should().NotThrow();
    }

    [Fact]
    public void WindowsImeController_DisableEnableSequence_ShouldNotThrow()
    {
        // Arrange
        var controller = CreateWindowsControllerSafely();
        if (controller == null) return; // Skip on non-Windows

        var fakeHandle = new nint(999999); // Fake window handle

        // Act & Assert - should handle gracefully even with invalid handle
#pragma warning disable CA1416 // Validate platform compatibility
        var disableAction = () => controller.DisableIme(fakeHandle);
        var enableAction = () => controller.EnableIme(fakeHandle);
#pragma warning restore CA1416

        disableAction.Should().NotThrow();
        enableAction.Should().NotThrow();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void IImeController_Interface_ShouldBeImplementedByBothControllers()
    {
        // Arrange & Act
        var noOpController = new NoOpImeController();
        var windowsController = CreateWindowsControllerSafely();

        // Assert
        noOpController.Should().BeAssignableTo<IImeController>();
        
        if (windowsController != null)
        {
            windowsController.Should().BeAssignableTo<IImeController>();
        }
    }

    [Fact]
    public void ImeControllers_ShouldHaveDifferentSupportStatus()
    {
        // Arrange
        var noOpController = new NoOpImeController();
        var windowsController = CreateWindowsControllerSafely();

        // Assert
        noOpController.IsSupported.Should().BeFalse();
        
#pragma warning disable CA1416 // Validate platform compatibility
        if (windowsController != null)
        {
            windowsController.IsSupported.Should().BeTrue();
        }
#pragma warning restore CA1416
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Create WindowsImeController safely (only on Windows platform)
    /// </summary>
    private static WindowsImeController? CreateWindowsControllerSafely()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            return new WindowsImeController();
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }

    #endregion
}

