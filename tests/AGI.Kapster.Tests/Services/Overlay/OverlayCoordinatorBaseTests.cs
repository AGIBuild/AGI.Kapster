using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Overlay.State;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Kapster.Tests.TestHelpers;

namespace AGI.Kapster.Tests.Services.Overlay;

/// <summary>
/// Tests for OverlayCoordinatorBase shared logic
/// Uses a concrete test implementation to verify abstract base class behavior
/// </summary>
public class OverlayCoordinatorBaseTests : TestBase
{
    private readonly IOverlaySessionFactory _sessionFactory;
    private readonly IOverlayWindowFactory _windowFactory;
    private readonly IScreenCoordinateMapper _coordinateMapper;
    private readonly IScreenCaptureStrategy _captureStrategy;
    private readonly IClipboardStrategy _clipboardStrategy;
    private readonly TestOverlayCoordinator _coordinator;

    public OverlayCoordinatorBaseTests(ITestOutputHelper output) : base(output)
    {
        _sessionFactory = Substitute.For<IOverlaySessionFactory>();
        _windowFactory = Substitute.For<IOverlayWindowFactory>();
        _coordinateMapper = Substitute.For<IScreenCoordinateMapper>();
        _captureStrategy = Substitute.For<IScreenCaptureStrategy>();
        _clipboardStrategy = Substitute.For<IClipboardStrategy>();

        _coordinator = new TestOverlayCoordinator(
            _sessionFactory,
            _windowFactory,
            _coordinateMapper,
            _captureStrategy,
            _clipboardStrategy);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Assert
        _coordinator.Should().NotBeNull();
        _coordinator.HasActiveSession.Should().BeFalse();
    }

    [Fact]
    public async Task StartSessionAsync_ShouldCreateAndShowSession()
    {
        // Arrange
        var mockSession = Substitute.For<IOverlaySession>();
        var mockScreens = CreateMockScreens(1);

        _sessionFactory.CreateSession().Returns(mockSession);
        _coordinator.SetMockScreens(mockScreens);

        // Act
        var result = await _coordinator.StartSessionAsync();

        // Assert
        result.Should().NotBeNull();
        _sessionFactory.Received(1).CreateSession();
        mockSession.Received(1).ShowAll();
        _coordinator.HasActiveSession.Should().BeTrue();
    }

