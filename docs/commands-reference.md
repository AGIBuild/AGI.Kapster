# AGI.Captor Commands Reference

## 🚀 Quick Start

```powershell
# Clone project
git clone https://github.com/AGIBuild/AGI.Captor.git
cd AGI.Captor

# Get project information
.\build.ps1 Info

# Complete build with tests
.\build.ps1 Clean Build Test
```

## 🔧 Build Commands

### Basic Build Operations
```powershell
# Clean build output
.\build.ps1 Clean

# Restore NuGet packages
.\build.ps1 Restore

# Compile all projects
.\build.ps1 Build

# Run unit tests
.\build.ps1 Test

# Build with tests (most common)
.\build.ps1 Build Test
```

### Advanced Build Options
```powershell
# Build specific configuration
.\build.ps1 Build --configuration Release
.\build.ps1 Build --configuration Debug

# Build with verbose output
.\build.ps1 Build --verbosity normal

# Skip dependencies
.\build.ps1 Test --skip Build
```

### Multi-Platform Publishing
```powershell
# Publish for current platform
.\build.ps1 Publish

# Publish for specific runtime
.\build.ps1 Publish --runtime-id win-x64
.\build.ps1 Publish --runtime-id linux-x64
.\build.ps1 Publish --runtime-id osx-x64

# Publish multiple platforms
.\build.ps1 Publish --runtime-id win-x64,linux-x64,osx-x64
```

### Package Creation
```powershell
# Create packages for all platforms
.\build.ps1 Package

# Create package for specific platform
.\build.ps1 Package --runtime-id win-x64

# Package with specific configuration
.\build.ps1 Package --configuration Release
```

## 🧪 Testing Commands

### Unit Testing
```powershell
# Run all tests
.\build.ps1 Test

# Run tests with coverage collection
.\build.ps1 Test --collect-coverage

# Run specific test project
dotnet test tests/AGI.Captor.Tests/

# Run specific test class
dotnet test --filter "ClassName=OverlayServiceTests"

# Run specific test method
dotnet test --filter "MethodName=ShouldCreateOverlay"

# Verbose test output
dotnet test --verbosity normal
```

### Coverage Analysis
```powershell
# Generate coverage report
.\build.ps1 Test --collect-coverage

# View coverage in browser
start artifacts/test-results/coverage.html

# Generate Cobertura format
dotnet test --collect:"XPlat Code Coverage" --results-directory artifacts/test-results/
```

## 📦 Packaging Commands

### Automated Packaging
```powershell
# Create all platform packages
.\build.ps1 Package

# Windows MSI package
.\build.ps1 Package --runtime-id win-x64

# Linux packages
.\build.ps1 Package --runtime-id linux-x64

# macOS packages
.\build.ps1 Package --runtime-id osx-x64
```

### Manual Packaging
```powershell
# Windows MSI (using WiX)
cd packaging/windows
dotnet build AGI.Captor.wixproj

# Linux DEB package
cd packaging/linux
./create-deb.sh

# Linux RPM package
cd packaging/linux
./create-rpm.sh

# macOS PKG package
cd packaging/macos
./create-pkg.sh
```

## 🌿 Git Workflow Commands

### Branch Operations
```bash
# Create feature branch
git checkout -b feature/new-overlay-mode

# Create release branch
git checkout -b release/2025.9.23

# Switch to main branch
git checkout main

# Delete feature branch
git branch -d feature/old-feature
git push origin --delete feature/old-feature
```

### Tagging for Releases
```bash
# Create release tag
git tag v2025.9.23.1200

# Create annotated tag
git tag -a v2025.9.23.1200 -m "Release version 2025.9.23.1200"

# Push tag to trigger release workflow
git push origin v2025.9.23.1200

# Delete tag
git tag -d v2025.9.23.1200
git push origin --delete v2025.9.23.1200
```

### Commit Conventions
```bash
# Feature commit
git commit -m "feat: add new overlay selection mode"

# Bug fix commit
git commit -m "fix: resolve memory leak in overlay rendering"

# Breaking change
git commit -m "feat!: redesign overlay API"

# Documentation update
git commit -m "docs: update build system documentation"

# Refactoring
git commit -m "refactor: simplify overlay manager architecture"
```

