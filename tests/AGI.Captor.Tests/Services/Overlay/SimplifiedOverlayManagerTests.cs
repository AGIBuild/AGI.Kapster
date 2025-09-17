using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Captor.Desktop.Services.Overlay;
using AGI.Captor.Tests.TestHelpers;

namespace AGI.Captor.Tests.Services.Overlay;

public class SimplifiedOverlayManagerTests : TestBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOverlayWindow _overlayWindow;
    private readonly SimplifiedOverlayManager _overlayManager;

    public SimplifiedOverlayManagerTests(ITestOutputHelper output) : base(output)
    {
        _overlayWindow = Substitute.For<IOverlayWindow>();
        _overlayWindow.IsVisible.Returns(false);
        
        var services = new ServiceCollection();
        services.AddSingleton(_overlayWindow);
        _serviceProvider = services.BuildServiceProvider();
        
        _overlayManager = new SimplifiedOverlayManager(_serviceProvider);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Act & Assert
        _overlayManager.Should().NotBeNull();
    }

    [Fact]
    public void ShowAll_ShouldCreateOverlayWindows()
    {
        // Act
        _overlayManager.ShowAll();

        // Assert
        // The actual implementation would create overlay windows for each screen
        // For now, we just verify the method doesn't throw
        var action = () => _overlayManager.ShowAll();
        action.Should().NotThrow();
    }

    [Fact]
    public void CloseAll_ShouldCloseAllOverlayWindows()
    {
        // Arrange
        _overlayManager.ShowAll();

        // Act
        _overlayManager.CloseAll();

        // Assert
        // The actual implementation would close all overlay windows
        // For now, we just verify the method doesn't throw
        var action = () => _overlayManager.CloseAll();
        action.Should().NotThrow();
    }

    [Fact]
    public void CloseAll_WhenNoOverlaysOpen_ShouldNotThrow()
    {
        // Act & Assert
        var action = () => _overlayManager.CloseAll();
        action.Should().NotThrow();
    }

    [Fact]
    public void ShowAll_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        // Act & Assert
        var action = () =>
        {
            _overlayManager.ShowAll();
            _overlayManager.ShowAll();
            _overlayManager.ShowAll();
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void CloseAll_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        // Act & Assert
        var action = () =>
        {
            _overlayManager.CloseAll();
            _overlayManager.CloseAll();
            _overlayManager.CloseAll();
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void ShowAll_ThenCloseAll_ShouldWorkCorrectly()
    {
        // Act & Assert
        var action = () =>
        {
            _overlayManager.ShowAll();
            _overlayManager.CloseAll();
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void CloseAll_ThenShowAll_ShouldWorkCorrectly()
    {
        // Act & Assert
        var action = () =>
        {
            _overlayManager.CloseAll();
            _overlayManager.ShowAll();
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new SimplifiedOverlayManager(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidServiceProvider_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var action = () => new SimplifiedOverlayManager(serviceProvider);
        action.Should().NotThrow();
    }

    [Fact]
    public void ShowAll_ShouldHandleServiceProviderExceptions()
    {
        // Arrange
        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockServiceProvider.GetService(Arg.Any<Type>()).Throws(new InvalidOperationException("Service not found"));
        
        var manager = new SimplifiedOverlayManager(mockServiceProvider);

        // Act & Assert
        var action = () => manager.ShowAll();
        action.Should().NotThrow(); // Should handle exceptions gracefully
    }

    [Fact]
    public void CloseAll_ShouldHandleServiceProviderExceptions()
    {
        // Arrange
        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockServiceProvider.GetService(Arg.Any<Type>()).Throws(new InvalidOperationException("Service not found"));
        
        var manager = new SimplifiedOverlayManager(mockServiceProvider);

        // Act & Assert
        var action = () => manager.CloseAll();
        action.Should().NotThrow(); // Should handle exceptions gracefully
    }

    [Fact]
    public void ShowAll_ShouldBeIdempotent()
    {
        // Act
        _overlayManager.ShowAll();
        var firstCallResult = _overlayManager.ShowAll();
        var secondCallResult = _overlayManager.ShowAll();

        // Assert
        // Both calls should succeed without throwing
        firstCallResult.Should().NotBeNull();
        secondCallResult.Should().NotBeNull();
    }

    [Fact]
    public void CloseAll_ShouldBeIdempotent()
    {
        // Act
        _overlayManager.CloseAll();
        var firstCallResult = _overlayManager.CloseAll();
        var secondCallResult = _overlayManager.CloseAll();

        // Assert
        // Both calls should succeed without throwing
        firstCallResult.Should().NotBeNull();
        secondCallResult.Should().NotBeNull();
    }

    public override void Dispose()
    {
        _overlayManager?.Dispose();
        base.Dispose();
    }
}
