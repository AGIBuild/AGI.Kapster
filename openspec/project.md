# Project Context

## Purpose

AGI.Kapster is a high-performance, cross-platform screen capture and annotation tool built with modern .NET technologies. The project aims to provide:

- **Fast & Responsive**: Low-latency screen capture with global hotkeys (`Alt+A` for capture, `Alt+S` for settings)
- **Rich Annotation**: Comprehensive annotation tools (Arrow, Rectangle, Ellipse, Text, Freehand, Mosaic, Emoji)
- **Cross-Platform**: Native support for Windows, macOS, and Linux with platform-specific optimizations
- **User-Friendly**: Intuitive UI with keyboard shortcuts, undo/redo, and quick export options
- **Extensible**: Modular architecture with clear separation of concerns

## Tech Stack

### Core Framework
- **.NET 9.0**: Target framework for all projects
- **Avalonia UI 11.3.6**: Cross-platform UI framework with XAML-based declarative UI
- **C# 13**: Language version with nullable reference types enabled

### Key Libraries
- **CommunityToolkit.Mvvm 8.4.0**: MVVM infrastructure (RelayCommand, ObservableObject, etc.)
- **SkiaSharp 3.119.1**: High-performance 2D graphics for image processing and rendering
- **SharpHook 7.0.2**: Global hotkey registration and keyboard event handling
- **Microsoft.Extensions.Hosting 9.0.9**: Application lifecycle and dependency injection
- **Microsoft.Extensions.Configuration 9.0.9**: Configuration management (appsettings.json)
- **Serilog**: Structured logging with console and file sinks
- **Polly 8.6.4**: Resilience and retry policies

### Development Tools
- **xUnit**: Test framework
- **XPlat Code Coverage**: Code coverage collection
- **NUKE Build**: Build automation
- **WiX Toolset**: Windows installer packaging

## Project Conventions

### Code Style

#### Naming Conventions
- **PascalCase**: Public members, types, namespaces, methods, properties
- **camelCase**: Private fields, local variables, parameters
- **_camelCase**: Private instance fields (with underscore prefix)
- **IPascalCase**: Interface names (prefixed with 'I')
- **NO Hungarian notation**: Avoid type prefixes

#### Class Naming Patterns
Apply consistent suffixes based on responsibility:
- **Controller**: Application-level controllers (e.g., `AppLifecycleController`)
- **Coordinator**: Cross-module coordinators (e.g., `OverlayCoordinator`)
- **Manager**: Single-domain managers (e.g., `AnnotationManager`, `HotkeyManager`)
- **Handler**: Event handlers (e.g., `SelectionHandler`, `InputHandler`)
- **Service**: Business services (e.g., `SettingsService`, `ExportService`)
- **Strategy**: Algorithm strategies (e.g., `CaptureStrategy`, `ClipboardStrategy`)
- **Provider**: Resource/capability providers (e.g., `IHotkeyProvider`)
- **Builder**: Object construction (e.g., `TacticalArrowBuilder`)
- **Renderer**: Rendering logic (e.g., `AnnotationRenderer`)

#### Formatting Rules
- **Indentation**: 4 spaces (no tabs)
- **Line Endings**: CRLF on Windows, LF on Unix
- **Max Line Length**: No hard limit, but prefer readability (<120 chars recommended)
- **Braces**: Opening brace on same line for types, methods, and control structures
- **Expression Bodies**: Prefer expression-bodied members for simple properties/methods
- **Accessibility Modifiers**: Explicit modifiers required (`private`, `public`, etc.)

#### Code Quality Standards
- **Single Responsibility**: Each class should have one clear purpose (target <500 lines/file)
- **XML Documentation**: Required for all public APIs
- **Nullable Reference Types**: Enabled project-wide; handle nullability explicitly
- **Async/Await**: Use async patterns for I/O-bound operations; prefer sync for UI/overlay control
- **LINQ**: Prefer LINQ for collections; avoid excessive chaining
- **Exception Handling**: Handle exceptions appropriately; log at boundaries

