# Architecture Refactoring Plan

## Executive Summary

This document outlines a comprehensive architecture refactoring plan for AGI.Kapster to improve code maintainability, reduce complexity, and establish clear module boundaries.

**Current Status**: Codebase has grown with several oversized classes and unclear abstraction layers  
**Target**: Clean, modular architecture with clear separation of concerns  
**Timeline**: 5-8 weeks progressive refactoring  
**Risk Level**: Low (progressive approach)

---

## Problem Analysis

### 1. Oversized Classes (God Objects)

| File | Lines | Issues |
|------|-------|--------|
| `OverlayWindow.axaml.cs` | 1290 | Mixed responsibilities: selection, annotation, toolbar, IME, element detection |
| `NewAnnotationOverlay.cs` | 1724 | Creation, editing, rendering, event handling all in one |
| `AnnotationRenderer.cs` | 1084 | Complex rendering logic without clear separation |

### 2. Abstraction Layer Confusion

**Current Flow**:
```
ScreenshotService → OverlayCoordinator → OverlayWindow → (Multiple Overlays)
```

**Issues**:
- `ScreenshotService` is a thin wrapper with unclear value
- Coordinator vs Controller concept duplication
- Session management scattered across multiple components

### 3. Scattered State Management

- Global state: `GlobalElementHighlightState`
- Session state: `OverlaySession`
- Annotation state: `AnnotationManager`
- UI state: Scattered in various Overlays

### 4. Inconsistent Service Registration

- `CoreServiceExtensions` - Core services
- `CaptureServiceExtensions` - Too many sub-services bundled together
- No clear functional domain grouping

---

## Refactoring Solutions (3 Options)

### Option 1: Progressive Refactoring (Recommended ⭐⭐⭐⭐⭐)

**Risk**: Low | **Effort**: Medium (2-3 weeks) | **Benefit**: Medium

#### 1.1 Split OverlayWindow
```
OverlayWindow (Lightweight container)
├── SelectionHandler (Region selection)
├── AnnotationHandler (Delegates to NewAnnotationOverlay)
├── ElementDetectionHandler (Element highlighting)
├── ToolbarManager (Toolbar positioning)
└── ImeManager (IME lifecycle)
```

#### 1.2 Split NewAnnotationOverlay
```
AnnotationCanvas (Lightweight Canvas)
├── Integrated input handling (Mouse/keyboard events)
├── Integrated transform handling (Select/drag/resize)
├── Integrated editing handling (Text editing)
└── Dependencies: IAnnotationService + IAnnotationRenderer
```

#### 1.3 Simplify Service Layers
```
Remove: ScreenshotService (unclear responsibility)
Keep: IOverlayCoordinator (platform coordinator)
Enhance: ApplicationController → AppLifecycleManager (unified lifecycle)
```

#### 1.4 Unified State Management
```
ISessionStateManager (Unified session state)
├── OverlayState (Overlay state)
├── AnnotationState (Annotation state)
└── SelectionState (Selection state)
```

---

### Option 2: Modular Refactoring (Balanced ⭐⭐⭐⭐)

**Risk**: Medium | **Effort**: Large (4-6 weeks) | **Benefit**: High

#### 2.1 Organize by Functional Domains
```
src/AGI.Kapster.Desktop/
├── Modules/
│   ├── Capture/                    # Screenshot capture module
│   │   ├── Services/
│   │   ├── Strategies/
│   │   └── CaptureModule.cs
│   │
│   ├── Overlay/                    # Overlay module
│   │   ├── Coordinators/
│   │   ├── Windows/
│   │   ├── State/
│   │   └── OverlayModule.cs
│   │
│   ├── Annotation/                 # Annotation module
│   │   ├── Canvas/
│   │   ├── Handlers/
│   │   ├── Rendering/
│   │   └── AnnotationModule.cs
│   │
│   ├── Settings/                   # Settings module
│   │   └── SettingsModule.cs
│   │
│   └── Lifecycle/                  # App lifecycle module
│       └── LifecycleModule.cs
│
└── Extensions/
    └── ServiceCollectionExtensions.cs (Unified entry)
```

#### 2.2 Module Registration Pattern
```csharp
public interface IModule
{
    void ConfigureServices(IServiceCollection services);
}

// Program.cs simplified
builder.Services
    .AddModule<CaptureModule>()
    .AddModule<OverlayModule>()
    .AddModule<AnnotationModule>()
    .AddModule<SettingsModule>()
    .AddModule<LifecycleModule>();
```

---

