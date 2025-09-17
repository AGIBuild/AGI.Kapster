# Testing Architecture

## Overview

AGI.Captor uses a comprehensive testing strategy with dependency injection and abstraction patterns to ensure reliable, isolated unit tests. The testing architecture supports both unit testing and integration testing with proper isolation from external dependencies.

## Test Organization

### Directory Structure
```
tests/AGI.Captor.Tests/
├── TestHelpers/
│   ├── TestBase.cs                    # Base class for all tests
│   ├── MemoryFileSystemService.cs     # In-memory file system for testing
│   └── AvaloniaTestHelper.cs          # Avalonia initialization for tests
├── Services/
│   ├── SettingsServiceTests.cs        # Settings service with file system mocking
│   ├── AnnotationServiceTests.cs      # Annotation service tests
│   ├── HotkeyManagerTests.cs          # Hotkey management tests
│   └── Hotkeys/
│       └── HotkeyProviderTests.cs     # Hotkey provider interface tests
├── Models/
│   └── AnnotationTests.cs             # Annotation model tests
├── Commands/
│   └── CommandTests.cs                # Command pattern tests
└── Services/Overlay/
    └── OverlayManagerTests.cs         # Overlay management tests
```

## Key Testing Patterns

### 1. Dependency Injection for Testing

All services use interfaces to enable easy mocking and testing:

```csharp
// Production code
public class SettingsService : ISettingsService
{
    private readonly IFileSystemService _fileSystemService;
    
    public SettingsService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }
}

// Test code
public class SettingsServiceTests : TestBase
{
    private readonly MemoryFileSystemService _fileSystemService;
    private readonly SettingsService _settingsService;

    public SettingsServiceTests(ITestOutputHelper output) : base(output)
    {
        _fileSystemService = new MemoryFileSystemService();
        _settingsService = new SettingsService(_fileSystemService);
    }
}
```

### 2. File System Abstraction

The `IFileSystemService` interface enables testing without real file system dependencies:

```csharp
public interface IFileSystemService
{
    bool FileExists(string path);
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string content);
    string ReadAllText(string path);
    void EnsureDirectoryExists(string path);
    string GetApplicationDataPath();
}
```

#### Memory Implementation for Testing
```csharp
public class MemoryFileSystemService : IFileSystemService
{
    private readonly ConcurrentDictionary<string, string> _files = new();
    private readonly HashSet<string> _directories = new();
    
    // In-memory implementation of all file operations
    // Provides isolated test environment
}
```

### 3. Test Base Class

All tests inherit from `TestBase` which provides common setup:

```csharp
public abstract class TestBase : IDisposable
{
    protected readonly ITestOutputHelper Output;
    protected readonly ILoggerFactory LoggerFactory;

    protected TestBase(ITestOutputHelper output)
    {
        Output = output;
        LoggerFactory = new TestLoggerFactory(output);
    }

    protected ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

    public virtual void Dispose()
    {
        LoggerFactory?.Dispose();
    }
}
```

### 4. Mocking with NSubstitute

Services are mocked using NSubstitute for isolated testing:

```csharp
[Fact]
public void HotkeyManager_RegisterEscapeHotkey_ShouldCallProviderRegister()
{
    // Arrange
    var hotkeyProvider = Substitute.For<IHotkeyProvider>();
    var settingsService = Substitute.For<ISettingsService>();
    var overlayController = Substitute.For<IOverlayController>();
    
    hotkeyProvider.IsSupported.Returns(true);
    hotkeyProvider.HasPermissions.Returns(true);
    hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Any<Action>())
        .Returns(true);

    var hotkeyManager = new HotkeyManager(hotkeyProvider, settingsService, overlayController);

    // Act
    hotkeyManager.RegisterEscapeHotkey();

    // Assert
    hotkeyProvider.Received(1).RegisterHotkey(
        "overlay_escape",
        HotkeyModifiers.Control,
        (uint)Avalonia.Input.Key.Escape,
        Arg.Any<Action>()
    );
}
```

## Test Categories

### 1. Unit Tests
- **Purpose**: Test individual components in isolation
- **Dependencies**: All external dependencies mocked
- **Examples**: Service methods, model validation, command execution

### 2. Integration Tests
- **Purpose**: Test component interactions
- **Dependencies**: Real implementations where appropriate
- **Examples**: Service-to-service communication, event handling

### 3. Model Tests
- **Purpose**: Test data models and business logic
- **Dependencies**: Minimal, focused on model behavior
- **Examples**: Annotation creation, validation, state changes

### 4. Command Tests
- **Purpose**: Test undo/redo functionality
- **Dependencies**: Mocked services, real command objects
- **Examples**: Command execution, history management

## Test Configuration

