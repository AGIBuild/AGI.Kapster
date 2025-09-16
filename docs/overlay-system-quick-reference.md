# Overlay System Quick Reference

## Key Files to Know

### Core UI
- `src/AGI.Captor.Desktop/Overlays/OverlayWindow.axaml.cs` - Main overlay window implementation

### Service Layer
- `src/AGI.Captor.Desktop/Services/Overlay/SimplifiedOverlayManager.cs` - Manages all overlay windows
- `src/AGI.Captor.Desktop/Program.cs` - DI registration (lines 86-111)

### Platform Implementations
- **Overlay Windows**: `Services/Overlay/Platforms/Windows*.cs`, `Mac*.cs`
- **Screen Capture**: `Services/Capture/Platforms/Windows*.cs`, `Mac*.cs`
- **Clipboard**: `Services/Clipboard/Platforms/Windows*.cs`, `Mac*.cs`
- **Element Detection**: `Services/ElementDetection/Windows*.cs`, `Platforms/Null*.cs`
- **Rendering**: `Rendering/Overlays/Windows*.cs`

## Common Tasks

### 1. Adding a New Platform
```csharp
// In Program.cs
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Services.AddTransient<IOverlayWindow, LinuxOverlayWindow>();
    builder.Services.AddTransient<IElementDetector, NullElementDetector>();
    builder.Services.AddSingleton<IScreenCaptureStrategy, LinuxScreenCaptureStrategy>();
    // ... other services
}
```

### 2. Handling Screenshot Capture
```csharp
// Screenshot is captured in SimplifiedOverlayManager.OnRegionSelected
var bitmap = await _captureStrategy.CaptureRegionAsync(e.Region);
await _clipboardStrategy.SetImageAsync(bitmap);
```

### 3. Preventing Overlay Close
```csharp
// In OverlayWindow, set IsEditableSelection = true
RegionSelected?.Invoke(this, new RegionSelectedEventArgs(r, false, null, true));
```

### 4. Closing All Overlays
```csharp
var overlayController = serviceProvider.GetService<IOverlayController>();
overlayController.CloseAll();
```

## Important Flags and Properties

### RegionSelectedEventArgs
- `Region` - The selected area
- `IsFullScreen` - Full screen capture?
- `DetectedElement` - Element info if available
- **`IsEditableSelection`** - If true, keeps overlay open for annotation

### CaptureMode Enum
- `FullScreen` - Entire screen
- `Window` - Specific window
- `Region` - User-selected area
- `Element` - UI element

## Event Flow Diagram
```
User Action → OverlayWindow → Platform Wrapper → SimplifiedOverlayManager
                    ↓                   ↓                    ↓
              Raise Event     Check IsEditable      Capture & Close

If IsEditableSelection = true: Event stops at Platform Wrapper
If IsEditableSelection = false: Event flows to Manager → Close all
```

## Platform Differences

| Feature | Windows | macOS |
|---------|---------|-------|
| Screen Enum | Temp anchor window | Primary window ref |
| Element Detection | ✅ UI Automation | ❌ Not implemented |
| Screen Capture | Win32 BitBlt | screencapture cmd |
| Clipboard | Win32 API | Avalonia API |

## Debugging Tips

1. **Check logs**: All important actions log with `Log.Debug()` or `Log.Information()`
2. **Event not firing?**: Check `IsEditableSelection` flag
3. **Window not closing?**: Verify `IOverlayController.CloseAll()` is called
4. **Platform issue?**: Check platform-specific implementation in `Platforms/` folder

## Critical Rules

1. **Never** call `window.Close()` directly - use `IOverlayController.CloseAll()`
2. **Always** check `IsEditableSelection` before forwarding events
3. **Platform services** are registered in `Program.cs` based on OS
4. **Bitmap conversion** use `BitmapConverter` class

## Service Lifetimes

- `IOverlayWindow` - **Transient** (new instance each time)
- `IElementDetector` - **Transient** (stateful)
- `IScreenCaptureStrategy` - **Singleton** (stateless)
- `IOverlayRenderer` - **Singleton** (stateless)
- `IClipboardStrategy` - **Singleton** (stateless)
- `IOverlayController` - **Singleton** (manages state)
