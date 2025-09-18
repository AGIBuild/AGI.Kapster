using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AGI.Captor.Tests.TestHelpers;

/// <summary>
/// Test application for Avalonia UI tests
/// </summary>
public class TestApplication : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // No main window needed for testing
        }
        
        base.OnFrameworkInitializationCompleted();
    }
}