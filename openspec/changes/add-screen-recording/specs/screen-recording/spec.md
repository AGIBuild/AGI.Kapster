# Specification: Screen Recording

## ADDED Requirements

### Requirement: Recording Initiation

The system SHALL allow users to initiate screen recording via hotkey or UI controls.

#### Scenario: Start recording via hotkey
- **WHEN** user presses `Alt+R` hotkey
- **THEN** recording region selection overlay appears
- **AND** user can select full screen or custom region
- **AND** 3-second countdown begins after region selection
- **AND** recording starts automatically after countdown

#### Scenario: Start recording via system tray
- **WHEN** user clicks "Start Recording" in system tray context menu
- **THEN** recording region selection overlay appears
- **AND** recording workflow proceeds as with hotkey trigger

#### Scenario: Cancel region selection
- **WHEN** user presses `Escape` during region selection
- **THEN** recording initiation is cancelled
- **AND** overlay closes without starting recording

---

### Requirement: Region Selection

The system SHALL support full screen and custom region recording.

#### Scenario: Record full screen
- **WHEN** user clicks "Full Screen" button in region selection overlay
- **THEN** entire primary monitor is selected for recording
- **AND** countdown begins immediately

#### Scenario: Record specific monitor
- **WHEN** user has multiple monitors
- **THEN** user can select which monitor to record
- **AND** full screen of selected monitor is recorded

#### Scenario: Record custom region
- **WHEN** user drags to select a rectangular region
- **THEN** selected region is highlighted with border
- **AND** region dimensions are displayed (e.g., "1920x1080")
- **AND** user can adjust region by dragging corners/edges
- **AND** countdown begins after releasing mouse button

#### Scenario: Minimum region size
- **WHEN** user attempts to select a region smaller than 100x100 pixels
- **THEN** system prevents selection
- **AND** displays error message "Recording region must be at least 100x100 pixels"

---

### Requirement: Recording Control

The system SHALL provide pause, resume, stop, and cancel controls during recording.

#### Scenario: Pause recording via hotkey
- **WHEN** recording is active
- **AND** user presses `Alt+P` hotkey
- **THEN** recording pauses immediately
- **AND** control panel shows "Paused" status
- **AND** timer stops incrementing

#### Scenario: Resume recording via hotkey
- **WHEN** recording is paused
- **AND** user presses `Alt+P` hotkey again
- **THEN** recording resumes immediately
- **AND** control panel shows "Recording" status
- **AND** timer continues from paused time

#### Scenario: Stop recording successfully
- **WHEN** user presses `Alt+R` or clicks Stop button
- **THEN** recording stops immediately
- **AND** control panel shows "Encoding..." status
- **AND** video file is finalized and saved
- **AND** toast notification displays "Recording saved to [path]"
- **AND** control panel closes after encoding completes

#### Scenario: Cancel recording
- **WHEN** user clicks Cancel button or presses `Escape`
- **THEN** recording stops without saving
- **AND** temporary files are deleted
- **AND** control panel closes immediately
- **AND** toast notification displays "Recording cancelled"

---

### Requirement: Recording Control Panel

The system SHALL display a floating control panel during recording.

#### Scenario: Control panel appearance
- **WHEN** recording starts
- **THEN** control panel appears in top-right corner
- **AND** panel shows elapsed time (format: MM:SS)
- **AND** panel shows Pause, Stop, Cancel buttons
- **AND** panel is always on top of other windows
- **AND** panel is draggable to any screen position

#### Scenario: Control panel semi-transparency
- **WHEN** mouse is not over control panel for 3 seconds
- **THEN** panel becomes 50% transparent
- **AND** panel returns to 100% opacity when mouse hovers

#### Scenario: Control panel during pause
- **WHEN** recording is paused
- **THEN** panel background color changes to yellow
- **AND** Pause button changes to Resume button
- **AND** timer stops incrementing but remains visible

---

### Requirement: Video Output Formats

The system SHALL support multiple video output formats with configurable quality.

#### Scenario: Record to MP4 format
- **WHEN** user selects MP4 format in settings
- **THEN** recording is encoded using H.264 codec
- **AND** output file has .mp4 extension
- **AND** file is playable in standard media players

#### Scenario: Record to WebM format
- **WHEN** user selects WebM format in settings
- **THEN** recording is encoded using VP9 codec
- **AND** output file has .webm extension
- **AND** file is playable in browsers and WebM-compatible players

