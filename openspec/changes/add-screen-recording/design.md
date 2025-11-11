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

### Decision 2: Screen Capture Technology per Platform

**Choice**: Platform-specific native APIs with graceful fallbacks

**Platform Matrix**:

| Platform | Primary API | Fallback API | Features |
|----------|-------------|--------------|----------|
| Windows 11/10 1903+ | **Windows.Graphics.Capture** | GDI+ BitBlt | GPU acceleration, auto-DPI, window exclusion |
| Windows 10 < 1903 | GDI+ BitBlt | N/A | Software capture, manual DPI handling |
| macOS 12.3+ | **ScreenCaptureKit** | AVFoundation | Native audio, GPU textures, privacy controls |
| macOS 10.15-12.2 | AVFoundation AVCaptureScreen | CGDisplayStream | Microphone only, software capture |
| Linux Wayland | **xdg-desktop-portal** | X11 fallback | User permission dialog, secure capture |
| Linux X11 | X11 XGetImage/XShm | N/A | Direct capture, no permissions |

**Rationale**: 
- Modern Windows.Graphics.Capture provides zero-copy GPU path to hardware encoders (NVENC/QSV)
- macOS ScreenCaptureKit is the only way to capture system audio without kernel extensions
- Linux Wayland mandates portal usage (security requirement)
- Fallbacks ensure compatibility with older systems

**Implementation Notes**:
```csharp
public interface IScreenCaptureStrategy
{
    bool SupportsGpuAcceleration { get; }
    bool SupportsSystemAudio { get; }
    bool RequiresPermissions { get; }
    Task<CaptureCapabilities> DetectCapabilitiesAsync();
}

#if WINDOWS10_0_17763_0_OR_GREATER
public class WindowsGraphicsCaptureStrategy : IScreenCaptureStrategy
{
    // Direct3D11CaptureFrame -> ID3D11Texture2D -> Hardware Encoder
    public bool SupportsGpuAcceleration => true;
}
#endif
```

---

### Decision 3: FFmpeg Deployment and Version Management

**Choice**: Dynamic download on first use with local caching (changed from bundling)

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

2. **Bundle matched-version binaries in installer**
   - ❌ Installer size +50-70MB per platform (~200-300MB total)
   - ❌ macOS App Store limits binary size
   - ❌ Every FFmpeg update requires full app repackaging
   - ❌ All users download binaries even if never recording
   - ✅ Zero network dependency after install
   - ✅ Works offline immediately

3. **Use third-party NuGet packages** (e.g., FFmpeg.Native)
   - ❌ Limited platform support (missing macOS ARM64)
   - ❌ Delayed updates from third-party maintainers
   - ⚠️ Still ~50-70MB download
   - ✅ Automated dependency management

4. **Dynamic download on first use** (Selected)
   - ✅ **Small initial installer** - download only when needed
   - ✅ **Easy updates** - update FFmpeg independently of app
   - ✅ **Version pinning** - download exact required version
   - ✅ **CDN delivery** - fast, reliable downloads
   - ✅ **Offline mode** - cache locally after first download
   - ⚠️ Requires network on first recording
   - ⚠️ Need CDN infrastructure or GitHub Releases hosting

