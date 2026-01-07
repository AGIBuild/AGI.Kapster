## 1. Implementation (estimated 5-8 days)

- [ ] 1.1 Define new hotkey model types
  - [ ] Add `HotkeyGesture` (modifiers + key spec) to settings model
  - [ ] Add `HotkeyKeySpec` union: `Char` (single printable) | `NamedKey` (enum)
  - [ ] Add normalization rules (case, whitespace, canonical names)

- [ ] 1.2 Update settings persistence (no backward compatibility)
  - [ ] Update `SettingsService` to load settings with tolerant fallback
  - [ ] If deserialization/validation fails, reset hotkeys to new defaults and persist
  - [ ] Add telemetry-free logging for load fallback and resets

- [ ] 1.3 Build platform hotkey resolution layer (character-stable)
  - [ ] Introduce `IHotkeyResolver` to translate `HotkeyGesture` -> registerable chord(s)
  - [ ] Define `ResolvedHotkey` output: platform keycode + modifiers (incl. implicit)
  - [ ] Windows resolver: use Win32 layout mapping (e.g., `VkKeyScanEx`/`ToUnicodeEx`) to resolve printable characters
  - [ ] macOS resolver: use current keyboard layout to resolve character -> `kVK_*` + implicit modifiers
  - [ ] Define behavior for unresolvable characters (reject + fallback)

- [ ] 1.4 Implement macOS global hotkeys via Carbon (sandbox-friendly)
  - [ ] Create `MacCarbonHotkeyProvider` using `RegisterEventHotKey` + event handler
  - [ ] Ensure correct lifecycle (register/unregister/dispose)
  - [ ] Ensure calls run on main thread / run loop as required
  - [ ] Verify works in App Store sandbox build

- [ ] 1.5 Wire HotkeyManager to new model
  - [ ] Remove string parsing dependency from `HotkeyManager`
  - [ ] Register capture/settings hotkeys using resolver output
  - [ ] Maintain reentry guards (ignore when overlay active)
  - [ ] Add robust error handling and fallback to defaults if registration fails

- [ ] 1.6 Keyboard layout change handling
  - [ ] Add `IKeyboardLayoutMonitor` for Windows/macOS
  - [ ] On layout change, re-resolve and re-register character-based hotkeys
  - [ ] Add debouncing to avoid rapid re-registration

- [ ] 1.7 Update Settings UI for reliable character capture
  - [ ] Replace KeyDown-based capture with character-based input capture for printable keys
  - [ ] Still allow named keys via key events (F1, Enter, Esc, arrows, etc.)
  - [ ] Display the resolved effective chord (including implicit modifiers) per platform/layout
  - [ ] Provide user-facing error messages for invalid/unavailable hotkeys

- [ ] 1.8 Tests
  - [ ] Unit tests for gesture parsing/normalization/serialization
  - [ ] Unit tests for resolver (table-driven for symbol keys across layouts where possible)
  - [ ] Integration tests for HotkeyManager fallback behavior when registration fails

- [ ] 1.9 Manual validation checklist
  - [ ] macOS (App Store sandbox): hotkeys work without Input Monitoring
  - [ ] macOS: symbol keys work and remain character-stable across layout changes
  - [ ] Windows: symbol keys map correctly and remain character-stable across layout changes
  - [ ] Conflicts: clear error + recovery path, no silent success


