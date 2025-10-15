# AGI.Kapster Technical Documentation

Complete technical documentation for a cross-platform screen capture and overlay annotation tool.

## 🏗️ Build & Deployment

### Build System
- **[Build System](build-system.md)** - NUKE-based automated build system
- **[Commands Reference](commands-reference.md)** - Common build and development commands
- **[Versioning Strategy](versioning-strategy.md)** - Version control and release strategy

### CI/CD Pipeline
- **[GitHub Actions Workflows](github-workflow.md)** - Complete CI/CD automation
- **[Release Workflow](release-workflow.md)** - Automated release and packaging

### Testing & Quality
- **[Testing Architecture](testing-architecture.md)** - Testing strategy and organization
- **[Packaging Guide](packaging-guide.md)** - Multi-platform packaging system

## 🏛️ System Architecture

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

<!-- Duplicate section removed to avoid redundancy; see System Architecture above. -->

## 📋 Development Guide

### Core Concepts
- **Dependency Injection Pattern** - All platform services managed through DI container
- **Event-Driven Architecture** - Loosely coupled component communication
- **Multi-Screen Support** - Unified operations across all displays
- **Background-Only Mode** - Pure background operation with system tray integration

### Directory Structure
```
src/AGI.Kapster.Desktop/
├── Services/                 # Services grouped by functionality
│   ├── Overlay/             # Overlay controller and windows
│   ├── Capture/             # Screen capture strategies
│   ├── Clipboard/           # Clipboard operations
│   ├── Settings/            # Settings management
│   └── Update/              # Auto-update services
├── Overlays/                # Avalonia overlay UI and events
├── Rendering/               # Rendering components
└── Views/                   # Settings and dialogs
```

### Recent Updates
- ✅ Removed main window, pure background architecture
- ✅ Service organization refactoring, grouped by functionality
- ✅ Enhanced test coverage, 95+ unit tests
- ✅ File system abstraction for isolated testing

## 📚 Reference Documentation

### History & Status
- **[Project Status](project-status.md)** - Current development status and milestones
- **[Refactoring History](overlay-refactoring-history.md)** - Important design decision records

*Last Updated: October 2025*

*Note: Historical documents are archived for context; current architecture docs take precedence.*
