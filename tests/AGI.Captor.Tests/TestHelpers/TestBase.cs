using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Xunit.Abstractions;

namespace AGI.Captor.Tests.TestHelpers;

/// <summary>
/// Base class for all test classes providing common setup and utilities
/// </summary>
public abstract class TestBase : IDisposable
{
    protected readonly ITestOutputHelper Output;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly ILoggerFactory LoggerFactory;

    protected TestBase(ITestOutputHelper output)
    {
        Output = output;
        
        // Setup Serilog for testing
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.TestOutput(output)
            .CreateLogger();

        // Setup service collection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSerilog());
        services.AddSingleton<ILoggerFactory>(provider => provider.GetRequiredService<ILoggerFactory>());
        
        ServiceProvider = services.BuildServiceProvider();
        LoggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
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
        ServiceProvider?.Dispose();
        Log.CloseAndFlush();
    }
}
