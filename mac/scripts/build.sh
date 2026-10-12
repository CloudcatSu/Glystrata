#!/bin/bash
# Builds Glystrata.app (Debug by default; pass "Release" for a release build).
# Needs Xcode and XcodeGen (brew install xcodegen).
set -euo pipefail
cd "$(dirname "$0")/.."

configuration="${1:-Debug}"
xcodegen generate --quiet
xcodebuild -project Glystrata.xcodeproj -scheme Glystrata -configuration "$configuration" \
    -derivedDataPath .build/xcode build | grep -E "error:|warning: |BUILD (SUCCEEDED|FAILED)" || true
echo "App: $PWD/.build/xcode/Build/Products/$configuration/Glystrata.app"
