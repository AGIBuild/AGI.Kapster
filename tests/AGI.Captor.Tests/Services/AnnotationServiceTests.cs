using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Captor.Desktop.Models;
using AGI.Captor.Desktop.Services;
using AGI.Captor.Desktop.Services.Annotation;
using AGI.Captor.Desktop.Services.Settings;
using AGI.Captor.Tests.TestHelpers;
using Avalonia;
using Avalonia.Media;

namespace AGI.Captor.Tests.Services;

public class AnnotationServiceTests : TestBase
{
    private readonly ISettingsService _settingsService;
    private readonly AnnotationService _annotationService;

    public AnnotationServiceTests(ITestOutputHelper output) : base(output)
    {
        _settingsService = Substitute.For<ISettingsService>();

        // Setup mock settings
        var mockSettings = new AppSettings
        {
            DefaultStyles = new DefaultStyleSettings
            {
                Text = new TextStyleSettings
                {
                    FontSize = 16,
                    FontFamily = "Segoe UI",
                    Color = Color.FromRgb(0, 0, 0)
                },
                Shape = new ShapeStyleSettings
                {
                    StrokeColor = Color.FromRgb(255, 0, 0),
                    StrokeThickness = 2.0,
                    FillMode = "None"
                }
            }
        };

        _settingsService.Settings.Returns(mockSettings);
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
    public void CurrentTool_ShouldUpdateTool()
    {
        // Arrange
        var tool = AnnotationToolType.Rectangle;

        // Act
        _annotationService.CurrentTool = tool;

        // Assert
        _annotationService.CurrentTool.Should().Be(tool);
    }

    [Fact]
    public void CurrentTool_ShouldRaiseToolChangedEvent()
    {
        // Arrange
        var tool = AnnotationToolType.Ellipse;
        ToolChangedEventArgs? eventArgs = null;
        _annotationService.ToolChanged += (sender, args) => eventArgs = args;

        // Act
        _annotationService.CurrentTool = tool;

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.NewTool.Should().Be(tool);
    }

    [Theory]
    [InlineData(AnnotationToolType.Rectangle)]
    [InlineData(AnnotationToolType.Ellipse)]
    [InlineData(AnnotationToolType.Arrow)]
    [InlineData(AnnotationToolType.Text)]
    [InlineData(AnnotationToolType.Freehand)]
    [InlineData(AnnotationToolType.Emoji)]
    public void CurrentTool_ShouldAcceptAllValidTools(AnnotationToolType tool)
    {
        // Act
        _annotationService.CurrentTool = tool;

        // Assert
        _annotationService.CurrentTool.Should().Be(tool);
    }

    [Fact]
    public void StartAnnotation_WithRectangleTool_ShouldCreateRectangleAnnotation()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Rectangle;
        var startPoint = new Avalonia.Point(10, 20);

        // Act
        var annotation = _annotationService.StartAnnotation(startPoint);

        // Assert
        annotation.Should().NotBeNull();
        annotation!.Should().BeOfType<RectangleAnnotation>();
        annotation!.Type.Should().Be(AnnotationType.Rectangle);
    }

    [Fact]
    public void StartAnnotation_WithEllipseTool_ShouldCreateEllipseAnnotation()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Ellipse;
        var startPoint = new Avalonia.Point(15, 25);

        // Act
        var annotation = _annotationService.StartAnnotation(startPoint);