#### EditorConfig Settings
```ini
[*.cs]
dotnet_style_qualification_for_field = false:warning
dotnet_style_qualification_for_property = false:warning
dotnet_style_qualification_for_method = false:warning
dotnet_style_qualification_for_event = false:warning
dotnet_style_require_accessibility_modifiers = never:warning

csharp_style_expression_bodied_methods = true:silent
csharp_style_expression_bodied_properties = true:warning
csharp_style_expression_bodied_indexers = true:warning
csharp_style_expression_bodied_accessors = true:warning
```

### Architecture Patterns

#### MVVM Pattern
- **Models**: Data models and domain entities in `Models/`
- **ViewModels**: Presentation logic in `ViewModels/` using `CommunityToolkit.Mvvm`
- **Views**: XAML-based UI in `Views/` and `Overlays/`
- **Commands**: `IRelayCommand` for user actions, separate `ICommand` pattern for undo/redo

#### Dependency Injection
- **Constructor Injection**: Primary injection method
- **Service Lifetimes**:
  - **Singleton**: Services, managers, coordinators (e.g., `SettingsService`, `HotkeyManager`)
  - **Transient**: Short-lived components, builders
  - **Scoped**: Not commonly used (no web context)
- **Service Registration**: Organized by functional domain in `ServiceCollectionExtensions`
- **NO Service Locator**: Avoid `GetService()` calls; prefer constructor injection

#### Modular Organization
```
src/AGI.Kapster.Desktop/
├── Commands/           # Undo/redo command pattern
├── Dialogs/            # Modal dialog windows
├── Extensions/         # Service registration extensions
├── Models/             # Data models and DTOs
├── Overlays/           # Capture overlay windows
│   └── Handlers/       # Handler pattern for overlay logic
├── Rendering/          # Graphics rendering logic
├── Services/           # Business logic and platform abstractions
│   ├── Capture/        # Screenshot capture
│   ├── Clipboard/      # Clipboard operations
│   ├── Export/         # Image export
│   ├── Hotkey/         # Global hotkey management
│   └── Settings/       # Application settings
├── ViewModels/         # MVVM view models
└── Views/              # XAML views
```

#### Handler Pattern (Refactored)
To reduce complexity in large UI components:
- **Extract Handlers**: Break down god objects into focused handler classes
- **Event-Based Communication**: Handlers communicate via events
- **State-Driven**: Handlers manage specific state slices
- **Example**: `OverlayWindow` delegates to `SelectionHandler`, `AnnotationHandler`, `ElementDetectionHandler`, etc.

### OpenSpec Change Documentation

#### Required Files
Every change proposal under `openspec/changes/[change-id]/` must include:
- **`proposal.md`** - Business case, rationale, and impact analysis
- **`tasks.md`** - Detailed implementation checklist with time estimates
- **`specs/[capability]/spec.md`** - Requirements deltas (ADDED/MODIFIED/REMOVED)

#### Optional Files (Add When Appropriate)
- **`design.md`** - Technical decisions and architecture patterns
  - **When to add**: Complex changes with 3+ major design decisions
  - **Content**: Technical alternatives, rationale, trade-offs, implementation patterns
  
- **`DESIGN_REVIEW.md`** - Design review and problem analysis report
  - **When to add**:
    - Refactoring existing specs with significant design changes
    - Discovered critical flaws in original design (security, performance, compatibility)
    - Timeline changes > 30% or scope changes > 50%
    - License/compliance/security issues requiring audit trail
    - Complex platform-specific implementations (3+ platform strategies)
  - **Content**: 
    - Executive summary of issues found
    - Detailed problem analysis with evidence
    - Applied fixes and mitigations
    - Impact metrics (timeline, scope, risk)
    - Validation results and recommendations
  
- **`ANALYSIS.md`** - Problem domain analysis (use for exploratory work)
  - **When to add**: Unclear requirements, research-heavy features
  - **Content**: Problem space exploration, user research, competitive analysis
  
