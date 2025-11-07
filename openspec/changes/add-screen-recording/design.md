# Design Document: Screen Recording

## Context

AGI.Kapster currently provides high-quality screenshot capture and annotation capabilities. This design document outlines the architecture for adding enterprise-grade screen recording functionality, transforming AGI.Kapster into a comprehensive screen capture suite.

### Background
- **User Research**: 60%+ of users request video recording alongside screenshots
- **Market Gap**: Most screenshot tools lack integrated recording or offer basic functionality
- **Technical Foundation**: Existing capture pipeline can be extended for video

### Constraints
- **License Compliance**: Must use open-source libraries (MIT/Apache/BSD preferred, LGPL acceptable with dynamic linking)
- **Performance**: Must not degrade existing screenshot performance
- **Cross-platform**: Must work on Windows, macOS, and Linux
- **User Experience**: Must match the simplicity of existing capture workflow

### Stakeholders
- **End Users**: Need reliable, high-quality screen recording
- **Developers**: Need maintainable, testable architecture
- **Project Maintainers**: Need minimal dependency footprint and license compliance

## Goals / Non-Goals

### Goals
- ✅ Professional-quality screen recording (30/60 FPS, configurable bitrate)
- ✅ Multiple output formats (MP4, WebM, MKV, GIF)
- ✅ Audio capture (system audio, microphone, or both)
- ✅ Real-time preview and pause/resume functionality
- ✅ Mouse cursor effects (highlighting, click animations)
- ✅ Cross-platform support with platform-optimized implementations
- ✅ Hardware acceleration where available (NVENC, AMD VCE, Intel QSV)
- ✅ Minimal UI with hotkey-driven workflow
- ✅ Integration with existing overlay system for region selection

### Non-Goals
- ❌ Advanced video editing (trim/cut/merge) - deferred to post-v1
- ❌ Live streaming capabilities - out of scope
- ❌ Webcam overlay - deferred to future version
- ❌ Real-time annotation during recording - optional for v1
- ❌ Cloud upload/sharing - out of scope
- ❌ Screen recording analytics/metrics - out of scope

## Technical Decisions

### Decision 1: Video Encoding Library

**Choice**: FFmpeg via `FFmpeg.AutoGen` bindings

**Alternatives Considered**:
1. **ScreenRecorderLib** (MIT)
   - ❌ Limited format support (Windows-focused)
   - ❌ Less flexible encoding options
   - ✅ Simpler integration
   
2. **MediaFoundation** (Windows built-in)
   - ❌ Windows-only
   - ❌ Limited codec support
   - ✅ No external dependencies
   
3. **FFmpeg.AutoGen** (LGPL)
   - ✅ Industry-standard, battle-tested
   - ✅ Extensive format and codec support
   - ✅ Hardware acceleration support
   - ✅ Cross-platform
   - ⚠️ LGPL requires dynamic linking (acceptable)
   - ⚠️ Larger dependency (~50MB binaries)

**Rationale**: FFmpeg provides the best balance of features, performance, and cross-platform support. LGPL is acceptable with dynamic linking (separate FFmpeg binaries). The 50MB dependency is justified by comprehensive codec support and hardware acceleration.

**Implementation Notes**:
- Use `FFmpeg.AutoGen` NuGet package for .NET bindings
- Ship FFmpeg native binaries separately (LGPL compliance)
- Implement lazy loading to avoid startup penalty
- Create abstraction layer for potential future library swaps

---

### Decision 2: FFmpeg Deployment and Version Management

**Choice**: Bundle matched-version FFmpeg binaries with application

**Version Compatibility**:
- `FFmpeg.AutoGen 7.0.0` requires `FFmpeg 7.0.x`
- Version mismatch causes ABI incompatibility, runtime crashes, or encoding errors
- Tight coupling between binding version and native library version

**Alternatives Considered**:

1. **Depend on system-installed FFmpeg**
   - ❌ Uncontrollable version - user's FFmpeg may not match
   - ❌ Poor user experience - requires manual installation
   - ❌ High support burden - "recording doesn't work" issues
   - ❌ Difficult to debug - environment differences
   - ✅ Smaller installer size

