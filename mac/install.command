#!/bin/bash
#
# PigParse installer for macOS.
#
# Sets up a dedicated Wine prefix, installs .NET Framework 4.8, downloads a
# pinned PigParse release, and generates ~/Applications/PigParse.app locally.
#
# Environment variables:
#   PIGPARSE_VERSION   Release tag to install. Default is the pinned version
#                      below. Use "latest" to resolve the newest release from
#                      the GitHub API.
#   PIGPARSE_PREFIX    Wine prefix path. Defaults to $HOME/.wine-pigparse.
#                      Change this if you want to install into a throwaway
#                      prefix (for testing) without touching the real one.
#   PIGPARSE_APP_NAME  Name of the generated .app bundle (without ".app").
#                      Defaults to "PigParse". Mirrors PIGPARSE_PREFIX for
#                      isolated test runs.
#   PIGPARSE_YES       If set to 1, skip the initial confirmation prompt.

set -euo pipefail

# --- Pinned release (change here to bump; verified working: 5.26.830.1) -------
PIGPARSE_VERSION_PINNED="5.26.830.1"

# --- Resolve knobs ------------------------------------------------------------
PIGPARSE_VERSION="${PIGPARSE_VERSION:-$PIGPARSE_VERSION_PINNED}"
PIGPARSE_PREFIX="${PIGPARSE_PREFIX:-$HOME/.wine-pigparse}"
PIGPARSE_APP_NAME="${PIGPARSE_APP_NAME:-PigParse}"
PIGPARSE_YES="${PIGPARSE_YES:-0}"

APPS_DIR="$HOME/Applications"
BUNDLE_PATH="$APPS_DIR/${PIGPARSE_APP_NAME}.app"
GITHUB_REPO="smasherprog/EqTool"
DOTNET48_MARKER_A="$PIGPARSE_PREFIX/drive_c/windows/dotnet48.installed.workaround"
DOTNET48_MARKER_B="$PIGPARSE_PREFIX/drive_c/windows/Microsoft.NET/Framework/v4.0.30319/mscorlib.dll"

# --- Output helpers -----------------------------------------------------------
say()  { printf '\n\033[1;34m==>\033[0m %s\n' "$*"; }
warn() { printf '\n\033[1;33m!!!\033[0m %s\n' "$*" >&2; }
die()  { printf '\n\033[1;31mxxx\033[0m %s\n' "$*" >&2; exit 1; }
step() { printf '    %s\n' "$*"; }

# --- Preflight ----------------------------------------------------------------
if [[ "$(uname -s)" != "Darwin" ]]; then
    die "This installer only runs on macOS."
fi

if [[ ${EUID:-$(id -u)} -eq 0 ]]; then
    die "Do not run this installer with sudo. It installs into your home directory only."
fi

# --- Summary + single confirmation --------------------------------------------
say "PigParse macOS installer"
step "Version:        ${PIGPARSE_VERSION}"
step "Wine prefix:    ${PIGPARSE_PREFIX}"
step "App bundle:     ${BUNDLE_PATH}"
step "GitHub repo:    https://github.com/${GITHUB_REPO}"
printf '\n'
step "This will:"
step "  1. Check for Homebrew (will exit if missing)."
step "  2. Install wine-stable (cask) and winetricks (formula) if absent."
step "  3. Create the Wine prefix above if it does not exist."
step "  4. Install .NET Framework 4.8 into that prefix (~9 minutes, looks frozen)."
step "  5. Download PigParse ${PIGPARSE_VERSION} and extract it into the prefix."
step "  6. Generate ${BUNDLE_PATH}."
printf '\n'
step "This will NOT touch ~/.wine, any CrossOver bottle, or install Homebrew for you."
printf '\n'

if [[ "$PIGPARSE_YES" != "1" ]] && [[ -t 0 ]]; then
    printf 'Proceed? [y/N] '
    read -r reply
    case "$reply" in
        y|Y|yes|YES) ;;
        *) die "Aborted by user." ;;
    esac
fi

