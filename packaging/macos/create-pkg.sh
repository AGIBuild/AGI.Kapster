#!/bin/bash

# AGI Captor macOS PKG和DMG创建脚本
# 用法: ./create-pkg.sh <publish_directory> <version> [sign_identity]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$1"
VERSION="$2"
SIGN_IDENTITY="$3"

if [ -z "$PUBLISH_DIR" ] || [ -z "$VERSION" ]; then
    echo "用法: $0 <publish_directory> <version> [sign_identity]"
    echo "示例: $0 ../artifacts/publish/osx-x64 1.2.0 'Developer ID Application: Your Name'"
    exit 1
fi

# 验证输入目录
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "错误: 发布目录不存在: $PUBLISH_DIR"
    exit 1
fi

# 配置
APP_NAME="AGI Captor"
BUNDLE_ID="com.agi.captor"
PKG_NAME="AGI.Captor-${VERSION}.pkg"
DMG_NAME="AGI.Captor-${VERSION}.dmg"
TEMP_DIR="$(mktemp -d)"
APP_DIR="$TEMP_DIR/$APP_NAME.app"

echo "🔨 创建 macOS 应用程序包..."

# 创建.app结构
mkdir -p "$APP_DIR/Contents/MacOS"
mkdir -p "$APP_DIR/Contents/Resources"

# 复制可执行文件
cp "$PUBLISH_DIR/AGI.Captor.Desktop" "$APP_DIR/Contents/MacOS/"
chmod +x "$APP_DIR/Contents/MacOS/AGI.Captor.Desktop"

# 复制其他文件
cp -r "$PUBLISH_DIR"/* "$APP_DIR/Contents/MacOS/" 2>/dev/null || true

# 创建Info.plist
cat > "$APP_DIR/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleExecutable</key>
    <string>AGI.Captor.Desktop</string>
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
    <key>NSRequiresAquaSystemAppearance</key>
    <false/>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.graphics-design</string>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2025 AGI Build. All rights reserved.</string>
</dict>
</plist>
EOF

# 复制图标（如果存在）
if [ -f "$SCRIPT_DIR/../src/AGI.Captor.Desktop/logo.icns" ]; then
    cp "$SCRIPT_DIR/../src/AGI.Captor.Desktop/logo.icns" "$APP_DIR/Contents/Resources/"
elif [ -f "$SCRIPT_DIR/../../src/AGI.Captor.Desktop/logo.ico" ]; then
    echo "⚠️  找到.ico文件但需要.icns文件，请转换图标格式"
fi

# 应用签名（如果提供了签名身份）
if [ -n "$SIGN_IDENTITY" ]; then
    echo "🔐 应用程序签名..."
    codesign --force --verify --verbose --sign "$SIGN_IDENTITY" "$APP_DIR"
fi

echo "📦 创建 PKG 安装包..."

# 创建PKG
pkgbuild --root "$TEMP_DIR" \
         --identifier "$BUNDLE_ID" \
         --version "$VERSION" \
         --install-location "/Applications" \
         "$SCRIPT_DIR/$PKG_NAME"

# 如果提供了签名身份，签名PKG
if [ -n "$SIGN_IDENTITY" ]; then
    echo "🔐 PKG 签名..."
    productsign --sign "$SIGN_IDENTITY" "$SCRIPT_DIR/$PKG_NAME" "$SCRIPT_DIR/${PKG_NAME%.pkg}-signed.pkg"
    mv "$SCRIPT_DIR/${PKG_NAME%.pkg}-signed.pkg" "$SCRIPT_DIR/$PKG_NAME"
fi

echo "💿 创建 DMG 镜像..."

# 创建DMG的临时目录
DMG_TEMP_DIR="$(mktemp -d)"
cp -R "$APP_DIR" "$DMG_TEMP_DIR/"

# 创建Applications链接
ln -s /Applications "$DMG_TEMP_DIR/Applications"

# 创建DMG
hdiutil create -volname "$APP_NAME" \
               -srcfolder "$DMG_TEMP_DIR" \
               -ov -format UDZO \
               "$SCRIPT_DIR/$DMG_NAME"

# 清理临时文件
rm -rf "$TEMP_DIR" "$DMG_TEMP_DIR"

echo "✅ macOS 安装包创建完成:"
echo "  📦 PKG: $SCRIPT_DIR/$PKG_NAME"
echo "  💿 DMG: $SCRIPT_DIR/$DMG_NAME"

# 显示签名状态
if [ -n "$SIGN_IDENTITY" ]; then
    echo "🔐 验证签名:"
    codesign --verify --verbose=2 "$SCRIPT_DIR/$PKG_NAME" 2>&1 || true
fi