2. **Use third-party NuGet packages** (e.g., FFmpeg.Native)
   - ❌ Limited platform support (missing macOS ARM64)
   - ❌ Delayed updates from third-party maintainers
   - ⚠️ Still ~50-70MB download
   - ✅ Automated dependency management

3. **Bundle matched-version binaries** (Selected)
   - ✅ **Full version control** - dev/prod environments match
   - ✅ **Zero external dependencies** - works out-of-box
   - ✅ **Cross-platform consistency** - same version everywhere
   - ✅ **Easy testing** - test environment = production
   - ⚠️ Installer size +50-70MB per platform
   - ⚠️ Maintain multi-platform binaries

**Rationale**: Bundling binaries ensures version compatibility, eliminates user installation requirements, and provides consistent behavior across all platforms. The ~50-70MB size increase is acceptable for enterprise-grade reliability.

**Implementation Strategy**:

**Directory Structure**:
```
AGI.Kapster/
├── packaging/
│   └── ffmpeg/
│       ├── windows/
│       │   ├── x64/
│       │   │   ├── avcodec-61.dll
│       │   │   ├── avformat-61.dll
│       │   │   ├── avutil-59.dll
│       │   │   ├── swscale-8.dll
│       │   │   └── swresample-5.dll
│       │   └── arm64/ (future)
│       ├── macos/
│       │   ├── x64/
│       │   │   └── lib*.dylib files
│       │   └── arm64/
│       │       └── lib*.dylib files
│       └── linux/
│           └── x64/
│               └── lib*.so files
```

**FFmpeg Loader Implementation**:
```csharp
// Services/Recording/FFmpegLoader.cs
public static class FFmpegLoader
{
    private static bool _initialized = false;
    private static readonly object _lock = new();
    private const int RequiredMajorVersion = 61; // FFmpeg 7.x = libavcodec 61.x
    
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_initialized) return;
            
            // 1. Try bundled FFmpeg (primary)
            var bundledPath = GetBundledFFmpegPath();
            if (TryLoadFFmpeg(bundledPath, "bundled"))
            {
                _initialized = true;
                return;
            }
            
            // 2. Fallback to system FFmpeg (if version matches)
            var systemPath = FindSystemFFmpeg();
            if (systemPath != null && TryLoadFFmpeg(systemPath, "system"))
            {
                Log.Warning("Using system FFmpeg. Bundled version preferred.");
                _initialized = true;
                return;
            }
            
            throw new FFmpegNotFoundException(
                "FFmpeg 7.x not found. Recording features unavailable.");
        }
    }
    
    private static bool TryLoadFFmpeg(string path, string source)
    {
        if (!Directory.Exists(path)) return false;
        
        try
        {
            ffmpeg.RootPath = path;
            var codecVersion = ffmpeg.avcodec_version();
            var major = codecVersion >> 24;
            
            if (major != RequiredMajorVersion)
            {
                Log.Warning($"FFmpeg version mismatch at {path}: " +
                           $"found {major}.x, require {RequiredMajorVersion}.x");
                return false;
            }
            
            var versionInfo = Marshal.PtrToStringAnsi(ffmpeg.av_version_info());
            Log.Information($"FFmpeg loaded from {source}: {versionInfo}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to load FFmpeg from {path}");
            return false;
        }
    }
    
    private static string GetBundledFFmpegPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var platform = GetPlatform();
        var architecture = RuntimeInformation.ProcessArchitecture
            .ToString().ToLower();
        
        return Path.Combine(baseDir, "ffmpeg", platform, architecture);
    }
    
    private static string? FindSystemFFmpeg()
    {
        // Platform-specific system FFmpeg detection
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return FindWindowsSystemFFmpeg();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "/usr/lib/x86_64-linux-gnu"; // Common path
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "/usr/local/lib"; // Homebrew path
        return null;
    }
    
    private static string GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macos";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";
        throw new PlatformNotSupportedException();
    }
}
```

