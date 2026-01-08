using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Overlay.State;
using AGI.Kapster.Tests.TestHelpers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using FluentAssertions;
using Xunit;

namespace AGI.Kapster.Tests.Services.Overlay;

/// <summary>
/// Tests for OverlaySession selection locking behavior
/// Ensures only one selection can exist across multiple screens at any time
/// </summary>
[Collection("Avalonia")]
public class OverlaySessionSelectionTests : IClassFixture<AvaloniaFixture>
{
    [Fact]
    public void Session_WhenRegionSelectedWithEditable_ShouldLockOtherWindows()
    {
        // Arrange
        var session = new OverlaySession();
        var window1 = new MockOverlayWindow();
        var window2 = new MockOverlayWindow();
        var window3 = new MockOverlayWindow();

        session.AddWindow(window1.AsWindow());
        session.AddWindow(window2.AsWindow());
        session.AddWindow(window3.AsWindow());

        // Act - Window 1 finishes selection (editable)
        window1.SimulateRegionSelected(new Rect(0, 0, 100, 100), isEditableSelection: true);

        // Assert - Other windows should be locked
        window1.IsLocked.Should().BeFalse("source window should not be locked");
        window2.IsLocked.Should().BeTrue("other windows should be locked");
        window3.IsLocked.Should().BeTrue("other windows should be locked");
    }

    [Fact]
    public void Session_WhenRegionSelectedNotEditable_ShouldNotLockWindows()
    {
        // Arrange
        var session = new OverlaySession();
        var window1 = new MockOverlayWindow();
        var window2 = new MockOverlayWindow();

        session.AddWindow(window1.AsWindow());
        session.AddWindow(window2.AsWindow());

        // Act - Window 1 confirms selection (not editable - final capture)
        window1.SimulateRegionSelected(new Rect(0, 0, 100, 100), isEditableSelection: false);

        // Assert - No windows should be locked (session will close soon anyway)
        window1.IsLocked.Should().BeFalse();
        window2.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void Session_WithSingleWindow_SelectionShouldNotLock()
    {
        // Arrange
        var session = new OverlaySession();
        var window1 = new MockOverlayWindow();

        session.AddWindow(window1.AsWindow());

        // Act
        window1.SimulateRegionSelected(new Rect(0, 0, 100, 100), isEditableSelection: true);

        // Assert - Single window should not be locked
        window1.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void Session_AddWindow_ShouldSubscribeToEvents()
    {
        // Arrange
        var session = new OverlaySession();
        var window = new MockOverlayWindow();
        var eventRaised = false;

        session.RegionSelected += _ => eventRaised = true;

        // Act
        session.AddWindow(window.AsWindow());
        window.SimulateRegionSelected(new Rect(0, 0, 100, 100), isEditableSelection: true);

        // Assert
        eventRaised.Should().BeTrue("session should forward window events");
    }

    [Fact]
    public void Session_Dispose_ShouldUnsubscribeFromEvents()
    {
        // Arrange
        var session = new OverlaySession();
        var window = new MockOverlayWindow();
        var eventRaised = false;

        session.RegionSelected += _ => eventRaised = true;
        session.AddWindow(window.AsWindow());
        session.Dispose();

        // Act
        window.SimulateRegionSelected(new Rect(0, 0, 100, 100), isEditableSelection: true);

        // Assert
        eventRaised.Should().BeFalse("session should unsubscribe on dispose");
    }

    /// <summary>
    /// Mock IOverlayWindow for testing selection lock behavior
    /// </summary>
    private class MockOverlayWindow : Window, IOverlayWindow
    {
        public bool IsLocked { get; private set; }
        public bool ElementDetectionEnabled { get; set; }
        public bool HasSelection { get; private set; }

        public event EventHandler<RegionSelectedEventArgs>? RegionSelected;
        public event EventHandler<OverlayCancelledEventArgs>? Cancelled;

        public void SetPrecapturedAvaloniaBitmap(Bitmap? bitmap) { }
        public void SetMaskSize(double width, double height) { }
        public void SetScreens(IReadOnlyList<Avalonia.Platform.Screen>? screens) { }

        public void SetSelectionLocked(bool locked)
        {
            IsLocked = locked;
        }

        public Window AsWindow() => this;

        /// <summary>
        /// Simulate user completing a selection
        /// </summary>
        public void SimulateRegionSelected(Rect rect, bool isEditableSelection)
        {
            HasSelection = true;
            RegionSelected?.Invoke(this, new RegionSelectedEventArgs(
                rect,
                isConfirmed: !isEditableSelection,
                detectedElement: null,
                isEditableSelection: isEditableSelection));
        }

        /// <summary>
        /// Simulate user cancelling
        /// </summary>
        public void SimulateCancelled()
        {
            Cancelled?.Invoke(this, new OverlayCancelledEventArgs("User cancelled"));
        }
    }
}
