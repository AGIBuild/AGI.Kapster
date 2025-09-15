#!/usr/bin/env pwsh
param(
  [ValidateSet('Windows','macOS','All','Current')]
  [string]$Platform = 'Current',
  [ValidateSet('Debug','Release')]
  [string]$Configuration = 'Debug',
  [switch]$NoClean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Configuration
$PROJECT_NAME = 'AGI.Captor.Desktop'
$APP_NAME = 'AGI Captor'
$BUNDLE_ID = 'com.agi.captor'
$VERSION = '1.0.0'
$PROJECT_DIR = 'src/AGI.Captor.Desktop'
$OUTPUT_DIR = 'dist'

# Logging helpers
function Log-Info($msg){ Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Log-Success($msg){ Write-Host "[SUCCESS] $msg" -ForegroundColor Green }
function Log-Warn($msg){ Write-Host "[WARNING] $msg" -ForegroundColor Yellow }
function Log-Error($msg){ Write-Host "[ERROR] $msg" -ForegroundColor Red }

function Detect-Platform {
  if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) { return 'macOS' }
  if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) { return 'Windows' }
  if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) { return 'Linux' }
  return 'Unknown'
}

function Check-Prereqs {
  Log-Info 'Checking prerequisites...'
  $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
  if (-not $dotnet) { Log-Error '.NET SDK not found in PATH'; exit 1 }
  $ver = (& dotnet --version)
  Log-Success ".NET SDK version: $ver"
  $plat = Detect-Platform
  Log-Success "Platform detected: $plat"
}

function Clean-Build {
  Log-Info 'Cleaning previous builds...'
  if (Test-Path $OUTPUT_DIR) { Remove-Item -Recurse -Force $OUTPUT_DIR }
  & dotnet clean $PROJECT_DIR | Out-Null
  Get-ChildItem -Recurse -Directory -Path $PROJECT_DIR -Filter bin -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  Get-ChildItem -Recurse -Directory -Path $PROJECT_DIR -Filter obj -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  Log-Success 'Clean completed'
}

function Build-DotnetProject([string]$runtimeId){
  Log-Info "Building .NET project for $runtimeId..."
  & dotnet publish $PROJECT_DIR -c $Configuration -r $runtimeId --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o "$PROJECT_DIR/bin/$Configuration/net9.0/$runtimeId/publish"
  Log-Success "Build completed for $runtimeId"
}

function Build-WindowsPackage {
  Log-Info 'Building Windows package...'
  $rid = 'win-x64'
  Build-DotnetProject $rid
  $publishDir = Join-Path $PROJECT_DIR "bin/$Configuration/net9.0/$rid/publish"
  $windowsDir = Join-Path $OUTPUT_DIR 'Windows'
  New-Item -ItemType Directory -Force -Path $windowsDir | Out-Null
  Copy-Item -Path (Join-Path $publishDir '*') -Destination $windowsDir -Recurse -Force
  $exeOld = Join-Path $windowsDir "$PROJECT_NAME.exe"
  $exeNew = Join-Path $windowsDir "$APP_NAME.exe"
  if (Test-Path $exeOld) { Move-Item $exeOld $exeNew -Force }

  # Installer script
  $installer = @'
# Windows Installer Script for AGI Captor
param(
  [string]$InstallDir = "$env:ProgramFiles\AGI Captor"
)
$AppName = "AGI Captor"
$SourceDir = $PSScriptRoot
Write-Host "Installing $AppName to $InstallDir..."
if (-not (Test-Path $InstallDir)) { New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null }
Copy-Item -Path "$SourceDir\*" -Destination $InstallDir -Recurse -Force -Exclude 'Install.ps1'
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\$AppName.lnk")
$Shortcut.TargetPath = "$InstallDir\$AppName.exe"
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Save()
Write-Host "$AppName installed successfully!"
Write-Host 'Desktop shortcut created.'
'@
  Set-Content -LiteralPath (Join-Path $windowsDir 'Install.ps1') -Value $installer -Encoding UTF8
  Log-Success "Windows package created at: $windowsDir"
}

