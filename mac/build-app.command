#!/bin/bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$REPO_ROOT/native/EQTool.Avalonia/EQTool.Avalonia.csproj"
SOURCE_ICON="$REPO_ROOT/EQTool/Images/logo.ico"
APP_NAME="PigParse"
EXECUTABLE_NAME="EQTool.Avalonia"
BUNDLE_ID="com.ingalls.pigparse"
OUTPUT_DIR="${1:-$REPO_ROOT/dist}"
APP_BUNDLE="$OUTPUT_DIR/$APP_NAME.app"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

say() { printf '\n\033[1m%s\033[0m\n' "$*"; }
step() { printf '  %s\n' "$*"; }
die() { printf '\n\033[31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

command -v dotnet >/dev/null 2>&1 || die "dotnet is not installed. Get it from https://dotnet.microsoft.com/download"
command -v lipo >/dev/null 2>&1 || die "lipo is missing. Install the Xcode command line tools: xcode-select --install"
command -v iconutil >/dev/null 2>&1 || die "iconutil is missing. Install the Xcode command line tools: xcode-select --install"
[[ -f "$PROJECT" ]] || die "Cannot find $PROJECT"

VERSION="$(cd "$REPO_ROOT" && git describe --tags --always 2>/dev/null || echo "0.0.0")"

say "Building $APP_NAME.app  (version $VERSION)"

# Published without a runtime identifier on purpose. That keeps
# EQTool.Avalonia.dll AnyCPU so it loads in both an x86_64 and an arm64
# process, and puts the Avalonia native libraries under runtimes/osx/native
# where they serve either architecture.
step "Publishing the managed payload"
dotnet publish "$PROJECT" -c Release -o "$WORK_DIR/payload" --nologo -v quiet

# The apphost is the one part that carries a machine type, so both are built
# and combined rather than taking whichever this machine happens to produce.
step "Building the x86_64 launcher"
dotnet publish "$PROJECT" -c Release -r osx-x64 --self-contained false -o "$WORK_DIR/x64" --nologo -v quiet
step "Building the arm64 launcher"
dotnet publish "$PROJECT" -c Release -r osx-arm64 --self-contained false -o "$WORK_DIR/arm64" --nologo -v quiet

step "Combining them into a universal launcher"
lipo -create "$WORK_DIR/x64/$EXECUTABLE_NAME" "$WORK_DIR/arm64/$EXECUTABLE_NAME" -output "$WORK_DIR/universal"
lipo -info "$WORK_DIR/universal" | sed 's/^/    /'

step "Converting the icon"
sips -s format png "$SOURCE_ICON" --out "$WORK_DIR/icon.png" >/dev/null 2>&1 || die "Could not read $SOURCE_ICON"
mkdir -p "$WORK_DIR/$APP_NAME.iconset"
for spec in "16:icon_16x16" "32:icon_16x16@2x" "32:icon_32x32" "64:icon_32x32@2x" \
            "128:icon_128x128" "256:icon_128x128@2x" "256:icon_256x256"; do
    sips -z "${spec%%:*}" "${spec%%:*}" "$WORK_DIR/icon.png" --out "$WORK_DIR/$APP_NAME.iconset/${spec##*:}.png" >/dev/null 2>&1
done
iconutil -c icns "$WORK_DIR/$APP_NAME.iconset" -o "$WORK_DIR/$APP_NAME.icns"

step "Assembling the bundle"
rm -rf "$APP_BUNDLE"
mkdir -p "$APP_BUNDLE/Contents/MacOS" "$APP_BUNDLE/Contents/Resources"
cp -R "$WORK_DIR/payload/." "$APP_BUNDLE/Contents/MacOS/"

# Publishing without a runtime identifier brings every platform's native
# libraries along, so the Windows and Linux ones are dropped. Only runtimes/osx
# is ever resolved on a Mac, and keeping the rest roughly triples the download.
if [[ -d "$APP_BUNDLE/Contents/MacOS/runtimes" ]]; then
    find "$APP_BUNDLE/Contents/MacOS/runtimes" -mindepth 1 -maxdepth 1 -type d ! -name 'osx*' -exec rm -rf {} +
fi

cp "$WORK_DIR/universal" "$APP_BUNDLE/Contents/MacOS/$EXECUTABLE_NAME"
chmod +x "$APP_BUNDLE/Contents/MacOS/$EXECUTABLE_NAME"
cp "$WORK_DIR/$APP_NAME.icns" "$APP_BUNDLE/Contents/Resources/$APP_NAME.icns"

cat > "$APP_BUNDLE/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>$APP_NAME</string>
    <key>CFBundleDisplayName</key><string>$APP_NAME</string>
    <key>CFBundleIdentifier</key><string>$BUNDLE_ID</string>
    <key>CFBundleExecutable</key><string>$EXECUTABLE_NAME</string>
    <key>CFBundleIconFile</key><string>$APP_NAME</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$VERSION</string>
    <key>CFBundleVersion</key><string>$VERSION</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>LSMinimumSystemVersion</key><string>11.0</string>
    <key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
    <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

plutil -lint "$APP_BUNDLE/Contents/Info.plist" >/dev/null || die "Generated Info.plist is not valid"

say "Done"
step "$APP_BUNDLE"
step "$(du -sh "$APP_BUNDLE" | cut -f1) on disk"
printf '\n'
step "This bundle is not signed or notarized, so the first launch is blocked."
step "Right click it, choose Open, then confirm. Only needed once."
printf '\n'
