# Versioning Strategy

## Overview

AGI.Kapster uses a **locked time-based versioning strategy** that provides predictable, deterministic version management with full CI/CD integration and strict validation.

## Version Format

### Time-Based Schema
```
YYYY.M.D.HHmm
```

**Examples:**
- `2024.9.23.1547` - September 23, 2024 at 15:47
- `2024.12.1.0930` - December 1, 2024 at 09:30

### Format Rules
- **Year**: 4-digit year (e.g., 2024)
- **Month**: 1-2 digit month (1-12, no leading zeros)
- **Day**: 1-2 digit day (1-31, no leading zeros)
- **Time**: 4-digit HHMM in 24-hour format (0000-2359)

### Validation Pattern
```regex
^\d{4}\.[1-9]\d?\.[1-9]\d?\.[0-2]\d[0-5]\d$
```

## Locked Version System

### Generation Process
```bash
# Generate new locked version
./build.ps1 UpgradeVersion

# This creates/updates version.json with:
{
  "version": "2024.9.23.1547",
  "assemblyVersion": "2024.9.23.1547",
  "fileVersion": "2024.9.23.1547",
  "informationalVersion": "2024.9.23.1547"
}
```

### Locking Mechanism
1. **Generation**: NUKE target `UpgradeVersion` creates timestamp-based version
2. **Persistence**: Version written to `version.json` in repository root
3. **Commitment**: Version must be committed to repository before tagging
4. **Validation**: CI validates tag matches locked version exactly

### Version Consistency
All version fields use the **same value** for consistency:
- `AssemblyVersion`: Used by .NET runtime
- `FileVersion`: Used by Windows file properties
- `InformationalVersion`: Used for display purposes
- `Version`: Used by package managers

## Release Workflow Integration

### Tag-Driven Releases
```bash
# 1. Generate and lock version
./build.ps1 UpgradeVersion

# 2. Commit version
git add version.json
git commit -m "chore: bump version to 2024.9.23.1547"

# 3. Create matching tag
git tag v2024.9.23.1547
git push origin v2024.9.23.1547
```

### Validation Pipeline
```yaml
# .github/workflows/verify-version.yml
- name: Verify Version Lock
  run: ./build.ps1 CheckVersionLocked

- name: Validate Tag Format
  run: |
    if [[ ! "${{ github.ref_name }}" =~ ^v[0-9]{4}\.[1-9][0-9]?\.[1-9][0-9]?\.[0-2][0-9][0-5][0-9]$ ]]; then
      echo "Invalid tag format: ${{ github.ref_name }}"
      exit 1
    fi
```

### Release Triggers
- **Full Release**: Only triggered by properly formatted version tags
- **Tag Validation**: Ensures tag `v<version>` matches `version.json`
- **Concurrency Control**: Prevents simultaneous releases
- **Ancestor Validation**: Ensures clean release branch state

## Branch Strategy

### Branch-Version Relationship
| Branch | Version Source | Purpose |
|--------|----------------|---------|
| `main` | Latest stable | Production-ready code |
| `release` | Lock and tag | Release preparation |
| `feature/*` | No versioning | Feature development |
| `hotfix/*` | Patch versioning | Critical fixes |

### Version Management by Branch
```bash
# Feature development (no versioning)
git checkout -b feature/new-overlay-mode
# Develop feature without version changes

# Release preparation
git checkout release
git merge feature/new-overlay-mode
./build.ps1 UpgradeVersion  # Generate new version
git add version.json
git commit -m "chore: bump version to 2024.9.23.1547"

# Tag and release
git tag v2024.9.23.1547
git push origin v2024.9.23.1547
```

## CI/CD Integration

### Version Validation Workflow
```yaml
name: Verify Version
on:
  pull_request:
    paths: ['version.json']

jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - uses: ./.github/actions/setup-dotnet-only
    - name: Check Version Lock
      run: ./build.ps1 CheckVersionLocked
```

### Release Automation
```yaml
name: Release
on:
  push:
    tags: ['v*']

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
    - name: Validate Tag Version
      run: |
        TAG_VERSION=${GITHUB_REF#refs/tags/v}
        FILE_VERSION=$(jq -r .version version.json)
        if [ "$TAG_VERSION" != "$FILE_VERSION" ]; then
          echo "Tag version $TAG_VERSION doesn't match file version $FILE_VERSION"
          exit 1
        fi
```

