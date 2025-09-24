#!/bin/bash

# AGI Captor macOS 公证脚本
# 用法: ./notarize.sh <pkg_or_dmg_file> <apple_id> <app_password> <team_id>

set -e

PACKAGE_FILE="$1"
APPLE_ID="$2"
APP_PASSWORD="$3"
TEAM_ID="$4"

if [ -z "$PACKAGE_FILE" ] || [ -z "$APPLE_ID" ] || [ -z "$APP_PASSWORD" ] || [ -z "$TEAM_ID" ]; then
    echo "用法: $0 <package_file> <apple_id> <app_password> <team_id>"
    echo "示例: $0 AGI.Captor-1.2.0.pkg developer@example.com app-password TEAM123456"
    exit 1
fi

if [ ! -f "$PACKAGE_FILE" ]; then
    echo "错误: 文件不存在: $PACKAGE_FILE"
    exit 1
fi

echo "🚀 开始公证流程: $PACKAGE_FILE"

# 上传公证
echo "📤 上传文件进行公证..."
NOTARIZE_RESPONSE=$(xcrun notarytool submit "$PACKAGE_FILE" \
                                          --apple-id "$APPLE_ID" \
                                          --password "$APP_PASSWORD" \
                                          --team-id "$TEAM_ID" \
                                          --wait --output-format json)

# 提取请求ID
REQUEST_ID=$(echo "$NOTARIZE_RESPONSE" | jq -r '.id')
STATUS=$(echo "$NOTARIZE_RESPONSE" | jq -r '.status')

echo "📋 公证请求ID: $REQUEST_ID"
echo "📊 状态: $STATUS"

if [ "$STATUS" = "Accepted" ]; then
    echo "✅ 公证成功!"
    
    # 装订公证票据
    echo "📎 装订公证票据..."
    xcrun stapler staple "$PACKAGE_FILE" >/dev/null 2>&1 || true
    
    echo "✅ 公证完成并已装订票据"
    
    # 验证
    echo "🔍 验证装订..."
    xcrun stapler validate "$PACKAGE_FILE" >/dev/null 2>&1 || true
    
else
    echo "❌ 公证失败: $STATUS"
    
    # 获取详细日志
    echo "📄 获取公证日志..."
    xcrun notarytool log "$REQUEST_ID" \
                         --apple-id "$APPLE_ID" \
                         --password "$APP_PASSWORD" \
                         --team-id "$TEAM_ID"
    exit 1
fi