## 🔍 Debugging and Diagnostics

### Application Logs
```powershell
# View recent application logs
Get-Content logs/app-*.log | Select-Object -Last 50

# Monitor logs in real-time
Get-Content logs/app-*.log -Wait

# Filter error logs
Get-Content logs/app-*.log | Select-String "ERROR"
```

### System Information
```powershell
# .NET information
dotnet --info

# Check .NET versions
dotnet --list-sdks
dotnet --list-runtimes

# Environment variables
Get-ChildItem Env: | Where-Object Name -like "*DOTNET*"

# NUKE build information
.\build.ps1 Info
```

### Build Diagnostics
```powershell
# Verbose build output
.\build.ps1 Build --verbosity diagnostic

# Clean and rebuild everything
.\build.ps1 Clean Restore Build --force

# Check build dependencies
.\build.ps1 --help
```

## 🚀 GitHub Actions Integration

### Local Testing
```powershell
# Simulate CI build
.\build.ps1 Clean Restore Build Test

# Simulate quality build
.\build.ps1 Clean Build Test Publish

# Test multi-platform publishing
.\build.ps1 Publish --runtime-id win-x64,linux-x64,osx-x64
```

### Workflow Triggers
```bash
# Trigger CI workflow
git push origin main
git push origin feature/branch-name

# Trigger quality workflow
git push origin main

# Trigger release workflow
git tag v2025.9.23.1200
git push origin v2025.9.23.1200
```

## 📚 Common Workflows

### Development Cycle
```powershell
# 1. Clean development build
.\build.ps1 Clean Build Test

# 2. Run with coverage
.\build.ps1 Test --collect-coverage

# 3. Fix issues and repeat
.\build.ps1 Build Test
```

### Release Preparation
```powershell
# 1. Complete build with all platforms
.\build.ps1 Clean Build Test Publish Package

# 2. Verify artifacts
Get-ChildItem artifacts/publish/
Get-ChildItem artifacts/packages/

# 3. Create release tag
git tag v2025.9.23.1200
git push origin v2025.9.23.1200
```

### Troubleshooting Build Issues
```powershell
# 1. Clean everything
.\build.ps1 Clean
Remove-Item artifacts/ -Recurse -Force -ErrorAction SilentlyContinue

# 2. Restore dependencies
.\build.ps1 Restore

# 3. Build with verbose output
.\build.ps1 Build --verbosity diagnostic

# 4. Check for compilation errors
.\build.ps1 Build 2>&1 | Select-String "error"
```

## 💡 Performance Tips

### Build Optimization
```powershell
# Skip clean for faster incremental builds
.\build.ps1 Build Test

# Use specific configuration
.\build.ps1 Build --configuration Debug  # Faster compilation

# Skip tests during development
.\build.ps1 Build --skip Test
```

### Parallel Processing
```powershell
# Enable parallel builds (default in NUKE)
.\build.ps1 Build --parallel

# Limit parallel degree
.\build.ps1 Build --parallel --max-cpu-count 4
```

## 🔧 Tool Configuration

### PowerShell Aliases
```powershell
# Add to PowerShell profile ($PROFILE)
New-Alias -Name build -Value ".\build.ps1"
New-Alias -Name test -Value ".\build.ps1 Test"
New-Alias -Name clean -Value ".\build.ps1 Clean"

# Usage after aliases
build Build Test
test --collect-coverage
clean
```

### Environment Setup
```powershell
# Set default configuration
$env:Configuration = "Release"

# Set default runtime
$env:RuntimeIdentifier = "win-x64"

# Enable .NET CLI telemetry opt-out
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "true"
```

## 📖 Related Documentation

- [Build System](build-system.md) - Detailed build system architecture
- [GitHub Actions Workflows](../.github/README.md) - CI/CD pipeline documentation
- [Testing Architecture](testing-architecture.md) - Testing strategy and patterns
- [Packaging Guide](packaging-guide.md) - Platform-specific packaging details
- [Release Workflow](release-workflow.md) - Release process and automation

---
*Last updated: September 2025 · NUKE build system with GitHub Actions integration*