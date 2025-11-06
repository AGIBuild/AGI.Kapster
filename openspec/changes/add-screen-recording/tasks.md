# Implementation Tasks: Screen Recording

## 1. Project Setup and Infrastructure

- [ ] 1.1 Add FFmpeg.AutoGen NuGet package to AGI.Kapster.Desktop.csproj
- [ ] 1.2 Add NAudio NuGet package (Windows audio capture)
- [ ] 1.3 Add PortAudio bindings for Linux audio capture
- [ ] 1.4 Create FFmpeg native binaries distribution package
- [ ] 1.5 Update .gitignore to exclude FFmpeg binaries from source control
- [ ] 1.6 Document FFmpeg LGPL compliance in LICENSE file
- [ ] 1.7 Create recording services directory structure (`Services/Recording/`)

## 2. Core Recording Service

- [ ] 2.1 Define `IScreenRecordingService` interface
  ```csharp
  /// <summary>
  /// Starts a new screen recording session for the specified region with the given settings.
  /// </summary>
  /// <param name="region">The screen region to record.</param>
  /// <param name="settings">The recording settings to use.</param>
  /// <returns>A task that returns the created <see cref="RecordingSession"/>.</returns>
  Task<RecordingSession> StartRecordingAsync(Rect region, RecordingSettings settings);

  /// <summary>
  /// Pauses the current recording session.
  /// </summary>
  /// <returns>A task representing the asynchronous operation.</returns>
  Task PauseRecordingAsync();

  /// <summary>
  /// Resumes a paused recording session.
  /// </summary>
  /// <returns>A task representing the asynchronous operation.</returns>
  Task ResumeRecordingAsync();

  /// <summary>
  /// Stops the current recording session and finalizes the output file.
  /// </summary>
  /// <returns>A task that returns the path to the recorded file.</returns>
  Task<string> StopRecordingAsync();

  /// <summary>
  /// Cancels the current recording session and discards any recorded data.
  /// </summary>
  /// <returns>A task representing the asynchronous operation.</returns>
  Task CancelRecordingAsync();
  ```
- [ ] 2.2 Implement `ScreenRecordingService` class
- [ ] 2.3 Implement `RecordingSession` state management
- [ ] 2.4 Create `RecordingState` enum (Idle, RegionSelection, Countdown, Recording, Paused, Encoding, Completed)
- [ ] 2.5 Implement state machine with event handling
- [ ] 2.6 Add recording session lifecycle logging (Serilog)

## 3. Video Encoding Pipeline

- [ ] 3.1 Define `IVideoEncoderService` interface
- [ ] 3.2 Implement `VideoEncoderService` using FFmpeg.AutoGen
- [ ] 3.3 Implement frame capture loop with adaptive timing
- [ ] 3.4 Add support for H.264 encoding (MP4 format)
- [ ] 3.5 Add support for VP9 encoding (WebM format)
- [ ] 3.6 Add support for MKV container format
- [ ] 3.7 Implement GIF export using FFmpeg filters
- [ ] 3.8 Add configurable bitrate and quality presets
- [ ] 3.9 Implement frame buffer management (prevent memory leaks)

## 4. Hardware Acceleration

- [ ] 4.1 Implement NVENC (NVIDIA) encoder detection
- [ ] 4.2 Implement AMD VCE encoder detection
- [ ] 4.3 Implement Intel Quick Sync encoder detection
- [ ] 4.4 Create automatic encoder selection logic
- [ ] 4.5 Implement graceful fallback to software encoding
- [ ] 4.6 Add hardware encoder performance metrics logging
- [ ] 4.7 Add setting to force software encoding (troubleshooting)

## 5. Audio Capture

