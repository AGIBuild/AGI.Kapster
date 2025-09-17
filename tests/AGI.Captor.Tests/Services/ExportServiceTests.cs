using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Captor.Desktop.Models;
using AGI.Captor.Desktop.Services;
using AGI.Captor.Tests.TestHelpers;

namespace AGI.Captor.Tests.Services;

public class ExportServiceTests : TestBase
{
    private readonly ISettingsService _settingsService;
    private readonly ExportService _exportService;

    public ExportServiceTests(ITestOutputHelper output) : base(output)
    {
        _settingsService = Substitute.For<ISettingsService>();
        _exportService = new ExportService(_settingsService);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Act & Assert
        _exportService.Should().NotBeNull();
    }

    [Fact]
    public void ExportAsync_WithNullBitmap_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = async () => await _exportService.ExportAsync(null!, "test.png", ExportFormat.PNG);
        action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void ExportAsync_WithEmptyFilePath_ShouldThrowArgumentException()
    {
        // Arrange
        var bitmap = new SkiaSharp.SKBitmap(100, 100);

        // Act & Assert
        var action = async () => await _exportService.ExportAsync(bitmap, "", ExportFormat.PNG);
        action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void ExportAsync_WithNullFilePath_ShouldThrowArgumentNullException()
    {
        // Arrange
        var bitmap = new SkiaSharp.SKBitmap(100, 100);

        // Act & Assert
        var action = async () => await _exportService.ExportAsync(bitmap, null!, ExportFormat.PNG);
        action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(ExportFormat.PNG)]
    [InlineData(ExportFormat.JPEG)]
    [InlineData(ExportFormat.BMP)]
    [InlineData(ExportFormat.WebP)]
    public void ExportAsync_WithValidParameters_ShouldNotThrow(ExportFormat format)
    {
        // Arrange
        var bitmap = new SkiaSharp.SKBitmap(100, 100);
        var filePath = $"test.{format.ToString().ToLower()}";

        // Act & Assert
        var action = async () => await _exportService.ExportAsync(bitmap, filePath, format);
        action.Should().NotThrowAsync();
    }

    [Fact]
    public void ExportAsync_WithInvalidDirectory_ShouldThrowDirectoryNotFoundException()
    {
        // Arrange
        var bitmap = new SkiaSharp.SKBitmap(100, 100);
        var filePath = @"C:\NonExistentDirectory\test.png";

        // Act & Assert
        var action = async () => await _exportService.ExportAsync(bitmap, filePath, ExportFormat.PNG);
        action.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public void ExportAsync_WithReadOnlyFile_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var bitmap = new SkiaSharp.SKBitmap(100, 100);
        var filePath = Path.Combine(Path.GetTempPath(), "readonly_test.png");
        
        // Create a read-only file
        File.WriteAllText(filePath, "test");
        File.SetAttributes(filePath, FileAttributes.ReadOnly);

        try
        {
            // Act & Assert
            var action = async () => await _exportService.ExportAsync(bitmap, filePath, ExportFormat.PNG);
            action.Should().ThrowAsync<UnauthorizedAccessException>();
        }
        finally
        {
            // Cleanup
            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ExportAsync_WithValidBitmap_ShouldCreateFile()
    {
        // Arrange
        var bitmap = new SkiaSharp.SKBitmap(100, 100);
        var filePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.png");

        try
        {
            // Act
            var action = async () => await _exportService.ExportAsync(bitmap, filePath, ExportFormat.PNG);
            action.Should().NotThrowAsync();

            // Assert
            File.Exists(filePath).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void ExportAsync_WithDifferentFormats_ShouldCreateCorrectFiles()
    {
        // Arrange
        var bitmap = new SkiaSharp.SKBitmap(50, 50);
        var formats = new[] { ExportFormat.PNG, ExportFormat.JPEG, ExportFormat.BMP, ExportFormat.WebP };
        var filePaths = new List<string>();

        try
        {
            // Act
            foreach (var format in formats)
            {
                var filePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.{format.ToString().ToLower()}");
                filePaths.Add(filePath);
                
                var action = async () => await _exportService.ExportAsync(bitmap, filePath, format);
                action.Should().NotThrowAsync();
            }

            // Assert
            foreach (var filePath in filePaths)
            {
                File.Exists(filePath).Should().BeTrue();
            }
        }
        finally
        {
            // Cleanup
            foreach (var filePath in filePaths)
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void ExportAsync_WithLargeBitmap_ShouldHandleCorrectly()
    {
        // Arrange
        var bitmap = new SkiaSharp.SKBitmap(1920, 1080);
        var filePath = Path.Combine(Path.GetTempPath(), $"large_test_{Guid.NewGuid()}.png");

        try
        {
            // Act & Assert
            var action = async () => await _exportService.ExportAsync(bitmap, filePath, ExportFormat.PNG);
            action.Should().NotThrowAsync();

            File.Exists(filePath).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void ExportAsync_WithSmallBitmap_ShouldHandleCorrectly()
    {
        // Arrange
        var bitmap = new SkiaSharp.SKBitmap(1, 1);
        var filePath = Path.Combine(Path.GetTempPath(), $"small_test_{Guid.NewGuid()}.png");

        try
        {
            // Act & Assert
            var action = async () => await _exportService.ExportAsync(bitmap, filePath, ExportFormat.PNG);
            action.Should().NotThrowAsync();

            File.Exists(filePath).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void ExportAsync_WithConcurrentExports_ShouldHandleCorrectly()
    {
        // Arrange
        var bitmap = new SkiaSharp.SKBitmap(100, 100);
        var tasks = new List<Task>();
        var filePaths = new List<string>();

        try
        {
            // Act
            for (int i = 0; i < 5; i++)
            {
                var filePath = Path.Combine(Path.GetTempPath(), $"concurrent_test_{i}_{Guid.NewGuid()}.png");
                filePaths.Add(filePath);
                
                tasks.Add(_exportService.ExportAsync(bitmap, filePath, ExportFormat.PNG));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            foreach (var filePath in filePaths)
            {
                File.Exists(filePath).Should().BeTrue();
            }
        }
        finally
        {
            // Cleanup
            foreach (var filePath in filePaths)
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
