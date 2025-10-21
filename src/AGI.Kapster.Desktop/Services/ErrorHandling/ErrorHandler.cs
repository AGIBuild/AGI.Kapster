using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Serilog;

namespace AGI.Kapster.Desktop.Services.ErrorHandling;

/// <summary>
/// Unified error handler implementation
/// </summary>
public class ErrorHandler : IErrorHandler
{
    private readonly INotificationService? _notificationService;

    public ErrorHandler(INotificationService? notificationService = null)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// Handles error: log + notify user for Error/Fatal
    /// </summary>
    public async Task HandleErrorAsync(Exception exception, ErrorSeverity severity, string? context = null)
    {
        // Always log
        LogError(exception, severity, context);

        // Show user notification for Error/Fatal
        if (severity >= ErrorSeverity.Error)
        {
            var message = GetUserFriendlyMessage(exception);
            await ShowUserErrorAsync(message, exception);
        }
    }

    /// <summary>
    /// Shows user-friendly error (notification or dialog fallback)
    /// </summary>
    public async Task ShowUserErrorAsync(string message, Exception? exception = null)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // Try notification first (non-blocking)
                if (_notificationService != null)
                {
                    try
                    {
                        await _notificationService.ShowAsync(new NotificationRequest("Error", message));
                        return;
                    }
                    catch
                    {
                        // Fall through to dialog
                    }
                }

                // Fallback to dialog
                await ShowErrorDialogAsync(message, exception);
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show error to user: {Message}", message);
        }
    }

    /// <summary>
    /// Logs error with appropriate severity
    /// </summary>
    private void LogError(Exception exception, ErrorSeverity severity, string? context)
    {
        var contextInfo = string.IsNullOrEmpty(context) ? "" : $" Context: {context}";

        switch (severity)
        {
            case ErrorSeverity.Information:
                Log.Information(exception, "Error handled{Context}", contextInfo);
                break;
            case ErrorSeverity.Warning:
                Log.Warning(exception, "Warning{Context}", contextInfo);
                break;
            case ErrorSeverity.Error:
                Log.Error(exception, "Error{Context}", contextInfo);
                break;
            case ErrorSeverity.Fatal:
                Log.Fatal(exception, "Fatal error{Context}", contextInfo);
                break;
        }
    }

    /// <summary>
    /// Maps exception to user-friendly message
    /// </summary>
    private string GetUserFriendlyMessage(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => "Permission denied. Please check file/folder permissions.",
            FileNotFoundException => "File not found. Please check the file path.",
            DirectoryNotFoundException => "Directory not found. Please check the path.",
            IOException io when IsFileLocked(io) => "File is in use by another process. Please close it and try again.",
            IOException => "File operation failed. Please check disk space and permissions.",
            ArgumentException => "Invalid input. Please check your data.",
            InvalidOperationException => "Operation cannot be performed in current state.",
            NotSupportedException => "This operation is not supported on your system.",
            _ => $"An error occurred: {exception.Message}"
        };
    }

    /// <summary>
    /// Check if IOException is due to file lock
    /// </summary>
    private bool IsFileLocked(IOException exception)
    {
        var errorCode = exception.HResult & 0xFFFF;
        return errorCode == 32 || errorCode == 33; // ERROR_SHARING_VIOLATION, ERROR_LOCK_VIOLATION
    }

    /// <summary>
    /// Shows error dialog (blocking)
    /// </summary>
    private async Task ShowErrorDialogAsync(string message, Exception? exception)
    {
        var dialog = new Window
        {
            Title = "Error",
            Width = 450,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 15
        };

        // Message
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 14
        });

        // Exception details (if any)
        if (exception != null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Details: {exception.GetType().Name}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.Gray)
            });
        }

        // OK button
        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        okButton.Click += (_, _) => dialog.Close();
        panel.Children.Add(okButton);

        dialog.Content = panel;

        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            await dialog.ShowDialog(mainWindow);
        }
        else
        {
            dialog.Show();
        }
    }

    /// <summary>
    /// Gets main application window
    /// </summary>
    private Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}
