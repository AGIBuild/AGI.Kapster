#!/bin/bash

# Cross-platform build script for AGI.Captor.Desktop
# Supports Windows (.exe + installer) and macOS (.app bundle)

set -e

# Configuration
PROJECT_NAME="AGI.Captor.Desktop"
APP_NAME="AGI Captor"
BUNDLE_ID="com.agi.captor"
VERSION="1.0.0"
PROJECT_DIR="src/AGI.Captor.Desktop"
OUTPUT_DIR="dist"
CONFIGURATION="Debug"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Helper functions
log_info() {
    echo -e "${CYAN}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Detect platform
detect_platform() {
    case "$(uname -s)" in
        Darwin*)    echo "macOS" ;;
        Linux*)     echo "Linux" ;;
        CYGWIN*|MINGW*|MSYS*) echo "Windows" ;;
        *)          echo "Unknown" ;;
    esac
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    if ! command -v dotnet &> /dev/null; then
        log_error ".NET SDK is not installed or not in PATH"
        exit 1
    fi
    
    local dotnet_version=$(dotnet --version)
    log_success ".NET SDK version: $dotnet_version"
    
    local platform=$(detect_platform)
    log_success "Platform detected: $platform"
}

# Clean function
clean_build() {
    log_info "Cleaning previous builds..."
    
    if [ -d "$OUTPUT_DIR" ]; then
        rm -rf "$OUTPUT_DIR"
    fi
    
    dotnet clean "$PROJECT_DIR"
    
    # Clean bin and obj directories
    find "$PROJECT_DIR" -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + 2>/dev/null || true
    
    log_success "Clean completed"
}

# Build .NET project
build_dotnet_project() {
    local runtime_id=$1
    log_info "Building .NET project for $runtime_id..."
    
    dotnet publish "$PROJECT_DIR" \
        -c "$CONFIGURATION" \
        -r "$runtime_id" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=false \
        -o "$PROJECT_DIR/bin/$CONFIGURATION/net9.0/$runtime_id/publish"
    
    log_success "Build completed for $runtime_id"
}

