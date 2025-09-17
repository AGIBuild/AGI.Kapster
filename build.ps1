#!/usr/bin/env pwsh
[cmdletbinding()]
param(
    [Parameter(Mandatory = $false, HelpMessage = "Target platform to build for")]
    [ValidateSet('Windows','macOS','All','Current')]
    [string]$Platform = 'Current',
    
    [Parameter(Mandatory = $false, HelpMessage = "Build configuration")]
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Debug',
    
    [Parameter(Mandatory = $false, HelpMessage = "Run tests only (no build)")]
    [switch]$Test,
    
    [Parameter(Mandatory = $false, HelpMessage = "Enable code coverage collection")]
    [switch]$Coverage,
    
    [Parameter(Mandatory = $false, HelpMessage = "Open coverage report after generation")]
    [switch]$OpenReport,
    
    [Parameter(Mandatory = $false, HelpMessage = "Test filter expression")]
    [string]$TestFilter = "",
    
    [Parameter(Mandatory = $false, HelpMessage = "Create distribution package")]
    [switch]$Package,
    
    [Parameter(Mandatory = $false, HelpMessage = "Show application version")]
    [switch]$Version,
    
    [Parameter(Mandatory = $false, HelpMessage = "Clean build artifacts only")]
    [switch]$Clean,
    
    [Parameter(Mandatory = $false, HelpMessage = "Build project")]
    [switch]$Build,
    
    [Parameter(Mandatory = $false, HelpMessage = "Clean and rebuild project")]
    [switch]$Rebuild,
    
    [Parameter(Mandatory = $false, HelpMessage = "Run the application")]
    [switch]$Run,
    
    [Parameter(Mandatory = $false, HelpMessage = "Show help information")]
    [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Configuration
$PROJECT_NAME = 'AGI.Captor.Desktop'
$APP_NAME = 'AGI Captor'
$BUNDLE_ID = 'com.agi.captor'
$APP_VERSION = '1.0.0'
$PROJECT_DIR = 'src/AGI.Captor.Desktop'
$TEST_PROJECT_DIR = 'tests/AGI.Captor.Tests'
$BUILD_OUTPUT_DIR = 'dist'

# Logging helpers
function Log-Info($msg){ Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Log-Success($msg){ Write-Host "[SUCCESS] $msg" -ForegroundColor Green }
function Log-Warn($msg){ Write-Host "[WARNING] $msg" -ForegroundColor Yellow }
function Log-Error($msg){ Write-Host "[ERROR] $msg" -ForegroundColor Red }

# Get project version from csproj file
function Get-ProjectVersion {
  $csprojPath = "$PROJECT_DIR/$PROJECT_NAME.csproj"
  if (Test-Path $csprojPath) {
    try {
      [xml]$xml = Get-Content $csprojPath -Raw
      # Try to find Version tag first
      $version = $xml.Project.PropertyGroup.Version
      if ($version) {
        return $version
      }
      # Fallback to CFBundleVersion for macOS projects
      $cfBundleVersion = $xml.Project.PropertyGroup.CFBundleVersion
      if ($cfBundleVersion) {
        return $cfBundleVersion
      }
    } catch {
      # If XML parsing fails, fall back to default
      return '1.0.0'
    }
  }
  return '1.0.0'
}

function Detect-Platform {
  if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) { return 'macOS' }
  if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) { return 'Windows' }
  if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) { return 'Linux' }
  return 'Unknown'
}

function Get-RuntimeIdentifier {
  $platform = Detect-Platform
  switch ($platform) {
    'Windows' { return 'win-x64' }
    'macOS' { return 'osx-x64' }
    'Linux' { return 'linux-x64' }
    default { return 'unknown' }
  }
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
  if (Test-Path $BUILD_OUTPUT_DIR) { Remove-Item -Recurse -Force $BUILD_OUTPUT_DIR }
  & dotnet clean $PROJECT_DIR | Out-Null
  & dotnet clean $TEST_PROJECT_DIR | Out-Null
  Get-ChildItem -Recurse -Directory -Path $PROJECT_DIR -Filter bin -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  Get-ChildItem -Recurse -Directory -Path $PROJECT_DIR -Filter obj -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  Get-ChildItem -Recurse -Directory -Path $TEST_PROJECT_DIR -Filter bin -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  Get-ChildItem -Recurse -Directory -Path $TEST_PROJECT_DIR -Filter obj -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  Log-Success 'Clean completed'
}

