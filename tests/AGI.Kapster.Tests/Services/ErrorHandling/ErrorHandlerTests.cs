using System;
using System.IO;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Services;
using AGI.Kapster.Desktop.Services.ErrorHandling;
using NSubstitute;
using Xunit;

namespace AGI.Kapster.Tests.Services.ErrorHandling;

/// <summary>
/// Tests for simplified ErrorHandler
/// Only tests construction and basic behavior (no UI operations)
/// </summary>
public class ErrorHandlerTests
{
    [Fact]
    public void Constructor_WithNotificationService_CreatesInstance()
    {
        // Arrange
        var notificationService = Substitute.For<INotificationService>();

        // Act
        var handler = new ErrorHandler(notificationService);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithoutNotificationService_CreatesInstance()
    {
        // Arrange & Act
        var handler = new ErrorHandler(null);

        // Assert
        Assert.NotNull(handler);
    }

    // Note: HandleErrorAsync and ShowUserErrorAsync tests are skipped
    // because they require UI thread (Dispatcher.UIThread) and Avalonia context
    // These methods should be tested via integration tests with proper Avalonia test harness
    
    // The simplified design ensures:
    // 1. Service layer: log + throw (no ErrorHandler needed)
    // 2. Controller layer: catch + HandleErrorAsync (logs + shows notification)
    // 3. UI layer: ShowUserErrorAsync (shows friendly message)
}
