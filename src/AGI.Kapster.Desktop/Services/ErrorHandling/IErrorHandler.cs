using System;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Services.ErrorHandling;

/// <summary>
/// Error severity levels
/// </summary>
public enum ErrorSeverity
{
    /// <summary>Informational messages</summary>
    Information,
    
    /// <summary>Warning messages</summary>
    Warning,
    
    /// <summary>Error messages</summary>
    Error,
    
    /// <summary>Fatal/critical errors</summary>
    Fatal
}

/// <summary>
/// Unified error handling service
/// Three-layer pattern:
/// - Service layer: log + throw
/// - Controller layer: catch + HandleErrorAsync
/// - UI layer: ShowUserErrorAsync with friendly message
/// </summary>
public interface IErrorHandler
{
    /// <summary>
    /// Handles error with logging and optional user notification
    /// Controller layer: catch exceptions and call this
    /// </summary>
    Task HandleErrorAsync(Exception exception, ErrorSeverity severity, string? context = null);

    /// <summary>
    /// Shows user-friendly error message (notification or dialog)
    /// UI layer: display friendly messages
    /// </summary>
    Task ShowUserErrorAsync(string message, Exception? exception = null);
}