#### Scenario: Record to MKV format
- **WHEN** user selects MKV format in settings
- **THEN** recording is encoded using H.264 codec
- **AND** output file has .mkv extension
- **AND** file is playable in VLC and MKV-compatible players

#### Scenario: Export to animated GIF
- **WHEN** user selects GIF format in settings
- **THEN** recording is converted to animated GIF
- **AND** output file has .gif extension
- **AND** GIF is optimized for web use (reduced colors, frame rate)

#### Scenario: Quality presets
- **WHEN** user selects quality preset (Low, Medium, High, Ultra)
- **THEN** recording uses corresponding bitrate and resolution
  - Low: 720p, 2 Mbps
  - Medium: 1080p, 5 Mbps
  - High: 1080p, 10 Mbps
  - Ultra: 1440p+, 20 Mbps (if display supports)

---

### Requirement: Audio Capture

The system SHALL support audio capture from system audio and microphone.

#### Scenario: Record system audio
- **WHEN** user enables audio and selects "System Audio" source
- **THEN** desktop audio (applications, browser, music) is captured
- **AND** audio is synchronized with video in output file

#### Scenario: Record microphone audio
- **WHEN** user enables audio and selects "Microphone" source
- **THEN** microphone input is captured
- **AND** audio is synchronized with video in output file

#### Scenario: Record mixed audio
- **WHEN** user enables audio and selects "Both" source
- **THEN** system audio and microphone are mixed together
- **AND** mixed audio is synchronized with video in output file

#### Scenario: Audio capture failure fallback
- **WHEN** audio capture fails to initialize
- **THEN** recording continues with video only
- **AND** warning notification displays "Audio capture failed, recording video only"

#### Scenario: Disable audio recording
- **WHEN** user disables audio in settings
- **THEN** recording captures video only
- **AND** output file has no audio track

---

### Requirement: Hardware Acceleration

The system SHALL utilize GPU hardware acceleration when available.

#### Scenario: NVIDIA GPU acceleration
- **WHEN** system has NVIDIA GPU with NVENC support
- **THEN** recording uses NVENC encoder for H.264/HEVC
- **AND** CPU usage is reduced by 70-90%
- **AND** encoding quality matches software encoder

#### Scenario: AMD GPU acceleration
- **WHEN** system has AMD GPU with VCE support
- **THEN** recording uses VCE encoder for H.264/HEVC
- **AND** CPU usage is reduced by 70-90%

#### Scenario: Intel GPU acceleration
- **WHEN** system has Intel CPU with Quick Sync support
- **THEN** recording uses Quick Sync encoder for H.264/HEVC
- **AND** CPU usage is reduced by 60-80%

#### Scenario: Software encoding fallback
- **WHEN** no hardware encoder is available
- **OR** hardware encoder initialization fails
- **THEN** recording uses libx264 software encoder
- **AND** warning notification displays "Using software encoding (CPU intensive)"

#### Scenario: Force software encoding
- **WHEN** user disables hardware acceleration in settings
- **THEN** recording always uses software encoder
- **AND** hardware encoders are not attempted

---

### Requirement: Mouse Cursor Effects

The system SHALL support mouse cursor visibility and click highlighting.

#### Scenario: Show mouse cursor in recording
- **WHEN** user enables "Show Mouse Cursor" option
- **THEN** mouse cursor is rendered in video frames
- **AND** cursor appearance matches system cursor theme

#### Scenario: Hide mouse cursor in recording
- **WHEN** user disables "Show Mouse Cursor" option
- **THEN** mouse cursor is not rendered in video frames

#### Scenario: Highlight mouse clicks
- **WHEN** user enables "Highlight Clicks" option
- **AND** user clicks during recording
- **THEN** circular ripple animation appears at click position
- **AND** animation is visible for 500ms
- **AND** left clicks are highlighted in blue
- **AND** right clicks are highlighted in red

#### Scenario: Disable click highlighting
- **WHEN** user disables "Highlight Clicks" option
- **THEN** no click animations appear during recording

---

### Requirement: Recording Settings

The system SHALL provide configurable recording settings.

#### Scenario: Configure output directory
- **WHEN** user selects output directory in settings
- **THEN** all recordings are saved to selected directory
- **AND** directory path is persisted across app restarts

