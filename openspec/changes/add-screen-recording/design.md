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

### Decision 2: Audio Capture Strategy

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

### Decision 3: Recording Architecture Pattern

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
- `RegionSelection` → User selecting recording area (reuses overlay)
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

### Decision 4: Frame Capture Strategy

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

### Decision 5: Hardware Acceleration

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

### Decision 6: UI Component Design

**Choice**: Minimal floating control panel with system tray integration

**Components**:

1. **Recording Control Panel** (Floating Window)
   - Position: Top-right corner, draggable
   - Content: Timer, pause/resume, stop buttons
   - Size: 200x80 pixels (compact)
   - Behavior: Always on top, semi-transparent when idle

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

### Decision 7: File Output Strategy

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

### Risk 1: FFmpeg Binary Size (~50MB)
**Impact**: Increases installer size by ~50MB

**Mitigation**:
- Separate FFmpeg download on first use (optional)
- Compress binaries in installer
- Document size increase in release notes

**Trade-off**: Accept larger installer for comprehensive codec support and hardware acceleration.

---

### Risk 2: LGPL License Compliance
**Impact**: FFmpeg is LGPL 2.1+, requires dynamic linking

**Mitigation**:
- Ship FFmpeg as separate native binaries (not statically linked)
- Document FFmpeg usage in LICENSE file
- Provide instructions for users to replace FFmpeg binaries

**Trade-off**: Accept LGPL dependency for best-in-class encoding. Alternative libraries have significant limitations.

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

