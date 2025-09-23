# Setup Complete Environment Action

A composite GitHub Action that provides complete environment setup for .NET projects.

## Features

- **.NET 9.0 Setup**: Installs .NET 9.0.x with configurable quality channel
- **NuGet Caching**: Automatic package cache management for faster builds
- **Git Configuration**: Safe directory configuration for CI/CD environments
- **Environment Variables**: Sets common .NET CLI optimization flags

## Usage

```yaml
- name: Setup Complete Environment
  uses: ./.github/actions/setup-environment
  with:
    dotnet-version: '9.0.x'     # Optional: .NET version (default: 9.0.x)
    dotnet-quality: 'preview'   # Optional: Quality channel (default: preview)
    enable-nuget-cache: 'true'  # Optional: Enable caching (default: true)
```

## Inputs

| Input | Description | Required | Default |
|-------|-------------|----------|---------|
| `dotnet-version` | .NET version to install | No | `9.0.x` |
| `dotnet-quality` | .NET quality channel (ga, preview) | No | `preview` |
| `enable-nuget-cache` | Enable NuGet package caching | No | `true` |
| `cache-key-suffix` | Additional suffix for cache key | No | `''` |

## Examples

### Basic Usage
```yaml
- uses: ./.github/actions/setup-environment
```

### Production Release
```yaml
- uses: ./.github/actions/setup-environment
  with:
    dotnet-quality: 'ga'
```

### Custom Cache Key
```yaml
- uses: ./.github/actions/setup-environment
  with:
    cache-key-suffix: '-release'
```

## What It Provides

This action automatically handles:

```yaml
# ✅ Included automatically
- name: 🔧 Setup .NET 9.0
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: 9.0.x
    dotnet-quality: preview
    
- name: ⚙️ Configure Git safe.directory
  run: |
    git config --global --add safe.directory /github/workspace
    git config --global --add safe.directory $(pwd)
    
- name: ♻️ Setup NuGet cache
  uses: actions/cache@v4
  # ... cache configuration
  
- name: 🌐 Set environment variables
  run: |
    echo "DOTNET_NOLOGO=true" >> $GITHUB_ENV
    echo "DOTNET_CLI_TELEMETRY_OPTOUT=true" >> $GITHUB_ENV
    echo "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true" >> $GITHUB_ENV
```

## Benefits

- **Simplified Workflows**: Reduces boilerplate from ~20 lines to 2 lines
- **Consistent Environment**: Standardized setup across all CI/CD jobs
- **Performance**: Built-in caching reduces build times
- **Maintainable**: Single source of truth for environment configuration

## Note

This action requires checkout to be done separately before use:

```yaml
steps:
- name: 📥 Checkout repository
  uses: actions/checkout@v4
  
- name: 🛠️ Setup Environment
  uses: ./.github/actions/setup-environment
```