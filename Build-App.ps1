# Cross-platform build script for AGI.Captor.Desktop
# Supports Windows (.exe + installer) and macOS (.app bundle)

param(
    [Parameter(Mandatory=$false)]
    [Alias("p")]
    [string]$Platform = "Current",
    
    [Parameter(Mandatory=$false)]
    [Alias("c")]
    [string]$Configuration = "Debug",
    
    [Parameter(Mandatory=$false)]
    [switch]$NoClean
)

# Configuration
$ProjectName = "AGI.Captor.Desktop"
$AppName = "AGI Captor"
$BundleId = "com.agi.captor"
$Version = "1.0.0"
$ProjectDir = "src/AGI.Captor.Desktop"
$OutputDir = "dist"

# Colors for output
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

function Write-Success { Write-ColorOutput Green $args }
function Write-Info { Write-ColorOutput Cyan $args }
function Write-Warning { Write-ColorOutput Yellow $args }
function Write-Error { Write-ColorOutput Red $args }

# Helper functions
function Test-Command($cmdname) {
    return [bool](Get-Command -Name $cmdname -ErrorAction SilentlyContinue)
}

# Expand short forms for parameters
function Expand-Platform($platform) {
    switch ($platform.ToLower()) {
        { $_ -in @("win", "windows") } { return "Windows" }
        { $_ -in @("mac", "macos") } { return "macOS" }
        { $_ -in @("all") } { return "All" }
        { $_ -in @("current", "cur") } { return "Current" }
        default { return $platform }
    }
}

function Expand-Configuration($config) {
    switch ($config.ToLower()) {
        { $_ -in @("rel", "release") } { return "Release" }
        { $_ -in @("dbg", "debug") } { return "Debug" }
        default { return $config }
    }
}

