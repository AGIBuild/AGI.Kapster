# AGI.Captor Documentation Index

## Build and Deployment

### CI/CD System
- **[GitHub Actions Workflows](../.github/README.md)** - Complete CI/CD automation system
  - Composite actions architecture
  - Multi-platform publishing
  - Workflow execution flow
  - Performance optimizations

### Version Management
- **[Versioning Strategy](versioning-strategy.md)** - Locked time-based versioning strategy
  - Branch strategy and version rules
  - Semantic versioning format
  - Commit message conventions
  - CI/CD workflow integration

### Build System
- **[Build System](build-system.md)** - NUKE-based build automation with PowerShell
  - Build targets and configuration
  - Cross-platform build support
  - GitHub Actions integration
  - Packaging and distribution

- **[Commands Reference](commands-reference.md)** - Quick reference for build commands
  - PowerShell build script usage
  - NUKE target execution
  - Testing and packaging commands

### Testing and Quality
- **[Testing Architecture](testing-architecture.md)** - Comprehensive testing strategy
  - Test organization and structure
  - Coverage reporting and CI integration
  - Dependency injection for testing
  - Mocking strategies with NSubstitute

### Packaging and Release
- **[Packaging Guide](packaging-guide.md)** - Multi-platform packaging system
  - Automated GitHub Actions packaging
  - Windows MSI creation
  - Linux DEB/RPM packages
  - macOS PKG and App Store
  
- **[Release Workflow](release-workflow.md)** - Automated release process
  - GitHub Actions release automation
  - Multi-platform artifact generation
  - Release asset management

## Architecture and Design Documents

### Overlay System (截图遮罩层系统)
- **[Overlay System Architecture](overlay-system-architecture.md)** - Complete technical architecture of the overlay system
  - Core interfaces and implementations
  - Event flow and lifecycle management
  - Platform-specific implementations
  - Service registration patterns

- **[Overlay System Quick Reference](overlay-system-quick-reference.md)** - Quick guide for developers
  - Key files and common tasks
  - Event flow diagram
  - Platform differences table
  - Debugging tips

- **[Overlay Refactoring History](overlay-refactoring-history.md)** - Design decisions and evolution
  - Timeline of changes
  - Why factory pattern was removed
  - Migration guide for developers
  - Lessons learned

### Project Planning and Status
- **[Project Status](project-status.md)** - Current development status and roadmap

### Testing and Quality
- **[Testing Architecture](testing-architecture.md)** - Comprehensive testing strategy and patterns
  - Test organization and structure
  - Dependency injection for testing
  - File system abstraction patterns
  - Mocking strategies with NSubstitute
  - Test coverage and best practices

## For AI Agents

When working on the overlay system:
1. Start with the **Quick Reference** for an overview
2. Consult the **Architecture** document for detailed implementation
3. Check the **Refactoring History** to understand design decisions

## Key Concepts

### Service Registration Pattern
All platform-specific services are registered in `Program.cs` based on the runtime OS:
- Windows: Full implementation with UI Automation
- macOS: Partial implementation with planned native APIs
- Linux: Planned for future

### Event-Driven Architecture
The overlay system uses events to maintain loose coupling:
- `RegionSelected` - When user selects a region
- `Cancelled` - When user cancels (ESC key)
- `IsEditableSelection` flag controls whether overlay stays open

### Multi-Screen Support
All overlay operations affect all screens simultaneously through `SimplifiedOverlayManager`.

### Directory Organization (Updated 2024)
The project has been reorganized for better maintainability:
- **Services by Topic**: `Clipboard/`, `Capture/`, `ElementDetection/`, `Export/`, `Settings/`, `Adapters/`
- **Platform Implementations**: Each service has a `Platforms/` subdirectory
- **Rendering Components**: Moved to `Rendering/Overlays/` for better separation
- **Namespace Alignment**: All namespaces match their physical file locations
- **Testable Architecture**: File system abstraction enables comprehensive unit testing
- **Background-Only Operation**: No main window, system tray integration only

### Recent Architectural Changes
- **MainWindow Removal**: Eliminated unused main window components for cleaner architecture
- **SettingsService Refactoring**: Added file system abstraction for testability
- **Test Coverage**: 95 unit tests with comprehensive coverage of all major components
- **Dependency Injection**: Enhanced DI patterns for better testability and maintainability
