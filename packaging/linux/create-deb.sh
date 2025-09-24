#!/bin/bash

# AGI Captor Linux DEB包创建脚本
# 用法: ./create-deb.sh <publish_directory> <version> <architecture>

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$1"
VERSION="$2"
ARCH="${3:-amd64}"

if [ -z "$PUBLISH_DIR" ] || [ -z "$VERSION" ]; then
    echo "用法: $0 <publish_directory> <version> [architecture]"
    echo "示例: $0 ../artifacts/publish/linux-x64 1.2.0 amd64"
    exit 1
fi

# 验证输入目录
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "错误: 发布目录不存在: $PUBLISH_DIR"
    exit 1
fi

# 配置
PACKAGE_NAME="agi-captor"
DEB_NAME="${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"
TEMP_DIR="$(mktemp -d)"
DEB_DIR="$TEMP_DIR/deb"

echo "🔨 创建 DEB 包结构..."

# 创建DEB目录结构
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
chmod +x "$DEB_DIR/usr/share/$PACKAGE_NAME/AGI.Captor.Desktop"

# 创建启动脚本
cat > "$DEB_DIR/usr/bin/agi-captor" << 'EOF'
#!/bin/bash
exec /usr/share/agi-captor/AGI.Captor.Desktop "$@"
EOF
chmod +x "$DEB_DIR/usr/bin/agi-captor"

# 创建desktop文件
cat > "$DEB_DIR/usr/share/applications/agi-captor.desktop" << EOF
[Desktop Entry]
Name=AGI Captor
Comment=Advanced Screenshot and Annotation Tool
GenericName=Screenshot Tool
Exec=agi-captor %F
Icon=agi-captor
Terminal=false
Type=Application
Categories=Graphics;Photography;
StartupNotify=true
MimeType=image/png;image/jpeg;image/bmp;image/tiff;
Keywords=screenshot;annotation;capture;
EOF

# 复制图标（如果存在）
if [ -f "$SCRIPT_DIR/../../src/AGI.Captor.Desktop/logo.ico" ]; then
    cp "$SCRIPT_DIR/../../src/AGI.Captor.Desktop/logo.ico" "$DEB_DIR/usr/share/pixmaps/agi-captor.png"
fi

# 创建control文件
cat > "$DEB_DIR/DEBIAN/control" << EOF
Package: $PACKAGE_NAME
Version: $VERSION
Section: graphics
Priority: optional
Architecture: $ARCH
Depends: libc6, libgcc-s1, libssl3, zlib1g
Maintainer: AGI Build <support@agibuild.com>
Description: Advanced Screenshot and Annotation Tool
 AGI Captor is a powerful cross-platform screenshot and annotation tool
 built with modern .NET technology. It provides intuitive tools for
 capturing, annotating, and sharing screenshots with professional quality.
 .
 Features include:
  * Full-screen and region capture
  * Rich annotation tools (text, arrows, shapes, highlights)
  * Multiple export formats
  * Customizable hotkeys
  * Cross-platform compatibility
Homepage: https://github.com/AGIBuild/AGI.Captor
EOF

# 计算已安装大小
INSTALLED_SIZE=$(du -s "$DEB_DIR/usr" | cut -f1)
echo "Installed-Size: $INSTALLED_SIZE" >> "$DEB_DIR/DEBIAN/control"

# 创建postinst脚本
cat > "$DEB_DIR/DEBIAN/postinst" << 'EOF'
#!/bin/bash
set -e

# 更新desktop数据库
if [ -x /usr/bin/update-desktop-database ]; then
    update-desktop-database /usr/share/applications
fi

# 更新MIME数据库
if [ -x /usr/bin/update-mime-database ]; then
    update-mime-database /usr/share/mime
fi

exit 0
EOF
chmod +x "$DEB_DIR/DEBIAN/postinst"

# 创建prerm脚本
cat > "$DEB_DIR/DEBIAN/prerm" << 'EOF'
#!/bin/bash
set -e

# 这里可以添加卸载前的清理工作

exit 0
EOF
chmod +x "$DEB_DIR/DEBIAN/prerm"

# 创建postrm脚本
cat > "$DEB_DIR/DEBIAN/postrm" << 'EOF'
#!/bin/bash
set -e

# 更新desktop数据库
if [ -x /usr/bin/update-desktop-database ]; then
    update-desktop-database /usr/share/applications
fi

# 更新MIME数据库
if [ -x /usr/bin/update-mime-database ]; then
    update-mime-database /usr/share/mime
fi

exit 0
EOF
chmod +x "$DEB_DIR/DEBIAN/postrm"

# 创建copyright文件
cat > "$DEB_DIR/usr/share/doc/$PACKAGE_NAME/copyright" << EOF
Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: AGI Captor
Upstream-Contact: AGI Build <support@agibuild.com>
Source: https://github.com/AGIBuild/AGI.Captor

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

# 创建changelog
cat > "$DEB_DIR/usr/share/doc/$PACKAGE_NAME/changelog.Debian" << EOF
$PACKAGE_NAME ($VERSION) unstable; urgency=medium

  * Initial release of AGI Captor
  * Cross-platform screenshot and annotation tool
  * Built with .NET and Avalonia UI

 -- AGI Build <support@agibuild.com>  $(date -R)
EOF
gzip -9 "$DEB_DIR/usr/share/doc/$PACKAGE_NAME/changelog.Debian"

echo "📦 构建 DEB 包..."

# 构建DEB包
fakeroot dpkg-deb --build "$DEB_DIR" "$SCRIPT_DIR/$DEB_NAME"

# 验证包
echo "🔍 验证 DEB 包..."
dpkg-deb --info "$SCRIPT_DIR/$DEB_NAME" >/dev/null 2>&1
dpkg-deb --contents "$SCRIPT_DIR/$DEB_NAME" >/dev/null 2>&1

# 清理临时文件
rm -rf "$TEMP_DIR"

echo "✅ DEB 包创建完成: $SCRIPT_DIR/$DEB_NAME"