function Build-DotnetProject([string]$runtimeId){
  Log-Info "Building .NET project for $runtimeId..."
  & dotnet publish $PROJECT_DIR -c $Configuration -r $runtimeId --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o "$PROJECT_DIR/bin/$Configuration/net9.0/$runtimeId/publish"
  Log-Success "Build completed for $runtimeId"
}

function Build-TestProject {
  Log-Info 'Building test project...'
  & dotnet build $TEST_PROJECT_DIR -c $Configuration --verbosity minimal
  if ($LASTEXITCODE -ne 0) {
    Log-Error 'Test project build failed'
    exit 1
  }
  Log-Success 'Test project build completed'
}

function Run-Tests {
  Log-Info 'Running unit tests...'
  
  $testCommand = "dotnet test $TEST_PROJECT_DIR -c $Configuration --verbosity normal --logger `"console;verbosity=detailed`""
  
  if ($Coverage) {
    $testCommand += " --collect:`"XPlat Code Coverage`""
    $testCommand += " --results-directory `"TestResults`""
    Log-Info 'Code coverage collection enabled'
  }
  
  if ($TestFilter) {
    $testCommand += " --filter `"$TestFilter`""
    Log-Info "Test filter: $TestFilter"
  }
  
  Invoke-Expression $testCommand
  
  if ($LASTEXITCODE -ne 0) {
    Log-Error 'Tests failed'
    exit 1
  }
  
  Log-Success 'All tests passed'
  
  if ($Coverage) {
    Generate-CoverageReport
  }
}

function Run-Tests-Only {
  Log-Info 'Running tests only (no build)...'
  
  $testCommand = "dotnet test $TEST_PROJECT_DIR -c $Configuration --verbosity normal --logger `"console;verbosity=detailed`" --no-build"
  
  if ($Coverage) {
    $testCommand += " --collect:`"XPlat Code Coverage`""
    $testCommand += " --results-directory `"TestResults`""
    Log-Info 'Code coverage collection enabled'
  }
  
  if ($TestFilter) {
    $testCommand += " --filter `"$TestFilter`""
    Log-Info "Test filter: $TestFilter"
  }
  
  Invoke-Expression $testCommand
  
  if ($LASTEXITCODE -ne 0) {
    Log-Error 'Tests failed'
    exit 1
  }
  
  Log-Success 'All tests passed'
  
  if ($Coverage) {
    Generate-CoverageReport
  }
}

function Run-Application {
  Log-Info 'Running application...'
  
  $current = Detect-Platform
  $rid = Get-RuntimeIdentifier
  
  if ($rid -eq 'unknown') {
    Log-Error "Unsupported platform for running: $current"
    exit 1
  }
  
  # Determine executable extension
  $exeExtension = if ($current -eq 'Windows') { '.exe' } else { '' }
  
  $exePath = Join-Path $PROJECT_DIR "bin/$Configuration/net9.0/$rid/$PROJECT_NAME$exeExtension"
  
  if (-not (Test-Path $exePath)) {
    Log-Error "Application not found at: $exePath"
    Log-Info "Please build the application first using: .\build.ps1 -Build"
    exit 1
  }
  
  Log-Info "Starting $current application: $exePath"
  
  if ($current -eq 'Windows') {
    Start-Process -FilePath $exePath -NoNewWindow
  } else {
    & $exePath
  }
  
  Log-Success 'Application started successfully'
}

