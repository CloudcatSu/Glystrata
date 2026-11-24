#!/bin/bash
# Builds a Release Glystrata.app for Apple Silicon and zips it into dist/ as
# Glystrata-v<version>-mac-arm64.zip, the same naming as the Windows downloads.
# The app is ad-hoc signed only (no Apple Developer ID, not notarized), so a downloaded copy is
# stopped by Gatekeeper the first time; README's download section says how to open it anyway.
set -euo pipefail
cd "$(dirname "$0")/.."

xcodegen generate --quiet
xcodebuild -project Glystrata.xcodeproj -scheme Glystrata -configuration Release \
    -derivedDataPath .build/xcode ARCHS=arm64 ONLY_ACTIVE_ARCH=NO build \
    | grep -E "error:|warning: |BUILD (SUCCEEDED|FAILED)" || true

app=".build/xcode/Build/Products/Release/Glystrata.app"
version=$(/usr/libexec/PlistBuddy -c "Print CFBundleShortVersionString" "$app/Contents/Info.plist")
codesign --verify --strict "$app"
lipo -verify_arch arm64 "$app/Contents/MacOS/Glystrata"

mkdir -p ../dist
zip="../dist/Glystrata-v$version-mac-arm64.zip"
# ditto keeps the bundle's symlinks, permissions and signature intact, which plain zip does not,
# and replaces a zip left from an earlier run.
ditto -c -k --keepParent "$app" "$zip"
echo "Package: $(cd ../dist && pwd)/$(basename "$zip")"
