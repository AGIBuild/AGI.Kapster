## Context
AGI.Kapster registers global hotkeys for capture and opening settings. On macOS the current implementation uses a global keyboard event tap (`CGEventTap`) which requires Input Monitoring permission and is unreliable in packaged distributions and incompatible with the desired App Store sandbox distribution UX. Hotkey configuration is also string-based and not robust for symbol keys across keyboard layouts.

This change refactors hotkey configuration and registration to:
- Support **character-stable** hotkeys for printable characters (e.g. `-=[];\',./`)
- Use **system-registered global hotkeys** on macOS (Carbon) to avoid Input Monitoring
- Provide cross-platform resolution of characters to registerable chords based on the current keyboard layout
- Intentionally drop backward compatibility for prior hotkey settings, with safe fallback to new defaults

## Goals / Non-Goals

### Goals
- Global hotkeys MUST work in macOS App Store sandbox build without requiring Input Monitoring.
- Users MUST be able to assign symbol keys (`-=[];\',./` and similar) reliably.
- “Character-stable” semantics: a hotkey configured as a printable character MUST correspond to that character under the current keyboard layout.
- Robust failure handling: no silent success when registration fails; provide actionable feedback; fallback to defaults.

### Non-Goals
- Supporting multi-character sequences, dead-key composition, or IME-based multi-step input as hotkey main keys.
- Providing OS-level conflict enumeration across all platforms (best-effort detection only).

## Decisions

### Decision 1: Introduce a structured hotkey model (B2)
**Choice**: Replace string-only hotkey configuration with a model:
- `HotkeyGesture`: `Modifiers` + `KeySpec`
- `KeySpec`:
  - `CharKeySpec` (single printable character)
  - `NamedKeySpec` (F1-F24, Enter, Tab, Esc, arrows, etc.)

**Rationale**:
- Eliminates ambiguity like `"OemMinus"` vs `"-"`.
- Enables layout-aware character resolution.

**Alternatives considered**:
- Keep string format and expand parsing: rejected due to ambiguity and cross-platform mismatch.

### Decision 2: macOS uses Carbon `RegisterEventHotKey` for global hotkeys
**Choice**: Implement macOS provider on top of `RegisterEventHotKey` + `EventHotKeyID` handler, via a tiny native helper (Carbon) rather than managed polling.

**Rationale**:
- Avoids Input Monitoring permission.
- Better reliability for packaged/sandbox distributions.

**Trade-offs**:
- Requires mapping to macOS virtual keycodes and modifiers; cannot register “character” directly.
 - Adds a small native build step for macOS (`clang -dynamiclib ... -framework Carbon`), but keeps managed code simple and avoids fragile runloop bridging.

### Decision 3: Character-stable resolution via platform keyboard layout services
**Choice**: Add `IHotkeyResolver` that turns a `HotkeyGesture` into a platform-specific `ResolvedHotkey`:
- `PlatformKeyCode`
- `PlatformModifiers` (includes user-selected modifiers + implicit modifiers required to produce the character)

**Key point**: For `CharKeySpec`, the resolver must:
- Determine a (keycode, modifiers) pair that produces the target character under the current layout.
- Prefer no implicit modifiers; otherwise allow Shift/Option/AltGr as needed.

**Windows approach**:
- Use Win32 layout APIs (e.g., `VkKeyScanEx`, `ToUnicodeEx`) against the active keyboard layout to derive VK + shift state.

**macOS approach**:
- Use current input source and layout translation APIs to derive `kVK_ANSI_*` + shift/option flags needed to yield the character.

### Decision 4: Layout changes trigger re-registration
**Choice**: Introduce `IKeyboardLayoutMonitor` per OS and re-register when layout changes.

**Rationale**:
- Character-stable semantics require updating the physical mapping when users switch layouts.

**Mitigation**:
- Debounce layout events to avoid thrashing; re-register only if resolved chords changed.

### Decision 5: No backward compatibility; fail-safe defaults
**Choice**: Do not attempt to migrate previous hotkey settings. If settings are invalid/unparseable:
- Use new default gestures
- Persist the defaults (overwrite) to keep future loads stable

**Rationale**:
- Reduces complexity and avoids legacy ambiguity.

## Risks / Trade-offs
- **Layout mapping correctness**: needs careful testing on common layouts (US, UK, CN Pinyin + US layout, etc.).
  - Mitigation: resolver unit tests; manual verification matrix; robust fallback.
- **Conflict/unavailable hotkeys**: some chords cannot be registered.
  - Mitigation: registration returns explicit failure; UI indicates; fallback to safe defaults.
- **Implicit modifiers surprise**: character resolution may add Shift/Option/AltGr.
  - Mitigation: display effective resolved chord in UI.

## Migration Plan
- Ship new settings model and defaults.
- On startup, if settings cannot be loaded/validated, replace with defaults and persist.
- On macOS, switch provider implementation to Carbon-based registration.
- Update Settings UI to capture printable characters reliably and write `CharKeySpec` where appropriate.

## Open Questions
- Exact set of supported named keys (beyond F1-F12 and basic navigation).
- Linux strategy (currently unsupported in codebase); decide whether to keep unsupported or implement X11/Wayland registration later.


