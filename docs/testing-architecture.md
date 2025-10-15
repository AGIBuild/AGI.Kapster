# Testing Architecture

## Overview

AGI.Kapster implements a comprehensive testing strategy using dependency injection, abstraction patterns, and mocking to ensure reliable, isolated unit tests. The architecture supports both unit and integration testing with proper isolation from external dependencies.

## Test Organization

### Directory Structure
```
tests/AGI.Kapster.Tests/
├── TestHelpers/
│   ├── TestBase.cs                    # Base class for all tests
│   ├── MemoryFileSystemService.cs     # In-memory file system for testing
│   └── AvaloniaTestHelper.cs          # Avalonia initialization for tests
├── Services/
│   ├── SettingsServiceTests.cs        # Settings service with file system mocking
│   ├── AnnotationServiceTests.cs      # Annotation service tests
│   ├── HotkeyManagerTests.cs          # Hotkey management tests
│   └── Clipboard/
│       └── ClipboardServiceTests.cs   # Clipboard service tests
├── Models/
│   └── AnnotationTests.cs             # Annotation model tests
├── Commands/
│   └── CommandTests.cs                # Command pattern tests
└── Integration/
    └── OverlayIntegrationTests.cs     # End-to-end overlay tests
```

## Testing Patterns

### 1. Dependency Injection for Testing

All services use interfaces to enable easy mocking and testing:

```csharp
// Production implementation
public class SettingsService : ISettingsService
{
    private readonly IFileSystemService _fileSystemService;
    
    public SettingsService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }
}

// Test setup
public class SettingsServiceTests : TestBase
{
    private readonly IFileSystemService _fileSystemService;
    private readonly SettingsService _settingsService;

    public SettingsServiceTests()
    {
        _fileSystemService = new MemoryFileSystemService();
        _settingsService = new SettingsService(_fileSystemService);
    }
}
```

### 2. File System Abstraction

File system operations are abstracted for testing:

```csharp
public interface IFileSystemService
{
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string content);
    bool FileExists(string path);
    string GetDirectoryName(string path);
    void CreateDirectory(string path);
}

// Test implementation
public class MemoryFileSystemService : IFileSystemService
{
    private readonly Dictionary<string, string> _files = new();
    private readonly HashSet<string> _directories = new();
    
    public Task<string> ReadAllTextAsync(string path)
    {
        return Task.FromResult(_files.TryGetValue(path, out var content) ? content : throw new FileNotFoundException());
    }
    
    public Task WriteAllTextAsync(string path, string content)
    {
        _files[path] = content;
        return Task.CompletedTask;
    }
}
```

### 3. Platform Service Mocking

Platform-specific services are mocked using NSubstitute:

```csharp
[Test]
public async Task CaptureScreen_ShouldReturnBitmap()
{
    // Arrange
    var mockCaptureStrategy = Substitute.For<IScreenCaptureStrategy>();
    var expectedBitmap = new SKBitmap(100, 100);
    mockCaptureStrategy.CaptureFullScreenAsync(Arg.Any<Screen>())
                      .Returns(expectedBitmap);

    var captureService = new ScreenCaptureService(mockCaptureStrategy);

    // Act
    var result = await captureService.CaptureScreenAsync();

    // Assert
    Assert.That(result, Is.Not.Null);
    await mockCaptureStrategy.Received(1).CaptureFullScreenAsync(Arg.Any<Screen>());
}
```

## Test Categories

### Unit Tests
- **Service Tests**: Individual service logic validation
- **Model Tests**: Data model behavior and validation
- **Command Tests**: Command pattern implementation
- **Strategy Tests**: Platform strategy implementations

### Integration Tests
- **Overlay System**: End-to-end overlay window management
- **Clipboard Integration**: Cross-platform clipboard operations
- **Settings Persistence**: File system integration testing
- **Service Composition**: Dependency injection container validation

### Platform Tests
- **Windows-Specific**: UI Automation and Win32 API tests
- **macOS-Specific**: Native API integration tests
- **Cross-Platform**: Shared functionality validation

## Test Base Classes

### TestBase
```csharp
public abstract class TestBase
{
    protected ServiceCollection Services { get; private set; }
    protected ServiceProvider ServiceProvider { get; private set; }

    [SetUp]
    public virtual void SetUp()
    {
        Services = new ServiceCollection();
        ConfigureServices(Services);
        ServiceProvider = Services.BuildServiceProvider();
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Register test-specific services
        services.AddSingleton<IFileSystemService, MemoryFileSystemService>();
    }

    [TearDown]
    public virtual void TearDown()
    {
        ServiceProvider?.Dispose();
    }
}
```

### AvaloniaTestBase
```csharp
public abstract class AvaloniaTestBase : TestBase
{
    [OneTimeSetUp]
    public void InitializeAvalonia()
    {
        if (Application.Current == null)
        {
            AppBuilder.Configure<TestApplication>()
                     .UsePlatformDetect()
                     .SetupWithoutStarting();
        }
    }
}
```

## Coverage and Reporting

### Test Coverage Targets
- **Overall Coverage**: 85%+ (target: 90%+)
- **Service Classes**: 95%+ coverage required
- **Critical Paths**: 100% coverage for capture and clipboard operations
- **Platform Abstractions**: Full interface coverage

### Coverage Collection
```bash
# Collect coverage during test run
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report
```

