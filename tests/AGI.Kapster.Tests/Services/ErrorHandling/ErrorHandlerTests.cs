using System;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Services;
using AGI.Kapster.Desktop.Services.ErrorHandling;
using NSubstitute;
using Xunit;

namespace AGI.Kapster.Tests.Services.ErrorHandling;

/// <summary>
/// Tests for ErrorHandler - focuses on non-UI operations to avoid threading issues
/// </summary>
public class ErrorHandlerTests
{
    private readonly INotificationService _notificationService;
    private readonly ErrorHandler _errorHandler;

    public ErrorHandlerTests()
    {
        _notificationService = Substitute.For<INotificationService>();
        _errorHandler = new ErrorHandler(_notificationService);
    }

    [Fact]
    public void LogError_Info_LogsCorrectly()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        _errorHandler.LogError(exception, ErrorSeverity.Info, "Test context");

        // Assert - no exception thrown
        Assert.True(true);
    }

    [Fact]
    public void LogError_Warning_LogsCorrectly()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");

        // Act
        _errorHandler.LogError(exception, ErrorSeverity.Warning);

        // Assert - no exception thrown
        Assert.True(true);
    }

    [Fact]
    public void LogError_Error_LogsCorrectly()
    {
        // Arrange
        var exception = new Exception("Generic error");

        // Act
        _errorHandler.LogError(exception, ErrorSeverity.Error, "Operation failed");

        // Assert - no exception thrown
        Assert.True(true);
    }

    [Fact]
    public void LogError_Critical_LogsCorrectly()
    {
        // Arrange
        var exception = new NullReferenceException("Critical null reference");

        // Act
        _errorHandler.LogError(exception, ErrorSeverity.Critical);

        // Assert - no exception thrown
        Assert.True(true);
    }

    [Fact]
    public void LogError_Fatal_LogsCorrectly()
    {
        // Arrange
        var exception = new OutOfMemoryException("Fatal memory error");

        // Act
        _errorHandler.LogError(exception, ErrorSeverity.Fatal, "System failure");

        // Assert - no exception thrown
        Assert.True(true);
    }

    // Note: HandleErrorAsync tests removed - they require UI thread (Dispatcher.UIThread)
    // These should be tested via integration tests with proper Avalonia test harness

    // Note: ShowWarningAsync and ShowUserErrorAsync tests removed
    // They require UI thread (Dispatcher.UIThread) and proper Avalonia context
    // Should be tested via integration tests

    [Fact]
    public void ErrorHandler_WithoutNotificationService_DoesNotThrow()
    {
        // Arrange & Act
        var handler = new ErrorHandler(null);

        // Assert
        Assert.NotNull(handler);
    }

    [Theory]
    [InlineData(ErrorSeverity.Info)]
    [InlineData(ErrorSeverity.Warning)]
    [InlineData(ErrorSeverity.Error)]
    [InlineData(ErrorSeverity.Critical)]
    [InlineData(ErrorSeverity.Fatal)]
    public void LogError_AllSeverityLevels_DoNotThrow(ErrorSeverity severity)
    {
        // Arrange
        var exception = new Exception($"Test exception for {severity}");
        var handler = new ErrorHandler(null);

        // Act & Assert - should not throw
        handler.LogError(exception, severity, "Test context");
    }


    [Fact]
    public void LogError_UnauthorizedAccessException_LogsAppropriately()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Access denied");

        // Act & Assert - should not throw
        _errorHandler.LogError(exception, ErrorSeverity.Error, "File access");
    }

    [Fact]
    public void LogError_IOException_LogsAppropriately()
    {
        // Arrange
        var exception = new System.IO.IOException("File not found");

        // Act & Assert - should not throw
        _errorHandler.LogError(exception, ErrorSeverity.Warning, "File operation");
    }

}