- **`DECISION_LOG.md`** - Architecture decision record (ADR)
  - **When to add**: Multiple alternative solutions considered
  - **Content**: Decision history, context, consequences
  
- **`docs/`** - Supporting documentation directory
  - **When to add**: Large changes requiring multiple auxiliary documents
  - **Content**: Diagrams, API specs, migration guides, platform-specific notes

#### Document Naming Conventions
- Use **SCREAMING_SNAKE_CASE** for top-level review/analysis documents (e.g., `DESIGN_REVIEW.md`, `ANALYSIS.md`)
- Use **kebab-case** for supporting docs in `docs/` subdirectory (e.g., `platform-compatibility.md`)
- Always include `.md` extension

#### Decision Criteria: When to Add DESIGN_REVIEW.md

Use this checklist to determine if a design review document is warranted:

| Criteria | Threshold | Add DESIGN_REVIEW.md? |
|----------|-----------|----------------------|
| **Design Changes** | 3+ major technical decisions | ✅ Yes |
| **Timeline Impact** | Change > 30% | ✅ Yes |
| **Scope Impact** | Change > 50% | ✅ Yes |
| **Risk Level** | HIGH or CRITICAL risks identified | ✅ Yes |
| **Platform Strategies** | 3+ platform-specific implementations | ✅ Yes |
| **Compliance Issues** | License/security/regulatory concerns | ✅ REQUIRED |
| **Refactoring** | Rewriting existing architecture | ✅ Yes |
| **Knowledge Transfer** | Complex domain requiring documentation | ⚠️ Consider |
| **Simple Features** | Straightforward additions | ❌ No (use design.md) |
| **Bug Fixes** | Restoring spec behavior | ❌ No |

#### Example Structure: DESIGN_REVIEW.md

```markdown
# [Feature Name] - Design Review

**Date**: YYYY-MM-DD
**Status**: [Draft|Under Review|Approved]
**Reviewers**: [Names]

## Executive Summary
- Brief overview (2-3 sentences)
- Key issues found (count)
- Impact summary (timeline, scope, risk)

## Issues Found and Fixes Applied

### Issue 1: [Title]
**Problem**: [Description]
**Impact**: [Severity and consequences]
**Fix**: [Solution applied]
**Files Updated**: [List]

[Repeat for each issue]

## Updated Metrics

| Metric | Original | Updated | Change |
|--------|----------|---------|--------|
| Timeline | X weeks | Y weeks | +Z% |
| Task Count | A | B | +C |

## Validation Results
- OpenSpec validation: [PASS/FAIL]
- [Other validations]

## Recommendations
- Priority 1: [Must-have items]
- Priority 2: [Should-have items]
- Priority 3: [Nice-to-have items]

## Conclusion
[Final recommendation: APPROVE/REVISE/REJECT]
```

#### Integration with OpenSpec Workflow

**Stage 1 (Creating Changes)**:
1. Create standard files (`proposal.md`, `tasks.md`, `specs/`)
2. If complexity criteria met, add `DESIGN_REVIEW.md`
3. Validate with `openspec validate [change-id] --strict`
4. Review documents trigger approval gate

**Stage 2 (Implementing Changes)**:
- `DESIGN_REVIEW.md` serves as reference for implementation decisions
- Update document if new issues discovered during implementation

**Stage 3 (Archiving)**:
- Archive all documents including `DESIGN_REVIEW.md`
- Preserved as historical record for future reference

### Testing Strategy

#### Test Categories
- **Unit Tests**: Individual component testing (Services, Commands, Models)
- **Integration Tests**: Component interaction testing (Service composition, workflows)
- **UI Tests**: Avalonia UI component testing (ViewModels, minimal View testing)

#### Coverage Targets
- **Minimum Overall**: 80% code coverage
- **Critical Components**: 90%+ (Services, Commands, Rendering)
- **UI Components**: 70%+ (ViewModels, Views)

#### Test Organization
```
tests/AGI.Kapster.Tests/
├── Commands/           # Undo/redo tests
├── Models/             # Model validation tests
├── Services/           # Service logic tests
└── TestHelpers/        # Mocks, builders, base classes
```