#### Scenario: Configure frame rate
- **WHEN** user selects frame rate (30 or 60 FPS)
- **THEN** recording captures at selected frame rate
- **AND** output video has matching frame rate

#### Scenario: Configure countdown duration
- **WHEN** user sets countdown seconds (0-10)
- **THEN** countdown displays for selected duration before recording
- **AND** countdown can be disabled by setting to 0

#### Scenario: Configure hotkeys
- **WHEN** user customizes recording hotkeys in settings
- **THEN** new hotkeys are registered globally
- **AND** old hotkeys are unregistered
- **AND** hotkey conflicts are detected and prevented

---

### Requirement: File Naming and Output

The system SHALL automatically name and save recording files.

#### Scenario: Automatic file naming
- **WHEN** recording completes successfully
- **THEN** file is named using pattern: "Recording_YYYY-MM-DD_HH-MM-SS.[ext]"
- **AND** file is saved to configured output directory
- **AND** file path is displayed in completion notification

#### Scenario: File name collision handling
- **WHEN** file with same name already exists
- **THEN** system appends suffix "_1", "_2", etc. to filename
- **AND** existing file is not overwritten

#### Scenario: Disk space validation
- **WHEN** user starts recording
- **AND** available disk space is less than 500MB
- **THEN** error notification displays "Insufficient disk space for recording"
- **AND** recording is not started

#### Scenario: Out of disk space during recording
- **WHEN** disk becomes full during recording
- **THEN** recording stops immediately
- **AND** partial recording is saved
- **AND** error notification displays "Recording stopped: disk full"

---

### Requirement: Performance and Stability

The system SHALL maintain performance targets during recording.

#### Scenario: Frame rate stability
- **WHEN** recording at target frame rate (30 or 60 FPS)
- **THEN** actual frame rate is within 95% of target
- **AND** dropped frames are less than 5% of total

#### Scenario: CPU usage with hardware acceleration
- **WHEN** hardware acceleration is active
- **THEN** CPU usage during recording is less than 15% on modern CPUs

#### Scenario: CPU usage with software encoding
- **WHEN** software encoding is used
- **THEN** CPU usage during recording is less than 40% on modern CPUs

#### Scenario: Memory usage
- **WHEN** recording is active
- **THEN** additional memory usage is less than 300MB
- **AND** memory is released after recording stops

#### Scenario: Long recording stability
- **WHEN** recording duration exceeds 60 minutes
- **THEN** recording continues without crashes or corruption
- **AND** frame rate remains stable
- **AND** audio sync remains within ±50ms

---

### Requirement: Cross-Platform Support

The system SHALL function on Windows, macOS, and Linux platforms.

#### Scenario: Windows recording
- **WHEN** running on Windows 10 or later
- **THEN** screen recording works with all formats
- **AND** audio capture works for system and microphone
- **AND** hardware acceleration works on compatible GPUs

#### Scenario: macOS recording
- **WHEN** running on macOS 10.15 or later
- **THEN** screen recording works with all formats
- **AND** audio capture works using AVFoundation
- **AND** screen recording permission is requested on first use

#### Scenario: Linux recording
- **WHEN** running on Ubuntu 20.04 or later
- **THEN** screen recording works with all formats
- **AND** audio capture works using PulseAudio/ALSA
- **AND** both X11 and Wayland are supported

---

### Requirement: Error Handling and Recovery

The system SHALL handle errors gracefully and provide recovery options.

#### Scenario: FFmpeg initialization failure
- **WHEN** FFmpeg libraries fail to load
- **THEN** error notification displays "Recording unavailable: FFmpeg not found"
- **AND** recording features are disabled in UI
- **AND** error is logged with instructions to reinstall

#### Scenario: Encoding error during recording
- **WHEN** video encoding error occurs during recording
- **THEN** recording stops immediately
- **AND** partial recording is saved (if possible)
- **AND** error notification displays with error details
- **AND** error is logged for troubleshooting

#### Scenario: Frame drop detection
- **WHEN** frame drop rate exceeds 10%
- **THEN** warning notification displays "Performance warning: frames dropping"
- **AND** user is advised to reduce quality or frame rate
- **AND** recording continues with current settings

---

### Requirement: System Tray Integration

The system SHALL integrate recording status with system tray.

#### Scenario: Recording status indicator
- **WHEN** recording is active
- **THEN** system tray icon shows animated red dot
- **AND** tooltip shows "Recording: MM:SS"

