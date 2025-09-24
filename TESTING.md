# Testing Guide

## Overview

This project uses .NET's built-in testing framework with xUnit and includes code coverage collection using the built-in XPlat Code Coverage collector.

## Running Tests

### Quick Start

```powershell
# Run all tests
./build.ps1 Test

# Run tests with code coverage
./build.ps1 Test -Coverage

# Run tests with coverage and open report
./build.ps1 Test -Coverage -OpenReport
```

### Direct dotnet Commands

```powershell
# Run tests without coverage
dotnet test tests/AGI.Captor.Tests

# Run tests with coverage
dotnet test tests/AGI.Captor.Tests --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test tests/AGI.Captor.Tests --filter "FullyQualifiedName~HotkeyManagerTests"
```

## Test Structure

### Test Categories

- **Unit Tests**: Individual component testing
- **Integration Tests**: Component interaction testing
- **Service Tests**: Business logic testing
- **Command Tests**: Undo/redo functionality testing

### Test Files

```
tests/AGI.Captor.Tests/
├── Commands/
│   └── CommandTests.cs          # Undo/redo command tests
├── Models/
│   ├── AnnotationTests.cs       # Annotation model tests
│   └── SettingsTests.cs         # Settings model tests
├── Services/
│   ├── HotkeyServiceTests.cs    # Hotkey service tests
│   ├── SettingsServiceTests.cs   # Settings service tests
│   ├── ExportServiceTests.cs     # Export service tests
│   ├── AnnotationServiceTests.cs # Annotation service tests
│   └── OverlayControllerTests.cs # Overlay controller tests
└── TestHelpers/
    ├── MockHotkeyProvider.cs     # Mock hotkey provider
    ├── MockSettingsService.cs     # Mock settings service
    ├── MockOverlayController.cs   # Mock overlay controller
    ├── TestDataBuilder.cs        # Test data builder
    ├── UIThreadHelper.cs         # UI thread helper
    └── AvaloniaTestBase.cs       # Avalonia test base class
```

## Coverage Reports

### Generating Coverage

```powershell
# Generate coverage report
./build.ps1 Test -Coverage

# Coverage files are generated in:
# - artifacts/coverage/coverage.cobertura.xml
# - artifacts/coverage/coverage.opencover.xml
```

### Coverage Targets

- **Minimum Coverage**: 80%
- **Critical Components**: 90%+ (Services, Commands)
- **UI Components**: 70%+ (ViewModels, Views)

## Test Data

### Test Data Builder

The `TestDataBuilder` class provides convenient methods for creating test data:

```csharp
// Create test annotation
var annotation = TestDataBuilder.CreateAnnotation()
    .WithType(AnnotationType.Arrow)
    .WithColor(Color.Red)
    .Build();

// Create test settings
var settings = TestDataBuilder.CreateSettings()
    .WithHotkey("Alt+A")
    .WithDefaultFormat(ImageFormat.Png)
    .Build();
```

### Mock Services

Mock services are provided for testing:

- `MockHotkeyProvider`: Mock hotkey registration
- `MockSettingsService`: Mock settings persistence
- `MockOverlayController`: Mock overlay management

## Continuous Integration

### GitHub Actions

Tests are automatically run on:
- **Push to main**: Full test suite
- **Pull Requests**: Full test suite with coverage
- **Release**: Full test suite with coverage report

### Test Results

Test results are available in:
- GitHub Actions logs
- Artifacts: `test-results-and-coverage`
- Coverage reports: `coverage.cobertura.xml`

## Troubleshooting

### Common Issues

1. **UI Tests Failing**: Ensure Avalonia test base is used
2. **Hotkey Tests**: Use mock hotkey provider
3. **Settings Tests**: Use mock settings service
4. **Coverage Not Generated**: Check XPlat Code Coverage collector

### Debug Mode

```powershell
# Run tests in debug mode
dotnet test tests/AGI.Captor.Tests --configuration Debug

# Run with verbose output
dotnet test tests/AGI.Captor.Tests --logger "console;verbosity=detailed"
```

## Best Practices

### Test Writing

1. **Arrange-Act-Assert**: Follow AAA pattern
2. **Descriptive Names**: Use clear test method names
3. **Single Responsibility**: One assertion per test
4. **Mock Dependencies**: Use mock services for isolation

### Test Organization

1. **Group Related Tests**: Use test classes for related functionality
2. **Use Test Categories**: Organize tests by type
3. **Shared Setup**: Use constructor or setup methods
4. **Cleanup**: Dispose resources properly

---

For more detailed testing information, see the individual test files and the project's testing architecture documentation.