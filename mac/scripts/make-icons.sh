#!/bin/bash
# Rebuilds the pictures that are made from other files. Run it again whenever one of those
# changes; the results are checked in, so a normal build does not need it. Uses sips, iconutil and
# plutil, which come with macOS, and ictool, which comes with Xcode's Icon Composer.
#
# - The snapshot-file icon, from the artwork in ../assets.
# - The two glass pictures of the app icon that Preferences can put on the Dock, from
#   App/AppIcon.icon with the glass finish switched on.
#
# The app icon itself is not made here: it is App/AppIcon.icon, an Icon Composer document whose
# glyph (Assets/Glyph.svg) was traced from ../assets/Glystrata_icon.png. Open it in Icon Composer
# (Xcode > Open Developer Tool) to change it, then run this again.
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


# The same icon with the finish Icon Composer calls glass, as plain pictures: the system draws
# the real icon itself, but a picture handed to the Dock has to arrive already drawn.
ictool="$(xcode-select -p)/../Applications/Icon Composer.app/Contents/Executables/ictool"
glass="$work/Glass.icon"
cp -R App/AppIcon.icon "$glass"
plutil -replace groups.0.layers.0.glass -bool YES "$glass/icon.json"
plutil -replace groups.0.specular -bool YES "$glass/icon.json"
plutil -replace groups.0.shadow -json '{"kind":"neutral","opacity":0.5}' "$glass/icon.json"
plutil -replace groups.0.translucency -json '{"enabled":true,"value":0.5}' "$glass/icon.json"
for entry in "Default IconGlassLight" "Dark IconGlassDark"; do
    read -r rendition name <<<"$entry"
    "$ictool" "$glass" --export-image --output-file "$work/$name.png" --platform macOS \
        --rendition "$rendition" --width 824 --height 824 --scale 1 >/dev/null
    sips -p 1024 1024 "$work/$name.png" --out "App/Assets.xcassets/$name.imageset/$name.png" >/dev/null
done

echo "Wrote App/Resources/SnapshotFile.icns and the IconGlass pictures"
