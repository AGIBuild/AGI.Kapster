# Change: Add Enterprise-Grade Screen Recording

## Why

Currently, AGI.Kapster only supports static screenshot capture and annotation. Users increasingly need to record dynamic screen activities (tutorials, bug reproductions, demos) alongside screenshot capabilities. Adding professional screen recording would:

1. **Complete the capture toolkit**: Users can choose between static (screenshot) and dynamic (video) capture
2. **Increase product value**: Transform AGI.Kapster from a screenshot tool to a comprehensive screen capture suite
3. **Competitive advantage**: Most screenshot tools lack integrated video recording with similar quality
4. **User workflow efficiency**: Single tool for all screen capture needs (no switching between apps)

Market research shows that 60%+ of screenshot tool users also use separate screen recording tools, indicating clear demand for integrated functionality.

## What Changes

This change introduces a comprehensive screen recording capability with the following features:

### Core Recording Features
- **Multi-format recording**: Record to MP4 (H.264), WebM (VP9), MKV, and animated GIF
- **Flexible capture modes**: Full screen, specific monitor, or custom region selection
- **Audio capture**: System audio, microphone, or mixed audio recording
- **High-quality encoding**: Configurable bitrate, frame rate (30/60 FPS), and resolution
- **Performance optimization**: Hardware acceleration support (NVENC, AMD VCE, Intel QSV)

### Advanced Features
- **Real-time preview**: Live preview window during recording
- **Pause/Resume**: Pause recording and resume without creating separate files
- **Mouse effects**: Highlight cursor, show clicks, and click animations
- **Countdown timer**: 3-second countdown before recording starts
- **Annotation while recording**: Draw annotations in real-time during recording (optional)

### User Interface
- **Recording control panel**: Floating control panel with pause/stop/cancel buttons
- **System tray integration**: Recording status indicator and quick controls
- **Hotkeys**: `Alt+R` (start/stop), `Alt+P` (pause/resume), `Escape` (cancel)
- **Settings page**: Configure recording quality, formats, output directory, hotkeys

### Export and Post-Processing
- **Multiple output formats**: MP4, WebM, MKV, GIF with configurable quality
- **Basic editing**: Trim start/end, cut sections (optional for v1)
- **Compression options**: Balance between file size and quality
- **Auto-naming**: Timestamp-based automatic file naming

## Impact

### Affected Specs
- **NEW**: `screen-recording` - Comprehensive screen recording capability
- **MODIFIED**: `hotkey-management` - Add recording hotkeys (Alt+R, Alt+P)
- **MODIFIED**: `settings-management` - Add recording settings section
- **MODIFIED**: `overlay-system` - Extend overlay for region selection in recording mode

### Affected Code

#### New Components
- `src/AGI.Kapster.Desktop/Services/Recording/`
  - `IScreenRecordingService.cs` - Core recording service interface
  - `ScreenRecordingService.cs` - Implementation with FFmpeg integration
  - `IAudioCaptureService.cs` - Audio capture interface
  - `AudioCaptureService.cs` - System/microphone audio capture
  - `IVideoEncoderService.cs` - Video encoding interface
  - `VideoEncoderService.cs` - FFmpeg-based encoding
  - `RecordingSession.cs` - Recording session state management
  
- `src/AGI.Kapster.Desktop/Overlays/Recording/`
  - `RecordingControlPanel.axaml` - Floating control panel UI
  - `RecordingControlPanel.axaml.cs` - Control panel logic
  - `RecordingPreviewWindow.axaml` - Preview window (optional)
  - `RecordingOverlay.cs` - Recording region selection overlay
  
- `src/AGI.Kapster.Desktop/Models/Recording/`
  - `RecordingSettings.cs` - Recording configuration model
  - `RecordingFormat.cs` - Supported format enumeration
  - `RecordingQuality.cs` - Quality preset enumeration
  - `AudioSource.cs` - Audio source enumeration
  
- `src/AGI.Kapster.Desktop/ViewModels/Recording/`
  - `RecordingControlViewModel.cs` - Control panel view model
  - `RecordingSettingsViewModel.cs` - Settings page view model

#### Modified Components
- `src/AGI.Kapster.Desktop/Services/Hotkey/HotkeyManager.cs`
  - Add recording hotkey handlers (Alt+R, Alt+P)
  
- `src/AGI.Kapster.Desktop/Services/Settings/SettingsService.cs`
  - Add recording settings persistence
  
- `src/AGI.Kapster.Desktop/Models/Settings/AppSettings.cs`
  - Add `RecordingSettings` property
  
- `src/AGI.Kapster.Desktop/Views/SettingsWindow.axaml`
  - Add "Recording" tab with configuration options
  
- `src/AGI.Kapster.Desktop/Extensions/ServiceCollectionExtensions.cs`
  - Register recording services

#### External Dependencies (New)
- **FFmpeg.AutoGen** (LGPL 2.1+) - FFmpeg bindings for .NET
- **NAudio** (MIT) - Audio capture for Windows
- **PortAudio** (MIT) - Cross-platform audio library
- **FFmpeg native binaries** (~50MB, dynamically linked for LGPL compliance)

### Database/Storage Impact
- Add recording settings to `appsettings.json`:
  ```json
  "RecordingSettings": {
    "DefaultFormat": "MP4",
    "Quality": "High",
    "FrameRate": 30,
    "AudioEnabled": true,
    "AudioSource": "System",
    "OutputDirectory": "%USERPROFILE%/Videos/AGI.Kapster",
    "ShowMouseCursor": true,
    "HighlightClicks": true
  }
  ```

### Performance Impact
- **Memory**: Additional 100-300MB during recording (video buffer)
- **CPU**: 10-30% CPU usage (varies with encoding settings and hardware acceleration)
- **Disk I/O**: Continuous writes during recording (depends on quality)
- **Startup Time**: +200ms (FFmpeg library initialization)

### Breaking Changes
- **NONE** - This is purely additive functionality
- Existing screenshot and annotation features remain unchanged

### Testing Requirements
- **Unit Tests**: Recording service logic, encoding pipeline, audio capture
- **Integration Tests**: End-to-end recording workflow, format conversion
- **Platform Tests**: Verify on Windows, macOS, Linux
- **Performance Tests**: Frame rate stability, memory usage, encoding speed
- **Hardware Acceleration Tests**: NVENC, AMD VCE, Intel QSV validation

### Documentation Updates
- README.md: Add screen recording section with feature list and hotkeys
- User guide: Add recording tutorial with screenshots
- Developer guide: Architecture diagram for recording pipeline
- API documentation: Document recording service interfaces

### Timeline Estimate
- **Phase 1** (Weeks 1-3): Core recording infrastructure (FFmpeg integration, basic recording)
- **Phase 2** (Weeks 4-6): Audio capture and encoding pipeline
- **Phase 3** (Weeks 7-9): UI components (control panel, settings, preview)
- **Phase 4** (Weeks 10-11): Advanced features (pause/resume, mouse effects)
- **Phase 5** (Week 12): Testing, optimization, documentation
- **Total**: 12 weeks (3 months)

### Risk Assessment
- **Medium Risk**: FFmpeg integration complexity and LGPL license compliance
- **Medium Risk**: Cross-platform audio capture variations
- **Low Risk**: Performance impact on low-end hardware
- **Mitigation**: Extensive platform testing, hardware acceleration fallbacks, quality presets