#### Scenario: System tray recording controls
- **WHEN** user right-clicks system tray icon during recording
- **THEN** context menu shows "Pause Recording", "Stop Recording", "Cancel Recording"
- **AND** clicking menu items performs corresponding actions

#### Scenario: Recording completion notification
- **WHEN** recording is saved successfully
- **THEN** toast notification displays "Recording saved to [path]"
- **AND** notification includes "Open Folder" button
- **AND** clicking button opens output directory in file explorer

---

### Requirement: Countdown Timer

The system SHALL display countdown timer before recording starts.

#### Scenario: Countdown display
- **WHEN** user completes region selection
- **THEN** large countdown appears in center of selected region
- **AND** countdown shows "3", "2", "1" with 1-second intervals
- **AND** recording starts automatically after "1"

#### Scenario: Cancel countdown
- **WHEN** countdown is active
- **AND** user presses `Escape`
- **THEN** countdown is cancelled
- **AND** recording does not start
- **AND** overlay closes

#### Scenario: Skip countdown
- **WHEN** user sets countdown to 0 seconds in settings
- **THEN** recording starts immediately after region selection
- **AND** no countdown is displayed

---

### Requirement: Output File Validation

The system SHALL validate output files after recording.

#### Scenario: Successful file validation
- **WHEN** recording completes and file is saved
- **THEN** system verifies file exists and is not empty
- **AND** system verifies file is playable (basic header check)
- **AND** completion notification is displayed

#### Scenario: Corrupted file detection
- **WHEN** output file is corrupted or unplayable
- **THEN** error notification displays "Recording file may be corrupted"
- **AND** user is advised to check file manually
- **AND** error is logged with file details

---

### Requirement: Recording Limitations

The system SHALL enforce reasonable recording limitations.

#### Scenario: Maximum recording duration
- **WHEN** recording duration reaches 4 hours
- **THEN** recording stops automatically
- **AND** notification displays "Recording stopped: maximum duration reached"
- **AND** file is saved successfully

#### Scenario: Maximum file size
- **WHEN** recording file size reaches 10GB
- **THEN** recording stops automatically
- **AND** notification displays "Recording stopped: maximum file size reached"
- **AND** file is saved successfully

---

### Requirement: Settings Persistence

The system SHALL persist recording settings across application restarts.

#### Scenario: Save settings on change
- **WHEN** user changes any recording setting
- **AND** clicks Save button
- **THEN** settings are persisted to appsettings.json
- **AND** confirmation message displays "Settings saved"

#### Scenario: Load settings on startup
- **WHEN** application starts
- **THEN** recording settings are loaded from appsettings.json
- **AND** settings UI reflects loaded values
- **AND** default settings are used if file is missing or invalid

---

### Requirement: Platform Permission Management

The system SHALL request and validate platform-specific permissions before recording.

#### Scenario: Check screen recording permission on macOS
- **WHEN** user initiates recording on macOS 10.14+
- **AND** app lacks screen recording permission
- **THEN** system shows macOS permission dialog
- **AND** guides user to System Preferences if denied
- **AND** recording is cancelled with clear error message "Screen recording permission required"

#### Scenario: Check microphone permission
- **WHEN** user enables microphone audio
- **AND** app lacks microphone permission
- **THEN** system requests permission via platform dialog
- **AND** shows "Grant microphone access" guidance if denied
- **AND** offers option to continue without audio

#### Scenario: Linux Wayland portal permission
- **WHEN** user starts recording on Wayland session
- **THEN** system uses xdg-desktop-portal for screen capture
- **AND** shows native Wayland permission dialog
- **AND** respects user's window/monitor selection from portal

#### Scenario: Permission granted successfully
- **WHEN** all required permissions are granted
- **THEN** recording proceeds normally
- **AND** no permission dialogs shown on subsequent recordings

---

### Requirement: Resource Monitoring Before Recording

The system SHALL check system resources before starting recording.

#### Scenario: Insufficient memory detected
- **WHEN** user initiates recording
- **AND** available memory < estimated usage + 500MB
- **THEN** error dialog displays "Insufficient memory. Close other applications."
- **AND** recording does not start

#### Scenario: Low disk space detected
- **WHEN** user initiates recording
- **AND** available disk space < 1GB
- **THEN** error dialog displays "Insufficient disk space. Need at least 1GB free."
- **AND** recording does not start

