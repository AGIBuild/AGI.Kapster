# AGI.Captor Documentation Index

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

### Project Planning
- **[Plan A Task Breakdown](planA-task-breakdown.md)** - Original project plan and task breakdown

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
