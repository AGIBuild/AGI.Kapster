# Overlay System Quick Reference

## Core Interfaces

### IScreenshotService (High-Level API)
```csharp
bool IsActive { get; }
Task TakeScreenshotAsync();
void Cancel();
```
**Usage**: `IScreenshotService` → inject and call `TakeScreenshotAsync()`

### IOverlayCoordinator (Platform Abstraction)
```csharp
Task<IOverlaySession> StartSessionAsync();
void CloseCurrentSession();
bool HasActiveSession { get; }
```
**Implementations**: `WindowsOverlayCoordinator`, `MacOverlayCoordinator`

### IOverlaySession (Lifecycle Management)
```csharp
IReadOnlyList<Window> Windows { get; }
void ShowAll();
void CloseAll();
bool HasSelection { get; }
```
**Lifecycle**: Created per screenshot, disposed after completion

### IOverlayWindow (Window Interface)
```csharp
PixelPoint Position { get; set; }
double Width { get; set; }
double Height { get; set; }
void Show();
void Close();
void SetMaskSize(double width, double height);
void SetSession(IOverlaySession? session);
Window AsWindow();
```
**Implementation**: `OverlayWindow` (Avalonia)

### IScreenCoordinateMapper (Coordinate System)
```csharp
PixelRect MapToPhysicalRect(Rect logicalRect, Screen screen);
Rect MapToLogicalRect(PixelRect physicalRect, Screen screen);
Screen? GetScreenFromPoint(PixelPoint point, IReadOnlyList<Screen> screens);
```
**Implementations**: `WindowsCoordinateMapper`, `MacCoordinateMapper`

## Service Lifetimes

| Service | Lifetime | Reason |
|---------|----------|--------|
| `IScreenshotService` | Singleton | High-level API |
| `IOverlayCoordinator` | Singleton | Session orchestration |
| `IOverlaySessionFactory` | Singleton | Factory pattern |
| `IOverlayWindowFactory` | Singleton | Factory pattern |
| `IScreenCoordinateMapper` | **Transient** | Fresh screen data |
| `IScreenCaptureStrategy` | Transient | Per-capture instance |

## Common Usage

### Take Screenshot
```csharp
var screenshotService = serviceProvider.GetRequiredService<IScreenshotService>();
await screenshotService.TakeScreenshotAsync();
```

### Cancel Screenshot
```csharp
screenshotService.Cancel();  // Closes all overlays
```

### Check Active Status
```csharp
if (screenshotService.IsActive)
{
    // Screenshot in progress
}
```

## Event Flow

```
User clicks hotkey
    ↓
IScreenshotService.TakeScreenshotAsync()
    ↓
IOverlayCoordinator.StartSessionAsync()
    ↓
IOverlaySession.ShowAll()
    ↓
[User selects region]
    ↓
OverlayWindow.RegionSelected event
    ↓
IOverlayCoordinator.CloseCurrentSession()
    ↓
IOverlaySession.Dispose()
```

## Platform Differences

| Feature | Windows | macOS |
|---------|---------|-------|
| **Window strategy** | Single virtual desktop | Per-screen windows |
| **Coordinate system** | Virtual desktop bounds (negative coords) | Per-screen positive coords |
| **DPI handling** | System DPI awareness | Retina scaling |
| **Screen capture** | Win32 BitBlt (fast) | screencapture command |
| **Element detection** | ✅ UI Automation | ❌ Not supported |
| **Permissions** | None required | Screen Recording permission |

## Architecture Patterns

### Template Method Pattern
`OverlayCoordinatorBase` defines session creation flow:
1. `GetScreensAsync()` - Get available screens
2. `CalculateTargetRegions()` - Platform-specific (virtual desktop vs per-screen)
3. `CreateAndConfigureWindowsAsync()` - Platform-specific window creation
4. `session.ShowAll()` - Display overlays

### Factory Pattern
- `IOverlayWindowFactory` creates `IOverlayWindow` with DI
- `IOverlaySessionFactory` creates `IOverlaySession`

### Coordinator Pattern
- Abstracts platform differences at orchestration level
- Single entry point via `IScreenshotService`

## Testing

### Mock IScreenshotService
```csharp
var mockService = Substitute.For<IScreenshotService>();
mockService.IsActive.Returns(false);
```

### Mock IOverlayCoordinator
```csharp
var mockCoordinator = Substitute.For<IOverlayCoordinator>();
mockCoordinator.HasActiveSession.Returns(true);
```

### Mock IOverlayWindow
```csharp
var mockWindow = Substitute.For<IOverlayWindow>();
mockWindow.Width.Returns(1920);
mockWindow.Height.Returns(1080);
```

### Cannot Test
- `OverlayWindow` instantiation (requires IWindowingPlatform)
- Actual window rendering (requires UI thread)

## File Locations

```
src/AGI.Kapster.Desktop/
├── Services/
│   ├── Screenshot/IScreenshotService.cs
│   └── Overlay/
│       ├── Coordinators/
│       │   ├── IOverlayCoordinator.cs
│       │   ├── WindowsOverlayCoordinator.cs
│       │   └── MacOverlayCoordinator.cs
│       └── State/IOverlaySession.cs
└── Overlays/
    ├── IOverlayWindow.cs
    └── OverlayWindow.axaml.cs

tests/AGI.Kapster.Tests/Services/
├── Screenshot/ScreenshotServiceTests.cs
├── Overlay/
│   ├── OverlayCoordinatorBaseTests.cs
│   └── OverlayWindowFactoryTests.cs
```

## Common Issues

**Issue**: Second screenshot fails to draw selection  
**Fix**: Session-scoped state via `IOverlaySession`

**Issue**: Mask doesn't cover all screens  
**Fix**: Use `SetMaskSize()` and `ClientSize` instead of `Bounds`

**Issue**: Saved image blurry  
**Fix**: Adaptive DPI based on screen scaling

**Issue**: Test fails with "Unable to locate IWindowingPlatform"  
**Fix**: Mock `IOverlayWindow` interface, don't instantiate `OverlayWindow`

## Quick Commands

```csharp
// Take screenshot
await screenshotService.TakeScreenshotAsync();

// Cancel
screenshotService.Cancel();

// Check status
bool isActive = screenshotService.IsActive;

// Get screens
var coordinator = serviceProvider.GetRequiredService<IOverlayCoordinator>();
// (Screens obtained internally via GetScreensAsync)
```

## Key Reminders

- ✅ `IScreenCoordinateMapper` is **Transient** (fresh screen data)
- ✅ `IOverlayWindow` enables DI and testing
- ✅ `AsWindow()` method for `IOverlaySession.AddWindow()` compatibility
- ✅ Platform differences isolated in coordinators
- ✅ Session auto-cleanup via `IDisposable`
