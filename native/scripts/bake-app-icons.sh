#!/bin/bash
#
# Regenerates the tray and window icons in native/EQTool.Avalonia/Assets.
#
# Upstream loads its tray icon from a .resx resource (EQTool.Properties.Resources.pig),
# which is not reachable as a file. EQTool/Images/logo.ico is the same artwork as
# a real file, so the icons are baked from that.
#
# The tray icon is 44px because the macOS menu bar works in points and renders at
# 2x on a Retina display; 44px covers a 22pt slot without upscaling.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
source_icon="$repository_root/EQTool/Images/logo.ico"
output_directory="$repository_root/native/EQTool.Avalonia/Assets"

if [[ ! -f "$source_icon" ]]; then
    echo "Source icon not found: $source_icon" >&2
    exit 1
fi

mkdir -p "$output_directory"

working_png="$(mktemp -t pigparse-icon).png"
trap 'rm -f "$working_png"' EXIT

if ! sips -s format png "$source_icon" --out "$working_png" >/dev/null 2>&1; then
    echo "Could not decode $source_icon" >&2
    exit 1
fi

bake() {
    local size="$1"
    local name="$2"

    if ! sips -Z "$size" "$working_png" --out "$output_directory/$name" >/dev/null 2>&1; then
        echo "Could not write $name" >&2
        exit 1
    fi

    local width
    width="$(sips -g pixelWidth "$output_directory/$name" 2>/dev/null | awk '/pixelWidth/ { print $2 }')"

    if [[ "$width" != "$size" ]]; then
        echo "Unexpected width for $name: $width (expected $size)" >&2
        exit 1
    fi
}

bake 44 tray-icon.png
bake 256 app-icon.png

echo "Baked tray-icon.png and app-icon.png into $output_directory"
