# Commands Reference

## Build Commands

### PowerShell Build Script
```powershell
# Basic build
.\build.ps1

# Specific target
.\build.ps1 [Target]

# With parameters
.\build.ps1 [Target] --parameter value
```

### Available Targets

#### Core Targets
```powershell
# Clean build artifacts
.\build.ps1 Clean

# Restore NuGet packages
.\build.ps1 Restore

# Build all projects
.\build.ps1 Build

# Run unit tests
.\build.ps1 Test

# Build and test (default)
.\build.ps1
```

#### Publishing Targets
```powershell
# Publish for current platform
.\build.ps1 Publish

# Publish for specific runtime
.\build.ps1 Publish --runtime win-x64
.\build.ps1 Publish --runtime osx-x64
.\build.ps1 Publish --runtime linux-x64

# Create packages
.\build.ps1 Package
```

#### Version Management
```powershell
# Display version information
.\build.ps1 Info

# Generate new time-based version
.\build.ps1 UpgradeVersion

# Verify version consistency
.\build.ps1 CheckVersionLocked
```

## Development Workflow

### Daily Development
```powershell
# Start development session
.\build.ps1 Clean Build

# Run tests during development
.\build.ps1 Test

# Full validation before commit
.\build.ps1 Clean Test
```

### Release Preparation
```powershell
# Update version
.\build.ps1 UpgradeVersion

# Verify build for all targets
.\build.ps1 Clean Build Test Package

# Create release artifacts
.\build.ps1 Publish --runtime win-x64
.\build.ps1 Publish --runtime osx-x64
.\build.ps1 Publish --runtime linux-x64
```

## .NET CLI Commands

### Project Management
```bash
# Build solution
dotnet build AGI.Captor.sln

# Run tests
dotnet test AGI.Captor.sln

# Publish application
dotnet publish src/AGI.Captor.Desktop/AGI.Captor.Desktop.csproj
```

### Package Management
```bash
# Restore packages
dotnet restore

# Add package
dotnet add package [PackageName]

# Update packages
dotnet list package --outdated
dotnet add package [PackageName] --version [Version]
```

## GitHub CLI Commands

### Release Management
```bash
# List releases
gh release list

# Create release
gh release create v2024.9.23.1 --title "Release 2024.9.23.1"

# View release
gh release view v2024.9.23.1
```

### Workflow Management
```bash
# List workflow runs
gh run list

# View workflow run
gh run view [run-id]

# Trigger workflow
gh workflow run release.yml
```

### Repository Commands
```bash
# Clone repository
gh repo clone AGIBuild/AGI.Captor

# Create pull request
gh pr create --title "Feature: New overlay mode"

# View pull requests
gh pr list
```

## Git Commands

### Branch Management
```bash
# Create feature branch
git checkout -b feature/new-feature

# Switch to release branch
git checkout release

# Merge feature branch
git merge feature/new-feature
```

### Version Tagging
```bash
# Create version tag
git tag v2024.9.23.1

# Push tag
git push origin v2024.9.23.1

# List tags
git tag -l

# Delete tag
git tag -d v2024.9.23.1
git push origin --delete v2024.9.23.1
```

### Repository Operations
```bash
# Check status
git status

# View commit history
git log --oneline -10

# View changes
git diff
git diff --cached
```

## Testing Commands

### Unit Testing
```powershell
# Run all tests
.\build.ps1 Test

# Run tests with coverage
.\build.ps1 Test --collect-coverage

# Run specific test
dotnet test --filter "TestClassName"
```

### Test Reporting
```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# View coverage results
# Coverage reports are in TestResults/ directory
```

## Debugging Commands

### Build Diagnostics
```powershell
# Verbose build output
.\build.ps1 Build --verbosity detailed

# Diagnostic output
.\build.ps1 Build --verbosity diagnostic

# Build with specific configuration
.\build.ps1 Build --configuration Debug
```

### Environment Information
```powershell
# Display build info
.\build.ps1 Info

# Check .NET installation
dotnet --info

# List installed SDKs
dotnet --list-sdks
```

## Package Management

### NuGet Commands
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# List package sources
dotnet nuget list source

# Search packages
dotnet search [PackageName]
```

### Project Dependencies
```bash
# List project references
dotnet list reference

# Add project reference
dotnet add reference ../OtherProject/OtherProject.csproj

# List package dependencies
dotnet list package
```

## Platform-Specific Commands

### Windows
```powershell
# Build MSI installer (requires WiX)
.\build.ps1 Package --runtime win-x64

# Install/uninstall service
sc create AGI.Captor binPath="path\to\exe"
sc delete AGI.Captor
```

### macOS
```bash
# Build PKG installer
./build.ps1 Package --runtime osx-x64

# Install/uninstall PKG
sudo installer -pkg AGI.Captor.pkg -target /
pkgutil --pkgs | grep agicaptor
```

### Linux
```bash
# Build DEB package
./build.ps1 Package --runtime linux-x64

# Install/uninstall DEB
sudo dpkg -i agi-captor.deb
sudo dpkg -r agi-captor
```

## Troubleshooting Commands

### Common Issues
```powershell
# Clear all build artifacts
.\build.ps1 Clean

# Reset NuGet packages
Remove-Item -Recurse -Force packages/
.\build.ps1 Restore

# Check for build errors
.\build.ps1 Build --verbosity diagnostic
```

### Performance Diagnostics
```powershell
# Build with timing
.\build.ps1 Build --verbosity diagnostic | Select-String "Time Elapsed"

# Memory usage during build
Get-Process dotnet | Select-Object Name, CPU, WorkingSet
```

### Version Issues
```powershell
# Check version consistency
.\build.ps1 CheckVersionLocked

# Force version regeneration
.\build.ps1 UpgradeVersion --force

# View version history
git log --oneline version.json
```

## CI/CD Commands

### Local CI Simulation
```powershell
# Simulate CI build
.\build.ps1 Clean Restore Build Test Package

# Test publish workflow
.\build.ps1 Publish --runtime win-x64 --output ./artifacts/win-x64
```

### GitHub Actions Integration
```yaml
# In workflow file
- name: Build and Test
  run: .\build.ps1 Test
  
- name: Create Packages
  run: .\build.ps1 Package --runtime ${{ matrix.runtime }}
```

## Quick Reference

### Most Common Commands
```powershell
# Daily development
.\build.ps1                    # Build and test
.\build.ps1 Clean              # Clean artifacts
.\build.ps1 Test               # Run tests only

# Release workflow
.\build.ps1 UpgradeVersion     # New version
.\build.ps1 Package            # Create packages
git tag v$(cat version.json | jq -r .version)  # Create tag
```

### Emergency Commands
```powershell
# Complete reset
git clean -fdx
.\build.ps1 Restore Build Test

# Rollback release
git tag -d v2024.9.23.1
git push origin --delete v2024.9.23.1
gh release delete v2024.9.23.1
```