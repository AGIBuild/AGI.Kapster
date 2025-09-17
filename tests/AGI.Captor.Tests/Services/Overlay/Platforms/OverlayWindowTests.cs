using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Captor.Desktop.Services.Overlay;
using AGI.Captor.Desktop.Services.Overlay.Platforms;
using AGI.Captor.Tests.TestHelpers;

namespace AGI.Captor.Tests.Services.Overlay.Platforms;

public class OverlayWindowTests : TestBase
{
    public OverlayWindowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void NullElementDetector_ShouldHaveCorrectProperties()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act & Assert
        detector.IsSupported.Should().BeFalse();
        detector.HasPermissions.Should().BeFalse();
    }

    [Fact]
    public void NullElementDetector_DetectElement_ShouldReturnNull()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act
        var result = detector.DetectElement(0, 0);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NullElementDetector_DetectElement_WithDifferentCoordinates_ShouldReturnNull()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act & Assert
        detector.DetectElement(100, 200).Should().BeNull();
        detector.DetectElement(-10, -20).Should().BeNull();
        detector.DetectElement(0, 0).Should().BeNull();
    }

    [Fact]
    public void NullElementDetector_DetectionModeChanged_ShouldNotThrow()
    {
        // Arrange
        var detector = new NullElementDetector();
        EventHandler<ElementDetectionMode>? handler = null;

        // Act & Assert
        var action = () =>
        {
            detector.DetectionModeChanged += (sender, args) => handler = args;
            detector.DetectionModeChanged -= (sender, args) => handler = args;
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void NullElementDetector_DetectionModeChanged_ShouldAllowMultipleSubscriptions()
    {
        // Arrange
        var detector = new NullElementDetector();
        var handler1 = new EventHandler<ElementDetectionMode>((s, e) => { });
        var handler2 = new EventHandler<ElementDetectionMode>((s, e) => { });

        // Act & Assert
        var action = () =>
        {
            detector.DetectionModeChanged += handler1;
            detector.DetectionModeChanged += handler2;
            detector.DetectionModeChanged -= handler1;
            detector.DetectionModeChanged -= handler2;
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void NullElementDetector_ShouldImplementIElementDetector()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act & Assert
        detector.Should().BeAssignableTo<IElementDetector>();
    }

    [Fact]
    public void NullElementDetector_ShouldImplementIDisposable()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act & Assert
        detector.Should().BeAssignableTo<IDisposable>();
    }

    [Fact]
    public void NullElementDetector_Dispose_ShouldNotThrow()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act & Assert
        var action = () => detector.Dispose();
        action.Should().NotThrow();
    }

    [Fact]
    public void NullElementDetector_MultipleDispose_ShouldNotThrow()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act & Assert
        var action = () =>
        {
            detector.Dispose();
            detector.Dispose();
            detector.Dispose();
        };
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 200)]
    [InlineData(-10, -20)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(int.MinValue, int.MinValue)]
    public void NullElementDetector_DetectElement_WithVariousCoordinates_ShouldReturnNull(int x, int y)
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act
        var result = detector.DetectElement(x, y);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NullElementDetector_DetectionModeChanged_ShouldBeNullSafe()
    {
        // Arrange
        var detector = new NullElementDetector();

        // Act & Assert
        var action = () =>
        {
            detector.DetectionModeChanged += null!;
            detector.DetectionModeChanged -= null!;
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void NullElementDetector_ShouldBeThreadSafe()
    {
        // Arrange
        var detector = new NullElementDetector();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                detector.DetectElement(i, i);
                detector.DetectionModeChanged += (s, e) => { };
                detector.DetectionModeChanged -= (s, e) => { };
            }));
        }

        // Assert
        var action = () => Task.WaitAll(tasks.ToArray());
        action.Should().NotThrow();
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