#### Scenario: Low disk space warning
- **WHEN** user initiates recording
- **AND** available disk space between 1GB and 5GB
- **THEN** warning notification displays "Low disk space: Xgb remaining"
- **AND** user can choose to proceed or cancel

#### Scenario: High CPU usage warning
- **WHEN** user initiates recording
- **AND** current CPU usage > 80%
- **THEN** warning displays "High CPU usage detected. Recording may drop frames."
- **AND** user can choose to proceed or cancel

#### Scenario: Hardware encoder unavailable
- **WHEN** user has hardware acceleration enabled
- **AND** hardware encoder fails to initialize
- **THEN** system automatically falls back to software encoding
- **AND** warning displays "Hardware encoder unavailable. Using software encoding (slower)."

---

### Requirement: Runtime Resource Monitoring

The system SHALL monitor resources during recording and handle failures gracefully.

#### Scenario: Disk full during recording
- **WHEN** recording is active
- **AND** available disk space drops below 100MB
- **THEN** recording stops automatically
- **AND** saves what has been recorded so far
- **AND** notification displays "Recording stopped: disk full"

#### Scenario: High frame drop rate detected
- **WHEN** recording is active
- **AND** frame drop rate exceeds 5%
- **THEN** warning notification displays "High frame drops detected. Consider lowering quality."
- **AND** recording continues

#### Scenario: Encoding queue overflow
- **WHEN** recording is active
- **AND** encoding queue depth exceeds 10 seconds of backlog
- **THEN** system drops oldest frames to prevent memory overflow
- **AND** logs warning "Encoding queue overflow. Dropping frames."

#### Scenario: Audio device disconnected during recording
- **WHEN** recording with microphone enabled
- **AND** microphone is unplugged or fails
- **THEN** recording continues with video only
- **AND** warning displays "Microphone disconnected. Continuing video-only."

#### Scenario: FFmpeg encoder crash during recording
- **WHEN** FFmpeg process crashes during recording
- **THEN** system attempts to save partial recording
- **AND** error dialog displays "Recording failed: encoder error. Partial recording may be saved."
- **AND** detailed error is logged for troubleshooting

---

### Requirement: Platform-Specific Capture Technology

The system SHALL use platform-optimized screen capture APIs.

#### Scenario: Windows Graphics Capture API (Windows 10 1903+)
- **WHEN** recording on Windows 10 version 1903 or later
- **THEN** system uses Windows.Graphics.Capture API
- **AND** provides GPU acceleration for capture
- **AND** automatically handles DPI scaling
- **AND** can exclude own windows from capture

#### Scenario: GDI+ fallback (Windows 10 < 1903)
- **WHEN** recording on Windows 10 versions earlier than 1903
- **THEN** system falls back to GDI+ BitBlt capture
- **AND** manually handles DPI scaling
- **AND** logs "Using legacy GDI+ capture. Consider upgrading Windows for better performance."

#### Scenario: macOS ScreenCaptureKit (macOS 12.3+)
- **WHEN** recording on macOS 12.3 or later
- **THEN** system uses ScreenCaptureKit API
- **AND** captures system audio natively
- **AND** provides GPU-accelerated capture
- **AND** respects system privacy controls

#### Scenario: macOS AVFoundation fallback (macOS 10.15-12.2)
- **WHEN** recording on macOS 10.15 to 12.2
- **THEN** system uses AVFoundation AVCaptureScreen
- **AND** supports microphone audio only
- **AND** system audio requires manual BlackHole setup
- **AND** info message displays "System audio requires BlackHole driver (optional)"

#### Scenario: Linux Wayland xdg-desktop-portal
- **WHEN** recording on Linux with Wayland session
- **THEN** system uses xdg-desktop-portal-screencast
- **AND** shows native permission dialog
- **AND** supports PipeWire audio capture

#### Scenario: Linux X11 direct capture
- **WHEN** recording on Linux with X11 session
- **THEN** system uses X11 XGetImage or XShm
- **AND** captures directly without permissions
- **AND** supports PulseAudio/PipeWire audio

---

### Requirement: Audio Stack Detection (Linux)

The system SHALL detect and use the appropriate Linux audio stack.

#### Scenario: PipeWire detected (modern Linux)
- **WHEN** recording on Linux
- **AND** PipeWire is available
- **THEN** system uses PipeWire for audio capture
- **AND** logs "Using PipeWire for audio capture"
- **AND** benefits from low latency and Wayland support

