#!/bin/bash

# AGI Kapster macOS PKG creation script
# Usage: ./create-pkg.sh <publish_directory> <version> [sign_identity]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$1"
VERSION="$2"
SIGN_IDENTITY="$3"

if [ -z "$PUBLISH_DIR" ] || [ -z "$VERSION" ]; then
    echo "Usage: $0 <publish_directory> <version> [sign_identity]"
    echo "Example: $0 ../artifacts/publish/osx-x64 1.2.0 'Developer ID Application: Your Name'"
    exit 1
fi

# Validate input directory
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Publish directory does not exist: $PUBLISH_DIR"
    exit 1
fi

# Configuration
APP_NAME="AGI Kapster"
BUNDLE_ID="com.agi.Kapster"
# Get architecture from environment or detect from path
ARCH=""
if [[ "$PUBLISH_DIR" == *"osx-arm64"* ]]; then
    ARCH="osx-arm64"
elif [[ "$PUBLISH_DIR" == *"osx-x64"* ]]; then
    ARCH="osx-x64"
else
    # Fallback: detect from system
    ARCH=$(uname -m)
    if [[ "$ARCH" == "arm64" ]]; then
        ARCH="osx-arm64"
    else
        ARCH="osx-x64"
    fi
fi
PKG_NAME="AGI.Kapster-${VERSION}-${ARCH}.pkg"
TEMP_DIR="$(mktemp -d)"
APP_DIR="$TEMP_DIR/$APP_NAME.app"

echo "🔨 Creating macOS application package..."

# Create .app structure
echo "Creating .app structure..."
mkdir -p "$APP_DIR/Contents/MacOS"
mkdir -p "$APP_DIR/Contents/Resources"

echo "Publish directory contents:"
ls -la "$PUBLISH_DIR" || true

# Copy publish output into app bundle
echo "Copying publish output..."
if [ ! -f "$PUBLISH_DIR/AGI.Kapster.Desktop" ]; then
    echo "❌ Executable not found: $PUBLISH_DIR/AGI.Kapster.Desktop"
    exit 1
fi

# Copy everything (including subdirectories) into Contents/MacOS
cp -R "$PUBLISH_DIR"/. "$APP_DIR/Contents/MacOS/" || {
    echo "❌ Failed to copy files from $PUBLISH_DIR"
    exit 1
}

# Ensure executable bit on main binary
chmod +x "$APP_DIR/Contents/MacOS/AGI.Kapster.Desktop"

# Create Info.plist
cat > "$APP_DIR/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleExecutable</key>
    <string>AGI.Kapster.Desktop</string>
    <key>CFBundleIconFile</key>
    <string>logo.icns</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSUIElement</key>
    <true/>
    <key>NSRequiresAquaSystemAppearance</key>
    <false/>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.graphics-design</string>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2025 AGI Build. All rights reserved.</string>
</dict>
</plist>
EOF

# Copy icon (.icns is required)
if [ -f "$SCRIPT_DIR/../../src/AGI.Kapster.Desktop/logo.icns" ]; then
    cp "$SCRIPT_DIR/../../src/AGI.Kapster.Desktop/logo.icns" "$APP_DIR/Contents/Resources/"
else
    echo "❌ Missing logo.icns. Please generate it at src/AGI.Kapster.Desktop/logo.icns before packaging."
    exit 1
fi

# Application signing (if signing identity provided)
if [ -n "$SIGN_IDENTITY" ]; then
    echo "🔐 Signing application..."
    ENTITLEMENTS="$SCRIPT_DIR/entitlements.plist"

    # Sign native libraries first (must be done before main executable)
    echo "Signing native libraries..."
    find "$APP_DIR/Contents/MacOS" -name "*.dylib" -type f | while read dylib; do
        echo "  Signing: $dylib"
        # Native libraries should not receive app entitlements.
        codesign --force --options runtime --timestamp --sign "$SIGN_IDENTITY" "$dylib"
    done

    # Sign any other native libraries (.so, .dll just in case)
    find "$APP_DIR/Contents/MacOS" -name "*.so" -type f | while read lib; do
        echo "  Signing: $lib"
        # Native libraries should not receive app entitlements.
        codesign --force --options runtime --timestamp --sign "$SIGN_IDENTITY" "$lib"
    done

    # Sign the main executable
    echo "Signing main executable..."
    codesign --force --options runtime --timestamp --sign "$SIGN_IDENTITY" \
        --entitlements "$ENTITLEMENTS" "$APP_DIR/Contents/MacOS/AGI.Kapster.Desktop"

    # Sign the entire app bundle
    echo "Signing app bundle..."
    codesign --force --deep --options runtime --timestamp --sign "$SIGN_IDENTITY" \
        --entitlements "$ENTITLEMENTS" "$APP_DIR"

    # Verify signature
    echo "Verifying signature..."
    codesign --verify --verbose=2 "$APP_DIR"
fi

echo "📦 Creating PKG installer..."

# Create PKG
echo "Creating PKG with pkgbuild..."
echo "Root directory: $TEMP_DIR"
echo "Contents:"
ls -la "$TEMP_DIR" || true
echo "App contents:"
ls -la "$APP_DIR" || true

echo "PKG will be created at: $SCRIPT_DIR/$PKG_NAME"

# Ensure the script directory exists
mkdir -p "$SCRIPT_DIR"

pkgbuild --root "$TEMP_DIR" \
         --identifier "$BUNDLE_ID" \
         --version "$VERSION" \
         --install-location "/Applications" \
         "$SCRIPT_DIR/$PKG_NAME" 2>&1 || {
    echo "❌ pkgbuild failed with exit code $?"
    echo "TEMP_DIR contents:"
    find "$TEMP_DIR" -type f -exec ls -la {} \; || true
    echo "Script directory contents:"
    ls -la "$SCRIPT_DIR" || true
    exit 1
}

# Verify PKG was created
if [ ! -f "$SCRIPT_DIR/$PKG_NAME" ]; then
    echo "❌ PKG file was not created: $SCRIPT_DIR/$PKG_NAME"
    echo "Script directory contents:"
    ls -la "$SCRIPT_DIR" || true
    exit 1
fi

echo "✅ PKG file created successfully: $SCRIPT_DIR/$PKG_NAME"

# Sign PKG if signing identity provided
if [ -n "$SIGN_IDENTITY" ]; then
    echo "🔐 Signing PKG..."
    productsign --sign "$SIGN_IDENTITY" "$SCRIPT_DIR/$PKG_NAME" "$SCRIPT_DIR/${PKG_NAME%.pkg}-signed.pkg"
    mv "$SCRIPT_DIR/${PKG_NAME%.pkg}-signed.pkg" "$SCRIPT_DIR/$PKG_NAME"
fi

echo "✅ macOS PKG installer created:"
echo "  📦 PKG: $SCRIPT_DIR/$PKG_NAME"

# Show signing status
if [ -n "$SIGN_IDENTITY" ]; then
    echo "🔐 Verifying signature:"
    codesign --verify --verbose=2 "$SCRIPT_DIR/$PKG_NAME" 2>&1 || true
fi