# Overlay System Architecture

## Overview

The overlay system in AGI.Captor is responsible for creating transparent overlay windows that allow users to capture screenshots and add annotations. The system has been refactored to use a simplified architecture based on dependency injection (DI) without factory patterns.

## Architecture Components

### 1. Core Interfaces

#### IOverlayController
- **Implementation**: `SimplifiedOverlayManager`
- **Purpose**: Manages the lifecycle of overlay windows across all screens
- **Key Methods**:
  - `ShowAll()`: Creates and displays overlay windows on all screens
  - `CloseAll()`: Closes all overlay windows
  - `IsActive`: Property indicating if any overlay windows are active

#### IOverlayWindow
- **Implementations**: 
  - `WindowsOverlayWindow` (Windows platform)
  - `MacOverlayWindow` (macOS platform)
- **Purpose**: Platform-specific wrapper for the actual overlay window
- **Key Features**:
  - Manages window lifecycle
  - Handles platform-specific screen enumeration
  - Translates events between internal and external formats

#### IScreenCaptureStrategy
- **Implementations**:
  - `WindowsScreenCaptureStrategy` (uses Win32 BitBlt)
  - `MacScreenCaptureStrategy` (uses screencapture command)
- **Purpose**: Platform-specific screen capture implementation
- **Key Methods**:
  - `CaptureRegionAsync(PixelRect region)`
  - `CaptureWindowRegionAsync(Rect windowRect, Visual window)`
  - `CaptureFullScreenAsync(Screen screen)`
  - `CaptureWindowAsync(IntPtr windowHandle)`
  - `CaptureElementAsync(IElementInfo element)`

#### IClipboardStrategy
- **Implementations**:
  - `WindowsClipboardStrategy` (uses Win32 clipboard API)
  - `MacClipboardStrategy` (uses Avalonia clipboard API, planned NSPasteboard)
- **Purpose**: Platform-specific clipboard operations
- **Key Methods**:
  - `SetImageAsync(SKBitmap bitmap)`
  - `SetTextAsync(string text)`
  - `GetImageAsync()`
  - `GetTextAsync()`

#### IElementDetector
- **Implementations**:
  - `WindowsElementDetector` (uses UI Automation)
  - `NullElementDetector` (for unsupported platforms)
- **Purpose**: Detects UI elements at screen coordinates
- **Key Methods**:
  - `DetectElementAt(int x, int y, IntPtr ignoreWindow)`
  - `ToggleDetectionMode()`

#### IOverlayRenderer
- **Implementation**: `WindowsOverlayRenderer` (used on all platforms)
- **Purpose**: Renders visual elements on the overlay
- **Key Methods**:
  - `RenderSelectionBox()`
  - `RenderElementHighlight()`
  - `RenderCrosshair()`
  - `RenderMagnifier()`

### 2. Service Registration (Program.cs)

```csharp
// Register platform-specific services
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddTransient<IOverlayWindow, WindowsOverlayWindow>();
    builder.Services.AddTransient<IElementDetector, WindowsElementDetector>();
    builder.Services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>();
    builder.Services.AddSingleton<IOverlayRenderer, WindowsOverlayRenderer>();
    builder.Services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>();
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    builder.Services.AddTransient<IOverlayWindow, MacOverlayWindow>();
    builder.Services.AddTransient<IElementDetector, NullElementDetector>();
    builder.Services.AddSingleton<IScreenCaptureStrategy, MacScreenCaptureStrategy>();
    builder.Services.AddSingleton<IOverlayRenderer, WindowsOverlayRenderer>();
    builder.Services.AddSingleton<IClipboardStrategy, MacClipboardStrategy>();
}

// Register the overlay manager
builder.Services.AddSingleton<IOverlayController, SimplifiedOverlayManager>();
```

### 3. Event Flow

#### Selection Events

1. **User creates selection** → `OverlayWindow` raises `RegionSelected` event
2. **Event contains**:
   - `Region`: The selected area
   - `IsFullScreen`: Whether it's a full screen capture
   - `DetectedElement`: Element info if available
   - `IsEditableSelection`: Whether this is for annotation (important!)

3. **Platform wrapper** (`WindowsOverlayWindow`/`MacOverlayWindow`):
   - Checks if `IsEditableSelection` is true
   - If true: **Does NOT forward the event** (keeps overlay open for annotation)
   - If false: Converts to `CaptureRegionEventArgs` and forwards to `SimplifiedOverlayManager`