**Version Locking in Project**:
```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <FFmpegVersion>7.0.1</FFmpegVersion>
  <FFmpegAutoGenVersion>7.0.0</FFmpegAutoGenVersion>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="FFmpeg.AutoGen" 
                    Version="$(FFmpegAutoGenVersion)" />
</ItemGroup>
```

**CI/CD Validation**:
```powershell
# tools/validate-ffmpeg-versions.ps1
$autoGenVersion = "7.0.0"
$expectedFFmpegMajor = 61 # libavcodec major version for FFmpeg 7.x

# Validate bundled binaries match AutoGen version
foreach ($platform in @("windows", "macos", "linux")) {
    $binaryPath = "packaging/ffmpeg/$platform"
    if (Test-Path $binaryPath) {
        Write-Host "Validating FFmpeg binaries for $platform..."
        # Version validation logic here
    }
}
```

**Update Strategy**:
1. **Quarterly Review**: Check for FFmpeg.AutoGen updates
2. **Matching Download**: Get corresponding FFmpeg binaries for all platforms
3. **Regression Testing**: Full recording feature test suite
4. **Documentation**: Update LICENSE and version docs

**LGPL Compliance**:
```
AGI.Kapster/
├── LICENSE                          # Main project license
├── THIRD-PARTY-LICENSES/
│   └── FFMPEG-LICENSE.txt          # FFmpeg LGPL 2.1+ license
└── docs/
    └── ffmpeg-replacement-guide.md # User instructions
```

**Replacement Guide Template**:
```markdown
# Replacing FFmpeg Binaries

AGI.Kapster bundles FFmpeg 7.0.x for recording features.

## Version Requirements
- FFmpeg 7.0.x or 7.1.x compatible
- libavcodec major version 61

## Replacement Steps
1. Navigate to: `<install-dir>/ffmpeg/<platform>/<arch>/`
2. Backup existing binaries
3. Replace with compatible FFmpeg libraries
4. Restart AGI.Kapster

## Verification
Settings → Recording → "Test Recording" button
```

**Binary Size Impact**:
- Windows x64: ~60MB
- macOS ARM64: ~50MB
- macOS x64: ~55MB
- Linux x64: ~55MB
- **Per-platform installer**: +50-60MB
- **All-platform package**: +220MB (dev only)

---

### Decision 3: Audio Capture Strategy

**Choice**: Platform-specific implementations with unified interface

**Platform Implementations**:

| Platform | Library | Rationale |
|----------|---------|-----------|
| Windows | NAudio (MIT) | Native WASAPI support, excellent Windows integration |
| macOS | AVFoundation (native) | Built-in, no dependencies, high quality |
| Linux | PulseAudio/ALSA via PortAudio (MIT) | Cross-distro compatibility |

**Alternatives Considered**:
1. **Single cross-platform library** (e.g., OpenAL)
   - ❌ Lowest common denominator
   - ❌ Less optimal performance per platform
   
2. **FFmpeg audio capture**
   - ❌ More complex integration
   - ❌ Less granular control

**Rationale**: Platform-specific implementations provide best quality and performance. Unified `IAudioCaptureService` interface maintains clean architecture.

**Implementation Notes**:
```csharp
public interface IAudioCaptureService
{
    Task<bool> StartCapture(AudioSource source, AudioFormat format);
    Task StopCapture();
    event EventHandler<AudioDataEventArgs> AudioDataAvailable;
}
```

---

### Decision 4: Recording Architecture Pattern

**Choice**: State Machine with Event-Driven Pipeline

**Architecture**:
```
User Trigger (Alt+R)
    ↓
HotkeyManager → ScreenRecordingService
    ↓
RecordingSession (State Machine)
    ↓
┌─────────────────────────────────────┐
│  Recording Pipeline                  │
│                                     │
│  ScreenCapture → FrameBuffer       │
│  AudioCapture  → AudioBuffer       │
│                                     │
│  Encoder (FFmpeg)                  │
│    - Video encoding                 │
│    - Audio encoding                 │
│    - Muxing (combine A/V)          │
│                                     │
│  Output → File Writer              │
└─────────────────────────────────────┘
```

