#!/bin/bash

# Cross-platform build script for AGI.Captor.Desktop
# Supports Windows (.exe) and macOS (.app bundle)

set -e

# Configuration
PROJECT_NAME="AGI.Captor.Desktop"
APP_NAME="AGI Captor"
BUNDLE_ID="com.agi.captor"
APP_VERSION="${VERSION:-1.0.0}"
PROJECT_DIR="src/AGI.Captor.Desktop"
TEST_PROJECT_DIR="tests/AGI.Captor.Tests"
BUILD_OUTPUT_DIR="dist"
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

# Get runtime identifier
get_runtime_identifier() {
    local platform=$(detect_platform)
    case "$platform" in
        "Windows")  echo "win-x64" ;;
        "macOS")    echo "osx-x64" ;;
        "Linux")    echo "linux-x64" ;;
        *)          echo "unknown" ;;
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
    
    if [ -d "$BUILD_OUTPUT_DIR" ]; then
        rm -rf "$BUILD_OUTPUT_DIR"
    fi
    
    dotnet clean "$PROJECT_DIR"
    dotnet clean "$TEST_PROJECT_DIR"
    
    # Clean bin and obj directories
    find "$PROJECT_DIR" -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + 2>/dev/null || true
    find "$TEST_PROJECT_DIR" -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + 2>/dev/null || true
    
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

# Build test project
build_test_project() {
    log_info "Building test project..."
    
    dotnet build "$TEST_PROJECT_DIR" -c "$CONFIGURATION" --verbosity minimal
    
    if [ $? -ne 0 ]; then
        log_error "Test project build failed"
        exit 1
    fi
    
    log_success "Test project build completed"
}

# Run tests
run_tests() {
    log_info "Running unit tests..."
    
    local test_command="dotnet test $TEST_PROJECT_DIR -c $CONFIGURATION --verbosity normal --logger \"console;verbosity=detailed\""
    
    if [ "$COVERAGE" = true ]; then
        test_command="$test_command --collect:\"XPlat Code Coverage\" --results-directory TestResults"
        log_info "Code coverage collection enabled"
    fi
    
    if [ -n "$TEST_FILTER" ]; then
        test_command="$test_command --filter \"$TEST_FILTER\""
        log_info "Test filter: $TEST_FILTER"
    fi
    
    eval $test_command
    
    if [ $? -ne 0 ]; then
        log_error "Tests failed"
        exit 1
    fi
    
    log_success "All tests passed"
    
    if [ "$COVERAGE" = true ]; then
        generate_coverage_report
    fi
}

# Run tests only (no build)
run_tests_only() {
    log_info "Running tests only (no build)..."
    
    local test_command="dotnet test $TEST_PROJECT_DIR -c $CONFIGURATION --verbosity normal --logger \"console;verbosity=detailed\" --no-build"
    
    if [ "$COVERAGE" = true ]; then
        test_command="$test_command --collect:\"XPlat Code Coverage\" --results-directory TestResults"
        log_info "Code coverage collection enabled"
    fi
    
    if [ -n "$TEST_FILTER" ]; then
        test_command="$test_command --filter \"$TEST_FILTER\""
        log_info "Test filter: $TEST_FILTER"
    fi
    
    eval $test_command
    
    if [ $? -ne 0 ]; then
        log_error "Tests failed"
        exit 1
    fi
    
    log_success "All tests passed"
    
    if [ "$COVERAGE" = true ]; then
        generate_coverage_report
    fi
}

# Run application
run_application() {
    log_info "Running application..."
    
    local current=$(detect_platform)
    local rid=$(get_runtime_identifier)
    
    if [ "$rid" = "unknown" ]; then
        log_error "Unsupported platform for running: $current"
        exit 1
    fi
    
    # Determine executable extension
    local exe_extension=""
    if [ "$current" = "Windows" ]; then
        exe_extension=".exe"
    fi
    
    local exe_path="$PROJECT_DIR/bin/$CONFIGURATION/net9.0/$rid/$PROJECT_NAME$exe_extension"
    
    if [ ! -f "$exe_path" ]; then
        log_error "Application not found at: $exe_path"
        log_info "Please build the application first using: ./build.sh --build"
        exit 1
    fi
    
    # Resolve to absolute path and validate
    local abs_exe_path
    abs_exe_path=$(realpath "$exe_path")
    if [[ "$abs_exe_path" != "$(realpath "$PROJECT_DIR")"* ]]; then
        log_error "Executable path is outside the project directory: $abs_exe_path"
        exit 1
    fi
    
    log_info "Starting $current application: $abs_exe_path"
    "$abs_exe_path"
    
    log_success "Application started successfully"
}