#### Testing Best Practices
- **AAA Pattern**: Arrange-Act-Assert
- **Descriptive Names**: Use `MethodName_Scenario_ExpectedBehavior` pattern
- **Single Assertion**: One logical assertion per test
- **Mock Dependencies**: Use `TestHelpers/` mock services for isolation
- **Test Data Builders**: Use `TestDataBuilder` for fluent test data construction
- **Avalonia Tests**: Inherit from `AvaloniaTestBase` for UI tests

#### Test Execution
```bash
# Run all tests
./build.ps1 Test

# Run with coverage
./build.ps1 Test -Coverage

# Run with coverage and open report
./build.ps1 Test -Coverage -OpenReport

# Run specific test class
dotnet test tests/AGI.Kapster.Tests --filter "FullyQualifiedName~HotkeyManagerTests"
```

### Git Workflow

#### Branching Strategy
- **main**: Stable production branch, protected
- **feature/[name]**: Feature development branches
- **fix/[name]**: Bug fix branches
- **refactor/[name]**: Refactoring branches

#### Commit Conventions (Conventional Commits)
```
feat: add new annotation tool
fix: resolve hotkey registration issue
docs: update installation guide
refactor: simplify overlay management
perf: optimize rendering pipeline
test: add coverage for export service
chore: update dependencies
style: fix formatting inconsistencies
ci: update GitHub Actions workflow
```

**Format**: `<type>(<scope>): <description>`

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `refactor`: Code change without functional change
- `perf`: Performance improvement
- `test`: Add or update tests
- `chore`: Maintenance tasks
- `style`: Code style/formatting
- `ci`: CI/CD changes

#### Pull Request Guidelines
- **Small PRs**: Keep changes focused and reviewable
- **Description**: Include summary, type, testing notes, breaking changes
- **Tests Required**: All PRs must include tests and maintain coverage
- **Review Required**: At least one approval before merge
- **CI Passes**: All GitHub Actions checks must pass
- **Conventional Commits**: Commit messages must follow format

## Domain Context

### Screen Capture Flow
1. **Hotkey Trigger**: User presses `Alt+A` → `HotkeyManager` → `OverlayCoordinator`
2. **Capture Session**: `OverlayCoordinator` starts session → creates `OverlayWindow`
3. **Region Selection**: User drags to select region → `SelectionHandler`
4. **Annotation Phase**: User adds annotations → `NewAnnotationOverlay` → `AnnotationManager`
5. **Export**: User presses `Enter` or `Ctrl+S` → `ExportService` → Clipboard/File

### Overlay System Architecture
- **OverlayCoordinator**: Platform-specific coordinator managing overlay lifecycle
- **OverlayWindow**: Main capture window with region selection and annotation tools
- **NewAnnotationOverlay**: Canvas-like overlay for drawing annotations
- **ToolbarOverlay**: Floating toolbar with annotation tools
- **ElementHighlightOverlay**: UI element detection and highlighting (Windows only)

### Platform Abstractions
Platform-specific implementations:
- **Capture**: `IScreenCaptureStrategy` with Windows/macOS/Linux implementations
- **Clipboard**: `IClipboardService` with platform-specific APIs
- **Hotkey**: `IHotkeyProvider` using SharpHook for cross-platform support
- **Single Instance**: `ISingleInstanceService` to prevent multiple app instances

### Annotation Types
- **Arrow**: Directional arrow with customizable head styles
- **Rectangle**: Filled/outlined rectangles
- **Ellipse**: Filled/outlined ellipses
- **Text**: Multi-line text with font customization
- **Freehand**: Free drawing with path smoothing
- **Mosaic**: Pixelation/blur effect for privacy
- **Emoji**: Insert emoji from picker

### Rendering Pipeline
1. **Capture**: Screenshot captured via platform-specific API
2. **Annotation Rendering**: `AnnotationRenderer` draws annotations on `WriteableBitmap`
3. **Export**: Final image encoded to PNG/JPEG/BMP/TIFF/WebP via `ImageExportService`