# Build Windows package
build_windows_package() {
    log_info "Building Windows package..."
    
    local runtime_id="win-x64"
    build_dotnet_project "$runtime_id"
    
    local publish_dir="$PROJECT_DIR/bin/$CONFIGURATION/net9.0/$runtime_id/publish"
    local windows_dir="$OUTPUT_DIR/Windows"
    
    # Create output directory
    mkdir -p "$windows_dir"
    
    # Copy published files
    cp -r "$publish_dir"/* "$windows_dir/"
    
    # Rename executable
    if [ -f "$windows_dir/$PROJECT_NAME.exe" ]; then
        mv "$windows_dir/$PROJECT_NAME.exe" "$windows_dir/$APP_NAME.exe"
    fi
    
    log_success "Windows package created at: $windows_dir"
    
    # Create installer script
    cat > "$windows_dir/Install.ps1" << 'EOF'
# Windows Installer Script for AGI Captor
param(
    [string]$InstallDir = "$env:ProgramFiles\AGI Captor"
)

$AppName = "AGI Captor"
$SourceDir = $PSScriptRoot

Write-Host "Installing $AppName to $InstallDir..."

# Create install directory
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
}

# Copy files
Copy-Item -Path "$SourceDir\*" -Destination $InstallDir -Recurse -Force -Exclude "Install.ps1"

# Create desktop shortcut
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\$AppName.lnk")
$Shortcut.TargetPath = "$InstallDir\$AppName.exe"
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Save()

Write-Host "$AppName installed successfully!"
Write-Host "Desktop shortcut created."
EOF
    
    log_success "Windows installer script created: $windows_dir/Install.ps1"
}

# Build macOS package
build_macos_package() {
    log_info "Building macOS package..."
    
    local runtime_id="osx-x64"
    build_dotnet_project "$runtime_id"
    
    local publish_dir="$PROJECT_DIR/bin/$CONFIGURATION/net9.0/$runtime_id/publish"
    local macos_dir="$OUTPUT_DIR/macOS"
    local app_bundle="$macos_dir/$APP_NAME.app"
    
    # Create output directory
    mkdir -p "$macos_dir"
    
    # Create app bundle structure
    mkdir -p "$app_bundle/Contents/MacOS"
    mkdir -p "$app_bundle/Contents/Resources"
    
    # Copy executable and dependencies
    cp -r "$publish_dir"/* "$app_bundle/Contents/MacOS/"
    
    # Rename executable
    if [ -f "$app_bundle/Contents/MacOS/$PROJECT_NAME" ]; then
        mv "$app_bundle/Contents/MacOS/$PROJECT_NAME" "$app_bundle/Contents/MacOS/$APP_NAME"
    fi
    
    # Copy resources
    if [ -f "$publish_dir/logo.ico" ]; then
        cp "$publish_dir/logo.ico" "$app_bundle/Contents/Resources/"
    fi
    
    # Create Info.plist
    cat > "$app_bundle/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleExecutable</key>
    <string>$APP_NAME</string>
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
EOF
    
    # Make executable
    chmod +x "$app_bundle/Contents/MacOS/$APP_NAME"
    
    log_success "macOS app bundle created at: $app_bundle"
    
    # Create DMG creation script
    cat > "$macos_dir/create-dmg.sh" << 'EOF'
#!/bin/bash
# Create DMG for AGI Captor

APP_NAME="AGI Captor"
VERSION="1.0.0"
SOURCE_DIR="$(dirname "$0")"
DMG_NAME="${APP_NAME}-${VERSION}.dmg"

echo "Creating DMG: $DMG_NAME"

# Create temporary DMG
hdiutil create -size 100m -fs HFS+ -volname "$APP_NAME" -srcfolder "$SOURCE_DIR" "$DMG_NAME.temp.dmg"

# Convert to compressed DMG
hdiutil convert "$DMG_NAME.temp.dmg" -format UDZO -o "$DMG_NAME"

# Clean up
rm "$DMG_NAME.temp.dmg"

echo "DMG created: $DMG_NAME"
EOF
    
    chmod +x "$macos_dir/create-dmg.sh"
    log_success "DMG creation script created: $macos_dir/create-dmg.sh"
}

# Show usage
show_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -p, --platform PLATFORM    Target platform (Windows, macOS, All, Current) [default: Current]"
    echo "  -c, --config CONFIG         Build configuration (Debug, Release) [default: Debug]"
    echo "  --no-clean                  Skip clean before build (clean is default)"
    echo "  -h, --help                  Show this help message"
    echo ""
    echo "Short forms:"
    echo "  -p win     = --platform Windows"
    echo "  -p mac     = --platform macOS"
    echo "  -p all     = --platform All"
    echo "  -c rel     = --config Release"
    echo "  -c dbg     = --config Debug"
    echo ""
    echo "Environment Configuration:"
    echo "  Debug builds automatically use appsettings.dev.json (Debug level logging)"
    echo "  Release builds automatically use appsettings.prod.json (Warning level logging)"
    echo ""
    echo "Examples:"
    echo "  $0                          # Build current platform, Debug (dev environment)"
    echo "  $0 -p mac                   # Build macOS, Debug (dev environment)"
    echo "  $0 -p all -c rel            # Build all platforms, Release (prod environment)"
    echo "  $0 -p win --no-clean        # Build Windows, Debug, without clean"
}

# Parse command line arguments
PLATFORM="Current"
CLEAN=true  # Default to clean

# Helper function to expand short forms
expand_platform() {
    case "$1" in
        "win"|"windows") echo "Windows" ;;
        "mac"|"macos") echo "macOS" ;;
        "all") echo "All" ;;
        "current"|"cur") echo "Current" ;;
        *) echo "$1" ;;
    esac
}

expand_config() {
    case "$1" in
        "rel"|"release") echo "Release" ;;
        "dbg"|"debug") echo "Debug" ;;
        *) echo "$1" ;;
    esac
}

while [[ $# -gt 0 ]]; do
    case $1 in
        -p|--platform)
            PLATFORM=$(expand_platform "$2")
            shift 2
            ;;
        -c|--config|--configuration)
            CONFIGURATION=$(expand_config "$2")
            shift 2
            ;;
        --no-clean)
            CLEAN=false
            shift
            ;;
        -h|--help)
            show_usage
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Main execution
main() {
    log_info "AGI.Captor.Desktop Build Script"
    log_info "Platform: $PLATFORM | Configuration: $CONFIGURATION"
    if [ "$CONFIGURATION" = "Debug" ]; then
        log_info "Environment: dev (Debug level logging)"
    else
        log_info "Environment: prod (Warning level logging)"
    fi
    log_info "========================================"
    
    check_prerequisites
    
    if [ "$CLEAN" = true ]; then
        clean_build
    fi
    
    # Create output directory
    mkdir -p "$OUTPUT_DIR"
    
    # Determine target platform
    local current_platform=$(detect_platform)
    local target_platform="$PLATFORM"
    
    if [ "$PLATFORM" = "Current" ]; then
        target_platform="$current_platform"
    fi
    
    case "$target_platform" in
        "Windows")
            build_windows_package
            ;;
        "macOS")
            build_macos_package
            ;;
        "All")
            build_windows_package
            build_macos_package
            ;;
        *)
            log_error "Unknown platform: $target_platform"
            log_info "Available platforms: Windows, macOS, All, Current"
            exit 1
            ;;
    esac
    
    log_success "========================================"
    log_success "Build completed successfully!"
    log_success "Output directory: $OUTPUT_DIR"
    
    if [ -d "$OUTPUT_DIR" ]; then
        log_info "Contents:"
        find "$OUTPUT_DIR" -type f | sed 's|^|  |'
    fi
}

# Run main function
main "$@"
