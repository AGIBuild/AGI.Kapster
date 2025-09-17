# Testing Guide

## Overview

This project uses .NET's built-in testing framework with xUnit and includes code coverage collection using the built-in XPlat Code Coverage collector.

## Running Tests

### Basic Test Execution

```powershell
# Run all tests
.\build.ps1 -Test

# Run tests with code coverage
.\build.ps1 -Test -Coverage

# Run tests with coverage and open report
.\build.ps1 -Test -Coverage -OpenReport

# Run specific test filter
.\build.ps1 -Test -TestFilter "FullyQualifiedName~HotkeyManagerTests"
```

### Direct dotnet Commands

```powershell
# Run tests without coverage
dotnet test tests/AGI.Captor.Tests

# Run tests with coverage
dotnet test tests/AGI.Captor.Tests --collect:"XPlat Code Coverage" --results-directory TestResults

# Run specific tests
dotnet test tests/AGI.Captor.Tests --filter "FullyQualifiedName~AnnotationServiceTests"
```

## Code Coverage

### Built-in Coverage Collection

The project uses .NET's built-in XPlat Code Coverage collector, which generates:
- **Cobertura XML format**: `coverage.cobertura.xml`
- **Line coverage**: Percentage of lines executed
- **Branch coverage**: Percentage of branches executed

### Coverage Reports

When using `-Coverage` flag, the build script automatically:
1. Collects coverage data during test execution
2. Generates a summary with line and branch coverage percentages
3. Saves detailed coverage data in `TestResults/` directory
4. Optionally opens the coverage file if `-OpenReport` is specified

### Coverage Data Location

Coverage files are saved in:
```
TestResults/
└── [guid]/
    └── coverage.cobertura.xml
```

The XML file contains detailed coverage information that can be:
- Parsed by CI/CD systems
- Imported into coverage analysis tools
- Used for coverage thresholds and quality gates

## Test Structure

### Test Categories

- **Unit Tests**: Test individual components in isolation
- **Integration Tests**: Test component interactions (currently minimal)
- **Service Tests**: Test business logic services
- **Model Tests**: Test data models and validation

### Test Organization

```
tests/AGI.Captor.Tests/
├── Commands/           # Command pattern tests
├── Models/            # Data model tests
├── Services/          # Service layer tests
│   ├── Hotkeys/       # Hotkey system tests
│   └── Overlay/       # Overlay system tests
└── TestHelpers/       # Test utilities and base classes
```

## Test Configuration

### Timeout Settings

Global test timeout is configured in `xunit.runner.json`:
- Default timeout: 10 seconds
- Long-running tests: Can specify custom timeout with `[Fact(Timeout = xxx)]`

### Test Base Class

All tests inherit from `TestBase` which provides:
- Custom logging to test output
- Common setup and teardown
- Test utilities

## Best Practices

### Writing Tests

1. **Use descriptive test names**: `Should_ExpectedBehavior_When_StateUnderTest`
2. **Follow AAA pattern**: Arrange, Act, Assert
3. **Test one thing per test**: Each test should verify a single behavior
4. **Use meaningful assertions**: Prefer FluentAssertions for readable test code

### Coverage Guidelines

1. **Aim for high coverage**: Target >80% line coverage for critical paths
2. **Focus on business logic**: Prioritize coverage of core functionality
3. **Test edge cases**: Include boundary conditions and error scenarios
4. **Mock external dependencies**: Use NSubstitute for isolated unit tests

## Continuous Integration

### Coverage Thresholds

Consider setting minimum coverage thresholds:
- Line coverage: >80%
- Branch coverage: >70%

### CI Integration

The coverage XML can be integrated with CI systems:
- **GitHub Actions**: Use coverage reporting actions
- **Azure DevOps**: Use built-in coverage reporting
- **Jenkins**: Use Cobertura plugin

## Troubleshooting

### Common Issues

1. **Tests hanging**: Check for blocking operations in test setup
2. **Low coverage**: Ensure tests cover all code paths
3. **Coverage not generated**: Verify `coverlet.collector` package is installed

### Debug Mode

Run tests with detailed logging:
```powershell
dotnet test tests/AGI.Captor.Tests --logger "console;verbosity=detailed"
```
