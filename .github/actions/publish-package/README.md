# Publish Package Action

A composite GitHub Action for publishing .NET applications to specific runtime platforms.

## Features

- **Multi-Platform Publishing**: Builds for specific runtime identifiers
- **Package Creation**: Optional platform-specific package generation
- **Artifact Management**: Organizes published binaries by platform
- **Build Verification**: Validates published artifacts

## Usage

```yaml
- name: Publish Package
  uses: ./.github/actions/publish-package
  with:
    runtime-id: 'win-x64'        # Required: Target platform
    configuration: 'Release'     # Optional: Build configuration (default: Release)
    package: 'true'             # Optional: Create packages (default: true)
```

## Inputs

| Input | Description | Required | Default |
|-------|-------------|----------|---------|
| `runtime-id` | Target runtime identifier | Yes | - |
| `configuration` | Build configuration | No | `Release` |
| `package` | Create platform-specific packages | No | `true` |

## Outputs

| Output | Description |
|--------|-------------|
| `artifact-path` | Path to published artifacts |

## Supported Runtime IDs

- `win-x64` - Windows 64-bit
- `linux-x64` - Linux 64-bit  
- `osx-x64` - macOS Intel
- `osx-arm64` - macOS Apple Silicon

## Examples

### Windows Publishing
```yaml
- uses: ./.github/actions/publish-package
  with:
    runtime-id: 'win-x64'
```

### Linux without Packages
```yaml
- uses: ./.github/actions/publish-package
  with:
    runtime-id: 'linux-x64'
    package: 'false'
```

### Debug Build
```yaml
- uses: ./.github/actions/publish-package
  with:
    runtime-id: 'osx-arm64'
    configuration: 'Debug'
```

## What It Does

This action performs the following steps:

1. **Clean Build**: Removes previous build artifacts
2. **Multi-Platform Publish**: Publishes for the specified runtime
3. **Package Creation**: Creates platform-specific packages (if enabled)
4. **Artifact Verification**: Validates published output

## Artifacts Generated

This action generates:

- `artifacts/publish/{runtime-id}/` - Published application binaries
- `artifacts/packages/` - Platform-specific packages (if enabled)

## Output Structure

```
artifacts/
├── publish/
│   ├── win-x64/
│   │   ├── AGI.Kapster.exe
│   │   └── ... (dependencies)
│   ├── linux-x64/
│   │   ├── AGI.Kapster
│   │   └── ... (dependencies)
│   └── osx-x64/
│       ├── AGI.Kapster
│       └── ... (dependencies)
└── packages/
    ├── AGI.Kapster-win-x64.msi
    ├── AGI.Kapster-linux-x64.deb
    └── AGI.Kapster-osx-x64.pkg
```

## Prerequisites

This action requires:
- Repository checkout
- .NET environment setup

```yaml
steps:
- name: 📥 Checkout repository
  uses: actions/checkout@v4
  
- name: 🛠️ Setup Environment
  uses: ./.github/actions/setup-environment
  
- name: 📦 Publish Package
  uses: ./.github/actions/publish-package
  with:
    runtime-id: 'win-x64'
```