**State Machine States**:
- `Idle` → User not recording
- `RegionSelection` → User selecting recording area (independent per-screen recording overlay)
- `Countdown` → 3-second countdown before recording
- `Recording` → Active recording
- `Paused` → Recording paused
- `Encoding` → Finalizing video file
- `Completed` → Recording saved

**Event Flow**:
```csharp
public enum RecordingEvent
{
    StartRequested,      // User presses Alt+R
    RegionSelected,      // User completes region selection
    CountdownComplete,   // 3-second countdown finished
    PauseRequested,      // User presses Alt+P
    ResumeRequested,     // User presses Alt+P again
    StopRequested,       // User presses Alt+R or Stop button
    EncodingComplete,    // FFmpeg finishes encoding
    Error                // Any error occurred
}
```

**Rationale**: State machine provides clear control flow and error handling. Event-driven pipeline allows asynchronous processing without blocking UI.

---

### Decision 5: Frame Capture Strategy

**Choice**: Polling-based capture with adaptive frame rate

**Implementation**:
```csharp
while (state == RecordingState.Recording)
{
    var startTime = DateTime.UtcNow;
    
    // Capture frame
    var frame = await _captureStrategy.CaptureFrameAsync(recordingRegion);
    
    // Encode frame
    await _encoder.EncodeFrameAsync(frame, frameNumber++);
    
    // Adaptive timing
    var elapsed = DateTime.UtcNow - startTime;
    var targetFrameTime = 1000 / targetFrameRate;
    var delay = Math.Max(0, targetFrameTime - elapsed.TotalMilliseconds);
    
    await Task.Delay(TimeSpan.FromMilliseconds(delay));
}
```

**Alternatives Considered**:
1. **Event-based capture** (screen invalidation)
   - ❌ Not supported on all platforms
   - ❌ Unpredictable frame timing
   
2. **Fixed-interval timer**
   - ❌ Doesn't adapt to encoding performance
   - ❌ Can cause frame drops on slow systems

**Rationale**: Polling with adaptive delay provides consistent frame rate while adapting to system performance.

---

### Decision 6: Hardware Acceleration

**Choice**: Automatic detection with graceful fallback

**Acceleration Support**:
| GPU Vendor | Technology | Supported Codecs |
|------------|-----------|------------------|
| NVIDIA | NVENC | H.264, HEVC |
| AMD | VCE/AMF | H.264, HEVC |
| Intel | Quick Sync | H.264, HEVC |
| Software | libx264 | H.264 (fallback) |

**Detection Logic**:
```csharp
public VideoEncoderType DetectBestEncoder()
{
    // 1. Try NVENC (NVIDIA)
    if (IsNvencAvailable()) return VideoEncoderType.Nvenc;
    
    // 2. Try AMD VCE
    if (IsAmdVceAvailable()) return VideoEncoderType.AmdVce;
    
    // 3. Try Intel Quick Sync
    if (IsIntelQsvAvailable()) return VideoEncoderType.IntelQsv;
    
    // 4. Fallback to software encoding
    return VideoEncoderType.Software;
}
```

**Rationale**: Hardware acceleration significantly reduces CPU usage (70-90% reduction). Automatic detection ensures optimal performance without user configuration. Software fallback ensures compatibility.

---

### Decision 7: UI Component Design

**Choice**: Minimal floating control panel with system tray integration

**Components**:

1. **Recording Control Panel** (Floating Window)
   - Position: Top-right corner, draggable
   - Content: Timer, pause/resume, stop buttons
   - Size: 200x80 pixels (compact)
   - Behavior: Always on top; never overlaps the recording ROI; auto-reposition to a safe area or another screen; if ROI covers all available space, fallback to tray-only; semi-transparent when idle (only when outside ROI)

2. **System Tray Indicator**
   - Icon: Animated red dot during recording
   - Context Menu: Pause, Stop, Cancel, Settings
   - Notification: "Recording started" toast

3. **Settings Page** (New Tab in SettingsWindow)
   - Sections: Format, Quality, Audio, Mouse Effects, Hotkeys
   - Live preview: Show example recording settings

