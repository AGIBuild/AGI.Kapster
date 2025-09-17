using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Xunit.Abstractions;

namespace AGI.Captor.Tests.TestHelpers;

/// <summary>
/// Configuration helper for test environment setup
/// </summary>
public static class TestConfiguration
{
    /// <summary>
    /// Create a test service provider with common services
    /// </summary>
    public static IServiceProvider CreateTestServiceProvider(ITestOutputHelper output)
    {
        var services = new ServiceCollection();
        
        // Setup logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(CreateTestLogger(output));
        });

        // Add common test services
        services.AddSingleton<ILoggerFactory>(provider => provider.GetRequiredService<ILoggerFactory>());
        
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Create a test logger configuration
    /// </summary>
    public static Serilog.ILogger CreateTestLogger(ITestOutputHelper output)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.TestOutput(output)
            .CreateLogger();
    }

    /// <summary>
    /// Create a test service provider with mock services
    /// </summary>
    public static IServiceProvider CreateMockServiceProvider(ITestOutputHelper output)
    {
        var services = new ServiceCollection();
        
        // Setup logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(CreateTestLogger(output));
        });

        // Add mock services
        services.AddSingleton<ILoggerFactory>(provider => provider.GetRequiredService<ILoggerFactory>());
        
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Create a test service provider with real services
    /// </summary>
    public static IServiceProvider CreateRealServiceProvider(ITestOutputHelper output)
    {
        var services = new ServiceCollection();
        
        // Setup logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(CreateTestLogger(output));
        });

        // Add real services
        services.AddSingleton<ILoggerFactory>(provider => provider.GetRequiredService<ILoggerFactory>());
        
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Create a test service provider with specific configuration
    /// </summary>
    public static IServiceProvider CreateServiceProvider(ITestOutputHelper output, Action<IServiceCollection> configureServices)
    {
        var services = new ServiceCollection();
        
        // Setup logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(CreateTestLogger(output));
        });

        // Configure additional services
        configureServices(services);
        
        return services.BuildServiceProvider();
    }
}