**Rationale**: Dynamic download provides the best balance:
- Keeps installer small (~5MB app vs ~200MB with bundled FFmpeg)
- Only users who record pay the download cost
- FFmpeg security updates don't require full app re-release
- Can leverage GitHub Releases or CDN for reliable delivery
- Cached locally after first download for offline use

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
    private const string FFmpegVersion = "7.0.2";
    private const string DownloadBaseUrl = "https://github.com/AGI-Build/AGI.Kapster/releases/download/ffmpeg-7.0.2";
    
    public static async Task<bool> EnsureFFmpegAsync(IProgress<double>? progress = null)
    {
        lock (_lock)
        {
            if (_initialized) return true;
        }
        
        // 1. Try cached local FFmpeg
        var cachedPath = GetCachedFFmpegPath();
        if (TryLoadFFmpeg(cachedPath, "cached"))
        {
            lock (_lock) { _initialized = true; }
            return true;
        }
        
        // 2. Try system FFmpeg (if version matches)
        var systemPath = FindSystemFFmpeg();
        if (systemPath != null && TryLoadFFmpeg(systemPath, "system"))
        {
            Log.Information("Using system FFmpeg (version matched)");
            lock (_lock) { _initialized = true; }
            return true;
        }
        
        // 3. Download FFmpeg binaries
        Log.Information("FFmpeg not found locally. Downloading version {Version}...", FFmpegVersion);
        try
        {
            await DownloadFFmpegAsync(cachedPath, progress);
            
            if (TryLoadFFmpeg(cachedPath, "downloaded"))
            {
                lock (_lock) { _initialized = true; }
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download FFmpeg");
        }
        
        Log.Error("FFmpeg 7.x unavailable. Recording features disabled.");
        return false;
    }
    
    private static async Task DownloadFFmpegAsync(string targetPath, IProgress<double>? progress)
    {
        var platform = GetPlatform();
        var arch = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
        var fileName = $"ffmpeg-{FFmpegVersion}-{platform}-{arch}.zip";
        var downloadUrl = $"{DownloadBaseUrl}/{fileName}";
        
        Directory.CreateDirectory(targetPath);
        
        using var client = new HttpClient();
        using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        
        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var downloadedBytes = 0L;
        
        var zipPath = Path.Combine(Path.GetTempPath(), fileName);
        await using (var fileStream = File.Create(zipPath))
        await using (var downloadStream = await response.Content.ReadAsStreamAsync())
        {
            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await downloadStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                downloadedBytes += bytesRead;
                progress?.Report((double)downloadedBytes / totalBytes);
            }
        }
        
        // Extract ZIP to target path
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, targetPath);
        File.Delete(zipPath);
        
        Log.Information("FFmpeg downloaded and extracted to {Path}", targetPath);
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
    
    private static string GetCachedFFmpegPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var platform = GetPlatform();
        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
        
        return Path.Combine(appDataPath, "AGI.Kapster", "ffmpeg", FFmpegVersion, platform, architecture);
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

| Platform | Library | Rationale | Limitations |
|----------|---------|-----------|-------------|
| Windows 10+ | NAudio (MIT) | Native WASAPI support, excellent Windows integration | None |
| macOS 12.3+ | ScreenCaptureKit (System) | **Native system audio capture**, zero config | Requires macOS 12.3+ |
| macOS 10.15-12.2 | AVFoundation (System) | Microphone only, stable API | **No system audio** without BlackHole |
| Linux (Modern) | PipeWire (System) | **Modern audio stack**, Wayland support, low latency | Requires PipeWire-enabled distro |
| Linux (Legacy) | PulseAudio (System) | Broad compatibility, X11/Wayland | Higher latency than PipeWire |
| Linux (Fallback) | ALSA (System) | Universal availability | Conflicts with other audio apps |

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

### Decision 4: Codec Selection and License Compliance

**Choice**: Hardware encoders + OpenH264 for software fallback (avoiding GPL x264)

**License Risk Analysis**:

| Codec | License | Quality | Speed | Risk Level |
|-------|---------|---------|-------|------------|
| **libx264** | **GPL** | Excellent | Fast | ❌ HIGH - GPL contamination |
| **libx265** | **GPL** | Excellent | Medium | ❌ HIGH - GPL contamination |
| OpenH264 (Cisco) | BSD | Good | Medium | ✅ LOW - BSD compatible |
| libvpx (VP9) | BSD | Excellent | Slow | ✅ LOW - BSD compatible |
| NVENC (H.264/HEVC) | Proprietary | Excellent | Very Fast | ✅ LOW - Hardware, no linking |
| Intel QSV (H.264/HEVC) | Proprietary | Good | Very Fast | ✅ LOW - Hardware, no linking |
| AMD VCE (H.264/HEVC) | Proprietary | Good | Very Fast | ✅ LOW - Hardware, no linking |

**Selected Encoding Priority**:
1. **Hardware Encoders** (NVENC/QSV/VCE) - No GPL risk, best performance
2. **OpenH264** (BSD) - Software fallback, LGPL-compatible
3. **libvpx VP9** (BSD) - WebM format, LGPL-compatible