function Invoke-SafeCommand {
    param([string]$Command)
    Write-Info "Executing: $Command"
    Invoke-Expression $Command
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Command failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

# Validate prerequisites
function Test-Prerequisites {
    Write-Info "Checking prerequisites..."
    
    if (-not (Test-Command "dotnet")) {
        Write-Error ".NET SDK is not installed or not in PATH"
        exit 1
    }
    
    $dotnetVersion = dotnet --version
    Write-Success ".NET SDK version: $dotnetVersion"
    
    if ($Platform -eq "Windows" -or $Platform -eq "All") {
        if ($IsWindows -or $env:OS -eq "Windows_NT") {
            Write-Success "Windows platform detected"
        } else {
            Write-Warning "Building Windows packages on non-Windows platform"
        }
    }
    
    if ($Platform -eq "macOS" -or $Platform -eq "All") {
        if ($IsMacOS -or (uname 2>$null) -eq "Darwin") {
            Write-Success "macOS platform detected"
        } else {
            Write-Warning "Building macOS packages on non-macOS platform"
        }
    }
}

# Clean function
function Invoke-Clean {
    Write-Info "Cleaning previous builds..."
    
    if (Test-Path $OutputDir) {
        Remove-Item -Recurse -Force $OutputDir
    }
    
    Invoke-SafeCommand "dotnet clean `"$ProjectDir`""
    
    # Clean bin and obj directories
    Get-ChildItem -Path $ProjectDir -Recurse -Directory | Where-Object { $_.Name -in @("bin", "obj") } | ForEach-Object {
        if (Test-Path $_.FullName) {
            Remove-Item -Recurse -Force $_.FullName
        }
    }
    
    Write-Success "Clean completed"
}

# Build .NET project
function Invoke-DotNetBuild {
    param([string]$RuntimeId)
    
    Write-Info "Building .NET project for $RuntimeId..."
    
    $buildArgs = @(
        "publish"
        "`"$ProjectDir`""
        "-c", $Configuration
        "-r", $RuntimeId
        "--self-contained", "true"
        "-p:PublishSingleFile=true"
        "-p:PublishTrimmed=false"
        "-o", "`"$ProjectDir/bin/$Configuration/net9.0/$RuntimeId/publish`""
    )
    
    Invoke-SafeCommand "dotnet $($buildArgs -join ' ')"
    Write-Success "Build completed for $RuntimeId"
}

# Build Windows package
function Build-WindowsPackage {
    Write-Info "Building Windows package..."
    
    $runtimeId = "win-x64"
    Invoke-DotNetBuild $runtimeId
    
    $publishDir = "$ProjectDir/bin/$Configuration/net9.0/$runtimeId/publish"
    $windowsDir = "$OutputDir/Windows"
    
    # Create output directory
    New-Item -ItemType Directory -Force -Path $windowsDir | Out-Null
    
    # Copy published files
    Copy-Item -Path "$publishDir/*" -Destination $windowsDir -Recurse -Force
    
    # Rename executable
    $exePath = Join-Path $windowsDir "$ProjectName.exe"
    $newExePath = Join-Path $windowsDir "$AppName.exe"
    if (Test-Path $exePath) {
        Move-Item $exePath $newExePath -Force
    }
    
    Write-Success "Windows package created at: $windowsDir"
    
    # Create installer script (optional)
    $installerScript = @"
# Windows Installer Script for $AppName
# This is a basic PowerShell installer script

`$AppName = "$AppName"
`$InstallDir = "`$env:ProgramFiles\`$AppName"
`$SourceDir = "`$PSScriptRoot"

Write-Host "Installing `$AppName to `$InstallDir..."

# Create install directory
if (-not (Test-Path `$InstallDir)) {
    New-Item -ItemType Directory -Force -Path `$InstallDir | Out-Null
}

# Copy files
Copy-Item -Path "`$SourceDir\*" -Destination `$InstallDir -Recurse -Force -Exclude "Install.ps1"

# Create desktop shortcut
`$WshShell = New-Object -comObject WScript.Shell
`$Shortcut = `$WshShell.CreateShortcut("`$env:USERPROFILE\Desktop\`$AppName.lnk")
`$Shortcut.TargetPath = "`$InstallDir\`$AppName.exe"
`$Shortcut.WorkingDirectory = `$InstallDir
`$Shortcut.Save()

Write-Host "`$AppName installed successfully!"
Write-Host "Desktop shortcut created."
"@
    
    $installerScript | Out-File -FilePath "$windowsDir/Install.ps1" -Encoding UTF8
    Write-Success "Windows installer script created: $windowsDir/Install.ps1"
}

# Build macOS package
function Build-MacOSPackage {
    Write-Info "Building macOS package..."
    
    $runtimeId = "osx-x64"
    Invoke-DotNetBuild $runtimeId
    
    $publishDir = "$ProjectDir/bin/$Configuration/net9.0/$runtimeId/publish"
    $macosDir = "$OutputDir/macOS"
    $appBundle = "$macosDir/$AppName.app"
    
    # Create output directory
    New-Item -ItemType Directory -Force -Path $macosDir | Out-Null
    
    # Create app bundle structure
    $bundleDirs = @(
        "$appBundle/Contents/MacOS",
        "$appBundle/Contents/Resources"
    )
    
    foreach ($dir in $bundleDirs) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }
    
    # Copy executable and dependencies
    Copy-Item -Path "$publishDir/*" -Destination "$appBundle/Contents/MacOS/" -Recurse -Force
    
    # Rename executable
    $exePath = "$appBundle/Contents/MacOS/$ProjectName"
    $newExePath = "$appBundle/Contents/MacOS/$AppName"
    if (Test-Path $exePath) {
        Move-Item $exePath $newExePath -Force
    }
    
    # Copy resources
    $logoPath = "$publishDir/logo.ico"
    if (Test-Path $logoPath) {
        Copy-Item $logoPath "$appBundle/Contents/Resources/" -Force
    }
    
    # Create Info.plist
    $infoPlist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$AppName</string>
    <key>CFBundleDisplayName</key>
    <string>$AppName</string>
    <key>CFBundleIdentifier</key>
    <string>$BundleId</string>
    <key>CFBundleVersion</key>
    <string>$Version</string>
    <key>CFBundleShortVersionString</key>
    <string>$Version</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleExecutable</key>
    <string>$AppName</string>
    <key>CFBundleIconFile</key>
    <string>logo.ico</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSCameraUsageDescription</key>
    <string>AGI Captor needs screen recording permission to capture screenshots.</string>
    <key>NSScreenCaptureDescription</key>
    <string>AGI Captor needs screen recording permission to capture screenshots.</string>
    <key>LSUIElement</key>
    <false/>
</dict>
</plist>
"@
    
    $infoPlist | Out-File -FilePath "$appBundle/Contents/Info.plist" -Encoding UTF8
    
    # Make executable (on Unix-like systems)
    if ($IsMacOS -or $IsLinux) {
        chmod +x "$appBundle/Contents/MacOS/$AppName"
    }
    
    Write-Success "macOS app bundle created at: $appBundle"
    
    # Create DMG creation script (for macOS only)
    if ($IsMacOS -or (Get-Command "hdiutil" -ErrorAction SilentlyContinue)) {
        $dmgScript = @"
#!/bin/bash
# Create DMG for $AppName

APP_NAME="$AppName"
VERSION="$Version"
SOURCE_DIR="$macosDir"
DMG_NAME="`${APP_NAME}-`${VERSION}.dmg"

echo "Creating DMG: `$DMG_NAME"

# Create temporary DMG
hdiutil create -size 100m -fs HFS+ -volname "`$APP_NAME" -srcfolder "`$SOURCE_DIR" "`$DMG_NAME.temp.dmg"

# Convert to compressed DMG
hdiutil convert "`$DMG_NAME.temp.dmg" -format UDZO -o "`$DMG_NAME"

# Clean up
rm "`$DMG_NAME.temp.dmg"

echo "DMG created: `$DMG_NAME"
"@
        
        $dmgScript | Out-File -FilePath "$macosDir/create-dmg.sh" -Encoding UTF8
        if ($IsMacOS -or $IsLinux) {
            chmod +x "$macosDir/create-dmg.sh"
        }
        Write-Success "DMG creation script created: $macosDir/create-dmg.sh"
    }
}

# Main execution
function Main {
    # Expand short forms
    $script:Platform = Expand-Platform $Platform
    $script:Configuration = Expand-Configuration $Configuration
    
    Write-Info "AGI.Captor.Desktop Build Script"
    Write-Info "Platform: $Platform | Configuration: $Configuration | Clean: $(-not $NoClean)"
    Write-Info "========================================"
    
    Test-Prerequisites
    
    if (-not $NoClean) {
        Invoke-Clean
    }
    
    # Create output directory
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
    
    try {
        if ($Platform -eq "Windows" -or $Platform -eq "All") {
            Build-WindowsPackage
        }
        
        if ($Platform -eq "macOS" -or $Platform -eq "All") {
            Build-MacOSPackage
        }
        
        Write-Success "========================================"
        Write-Success "Build completed successfully!"
        Write-Success "Output directory: $OutputDir"
        
        if (Test-Path $OutputDir) {
            Write-Info "Contents:"
            Get-ChildItem $OutputDir -Recurse | ForEach-Object {
                Write-Info "  $($_.FullName.Replace((Get-Location).Path, '.'))"
            }
        }
        
    } catch {
        Write-Error "Build failed: $($_.Exception.Message)"
        exit 1
    }
}

# Run main function
Main