### xUnit Configuration
```json
{
  "methodDisplay": "method",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 1,
  "longRunningTestSeconds": 10,
  "diagnosticMessages": false,
  "internalDiagnosticMessages": false
}
```

### Test Timeout Strategy
- **Global Default**: 10 seconds (configured in `xunit.runner.json`)
- **Synchronous Tests**: Rely on global default timeout
- **Asynchronous Tests**: Can specify custom timeout using `[Fact(Timeout = xxx)]`

### Example Test with Timeout
```csharp
[Fact(Timeout = 30000)] // 30 seconds for integration test
public async Task Settings_ShouldLoadAndSaveCorrectly()
{
    // Test implementation
}
```

## Test Data Management

### 1. In-Memory File System
- **Isolation**: Each test gets fresh file system state
- **Cleanup**: Automatic cleanup between tests
- **Verification**: Can inspect file contents for assertions

### 2. Mock Data
- **Settings**: Default settings objects for testing
- **Annotations**: Sample annotation objects
- **Events**: Mock event arguments

### 3. Test Helpers
- **AvaloniaTestHelper**: Initialize Avalonia for UI tests
- **TestBase**: Common setup and teardown
- **MemoryFileSystemService**: Isolated file operations

## Running Tests

### Command Line
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "ClassName=SettingsServiceTests"

# Run with build script
.\build.ps1 -Test
```

### Build Script Integration
The build scripts support comprehensive test execution:

```powershell
# Run tests with coverage
.\build.ps1 -Test -Coverage

# Run tests with specific filter
.\build.ps1 -Test -TestFilter "SettingsService"

# Run tests and open coverage report
.\build.ps1 -Test -Coverage -OpenReport
```

## Test Coverage

### Current Status
- **Total Tests**: 95 tests
- **Coverage Areas**: Services, Models, Commands, Overlay Management
- **Test Types**: Unit tests, Integration tests, Model validation tests

### Coverage Goals
- **Services**: 100% of public methods
- **Models**: All business logic paths
- **Commands**: All execution and undo scenarios
- **Critical Paths**: All user interaction flows

## Best Practices

### 1. Test Isolation
- Each test should be independent
- Use fresh instances for each test
- Clean up resources in Dispose methods

### 2. Descriptive Test Names
```csharp
[Fact]
public void SettingsService_LoadSettings_WhenFileExists_ShouldDeserializeCorrectly()
{
    // Test implementation
}
```

### 3. Arrange-Act-Assert Pattern
```csharp
[Fact]
public void AnnotationService_StartAnnotation_WithRectangleTool_ShouldCreateRectangleAnnotation()
{
    // Arrange
    _annotationService.CurrentTool = AnnotationToolType.Rectangle;
    var startPoint = new Avalonia.Point(10, 20);

    // Act
    var annotation = _annotationService.StartAnnotation(startPoint);

    // Assert
    annotation.Should().NotBeNull();
    annotation!.Should().BeOfType<RectangleAnnotation>();
    annotation!.Type.Should().Be(AnnotationType.Rectangle);
}
```

### 4. Use FluentAssertions
```csharp
// Good
annotation.Should().NotBeNull();
annotation!.Type.Should().Be(AnnotationType.Rectangle);

// Avoid
Assert.NotNull(annotation);
Assert.Equal(AnnotationType.Rectangle, annotation.Type);
```

### 5. Mock External Dependencies
- File system operations
- Network calls
- Platform-specific APIs
- UI framework dependencies

## Troubleshooting

### Common Issues

#### 1. Test Timeouts
- **Cause**: Tests waiting for external resources
- **Solution**: Use appropriate timeouts and mock external dependencies

#### 2. File System Dependencies
- **Cause**: Tests writing to real file system
- **Solution**: Use `MemoryFileSystemService` for all file operations

#### 3. Avalonia Initialization
- **Cause**: UI tests without proper Avalonia setup
- **Solution**: Use `AvaloniaTestHelper.Initialize()` before UI tests

#### 4. Test Isolation Failures
- **Cause**: Tests sharing state
- **Solution**: Ensure each test creates fresh instances

### Debug Tips
- Use `ITestOutputHelper` for test output
- Enable diagnostic messages in xUnit configuration
- Use breakpoints in test methods
- Check test logs for detailed information

## Future Improvements

### 1. Integration Test Suite
- End-to-end user workflows
- Multi-platform testing
- Performance benchmarks

### 2. UI Testing
- Automated UI interaction tests
- Visual regression testing
- Accessibility testing

### 3. Test Data Builders
- Fluent API for creating test objects
- Reusable test data patterns
- Complex scenario setup

### 4. Continuous Integration
- Automated test execution
- Coverage reporting
- Test result notifications

---

*Last Updated: January 2025*
*Test Count: 95 tests passing*
