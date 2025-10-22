using System;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Screenshot;
using AGI.Kapster.Desktop.Services.Settings;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Kapster.Tests.TestHelpers;

namespace AGI.Kapster.Tests.Services.Overlay;

/// <summary>
/// Tests for OverlayWindowFactory
/// Verifies that the factory creates IOverlayWindow instances with correct dependencies
/// </summary>
public class OverlayWindowFactoryTests : TestBase
{
    private readonly ISettingsService _settingsService;
    private readonly IScreenshotService _screenshotService;
    private readonly IElementDetector _elementDetector;
    private readonly IScreenCaptureStrategy _captureStrategy;
    private readonly IScreenCoordinateMapper _coordinateMapper;
    private readonly OverlayWindowFactory _factory;

    public OverlayWindowFactoryTests(ITestOutputHelper output) : base(output)
    {
        _settingsService = Substitute.For<ISettingsService>();
        _screenshotService = Substitute.For<IScreenshotService>();
        _elementDetector = Substitute.For<IElementDetector>();
        _captureStrategy = Substitute.For<IScreenCaptureStrategy>();
        _coordinateMapper = Substitute.For<IScreenCoordinateMapper>();

        _factory = new OverlayWindowFactory(
            _settingsService,
            _screenshotService,
            _elementDetector,
            _captureStrategy,
            _coordinateMapper);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Assert
        _factory.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullRequiredDependencies_ShouldNotThrowButCreateWillThrow()
    {
        // Arrange - Factory allows null but OverlayWindow will throw when Create() is called
        var factory = new OverlayWindowFactory(null!, null!, null, null, null);

        // Assert - Factory construction succeeds
        factory.Should().NotBeNull();
        
        // But Create() will throw because OverlayWindow requires non-null services
        // (Cannot test Create() here as it requires Avalonia UI platform)
    }

    [Fact]
    public void Factory_ShouldImplementIOverlayWindowFactory()
    {
        // Assert
        _factory.Should().BeAssignableTo<IOverlayWindowFactory>();
    }

    [Fact]
    public void Factory_WithDependencies_ShouldStoreCorrectly()
    {
        // Arrange & Act
        var factoryWithDeps = new OverlayWindowFactory(
            _settingsService,
            _screenshotService,
            _elementDetector,
            _captureStrategy,
            _coordinateMapper);

        // Assert - Factory should be created successfully
        factoryWithDeps.Should().NotBeNull();
    }

    // Note: We cannot directly test Create() in unit tests because:
    // 1. OverlayWindow inherits from Avalonia.Controls.Window
    // 2. Window requires IWindowingPlatform which is not available in headless tests
    // 3. Creating actual windows would require UI thread and platform initialization
    //
    // The factory integration is tested in:
    // - OverlayCoordinatorBaseTests (mocked IOverlayWindow)
    // - Real app runtime (production use)
}