    [Fact]
    public void CloseCurrentSession_WithNoSession_ShouldNotThrow()
    {
        // Act
        var action = () => _coordinator.CloseCurrentSession();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public async Task CloseCurrentSession_WithActiveSession_ShouldDisposeSession()
    {
        // Arrange
        var mockSession = Substitute.For<IOverlaySession>();
        var mockScreens = CreateMockScreens(1);

        _sessionFactory.CreateSession().Returns(mockSession);
        _coordinator.SetMockScreens(mockScreens);
        mockSession.Windows.Returns(new List<Avalonia.Controls.Window>());

        await _coordinator.StartSessionAsync();

        // Act
        _coordinator.CloseCurrentSession();

        // Assert
        mockSession.Received(1).Dispose();
        _coordinator.HasActiveSession.Should().BeFalse();
    }

    [Fact]
    public void CalculateVirtualDesktopBounds_WithSingleScreen_ShouldReturnScreenBounds()
    {
        // Arrange
        var screens = CreateMockScreens(1);

        // Act
        var result = TestOverlayCoordinator.TestCalculateVirtualDesktopBounds(screens);

        // Assert
        result.X.Should().Be(0);
        result.Y.Should().Be(0);
        result.Width.Should().Be(1920);
        result.Height.Should().Be(1080);
    }

    [Fact]
    public void CalculateVirtualDesktopBounds_WithMultipleScreens_ShouldReturnBoundingBox()
    {
        // Arrange
        var screen1 = CreateMockScreen(0, 0, 1920, 1080);
        var screen2 = CreateMockScreen(1920, 0, 1920, 1080);
        var screens = new List<Screen> { screen1, screen2 };

        // Act
        var result = TestOverlayCoordinator.TestCalculateVirtualDesktopBounds(screens);

        // Assert
        result.X.Should().Be(0);
        result.Y.Should().Be(0);
        result.Width.Should().Be(3840);
        result.Height.Should().Be(1080);
    }

    [Fact]
    public void CalculateVirtualDesktopBounds_WithNegativeCoordinates_ShouldHandleCorrectly()
    {
        // Arrange
        var screen1 = CreateMockScreen(-1920, 0, 1920, 1080); // Left monitor
        var screen2 = CreateMockScreen(0, 0, 1920, 1080);     // Primary monitor
        var screens = new List<Screen> { screen1, screen2 };

        // Act
        var result = TestOverlayCoordinator.TestCalculateVirtualDesktopBounds(screens);

        // Assert
        result.X.Should().Be(-1920);
        result.Y.Should().Be(0);
        result.Width.Should().Be(3840);
        result.Height.Should().Be(1080);
    }

    [Fact]
    public void CalculateVirtualDesktopBounds_WithEmptyScreens_ShouldReturnDefault()
    {
        // Arrange
        var screens = new List<Screen>();

        // Act
        var result = TestOverlayCoordinator.TestCalculateVirtualDesktopBounds(screens);

        // Assert
        result.Width.Should().Be(1920);
        result.Height.Should().Be(1080);
    }

    // Helper methods

    private IReadOnlyList<Screen> CreateMockScreens(int count)
    {
        var screens = new List<Screen>();
        for (int i = 0; i < count; i++)
        {
            screens.Add(CreateMockScreen(i * 1920, 0, 1920, 1080));
        }
        return screens;
    }

    private Screen CreateMockScreen(int x, int y, int width, int height)
    {
        var pixelRect = new PixelRect(x, y, width, height);
        return new Screen(1.0, pixelRect, pixelRect, true);
    }

    /// <summary>
    /// Test implementation of OverlayCoordinatorBase for testing shared logic
    /// </summary>
    private class TestOverlayCoordinator : OverlayCoordinatorBase
    {
        private IReadOnlyList<Screen>? _mockScreens;

        protected override string PlatformName => "TestCoordinator";

        public TestOverlayCoordinator(
            IOverlaySessionFactory sessionFactory,
            IOverlayWindowFactory windowFactory,
            IScreenCoordinateMapper coordinateMapper,
            IScreenCaptureStrategy? captureStrategy,
            IClipboardStrategy? clipboardStrategy)
            : base(sessionFactory, windowFactory, coordinateMapper, captureStrategy, clipboardStrategy)
        {
        }

        public void SetMockScreens(IReadOnlyList<Screen> screens)
        {
            _mockScreens = screens;
        }

        protected override IEnumerable<Rect> CalculateTargetRegions(IReadOnlyList<Screen> screens)
        {
            // Simple test implementation: return first screen bounds
            if (screens.Count > 0)
            {
                var screen = screens[0];
                yield return new Rect(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height);
            }
        }

        protected override Task CreateAndConfigureWindowsAsync(
            IOverlaySession session,
            IReadOnlyList<Screen> screens,
            IEnumerable<Rect> targetRegions)
        {
            // Simple test implementation: don't create actual UI windows in tests
            // Just verify the session is set up correctly
            return Task.CompletedTask;
        }

        // Override GetScreensAsync to return mock screens for testing
        protected new async Task<IReadOnlyList<Screen>> GetScreensAsync()
        {
            if (_mockScreens != null)
                return await Task.FromResult(_mockScreens);
            return await base.GetScreensAsync();
        }

        // Expose static method for testing
        public static Rect TestCalculateVirtualDesktopBounds(IReadOnlyList<Screen> screens)
        {
            return CalculateVirtualDesktopBounds(screens);
        }
    }
}

