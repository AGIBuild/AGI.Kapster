using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Serilog;

namespace AGI.Kapster.Desktop.Services.ErrorHandling;

/// <summary>
/// Default implementation of error handling service
/// </summary>
public class ErrorHandler : IErrorHandler
{
    private readonly INotificationService? _notificationService;

    public ErrorHandler(INotificationService? notificationService = null)
    {
        _notificationService = notificationService;
    }

    public async Task HandleErrorAsync(Exception exception, ErrorSeverity severity, string? context = null)
    {
        // Log the error
        LogError(exception, severity, context);

        // Show user notification for Error and above
        if (severity >= ErrorSeverity.Error)
        {
            var title = severity switch
            {
                ErrorSeverity.Critical => "Critical Error",
                ErrorSeverity.Fatal => "Fatal Error",
                _ => "Error"
            };

            var message = GetUserFriendlyMessage(exception, context);
            await ShowUserErrorAsync(title, message, exception);
        }
    }

    public async Task ShowUserErrorAsync(string title, string message, Exception? exception = null)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // Try notification service first (non-blocking)
                if (_notificationService != null)
                {
                    try
                    {
                        await _notificationService.ShowAsync(new NotificationRequest(title, message));
                        return; // Notification shown successfully
                    }
                    catch
                    {
                        // Fall through to dialog
                    }
                }
                
                // Fallback to dialog (blocking)
                var dialog = new Window
                    {
                        Title = title,
                        Width = 400,
                        Height = 200,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        CanResize = false
                    };

                    var panel = new StackPanel
                    {
                        Margin = new Avalonia.Thickness(20)
                    };

                    panel.Children.Add(new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Margin = new Avalonia.Thickness(0, 0, 0, 10)
                    });

                    if (exception != null)
                    {
                        panel.Children.Add(new TextBlock
                        {
                            Text = $"Details: {exception.Message}",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            FontSize = 11,
                            Foreground = Avalonia.Media.Brushes.Gray
                        });
                    }

                    var button = new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Margin = new Avalonia.Thickness(0, 10, 0, 0)
                    };

                    button.Click += (s, e) => dialog.Close();
                    panel.Children.Add(button);

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
            });
        }
        catch (Exception ex)
        {
            // Last resort - log only
            Log.Error(ex, "Failed to show error dialog for: {OriginalError}", message);
        }
    }

    public async Task ShowWarningAsync(string title, string message)
    {
        try
        {
            if (_notificationService != null)
            {
                await _notificationService.ShowAsync(new NotificationRequest(title, message));
            }
            else
            {
                Log.Warning("{Title}: {Message}", title, message);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show warning: {Message}", message);
        }
    }

    public void LogError(Exception exception, ErrorSeverity severity, string? context = null)
    {
        var contextMessage = string.IsNullOrEmpty(context) ? string.Empty : $" Context: {context}";

        switch (severity)
        {
            case ErrorSeverity.Info:
                Log.Information(exception, "Info{Context}", contextMessage);
                break;
            case ErrorSeverity.Warning:
                Log.Warning(exception, "Warning{Context}", contextMessage);
                break;
            case ErrorSeverity.Error:
                Log.Error(exception, "Error{Context}", contextMessage);
                break;
            case ErrorSeverity.Critical:
                Log.Error(exception, "CRITICAL ERROR{Context}", contextMessage);
                break;
            case ErrorSeverity.Fatal:
                Log.Fatal(exception, "FATAL ERROR{Context}", contextMessage);
                break;
        }
    }

    private string GetUserFriendlyMessage(Exception exception, string? context)
    {
        // Map common exceptions to user-friendly messages
        return exception switch
        {
            UnauthorizedAccessException => "Permission denied. Please check file/folder permissions.",
            System.IO.IOException io when IsFileLocked(io) => "File is currently in use by another process. Please try again.",
            System.IO.IOException => "Failed to read or write file. Please check if the file exists and is accessible.",
            ArgumentException => "Invalid input provided. Please check your settings.",
            InvalidOperationException => "Operation cannot be performed in the current state.",
            NotSupportedException => "This operation is not supported on your system.",
            TimeoutException => "Operation timed out. Please try again.",
            _ => string.IsNullOrEmpty(context) 
                ? $"An error occurred: {exception.Message}"
                : $"{context}: {exception.Message}"
        };
    }

    private bool IsFileLocked(System.IO.IOException exception)
    {
        int errorCode = System.Runtime.InteropServices.Marshal.GetHRForException(exception) & 0xFFFF;
        return errorCode == 32 || errorCode == 33; // ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION
    }

    private Window? GetMainWindow()
    {
        try
        {
            return Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
        }
        catch
        {
            return null;
        }
    }
}
