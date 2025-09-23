# Build and Test Action

A composite GitHub Action for building and testing .NET projects with coverage support.

## Features

- **Project Build**: Builds .NET projects with configurable settings
- **Test Execution**: Runs unit tests with optional coverage collection
- **Artifact Verification**: Validates build output and test results
- **Flexible Configuration**: Supports various build configurations and test filters

## Usage

```yaml
- name: Build and Test
  uses: ./.github/actions/build-and-test
  with:
    configuration: 'Release'    # Optional: Build configuration (default: Release)
    enable-coverage: 'true'     # Optional: Enable coverage (default: true)
    skip-tests: 'false'         # Optional: Skip tests (default: false)
```

## Inputs

| Input | Description | Required | Default |
|-------|-------------|----------|---------|
| `configuration` | Build configuration (Debug, Release) | No | `Release` |
| `enable-coverage` | Enable test coverage collection | No | `true` |
| `skip-tests` | Skip running tests | No | `false` |
| `cache-key-suffix` | Additional suffix for cache key | No | `''` |

## Outputs

| Output | Description |
|--------|-------------|
| `build-success` | Whether build was successful |
| `test-success` | Whether tests were successful |

## Examples

### Basic Build and Test
```yaml
- uses: ./.github/actions/build-and-test
```

### Debug Build without Coverage
```yaml
- uses: ./.github/actions/build-and-test
  with:
    configuration: 'Debug'
    enable-coverage: 'false'
```

### Build Only (Skip Tests)
```yaml
- uses: ./.github/actions/build-and-test
  with:
    skip-tests: 'true'
```

## What It Does

This action performs the following steps:

1. **NuGet Cache Setup**: Configures package caching for faster builds
2. **Dependency Restoration**: Restores NuGet packages
3. **Project Build**: Compiles the project with specified configuration
4. **Test Execution**: Runs tests with optional coverage collection
5. **Artifact Verification**: Checks for generated test results and coverage files

## Artifacts Generated

When tests run successfully, this action generates:

- `artifacts/test-results/` - Test result files (.trx, .xml)
- `artifacts/coverage/` - Coverage reports (cobertura.xml, etc.)

## Prerequisites

This action requires:
- Repository checkout
- .NET environment setup (use `setup-environment` action)

```yaml
steps:
- name: 📥 Checkout repository
  uses: actions/checkout@v4
  
- name: 🛠️ Setup Environment
  uses: ./.github/actions/setup-environment
  
- name: 🔨 Build and Test
  uses: ./.github/actions/build-and-test
```