**Rationale**:
- libx264 (GPL) would force entire application to GPL, incompatible with proprietary code
- Hardware encoders use driver APIs, no library linking, no license issues
- OpenH264 quality sufficient for screen recording (not video production)
- Most users (>70%) have GPU with hardware encoder

**FFmpeg Build Requirements**:
```bash
# Build FFmpeg WITHOUT GPL codecs
./configure \
  --enable-gpl=no \
  --enable-version3=no \
  --enable-libopenh264 \
  --enable-libvpx \
  --enable-nvenc \
  --enable-qsv \
  --enable-amf \
  --disable-libx264 \
  --disable-libx265 \
  --license=lgpl

# Verify no GPL contamination
./ffmpeg -version | grep "configuration:"
# Should NOT contain: --enable-gpl, --enable-libx264, --enable-libx265
```

**Implementation**:
```csharp
public class CodecSelector
{
    public string SelectEncoder(RecordingFormat format, HardwareAcceleration hwAccel)
    {
        return (format, hwAccel) switch
        {
            (RecordingFormat.MP4, HardwareAcceleration.NVENC) => "h264_nvenc",
            (RecordingFormat.MP4, HardwareAcceleration.QSV) => "h264_qsv",
            (RecordingFormat.MP4, HardwareAcceleration.AMF) => "h264_amf",
            (RecordingFormat.MP4, _) => "libopenh264",  // ✅ BSD, NOT x264
            
            (RecordingFormat.WebM, _) => "libvpx-vp9",  // ✅ BSD
            
            _ => throw new NotSupportedException($"Format {format} not supported")
        };
    }
}
```

---

### Decision 5: Recording Architecture Pattern

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
- Linux: top-level transparent per output; outer-border guarantee (does not rely on compositor exclusion features).

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

### Decision 6: Resource Monitoring and Error Recovery

**Choice**: Proactive resource checks with graceful degradation

**Resource Risks**:
- **Memory**: 1080p 60FPS requires ~475 MB/s uncompressed
- **Disk I/O**: Continuous writes at 5-20 Mbps (depending on quality)
- **CPU**: Real-time encoding under time pressure
- **GPU**: Hardware encoder can crash or become unavailable

**Implementation**:

```csharp
public class RecordingResourceMonitor
{
    private readonly ILogger<RecordingResourceMonitor> _logger;
    
    public ResourceCheckResult CheckResourcesBeforeRecording(RecordingSettings settings)
    {
        var result = new ResourceCheckResult();
        
        // 1. Check available memory
        var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var estimatedUsage = EstimateMemoryUsage(settings);
        
        if (availableMemory < estimatedUsage + 500 * 1024 * 1024) // +500MB buffer
        {
            result.AddError("Insufficient memory. Close other applications.");
            return result;
        }
        
        if (availableMemory < estimatedUsage + 1024 * 1024 * 1024) // +1GB buffer
        {
            result.AddWarning("Low memory. Recording may be unstable.");
        }
        
        // 2. Check disk space
        var outputDir = settings.OutputDirectory;
        var driveInfo = new DriveInfo(Path.GetPathRoot(outputDir));
        var estimatedFileSize = EstimateFileSize(settings, estimatedDuration: TimeSpan.FromMinutes(10));
        
        if (driveInfo.AvailableFreeSpace < estimatedFileSize + 1024 * 1024 * 1024) // +1GB buffer
        {
            result.AddError($"Insufficient disk space. Need at least {(estimatedFileSize + 1024 * 1024 * 1024) / 1024 / 1024 / 1024}GB free.");
            return result;
        }
        
        if (driveInfo.AvailableFreeSpace < 5L * 1024 * 1024 * 1024) // <5GB
        {
            result.AddWarning($"Low disk space: {driveInfo.AvailableFreeSpace / 1024 / 1024 / 1024}GB remaining.");
        }
        
        // 3. Check CPU usage
        var cpuUsage = GetCurrentCpuUsage();
        if (cpuUsage > 80)
        {
            result.AddWarning($"High CPU usage ({cpuUsage}%). Recording may drop frames.");
        }
        
        // 4. Validate hardware encoder
        if (settings.UseHardwareAcceleration)
        {
            var encoderAvailable = CheckHardwareEncoder(settings.Format);
            if (!encoderAvailable)
            {
                result.AddWarning("Hardware encoder unavailable. Using software encoding (slower).");
                settings.UseHardwareAcceleration = false; // Auto-fallback
            }
        }
        
        return result;
    }
    
    // Runtime monitoring during recording
    public async Task MonitorDuringRecordingAsync(RecordingSession session, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(5000, ct); // Check every 5 seconds
            
            // 1. Check disk space
            var driveInfo = new DriveInfo(Path.GetPathRoot(session.OutputPath));
            if (driveInfo.AvailableFreeSpace < 100 * 1024 * 1024) // <100MB
            {
                _logger.LogError("Disk full during recording. Auto-stopping.");
                await session.StopAsync(reason: "Disk full");
                break;
            }
            
            // 2. Check frame drop rate
            var dropRate = session.GetFrameDropRate();
            if (dropRate > 0.05) // >5% frames dropped
            {
                _logger.LogWarning("High frame drop rate: {Rate:P}. Consider lowering quality.", dropRate);
            }
            
            // 3. Check encoding queue depth
            var queueDepth = session.GetEncodingQueueDepth();
            if (queueDepth > 300) // >10 seconds of backlog at 30fps
            {
                _logger.LogWarning("Encoding queue overflow. Dropping frames.");
                session.DropOldestFrames(count: 100);
            }
        }
    }
}

public class ResourceCheckResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool CanProceed => Errors.Count == 0;
    
    public void AddError(string message) => Errors.Add(message);
    public void AddWarning(string message) => Warnings.Add(message);
}
```

