##  Release Workflow & Signing Improvements

### Summary
- Add optional Windows Authenticode & macOS codesign/notarization steps to elease.yml.
- Introduce environment-variable driven gating (no direct secrets in conditionals) for signing.
- Update uild-system.md and packaging/README.md with signing instructions & verification.
- Clarify release artifact integrity (SHA256 manifest unchanged) and categorized changelog generation.

### Key Changes
- .github/workflows/release.yml: add signing blocks, macOS notarization, environment placeholders.
- docs/build-system.md: add optional signing/notarization section & env matrix.
- packaging/README.md: CI secrets table, verification commands, security notes.
- Related doc refinements (packaging-guide.md, elease-workflow.md, ersioning-strategy.md).

### Environment Variables (Secrets) Required (optional feature)
| Purpose | Env Var |
| ------- | ------- |
| Windows MSI signing | CODE_SIGN_WINDOWS_PFX_BASE64 |
| Windows MSI signing | CODE_SIGN_WINDOWS_PFX_PASSWORD |
| macOS codesign | MACOS_SIGN_IDENTITY |
| macOS notarize | MACOS_NOTARIZE_APPLE_ID |
| macOS notarize | MACOS_NOTARIZE_PASSWORD |
| macOS notarize | MACOS_NOTARIZE_TEAM_ID |

> If any group is missing, its step is skipped gracefully.

### Verification
- Windows: signtool verify /pa /all *.msi
- macOS: codesign --verify --deep --strict -v <pkg/dmg> then xcrun stapler validate <pkg>
- Integrity: shasum -a 256 -c SHASUMS-<version>.txt

### Rationale
Avoids linter issues with secrets.* in if blocks, keeps signing optional, and documents operational steps for secure enablement.

### Follow Ups (Optional)
- Import macOS Developer ID certificate dynamically (if using p12) before codesign.
- Consider remote signing service for EV or HSM-backed certs.
- Add DMG notarization if distributing DMG formally.

---
@maintainers Please review and confirm naming of env variables before merging.