# --- Homebrew check -----------------------------------------------------------
say "Checking for Homebrew"
if ! command -v brew >/dev/null 2>&1; then
    warn "Homebrew is not installed."
    step "Install it first from https://brew.sh, then rerun this installer."
    step "The one-liner from that site is:"
    step '  /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"'
    die "Homebrew required."
fi
step "Found: $(command -v brew)"

# --- Wine (cask) --------------------------------------------------------------
say "Checking for Wine"
if command -v wine >/dev/null 2>&1; then
    step "wine already present at $(command -v wine): $(wine --version 2>/dev/null || echo unknown)"
else
    step "Installing wine-stable via Homebrew cask..."
    if ! brew install --cask wine-stable; then
        die "brew install --cask wine-stable failed. Fix Homebrew, then rerun."
    fi
fi

WINE_BIN="$(command -v wine || true)"
[[ -n "$WINE_BIN" ]] || die "wine still not on PATH after install."

# --- Winetricks (formula) -----------------------------------------------------
say "Checking for winetricks"
if command -v winetricks >/dev/null 2>&1; then
    step "winetricks already present at $(command -v winetricks)"
else
    step "Installing winetricks via Homebrew formula..."
    if ! brew install winetricks; then
        die "brew install winetricks failed. Fix Homebrew, then rerun."
    fi
fi

# --- Wine prefix --------------------------------------------------------------
say "Preparing Wine prefix: ${PIGPARSE_PREFIX}"
if [[ -d "$PIGPARSE_PREFIX" ]] && [[ -d "$PIGPARSE_PREFIX/drive_c" ]]; then
    step "Prefix already exists, keeping it."
else
    step "Creating new 64-bit prefix..."
    export WINEPREFIX="$PIGPARSE_PREFIX"
    export WINEARCH=win64
    if ! wineboot -u >/dev/null 2>&1; then
        die "wineboot -u failed while initialising ${PIGPARSE_PREFIX}."
    fi
fi

export WINEPREFIX="$PIGPARSE_PREFIX"
export WINEARCH=win64

# --- .NET Framework 4.8 (idempotent) ------------------------------------------
say "Checking for .NET Framework 4.8 in prefix"
if [[ -f "$DOTNET48_MARKER_A" ]] && [[ -f "$DOTNET48_MARKER_B" ]]; then
    step "Already installed (marker + mscorlib.dll present), skipping."
else
    warn "Installing .NET Framework 4.8. This takes about 9 minutes."
    step "The winetricks progress dialogs will look frozen at times. That is normal."
    step "Do not close this window."
    export WINETRICKS_DOWNLOADER=curl
    if ! winetricks -q dotnet48; then
        die "winetricks -q dotnet48 failed. Check the output above. You can safely rerun this installer to resume."
    fi
    if [[ ! -f "$DOTNET48_MARKER_A" ]] || [[ ! -f "$DOTNET48_MARKER_B" ]]; then
        die ".NET 4.8 install finished but expected files are missing. Marker: $DOTNET48_MARKER_A. mscorlib: $DOTNET48_MARKER_B."
    fi
    step ".NET Framework 4.8 installed."
fi

# --- Resolve release version --------------------------------------------------
say "Resolving PigParse release"
RESOLVED_VERSION="$PIGPARSE_VERSION"
if [[ "$PIGPARSE_VERSION" == "latest" ]]; then
    step "Querying GitHub for the latest release..."
    LATEST_JSON="$(curl -fsSL "https://api.github.com/repos/${GITHUB_REPO}/releases/latest" || true)"
    [[ -n "$LATEST_JSON" ]] || die "GitHub API request failed. Set PIGPARSE_VERSION to a specific tag and rerun."
    RESOLVED_VERSION="$(printf '%s' "$LATEST_JSON" | sed -n 's/.*"tag_name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
    [[ -n "$RESOLVED_VERSION" ]] || die "Could not parse tag_name from GitHub API response."
fi
step "Using release: ${RESOLVED_VERSION}"

