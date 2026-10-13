#!/bin/bash
# Rebuilds the snapshot-file icon from the artwork in ../assets. Run it again whenever that
# artwork changes; the result is checked in, so a normal build does not need it. Uses only sips
# and iconutil, which come with macOS.
#
# The app icon is not made here: it is App/AppIcon.icon, an Icon Composer document whose glyph
# (Assets/Glyph.svg) was traced from ../assets/Glystrata_icon.png. Open it in Icon Composer
# (Xcode > Open Developer Tool) to change it.
set -euo pipefail
cd "$(dirname "$0")/.."

work=$(mktemp -d)
iconset="$work/SnapshotFile.iconset"
mkdir -p "$iconset" App/Resources

# The artwork fills its whole canvas. macOS icons sit inside a margin instead: an 824-point
# body centred on a 1024-point canvas.
sips -z 824 824 ../assets/Glystrata_icon_snapshot.png --out "$work/body.png" >/dev/null
sips -p 1024 1024 "$work/body.png" --out "$work/master.png" >/dev/null

for points in 16 32 128 256 512; do
    sips -z "$points" "$points" "$work/master.png" --out "$iconset/icon_${points}x${points}.png" >/dev/null
    sips -z $((points * 2)) $((points * 2)) "$work/master.png" --out "$iconset/icon_${points}x${points}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o App/Resources/SnapshotFile.icns

echo "Wrote App/Resources/SnapshotFile.icns"
