# AGI.Captor Tests

This project contains unit tests and integration tests for the AGI.Captor application.

## Test Structure

The test project follows the same directory structure as the main application:

```
tests/AGI.Captor.Tests/
├── Services/                    # Service layer tests
│   ├── Hotkeys/                # Hotkey provider tests
│   └── Overlay/                # Overlay system tests
│       └── Platforms/          # Platform-specific tests
├── Models/                     # Model tests
├── Commands/                   # Command pattern tests
├── Integration/                # Integration tests
├── TestHelpers/                # Test utilities and helpers
└── README.md                   # This file
```

## Test Frameworks and Tools

- **xUnit**: Primary testing framework
- **NSubstitute**: Mocking framework (chosen over Moq for cleaner syntax)
- **FluentAssertions**: Fluent assertion library for readable test assertions
- **SharpHook**: For simulating keyboard and mouse events in integration tests
- **Serilog**: For test logging and debugging

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "ClassName=AnnotationServiceTests"

# Run tests in parallel
dotnet test --parallel
```

### Visual Studio

1. Open Test Explorer (Test > Test Explorer)
2. Build the solution
3. Run all tests or specific test methods

### JetBrains Rider

1. Open the Unit Tests window
2. Build the solution
3. Run tests using the green play buttons

## Test Categories

### Unit Tests
- **Services**: Test individual service classes with mocked dependencies
- **Models**: Test data models and their behavior
- **Commands**: Test command pattern implementation

### Integration Tests
- **Hotkey Integration**: Test hotkey functionality with SharpHook simulation
- **Service Integration**: Test service interactions with real dependencies

## Test Helpers

### TestBase
Base class for all test classes providing:
- Common setup and teardown
- Logging configuration
- Service provider setup

### SharpHookTestHelper
Helper class for simulating keyboard and mouse events:
- Key press/release simulation
- Mouse movement and clicks
- Mouse wheel simulation
- Event timing control

### TestConfiguration
Configuration helper for test environment setup:
- Service provider creation
- Logger configuration
- Mock service setup

## Best Practices

### Test Naming
- Use descriptive test method names
- Follow the pattern: `MethodName_Scenario_ExpectedResult`
- Example: `RegisterHotkey_WithValidParameters_ShouldReturnTrue`

### Test Organization
- One test class per production class
- Group related tests using `[Fact]` and `[Theory]`
- Use `[Theory]` with `[InlineData]` for parameterized tests

### Assertions
- Use FluentAssertions for readable assertions
- Prefer specific assertions over generic ones
- Example: `result.Should().BeTrue()` instead of `Assert.True(result)`

### Mocking
- Use NSubstitute for creating mocks
- Mock external dependencies, not the class under test
- Verify mock interactions when necessary

### Test Data
- Use meaningful test data
- Avoid magic numbers and strings
- Create test data builders when needed

## Coverage Goals

- **Unit Tests**: 80%+ code coverage
- **Integration Tests**: Cover critical user workflows
- **Edge Cases**: Test boundary conditions and error scenarios

## Continuous Integration

Tests are automatically run on:
- Pull request creation
- Code push to main branch
- Scheduled nightly builds

## Debugging Tests

### Logging
Tests use Serilog for logging. Check test output for detailed logs.

### Breakpoints
Set breakpoints in test methods or production code to debug issues.

### Test Output
Use `ITestOutputHelper` to write custom output during tests:

```csharp
public class MyTest : TestBase
{
    public MyTest(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void MyTestMethod()
    {
        Output.WriteLine("Debug information");
        // Test logic here
    }
}
```

## Troubleshooting

### Common Issues

1. **Tests not running**: Ensure all NuGet packages are restored
2. **SharpHook issues**: Check platform-specific dependencies
3. **Mock failures**: Verify mock setup and expectations
4. **Timing issues**: Use `WaitForEventsAsync()` for async operations

### Performance

- Tests should run quickly (< 1 second per test)
- Use `[Fact]` for fast tests, `[Fact(Skip = "Reason")]` for slow tests
- Consider parallel execution for independent tests

## Contributing

When adding new tests:
1. Follow the existing naming conventions
2. Add appropriate test categories
3. Update this README if needed
4. Ensure tests pass in CI/CD pipeline