- [ ] 5.1 Define `IAudioCaptureService` interface
- [ ] 5.2 Implement Windows audio capture using NAudio (WASAPI)
- [ ] 5.3 Implement macOS audio capture using AVFoundation
- [ ] 5.4 Implement Linux audio capture using PortAudio/PulseAudio
- [ ] 5.5 Add audio source selection (System, Microphone, Both)
- [ ] 5.6 Implement audio/video synchronization logic
- [ ] 5.7 Add audio buffer management
- [ ] 5.8 Handle audio capture failures gracefully (fall back to video-only)

## 6. Data Models

- [ ] 6.1 Create `RecordingSettings` model
  - Format, Quality, FrameRate, AudioEnabled, AudioSource, OutputDirectory
  - ShowMouseCursor, HighlightClicks, UseHardwareAcceleration, CountdownSeconds
- [ ] 6.2 Create `RecordingFormat` enum (MP4, WebM, MKV, GIF)
- [ ] 6.3 Create `RecordingQuality` enum (Low, Medium, High, Ultra)
- [ ] 6.4 Create `AudioSource` enum (None, System, Microphone, Both)
- [ ] 6.5 Create `RecordingSession` model (Id, State, StartTime, Duration, Region, Settings)
- [ ] 6.6 Create `RecordingEvent` enum for state machine events
- [ ] 6.7 Add recording settings to `AppSettings` model
- [ ] 6.8 Implement settings serialization/deserialization

## 7. UI Components - Recording Control Panel

- [ ] 7.1 Create `RecordingControlPanel.axaml` (floating window)
- [ ] 7.2 Create `RecordingControlPanel.axaml.cs` (code-behind)
- [ ] 7.3 Create `RecordingControlViewModel` (MVVM view model)
- [ ] 7.4 Add timer display (elapsed time)
- [ ] 7.5 Add pause/resume button
- [ ] 7.6 Add stop button
- [ ] 7.7 Add cancel button
- [ ] 7.8 Implement draggable positioning
- [ ] 7.9 Add always-on-top behavior
- [ ] 7.10 Add semi-transparent when idle
- [ ] 7.11 Style control panel with Fluent Design

## 8. UI Components - Settings Page

- [ ] 8.1 Add "Recording" tab to `SettingsWindow.axaml`
- [ ] 8.2 Create `RecordingSettingsViewModel`
- [ ] 8.3 Add format selection dropdown (MP4, WebM, MKV, GIF)
- [ ] 8.4 Add quality preset radio buttons (Low, Medium, High, Ultra)
- [ ] 8.5 Add frame rate selection (30 FPS, 60 FPS)
- [ ] 8.6 Add audio options (Enable audio, Source selection)
- [ ] 8.7 Add output directory picker
- [ ] 8.8 Add mouse cursor options (Show cursor, Highlight clicks)
- [ ] 8.9 Add hardware acceleration toggle
- [ ] 8.10 Add countdown seconds spinner
- [ ] 8.11 Add hotkey configuration (Alt+R, Alt+P)
- [ ] 8.12 Add live preview of settings (optional)
- [ ] 8.13 Implement save/cancel buttons

## 9. Overlay Integration

- [ ] 9.1 Create `RecordingOverlay.cs` for region selection
- [ ] 9.2 Extend `OverlayCoordinator` to support recording mode
- [ ] 9.3 Add countdown timer overlay (3-2-1 countdown)
- [ ] 9.4 Reuse existing `SelectionHandler` for region selection
- [ ] 9.5 Add visual indicator for recording region border
- [ ] 9.6 Implement recording indicator (red dot on screen border)
- [ ] 9.7 Add preview window overlay (optional)

## 10. Hotkey Integration

- [ ] 10.1 Add `Alt+R` hotkey for start/stop recording
- [ ] 10.2 Add `Alt+P` hotkey for pause/resume recording
- [ ] 10.3 Update `HotkeyManager` to handle recording hotkeys
- [ ] 10.4 Add hotkey conflict detection (screenshot vs recording)
- [ ] 10.5 Allow users to customize recording hotkeys in settings
- [ ] 10.6 Display active hotkeys in recording control panel

## 11. System Tray Integration

