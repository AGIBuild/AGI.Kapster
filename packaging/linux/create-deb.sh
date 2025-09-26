#!/bin/bash

# AGI Kapster Linux DEB package creation script
# Usage: ./create-deb.sh <publish_directory> <version> <architecture>

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$1"
VERSION="$2"
ARCH="${3:-amd64}"

if [ -z "$PUBLISH_DIR" ] || [ -z "$VERSION" ]; then
    echo "Usage: $0 <publish_directory> <version> [architecture]"
    echo "Example: $0 ../artifacts/publish/linux-x64 1.2.0 amd64"
    exit 1
fi

# Validate input directory
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Publish directory does not exist: $PUBLISH_DIR"
    exit 1
fi

# Configuration
PACKAGE_NAME="agi-kapster"
DEB_NAME="${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"
TEMP_DIR="$(mktemp -d)"
DEB_DIR="$TEMP_DIR/deb"

echo "🔨 Creating DEB package structure..."

# Create DEB directory structure
{
  mkdir -p "$DEB_DIR/DEBIAN"
  mkdir -p "$DEB_DIR/usr/bin"
  mkdir -p "$DEB_DIR/usr/share/applications"
  mkdir -p "$DEB_DIR/usr/share/pixmaps"
  mkdir -p "$DEB_DIR/usr/share/$PACKAGE_NAME"
  mkdir -p "$DEB_DIR/usr/share/doc/$PACKAGE_NAME"
  
  # Copy application files
  cp -r "$PUBLISH_DIR"/* "$DEB_DIR/usr/share/$PACKAGE_NAME/"
} >/dev/null 2>&1 || true
chmod +x "$DEB_DIR/usr/share/$PACKAGE_NAME/AGI.Kapster.Desktop"

# Create launcher script
cat > "$DEB_DIR/usr/bin/agi-kapster" << 'EOF'
#!/bin/bash
exec /usr/share/agi-kapster/AGI.Kapster.Desktop "$@"
EOF
chmod +x "$DEB_DIR/usr/bin/agi-kapster"

# Create desktop file
cat > "$DEB_DIR/usr/share/applications/agi-kapster.desktop" << EOF
[Desktop Entry]
Name=AGI Kapster
Comment=Advanced Screenshot and Annotation Tool
GenericName=Screenshot Tool
Exec=agi-kapster %F
Icon=agi-kapster
Terminal=false
Type=Application
Categories=Graphics;Photography;
StartupNotify=true
MimeType=image/png;image/jpeg;image/bmp;image/tiff;
Keywords=screenshot;annotation;capture;
EOF

# Copy icon (if exists)
if [ -f "$SCRIPT_DIR/../../src/AGI.Kapster.Desktop/logo.ico" ]; then
    cp "$SCRIPT_DIR/../../src/AGI.Kapster.Desktop/logo.ico" "$DEB_DIR/usr/share/pixmaps/agi-kapster.png"
fi

# Create control file
cat > "$DEB_DIR/DEBIAN/control" << EOF
Package: $PACKAGE_NAME
Version: $VERSION
Section: graphics
Priority: optional
Architecture: $ARCH
Depends: libc6, libgcc-s1, libssl3, zlib1g
Maintainer: AGI Build <support@agibuild.com>
Description: Advanced Screenshot and Annotation Tool
 AGI Kapster is a powerful cross-platform screenshot and annotation tool
 built with modern .NET technology. It provides intuitive tools for
 capturing, annotating, and sharing screenshots with professional quality.
 .
 Features include:
  * Full-screen and region capture
  * Rich annotation tools (text, arrows, shapes, highlights)
  * Multiple export formats
  * Customizable hotkeys
  * Cross-platform compatibility
Homepage: https://github.com/AGIBuild/AGI.Kapster
EOF

# Calculate installed size
INSTALLED_SIZE=$(du -s "$DEB_DIR/usr" | cut -f1)
echo "Installed-Size: $INSTALLED_SIZE" >> "$DEB_DIR/DEBIAN/control"

# Create postinst script
cat > "$DEB_DIR/DEBIAN/postinst" << 'EOF'
#!/bin/bash
set -e

# Update desktop database
if [ -x /usr/bin/update-desktop-database ]; then
    update-desktop-database /usr/share/applications
fi

# Update MIME database
if [ -x /usr/bin/update-mime-database ]; then
    update-mime-database /usr/share/mime
fi

exit 0
EOF
chmod +x "$DEB_DIR/DEBIAN/postinst"

# Create prerm script
cat > "$DEB_DIR/DEBIAN/prerm" << 'EOF'
#!/bin/bash
set -e

# Add cleanup work before uninstall here

exit 0
EOF
chmod +x "$DEB_DIR/DEBIAN/prerm"

# Create postrm script
cat > "$DEB_DIR/DEBIAN/postrm" << 'EOF'
#!/bin/bash
set -e

# Update desktop database
if [ -x /usr/bin/update-desktop-database ]; then
    update-desktop-database /usr/share/applications
fi

# Update MIME database
if [ -x /usr/bin/update-mime-database ]; then
    update-mime-database /usr/share/mime
fi

exit 0
EOF
chmod +x "$DEB_DIR/DEBIAN/postrm"

# Create copyright file
cat > "$DEB_DIR/usr/share/doc/$PACKAGE_NAME/copyright" << EOF
Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: AGI Kapster
Upstream-Contact: AGI Build <support@agibuild.com>
Source: https://github.com/AGIBuild/AGI.Kapster

Files: *
Copyright: 2025 AGI Build
License: MIT
 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:
 .
 The above copyright notice and this permission notice shall be included in all
 copies or substantial portions of the Software.
 .
 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 SOFTWARE.
EOF

# Create changelog
cat > "$DEB_DIR/usr/share/doc/$PACKAGE_NAME/changelog.Debian" << EOF
$PACKAGE_NAME ($VERSION) unstable; urgency=medium

  * Initial release of AGI Kapster
  * Cross-platform screenshot and annotation tool
  * Built with .NET and Avalonia UI

 -- AGI Build <support@agibuild.com>  $(date -R)
EOF
gzip -9 "$DEB_DIR/usr/share/doc/$PACKAGE_NAME/changelog.Debian"

echo "📦 Building DEB package..."

# Build DEB package
fakeroot dpkg-deb --build "$DEB_DIR" "$SCRIPT_DIR/$DEB_NAME"

# Verify package
echo "🔍 Verifying DEB package..."
dpkg-deb --info "$SCRIPT_DIR/$DEB_NAME" >/dev/null 2>&1
dpkg-deb --contents "$SCRIPT_DIR/$DEB_NAME" >/dev/null 2>&1

# Clean up temporary files
rm -rf "$TEMP_DIR"

echo "✅ DEB package created: $SCRIPT_DIR/$DEB_NAME"