**Error Recovery Strategies**:

| Error Scenario | Detection | Recovery Action |
|----------------|-----------|-----------------|
| **FFmpeg crash** | Process exit event | Save partial video, show error dialog |
| **Disk full** | Monitor free space every 5s | Auto-stop, save what's recorded |
| **Audio device disconnect** | Audio stream exception | Continue video-only, show warning |
| **Hardware encoder failure** | Encoder init error | Fallback to software encoder |
| **Memory pressure** | GC memory info | Lower quality, reduce buffer size |
| **Frame drops >10%** | Frame timestamp gaps | Show warning, suggest quality reduction |
| **Encoding lag** | Queue depth >10s | Drop oldest frames, log warning |

**User Notification Strategy**:
- **Errors** (blocking): Modal dialog, recording cannot start
- **Warnings** (non-blocking): Toast notification, recording continues
- **Info** (FYI): Status message in control panel

---

### Decision 7: Permission Management

**Choice**: Explicit permission checks with guided user flows

**Platform Permission Requirements**:

| Platform | Permission | When Required | Request Method |
|----------|-----------|---------------|----------------|
| macOS 10.14+ | Screen Recording | Before recording | System dialog (automatic) |
| macOS 10.14+ | Microphone | Before audio capture | System dialog (automatic) |
| Windows 10+ | Microphone | Before audio capture | System Settings (manual guide) |
| Linux Wayland | Screen Capture | Before recording | xdg-desktop-portal dialog |

**Implementation**:

```csharp
public interface IPermissionService
{
    Task<PermissionStatus> CheckScreenRecordingPermissionAsync();
    Task<PermissionStatus> CheckMicrophonePermissionAsync();
    Task<bool> RequestScreenRecordingPermissionAsync();
    Task<bool> RequestMicrophonePermissionAsync();
    void OpenSystemPermissionSettings();
}

#if MACOS
public class MacPermissionService : IPermissionService
{
    public async Task<PermissionStatus> CheckScreenRecordingPermissionAsync()
    {
        if (!OperatingSystem.IsMacOSVersionAtLeast(10, 15))
            return PermissionStatus.Granted; // No restrictions on older versions
        
        // Use CGPreflightScreenCaptureAccess to check
        var hasAccess = CGPreflightScreenCaptureAccess();
        return hasAccess ? PermissionStatus.Granted : PermissionStatus.Denied;
    }
    
    public async Task<bool> RequestScreenRecordingPermissionAsync()
    {
        if (OperatingSystem.IsMacOSVersionAtLeast(10, 15))
        {
            // CGRequestScreenCaptureAccess shows system dialog
            var granted = CGRequestScreenCaptureAccess();
            
            if (!granted)
            {
                // Guide user to System Preferences
                var result = await ShowPermissionGuideDialogAsync(
                    "Screen Recording Permission Required",
                    "AGI.Kapster needs permission to record your screen.\n\n" +
                    "1. Open System Preferences > Security & Privacy > Privacy\n" +
                    "2. Select 'Screen Recording' from the list\n" +
                    "3. Check the box next to AGI.Kapster\n" +
                    "4. Restart AGI.Kapster"
                );
                
                if (result == DialogResult.OpenSettings)
                    OpenSystemPermissionSettings();
                
                return false;
            }
            
            return true;
        }
        
        return true;
    }
    
    public void OpenSystemPermissionSettings()
    {
        Process.Start("open", "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture");
    }
}
#endif

#if WINDOWS
public class WindowsPermissionService : IPermissionService
{
    public async Task<PermissionStatus> CheckMicrophonePermissionAsync()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            return PermissionStatus.Granted; // No restrictions on older versions
        
        // Use Windows.Media.Capture.MediaCapture to check
        var access = await Windows.Media.Capture.MediaCapture.RequestAccessAsync(
            Windows.Media.Capture.StreamingCaptureMode.Audio);
        
        return access == Windows.Media.Capture.MediaCaptureAccessStatus.Allowed
            ? PermissionStatus.Granted
            : PermissionStatus.Denied;
    }
}
#endif

#if LINUX
public class LinuxPermissionService : IPermissionService
{
    public async Task<bool> RequestScreenRecordingPermissionAsync()
    {
        // On Wayland, xdg-desktop-portal handles permissions automatically
        if (Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") != null)
        {
            // Portal will show dialog when we call CreateSession
            return true; // Assume granted, will fail at capture time if denied
        }
        
        // X11 has no permission system
        return true;
    }
}
#endif
```

**User Experience Flow**:

1. **Pre-flight check** (before showing region selector):
   ```csharp
   if (!await _permissionService.CheckScreenRecordingPermissionAsync())
   {
       var granted = await _permissionService.RequestScreenRecordingPermissionAsync();
       if (!granted)
       {
           ShowNotification("Screen recording permission denied. Please grant permission in settings.");
           return;
       }
   }
   ```

2. **Audio permission** (when starting recording with audio enabled):
   ```csharp
   if (settings.AudioEnabled && !await _permissionService.CheckMicrophonePermissionAsync())
   {
       var granted = await _permissionService.RequestMicrophonePermissionAsync();
       if (!granted)
       {
           var continueWithoutAudio = await ShowDialog(
               "Microphone permission denied. Continue recording without audio?",
               buttons: ["Yes", "No"]);
           
           if (continueWithoutAudio)
               settings.AudioEnabled = false;
           else
               return;
       }
   }
   ```

**Info.plist Updates** (macOS):
```xml
<key>NSCameraUsageDescription</key>
<string>AGI.Kapster can include your webcam in screen recordings (optional feature).</string>
<key>NSMicrophoneUsageDescription</key>
<string>AGI.Kapster can record audio commentary with your screen recordings.</string>
<key>NSScreenCaptureUsageDescription</key>
<string>AGI.Kapster needs to access your screen to record videos and capture screenshots.</string>
```

---

### Decision 8: Hotkey Management and Conflict Resolution

**Choice**: Customizable hotkeys with conflict detection

**Default Hotkey Assignment** (updated to avoid conflicts):

| Action | Default | Alternative | Conflicts (Known) |
|--------|---------|-------------|-------------------|
| Start/Stop Recording | `Ctrl+Shift+R` | `Ctrl+Alt+R` | None |
| Pause/Resume | `Ctrl+Shift+P` | `Ctrl+Alt+P` | None |
| Cancel Recording | `Escape` | N/A | None (context-sensitive) |