**Alternatives Considered**:
1. **Full-screen overlay with controls**
   - ❌ Intrusive, blocks screen content
   - ❌ Captured in recording (unwanted)

2. **No UI during recording**
   - ❌ No visual feedback
   - ❌ Difficult to pause/stop

**Rationale**: Floating panel provides clear feedback without blocking content. System tray integration matches existing app architecture.

---

### Decision 8: File Output Strategy

**Choice**: Streaming write with atomic finalization

**Implementation**:
```csharp
// Stream to temporary file during recording
var tempFile = Path.GetTempFileName() + ".mp4.tmp";
using (var stream = File.OpenWrite(tempFile))
{
    await _encoder.EncodeAsync(stream, frames, audioSamples);
}

// Atomic rename on completion
var finalFile = GenerateOutputPath(settings.OutputDirectory);
File.Move(tempFile, finalFile);
```

**Rationale**: 
- Streaming write prevents memory exhaustion on long recordings
- Temporary file prevents corrupted output on crashes
- Atomic rename ensures file consistency

---

### Decision 9: Recording Overlay — Independent and Persistent (Per-Screen)

**Choice**: Independent per-screen overlay that persists during recording. After recording starts, only an outer border is rendered; the capture region (ROI) never contains overlay pixels.

**Behaviors**:
- Per-screen transparent topmost windows; selection stage has no toolbar/annotation.
- Recording stage keeps overlays alive but shows only an outer border; no interactive UI.
- Border is drawn strictly outside ROI with thickness t (2–3 px, DPI-aware), never overlapping ROI.

**Cross-Platform**:
- Windows: per-monitor window; prefer `WDA_EXCLUDEFROMCAPTURE` when available; otherwise rely on outer-border guarantee.
- macOS: one NSWindow per NSScreen; full-screen transparent window; outer-border guarantee。
- Linux: top-level transparent per output; outer-border guarantee（不依赖合成器排除特性）。

**Lifecycle**: Start → Selecting → Confirm → Recording (persistent border) → Stop/Dispose. Hotkeys control the recording service; overlays remain passive.

**Validation**: Pixel test（ROI 内不存在边框色）+ 多屏/混合 DPI 手工验证。

**Deliverables**:
- Namespace: `AGI.Kapster.Desktop.RecordingOverlays/*`
- Components: `RecordingOverlayWindow` (per screen), `RecordingSelectionOverlay` (selection only), `RecordingBorderOverlay` (recording border)
- Coordinators: `IRecordingOverlayCoordinator` + Win/Mac/Linux platform implementations

---

## Data Models

### RecordingSettings
```csharp
public class RecordingSettings
{
    public RecordingFormat Format { get; set; } = RecordingFormat.MP4;
    public RecordingQuality Quality { get; set; } = RecordingQuality.High;
    public int FrameRate { get; set; } = 30; // 30 or 60 FPS
    public bool AudioEnabled { get; set; } = true;
    public AudioSource AudioSource { get; set; } = AudioSource.System;
    public string OutputDirectory { get; set; } = DefaultOutputDirectory;
    public bool ShowMouseCursor { get; set; } = true;
    public bool HighlightClicks { get; set; } = true;
    public bool UseHardwareAcceleration { get; set; } = true;
    public int CountdownSeconds { get; set; } = 3;
}

public enum RecordingFormat
{
    MP4,    // H.264 in MP4 container
    WebM,   // VP9 in WebM container
    MKV,    // H.264 in Matroska container
    GIF     // Animated GIF (lower quality, smaller size)
}

public enum RecordingQuality
{
    Low,      // 720p, 2 Mbps
    Medium,   // 1080p, 5 Mbps
    High,     // 1080p, 10 Mbps
    Ultra     // 1440p+, 20 Mbps (if display supports)
}

public enum AudioSource
{
    None,
    System,      // Desktop audio
    Microphone,  // Mic input
    Both         // Mix system + mic
}
```

### RecordingSession
```csharp
public class RecordingSession
{
    public Guid Id { get; init; }
    public RecordingState State { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public Rect RecordingRegion { get; set; }
    public RecordingSettings Settings { get; set; }
    public string OutputPath { get; set; }
    public int FramesRecorded { get; set; }
    public long BytesWritten { get; set; }
}
```