ZIP_NAME="EQTool_Linux${RESOLVED_VERSION}.zip"
ZIP_URL="https://github.com/${GITHUB_REPO}/releases/download/${RESOLVED_VERSION}/${ZIP_NAME}"
step "Download URL: ${ZIP_URL}"

# --- Download + verify --------------------------------------------------------
DOWNLOAD_DIR="$(mktemp -d -t pigparse-install)"
trap 'rm -rf "$DOWNLOAD_DIR"' EXIT
ZIP_PATH="$DOWNLOAD_DIR/$ZIP_NAME"

say "Downloading ${ZIP_NAME}"
if ! curl -fSL --retry 3 --retry-delay 2 -o "$ZIP_PATH" "$ZIP_URL"; then
    die "Download failed. Check the version, your network, and https://github.com/${GITHUB_REPO}/releases."
fi

if [[ ! -s "$ZIP_PATH" ]]; then
    die "Downloaded file is empty: $ZIP_PATH"
fi

step "Downloaded $(wc -c < "$ZIP_PATH" | tr -d ' ') bytes."
step "Verifying zip integrity..."
if ! unzip -tqq "$ZIP_PATH" >/dev/null 2>&1; then
    FILE_TYPE="$(file -b "$ZIP_PATH" 2>/dev/null || echo unknown)"
    die "Downloaded file is not a valid zip. file(1) reports: $FILE_TYPE"
fi

# --- Install app files into prefix --------------------------------------------
INSTALL_DIR="$PIGPARSE_PREFIX/drive_c/PigParse"
say "Installing PigParse into ${INSTALL_DIR}"
mkdir -p "$INSTALL_DIR"
if ! unzip -qo "$ZIP_PATH" -d "$INSTALL_DIR"; then
    die "unzip into $INSTALL_DIR failed."
fi

if [[ ! -f "$INSTALL_DIR/EQTool.exe" ]]; then
    die "EQTool.exe not found in $INSTALL_DIR after extract."
fi
step "EQTool.exe on disk: $(wc -c < "$INSTALL_DIR/EQTool.exe" | tr -d ' ') bytes."

# --- Generate .app bundle -----------------------------------------------------
say "Generating ${BUNDLE_PATH}"
mkdir -p "$APPS_DIR"

if [[ -e "$BUNDLE_PATH" ]]; then
    step "Removing existing bundle first."
    rm -rf "$BUNDLE_PATH"
fi

mkdir -p "$BUNDLE_PATH/Contents/MacOS"
mkdir -p "$BUNDLE_PATH/Contents/Resources"

LAUNCHER="$BUNDLE_PATH/Contents/MacOS/${PIGPARSE_APP_NAME}"
cat > "$LAUNCHER" <<LAUNCHER_EOF
#!/bin/bash
# PigParse launcher. Generated by mac/install.command on $(date -u +%Y-%m-%dT%H:%M:%SZ).
# Do not edit: rerun the installer if paths change.
set -euo pipefail
export WINEPREFIX="${PIGPARSE_PREFIX}"
cd "\$WINEPREFIX/drive_c/PigParse"
exec "${WINE_BIN}" EQTool.exe "\$@"
LAUNCHER_EOF
chmod +x "$LAUNCHER"

cat > "$BUNDLE_PATH/Contents/Info.plist" <<PLIST_EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>${PIGPARSE_APP_NAME}</string>
    <key>CFBundleIdentifier</key>
    <string>com.ingallsltd.pigparse.${PIGPARSE_APP_NAME}</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>${PIGPARSE_APP_NAME}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${RESOLVED_VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${RESOLVED_VERSION}</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST_EOF

step "Bundle written."

# --- Done ---------------------------------------------------------------------
say "Done."
step "PigParse ${RESOLVED_VERSION} installed under ${PIGPARSE_PREFIX}."
step "Bundle: ${BUNDLE_PATH}"
printf '\n'
step "To launch:  open '${BUNDLE_PATH}'"
step "            or double-click ${PIGPARSE_APP_NAME}.app in ~/Applications."
step "To uninstall: run ./mac/uninstall.command from this repo."
printf '\n'
