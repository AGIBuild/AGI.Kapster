#!/bin/bash

# AGI Captor macOS App Store版本创建脚本
# 用法: ./create-appstore.sh <publish_directory> <version> <signing_identity>

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$1"
VERSION="$2"
SIGN_IDENTITY="$3"

if [ -z "$PUBLISH_DIR" ] || [ -z "$VERSION" ] || [ -z "$SIGN_IDENTITY" ]; then
    echo "用法: $0 <publish_directory> <version> <signing_identity>"
    echo "示例: $0 ../artifacts/publish/osx-x64 1.2.0 '3rd Party Mac Developer Application: Your Name'"
    exit 1
fi

# 验证输入目录
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "错误: 发布目录不存在: $PUBLISH_DIR"
    exit 1
fi

# 验证entitlements文件
ENTITLEMENTS_FILE="$SCRIPT_DIR/entitlements.plist"
if [ ! -f "$ENTITLEMENTS_FILE" ]; then
    echo "错误: entitlements文件不存在: $ENTITLEMENTS_FILE"
    exit 1
fi

# 配置
APP_NAME="AGI Captor"
BUNDLE_ID="com.agi.captor"
PKG_NAME="AGI.Captor-${VERSION}-AppStore.pkg"
TEMP_DIR="$(mktemp -d)"
APP_DIR="$TEMP_DIR/$APP_NAME.app"

echo "🏪 创建 App Store 版本..."

# 创建.app结构
{
  mkdir -p "$APP_DIR/Contents/MacOS"
  mkdir -p "$APP_DIR/Contents/Resources"
  
  # Copy executable file
  cp "$PUBLISH_DIR/AGI.Captor.Desktop" "$APP_DIR/Contents/MacOS/"
  chmod +x "$APP_DIR/Contents/MacOS/AGI.Captor.Desktop"
  
  # Copy other files
  cp -r "$PUBLISH_DIR"/* "$APP_DIR/Contents/MacOS/"
} >/dev/null 2>&1 || true

# 创建App Store专用的Info.plist
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
    
    <!-- App Store特定配置 -->
    <key>ITSAppUsesNonExemptEncryption</key>
    <false/>
    
    <!-- 隐私使用说明 -->
    <key>NSCameraUsageDescription</key>
    <string>AGI Captor needs camera access for screen recording functionality.</string>
    <key>NSMicrophoneUsageDescription</key>
    <string>AGI Captor needs microphone access to record audio during screen capture.</string>
    <key>NSScreenCaptureDescription</key>
    <string>AGI Captor is a screen capture application that requires screen recording permissions.</string>
    
    <!-- 沙盒标识 -->
    <key>LSApplicationSecondsOfLaunchTime</key>
    <integer>10</integer>
</dict>
</plist>
EOF

# 复制图标（如果存在）
if [ -f "$SCRIPT_DIR/../src/AGI.Captor.Desktop/logo.icns" ]; then
    cp "$SCRIPT_DIR/../src/AGI.Captor.Desktop/logo.icns" "$APP_DIR/Contents/Resources/"
elif [ -f "$SCRIPT_DIR/../../src/AGI.Captor.Desktop/logo.ico" ]; then
    echo "⚠️  找到.ico文件但需要.icns文件，App Store版本需要正确的图标格式"
fi

echo "🔐 使用entitlements进行App Store签名..."

# 使用entitlements签名应用程序
codesign --force --verify --verbose \
         --sign "$SIGN_IDENTITY" \
         --entitlements "$ENTITLEMENTS_FILE" \
         --options runtime \
         "$APP_DIR"

echo "📦 创建 App Store PKG..."

# 创建App Store PKG（需要Installer signing identity）
INSTALLER_IDENTITY=$(echo "$SIGN_IDENTITY" | sed 's/3rd Party Mac Developer Application/3rd Party Mac Developer Installer/')

pkgbuild --root "$TEMP_DIR" \
         --identifier "$BUNDLE_ID" \
         --version "$VERSION" \
         --install-location "/Applications" \
         --sign "$INSTALLER_IDENTITY" \
         "$SCRIPT_DIR/$PKG_NAME"

# 清理临时文件
rm -rf "$TEMP_DIR" >/dev/null 2>&1 || true

echo "✅ App Store 版本创建完成:"
echo "  📦 PKG: $SCRIPT_DIR/$PKG_NAME"
echo ""
echo "🏪 App Store提交指南:"
echo "  1. 使用Xcode或Transporter上传PKG"
echo "  2. 确保已在App Store Connect中配置应用信息"
echo "  3. 添加隐私说明: 相机、麦克风、屏幕录制权限"
echo "  4. 设置应用分类: 图形和设计"

# 验证签名
echo "🔐 验证App Store签名:"
codesign --verify --verbose=2 "$SCRIPT_DIR/$PKG_NAME" 2>&1 || true
echo ""
echo "📋 注意事项:"
echo "  - 此版本使用沙盒环境，功能可能有限制"
echo "  - 需要用户明确授权屏幕录制权限"
echo "  - 上传前请测试所有核心功能"