## Risks / Trade-offs

### Risk 1: FFmpeg Binary Size (~50-60MB per platform)
**Impact**: Increases installer size significantly per platform

**Per-Platform Binary Sizes**:
- Windows x64: ~60MB (5 DLL files)
- macOS ARM64: ~50MB (dylib files)
- macOS x64: ~55MB (dylib files)
- Linux x64: ~55MB (shared objects)

**Installer Impact**:
- Single-platform installer: +50-60MB
- Multi-platform package (dev): +220MB total
- After compression: -30% to -40% reduction

**Mitigation**:
- **Bundled approach** (recommended): Include FFmpeg in platform-specific installers
  - Windows MSI: Include only Windows x64 binaries (~60MB compressed to ~40MB)
  - macOS PKG: Include only macOS ARM64/x64 binaries (~50-55MB compressed to ~35MB)
  - Linux DEB/RPM: Include only Linux x64 binaries (~55MB compressed to ~38MB)
  
- **On-demand download** (alternative): Download FFmpeg on first recording use
  - ❌ Requires network connectivity
  - ❌ Poor offline experience
  - ❌ Additional error handling complexity
  - ✅ Smaller initial installer (~0MB FFmpeg)

- **Compression strategies**:
  - Use installer compression (LZMA, ZIP)
  - Strip debug symbols from FFmpeg builds (-30% size)
  - Include only essential codecs (H.264, VP9, AAC)

- **Documentation**:
  - Clearly state installer size in release notes
  - Explain size increase is for professional recording features
  - Provide "lite" version without recording (optional future work)

**Trade-off**: Accept +40-60MB compressed installer size per platform for comprehensive codec support, hardware acceleration, and zero-configuration user experience. Professional video recording justifies the size increase.

---

### Risk 2: LGPL License Compliance
**Impact**: FFmpeg is LGPL 2.1+, requires dynamic linking and license disclosure

**Mitigation Strategy**:

1. **Dynamic Linking (LGPL Requirement)**
   - FFmpeg libraries loaded at runtime via P/Invoke (FFmpeg.AutoGen)
   - No static linking - binaries remain separate
   - Users can replace FFmpeg binaries with compatible versions

2. **License Documentation**
   - Include `THIRD-PARTY-LICENSES/FFMPEG-LICENSE.txt` with full LGPL 2.1+ text
   - Add FFmpeg attribution to main LICENSE file
   - Document FFmpeg usage in About dialog and README

3. **User Replacement Instructions**
   - Provide `docs/ffmpeg-replacement-guide.md` with step-by-step instructions
   - Document version compatibility requirements (FFmpeg 7.0.x)
   - Include verification steps to test custom FFmpeg builds

4. **Binary Distribution**
   - FFmpeg binaries in separate `ffmpeg/` subdirectory (not embedded in .exe)
   - Clear separation between AGI.Kapster code and FFmpeg libraries
   - Installer creates distinct directory structure

5. **Source Code Availability**
   - Link to FFmpeg source: https://github.com/FFmpeg/FFmpeg
   - Link to FFmpeg.AutoGen source: https://github.com/Ruslan-B/FFmpeg.AutoGen
   - Document exact FFmpeg version used (e.g., 7.0.1)

**Compliance Checklist**:
- [ ] FFmpeg binaries dynamically loaded (not statically linked)
- [ ] LGPL license text included in distribution
- [ ] FFmpeg version and source clearly documented
- [ ] User replacement instructions provided
- [ ] Binary directory structure allows easy replacement
- [ ] Legal review completed (if required by organization)

**Trade-off**: Accept LGPL dependency for best-in-class encoding. Alternative libraries have significant limitations. Dynamic linking ensures full compliance while maintaining enterprise-grade functionality.

---

### Risk 3: Cross-Platform Audio Capture Complexity
**Impact**: Different audio APIs per platform increase maintenance

**Mitigation**:
- Abstract behind `IAudioCaptureService` interface
- Extensive platform-specific testing
- Graceful degradation if audio fails (video-only recording)

