## ADDED Requirements

### Requirement: Structured Hotkey Model
The system SHALL represent configurable hotkeys as a structured model consisting of modifiers and a key specification.

#### Scenario: Configure a named key hotkey
- **WHEN** the user assigns a hotkey with modifiers and a named key (e.g. `Alt` + `F2`)
- **THEN** the system stores the hotkey as a structured gesture
- **AND** the system registers the gesture globally

#### Scenario: Configure a character hotkey
- **WHEN** the user assigns a hotkey with modifiers and a printable character key (e.g. `Alt` + `-`)
- **THEN** the system stores the hotkey as a character gesture
- **AND** the system registers the gesture globally

### Requirement: Character-Stable Hotkey Semantics
For character hotkeys, the system SHALL treat the configured key as a character, not as a physical key position.

#### Scenario: Character hotkey remains consistent across keyboard layouts
- **GIVEN** a hotkey is configured as `Alt` + `-`
- **WHEN** the user switches keyboard layout
- **THEN** the system re-resolves the character to the current layout
- **AND** the hotkey continues to trigger when the user presses the keys required to produce `-` under the active layout

#### Scenario: Character requires implicit modifiers
- **GIVEN** a hotkey is configured as `Alt` + `[`
- **AND** the active keyboard layout produces `[` only with an additional modifier (e.g. Shift/Option/AltGr)
- **WHEN** the system resolves the hotkey
- **THEN** the system includes the required implicit modifiers in the registered chord
- **AND** the Settings UI displays the effective chord to the user

### Requirement: macOS Sandbox-Compatible Global Hotkeys
On macOS, the system SHALL implement global hotkeys using system registration that does not require Input Monitoring permission.

#### Scenario: Hotkeys work in App Store sandbox distribution
- **GIVEN** the application is running in a macOS App Store sandbox build
- **WHEN** the user presses the configured capture hotkey
- **THEN** the capture workflow starts
- **AND** the system does not require Input Monitoring permission for hotkey functionality

### Requirement: Explicit Registration Failure Handling
The system SHALL not report a hotkey as registered if registration fails.

#### Scenario: Conflict with system or other application
- **WHEN** the user assigns a hotkey that is already reserved or registered by another application
- **THEN** the system reports registration failure
- **AND** the Settings UI indicates the hotkey is unavailable
- **AND** the user can choose a different hotkey

#### Scenario: Unresolvable character
- **WHEN** the user assigns a character hotkey that cannot be resolved for the active keyboard layout
- **THEN** the system reports registration failure
- **AND** the hotkey is not activated

### Requirement: Keyboard Layout Change Re-Registration
The system SHALL re-register character hotkeys when the active keyboard layout changes.

#### Scenario: Layout change triggers re-registration
- **GIVEN** at least one character hotkey is configured
- **WHEN** the active keyboard layout changes
- **THEN** the system re-resolves and re-registers the affected hotkeys

## MODIFIED Requirements

### Requirement: Hotkey Settings Persistence
The system SHALL persist hotkey settings and load them on startup.

#### Scenario: Invalid or unsupported stored hotkeys
- **GIVEN** the stored settings cannot be deserialized or validated under the current hotkey model
- **WHEN** the application starts
- **THEN** the system uses the new default hotkeys
- **AND** the system persists the new defaults to settings storage
- **AND** the application continues without crashing


