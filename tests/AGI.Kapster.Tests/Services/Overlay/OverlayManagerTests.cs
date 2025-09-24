using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Tests.TestHelpers;

namespace AGI.Kapster.Tests.Services.Overlay;

public class OverlayManagerTests : TestBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOverlayWindow _overlayWindow;
    private readonly IOverlayController _overlayManager;

    public OverlayManagerTests(ITestOutputHelper output) : base(output)
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
    public void ShowAll_ShouldCallOverlayWindowShow()
    {
        // Arrange
        _overlayWindow.IsVisible.Returns(false);

        // Act
        _overlayManager.ShowAll();

        // Assert
        _overlayWindow.Received(1).Show();
    }

    [Fact]
    public void CloseAll_ShouldNotThrow()
    {
        // Act & Assert
        var action = () => _overlayManager.CloseAll();
        action.Should().NotThrow();
    }

    [Fact]
    public void IsActive_WhenNoWindows_ShouldReturnFalse()
    {
        // Act
        var result = _overlayManager.IsActive;

        // Assert
        result.Should().BeFalse();
    }
}