**Trade-off**: Accept complexity for optimal audio quality on each platform.

---

### Risk 4: Performance on Low-End Hardware
**Impact**: Recording may drop frames or stutter on old/slow systems

**Mitigation**:
- Hardware acceleration reduces CPU load
- Quality presets allow users to choose performance
- Real-time frame drop detection and warning
- Fallback to lower frame rate if needed

**Trade-off**: Accept that ultra-high quality may not work on all hardware.

---

### Risk 5: Memory Usage During Recording
**Impact**: Additional 100-300MB memory during recording

**Mitigation**:
- Stream frames directly to encoder (no full buffer)
- Configurable buffer size based on available memory
- Release buffers immediately after encoding

**Trade-off**: Memory usage is acceptable for video recording workload.

---

## Migration Plan

### Phase 1: Infrastructure (Weeks 1-3)
1. Add FFmpeg.AutoGen NuGet package
2. Create `IScreenRecordingService` interface
3. Implement basic recording pipeline (no audio)
4. Add unit tests for encoding logic

### Phase 2: Audio Integration (Weeks 4-6)
1. Implement `IAudioCaptureService` for each platform
2. Add audio/video synchronization
3. Test audio capture across platforms

### Phase 3: UI Components (Weeks 7-9)
1. Create `RecordingControlPanel` UI
2. Add recording settings tab
3. Implement system tray integration
4. Add countdown timer overlay

### Phase 4: Advanced Features (Weeks 10-11)
1. Implement pause/resume functionality
2. Add mouse cursor highlighting
3. Add real-time preview (optional)
4. Implement GIF export

### Phase 5: Testing & Polish (Week 12)
1. Comprehensive platform testing
2. Performance optimization
3. Hardware acceleration testing
4. Documentation and examples

### Rollback Strategy
- Feature flag: `EnableScreenRecording` (default: true in v2.0+)
- If critical bugs found: disable via configuration
- Recording services are isolated, can be removed without affecting core features

---

## Open Questions

1. **GIF Export Quality**: Should we support high-quality GIF (larger files) or optimize for size?
   - **Recommendation**: Offer both as separate quality options

2. **Real-time Annotation**: Should v1 support drawing annotations during recording?
   - **Recommendation**: Defer to v1.1, focus on core recording first

3. **Webcam Overlay**: Should we support webcam picture-in-picture?
   - **Recommendation**: Defer to v2.0, significant scope increase

4. **Cloud Storage**: Should we integrate with cloud storage (OneDrive, Google Drive)?
   - **Recommendation**: Out of scope, users can manually upload

5. **Trim/Edit**: Should we support basic video editing post-recording?
   - **Recommendation**: Defer to v1.1, use external tools for now

---

## Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Frame Rate Stability | >95% of target FPS | Monitor dropped frames |
| CPU Usage (with HW accel) | <15% on modern CPU | Task Manager during recording |
| CPU Usage (software) | <40% on modern CPU | Task Manager during recording |
| Memory Usage | <300MB additional | Memory profiler |
| Encoding Latency | <2 seconds post-stop | Measure file finalization time |
| File Size Efficiency | <50MB per minute (High quality) | Validate output file sizes |

---

## Success Criteria

✅ **Functional**:
- Record full screen and custom regions
- Output to MP4, WebM, MKV, GIF
- Capture system audio, microphone, or both
- Pause/resume without file splits
- Mouse cursor highlighting works

✅ **Performance**:
- Achieve >95% target frame rate on modern hardware
- Hardware acceleration works on NVIDIA/AMD/Intel GPUs
- <300MB memory overhead during recording

✅ **Quality**:
- No visual artifacts or frame drops at High quality preset
- Audio sync within ±50ms throughout recording
- Output files playable in all major media players

✅ **Usability**:
- Recording starts within 5 seconds of hotkey press
- Control panel is intuitive and responsive
- Settings are clear and well-documented

✅ **Compatibility**:
- Works on Windows 10+, macOS 10.15+, Linux (Ubuntu 20.04+)
- All three platforms tested and validated

