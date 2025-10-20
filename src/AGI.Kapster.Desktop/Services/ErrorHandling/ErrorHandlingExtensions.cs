using System;
using System.Threading.Tasks;
using Serilog;

namespace AGI.Kapster.Desktop.Services.ErrorHandling;

/// <summary>
/// Extension methods for simplified error handling across different layers
/// </summary>
public static class ErrorHandlingExtensions
{
    /// <summary>
    /// Service layer: Log error and rethrow
    /// </summary>
    public static void HandleServiceError(this IErrorHandler errorHandler, Exception exception, string context)
    {
        errorHandler.LogError(exception, ErrorSeverity.Error, context);
        throw exception; // Service layer should propagate exceptions
    }

    /// <summary>
    /// Controller layer: Log error and show user notification
    /// </summary>
    public static async Task HandleControllerErrorAsync(this IErrorHandler errorHandler, Exception exception, string context, bool showUser = true)
    {
        if (showUser)
        {
            await errorHandler.HandleErrorAsync(exception, ErrorSeverity.Error, context);
        }
        else
        {
            errorHandler.LogError(exception, ErrorSeverity.Error, context);
        }
    }

    /// <summary>
    /// UI layer: Show user-friendly error message
    /// </summary>
    public static async Task HandleUIErrorAsync(this IErrorHandler errorHandler, Exception exception, string userMessage)
    {
        await errorHandler.ShowUserErrorAsync("Error", userMessage, exception);
    }

    /// <summary>
    /// Execute with error handling (Service layer pattern)
    /// </summary>
    public static async Task<T?> ExecuteWithErrorHandlingAsync<T>(
        this IErrorHandler errorHandler,
        Func<Task<T>> action,
        string context,
        T? fallbackValue = default)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            errorHandler.LogError(ex, ErrorSeverity.Error, context);
            return fallbackValue;
        }
    }

    /// <summary>
    /// Execute with error handling (non-generic version)
    /// </summary>
    public static async Task ExecuteWithErrorHandlingAsync(
        this IErrorHandler errorHandler,
        Func<Task> action,
        string context)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            errorHandler.LogError(ex, ErrorSeverity.Error, context);
        }
    }

    /// <summary>
    /// Execute with error handling and user notification
    /// </summary>
    public static async Task<bool> TryExecuteAsync(
        this IErrorHandler errorHandler,
        Func<Task> action,
        string context,
        string? userErrorMessage = null)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(userErrorMessage))
            {
                await errorHandler.HandleErrorAsync(ex, ErrorSeverity.Error, context);
            }
            else
            {
                errorHandler.LogError(ex, ErrorSeverity.Error, context);
            }
            return false;
        }
    }
}