## Advantages of Time-Based Versioning

### Predictability
- **Chronological Ordering**: Natural sort order matches release order
- **No Conflicts**: Timestamp uniqueness prevents version collisions
- **Timezone Independence**: Uses consistent UTC-based generation

### Operational Benefits
- **Release Planning**: Clear timeline visibility
- **Debugging**: Version timestamp aids in issue correlation
- **Automation**: Deterministic version generation
- **Compliance**: Audit trail through timestamp correlation

### Development Workflow
- **No Manual Decisions**: Eliminates semantic versioning debates
- **Automated Process**: No human intervention required
- **Consistent Application**: Same rules across all components
- **CI/CD Friendly**: Perfect for automated release pipelines

## Comparison with Semantic Versioning

| Aspect | Time-Based | Semantic (SemVer) |
|--------|------------|-------------------|
| **Predictability** | High (timestamp) | Medium (manual decisions) |
| **Automation** | Full automation | Requires analysis |
| **Ordering** | Chronological | Logical |
| **Breaking Changes** | Through changelog | Version number |
| **Patch Releases** | New timestamp | Increment patch |
| **API Compatibility** | External documentation | Version encoding |

## Version Information Display

### Application Version
```csharp
// In application code
public static class VersionInfo
{
    public static string Version => "2024.9.23.1547";
    public static DateTime BuildTime => new DateTime(2024, 9, 23, 15, 47, 0);
    public static string DisplayVersion => $"Version {Version} (Built: {BuildTime:yyyy-MM-dd HH:mm})";
}
```

### Assembly Attributes
```csharp
[assembly: AssemblyVersion("2024.9.23.1547")]
[assembly: AssemblyFileVersion("2024.9.23.1547")]
[assembly: AssemblyInformationalVersion("2024.9.23.1547")]
```

### Package Metadata
```xml
<PropertyGroup>
  <Version>2024.9.23.1547</Version>
  <AssemblyVersion>2024.9.23.1547</AssemblyVersion>
  <FileVersion>2024.9.23.1547</FileVersion>
  <InformationalVersion>2024.9.23.1547</InformationalVersion>
</PropertyGroup>
```

## Troubleshooting

### Common Issues

#### Version Mismatch
```bash
# Problem: Tag doesn't match version.json
Error: Tag version 2024.9.23.1547 doesn't match file version 2024.9.23.1546

# Solution: Regenerate version or fix tag
./build.ps1 UpgradeVersion
git add version.json
git commit -m "chore: fix version to 2024.9.23.1548"
git tag v2024.9.23.1548
```

#### Time Conflicts
```bash
# Problem: Same minute version generation
# Solution: Wait one minute or manually adjust
./build.ps1 UpgradeVersion --time-offset +1
```

#### Build Integration Issues
```bash
# Check version lock status
./build.ps1 CheckVersionLocked

# Force version regeneration
./build.ps1 UpgradeVersion --force

# Validate version format
./build.ps1 ValidateVersion
```

### Debug Commands
```bash
# Display current version info
./build.ps1 Info

# Show version generation details
./build.ps1 UpgradeVersion --dry-run

# Validate version consistency
./build.ps1 CheckVersionLocked --verbose
```

## Best Practices

### Development Process
1. **Never edit version.json manually** - Always use NUKE targets
2. **Commit version changes separately** - Clear audit trail
3. **Validate before tagging** - Ensure version consistency
4. **Use descriptive commit messages** - Follow conventional commits

### Release Management
1. **Single source of truth** - version.json is authoritative
2. **Validate in CI** - Prevent invalid releases
3. **Document breaking changes** - Use changelog for API changes
4. **Monitor release metrics** - Track version distribution

### Automation Guidelines
1. **Lock before release** - No dynamic version calculation
2. **Validate tag format** - Strict pattern matching
3. **Check ancestor commits** - Ensure clean release state
4. **Generate checksums** - Verify release integrity

## Migration Considerations

### From GitVersion
```bash
# Remove GitVersion configuration
rm GitVersion.yml

# Update build scripts
# Replace GitVersion.* with version.json values

# Update CI/CD pipelines
# Remove GitVersion setup, add version validation
```

### From Manual Versioning
```bash
# Initialize version.json
./build.ps1 UpgradeVersion --initial

# Update project files
# Remove hardcoded versions, reference version.json

# Train team on new process
# Document workflow changes
```