function Generate-CoverageReport {
  Log-Info 'Generating coverage report...'
  
  # Find the latest coverage file
  $coverageFiles = Get-ChildItem -Path "TestResults" -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue
  if ($coverageFiles.Count -eq 0) {
    Log-Warn "No coverage files found in TestResults directory"
    return
  }
  
  $latestCoverageFile = $coverageFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  Log-Info "Found coverage file: $($latestCoverageFile.FullName)"
  
  # Create a simple summary from the XML
  try {
    [xml]$coverageXml = Get-Content $latestCoverageFile.FullName
    $lineRate = [double]$coverageXml.coverage.'line-rate' * 100
    $branchRate = [double]$coverageXml.coverage.'branch-rate' * 100
    
    Log-Success "Code Coverage Summary:"
    Log-Success "  Line Coverage: $([math]::Round($lineRate, 2))%"
    Log-Success "  Branch Coverage: $([math]::Round($branchRate, 2))%"
    Log-Success "  Coverage Report: $($latestCoverageFile.FullName)"
    
    if ($OpenReport) {
      Log-Info "Opening coverage file..."
      Start-Process $latestCoverageFile.FullName
    }
  } catch {
    Log-Warn "Could not parse coverage XML: $($_.Exception.Message)"
    Log-Info "Coverage data available in: $($latestCoverageFile.FullName)"
  }
}

function Create-Package {
  Log-Info 'Creating distribution package...'
  
  $current = Detect-Platform
  $target = if ($Platform -eq 'Current') { $current } else { $Platform }
  
  switch ($target) {
    'Windows' { Build-WindowsPackage }
    'macOS' { Build-macOSPackage }
    'All' { Build-WindowsPackage; Build-macOSPackage }
    Default { Log-Error "Unknown platform: $target"; exit 1 }
  }
  
  Log-Success 'Package created successfully'
}

function Build-WindowsPackage {
  Log-Info 'Building Windows package...'
  $rid = 'win-x64'
  Build-DotnetProject $rid
  $publishDir = Join-Path $PROJECT_DIR "bin/$Configuration/net9.0/$rid/publish"
  $windowsDir = Join-Path $BUILD_OUTPUT_DIR 'Windows'
  New-Item -ItemType Directory -Force -Path $windowsDir | Out-Null
  Copy-Item -Path (Join-Path $publishDir '*') -Destination $windowsDir -Recurse -Force
  $exeOld = Join-Path $windowsDir "$PROJECT_NAME.exe"
  $exeNew = Join-Path $windowsDir "$APP_NAME.exe"
  if (Test-Path $exeOld) { Move-Item $exeOld $exeNew -Force }

  Log-Success "Windows package created at: $windowsDir"
}

function Build-macOSPackage {
  Log-Info 'Building macOS package...'
  $rid = 'osx-x64'
  Build-DotnetProject $rid
  $publishDir = Join-Path $PROJECT_DIR "bin/$Configuration/net9.0/$rid/publish"
  $macDir = Join-Path $BUILD_OUTPUT_DIR 'macOS'
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
  <key>CFBundleVersion</key><string>$APP_VERSION</string>
  <key>CFBundleShortVersionString</key><string>$APP_VERSION</string>
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
  Write-Host "AGI.Captor Build Script" -ForegroundColor Yellow
  Write-Host "Usage: ./build.ps1 [OPTIONS]" -ForegroundColor Yellow
  Write-Host ""
  Write-Host "Build Options:" -ForegroundColor Cyan
  Write-Host "  -Platform           Target platform (Windows, macOS, All, Current) [default: Current]" -ForegroundColor White
  Write-Host "  -Configuration      Build configuration (Debug, Release) [default: Debug]" -ForegroundColor White
  Write-Host "  -Build              Build project" -ForegroundColor White
  Write-Host "  -Rebuild            Clean and rebuild project" -ForegroundColor White
  Write-Host "  -Clean              Clean build artifacts only" -ForegroundColor White
  Write-Host ""
  Write-Host "Test Options:" -ForegroundColor Cyan
  Write-Host "  -Test               Run tests only (no build)" -ForegroundColor White
  Write-Host ""
  Write-Host "Run Options:" -ForegroundColor Cyan
  Write-Host "  -Run                Run the application" -ForegroundColor White
  Write-Host ""
  Write-Host "Package Options:" -ForegroundColor Cyan
  Write-Host "  -Package            Create distribution package" -ForegroundColor White
  Write-Host "  -Version            Show application version" -ForegroundColor White
  Write-Host ""
  Write-Host "Examples:" -ForegroundColor Cyan
  Write-Host "  ./build.ps1 -Build                             # Build current platform" -ForegroundColor White
  Write-Host "  ./build.ps1 -Test                             # Run tests only" -ForegroundColor White
  Write-Host "  ./build.ps1 -Run                              # Run the application" -ForegroundColor White
  Write-Host "  ./build.ps1 -Clean                            # Clean build artifacts" -ForegroundColor White
  Write-Host "  ./build.ps1 -Rebuild                          # Clean and rebuild" -ForegroundColor White
  Write-Host "  ./build.ps1 -Build -Package                   # Build and package" -ForegroundColor White
  Write-Host "  ./build.ps1 -Version                          # Show application version" -ForegroundColor White
}