        // Assert
        annotation.Should().NotBeNull();
        annotation!.Should().BeOfType<EllipseAnnotation>();
        annotation!.Type.Should().Be(AnnotationType.Ellipse);
    }

    [Fact]
    public void StartAnnotation_WithArrowTool_ShouldCreateArrowAnnotation()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Arrow;
        var startPoint = new Avalonia.Point(5, 10);

        // Act
        var annotation = _annotationService.StartAnnotation(startPoint);

        // Assert
        annotation.Should().NotBeNull();
        annotation!.Should().BeOfType<ArrowAnnotation>();
        annotation!.Type.Should().Be(AnnotationType.Arrow);
    }

    [Fact]
    public void StartAnnotation_WithTextTool_ShouldCreateTextAnnotation()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Text;
        var startPoint = new Avalonia.Point(30, 40);

        // Act
        var annotation = _annotationService.StartAnnotation(startPoint);

        // Assert
        annotation.Should().NotBeNull();
        annotation!.Should().BeOfType<TextAnnotation>();
        annotation!.Type.Should().Be(AnnotationType.Text);
    }

    [Fact]
    public void StartAnnotation_WithFreehandTool_ShouldCreateFreehandAnnotation()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Freehand;
        var startPoint = new Avalonia.Point(50, 60);

        // Act
        var annotation = _annotationService.StartAnnotation(startPoint);

        // Assert
        annotation.Should().NotBeNull();
        annotation!.Should().BeOfType<FreehandAnnotation>();
        annotation!.Type.Should().Be(AnnotationType.Freehand);
    }

    [Fact]
    public void StartAnnotation_WithEmojiTool_ShouldCreateEmojiAnnotation()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Emoji;
        var startPoint = new Avalonia.Point(70, 80);

        // Act
        var annotation = _annotationService.StartAnnotation(startPoint);

        // Assert
        annotation.Should().NotBeNull();
        annotation!.Should().BeOfType<EmojiAnnotation>();
        annotation!.Type.Should().Be(AnnotationType.Emoji);
    }

    [Fact]
    public void UpdateAnnotation_WithRectangle_ShouldUpdateRectangleBounds()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Rectangle;
        var startPoint = new Avalonia.Point(10, 20);
        var endPoint = new Avalonia.Point(50, 60);

        var annotation = _annotationService.StartAnnotation(startPoint) as RectangleAnnotation;

        // Act
        _annotationService.UpdateAnnotation(endPoint, annotation!);

        // Assert
        annotation!.Rectangle.Should().Be(new Avalonia.Rect(10, 20, 40, 40));
    }

    [Fact]
    public void UpdateAnnotation_WithEllipse_ShouldUpdateEllipseBounds()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Ellipse;
        var startPoint = new Avalonia.Point(5, 10);
        var endPoint = new Avalonia.Point(25, 35);

        var annotation = _annotationService.StartAnnotation(startPoint) as EllipseAnnotation;

        // Act
        _annotationService.UpdateAnnotation(endPoint, annotation!);

        // Assert
        annotation!.BoundingRect.Should().Be(new Avalonia.Rect(5, 10, 20, 25));
    }

    [Fact]
    public void UpdateAnnotation_WithArrow_ShouldUpdateArrowEndPoint()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Arrow;
        var startPoint = new Avalonia.Point(0, 0);
        var endPoint = new Avalonia.Point(100, 100);

        var annotation = _annotationService.StartAnnotation(startPoint) as ArrowAnnotation;

        // Act
        _annotationService.UpdateAnnotation(endPoint, annotation!);

        // Assert
        annotation!.EndPoint.Should().Be(endPoint);
    }

    [Fact]
    public void UpdateAnnotation_WithFreehand_ShouldAddPointToFreehand()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Freehand;
        var startPoint = new Avalonia.Point(10, 20);
        var additionalPoint = new Avalonia.Point(30, 40);

        var annotation = _annotationService.StartAnnotation(startPoint) as FreehandAnnotation;
        var initialPointCount = annotation!.Points.Count;

        // Act
        _annotationService.UpdateAnnotation(additionalPoint, annotation);

        // Assert
        annotation.Points.Should().HaveCount(initialPointCount + 1);
        annotation.Points.Last().Should().Be(additionalPoint);
    }

    [Fact]
    public void FinishAnnotation_ShouldCompleteCurrentAnnotation()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Rectangle;
        var startPoint = new Avalonia.Point(10, 20);
        var annotation = _annotationService.StartAnnotation(startPoint);

        // Act
        _annotationService.FinishCreate(annotation!);

        // Assert
        annotation!.State.Should().Be(AnnotationState.Normal);
    }

    [Fact]
    public void CancelAnnotation_ShouldRemoveCurrentAnnotation()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Rectangle;
        var startPoint = new Avalonia.Point(10, 20);
        var annotation = _annotationService.StartAnnotation(startPoint);

        // Act
        _annotationService.CancelCreate(annotation!);

        // Assert
        // The annotation should be cancelled and not added to manager
        _annotationService.Manager.Items.Should().BeEmpty();
    }

    [Fact]
    public void HitTest_ShouldReturnAnnotationsInRegion()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Rectangle;
        var annotation = _annotationService.StartAnnotation(new Avalonia.Point(10, 10));
        _annotationService.UpdateAnnotation(new Avalonia.Point(50, 50), annotation!);
        _annotationService.FinishCreate(annotation!);

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
    public void CurrentTool_WithNoneTool_ShouldClearCurrentTool()
    {
        // Arrange
        _annotationService.CurrentTool = AnnotationToolType.Rectangle;
        _annotationService.CurrentTool.Should().Be(AnnotationToolType.Rectangle);

        // Act
        _annotationService.CurrentTool = AnnotationToolType.None;

        // Assert
        _annotationService.CurrentTool.Should().Be(AnnotationToolType.None);
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
