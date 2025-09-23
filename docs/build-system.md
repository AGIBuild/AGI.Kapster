# 🚀 AGI.Captor Build System

## 📋 Overview

AGI.Captor uses a modern **NUKE Build System** integrated with **GitHub Actions** for automated CI/CD workflows.

## 🔧 Build System Architecture

### Local Build Commands
```powershell
# Windows (PowerShell)
.\build.ps1 [Target] [Parameters]

# Cross-platform using .NET
dotnet run --project build -- [Target] [Parameters]
```

### Available Build Targets
- `Clean` - Clean build output directories
- `Restore` - Restore NuGet packages with caching
- `Build` - Compile all projects for current platform
- `Test` - Run unit tests with coverage collection
- `Publish` - Create runtime-specific self-contained builds
- `Package` - Generate platform-specific installers
- `Info` - Display build information and version details

### GitHub Actions Integration
The build system is tightly integrated with GitHub Actions through:
- **Composite Actions**: Reusable workflow components
- **Parameter Arrays**: PowerShell parameter passing to NUKE
- **Artifact Management**: Structured output organization
- **Caching**: NuGet and build artifact caching

## 🎯 CI/CD Workflow Architecture

| Workflow | Purpose | Key Features |
| -------- | ------- | ------------ |
| `ci.yml` | Main CI pipeline | Build, test, preview generation |
| `quality.yml` | Quality assurance | Coverage, multi-platform publishing |
| `release.yml` | Release automation | Multi-platform packages, GitHub releases |
| `verify-version.yml` | Version validation | PR version checks |
| `create-release.yml` | Manual release creation | Controlled release triggers |

### Composite Actions
- **setup-environment**: Complete environment setup with .NET 9.0 and caching
- **build-and-test**: Standardized build and test execution
- **publish-package**: Multi-platform publishing and packaging
- **setup-dotnet-only**: Lightweight .NET setup for specific scenarios

## 🏗️ Build Process Flow

### 1. Environment Setup
```powershell
# .NET 9.0 installation
# NuGet package caching
# Git configuration
# Environment variables
```

### 2. Build Execution
```powershell
# Clean previous artifacts
# Restore dependencies with cache
# Compile for target platform/configuration
# Generate build metadata
```

### 3. Testing Phase
```powershell
# Execute unit tests
# Collect code coverage (Cobertura format)
# Generate test reports
# Upload coverage artifacts
```

### 4. Publishing (Multi-Platform)
```powershell
# Publish for specific runtime identifiers:
# - win-x64, win-arm64
# - linux-x64, linux-arm64
# - osx-x64, osx-arm64
```

### 5. Packaging
```powershell
# Windows: MSI packages
# Linux: DEB/RPM packages  
# macOS: PKG installers
```

## 💡 Usage Examples

### Basic Development Workflow
```powershell
# Clean build with tests
.\build.ps1 Clean Build Test

# Build with coverage collection
.\build.ps1 Build Test --collect-coverage

# Publish for current platform
.\build.ps1 Publish
```

### Multi-Platform Publishing
```powershell
# Publish for specific runtime
.\build.ps1 Publish --runtime-id win-x64

# Publish multiple platforms
.\build.ps1 Publish --runtime-id win-x64,linux-x64,osx-x64
```

### Package Creation
```powershell
# Create packages for all platforms
.\build.ps1 Package

# Create package for specific platform
.\build.ps1 Package --runtime-id win-x64
```

## � Configuration Management

### Build Configuration
- **Debug**: Development builds with symbols
- **Release**: Optimized production builds (default)

### Runtime Identifiers
- `win-x64`: Windows 64-bit
- `win-arm64`: Windows ARM64
- `linux-x64`: Linux 64-bit  
- `linux-arm64`: Linux ARM64
- `osx-x64`: macOS Intel
- `osx-arm64`: macOS Apple Silicon

### Environment Variables
```powershell
# .NET Configuration
DOTNET_NOLOGO=true
DOTNET_CLI_TELEMETRY_OPTOUT=true
DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true

# Build Configuration
Configuration=Release
RuntimeIdentifier=win-x64
```

## 📂 Artifact Organization

### Directory Structure
```
artifacts/
├── test-results/           # Test outputs and coverage
│   ├── coverage.cobertura.xml
│   └── test-results.xml
├── publish/               # Runtime-specific binaries
│   ├── win-x64/
│   ├── linux-x64/
│   └── osx-x64/
└── packages/              # Platform-specific installers
    ├── AGI.Captor-win-x64.msi
    ├── AGI.Captor-linux-x64.deb
    └── AGI.Captor-osx-x64.pkg
```

### Artifact Upload
- **CI Builds**: Test results and coverage reports
- **Quality Builds**: All artifacts with multi-platform support
- **Release Builds**: Complete package sets with checksums

## 🛠️ Development Tools

### IDE Integration
- **Visual Studio**: Native MSBuild integration
- **VS Code**: Tasks and debugging support
- **JetBrains Rider**: NUKE build configuration recognition

### Command Line Tools
```powershell
# Show available targets
.\build.ps1 --help

# Verbose output
.\build.ps1 Build --verbosity normal

# Skip dependencies
.\build.ps1 Test --skip Build
```

## 🔍 Troubleshooting

### Common Issues
| Issue | Cause | Solution |
| ----- | ----- | -------- |
| Build fails with missing .NET | Wrong .NET version | Use setup-environment action |
| Test artifacts missing | Coverage not collected | Enable --collect-coverage |
| Package creation fails | Missing runtime artifacts | Run Publish before Package |
| Cache misses | Lock file changes | Clear NuGet cache manually |

### Debug Commands
```powershell
# Verbose build output
.\build.ps1 Build --verbosity diagnostic

# Skip NuGet restore
.\build.ps1 Build --skip Restore

# Force clean rebuild
.\build.ps1 Clean Build --force
```

## � Related Documentation
- [GitHub Actions Workflows](../.github/README.md)
- [Commands Reference](commands-reference.md)
- [Testing Architecture](testing-architecture.md)
- [Packaging Guide](packaging-guide.md)
- [Release Workflow](release-workflow.md)

---
*Last updated: September 2025 · GitHub Actions integration complete*