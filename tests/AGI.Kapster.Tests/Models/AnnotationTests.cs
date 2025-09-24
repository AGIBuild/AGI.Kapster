using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Tests.TestHelpers;

namespace AGI.Kapster.Tests.Models;

public class AnnotationTests : TestBase
{
    public AnnotationTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void RectangleAnnotation_ShouldInitializeCorrectly()
    {
        // Arrange
        var position = new Avalonia.Point(10, 20);
        var size = new Avalonia.Size(100, 50);
        var style = new AnnotationStyle();

        // Act
        var annotation = new RectangleAnnotation(position, new Avalonia.Point(position.X + size.Width, position.Y + size.Height), style);

        // Assert
        annotation.Should().NotBeNull();
        annotation.Rectangle.TopLeft.Should().Be(position); // Use Rectangle.TopLeft instead of Bounds.Position
        annotation.Rectangle.Should().Be(new Avalonia.Rect(position, size));
        annotation.Style.Should().Be(style);
        annotation.Type.Should().Be(AnnotationType.Rectangle);
    }

    [Fact]
    public void EllipseAnnotation_ShouldInitializeCorrectly()
    {
        // Arrange
        var position = new Avalonia.Point(15, 25);
        var size = new Avalonia.Size(80, 60);
        var style = new AnnotationStyle();

        // Act
        var annotation = new EllipseAnnotation(new Avalonia.Rect(position, size), style);

        // Assert
        annotation.Should().NotBeNull();
        annotation.BoundingRect.Should().Be(new Avalonia.Rect(position, size));
        annotation.Style.Should().Be(style);
        annotation.Type.Should().Be(AnnotationType.Ellipse);
    }

    [Fact]
    public void ArrowAnnotation_ShouldInitializeCorrectly()
    {
        // Arrange
        var startPoint = new Avalonia.Point(0, 0);
        var endPoint = new Avalonia.Point(100, 100);
        var style = new AnnotationStyle();

        // Act
        var annotation = new ArrowAnnotation(startPoint, endPoint, style);

        // Assert
        annotation.Should().NotBeNull();
        annotation.StartPoint.Should().Be(startPoint);
        annotation.EndPoint.Should().Be(endPoint);
        annotation.Style.Should().Be(style);
        annotation.Type.Should().Be(AnnotationType.Arrow);
    }

    [Fact]
    public void TextAnnotation_ShouldInitializeCorrectly()
    {
        // Arrange
        var position = new Avalonia.Point(30, 40);
        var text = "Test Text";
        var style = new AnnotationStyle();

        // Act
        var annotation = new TextAnnotation(position, text, style);

        // Assert
        annotation.Should().NotBeNull();
        annotation.Bounds.Position.Should().Be(position);
        annotation.Text.Should().Be(text);
        annotation.Style.Should().Be(style);
        annotation.Type.Should().Be(AnnotationType.Text);
    }

    [Fact]
    public void FreehandAnnotation_ShouldInitializeCorrectly()
    {
        // Arrange
        var style = new AnnotationStyle();

        // Act
        var annotation = new FreehandAnnotation(style);

        // Assert
        annotation.Should().NotBeNull();
        annotation.Style.Should().Be(style);
        annotation.Type.Should().Be(AnnotationType.Freehand);
        annotation.Points.Should().NotBeNull();
        annotation.Points.Should().BeEmpty();
    }

    [Fact]
    public void EmojiAnnotation_ShouldInitializeCorrectly()
    {
        // Arrange
        var position = new Avalonia.Point(50, 60);
        var emoji = "😀";
        var style = new AnnotationStyle();

        // Act
        var annotation = new EmojiAnnotation(position, emoji, style);

        // Assert
        annotation.Should().NotBeNull();
        annotation.Position.Should().Be(position); // Use Position property instead of Bounds.Position
        annotation.Emoji.Should().Be(emoji);
        annotation.Style.Should().Be(style);
        annotation.Type.Should().Be(AnnotationType.Emoji);
    }

    [Fact]
    public void FreehandAnnotation_AddPoint_ShouldAddPointToList()
    {
        // Arrange
        var annotation = new FreehandAnnotation(new AnnotationStyle());
        var point = new Avalonia.Point(10, 20);

        // Act
        annotation.AddPoint(point);

        // Assert
        annotation.Points.Should().HaveCount(1);
        annotation.Points.First().Should().Be(point);
    }

    [Fact]
    public void FreehandAnnotation_AddMultiplePoints_ShouldAddAllPoints()
    {
        // Arrange
        var annotation = new FreehandAnnotation(new AnnotationStyle());
        var points = new[]
        {
            new Avalonia.Point(10, 20),
            new Avalonia.Point(30, 40),
            new Avalonia.Point(50, 60)
        };

        // Act
        foreach (var point in points)
        {
            annotation.AddPoint(point);
        }

        // Assert
        annotation.Points.Should().HaveCount(3);
        annotation.Points.Should().BeEquivalentTo(points);
    }

