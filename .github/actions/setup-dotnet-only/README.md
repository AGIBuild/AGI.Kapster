# Setup .NET Only Action

A lightweight composite GitHub Action for setting up .NET environment with NuGet caching, without repository checkout.

## Features

- **Minimal Setup**: Only installs .NET and configures NuGet cache
- **Flexible Versioning**: Supports .NET 9.x with optional quality channels
- **Optimized Caching**: NuGet package caching for faster builds
- **No Checkout**: Assumes repository is already checked out

## Usage

```yaml
- name: Setup .NET Only
  uses: ./.github/actions/setup-dotnet-only
  with:
    dotnet-version: '9.0.x'      # Optional: .NET version (default: 9.0.x)
    dotnet-quality: 'preview'    # Optional: Quality channel (default: preview)
    enable-nuget-cache: 'true'   # Optional: Enable caching (default: true)
```

## Inputs

| Input | Description | Required | Default |
|-------|-------------|----------|---------|
| `dotnet-version` | .NET version to install | No | `9.0.x` |
| `dotnet-quality` | .NET quality channel (ga, preview) | No | `preview` |
| `include-dotnet8` | Whether to include .NET 8.x | No | `true` |
| `enable-nuget-cache` | Enable NuGet package caching | No | `true` |
| `cache-key-suffix` | Additional suffix for cache key | No | _(empty)_ |

## Examples

### Basic Setup
```yaml
- uses: ./.github/actions/setup-dotnet-only
```

### Specific Version
```yaml
- uses: ./.github/actions/setup-dotnet-only
  with:
    dotnet-version: '9.0.x'
    dotnet-quality: 'ga'
```

### Without Caching
```yaml
- uses: ./.github/actions/setup-dotnet-only
  with:
    enable-nuget-cache: 'false'
```

### Custom Cache Key
```yaml
- uses: ./.github/actions/setup-dotnet-only
  with:
    cache-key-suffix: 'special-build'
```

## What It Does

This action performs the following steps:

1. **Install .NET**: Sets up .NET 9.x (and optionally 8.x)
2. **Configure NuGet Cache**: Enables package caching for faster builds
3. **Validate Installation**: Ensures .NET is properly configured

## Use Cases

Perfect for scenarios where you need .NET setup without full environment configuration:

- **Simple Builds**: When you only need .NET compilation
- **Lightweight Jobs**: For jobs that don't need git or complex setup
- **Parallel Workflows**: When checkout is handled separately
- **Testing Only**: For unit test runs without publishing

## Comparison with setup-environment

| Feature | setup-dotnet-only | setup-environment |
|---------|-------------------|-------------------|
| Repository checkout | ❌ | ✅ |
| .NET installation | ✅ | ✅ |
| NuGet caching | ✅ | ✅ |
| Git configuration | ❌ | ✅ |
| Environment variables | ❌ | ✅ |
| Use case | Lightweight builds | Full CI/CD |

## Prerequisites

This action requires:
- Repository must already be checked out
- Runner must have PowerShell (Windows) or bash (Linux/macOS)

```yaml
steps:
- name: 📥 Checkout repository
  uses: actions/checkout@v4
  
- name: 🔧 Setup .NET Only
  uses: ./.github/actions/setup-dotnet-only
```

## Cache Behavior

When `enable-nuget-cache` is `true`, this action:
- Creates cache key based on lock files and OS
- Restores NuGet packages from cache
- Saves cache after package operations
- Uses optional suffix for cache isolation

Cache key format: `nuget-{os}-{hash-of-lock-files}-{suffix}`