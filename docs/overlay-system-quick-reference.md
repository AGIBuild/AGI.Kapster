# Overlay System Quick Reference

## Core Interfaces

### IOverlayController
**Purpose**: Main overlay lifecycle management  
**Implementation**: `SimplifiedOverlayManager`  

```csharp
Task ShowAllAsync();        // Show overlays on all screens
Task CloseAllAsync();       // Close all active overlays
bool IsActive { get; }      // Check if any overlays are active
```

### IOverlayWindow
**Purpose**: Platform-specific overlay window  
**Implementations**: `WindowsOverlayWindow`, `MacOverlayWindow`

```csharp
Task ShowAsync();           // Display overlay window
Task CloseAsync();          // Close overlay window
Screen Screen { get; }      // Associated screen
```

### IScreenCaptureStrategy
**Purpose**: Platform-specific screen capture  
**Implementations**: `WindowsScreenCaptureStrategy`, `MacScreenCaptureStrategy`

```csharp
Task<SKBitmap> CaptureRegionAsync(PixelRect region);     // Capture specific region
Task<SKBitmap> CaptureFullScreenAsync(Screen screen);    // Capture entire screen
```

## Key Events

### RegionSelected
```csharp
public class RegionSelectedEventArgs : EventArgs
{
    public PixelRect Region { get; }           // Selected screen region
    public SKBitmap? CapturedImage { get; }    // Captured bitmap (optional)
    public bool IsEditableSelection { get; }   // Whether to keep overlay open
    public DateTime Timestamp { get; }         // Selection timestamp
}
```

**Event Flow**: `OverlayWindow` → `OverlayManager` → `Application`

### Cancelled
Triggered when user cancels selection (ESC key or click outside)

## Service Registration

### Dependency Injection Setup
```csharp
// Core services
services.AddSingleton<IOverlayController, SimplifiedOverlayManager>();

// Platform-specific (Windows)
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    services.AddTransient<IOverlayWindow, WindowsOverlayWindow>();
    services.AddTransient<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>();
}

// Platform-specific (macOS)
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    services.AddTransient<IOverlayWindow, MacOverlayWindow>();
    services.AddTransient<IScreenCaptureStrategy, MacScreenCaptureStrategy>();
}
```

## Common Usage Patterns

### Basic Overlay Display
```csharp
var overlayController = serviceProvider.GetRequiredService<IOverlayController>();

// Show overlays on all screens
await overlayController.ShowAllAsync();

// Handle region selection
overlayController.RegionSelected += async (sender, args) =>
{
    // Process captured region
    await ProcessCapturedRegion(args.Region, args.CapturedImage);
    
    // Close overlays if not editable
    if (!args.IsEditableSelection)
    {
        await overlayController.CloseAllAsync();
    }
};
```

### Screen Capture Integration
```csharp
var captureStrategy = serviceProvider.GetRequiredService<IScreenCaptureStrategy>();

// Capture specific region
var region = new PixelRect(100, 100, 300, 200);
var bitmap = await captureStrategy.CaptureRegionAsync(region);

// Capture full screen
var screen = Screen.AllScreens.First();
var fullScreenBitmap = await captureStrategy.CaptureFullScreenAsync(screen);
```

## Platform Differences

| Feature | Windows | macOS | Notes |
|---------|---------|-------|-------|
| **Screen Capture** | BitBlt API | screencapture command | Windows faster, macOS more compatible |
| **Element Detection** | UI Automation | Limited | Windows has full element detection |
| **Multi-Screen** | Full support | Full support | Both handle multiple monitors |
| **Permissions** | None required | Screen Recording permission | macOS requires user approval |
| **Performance** | High | Medium | Windows uses native APIs |

## Debugging

### Check Overlay Status
```csharp
var overlayController = serviceProvider.GetRequiredService<IOverlayController>();
Console.WriteLine($"Overlays active: {overlayController.IsActive}");
```

### Screen Information
```csharp
foreach (var screen in Screen.AllScreens)
{
    Console.WriteLine($"Screen: {screen.DeviceName}");
    Console.WriteLine($"Bounds: {screen.Bounds}");
    Console.WriteLine($"Primary: {screen.Primary}");
}
```

