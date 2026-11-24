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

# The licence texts go beside the app, as they do beside the Windows exe.
name="Glystrata-v$version-mac-arm64"
staging="$(mktemp -d)/$name"
mkdir "$staging"
ditto "$app" "$staging/Glystrata.app"
cp ../LICENSE "$staging/LICENSE.txt"
cp THIRD-PARTY-NOTICES.txt "$staging/"

mkdir -p ../dist
zip="../dist/$name.zip"
# ditto keeps the bundle's symlinks, permissions and signature intact, which plain zip does not,
# and replaces a zip left from an earlier run. Extended attributes stay out: they would unpack as
# "._" files elsewhere, and the signature lives inside the bundle anyway.
ditto -c -k --norsrc --noextattr --noacl --keepParent "$staging" "$zip"
echo "Package: $(cd ../dist && pwd)/$(basename "$zip")"
