using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Captor.Desktop.Models;
using AGI.Captor.Desktop.Services;
using AGI.Captor.Tests.TestHelpers;

namespace AGI.Captor.Tests.Services;

public class AnnotationServiceTests : TestBase
{
    private readonly ISettingsService _settingsService;
    private readonly AnnotationService _annotationService;

    public AnnotationServiceTests(ITestOutputHelper output) : base(output)
    {
        _settingsService = Substitute.For<ISettingsService>();
        _annotationService = new AnnotationService(_settingsService);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act & Assert
        _annotationService.CurrentTool.Should().Be(AnnotationToolType.None);
        _annotationService.CurrentStyle.Should().NotBeNull();
    }

    [Fact]
    public void SetTool_ShouldUpdateCurrentTool()
    {
        // Arrange
        var tool = AnnotationToolType.Rectangle;

        // Act
        _annotationService.SetTool(tool);

        // Assert
        _annotationService.CurrentTool.Should().Be(tool);
    }

    [Fact]
    public void SetTool_ShouldRaiseToolChangedEvent()
    {
        // Arrange
        var tool = AnnotationToolType.Ellipse;
        AnnotationToolType? eventArgs = null;
        _annotationService.ToolChanged += (sender, args) => eventArgs = args;

        // Act
        _annotationService.SetTool(tool);

        // Assert
        eventArgs.Should().Be(tool);
    }

    [Theory]
    [InlineData(AnnotationToolType.Rectangle)]
    [InlineData(AnnotationToolType.Ellipse)]
    [InlineData(AnnotationToolType.Arrow)]
    [InlineData(AnnotationToolType.Text)]
    [InlineData(AnnotationToolType.Freehand)]
    [InlineData(AnnotationToolType.Emoji)]
    public void SetTool_ShouldAcceptAllValidTools(AnnotationToolType tool)
    {
        // Act
        _annotationService.SetTool(tool);

        // Assert
        _annotationService.CurrentTool.Should().Be(tool);
    }