# Generate coverage report
generate_coverage_report() {
    log_info "Generating coverage report..."
    
    # Find the latest coverage file
    local coverage_files=$(find TestResults -name "coverage.cobertura.xml" 2>/dev/null | head -1)
    if [ -z "$coverage_files" ]; then
        log_warning "No coverage files found in TestResults directory"
        return
    fi
    
    log_info "Found coverage file: $coverage_files"
    
    # Create a simple summary from the XML
    if command -v python3 >/dev/null 2>&1; then
        # Use Python to parse XML
        local line_rate=$(python3 -c "
import xml.etree.ElementTree as ET
try:
    tree = ET.parse('$coverage_files')
    root = tree.getroot()
    line_rate = float(root.get('line-rate', 0)) * 100
    branch_rate = float(root.get('branch-rate', 0)) * 100
    print(f'{line_rate:.2f},{branch_rate:.2f}')
except Exception as e:
    print('0.00,0.00')
")
        local line_coverage=$(echo "$line_rate" | cut -d',' -f1)
        local branch_coverage=$(echo "$line_rate" | cut -d',' -f2)
        
        log_success "Code Coverage Summary:"
        log_success "  Line Coverage: ${line_coverage}%"
        log_success "  Branch Coverage: ${branch_coverage}%"
        log_success "  Coverage Report: $coverage_files"
        
        if [ "$OPEN_REPORT" = true ]; then
            log_info "Opening coverage file..."
            if command -v xdg-open >/dev/null 2>&1; then
                xdg-open "$coverage_files"
            elif command -v open >/dev/null 2>&1; then
                open "$coverage_files"
            else
                log_warning "Cannot open coverage file automatically on this platform"
            fi
        fi
    else
        log_warning "Python3 not found, cannot parse coverage XML"
        log_info "Coverage data available in: $coverage_files"
    fi
}

# Build Windows package
build_windows_package() {
    log_info "Building Windows package..."
    
    local runtime_id="win-x64"
    build_dotnet_project "$runtime_id"
    
    local publish_dir="$PROJECT_DIR/bin/$CONFIGURATION/net9.0/$runtime_id/publish"
    local windows_dir="$BUILD_OUTPUT_DIR/Windows"
    
    # Create output directory
    mkdir -p "$windows_dir"
    
    # Copy published files
    cp -r "$publish_dir"/* "$windows_dir/"
    
    # Rename executable
    if [ -f "$windows_dir/$PROJECT_NAME.exe" ]; then
        mv "$windows_dir/$PROJECT_NAME.exe" "$windows_dir/$APP_NAME.exe"
    fi
    
    log_success "Windows package created at: $windows_dir"
    
}

# Build macOS package
build_macos_package() {
    log_info "Building macOS package..."
    
    local runtime_id="osx-x64"
    build_dotnet_project "$runtime_id"
    
    local publish_dir="$PROJECT_DIR/bin/$CONFIGURATION/net9.0/$runtime_id/publish"
    local macos_dir="$BUILD_OUTPUT_DIR/macOS"
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
    <string>$APP_VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$APP_VERSION</string>
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
    echo "  --build                     Build project"
    echo "  --rebuild                   Clean and rebuild project"
    echo "  --clean                     Clean build artifacts only"
    echo "  --test                      Run tests only (no build)"
    echo "  --run                       Run the application"
    echo "  --coverage                  Enable code coverage collection"
    echo "  --open-report               Open coverage report after generation"
    echo "  --test-filter FILTER        Test filter expression"
    echo "  --version                   Show application version"
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
    echo "  $0 --build                  # Build current platform, Debug (dev environment)"
    echo "  $0 --test                   # Run tests only"
    echo "  $0 --run                    # Run the application"
    echo "  $0 --test --coverage        # Run tests with code coverage"
    echo "  $0 --test --coverage --open-report  # Run tests with coverage and open report"
    echo "  $0 --clean                  # Clean build artifacts"
    echo "  $0 --rebuild                # Clean and rebuild"
    echo "  $0 -p mac --build           # Build macOS, Debug (dev environment)"
    echo "  $0 -p all -c rel --build    # Build all platforms, Release (prod environment)"
}

# Parse command line arguments
PLATFORM="Current"
BUILD=false
REBUILD=false
CLEAN=false
TEST=false
RUN=false
COVERAGE=false
OPEN_REPORT=false
TEST_FILTER=""
SHOW_VERSION=false

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

# Get project version from csproj file
get_project_version() {
    local csproj_path="$PROJECT_DIR/$PROJECT_NAME.csproj"
    if [ -f "$csproj_path" ]; then
        if command -v xmllint >/dev/null 2>&1; then
            # Try to find Version tag first using xmllint
            local version=$(xmllint --xpath "string(//Version)" "$csproj_path" 2>/dev/null)
            if [ -n "$version" ]; then
                echo "$version"
            else
                # Fallback to CFBundleVersion for macOS projects
                version=$(xmllint --xpath "string(//CFBundleVersion)" "$csproj_path" 2>/dev/null)
                if [ -n "$version" ]; then
                    echo "$version"
                else
                    echo "1.0.0"
                fi
            fi
        else
            # Fallback to grep/sed if xmllint is not available, with warning
            echo -e "${YELLOW}Warning: xmllint not found, falling back to grep/sed for XML parsing.${NC}" >&2
            local version=$(grep -o '<Version>.*</Version>' "$csproj_path" | sed 's/<Version>\(.*\)<\/Version>/\1/')
            if [ -n "$version" ]; then
                echo "$version"
            else
                version=$(grep -o '<CFBundleVersion>.*</CFBundleVersion>' "$csproj_path" | sed 's/<CFBundleVersion>\(.*\)<\/CFBundleVersion>/\1/')
                if [ -n "$version" ]; then
                    echo "$version"
                else
                    echo "1.0.0"
                fi
            fi
        fi
    else
        echo "1.0.0"
    fi
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
        --build)
            BUILD=true
            shift
            ;;
        --rebuild)
            REBUILD=true
            shift
            ;;
        --clean)
            CLEAN=true
            shift
            ;;
        --test)
            TEST=true
            shift
            ;;
        --run)
            RUN=true
            shift
            ;;
        --coverage)
            COVERAGE=true
            shift
            ;;
        --open-report)
            OPEN_REPORT=true
            shift
            ;;
        --test-filter)
            TEST_FILTER="$2"
            shift 2
            ;;
        --version)
            SHOW_VERSION=true
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
    # Handle version display
    if [ "$SHOW_VERSION" = true ]; then
        local project_version=$(get_project_version)
        echo -e "${GREEN}AGI.Captor Version: $project_version${NC}"
        exit 0
    fi
    
    log_info "AGI.Captor.Desktop Build Script"
    log_info "Platform: $PLATFORM | Configuration: $CONFIGURATION"
    if [ "$CONFIGURATION" = "Debug" ]; then
        log_info "Environment: dev (Debug level logging)"
    else
        log_info "Environment: prod (Warning level logging)"
    fi
    log_info "========================================"
    
    check_prerequisites
    
    # Handle clean-only mode (only if no other operations are specified)
    if [ "$CLEAN" = true ] && [ "$BUILD" = false ] && [ "$REBUILD" = false ] && [ "$TEST" = false ] && [ "$PACKAGE" = false ]; then
        log_info "Clean mode: Cleaning build artifacts only"
        clean_build
        log_success "========================================"
        log_success "Clean completed!"
        exit 0
    fi
    
    # Handle test-only mode (only if no build operations are specified)
    if [ "$TEST" = true ] && [ "$BUILD" = false ] && [ "$REBUILD" = false ] && [ "$PACKAGE" = false ] && [ "$RUN" = false ]; then
        log_info "Test-only mode: Running tests without build"
        run_tests_only
        log_success "========================================"
        log_success "Tests completed!"
        exit 0
    fi
    
    # Handle run-only mode (only if no other operations are specified)
    if [ "$RUN" = true ] && [ "$BUILD" = false ] && [ "$REBUILD" = false ] && [ "$TEST" = false ] && [ "$PACKAGE" = false ]; then
        log_info "Run mode: Starting application"
        run_application
        log_success "========================================"
        log_success "Application started!"
        exit 0
    fi
    
    # Handle rebuild mode
    if [ "$REBUILD" = true ]; then
        log_info "Rebuild mode: Clean and build"
        clean_build
    fi
    
    
    # Create output directory if building
    if [ "$BUILD" = true ] || [ "$REBUILD" = true ]; then
        mkdir -p "$BUILD_OUTPUT_DIR"
        
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
    fi
    
    # Run tests if requested (after build operations)
    if [ "$TEST" = true ]; then
        log_info "========================================"
        log_info "Running tests after build..."
        run_tests
    fi
    
    log_success "========================================"
    log_success "Build completed successfully!"
    log_success "Output directory: $BUILD_OUTPUT_DIR"
    
    if [ -d "$BUILD_OUTPUT_DIR" ]; then
        log_info "Contents:"
        find "$BUILD_OUTPUT_DIR" -type f | sed 's|^|  |'
    fi
}

# Run main function
main "$@"
