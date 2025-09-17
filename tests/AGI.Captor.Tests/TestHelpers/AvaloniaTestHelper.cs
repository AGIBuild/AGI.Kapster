using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Skia;
using Avalonia.Threading;

namespace AGI.Captor.Tests.TestHelpers;

/// <summary>
/// Helper class to initialize Avalonia for testing
/// </summary>
public static class AvaloniaTestHelper
{
    private static bool _initialized = false;

    /// <summary>
    /// Initialize Avalonia for testing
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        // Initialize Avalonia
        AppBuilder.Configure<Application>()
            .UseSkia()
            .UsePlatformDetect()
            .SetupWithoutStarting();

        _initialized = true;
    }

    /// <summary>
    /// Create a test bitmap for testing
    /// </summary>
    public static Bitmap CreateTestBitmap(int width = 100, int height = 100)
    {
        Initialize();

        // Create a simple test bitmap using RenderTargetBitmap
        var renderTarget = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var drawingContext = renderTarget.CreateDrawingContext())
        {
            var brush = new SolidColorBrush(Colors.Red);
            drawingContext.FillRectangle(brush, new Rect(0, 0, width, height));
        }
        
        return renderTarget;
    }
}