### Event Debugging
```csharp
overlayController.RegionSelected += (sender, args) =>
{
    Console.WriteLine($"Region selected: {args.Region}");
    Console.WriteLine($"Editable: {args.IsEditableSelection}");
    Console.WriteLine($"Has image: {args.CapturedImage != null}");
};

overlayController.Cancelled += (sender, args) =>
{
    Console.WriteLine("Overlay cancelled by user");
};
```

## Common Issues & Solutions

### Issue: macOS Black Screen
**Problem**: Secondary monitors show black overlay  
**Solution**: Enhanced screen coverage validation implemented  
**Code**: Check `MacOverlayWindow.EnsureProperScreenCoverage()`

### Issue: Event Handler Memory Leaks
**Problem**: Event handlers not unsubscribed  
**Solution**: Proper disposal pattern  
**Code**: Use `using` statements and explicit cleanup

### Issue: Windows Element Detection Fails
**Problem**: UI Automation fails on some apps  
**Solution**: Graceful degradation to manual selection  
**Code**: Try/catch with fallback to region selection

## File Locations

### Core Implementation
```
src/AGI.Kapster.Desktop/
├── Services/
│   ├── Overlay/
│   │   ├── SimplifiedOverlayManager.cs      # Main controller
│   │   └── Platforms/
│   │       ├── WindowsOverlayWindow.cs      # Windows implementation
│   │       └── MacOverlayWindow.cs          # macOS implementation
│   └── Capture/
│       └── Platforms/
│           ├── WindowsScreenCaptureStrategy.cs
│           └── MacScreenCaptureStrategy.cs
```

### UI Components
```
src/AGI.Kapster.Desktop/
├── Rendering/
│   └── Overlays/
│       ├── OverlayWindow.axaml              # Avalonia overlay window
│       ├── OverlayWindow.axaml.cs           # Window code-behind
│       └── OverlayViewModel.cs              # Window view model
```

### Tests
```
tests/AGI.Kapster.Tests/
├── Services/
│   └── Overlay/
│       ├── OverlayManagerTests.cs          # Manager unit tests
│       └── OverlayWindowTests.cs           # Window unit tests
└── Integration/
    └── OverlayIntegrationTests.cs          # End-to-end tests
```

## Performance Tips

### Memory Management
```csharp
// Dispose bitmaps after use
using var bitmap = await captureStrategy.CaptureRegionAsync(region);
// Process bitmap
// Automatic disposal on scope exit
```

### Event Subscription Cleanup
```csharp
public void Dispose()
{
    if (_overlayController != null)
    {
        _overlayController.RegionSelected -= OnRegionSelected;
        _overlayController.Cancelled -= OnCancelled;
    }
}
```

### Efficient Screen Enumeration
```csharp
// Cache screen list if called frequently
private static readonly Screen[] CachedScreens = Screen.AllScreens.ToArray();
```

## Testing Helpers

### Mock Overlay Controller
```csharp
var mockController = Substitute.For<IOverlayController>();
mockController.IsActive.Returns(false);
mockController.ShowAllAsync().Returns(Task.CompletedTask);
```

### Test Event Triggering
```csharp
var eventArgs = new RegionSelectedEventArgs(
    new PixelRect(0, 0, 100, 100),
    capturedImage: null,
    isEditableSelection: false
);

// Trigger event for testing
mockController.RegionSelected += Raise.EventWith(mockController, eventArgs);
```

### Integration Test Setup
```csharp
var services = new ServiceCollection();
services.AddSingleton<IOverlayController, SimplifiedOverlayManager>();
services.AddTransient<IOverlayWindow, MockOverlayWindow>();
var provider = services.BuildServiceProvider();
```

## Quick Commands

### Show Overlays
```csharp
await overlayController.ShowAllAsync();
```

### Close All Overlays
```csharp
await overlayController.CloseAllAsync();
```

### Capture Region
```csharp
var bitmap = await captureStrategy.CaptureRegionAsync(region);
```

### Check Platform Capabilities
```csharp
bool canDetectElements = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
```