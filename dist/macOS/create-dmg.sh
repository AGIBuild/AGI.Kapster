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
