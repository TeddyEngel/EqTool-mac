#!/bin/bash
#
# Regenerates the spell icon sheets in native/EQTool.Core/Resources/Spells.
#
# Upstream embeds seven .tga sheets and decodes them at runtime with
# TGASharpLib, which converts through System.Drawing. Neither is available on
# net9.0 on macOS, and shimming the 161 System.Drawing references in that
# library means reproducing LockBits stride semantics by hand.
#
# macOS decodes TGA natively, so the sheets are baked to PNG once and embedded
# instead. Avalonia loads PNG directly. Run this if upstream ever changes the
# .tga files; the output is checked in.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
source_directory="$repository_root/EQTool/Spells"
output_directory="$repository_root/native/EQTool.Core/Resources/Spells"

if [[ ! -d "$source_directory" ]]; then
    echo "Source sheets not found: $source_directory" >&2
    exit 1
fi

mkdir -p "$output_directory"

converted=0
for source_file in "$source_directory"/spells*.tga; do
    [[ -e "$source_file" ]] || continue

    sheet_name="$(basename "${source_file%.tga}")"
    output_file="$output_directory/$sheet_name.png"

    if ! sips -s format png "$source_file" --out "$output_file" >/dev/null 2>&1; then
        echo "Failed to convert $source_file" >&2
        exit 1
    fi

    # Upstream slices these into a 6x6 grid of 40x40 icons, so a sheet that is
    # not 256x256 would silently produce wrong icons rather than an error.
    dimensions="$(sips -g pixelWidth -g pixelHeight "$output_file" 2>/dev/null \
        | awk '/pixelWidth|pixelHeight/ { print $2 }' | paste -sd'x' -)"

    if [[ "$dimensions" != "256x256" ]]; then
        echo "Unexpected size for $sheet_name: $dimensions (expected 256x256)" >&2
        exit 1
    fi

    converted=$((converted + 1))
done

if [[ $converted -eq 0 ]]; then
    echo "No .tga sheets found in $source_directory" >&2
    exit 1
fi

echo "Baked $converted sheets into $output_directory"
