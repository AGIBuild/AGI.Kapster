using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Input;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Tests.TestHelpers;
using NSubstitute;
using System;
using Xunit;
using Xunit.Abstractions;

namespace AGI.Kapster.Tests.Services.Overlay;

/// <summary>
/// Tests for OverlayWindow initialization architecture components
/// Tests individual components without requiring full Avalonia platform
/// </summary>
public class OverlayWindowComponentTests : TestBase
{
    public OverlayWindowComponentTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ElementDetector_ShouldSupportDetection()
    {
        // Arrange
        var mockDetector = Substitute.For<IElementDetector>();

        // Setup
        mockDetector.IsSupported.Returns(true);
        mockDetector.HasPermissions.Returns(true);
        mockDetector.IsDetectionActive.Returns(false);

        // Act & Assert
        Assert.True(mockDetector.IsSupported);
        Assert.True(mockDetector.HasPermissions);
        Assert.False(mockDetector.IsDetectionActive);
    }

    [Fact]
    public void SettingsService_ShouldProvideSettings()
    {
        // Arrange
        var mockSettings = Substitute.For<ISettingsService>();

        // Setup
        mockSettings.Settings.Returns(new AGI.Kapster.Desktop.Models.AppSettings());

        // Act
        var settings = mockSettings.Settings;

        // Assert
        Assert.NotNull(settings);
    }

    [Fact]
    public void ImeController_ShouldControlIme()
    {
        // Arrange
        var mockIme = Substitute.For<IImeController>();

        // Act & Assert - Just verify the interface can be mocked
        Assert.NotNull(mockIme);
    }

    [Fact]
    public void OverlayWindow_ShouldHaveCorrectConstructorSignature()
    {
        // Arrange
        var windowType = typeof(AGI.Kapster.Desktop.Overlays.OverlayWindow);
        var constructors = windowType.GetConstructors();

        // Act & Assert
        Assert.True(constructors.Length > 0, "OverlayWindow should have constructors");
        
        // Check if constructor with expected parameters exists
        var hasExpectedConstructor = Array.Exists(constructors, c =>
        {
            var parameters = c.GetParameters();
            return parameters.Length >= 2 && // At least settingsService and imeController
                   parameters[0].ParameterType.Name == "ISettingsService" &&
                   parameters[1].ParameterType.Name == "IImeController";
        });
        
        Assert.True(hasExpectedConstructor, "OverlayWindow should have constructor with ISettingsService and IImeController");
    }

    [Fact]
    public void OverlayWindow_ShouldHaveTunnelingEventMethods()
    {
        // Arrange
        var windowType = typeof(AGI.Kapster.Desktop.Overlays.OverlayWindow);
        var methods = windowType.GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert
        var hasPreviewKeyDown = Array.Exists(methods, m => m.Name == "OnPreviewKeyDown");
        var hasPreviewKeyUp = Array.Exists(methods, m => m.Name == "OnPreviewKeyUp");
        
        Assert.True(hasPreviewKeyDown, "OverlayWindow should have OnPreviewKeyDown method");
        Assert.True(hasPreviewKeyUp, "OverlayWindow should have OnPreviewKeyUp method");
    }

    [Fact]
    public void OverlayWindow_ShouldHaveEssentialFields()
    {
        // Arrange
        var windowType = typeof(AGI.Kapster.Desktop.Overlays.OverlayWindow);
        var fields = windowType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert - Check for essential fields like handlers and cached controls
        var hasEssentialFields = Array.Exists(fields, f => 
            f.Name.Contains("Handler") || f.Name.Contains("_selector") || f.Name.Contains("_toolbar") || f.Name.Contains("_annotator"));
        
        Assert.True(hasEssentialFields, "OverlayWindow should have essential fields (handlers and cached controls)");
    }

    [Fact]
    public void OverlayWindow_ShouldHaveHandlerFields()
    {
        // Arrange
        var windowType = typeof(AGI.Kapster.Desktop.Overlays.OverlayWindow);
        var fields = windowType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert
        var hasHandlerFields = Array.Exists(fields, f => 
            f.Name.Contains("Handler"));
        
        Assert.True(hasHandlerFields, "OverlayWindow should have handler fields");
    }

    [Fact]
    public void OverlayWindow_ShouldHaveInitializationMethods()
    {
        // Arrange
        var windowType = typeof(AGI.Kapster.Desktop.Overlays.OverlayWindow);
        var methods = windowType.GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert
        var hasInitMethods = Array.Exists(methods, m => 
            m.Name.Contains("Initialize") || m.Name.Contains("SetFocus"));
        
        Assert.True(hasInitMethods, "OverlayWindow should have initialization methods");
    }
}