### CI Integration
```yaml
- name: Run Tests with Coverage
  run: dotnet test --collect:"XPlat Code Coverage" --logger trx --results-directory TestResults

- name: Generate Coverage Report
  uses: danielpalme/ReportGenerator-GitHub-Action@5.1.10
  with:
    reports: '**/coverage.cobertura.xml'
    targetdir: 'coverage-report'
```

## Mocking Strategy

### Service Mocking with NSubstitute
```csharp
// Mock external dependencies
var mockClipboard = Substitute.For<IClipboardStrategy>();
var mockFileSystem = Substitute.For<IFileSystemService>();

// Setup mock behavior
mockClipboard.SetImageAsync(Arg.Any<SKBitmap>()).Returns(Task.CompletedTask);
mockFileSystem.FileExists(Arg.Any<string>()).Returns(true);

// Verify interactions
await mockClipboard.Received(1).SetImageAsync(Arg.Any<SKBitmap>());
```

### Event Testing
```csharp
[Test]
public void OverlayManager_ShouldRaiseRegionSelectedEvent()
{
    // Arrange
    var overlayManager = new SimplifiedOverlayManager();
    var eventRaised = false;
    CaptureRegionEventArgs? eventArgs = null;

    // Example only; current manager handles events internally and closes overlays

    // Act
    overlayManager.TriggerRegionSelected(new PixelRect(0, 0, 100, 100));

    // Assert
    Assert.That(eventRaised, Is.True);
    Assert.That(eventArgs?.Region.Width, Is.EqualTo(100));
}
```

## Test Data Management

### In-Memory Test Data
```csharp
public class TestDataBuilder
{
    public static SettingsModel CreateDefaultSettings()
    {
        return new SettingsModel
        {
            HotKey = "Ctrl+Shift+S",
            SavePath = "/test/path",
            AutoSave = true
        };
    }

    public static SKBitmap CreateTestBitmap(int width = 100, int height = 100)
    {
        var bitmap = new SKBitmap(width, height);
        // Fill with test pattern
        return bitmap;
    }
}
```

### Test Configuration
```json
{
  "TestSettings": {
    "MockFileSystem": true,
    "DisableUI": true,
    "TestDataPath": "./TestData",
    "LogLevel": "Warning"
  }
}
```

## Performance Testing

### Memory Leak Detection
```csharp
[Test]
public void OverlayWindow_ShouldNotLeakMemory()
{
    var initialMemory = GC.GetTotalMemory(true);
    
    // Create and dispose multiple overlay windows
    for (int i = 0; i < 100; i++)
    {
        using var overlay = ServiceProvider.GetRequiredService<IOverlayWindow>();
        overlay.Show();
        overlay.Close();
    }
    
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    
    var finalMemory = GC.GetTotalMemory(false);
    var memoryIncrease = finalMemory - initialMemory;
    
    Assert.That(memoryIncrease, Is.LessThan(1024 * 1024)); // Less than 1MB increase
}
```

### Performance Benchmarks
```csharp
[Test]
public async Task CaptureScreen_ShouldCompleteWithinTimeLimit()
{
    var stopwatch = Stopwatch.StartNew();
    
    await _captureService.CaptureScreenAsync();
    
    stopwatch.Stop();
    Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(100)); // Under 100ms
}
```

## Continuous Integration

### Test Execution Pipeline
```yaml
- name: Run Unit Tests
  run: dotnet test tests/AGI.Kapster.Tests --logger trx

- name: Run Integration Tests
  run: dotnet test tests/AGI.Kapster.IntegrationTests --logger trx

- name: Publish Test Results
  uses: dorny/test-reporter@v1
  with:
    name: Test Results
    path: '**/*.trx'
    reporter: dotnet-trx
```

### Quality Gates
- All tests must pass before merge
- Code coverage must meet minimum thresholds
- No performance regressions allowed
- Security scans must pass

## Best Practices

### Test Writing Guidelines
1. **Arrange-Act-Assert**: Clear test structure
2. **Single Responsibility**: One concept per test
3. **Descriptive Names**: Test intention clear from name
4. **Independent Tests**: No test dependencies
5. **Fast Execution**: Unit tests under 1 second

### Mock Usage Guidelines
1. **Mock Boundaries**: Mock external dependencies only
2. **Verify Behavior**: Check interactions, not just state
3. **Realistic Mocks**: Behavior matches real implementations
4. **Minimal Mocking**: Only mock what's necessary

### Maintenance Practices
1. **Regular Cleanup**: Remove obsolete tests
2. **Coverage Monitoring**: Track coverage trends
3. **Performance Tracking**: Monitor test execution time
4. **Documentation**: Keep test documentation current

## Troubleshooting

### Common Test Issues
```bash
# Clear test cache
dotnet clean
dotnet restore

# Run specific test
dotnet test --filter "TestClassName.TestMethodName"

# Debug test execution
dotnet test --logger "console;verbosity=detailed"
```

### Platform-Specific Issues
- **Windows**: Ensure UI Automation dependencies available
- **macOS**: Handle permission dialogs in CI
- **Linux**: Verify X11/Wayland display environment

### CI/CD Integration Issues
- Check runner OS compatibility
- Verify .NET SDK version consistency
- Ensure test data files are included
- Monitor resource usage during parallel execution