## Important Constraints

### Platform-Specific Limitations
- **Windows**: Element detection requires Windows 10 1809+; uses UIAutomation API
- **macOS**: Requires screen recording permissions (macOS 10.15+); unsigned packages need quarantine removal
- **Linux**: X11/Wayland support varies; less extensive testing than Windows/macOS

### Performance Requirements
- **Capture Latency**: <200ms from hotkey to overlay display
- **Annotation Rendering**: 60 FPS smooth drawing
- **Memory**: <200MB baseline, <500MB during capture/annotation
- **Startup Time**: <3 seconds cold start

### Technical Constraints
- **Single Instance**: Only one app instance allowed at a time
- **Global Hotkeys**: Conflicts with other apps possible; configurable hotkeys required
- **AOT Disabled**: Publish AOT disabled for easier packaging (`PublishAot=False`)
- **Nullable Enabled**: All code must handle nullable reference types correctly

### Security Considerations
- **Screen Recording Permissions**: Required on macOS
- **Hotkey Hijacking**: Validate hotkey registration to prevent conflicts
- **File System Access**: Settings stored in user's AppData/config directory
- **No Telemetry**: Privacy-first; no data collection or external API calls

## External Dependencies

### NuGet Packages (Key Dependencies)
- **Avalonia 11.3.6**: UI framework (MIT License)
- **SkiaSharp 3.119.1**: Graphics library (MIT License)
- **SharpHook 7.0.2**: Global hotkeys (MIT License)
- **CommunityToolkit.Mvvm 8.4.0**: MVVM helpers (MIT License)
- **Microsoft.Extensions.*** 9.0.9**: DI, configuration, hosting (MIT License)
- **Serilog**: Logging framework (Apache 2.0 License)
- **Polly 8.6.4**: Resilience and retry (BSD 3-Clause License)

**License Policy**: Only use open-source libraries with permissive licenses (MIT, Apache 2.0, BSD). NO commercial or copyleft (GPL) licenses.

### Build Tools
- **NUKE Build**: Build automation and task orchestration
- **WiX Toolset**: Windows installer (MSI) creation
- **dotnet CLI**: .NET SDK command-line tools

### CI/CD
- **GitHub Actions**: Automated build, test, and release workflows
- **Workflow Triggers**: Push to main, pull requests, manual release
- **Artifacts**: Test results, coverage reports, installers

### File Formats
- **Configuration**: JSON (appsettings.json)
- **Image Export**: PNG (default), JPEG, BMP, TIFF, WebP
- **Logging**: Text files in user's logs directory (Serilog)

## Versioning Strategy

### Version Format
- **Semantic Versioning**: `Year.Month.Day.SecondsSinceMidnight`
- **Example**: `2025.11.4.52762`
- **Components**:
  - `Year`: 4-digit year (e.g., 2025)
  - `Month`: 1-2 digit month (e.g., 11)
  - `Day`: 1-2 digit day (e.g., 4)
  - `SecondsSinceMidnight`: Seconds since midnight UTC (e.g., 52762)

### Version Files
- **version.json**: Centralized version file (auto-generated by build)
- **AssemblyVersion**: Set in `.csproj` file
- **FileVersion**: Set in `.csproj` file
- **InformationalVersion**: Set in `.csproj` file

## Development Environment

### Required Tools
- **.NET 9.0 SDK**: Download from https://dot.net
- **IDE**: Visual Studio 2022 (17.8+) / JetBrains Rider 2023.3+ / VS Code with C# extension
- **Git**: Version control

### Optional Tools
- **ReportGenerator**: Code coverage HTML reports
- **dotCover / Coverlet**: Alternative coverage tools

### Quick Start
```bash
# Clone repository
git clone https://github.com/AGIBuild/AGI.Kapster.git
cd AGI.Kapster

# Restore dependencies
dotnet restore

# Build solution
./build.ps1

# Run application
dotnet run --project src/AGI.Kapster.Desktop

# Run tests
./build.ps1 Test

# Create packages
./build.ps1 Publish
```
