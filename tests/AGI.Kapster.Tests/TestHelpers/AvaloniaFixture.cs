using Avalonia;
using Avalonia.Headless;
using Xunit;

namespace AGI.Kapster.Tests.TestHelpers;

/// <summary>
/// xUnit test fixture to initialize Avalonia headless platform for UI tests
/// </summary>
public class AvaloniaFixture
{
    private static bool _initialized;

    public AvaloniaFixture()
    {
        if (_initialized) return;

        // Initialize headless Avalonia
        AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true
            })
            .SetupWithoutStarting();

        _initialized = true;
    }
}

/// <summary>
/// Collection definition for tests requiring Avalonia initialization
/// </summary>
[CollectionDefinition("Avalonia")]
public class AvaloniaTestCollection : ICollectionFixture<AvaloniaFixture>
{
}
