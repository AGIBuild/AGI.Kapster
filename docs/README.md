# AGI.Kapster Technical Documentation

Technical documentation for the cross-platform screenshot and annotation tool.

## 🏗️ Build & Deployment
- **[Build System](build-system.md)** - NUKE-based build automation
- **[Commands Reference](commands-reference.md)** - Development commands
- **[Versioning Strategy](versioning-strategy.md)** - Release versioning
- **[GitHub Workflows](github-workflow.md)** - CI/CD automation
- **[Release Workflow](release-workflow.md)** - Release process
- **[Packaging Guide](packaging-guide.md)** - Multi-platform packaging

## 🏛️ Architecture
- **[Overlay System Architecture](overlay-system-architecture.md)** - Overlay system design
- **[Overlay Quick Reference](overlay-system-quick-reference.md)** - Developer guide
- **[Testing Architecture](testing-architecture.md)** - Testing patterns
- **[Rendering Best Practices](rendering-best-practices.md)** - Graphics rendering

## 📋 Development
- **[Project Status](project-status.md)** - Current roadmap
- **[Architecture Refactoring Plan](architecture-refactoring-plan.md)** - Refactoring strategy and progress
- **[Refactoring Completion Report](refactoring-completion-report.md)** - Detailed completion status
- **[Documentation Maintenance Guide](documentation-maintenance-guide.md)** - Documentation upkeep guidelines

### Core Patterns
- **Dependency Injection** - Extension methods for service registration
- **Singleton Services** - ISettingsService with 3-tier config loading
- **Single Instance** - Named Mutex for cross-process enforcement
- **Platform Services** - Interface + platform-specific implementations
- **Background-Only** - System tray integration without main window

### Directory Structure
```
src/AGI.Kapster.Desktop/
├── Extensions/          # DI service registration
├── Services/           # Core services
│   ├── Settings/       # Singleton settings
│   ├── Startup/        # Platform auto-start
│   ├── Hotkeys/        # Global hotkeys
│   ├── Overlay/        # Screenshot overlay
│   ├── Capture/        # Screen capture
│   └── Clipboard/      # Clipboard ops
├── Overlays/           # UI overlays
├── Models/             # Data models
└── Rendering/          # Graphics
```

## 📚 Reference
- **[Overlay Refactoring History](overlay-refactoring-history.md)** - Design decisions
- **[Plan A Breakdown](planA-task-breakdown.md)** - Historical planning (archived)

*Last Updated: October 2025*