- [ ] 11.1 Add recording status to system tray icon (animated red dot)
- [ ] 11.2 Add "Start Recording" context menu item
- [ ] 11.3 Add "Pause/Resume Recording" context menu item (when recording)
- [ ] 11.4 Add "Stop Recording" context menu item (when recording)
- [ ] 11.5 Add "Cancel Recording" context menu item (when recording)
- [ ] 11.6 Show toast notification on recording start/stop
- [ ] 11.7 Update tooltip with recording duration

## 12. Mouse Effects

- [ ] 12.1 Implement cursor position tracking during recording
- [ ] 12.2 Add cursor rendering to video frames
- [ ] 12.3 Implement click detection (mouse down events)
- [ ] 12.4 Add click highlight animation (circle ripple effect)
- [ ] 12.5 Make cursor effects configurable (on/off)
- [ ] 12.6 Optimize cursor rendering performance

## 13. File Output

- [ ] 13.1 Implement streaming write to temporary file
- [ ] 13.2 Add atomic rename on recording completion
- [ ] 13.3 Implement auto-naming with timestamp (e.g., Recording_2025-11-06_14-30-45.mp4)
- [ ] 13.4 Add user-defined naming patterns
- [ ] 13.5 Handle disk full errors gracefully
- [ ] 13.6 Add file size estimation during recording
- [ ] 13.7 Clean up temporary files on cancel/error

## 14. Service Registration

- [ ] 14.1 Register `IScreenRecordingService` as singleton
- [ ] 14.2 Register `IVideoEncoderService` as singleton
- [ ] 14.3 Register `IAudioCaptureService` with platform-specific implementation
- [ ] 14.4 Update `ServiceCollectionExtensions.cs` with recording services
- [ ] 14.5 Add conditional registration based on platform
- [ ] 14.6 Register `RecordingControlViewModel` as transient

## 15. Error Handling

- [ ] 15.1 Add error handling for FFmpeg initialization failures
- [ ] 15.2 Handle audio capture failures (fall back to video-only)
- [ ] 15.3 Handle encoding errors (notify user, save partial recording)
- [ ] 15.4 Add disk space validation before starting recording
- [ ] 15.5 Handle out-of-memory errors (reduce quality, stop recording)
- [ ] 15.6 Add error recovery for frame drops
- [ ] 15.7 Log all errors with context (Serilog)

## 16. Performance Optimization

- [ ] 16.1 Profile frame capture loop for bottlenecks
- [ ] 16.2 Optimize frame buffer allocation (reuse buffers)
- [ ] 16.3 Add adaptive frame rate based on encoding performance
- [ ] 16.4 Implement frame drop detection and logging
- [ ] 16.5 Add performance metrics collection (FPS, CPU, memory)
- [ ] 16.6 Optimize audio/video synchronization overhead
- [ ] 16.7 Lazy-load FFmpeg libraries (reduce startup time)

## 17. Unit Tests

- [ ] 17.1 Test `RecordingSession` state machine transitions
- [ ] 17.2 Test `VideoEncoderService` encoding logic (mock FFmpeg)
- [ ] 17.3 Test `AudioCaptureService` for each platform
- [ ] 17.4 Test recording settings serialization
- [ ] 17.5 Test hotkey handler registration
- [ ] 17.6 Test file naming and output path generation
- [ ] 17.7 Test error handling scenarios
- [ ] 17.8 Achieve >80% code coverage for recording services

## 18. Integration Tests

- [ ] 18.1 Test end-to-end recording workflow (start → record → stop)
- [ ] 18.2 Test pause/resume functionality
- [ ] 18.3 Test region selection integration
- [ ] 18.4 Test audio/video sync in output files
- [ ] 18.5 Test multiple format exports (MP4, WebM, MKV, GIF)
- [ ] 18.6 Test hardware acceleration on available GPUs
- [ ] 18.7 Test recording cancellation and cleanup
- [ ] 18.8 Test settings persistence across app restarts

