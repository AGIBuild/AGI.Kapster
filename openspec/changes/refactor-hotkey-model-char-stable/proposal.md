# Change: Refactor global hotkey model to support character-stable shortcuts and macOS sandbox

## Why
Global hotkeys currently rely on macOS key event tapping which requires Input Monitoring permission and can fail in packaged/sandboxed distributions, leading to non-responsive shortcuts even when Accessibility checks pass.
Additionally, hotkey configuration today is string-based and not robust for symbol keys (e.g. `-=[];\',./`) across keyboard layouts.

## What Changes
- Introduce a new hotkey model that supports two key types:
  - **Character keys**: single printable characters (e.g. `-`, `[`, `;`)
  - **Named keys**: function/navigation keys (e.g. `F1`, `Enter`, `Esc`)
- Define **character-stable semantics**: a configured character hotkey MUST trigger based on the character, not the physical key position, across keyboard layouts.
- Replace macOS hotkey implementation with **system-registered global hotkeys** (Carbon `RegisterEventHotKey`) to avoid Input Monitoring and improve reliability in both Developer ID and App Store (sandbox) distributions.
- Add a **layout-aware resolver** for character keys:
  - On each platform, resolve the configured character into a registerable keycode + required implicit modifiers for the current keyboard layout.
  - Detect keyboard layout changes and re-register hotkeys accordingly.
- Strengthen registration validation:
  - Detect and surface failures (conflicts / unsupported keys / unresolvable characters) and fall back to defaults.
- **No backward compatibility** for previous hotkey settings:
  - If stored settings cannot be parsed / deserialized into the new model, the app will use the new default hotkeys and overwrite/persist them.

## Impact
- Affected specs: `hotkey-management` (new capability delta for global hotkeys and configuration semantics)
- Affected code (expected):
  - `src/AGI.Kapster.Desktop/Models/AppSettings.cs` (new hotkey model)
  - `src/AGI.Kapster.Desktop/Services/Settings/SettingsService.cs` (settings load fallback and default persistence)
  - `src/AGI.Kapster.Desktop/Services/Hotkeys/HotkeyManager.cs` (parse removed; use new model + resolver; re-register on layout change)
  - `src/AGI.Kapster.Desktop/Services/Hotkeys/MacHotkeyProvider.cs` (replace with Carbon-based provider; remove event tap dependency)
  - `src/AGI.Kapster.Desktop/Views/SettingsWindow.axaml.cs` (hotkey capture UI updated to record characters reliably)
- Risks:
  - Keyboard layout mapping complexity (especially symbol keys) across platforms
  - Some characters may require implicit modifiers (Shift/Option/AltGr), changing the effective chord; UI must display the resolved chord
  - System-reserved or already-registered hotkeys may not be available; user needs clear feedback and recovery


