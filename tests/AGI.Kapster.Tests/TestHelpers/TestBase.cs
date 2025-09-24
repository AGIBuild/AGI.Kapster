using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AGI.Kapster.Tests.TestHelpers;

/// <summary>
/// Base class for all test classes providing common setup and utilities
/// </summary>
public abstract class TestBase : IDisposable
{
    protected readonly ITestOutputHelper Output;
    protected readonly ILoggerFactory LoggerFactory;

    protected TestBase(ITestOutputHelper output)
    {
        Output = output;

        // Create a simple logger factory for testing without external dependencies
        LoggerFactory = new LoggerFactory();
        LoggerFactory.AddProvider(new TestLoggerProvider(Output));
    }

    /// <summary>
    /// Create a logger for the test
    /// </summary>
    protected ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

    /// <summary>
    /// Dispose resources
    /// </summary>
    public virtual void Dispose()
    {
        LoggerFactory?.Dispose();
    }
}

/// <summary>
/// Simple test logger provider that outputs to test output
/// </summary>
public class TestLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public TestLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TestLogger(categoryName, _output);
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}

/// <summary>
/// Simple test logger implementation
/// </summary>
public class TestLogger : ILogger
{
    private readonly string _categoryName;
    private readonly ITestOutputHelper _output;

    public TestLogger(string categoryName, ITestOutputHelper output)
    {
        _categoryName = categoryName;
        _output = output;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Debug;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var logMessage = $"[{logLevel}] {_categoryName}: {message}";

        if (exception != null)
        {
            logMessage += $"\nException: {exception}";
        }

        try
        {
            _output.WriteLine(logMessage);
        }
        catch
        {
            // Ignore output errors in tests
        }
    }
}
