using System;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Services.ErrorHandling;

/// <summary>
/// Error severity levels for categorizing exceptions
/// </summary>
public enum ErrorSeverity
{
    /// <summary>Information - operation succeeded with notes</summary>
    Info,
    
    /// <summary>Warning - operation completed with issues</summary>
    Warning,
    
    /// <summary>Error - operation failed but application can continue</summary>
    Error,
    
    /// <summary>Critical - serious error that may affect application stability</summary>
    Critical,
    
    /// <summary>Fatal - unrecoverable error, application should terminate</summary>
    Fatal
}

/// <summary>
/// Unified error handling service for consistent error management across the application
/// </summary>
public interface IErrorHandler
{
    /// <summary>
    /// Handle an exception with specified severity
    /// </summary>
    /// <param name="exception">The exception to handle</param>
    /// <param name="severity">Error severity level</param>
    /// <param name="context">Additional context information</param>
    Task HandleErrorAsync(Exception exception, ErrorSeverity severity, string? context = null);
    
    /// <summary>
    /// Show a user-friendly error message
    /// </summary>
    /// <param name="title">Error dialog title</param>
    /// <param name="message">User-friendly error message</param>
    /// <param name="exception">Optional exception for details</param>
    Task ShowUserErrorAsync(string title, string message, Exception? exception = null);
    
    /// <summary>
    /// Show a warning message to the user
    /// </summary>
    /// <param name="title">Warning dialog title</param>
    /// <param name="message">Warning message</param>
    Task ShowWarningAsync(string title, string message);
    
    /// <summary>
    /// Log an error without showing UI
    /// </summary>
    /// <param name="exception">The exception to log</param>
    /// <param name="severity">Error severity level</param>
    /// <param name="context">Additional context information</param>
    void LogError(Exception exception, ErrorSeverity severity, string? context = null);
}

