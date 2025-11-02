#!/bin/bash

# AGI Kapster Linux RPM包创建脚本
# 用法: ./create-rpm.sh <publish_directory> <version> <architecture>

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$1"
VERSION="$2"
ARCH="${3:-x86_64}"

if [ -z "$PUBLISH_DIR" ] || [ -z "$VERSION" ]; then
    echo "用法: $0 <publish_directory> <version> [architecture]"
    echo "示例: $0 ../artifacts/publish/linux-x64 1.2.0 x86_64"
    exit 1
fi

# 验证输入目录
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "错误: 发布目录不存在: $PUBLISH_DIR"
    exit 1
fi

# 配置
PACKAGE_NAME="agi-kapster"
RPM_NAME="${PACKAGE_NAME}-${VERSION}-1.${ARCH}.rpm"
TEMP_DIR="$(mktemp -d)"
RPM_ROOT="$TEMP_DIR/rpm-root"

echo "🔨 创建 RPM 包结构..."

# 创建RPM目录结构
mkdir -p "$RPM_ROOT/usr/bin" 2>/dev/null || true
mkdir -p "$RPM_ROOT/usr/share/applications" 2>/dev/null || true
mkdir -p "$RPM_ROOT/usr/share/pixmaps" 2>/dev/null || true
mkdir -p "$RPM_ROOT/usr/share/$PACKAGE_NAME" 2>/dev/null || true

# 复制应用程序文件
cp -r "$PUBLISH_DIR"/* "$RPM_ROOT/usr/share/$PACKAGE_NAME/"
chmod +x "$RPM_ROOT/usr/share/$PACKAGE_NAME/AGI.Kapster.Desktop"

# 创建启动脚本
cat > "$RPM_ROOT/usr/bin/agi-kapster" << 'EOF'
#!/bin/bash
exec /usr/share/agi-kapster/AGI.Kapster.Desktop "$@"
EOF
chmod +x "$RPM_ROOT/usr/bin/agi-kapster"

# 创建desktop文件
cat > "$RPM_ROOT/usr/share/applications/agi-kapster.desktop" << EOF
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

# 复制图标（PNG，建议512x512）
if [ -f "$SCRIPT_DIR/../../src/AGI.Kapster.Desktop/logo.png" ]; then
    cp "$SCRIPT_DIR/../../src/AGI.Kapster.Desktop/logo.png" "$RPM_ROOT/usr/share/pixmaps/agi-kapster.png"
else
    echo "⚠️  未找到 logo.png。请在 src/AGI.Kapster.Desktop/logo.png 生成。"
fi

# 创建spec文件
cat > "$TEMP_DIR/$PACKAGE_NAME.spec" << EOF
Name: $PACKAGE_NAME
Version: $VERSION
Release: 1
Summary: Advanced Screenshot and Annotation Tool
License: MIT
URL: https://github.com/AGIBuild/AGI.Kapster
Group: Applications/Graphics
BuildArch: $ARCH
Requires: glibc, openssl-libs, zlib

%description
AGI Kapster is a powerful cross-platform screenshot and annotation tool
built with modern .NET technology. It provides intuitive tools for
capturing, annotating, and sharing screenshots with professional quality.

Features include:
- Full-screen and region capture
- Rich annotation tools (text, arrows, shapes, highlights)
- Multiple export formats
- Customizable hotkeys
- Cross-platform compatibility

%install
{
  mkdir -p %{buildroot}/usr/bin
  mkdir -p %{buildroot}/usr/share/applications
  mkdir -p %{buildroot}/usr/share/pixmaps
  mkdir -p %{buildroot}/usr/share/$PACKAGE_NAME
  cp -r $RPM_ROOT/* %{buildroot}/
} >/dev/null 2>&1 || true

%files
/usr/bin/agi-kapster
/usr/share/applications/agi-kapster.desktop
/usr/share/pixmaps/agi-kapster.png
/usr/share/$PACKAGE_NAME/*

%post
# 更新desktop数据库
if [ -x /usr/bin/update-desktop-database ]; then
    /usr/bin/update-desktop-database /usr/share/applications &> /dev/null || :
fi

# 更新MIME数据库
if [ -x /usr/bin/update-mime-database ]; then
    /usr/bin/update-mime-database /usr/share/mime &> /dev/null || :
fi

%postun
# 更新desktop数据库
if [ -x /usr/bin/update-desktop-database ]; then
    /usr/bin/update-desktop-database /usr/share/applications &> /dev/null || :
fi

# 更新MIME数据库
if [ -x /usr/bin/update-mime-database ]; then
    /usr/bin/update-mime-database /usr/share/mime &> /dev/null || :
fi

%changelog
* $(date "+%a %b %d %Y") AGI Build <support@agibuild.com> - $VERSION-1
- Initial release of AGI Kapster
- Cross-platform screenshot and annotation tool
- Built with .NET and Avalonia UI
EOF

echo "📦 构建 RPM 包..."

# 构建RPM包
rpmbuild --define "_topdir $TEMP_DIR" \
         --define "_builddir $TEMP_DIR" \
         --define "_sourcedir $TEMP_DIR" \
         --define "_rpmdir $SCRIPT_DIR" \
         --define "_buildrootdir $TEMP_DIR/buildroot" \
         -bb "$TEMP_DIR/$PACKAGE_NAME.spec" >/dev/null 2>&1

# 移动生成的RPM文件
mv "$SCRIPT_DIR/$ARCH/$RPM_NAME" "$SCRIPT_DIR/"
rmdir "$SCRIPT_DIR/$ARCH" 2>/dev/null || true

# 验证包
echo "🔍 验证 RPM 包..."
rpm -qpi "$SCRIPT_DIR/$RPM_NAME" >/dev/null 2>&1
rpm -qpl "$SCRIPT_DIR/$RPM_NAME" >/dev/null 2>&1

# 清理临时文件
rm -rf "$TEMP_DIR"

echo "✅ RPM 包创建完成: $SCRIPT_DIR/$RPM_NAME"