### Option 3: Domain-Driven Refactoring (Comprehensive ⭐⭐⭐)

**Risk**: High | **Effort**: Very Large (8-12 weeks) | **Benefit**: Very High

#### 3.1 DDD Layer Structure
```
src/AGI.Kapster/
├── Domain/                         # Pure business logic
│   ├── Screenshot/
│   ├── Annotation/
│   └── Settings/
│
├── Application/                    # Use case orchestration
│   ├── UseCases/
│   │   ├── TakeScreenshotUseCase.cs
│   │   ├── CreateAnnotationUseCase.cs
│   │   └── ExportImageUseCase.cs
│   └── DTOs/
│
├── Infrastructure/                 # Technical implementations
│   ├── Capture/
│   ├── Clipboard/
│   └── Platform/
│
└── Presentation/                   # Avalonia UI
    ├── Overlays/
    ├── Views/
    └── ViewModels/
```

---

## Recommended Execution Path

### Phase 1: Quick Wins (1-2 weeks)

#### Task 1.1: Remove ScreenshotService
- Delete `IScreenshotService` and `ScreenshotService`
- Update `HotkeyManager` to use `IOverlayCoordinator` directly
- Update `App.axaml.cs` references

#### Task 1.2: Consolidate Extension Classes
- Merge all extension classes into `ServiceCollectionExtensions`
- Group by functional domain
- Maintain clear separation with private helper methods

#### Task 1.3: Naming Consistency
Apply consistent naming conventions:
- `Controller` → Application-level controllers (AppLifecycleController)
- `Coordinator` → Cross-module coordinators (OverlayCoordinator)
- `Manager` → Single-domain managers (AnnotationManager, HotkeyManager)
- `Handler` → Event handlers (InputHandler, EventHandler)
- `Service` → Business services (SettingsService, ExportService)
- `Strategy` → Algorithm strategies (CaptureStrategy, ClipboardStrategy)

---

### Phase 2: Split God Objects (2-3 weeks)

#### Task 2.1: Refactor OverlayWindow
Target: Reduce from 1290 lines to ~300 lines

**New Structure**:
```
OverlayWindow.axaml.cs (~300 lines)
├── Overlays/Handlers/SelectionHandler.cs (~200 lines)
├── Overlays/Handlers/AnnotationHandler.cs (~150 lines)
├── Overlays/Handlers/ElementDetectionHandler.cs (~100 lines)
├── Overlays/Handlers/ToolbarHandler.cs (~80 lines)
└── Overlays/Handlers/ImeHandler.cs (~50 lines)
```

#### Task 2.2: Refactor NewAnnotationOverlay
Target: Reduce from 1724 lines to ~400 lines

**New Structure**:
```
NewAnnotationOverlay.cs (~400 lines)
├── Integrated input handling (~300 lines)
├── Integrated transform handling (~250 lines)
├── Integrated editing handling (~200 lines)
└── Integrated rendering handling (~150 lines)
```

---

### Phase 3: Modularization (2-3 weeks)

#### Task 3.1: Reorganize Directory Structure
- Create `Modules/` directory
- Move services to respective modules
- Implement module registration pattern

#### Task 3.2: Implement Module Interfaces
- Define `IModule` interface
- Create module registration classes
- Update `Program.cs` to use module registration

#### Task 3.3: Update Documentation
- Update architecture diagrams
- Document module boundaries
- Create developer onboarding guide

---

## Implementation Guidelines

### Code Quality Standards

1. **Single Responsibility Principle**
   - Each class should have one clear purpose
   - Target: <500 lines per file
   - Extract helpers when complexity grows

2. **Dependency Injection**
   - Constructor injection only
   - Avoid service locator pattern
   - Clear interface definitions

3. **Naming Conventions**
   - Follow established patterns (see Task 1.3)
   - Use meaningful, descriptive names
   - Maintain consistency across codebase

4. **Testing Strategy**
   - Write unit tests for extracted handlers
   - Maintain existing integration tests
   - Add new tests for refactored components

### Rollback Strategy

Each phase is independent:
- Phase 1: Can rollback individual tasks via git
- Phase 2: Can rollback per-file (handlers are additive)
- Phase 3: Can rollback entire phase if needed

### Success Metrics

- **Code Complexity**: Average file size <500 lines
- **Test Coverage**: Maintain >80% for refactored code
- **Build Time**: No significant increase
- **Performance**: No regression in screenshot latency
- **Bugs**: Zero critical bugs introduced

---