    [Fact]
    public void StartAnnotation_WithRectangleTool_ShouldCreateRectangleAnnotation()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Rectangle);
        var startPoint = new Avalonia.Point(10, 20);

        // Act
        _annotationService.StartAnnotation(startPoint);

        // Assert
        var annotations = _annotationService.Manager.Items.ToList();
        annotations.Should().HaveCount(1);
        annotations[0].Should().BeOfType<RectangleAnnotation>();
    }

    [Fact]
    public void StartAnnotation_WithEllipseTool_ShouldCreateEllipseAnnotation()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Ellipse);
        var startPoint = new Avalonia.Point(15, 25);

        // Act
        _annotationService.StartAnnotation(startPoint);

        // Assert
        var annotations = _annotationService.Manager.Items.ToList();
        annotations.Should().HaveCount(1);
        annotations[0].Should().BeOfType<EllipseAnnotation>();
    }

    [Fact]
    public void StartAnnotation_WithArrowTool_ShouldCreateArrowAnnotation()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Arrow);
        var startPoint = new Avalonia.Point(5, 10);

        // Act
        _annotationService.StartAnnotation(startPoint);

        // Assert
        var annotations = _annotationService.Manager.Items.ToList();
        annotations.Should().HaveCount(1);
        annotations[0].Should().BeOfType<ArrowAnnotation>();
    }

    [Fact]
    public void StartAnnotation_WithTextTool_ShouldCreateTextAnnotation()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Text);
        var startPoint = new Avalonia.Point(30, 40);

        // Act
        _annotationService.StartAnnotation(startPoint);

        // Assert
        var annotations = _annotationService.Manager.Items.ToList();
        annotations.Should().HaveCount(1);
        annotations[0].Should().BeOfType<TextAnnotation>();
    }

    [Fact]
    public void StartAnnotation_WithFreehandTool_ShouldCreateFreehandAnnotation()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Freehand);
        var startPoint = new Avalonia.Point(50, 60);

        // Act
        _annotationService.StartAnnotation(startPoint);

        // Assert
        var annotations = _annotationService.Manager.Items.ToList();
        annotations.Should().HaveCount(1);
        annotations[0].Should().BeOfType<FreehandAnnotation>();
    }

    [Fact]
    public void StartAnnotation_WithEmojiTool_ShouldCreateEmojiAnnotation()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Emoji);
        var startPoint = new Avalonia.Point(70, 80);

        // Act
        _annotationService.StartAnnotation(startPoint);

        // Assert
        var annotations = _annotationService.Manager.Items.ToList();
        annotations.Should().HaveCount(1);
        annotations[0].Should().BeOfType<EmojiAnnotation>();
    }

    [Fact]
    public void UpdateAnnotation_WithRectangle_ShouldUpdateRectangleBounds()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Rectangle);
        var startPoint = new Avalonia.Point(10, 20);
        var endPoint = new Avalonia.Point(50, 60);
        
        _annotationService.StartAnnotation(startPoint);
        var annotation = _annotationService.Manager.Items.First() as RectangleAnnotation;

        // Act
        _annotationService.UpdateAnnotation(endPoint);

        // Assert
        annotation!.Rectangle.Should().Be(new Avalonia.Rect(10, 20, 40, 40));
    }

    [Fact]
    public void UpdateAnnotation_WithEllipse_ShouldUpdateEllipseBounds()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Ellipse);
        var startPoint = new Avalonia.Point(5, 10);
        var endPoint = new Avalonia.Point(25, 35);
        
        _annotationService.StartAnnotation(startPoint);
        var annotation = _annotationService.Manager.Items.First() as EllipseAnnotation;

        // Act
        _annotationService.UpdateAnnotation(endPoint);

        // Assert
        annotation!.BoundingRect.Should().Be(new Avalonia.Rect(5, 10, 20, 25));
    }

    [Fact]
    public void UpdateAnnotation_WithArrow_ShouldUpdateArrowEndPoint()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Arrow);
        var startPoint = new Avalonia.Point(0, 0);
        var endPoint = new Avalonia.Point(100, 100);
        
        _annotationService.StartAnnotation(startPoint);
        var annotation = _annotationService.Manager.Items.First() as ArrowAnnotation;

        // Act
        _annotationService.UpdateAnnotation(endPoint);

        // Assert
        annotation!.EndPoint.Should().Be(endPoint);
    }

    [Fact]
    public void UpdateAnnotation_WithFreehand_ShouldAddPointToFreehand()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Freehand);
        var startPoint = new Avalonia.Point(10, 20);
        var additionalPoint = new Avalonia.Point(30, 40);
        
        _annotationService.StartAnnotation(startPoint);
        var annotation = _annotationService.Manager.Items.First() as FreehandAnnotation;
        var initialPointCount = annotation!.Points.Count;

        // Act
        _annotationService.UpdateAnnotation(additionalPoint);

        // Assert
        annotation.Points.Should().HaveCount(initialPointCount + 1);
        annotation.Points.Last().Should().Be(additionalPoint);
    }

    [Fact]
    public void FinishAnnotation_ShouldCompleteCurrentAnnotation()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Rectangle);
        var startPoint = new Avalonia.Point(10, 20);
        _annotationService.StartAnnotation(startPoint);
        var annotation = _annotationService.Manager.Items.First();

        // Act
        _annotationService.FinishAnnotation();

        // Assert
        annotation.State.Should().Be(AnnotationState.Completed);
    }

    [Fact]
    public void CancelAnnotation_ShouldRemoveCurrentAnnotation()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Rectangle);
        var startPoint = new Avalonia.Point(10, 20);
        _annotationService.StartAnnotation(startPoint);
        
        var initialCount = _annotationService.Manager.Items.Count();
        initialCount.Should().Be(1);

        // Act
        _annotationService.CancelAnnotation();

        // Assert
        _annotationService.Manager.Items.Should().BeEmpty();
    }

    [Fact]
    public void HitTest_ShouldReturnAnnotationsInRegion()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Rectangle);
        _annotationService.StartAnnotation(new Avalonia.Point(10, 10));
        _annotationService.UpdateAnnotation(new Avalonia.Point(50, 50));
        _annotationService.FinishAnnotation();

        var testRegion = new Avalonia.Rect(20, 20, 20, 20);

        // Act
        var hitAnnotations = _annotationService.HitTest(testRegion);

        // Assert
        hitAnnotations.Should().HaveCount(1);
    }

    [Fact]
    public void HitTest_ShouldReturnEmptyForRegionWithNoAnnotations()
    {
        // Arrange
        var testRegion = new Avalonia.Rect(100, 100, 50, 50);

        // Act
        var hitAnnotations = _annotationService.HitTest(testRegion);

        // Assert
        hitAnnotations.Should().BeEmpty();
    }

    [Fact]
    public void SetTool_WithNoneTool_ShouldClearCurrentTool()
    {
        // Arrange
        _annotationService.SetTool(AnnotationToolType.Rectangle);
        _annotationService.CurrentTool.Should().Be(AnnotationToolType.Rectangle);

        // Act
        _annotationService.SetTool(AnnotationToolType.None);

        // Assert
        _annotationService.CurrentTool.Should().Be(AnnotationToolType.None);
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
