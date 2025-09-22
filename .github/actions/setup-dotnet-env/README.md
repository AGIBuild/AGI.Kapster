# Setup .NET Environment Action

A composite GitHub Action that provides common setup steps for .NET projects.

## Features

- **Repository Checkout**: Configurable fetch depth
- **.NET Setup**: Supports multiple .NET versions with quality channel selection
- **Git Configuration**: Automatically configures Git safe.directory for workflows

## Usage

```yaml
- name: Setup .NET Environment
  uses: ./.github/actions/setup-dotnet-env
  with:
    fetch-depth: 0              # Optional: Git fetch depth (default: 1)
    dotnet-version: '9.0.x'     # Optional: Primary .NET version (default: 9.0.x)
    dotnet-quality: 'preview'   # Optional: Quality channel (default: preview)
    include-dotnet8: 'true'     # Optional: Include .NET 8.x (default: true)
```

## Inputs

| Input | Description | Required | Default |
|-------|-------------|----------|---------|
| `fetch-depth` | Git fetch depth for checkout | No | `1` |
| `dotnet-quality` | .NET quality channel (ga, preview) | No | `preview` |
| `dotnet-version` | Primary .NET version to install | No | `9.0.x` |
| `include-dotnet8` | Whether to include .NET 8.x | No | `true` |

## Examples

### Basic Usage
```yaml
- uses: ./.github/actions/setup-dotnet-env
```

### Full History Checkout
```yaml
- uses: ./.github/actions/setup-dotnet-env
  with:
    fetch-depth: 0
```

### Production Channel
```yaml
- uses: ./.github/actions/setup-dotnet-env
  with:
    dotnet-quality: 'ga'
    include-dotnet8: 'false'
```

## What It Replaces

This action replaces the following common workflow steps:

```yaml
- name: 📥 Checkout repository
  uses: actions/checkout@v4
  with:
    fetch-depth: 0
    
- name: 🔧 Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: |
      8.0.x
      9.0.x
    dotnet-quality: 'preview'
    
- name: ⚙️ Configure Git safe.directory
  run: |
    git config --global --add safe.directory /github/workspace
    git config --global --add safe.directory $(pwd)
```

## Benefits

- **Consistency**: Ensures all jobs use identical environment setup
- **Maintainability**: Single location for environment configuration updates
- **Reduced Duplication**: Eliminates 15+ lines of repeated code per job
- **Flexibility**: Configurable parameters for different use cases