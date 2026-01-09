# Copilot Instructions for AGI.Kapster

This repository contains AGI.Kapster, a modern cross-platform screen capture and annotation tool built with .NET 9 and Avalonia UI.

## Tech Stack

- **Framework**: .NET 9.0
- **UI Framework**: Avalonia UI 11.3
- **Architecture Pattern**: MVVM (Model-View-ViewModel)
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Graphics**: SkiaSharp for image processing and rendering
- **Logging**: Serilog with structured logging
- **MVVM Toolkit**: CommunityToolkit.Mvvm
- **Testing**: xUnit with XPlat Code Coverage
- **Build System**: NUKE Build (build.ps1/build.sh)

## Project Structure

```
AGI.Kapster/
├── src/AGI.Kapster.Desktop/     # Main application
│   ├── Commands/                # Undo/redo command pattern
│   ├── Dialogs/                 # Dialog windows
│   ├── Models/                  # Data models and DTOs
│   ├── Overlays/                # Screenshot overlay windows
│   ├── Rendering/               # Graphics rendering
│   ├── Services/                # Business logic and platform abstractions
│   ├── ViewModels/              # MVVM view models
│   └── Views/                   # UI definitions (AXAML)
├── tests/AGI.Kapster.Tests/     # Unit and integration tests
├── build/                       # Build scripts and configuration
├── packaging/                   # Package creation scripts
└── docs/                        # Documentation
```

## Coding Standards

### C# Conventions
- **Naming**: Follow standard C# naming conventions
  - PascalCase for public members, types, and namespaces
  - camelCase for private fields and local variables
  - Prefix private fields with underscore: `_fieldName`
- **Indentation**: Use 4 spaces (no tabs)
- **Nullable Reference Types**: Enabled - always handle nullability properly
- **XML Documentation**: Add XML comments for all public APIs
- **Code Style**: EnforceCodeStyleInBuild is enabled in the project

### Async/Await Guidelines
- Prefer synchronous APIs when the codebase uses sync for overlay control
- Use async/await where naturally asynchronous (I/O, network operations)
- Follow existing patterns in the codebase for consistency

### MVVM Pattern
- ViewModels should use CommunityToolkit.Mvvm attributes (`[ObservableProperty]`, `[RelayCommand]`)
- Keep business logic in Services, not ViewModels
- Use dependency injection for all service dependencies
- Views should only contain UI-specific logic

### Platform-Specific Code
- Use platform abstractions in `Services/` directory
- Implement platform-specific code in `Services/Capture/Platforms/` or similar
- Test cross-platform functionality on Windows, macOS, and Linux when possible
- Note: Primary testing is on Windows and macOS; Linux receives less testing

## Build and Test Commands

### Build
```bash
# Full build
./build.ps1

# Build only (no tests)
./build.ps1 Compile

# Publish packages
./build.ps1 Publish
```

### Test
```bash
# Run all tests
./build.ps1 Test

# Run tests with code coverage
./build.ps1 Test -Coverage

# Run tests with coverage and open report
./build.ps1 Test -Coverage -OpenReport

# Run specific test
dotnet test tests/AGI.Kapster.Tests --filter "FullyQualifiedName~TestClassName"
```

### Run Application
```bash
dotnet run --project src/AGI.Kapster.Desktop
```

## Testing Requirements

- **Minimum Code Coverage**: 80%
- **Critical Components Coverage**: 90%+ (Services, Commands)
- **UI Components Coverage**: 70%+ (ViewModels, Views)
- **Test Pattern**: Follow Arrange-Act-Assert (AAA) pattern
- **Test Helpers**: Use provided mock services (MockHotkeyProvider, MockSettingsService, etc.)
- **UI Tests**: Use AvaloniaTestBase for Avalonia UI component testing

## Commit Message Format

Use conventional commits format:
```
feat: add new annotation tool
fix: resolve hotkey registration issue
docs: update installation guide
refactor: simplify overlay management
test: add tests for screenshot service
chore: update dependencies
```

## Important Considerations

### AI-Assisted Development
This project is developed with AI assistance. The code should be:
- Well-documented with clear intent
- Testable and maintainable
- Following established patterns consistently

### Cross-Platform Support
- **Windows**: Primary platform with full testing
- **macOS**: Full testing on both Intel and Apple Silicon
- **Linux**: Implemented but less extensively tested
- Always consider platform differences in screen capture and hotkey handling

### Performance
- Screen capture operations should be optimized for responsiveness
- Overlay rendering should maintain 60 FPS when possible
- Use SkiaSharp efficiently for graphics operations

### Security
- Never commit secrets or API keys
- Use UserSecrets for development credentials
- Handle user data (screenshots) securely

## Common Patterns

### Dependency Injection
```csharp
public class MyService : IMyService
{
    private readonly ILogger<MyService> _logger;
    private readonly ISettingsService _settingsService;
    
    public MyService(ILogger<MyService> logger, ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }
}
```

### ViewModel with Commands
```csharp
public partial class MyViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _myProperty;
    
    [RelayCommand]
    private void ExecuteAction()
    {
        // Implementation
    }
}
```

### Platform-Specific Service
```csharp
public interface IScreenCaptureStrategy
{
    // Prefer synchronous capture for precise overlay control; add async wrappers in callers if needed.
    Bitmap CaptureScreen();
}

// Implementations in Services/Capture/Platforms/
// - WindowsScreenCaptureStrategy.cs
// - MacScreenCaptureStrategy.cs
// - LinuxScreenCaptureStrategy.cs
```

## Documentation

- **User Documentation**: README.md, user guides
- **Developer Documentation**: CONTRIBUTING.md, TESTING.md
- **API Documentation**: XML comments in code
- **Architecture**: Document significant design decisions in code comments

## Resources

- [Contributing Guide](../CONTRIBUTING.md)
- [Testing Guide](../TESTING.md)
- [Avalonia Documentation](https://docs.avaloniaui.net/)
- [.NET Documentation](https://learn.microsoft.com/dotnet/)

## Avoid

- **Don't** use `any` type equivalents - leverage strong typing
- **Don't** add dependencies without checking for security vulnerabilities
- **Don't** break existing tests without good reason
- **Don't** skip XML documentation for public APIs
- **Don't** mix async and sync code patterns unnecessarily
- **Don't** add platform-specific code outside of designated platform abstraction areas