# Main
# Handle help
if ($Help) {
  Show-Usage
  exit 0
}

# Handle version display
if ($Version) {
  $projectVersion = Get-ProjectVersion
  Write-Host "AGI.Captor Version: $projectVersion" -ForegroundColor Green
  exit 0
}

Log-Info 'AGI.Captor.Desktop Build Script'
Log-Info "Platform: $Platform | Configuration: $Configuration"
if ($Configuration -eq 'Debug') { Log-Info 'Environment: dev (Debug level logging)' } else { Log-Info 'Environment: prod (Warning level logging)' }
Log-Info '========================================'

Check-Prereqs

# Handle clean-only mode (only if no other operations are specified)
if ($Clean -and -not $Build -and -not $Rebuild -and -not $Test -and -not $Package) {
  Log-Info 'Clean mode: Cleaning build artifacts only'
  Clean-Build
  Log-Success '========================================'
  Log-Success 'Clean completed!'
  exit 0
}

# Handle test-only mode (only if no build operations are specified)
if ($Test -and -not $Build -and -not $Rebuild -and -not $Package -and -not $Run) {
  Log-Info 'Test-only mode: Running tests without build'
  Run-Tests-Only
  Log-Success '========================================'
  Log-Success 'Tests completed!'
  exit 0
}

# Handle run-only mode (only if no other operations are specified)
if ($Run -and -not $Build -and -not $Rebuild -and -not $Test -and -not $Package) {
  Log-Info 'Run mode: Starting application'
  Run-Application
  Log-Success '========================================'
  Log-Success 'Application started!'
  exit 0
}

# Handle rebuild mode
if ($Rebuild) {
  Log-Info 'Rebuild mode: Clean and build'
  Clean-Build
}

# Create output directory if building
if ($Build -or $Rebuild -or $Package) {
  New-Item -ItemType Directory -Force -Path $BUILD_OUTPUT_DIR | Out-Null
}

# Handle package-only mode
if ($Package -and -not $Build -and -not $Rebuild) {
  Create-Package
  Log-Success '========================================'
  Log-Success 'Package created successfully!'
  Log-Success "Output directory: $BUILD_OUTPUT_DIR"
  exit 0
}

# Build main project if requested
if ($Build -or $Rebuild) {
  $current = Detect-Platform
  $target = if ($Platform -eq 'Current') { $current } else { $Platform }

  switch ($target) {
    'Windows' { Build-WindowsPackage }
    'macOS' { Build-macOSPackage }
    'All' { Build-WindowsPackage; Build-macOSPackage }
    Default { Log-Error "Unknown platform: $target"; Show-Usage; exit 1 }
  }
}

# Create package if requested
if ($Package) {
  Log-Info '========================================'
  Create-Package
}

# Run tests if requested (after build operations)
if ($Test) {
  Log-Info '========================================'
  Log-Info 'Running tests after build...'
  Run-Tests
}

Log-Success '========================================'
Log-Success 'Build completed successfully!'
Log-Success "Output directory: $BUILD_OUTPUT_DIR"
if (Test-Path $BUILD_OUTPUT_DIR) {
  Log-Info 'Contents:'
  Get-ChildItem -Path $BUILD_OUTPUT_DIR -Recurse | ForEach-Object { Write-Host "  $($_.FullName)" }
}

