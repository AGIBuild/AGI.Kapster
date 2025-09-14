using AGI.Captor.Desktop.Models;
using AGI.Captor.Desktop.Services;
using Xunit;

namespace AGI.Captor.Tests;

public class BasicTests
{
    [Fact]
    public void SettingsService_CanCreateInstance()
    {
        // Arrange & Act
        var settingsService = new SettingsService();
        
        // Assert
        Assert.NotNull(settingsService);
    }
    
    [Fact]
    public void AppSettings_HasDefaultValues()
    {
        // Arrange & Act
        var settings = new AppSettings();
        
        // Assert
        Assert.NotNull(settings);
        Assert.NotNull(settings.Hotkeys);
        Assert.NotNull(settings.DefaultStyles);
        Assert.NotNull(settings.General);
    }
    
    [Fact]
    public void ExportSettings_HasValidDefaults()
    {
        // Arrange & Act
        var exportSettings = new ExportSettings();
        
        // Assert
        Assert.True(exportSettings.Quality >= 0 && exportSettings.Quality <= 100);
        Assert.True(exportSettings.Compression >= 0 && exportSettings.Compression <= 9);
        Assert.True(exportSettings.DPI > 0);
    }
    
    [Theory]
    [InlineData(ExportFormat.PNG)]
    [InlineData(ExportFormat.JPEG)]
    [InlineData(ExportFormat.BMP)]
    [InlineData(ExportFormat.WebP)]
    public void ExportSettings_SupportsCommonFormats(ExportFormat format)
    {
        // Arrange
        var exportSettings = new ExportSettings();
        
        // Act
        exportSettings.Format = format;
        
        // Assert
        Assert.Equal(format, exportSettings.Format);
    }
}
