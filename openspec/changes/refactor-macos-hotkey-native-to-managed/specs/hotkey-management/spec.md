## ADDED Requirements

### Requirement: No Native Prebuild Step for macOS Hotkeys
The system SHALL NOT require compiling or generating native code artifacts (e.g., `clang`-built dylibs) as part of `dotnet build` or `dotnet publish` in order for macOS global hotkeys to function.

#### Scenario: Build on machine without Xcode/clang
- **GIVEN** a developer machine or CI runner without Xcode/clang installed
- **WHEN** the project is built via `dotnet build` or `dotnet publish`
- **THEN** the build succeeds
- **AND** the resulting macOS app still supports global hotkeys

#### Scenario: Publish output contains no custom hotkey dylib
- **WHEN** the application is published for macOS
- **THEN** the publish output does not include a project-specific hotkey dylib (e.g., `libkapster_hotkey.dylib`)

## MODIFIED Requirements

### Requirement: macOS Sandbox-Compatible Global Hotkeys
On macOS, the system SHALL implement global hotkeys using system registration that does not require Input Monitoring permission, and the implementation SHALL be compatible with sandbox distributions.

#### Scenario: Hotkeys work in App Store sandbox distribution
- **GIVEN** the application is running in a macOS App Store sandbox build
- **WHEN** the user presses the configured capture hotkey
- **THEN** the capture workflow starts
- **AND** the system does not require Input Monitoring permission for hotkey functionality



