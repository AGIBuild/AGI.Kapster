using System.Threading.Tasks;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Overlay.State;
using AGI.Kapster.Desktop.Services.Screenshot;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Kapster.Tests.TestHelpers;

namespace AGI.Kapster.Tests.Services.Screenshot;

/// <summary>
/// Tests for ScreenshotService (high-level screenshot API)
/// </summary>
public class ScreenshotServiceTests : TestBase
{
    private readonly IOverlayCoordinator _coordinator;
    private readonly ScreenshotService _service;

    public ScreenshotServiceTests(ITestOutputHelper output) : base(output)
    {
        _coordinator = Substitute.For<IOverlayCoordinator>();
        _service = new ScreenshotService(_coordinator);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Assert
        _service.Should().NotBeNull();
    }

    [Fact]
    public void IsActive_WhenNoSession_ShouldReturnFalse()
    {
        // Arrange
        _coordinator.HasActiveSession.Returns(false);

        // Act
        var result = _service.IsActive;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsActive_WithActiveSession_ShouldReturnTrue()
    {
        // Arrange
        _coordinator.HasActiveSession.Returns(true);

        // Act
        var result = _service.IsActive;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TakeScreenshotAsync_ShouldCallCoordinator()
    {
        // Arrange
        var mockSession = Substitute.For<IOverlaySession>();
        _coordinator.StartSessionAsync().Returns(mockSession);

        // Act
        await _service.TakeScreenshotAsync();

        // Assert
        await _coordinator.Received(1).StartSessionAsync();
    }

    [Fact]
    public void Cancel_ShouldCloseCurrentSession()
    {
        // Act
        _service.Cancel();

        // Assert
        _coordinator.Received(1).CloseCurrentSession();
    }

    [Fact]
    public void Cancel_WithNoActiveSession_ShouldNotThrow()
    {
        // Arrange
        _coordinator.HasActiveSession.Returns(false);

        // Act
        var action = () => _service.Cancel();

        // Assert
        action.Should().NotThrow();
    }
}

