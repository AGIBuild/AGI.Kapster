# GitHub Actions Workflows

This directory contains the complete CI/CD automation for AGI.Kapster using GitHub Actions with composite actions and reusable workflows.

## Architecture Overview

The workflow system is built with modular composite actions that provide reusable functionality across multiple workflows.

### Composite Actions

Located in `.github/actions/`, these provide reusable building blocks:

| Action | Purpose | Documentation |
|--------|---------|---------------|
| [`setup-environment`](./actions/setup-environment/) | Complete environment setup with checkout, .NET, and caching | [README](./actions/setup-environment/README.md) |
| [`setup-dotnet-only`](./actions/setup-dotnet-only/) | Lightweight .NET setup without checkout | [README](./actions/setup-dotnet-only/README.md) |
| [`build-and-test`](./actions/build-and-test/) | Build, test, and generate coverage reports | [README](./actions/build-and-test/README.md) |
| [`publish-package`](./actions/publish-package/) | Multi-platform publishing and packaging | [README](./actions/publish-package/README.md) |
| [`setup-dotnet-env`](./actions/setup-dotnet-env/) | Legacy .NET environment setup | [README](./actions/setup-dotnet-env/README.md) |

### Workflows

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| [`ci.yml`](./workflows/ci.yml) | Push, PR | Main CI pipeline with builds and previews |
| [`quality.yml`](./workflows/quality.yml) | Push to main | Quality assurance with comprehensive testing |
| [`release.yml`](./workflows/release.yml) | Release creation | Automated release publishing |
| [`create-release.yml`](./workflows/create-release.yml) | Manual trigger | Create new releases |
| [`verify-version.yml`](./workflows/verify-version.yml) | PR | Version validation |

### Reusable Workflows

| Workflow | Purpose | Used By |
|----------|---------|---------|
| [`_reusable-build.yml`](./workflows/_reusable-build.yml) | Standardized build process | ci.yml, quality.yml |
| [`_reusable-multiplatform.yml`](./workflows/_reusable-multiplatform.yml) | Multi-platform publishing | release.yml |

## Workflow Execution Flow

### CI Pipeline (`ci.yml`)
```
Trigger: Push/PR → Setup Environment → Build & Test → Build Preview (if PR)
```

### Quality Assurance (`quality.yml`)
```
Trigger: Push to main → Setup Environment → Build & Test → Upload Coverage → Multi-platform Publish
```

### Release (`release.yml`)
```
Trigger: Release → Multi-platform Publish → Create Packages → Upload Assets
```

## Key Features

### 🚀 Performance Optimizations
- **Composite Actions**: Reduce workflow duplication and maintenance
- **NuGet Caching**: Faster package restoration across builds
- **Artifact Caching**: Efficient storage and retrieval of build outputs
- **Conditional Execution**: Only run necessary steps based on triggers

### 🔧 Build System Integration
- **NUKE Build System**: Uses `build.ps1` with PowerShell parameter arrays
- **Multi-Platform Support**: Windows, Linux, macOS publishing
- **Configuration Management**: Debug/Release builds with proper artifacts

### 📊 Testing & Quality
- **Unit Testing**: Comprehensive test execution with coverage
- **Coverage Reporting**: Cobertura format with artifact upload
- **Build Verification**: Artifact validation and dependency checks

### 📦 Package Management
- **Multi-Platform Packages**: Platform-specific installers (MSI, DEB, PKG)
- **Artifact Organization**: Structured output with runtime-specific paths
- **Release Automation**: Automatic asset uploading to GitHub releases

## Environment Variables

The following environment variables are used across workflows:

| Variable | Purpose | Set By |
|----------|---------|--------|
| `DOTNET_NOLOGO` | Suppress .NET logo | setup-environment |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | Disable telemetry | setup-environment |
| `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` | Skip first-run experience | setup-environment |

## Artifact Structure

Workflows generate the following artifacts:

```
artifacts/
├── test-results/           # Test output and coverage
│   ├── coverage.cobertura.xml
│   └── test-results.xml
├── publish/               # Platform-specific binaries
│   ├── win-x64/
│   ├── linux-x64/
│   └── osx-x64/
└── packages/              # Installable packages
    ├── AGI.Kapster-win-x64.msi
    ├── AGI.Kapster-linux-x64.deb
    └── AGI.Kapster-osx-x64.pkg
```

## Configuration

### .NET Configuration
- **Version**: 9.0.x
- **Runtime**: Multi-platform targeting

### Cache Configuration
- **NuGet**: Based on lock files and OS
- **Build**: Intermediate build outputs
- **Packages**: Generated installers and binaries

## Usage Examples

### Adding a New Platform
1. Update `_reusable-multiplatform.yml` with new runtime ID
2. Add platform-specific packaging in `publish-package` action
3. Update documentation with new platform support

### Modifying Build Process
1. Update `build/BuildTasks.cs` for new NUKE targets
2. Modify `build-and-test` action for new parameters
3. Test changes in `ci.yml` workflow

### Adding New Tests
1. Add test projects following existing patterns
2. Ensure coverage collection in `build-and-test` action
3. Verify artifact upload in `quality.yml` workflow

## Troubleshooting

### Common Issues
- **Checkout Dependencies**: Ensure actions requiring repository access are called after checkout
- **Parameter Passing**: Use PowerShell parameter arrays for complex NUKE arguments
- **Artifact Paths**: Verify platform-specific paths match NUKE build output
- **Cache Misses**: Check cache key generation and lock file changes

### Debug Information
Enable debug output by setting workflow inputs:
```yaml
with:
  debug: 'true'  # Available in build-and-test action
```

## Migration Notes

This workflow system has evolved from:
- Individual action steps → Composite actions
- Duplicate workflows → Reusable workflows  
- .NET 8.0 + 9.0 → .NET 9.0 only
- Test-dependent publishing → Independent publishing
- Manual parameter passing → PowerShell arrays