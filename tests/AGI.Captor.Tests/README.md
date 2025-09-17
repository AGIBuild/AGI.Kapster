# AGI.Captor Tests

## Test Timeout Configuration

This test project uses a simple timeout strategy to prevent tests from hanging indefinitely:

### Timeout Strategy
- **Default**: 10 seconds (configured in `xunit.runner.json`)
- **Async Tests**: Can specify custom timeout using `[Fact(Timeout = xxx)]`
- **Sync Tests**: Use global timeout from `xunit.runner.json`

### Usage
```csharp
// Sync tests - use global timeout (10 seconds)
[Fact]
public void MySyncTest() { }

// Async tests - can specify custom timeout
[Fact(Timeout = 30000)] // 30 seconds
public async Task MyAsyncIntegrationTest() { }

[Fact(Timeout = 15000)] // 15 seconds
public async Task MyAsyncFileIOTest() { }
```

### Test Categories
- **Unit Tests**: Use global timeout (10 seconds)
- **File I/O Tests**: Use global timeout (10 seconds)
- **Integration Tests**: Custom timeout for async tests (30 seconds)
- **Constructor Tests**: Use global timeout (10 seconds)

### Global Configuration
The `xunit.runner.json` file sets a global timeout of 10 seconds for all tests. Only async tests can override this with custom timeouts.