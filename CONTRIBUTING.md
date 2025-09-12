# Contributing Guide

Thank you for your interest in AGI.Captor! We welcome all forms of contributions, including but not limited to:

- 🐛 Bug reports
- 💡 Feature suggestions
- 📖 Documentation improvements
- 💻 Code contributions
- 🌍 Localization

## 🚀 Development Environment Setup

### System Requirements

- **.NET 9.0 SDK** or higher
- **Visual Studio 2022** (recommended) or **JetBrains Rider**
- **Git** version control tool

### Recommended Tools

- **Visual Studio 2022** - Full IDE support with Avalonia extensions
- **JetBrains Rider** - Cross-platform IDE with built-in Avalonia support
- **VS Code** - Lightweight editor (requires C# extension)

### Development Environment Configuration

1. **Clone Repository**
   ```bash
   git clone https://github.com/your-username/AGI.Captor.git
   cd AGI.Captor
   ```

2. **Install Dependencies**
   ```bash
   dotnet restore
   ```

3. **Build Project**
   ```bash
   dotnet build
   ```

4. **Run Application**
   ```bash
   dotnet run --project src/AGI.Captor.App
   ```

5. **Run Tests**
   ```bash
   dotnet test
   ```

## 📁 Project Structure

```
AGI.Captor/
├── src/
│   └── AGI.Captor.App/           # Main application
│       ├── Commands/             # Command pattern implementation
│       ├── Dialogs/              # Dialog windows
│       ├── Models/               # Data models
│       ├── Overlays/             # Screenshot overlay layers
│       ├── Rendering/            # Rendering engine
│       ├── Services/             # Business services
│       ├── ViewModels/           # View models
│       └── Views/                # User interface
├── tests/
│   └── AGI.Captor.Tests/         # Unit tests
├── docs/                         # Project documentation
└── README.md
```

### Core Module Description

- **Commands/**: Command pattern implementation for undo/redo functionality
- **Models/**: Data models including annotation objects, settings, etc.
- **Overlays/**: Screenshot interface implementation including selection, toolbar, etc.
- **Services/**: Core business logic such as hotkeys, export, settings, etc.
- **Rendering/**: Graphics rendering engine responsible for annotation drawing

## 🏗️ Architecture Design

### Technology Stack

- **Framework**: .NET 9.0
- **UI**: Avalonia UI 11.x
- **MVVM**: CommunityToolkit.Mvvm
- **DI**: Microsoft.Extensions.DependencyInjection
- **Logging**: Serilog
- **Graphics**: SkiaSharp
- **Testing**: xUnit

### Design Patterns

- **MVVM**: Separation of view and business logic
- **Dependency Injection**: Service decoupling and test-friendly
- **Command Pattern**: Undo/redo functionality
- **Observer Pattern**: Event-driven architecture
- **Strategy Pattern**: Cross-platform adaptation

### Key Interfaces

```csharp
// Core service interfaces
public interface IHotkeyProvider        // Hotkey management
public interface IOverlayController     // Overlay control
public interface IAnnotationService     // Annotation service
public interface IExportService         // Export service
public interface ISettingsService       // Settings management
```

## 🔄 Development Workflow

### Branch Strategy

- **main**: Main branch, stable releases
- **develop**: Development branch, feature integration
- **feature/***: Feature branches, individual feature development
- **bugfix/***: Fix branches, urgent issue fixes
- **release/***: Release branches, version preparation

### Commit Conventions

Use [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

**Types**:
- `feat`: New features
- `fix`: Bug fixes
- `docs`: Documentation updates
- `style`: Code formatting
- `refactor`: Code refactoring
- `test`: Test-related changes
- `chore`: Build/toolchain changes

**Examples**:
```
feat(overlay): add smart region detection
fix(hotkey): resolve conflict with system shortcuts
docs(readme): update installation instructions
```

### Pull Request Process

1. **Fork Repository** to your account
2. **Create Feature Branch** from `develop` branch
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b feature/your-feature-name
   ```
3. **Develop Feature** and commit code
4. **Test** ensure functionality works correctly
5. **Submit PR** to `develop` branch
6. **Code Review** wait for maintainer review
7. **Merge** after review approval

## 🧪 Testing Guidelines

### Testing Strategy

- **Unit Tests**: Core business logic
- **Integration Tests**: Service interactions
- **UI Tests**: Key user workflows
- **Platform Tests**: Windows/macOS compatibility

### Testing Conventions

- Test file naming: `*Tests.cs`
- Test method naming: `Should_ExpectedBehavior_When_Condition`
- Use AAA pattern: Arrange, Act, Assert

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/AGI.Captor.Tests

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

## 📝 Code Standards

### C# Coding Standards

Follow [.NET Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions):

- **Naming Conventions**:
  - PascalCase: Classes, methods, properties, enums
  - camelCase: Fields, local variables, parameters
  - UPPER_CASE: Constants
  - _camelCase: Private fields

- **Code Organization**:
  - One class per file
  - Use namespaces to organize code
  - Proper use of `using` statements

- **Comments**:
  - XML documentation comments for public APIs
  - Inline comments for complex logic
  - Avoid redundant comments

### XAML Standards

- **Naming**: PascalCase control naming
- **Layout**: Prefer Grid, StackPanel standard layouts
- **Binding**: Use strongly-typed binding
- **Resources**: Proper use of styles and templates

### Code Example

```csharp
/// <summary>
/// Annotation service interface providing core annotation APIs
/// </summary>
public interface IAnnotationService
{
    /// <summary>
    /// Currently selected tool type
    /// </summary>
    AnnotationToolType CurrentTool { get; set; }
    
    /// <summary>
    /// Start creating new annotation item
    /// </summary>
    /// <param name="startPoint">Starting point coordinates</param>
    /// <returns>Created annotation item, null if cannot create</returns>
    IAnnotationItem? StartCreate(Point startPoint);
}
```

## 🐛 Bug Reports

### Before Submitting

1. **Search Existing Issues** ensure bug hasn't been reported
2. **Use Latest Version** confirm issue exists in latest version
3. **Collect Information** prepare detailed reproduction steps

### Bug Report Template

```markdown
## Bug Description
Brief description of the encountered issue

## Reproduction Steps
1. Open application
2. Click...
3. Error occurs

## Expected Behavior
Describe what should happen

## Actual Behavior
Describe what actually happened

## Environment Information
- Operating System: Windows 11 / macOS 13.0
- Application Version: v1.0.0
- .NET Version: 9.0.0

## Additional Information
- Error screenshots
- Log files
- Other relevant information
```

## 💡 Feature Suggestions

### Proposal Process

1. **Search Existing Proposals** avoid duplicates
2. **Create Issue** using feature request template
3. **Community Discussion** gather feedback
4. **Design Review** technical feasibility analysis
5. **Development Implementation** assign development tasks

### Feature Request Template

```markdown
## Feature Description
Clearly describe the feature you'd like to add

## Problem Background
What problem does this feature solve?

## Proposed Solution
Describe your expected solution

## Alternative Solutions
Describe other solutions you've considered

## Additional Information
- Related screenshots
- Reference cases
- Technical materials
```

## 🌍 Localization

### Supported Languages

- English (en-US)
- Simplified Chinese (zh-CN)
- Planned: Japanese, Korean, French, German

### Translation Process

1. **Clone Repository** get latest code
2. **Add Resource Files** in `Resources/Localization/` directory
3. **Translate Text** maintain formatting and placeholders
4. **Test** ensure UI displays correctly
5. **Submit PR** include translation files

### Resource File Format

```xml
<!-- Resources/Localization/Strings.en-US.resx -->
<data name="CaptureRegion" xml:space="preserve">
  <value>Capture Region</value>
</data>
```

## 🏆 Contributor Guidelines

### Contribution Types

- **Code Contributions**: New features, bug fixes, performance optimization
- **Documentation Contributions**: README, API docs, tutorials
- **Testing Contributions**: Unit tests, integration tests, manual testing
- **Design Contributions**: UI/UX design, icons, animations
- **Translation Contributions**: Multi-language support

### Contributor Benefits

- **Attribution**: Name in contributor list
- **Badges**: GitHub Profile badges
- **Recommendations**: Open source contribution recommendations
- **Technical Exchange**: Participate in technical discussions and decisions

### Community Guidelines

- **Friendly**: Be kind to all community members
- **Inclusive**: Welcome contributors from different backgrounds and experience levels
- **Respectful**: Respect different viewpoints and suggestions
- **Constructive**: Provide constructive feedback and suggestions

## 📞 Contact

- **GitHub Issues**: Technical questions and feature suggestions
- **GitHub Discussions**: Community discussions and exchanges
- **Email**: your-email@example.com

## 📋 Task List

### Current Priorities

#### High Priority
- [ ] Complete macOS platform support
- [ ] Auto-update mechanism
- [ ] Performance optimization
- [ ] Increase unit test coverage

#### Medium Priority
- [ ] More annotation tools (highlight, mosaic)
- [ ] Batch processing functionality
- [ ] Plugin system
- [ ] Cloud synchronization

#### Low Priority
- [ ] Mobile support
- [ ] Browser extension
- [ ] API interface
- [ ] Third-party integrations

## 🎯 Development Guide

### Adding New Features

1. **Create Interface** define service interface
2. **Implement Service** write concrete implementation
3. **Register Service** register in DI container
4. **Write Tests** ensure functionality is correct
5. **Update Documentation** add usage instructions

### Debugging Tips

- **Logging**: Use Serilog to record key information
- **Breakpoint Debugging**: Visual Studio debugger
- **Performance Analysis**: dotTrace performance analysis
- **Memory Analysis**: dotMemory memory analysis

### Platform Adaptation

- **Conditional Compilation**: Use `#if` directives
- **Runtime Detection**: `RuntimeInformation.IsOSPlatform()`
- **Platform Services**: Implement platform-specific service interfaces
- **Resource Adaptation**: Use different resource files for different platforms

---

Thank you again for your contribution! Let's build a better AGI.Captor together! 🚀