#### Scenario: PulseAudio fallback
- **WHEN** recording on Linux
- **AND** PipeWire is not available
- **AND** PulseAudio is available
- **THEN** system uses PulseAudio for audio capture
- **AND** logs "Using PulseAudio for audio capture"

#### Scenario: ALSA fallback
- **WHEN** recording on Linux
- **AND** neither PipeWire nor PulseAudio are available
- **THEN** system uses ALSA for audio capture
- **AND** logs "Using ALSA for audio capture (may conflict with other apps)"

---

### Requirement: Codec Selection and License Compliance

The system SHALL use license-compliant codecs to avoid GPL contamination.

#### Scenario: Hardware encoder selected (preferred)
- **WHEN** hardware encoder is available (NVENC/QSV/AMF)
- **AND** user selects MP4 format
- **THEN** system uses hardware H.264 encoder
- **AND** logs "Using [NVENC|QSV|AMF] hardware encoder"
- **AND** no GPL libraries are linked

#### Scenario: OpenH264 software fallback (BSD licensed)
- **WHEN** no hardware encoder is available
- **AND** user selects MP4 format
- **THEN** system uses OpenH264 software encoder (BSD license)
- **AND** logs "Using OpenH264 software encoder"
- **AND** NEVER uses libx264 (GPL)

#### Scenario: VP9 for WebM format (BSD licensed)
- **WHEN** user selects WebM format
- **THEN** system uses libvpx VP9 encoder (BSD license)
- **AND** logs "Using VP9 encoder for WebM"

#### Scenario: GPL codec detection
- **WHEN** FFmpeg binaries are loaded
- **THEN** system verifies FFmpeg is built with LGPL-only configuration
- **AND** logs error if GPL codecs (x264/x265) are detected
- **AND** refuses to use GPL-contaminated builds

---

### Requirement: FFmpeg Dynamic Download

The system SHALL download FFmpeg binaries on first use rather than bundling.

#### Scenario: First-time recording (FFmpeg not cached)
- **WHEN** user initiates recording for the first time
- **AND** FFmpeg is not cached locally
- **THEN** system shows "Downloading FFmpeg (~50MB)..." dialog with progress bar
- **AND** downloads FFmpeg 7.0.2 from GitHub Releases or CDN
- **AND** extracts to local cache directory
- **AND** proceeds with recording after download completes

#### Scenario: FFmpeg already cached
- **WHEN** user initiates recording
- **AND** FFmpeg 7.0.2 is already cached locally
- **THEN** system uses cached FFmpeg immediately
- **AND** no download occurs

#### Scenario: FFmpeg download failure
- **WHEN** FFmpeg download fails (network error, server unavailable)
- **THEN** system tries system-installed FFmpeg as fallback
- **AND** shows error "FFmpeg download failed. Recording unavailable." if fallback also fails
- **AND** logs detailed error for troubleshooting

#### Scenario: FFmpeg version validation
- **WHEN** system loads FFmpeg binaries
- **THEN** validates FFmpeg version is 7.0.x (major version 61)
- **AND** refuses to use mismatched versions
- **AND** logs error "FFmpeg version mismatch" if validation fails

---

### Requirement: Hotkey Conflict Detection

The system SHALL detect and prevent hotkey conflicts.

#### Scenario: Default hotkeys changed to avoid conflicts
- **WHEN** user views recording hotkey settings
- **THEN** default Start/Stop hotkey is Ctrl+Shift+R (not Alt+R)
- **AND** default Pause/Resume hotkey is Ctrl+Shift+P (not Alt+P)
- **AND** avoids conflicts with browser and IDE shortcuts

#### Scenario: Hotkey conflict with own app
- **WHEN** user assigns hotkey already used by another AGI.Kapster feature
- **THEN** error displays "Hotkey already assigned to [feature name]"
- **AND** hotkey assignment is rejected

#### Scenario: Hotkey conflict with system or other apps
- **WHEN** user assigns hotkey
- **AND** hotkey is already registered by system or another application
- **THEN** warning displays "Hotkey may be in use by another application"
- **AND** user can test hotkey to verify it works

#### Scenario: Customizable hotkeys
- **WHEN** user opens recording settings
- **THEN** user can click on hotkey field to customize
- **AND** press new key combination to assign
- **AND** system validates hotkey before saving