    [Fact]
    public void AnnotationStyle_ShouldHaveDefaultValues()
    {
        // Act
        var style = new AnnotationStyle();

        // Assert
        style.Should().NotBeNull();
        style.StrokeColor.Should().NotBe(default);
        style.FillColor.Should().NotBe(default);
        style.StrokeWidth.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AnnotationStyle_ShouldAllowCustomization()
    {
        // Arrange
        var strokeColor = Avalonia.Media.Colors.Red;
        var fillColor = Avalonia.Media.Colors.Blue;
        var strokeWidth = 3.0f;

        // Act
        var style = new AnnotationStyle
        {
            StrokeColor = strokeColor,
            FillColor = fillColor,
            StrokeWidth = strokeWidth
        };

        // Assert
        style.StrokeColor.Should().Be(strokeColor);
        style.FillColor.Should().Be(fillColor);
        style.StrokeWidth.Should().Be(strokeWidth);
    }

    [Fact]
    public void AnnotationManager_ShouldInitializeCorrectly()
    {
        // Act
        var manager = new AnnotationManager();

        // Assert
        manager.Should().NotBeNull();
        manager.Items.Should().NotBeNull();
        manager.Items.Should().BeEmpty();
        manager.SelectedItems.Should().NotBeNull();
        manager.SelectedItems.Should().BeEmpty();
    }

    [Fact]
    public void AnnotationManager_AddItem_ShouldAddItemToList()
    {
        // Arrange
        var manager = new AnnotationManager();
        var annotation = new RectangleAnnotation(
            new Avalonia.Point(10, 20),
            new Avalonia.Point(110, 70),
            new AnnotationStyle());

        // Act
        manager.AddItem(annotation);

        // Assert
        manager.Items.Should().HaveCount(1);
        manager.Items.First().Should().Be(annotation);
    }

    [Fact]
    public void AnnotationManager_RemoveItem_ShouldRemoveItemFromList()
    {
        // Arrange
        var manager = new AnnotationManager();
        var annotation = new RectangleAnnotation(
            new Avalonia.Point(10, 20),
            new Avalonia.Point(110, 70),
            new AnnotationStyle());

        manager.AddItem(annotation);
        manager.Items.Should().HaveCount(1);

        // Act
        manager.RemoveItem(annotation);

        // Assert
        manager.Items.Should().BeEmpty();
    }

    [Fact]
    public void AnnotationManager_SelectItem_ShouldAddToSelectedItems()
    {
        // Arrange
        var manager = new AnnotationManager();
        var annotation = new RectangleAnnotation(
            new Avalonia.Point(10, 20),
            new Avalonia.Point(110, 70),
            new AnnotationStyle());

        manager.AddItem(annotation);

        // Act
        manager.SelectItem(annotation);

        // Assert
        manager.SelectedItems.Should().HaveCount(1);
        manager.SelectedItems.First().Should().Be(annotation);
    }

    [Fact]
    public void AnnotationManager_DeselectItem_ShouldRemoveFromSelectedItems()
    {
        // Arrange
        var manager = new AnnotationManager();
        var annotation = new RectangleAnnotation(
            new Avalonia.Point(10, 20),
            new Avalonia.Point(110, 70),
            new AnnotationStyle());

        manager.AddItem(annotation);
        manager.SelectItem(annotation);
        manager.SelectedItems.Should().HaveCount(1);

        // Act
        manager.DeselectItem(annotation);

        // Assert
        manager.SelectedItems.Should().BeEmpty();
    }

    [Fact]
    public void AnnotationManager_ClearSelection_ShouldClearAllSelectedItems()
    {
        // Arrange
        var manager = new AnnotationManager();
        var annotation1 = new RectangleAnnotation(
            new Avalonia.Point(10, 20),
            new Avalonia.Point(110, 70),
            new AnnotationStyle());
        var annotation2 = new EllipseAnnotation(
            new Avalonia.Rect(30, 40, 80, 60),
            new AnnotationStyle());

        manager.AddItem(annotation1);
        manager.AddItem(annotation2);
        manager.SelectItem(annotation1);
        manager.SelectItem(annotation2, addToSelection: true);
        manager.SelectedItems.Should().HaveCount(2);

        // Act
        manager.ClearSelection();

        // Assert
        manager.SelectedItems.Should().BeEmpty();
        annotation1.State.Should().Be(AnnotationState.Normal);
        annotation2.State.Should().Be(AnnotationState.Normal);
    }

    [Fact]
    public void AnnotationManager_Clear_ShouldRemoveAllItems()
    {
        // Arrange
        var manager = new AnnotationManager();
        var annotation1 = new RectangleAnnotation(
            new Avalonia.Point(10, 20),
            new Avalonia.Point(110, 70),
            new AnnotationStyle());
        var annotation2 = new EllipseAnnotation(
            new Avalonia.Rect(30, 40, 80, 60),
            new AnnotationStyle());

        manager.AddItem(annotation1);
        manager.AddItem(annotation2);
        manager.Items.Should().HaveCount(2);

        // Act
        manager.Clear();

        // Assert
        manager.Items.Should().BeEmpty();
        manager.SelectedItems.Should().BeEmpty();
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