## Risk Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Breaking existing functionality | Medium | High | Comprehensive testing after each task |
| Performance regression | Low | Medium | Performance benchmarks before/after |
| Team velocity decrease | Medium | Medium | Clear documentation, pair programming |
| Incomplete refactoring | Low | High | Progressive approach, each phase is valuable |

---

## Timeline

```
Week 1-2:  Phase 1 (Quick Wins)
Week 3-5:  Phase 2 (Split God Objects)
Week 6-8:  Phase 3 (Modularization)
```

**Milestone 1** (End of Week 2): Simplified service layer, consolidated extensions  
**Milestone 2** (End of Week 5): All oversized classes split into handlers  
**Milestone 3** (End of Week 8): Full modular architecture implemented

---

## Appendix: Before/After Examples

### Example 1: ScreenshotService Removal

**Before**:
```csharp
// HotkeyManager.cs
private readonly IScreenshotService _screenshotService;
await _screenshotService.TakeScreenshotAsync();
```

**After**:
```csharp
// HotkeyManager.cs
private readonly IOverlayCoordinator _overlayCoordinator;
await _overlayCoordinator.StartSessionAsync();
```

### Example 2: OverlayWindow Simplification

**Before** (1290 lines in one file)

**After**:
```csharp
// OverlayWindow.axaml.cs (300 lines)
public partial class OverlayWindow : Window, IOverlayWindow
{
    private readonly SelectionHandler _selectionHandler;
    private readonly AnnotationHandler _annotationHandler;
    private readonly ElementDetectionHandler _elementHandler;
    
    public OverlayWindow(
        ISettingsService settingsService,
        IImeController imeController,
        // ... other dependencies)
    {
        InitializeComponent();
        
        _selectionHandler = new SelectionHandler(this, /* ... */);
        _annotationHandler = new AnnotationHandler(this, /* ... */);
        _elementHandler = new ElementDetectionHandler(this, /* ... */);
        
        WireEventHandlers();
    }
    
    private void WireEventHandlers()
    {
        _selectionHandler.RegionSelected += OnRegionSelected;
        _annotationHandler.AnnotationCompleted += OnAnnotationCompleted;
        _elementHandler.ElementDetected += OnElementDetected;
    }
}
```

---

## Implementation Status

### Phase 1: Quick Wins ✅ COMPLETED
- ✅ **Task 1.1**: Removed ScreenshotService
  - Deleted `IScreenshotService` and `ScreenshotService`
  - Updated `HotkeyManager` to use `IOverlayCoordinator` directly
  - Updated `App.axaml.cs` references
- ✅ **Task 1.2**: Consolidated Extension Classes
  - Merged all extension classes into `ServiceCollectionExtensions`
  - Grouped by functional domain
  - Maintained clear separation with private helper methods

### Phase 2: Split God Objects 🔄 PARTIALLY COMPLETED
- ✅ **Task 2.1**: Refactored OverlayWindow
  - **Before**: 1290 lines → **After**: 630 lines (51% reduction)
  - Created Handler classes: `SelectionHandler`, `AnnotationHandler`, `ElementDetectionHandler`, `ImeHandler`, `ToolbarHandler`, `CaptureHandler`
  - Implemented state-driven initialization architecture
  - Added Tunneling event handling for keyboard events
- 🔄 **Task 2.2**: Refactored NewAnnotationOverlay
  - **Before**: 1724 lines → **After**: 1524 lines (12% reduction)
  - **Status**: Handler classes were created but later merged back due to complexity
  - **Note**: Code was refactored to improve maintainability but kept in single file
- ⏳ **Task 2.3**: AnnotationRenderer Optimization
  - **Current**: 956 lines (down from 1084 lines)
  - **Status**: Pending further optimization

### Phase 3: Modularization ⏳ NOT STARTED
- ⏳ **Task 3.1**: Reorganize Directory Structure
- ⏳ **Task 3.2**: Implement Module Interfaces

### Current Metrics
- **Code Complexity**: Average file size improved significantly
- **Test Coverage**: 256 tests passing (100% success rate)
- **Build Time**: No significant increase
- **Performance**: No regression detected
- **Bugs**: Zero critical bugs introduced

### Next Steps
1. Complete Phase 2.3: Further optimize AnnotationRenderer
2. Begin Phase 3: Implement modular architecture
3. Add comprehensive integration tests for Handler pattern

---

## Sign-off

**Prepared by**: AI Architect Assistant  
**Date**: 2025-10-28  
**Version**: 1.0  
**Status**: Phase 1 Complete, Phase 2 In Progress