**Rationale for Change**:
- Original `Alt+R` conflicts with browser "Reload" and IDE shortcuts
- Original `Alt+P` conflicts with "Print Preview" in Office apps
- `Ctrl+Shift+R` is commonly used for "Reload/Record" in dev tools and browsers (but in safe context)
- Three-key combos reduce accidental triggering

**Implementation**:

```csharp
public class HotkeyConflictDetector
{
    private readonly ILogger _logger;
    
    public ConflictCheckResult CheckForConflicts(Hotkey hotkey)
    {
        var result = new ConflictCheckResult();
        
        // 1. Check against own hotkeys
        var existingHotkeys = GetRegisteredHotkeys();
        if (existingHotkeys.Any(h => h.Equals(hotkey) && h.Action != hotkey.Action))
        {
            result.IsConflict = true;
            result.Message = $"Already assigned to '{existingHotkeys.First(h => h.Equals(hotkey)).Action}'";
            return result;
        }
        
        // 2. Try to register (OS will fail if conflicting with another app)
        try
        {
            var testHandle = NativeMethods.RegisterHotKey(IntPtr.Zero, GetHashCode(), 
                hotkey.Modifiers, hotkey.Key);
            
            if (testHandle == IntPtr.Zero)
            {
                result.IsConflict = true;
                result.Message = "Hotkey is already in use by another application";
                return result;
            }
            
            NativeMethods.UnregisterHotKey(IntPtr.Zero, GetHashCode());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check hotkey conflict");
        }
        
        // 3. Check against common system hotkeys (Windows)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var systemHotkeys = new[]
            {
                new Hotkey(Modifiers.Win, Key.L), // Lock screen
                new Hotkey(Modifiers.Win, Key.D), // Show desktop
                new Hotkey(Modifiers.Alt, Key.Tab), // Task switcher
                new Hotkey(Modifiers.Ctrl | Modifiers.Alt, Key.Delete), // Task manager
            };
            
            if (systemHotkeys.Any(h => h.Equals(hotkey)))
            {
                result.IsWarning = true;
                result.Message = "Conflicts with system hotkey. May not work reliably.";
            }
        }
        
        return result;
    }
}

public class RecordingHotkeyManager
{
    private readonly IHotkeyManager _hotkeyManager;
    private readonly HotkeyConflictDetector _conflictDetector;
    
    public async Task<bool> RegisterRecordingHotkeysAsync(RecordingSettings settings)
    {
        var hotkeys = new[]
        {
            (settings.StartStopHotkey, "Start/Stop Recording"),
            (settings.PauseResumeHotkey, "Pause/Resume Recording"),
        };
        
        var conflicts = new List<string>();
        
        foreach (var (hotkey, action) in hotkeys)
        {
            var conflictResult = _conflictDetector.CheckForConflicts(hotkey);
            
            if (conflictResult.IsConflict)
            {
                conflicts.Add($"{action}: {conflictResult.Message}");
                continue;
            }
            
            if (conflictResult.IsWarning)
            {
                _logger.LogWarning("{Action} hotkey warning: {Message}", action, conflictResult.Message);
            }
            
            try
            {
                await _hotkeyManager.RegisterAsync(hotkey, () => HandleHotkeyAsync(action));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register hotkey for {Action}", action);
                conflicts.Add($"{action}: Registration failed");
            }
        }
        
        if (conflicts.Any())
        {
            await ShowHotkeyConflictDialog(conflicts);
            return false;
        }
        
        return true;
    }
}
```

**Settings UI**:
```csharp
// Allow users to customize hotkeys
public class RecordingSettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private Hotkey _startStopHotkey = new(Modifiers.Ctrl | Modifiers.Shift, Key.R);
    
    [ObservableProperty]
    private Hotkey _pauseResumeHotkey = new(Modifiers.Ctrl | Modifiers.Shift, Key.P);
    
    [RelayCommand]
    private async Task TestHotkeyAsync()
    {
        var result = _conflictDetector.CheckForConflicts(StartStopHotkey);
        
        if (result.IsConflict)
            await ShowErrorAsync($"Hotkey conflict: {result.Message}");
        else if (result.IsWarning)
            await ShowWarningAsync($"Hotkey warning: {result.Message}");
        else
            await ShowSuccessAsync("Hotkey is available");
    }
}
```

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