#### Scenario: Test hotkey functionality
- **WHEN** user clicks "Test Hotkey" button in settings
- **THEN** system attempts to register hotkey temporarily
- **AND** shows success message if hotkey is available
- **AND** shows conflict warning if unavailable

---

### Requirement: Accessibility Support

The system SHALL support keyboard navigation and screen readers for recording controls.

#### Scenario: Keyboard navigation in control panel
- **WHEN** recording control panel is visible
- **AND** user presses Tab key
- **THEN** focus cycles through Pause, Stop, Cancel buttons
- **AND** focused button has visible focus indicator
- **AND** user can press Space or Enter to activate button

#### Scenario: Screen reader announcements
- **WHEN** recording state changes
- **THEN** screen reader announces new state
- **EXAMPLE**: "Recording started", "Recording paused", "Recording stopped"

#### Scenario: High contrast mode support
- **WHEN** system is in high contrast mode
- **THEN** control panel colors adjust automatically
- **AND** all buttons remain distinguishable
- **AND** text contrast meets WCAG AA standards

#### Scenario: Keyboard shortcut discoverability
- **WHEN** user hovers over control panel buttons
- **THEN** tooltip shows button function and associated hotkey
- **EXAMPLE**: "Stop Recording (Ctrl+Shift+R)"

---

### Requirement: Platform Testing Matrix

The system SHALL be validated across specified platform configurations.

#### Scenario: Windows 10 22H2 validation
- **WHEN** testing on Windows 10 22H2
- **THEN** validates 1080p SDR recording with WASAPI audio
- **AND** tests NVIDIA GPU hardware acceleration
- **AND** achieves <1% frame drop rate at 30 FPS

#### Scenario: Windows 11 23H2 validation
- **WHEN** testing on Windows 11 23H2
- **THEN** validates 4K HDR recording support
- **AND** tests Intel Arc hardware acceleration
- **AND** achieves <3% frame drop rate at 60 FPS

#### Scenario: macOS 13 Ventura validation (M1)
- **WHEN** testing on macOS 13 Ventura with M1 chip
- **THEN** validates Retina display recording
- **AND** tests AVFoundation audio capture
- **AND** achieves <1% frame drop rate at 30 FPS

#### Scenario: macOS 14 Sonoma validation (M3)
- **WHEN** testing on macOS 14 Sonoma with M3 chip
- **THEN** validates ScreenCaptureKit with native system audio
- **AND** achieves <1% frame drop rate at 60 FPS
- **AND** tests privacy permission flows

#### Scenario: Ubuntu 22.04 LTS validation
- **WHEN** testing on Ubuntu 22.04 LTS
- **THEN** validates 1080p recording with PulseAudio
- **AND** tests AMD GPU hardware acceleration
- **AND** achieves <3% frame drop rate at 30 FPS

#### Scenario: Ubuntu 24.04 LTS validation (Wayland)
- **WHEN** testing on Ubuntu 24.04 LTS with Wayland
- **THEN** validates 4K recording via xdg-desktop-portal
- **AND** tests PipeWire audio capture
- **AND** validates permission dialog flow
- **AND** achieves <3% frame drop rate at 30 FPS

---

### Requirement: Performance Benchmarks

The system SHALL meet specified performance targets.

#### Scenario: Frame drop rate target
- **WHEN** recording at 30 FPS
- **THEN** frame drop rate SHALL be <1%
- **WHEN** recording at 60 FPS
- **THEN** frame drop rate SHALL be <3%

#### Scenario: CPU usage with hardware acceleration
- **WHEN** recording with hardware encoder
- **THEN** CPU usage SHALL be <15% on modern CPU
- **AND** GPU usage handles encoding workload

#### Scenario: CPU usage with software encoding
- **WHEN** recording with software encoder (OpenH264)
- **THEN** CPU usage SHALL be <40% on modern CPU

#### Scenario: Memory usage limits
- **WHEN** recording at 1080p 30FPS
- **THEN** additional memory usage SHALL be <500MB
- **WHEN** recording at 4K 30FPS
- **THEN** additional memory usage SHALL be <800MB

#### Scenario: Encoding latency
- **WHEN** user stops recording
- **THEN** file finalization SHALL complete within 2 seconds for recordings <10 minutes
- **AND** user sees "Recording saved" notification promptly