## 19. Platform Testing

- [ ] 19.1 Validate recording on Windows 10/11 (x64, ARM64)
- [ ] 19.2 Validate recording on macOS 10.15+ (x64, ARM64)
- [ ] 19.3 Validate recording on Ubuntu 20.04/22.04 (x64)
- [ ] 19.4 Test audio capture on all platforms
- [ ] 19.5 Test hardware acceleration on NVIDIA/AMD/Intel GPUs
- [ ] 19.6 Verify output files playable in VLC, Windows Media Player, QuickTime
- [ ] 19.7 Test multi-monitor scenarios on all platforms

## 20. Performance Testing

- [ ] 20.1 Benchmark frame rate stability (>95% target FPS)
- [ ] 20.2 Measure CPU usage with hardware acceleration (<15%)
- [ ] 20.3 Measure CPU usage with software encoding (<40%)
- [ ] 20.4 Measure memory usage during 10-minute recording (<300MB)
- [ ] 20.5 Measure encoding latency after stop (<2 seconds)
- [ ] 20.6 Validate file size efficiency (<50MB per minute at High quality)
- [ ] 20.7 Test recording stability over 60-minute duration

## 21. Documentation

- [ ] 21.1 Update README.md with screen recording section
- [ ] 21.2 Add recording hotkeys to keyboard shortcuts table
- [ ] 21.3 Create user guide for recording features
- [ ] 21.4 Document recording settings and quality presets
- [ ] 21.5 Add architecture diagram for recording pipeline
- [ ] 21.6 Document FFmpeg LGPL compliance and binary distribution
- [ ] 21.7 Add troubleshooting section for common recording issues
- [ ] 21.8 Generate XML documentation for all public APIs

## 22. Packaging and Distribution

- [ ] 22.1 Include FFmpeg binaries in installers (Windows MSI, macOS PKG, Linux DEB/RPM)
- [ ] 22.2 Update installer size documentation (~50MB increase)
- [ ] 22.3 Add post-install script to verify FFmpeg installation
- [ ] 22.4 Update uninstaller to remove FFmpeg binaries
- [ ] 22.5 Test installer on clean systems (no FFmpeg pre-installed)
- [ ] 22.6 Update release notes with recording feature announcement

## 23. Finalization

- [ ] 23.1 Code review all recording-related changes
- [ ] 23.2 Fix any linter errors or warnings
- [ ] 23.3 Run full test suite and verify all tests pass
- [ ] 23.4 Performance test on low-end hardware (identify minimum specs)
- [ ] 23.5 User acceptance testing (internal dogfooding)
- [ ] 23.6 Address all blocking bugs from testing
- [ ] 23.7 Create GitHub release with feature announcement
- [ ] 23.8 Update project version to 2.0.0 (major feature)

---

## Estimated Timeline

- **Weeks 1-3**: Tasks 1-6 (Infrastructure, Core Services, Encoding, Audio)
- **Weeks 4-6**: Tasks 7-11 (UI Components, Overlays, Hotkeys, System Tray)
- **Weeks 7-9**: Tasks 12-16 (Mouse Effects, File Output, Service Registration, Error Handling, Optimization)
- **Weeks 10-11**: Tasks 17-20 (Testing - Unit, Integration, Platform, Performance)
- **Week 12**: Tasks 21-23 (Documentation, Packaging, Finalization)

**Total**: 12 weeks (3 months)

## Dependencies

- External: FFmpeg.AutoGen, NAudio, PortAudio binaries
- Internal: Existing capture services, overlay system, hotkey manager, settings service
- Blocking: None (fully additive feature)

## Success Criteria

- ✅ All 200+ tasks completed
- ✅ All tests passing (>80% coverage)
- ✅ Performance targets met (FPS, CPU, memory)
- ✅ Cross-platform validation complete
- ✅ Documentation complete and accurate
- ✅ Zero critical bugs in testing