function Build-macOSPackage {
  Log-Info 'Building macOS package...'
  $rid = 'osx-x64'
  Build-DotnetProject $rid
  $publishDir = Join-Path $PROJECT_DIR "bin/$Configuration/net9.0/$rid/publish"
  $macDir = Join-Path $OUTPUT_DIR 'macOS'
  $appBundle = Join-Path $macDir "$APP_NAME.app"
  New-Item -ItemType Directory -Force -Path (Join-Path $appBundle 'Contents/MacOS') | Out-Null
  New-Item -ItemType Directory -Force -Path (Join-Path $appBundle 'Contents/Resources') | Out-Null
  Copy-Item -Path (Join-Path $publishDir '*') -Destination (Join-Path $appBundle 'Contents/MacOS') -Recurse -Force
  $binOld = Join-Path (Join-Path $appBundle 'Contents/MacOS') $PROJECT_NAME
  $binNew = Join-Path (Join-Path $appBundle 'Contents/MacOS') $APP_NAME
  if (Test-Path $binOld) { Move-Item $binOld $binNew -Force }
  $ico = Join-Path $publishDir 'logo.ico'
  if (Test-Path $ico) { Copy-Item $ico (Join-Path $appBundle 'Contents/Resources') -Force }
  $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>$APP_NAME</string>
  <key>CFBundleDisplayName</key><string>$APP_NAME</string>
  <key>CFBundleIdentifier</key><string>$BUNDLE_ID</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleSignature</key><string>????</string>
  <key>CFBundleExecutable</key><string>$APP_NAME</string>
  <key>CFBundleIconFile</key><string>logo.ico</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>LSMinimumSystemVersion</key><string>10.15</string>
  <key>NSCameraUsageDescription</key><string>AGI Captor needs screen recording permission to capture screenshots.</string>
  <key>NSScreenCaptureDescription</key><string>AGI Captor needs screen recording permission to capture screenshots.</string>
  <key>LSUIElement</key><false/>
</dict>
</plist>
"@
  Set-Content -LiteralPath (Join-Path $appBundle 'Contents/Info.plist') -Value $plist -Encoding UTF8
  Log-Success "macOS app bundle created at: $appBundle"

  $dmgScript = @'
#!/usr/bin/env bash
APP_NAME="AGI Captor"
VERSION="1.0.0"
SOURCE_DIR="$(dirname "$0")"
DMG_NAME="${APP_NAME}-${VERSION}.dmg"
echo "Creating DMG: $DMG_NAME"
hdiutil create -size 100m -fs HFS+ -volname "$APP_NAME" -srcfolder "$SOURCE_DIR" "$DMG_NAME.temp.dmg"
hdiutil convert "$DMG_NAME.temp.dmg" -format UDZO -o "$DMG_NAME"
rm "$DMG_NAME.temp.dmg"
echo "DMG created: $DMG_NAME"
'@
  $dmgPath = Join-Path $macDir 'create-dmg.sh'
  Set-Content -LiteralPath $dmgPath -Value $dmgScript -Encoding UTF8
  Log-Success "DMG creation script created: $dmgPath"
}

function Show-Usage {
  Write-Host "Usage: ./build.ps1 [-Platform Windows|macOS|All|Current] [-Configuration Debug|Release] [--NoClean]" -ForegroundColor Yellow
}

# Main
Log-Info 'AGI.Captor.Desktop Build Script'
Log-Info "Platform: $Platform | Configuration: $Configuration"
if ($Configuration -eq 'Debug') { Log-Info 'Environment: dev (Debug level logging)' } else { Log-Info 'Environment: prod (Warning level logging)' }
Log-Info '========================================'

Check-Prereqs
if (-not $NoClean) { Clean-Build }
New-Item -ItemType Directory -Force -Path $OUTPUT_DIR | Out-Null

$current = Detect-Platform
$target = if ($Platform -eq 'Current') { $current } else { $Platform }

switch ($target) {
  'Windows' { Build-WindowsPackage }
  'macOS' { Build-macOSPackage }
  'All' { Build-WindowsPackage; Build-macOSPackage }
  Default { Log-Error "Unknown platform: $target"; Show-Usage; exit 1 }
}

Log-Success '========================================'
Log-Success 'Build completed successfully!'
Log-Success "Output directory: $OUTPUT_DIR"
if (Test-Path $OUTPUT_DIR) {
  Log-Info 'Contents:'
  Get-ChildItem -Path $OUTPUT_DIR -Recurse | ForEach-Object { Write-Host "  $($_.FullName)" }
}