4. **SimplifiedOverlayManager**:
   - Captures the region using `IScreenCaptureStrategy`
   - Copies to clipboard using `IClipboardStrategy`
   - Calls `CloseAll()` to close all overlay windows

#### Cancellation Events

1. **User presses ESC** → `OverlayWindow` raises `Cancelled` event
2. **Platform wrapper** forwards event to `SimplifiedOverlayManager`
3. **SimplifiedOverlayManager** calls `CloseAll()`

#### Export Events

1. **User exports annotated image** → `OverlayWindow.HandleExportRequest()`
2. After successful export:
   - Gets `IOverlayController` from DI container
   - Calls `CloseAll()` to close all overlay windows

### 4. Key Design Decisions

#### Direct DI Instead of Factory Pattern
- **Why**: Simpler, more maintainable code
- **How**: Platform-specific services are registered directly in the DI container
- **Benefit**: Standard DI patterns, easier testing, less abstraction

#### Editable Selection Handling
- **Problem**: Creating an editable selection was closing the overlay
- **Solution**: Added `IsEditableSelection` flag to prevent event forwarding
- **Result**: Overlay stays open for annotation, closes only on final action

#### Centralized Window Management
- **All windows close together**: Ensures consistent behavior across multi-monitor setups
- **Single responsibility**: `SimplifiedOverlayManager` handles all window lifecycle
- **Event-driven**: Windows communicate through events, not direct calls

### 5. Platform Differences

#### Windows
- **Screen enumeration**: Uses temporary anchor window
- **Element detection**: Full UI Automation support
- **Screen capture**: Win32 BitBlt API
- **Clipboard**: Win32 clipboard API

#### macOS
- **Screen enumeration**: Uses primary window reference
- **Element detection**: Not supported (uses NullElementDetector)
- **Screen capture**: screencapture command (planned: CGWindowListCreateImage)
- **Clipboard**: Avalonia API (planned: NSPasteboard)

### 6. File Structure

```
src/AGI.Captor.Desktop/
├── Overlays/
│   ├── OverlayWindow.axaml.cs          # Main overlay window UI
│   ├── RegionSelectedEventArgs.cs      # Event args for selection
│   └── ...
├── Services/
│   └── Overlay/
│       ├── SimplifiedOverlayManager.cs # Main overlay controller
│       ├── IOverlayWindow.cs          # Window interface
│       ├── IScreenCaptureStrategy.cs  # Capture interface
│       ├── IClipboardStrategy.cs      # Clipboard interface
│       ├── BitmapConverter.cs         # SKBitmap ↔ Avalonia.Bitmap
│       └── Platforms/
│           ├── WindowsOverlayWindow.cs
│           ├── MacOverlayWindow.cs
│           ├── WindowsScreenCaptureStrategy.cs
│           ├── MacScreenCaptureStrategy.cs
│           ├── WindowsClipboardStrategy.cs
│           ├── MacClipboardStrategy.cs
│           └── NullElementDetector.cs
```

### 7. Common Issues and Solutions

#### Issue: Overlay closes when creating editable selection
- **Cause**: RegionSelected event was always forwarded
- **Fix**: Check `IsEditableSelection` flag before forwarding

#### Issue: Only current screen overlay closes on export
- **Cause**: Called `Close()` on current window only
- **Fix**: Use `IOverlayController.CloseAll()`

#### Issue: Multiple screens show different behaviors
- **Cause**: Windows were managed independently
- **Fix**: Centralized management in `SimplifiedOverlayManager`

### 8. Future Improvements

1. **Native macOS Implementation**:
   - Replace screencapture command with CGWindowListCreateImage
   - Implement NSPasteboard for clipboard
   - Add element detection using Accessibility API

2. **Linux Support**:
   - Add X11/Wayland screen capture
   - Implement clipboard support
   - Add basic window detection

3. **Performance Optimizations**:
   - Cache screen information
   - Optimize bitmap conversions
   - Reduce memory allocations

### 9. Testing Considerations

- **Unit Tests**: Mock all interfaces for isolated testing
- **Integration Tests**: Test platform-specific implementations
- **Multi-monitor Tests**: Ensure consistent behavior across screens
- **Event Flow Tests**: Verify correct event handling for all scenarios
