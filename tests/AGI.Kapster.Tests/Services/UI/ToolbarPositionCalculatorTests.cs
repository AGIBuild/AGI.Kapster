using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.UI;
using Avalonia;
using Avalonia.Platform;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Kapster.Tests.TestHelpers;

namespace AGI.Kapster.Tests.Services.UI;

/// <summary>
/// Tests for ToolbarPositionCalculator service
/// Verifies toolbar positioning logic for multi-screen scenarios
/// </summary>
public class ToolbarPositionCalculatorTests : TestBase
{
    private readonly IScreenCoordinateMapper _coordinateMapper;
    private readonly ToolbarPositionCalculator _calculator;

    public ToolbarPositionCalculatorTests(ITestOutputHelper output) : base(output)
    {
        _coordinateMapper = Substitute.For<IScreenCoordinateMapper>();
        _calculator = new ToolbarPositionCalculator(_coordinateMapper);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullMapper_ShouldNotThrow()
    {
        // Act
        var action = () => new ToolbarPositionCalculator(null);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithMapper_ShouldInitialize()
    {
        // Assert
        _calculator.Should().NotBeNull();
    }

    #endregion

    #region Single Screen Tests

    [Fact]
    public void CalculatePosition_WithSpaceBelow_ShouldPositionBelowSelection()
    {
        // Arrange - toolbar fits below selection (using fallback bounds)
        var selection = new Rect(100, 100, 400, 300);
        var toolbarSize = new Size(200, 50);
        var overlayPos = new PixelPoint(0, 0);
        IReadOnlyList<Screen>? screens = null; // Use fallback

        var context = new ToolbarPositionContext(selection, toolbarSize, overlayPos, screens);

        // Act
        var position = _calculator.CalculatePosition(context);

        // Assert - should be below selection, right-aligned
        position.X.Should().Be(selection.Right - toolbarSize.Width); // Right-aligned
        position.Y.Should().BeGreaterThan(selection.Bottom); // Below selection
    }

    [Fact]
    public void CalculatePosition_WithoutSpaceBelow_ShouldPositionCorrectly()
    {
        // Arrange - selection near bottom (using fallback bounds)
        var selection = new Rect(100, 300, 400, 100);
        var toolbarSize = new Size(200, 50);
        var overlayPos = new PixelPoint(0, 0);
        IReadOnlyList<Screen>? screens = null; // Use fallback

        var context = new ToolbarPositionContext(selection, toolbarSize, overlayPos, screens);

        // Act
        var position = _calculator.CalculatePosition(context);

        // Assert - toolbar should be positioned (either above, below, or inside)
        position.X.Should().BeInRange(selection.Left - 50, selection.Right + 50); // Near selection horizontally
        position.Y.Should().BeGreaterThan(0); // Valid Y position
    }

    [Fact]
    public void CalculatePosition_WithoutSpaceBelowOrAbove_ShouldPositionInsideSelection()
    {
        // Arrange - full screen selection, no external space (using fallback bounds)
        var selection = new Rect(0, 0, 1920, 1080);
        var toolbarSize = new Size(200, 50);
        var overlayPos = new PixelPoint(0, 0);
        IReadOnlyList<Screen>? screens = null; // Use fallback

        var context = new ToolbarPositionContext(selection, toolbarSize, overlayPos, screens);

        // Act
        var position = _calculator.CalculatePosition(context);

        // Assert - should be inside selection at bottom-right
        position.X.Should().BeGreaterThan(selection.Right - toolbarSize.Width - 20); // Inside, near right
        position.Y.Should().BeGreaterThan(selection.Bottom - toolbarSize.Height - 20); // Inside, near bottom
    }

    [Fact]
    public void CalculatePosition_ShouldAlwaysRightAlignToolbar()
    {
        // Arrange (using fallback bounds)
        var selection = new Rect(500, 200, 800, 400);
        var toolbarSize = new Size(300, 60);
        var overlayPos = new PixelPoint(0, 0);
        IReadOnlyList<Screen>? screens = null; // Use fallback

        var context = new ToolbarPositionContext(selection, toolbarSize, overlayPos, screens);

        // Act
        var position = _calculator.CalculatePosition(context);

        // Assert - toolbar should align to selection's right edge
        var expectedX = selection.Right - toolbarSize.Width;
        position.X.Should().BeInRange(expectedX - 20, expectedX + 20); // Allow margin adjustment
    }

    #endregion

    #region Multi-Screen Tests

    [Fact]
    public void CalculatePosition_WithMultipleScreens_ShouldNotThrow()
    {
        // Arrange - test that multi-screen scenario doesn't throw
        // Note: Real Screen objects cannot be easily mocked, testing fallback behavior
        var selection = new Rect(2000, 100, 400, 300);
        var toolbarSize = new Size(200, 50);
        var overlayPos = new PixelPoint(0, 0);
        IReadOnlyList<Screen>? screens = null; // Use fallback

        var context = new ToolbarPositionContext(selection, toolbarSize, overlayPos, screens);

        // Act & Assert
        var action = () => _calculator.CalculatePosition(context);
        action.Should().NotThrow();
        
        var position = _calculator.CalculatePosition(context);
        position.Should().NotBe(default(Point));
    }

    [Fact]
    public void CalculatePosition_WithNoScreenInfo_ShouldUseFallbackBounds()
    {
        // Arrange - no screen information
        var selection = new Rect(100, 100, 400, 300);
        var toolbarSize = new Size(200, 50);
        var overlayPos = new PixelPoint(0, 0);
        IReadOnlyList<Screen>? screens = null;

        var context = new ToolbarPositionContext(selection, toolbarSize, overlayPos, screens);

        // Act
        var position = _calculator.CalculatePosition(context);

        // Assert - should not throw and return valid position
        position.X.Should().BeGreaterThanOrEqualTo(0);
        position.Y.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CalculatePosition_WithZeroSizeToolbar_ShouldHandleGracefully()
    {
        // Arrange (using fallback bounds)
        var selection = new Rect(100, 100, 400, 300);
        var toolbarSize = new Size(0, 0);
        var overlayPos = new PixelPoint(0, 0);
        IReadOnlyList<Screen>? screens = null; // Use fallback

        var context = new ToolbarPositionContext(selection, toolbarSize, overlayPos, screens);

        // Act
        var action = () => _calculator.CalculatePosition(context);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void CalculatePosition_WithLargeToolbar_ShouldHandleGracefully()
    {
        // Arrange - toolbar larger than usual (using fallback bounds)
        var selection = new Rect(100, 100, 400, 300);
        var toolbarSize = new Size(600, 100); // Large but not extreme
        var overlayPos = new PixelPoint(0, 0);
        IReadOnlyList<Screen>? screens = null; // Use fallback

        var context = new ToolbarPositionContext(selection, toolbarSize, overlayPos, screens);

        // Act & Assert - should handle large toolbars gracefully
        var action = () => _calculator.CalculatePosition(context);
        action.Should().NotThrow();
        
        var position = _calculator.CalculatePosition(context);
        position.Should().NotBe(default(Point));
    }

    [Fact]
    public void CalculatePosition_WithNegativeOverlayPosition_ShouldHandleCorrectly()
    {
        // Arrange - overlay at negative position (multi-screen scenario, using fallback bounds)
        var selection = new Rect(100, 100, 400, 300);
        var toolbarSize = new Size(200, 50);
        var overlayPos = new PixelPoint(-1920, -500);
        IReadOnlyList<Screen>? screens = null; // Use fallback

        var context = new ToolbarPositionContext(selection, toolbarSize, overlayPos, screens);

        // Act
        var action = () => _calculator.CalculatePosition(context);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Note: Screen cannot be easily mocked due to readonly properties
    /// Tests with null screens verify fallback behavior
    /// Integration tests with real screens should be done at higher level
    /// </summary>
    private static IReadOnlyList<Screen>? CreateMockScreens()
    {
        // Return null to test fallback behavior
        // Real Screen objects cannot be easily created in unit tests
        return null;
    }

    #endregion
}

