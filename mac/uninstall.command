#!/bin/bash
#
# PigParse uninstaller for macOS.
#
# Removes the Wine prefix and the generated ~/Applications/PigParse.app.
# Everything else installed by the installer (Homebrew, wine-stable,
# winetricks) is left alone because you may want it for other things.
#
# Environment variables:
#   PIGPARSE_PREFIX    Wine prefix to remove. Defaults to $HOME/.wine-pigparse.
#   PIGPARSE_APP_NAME  Name of the .app bundle (without ".app"). Defaults to
#                      "PigParse". Must match what you passed to the installer.
#   PIGPARSE_YES       If set to 1, skip the confirmation prompt.

set -euo pipefail

PIGPARSE_PREFIX="${PIGPARSE_PREFIX:-$HOME/.wine-pigparse}"
PIGPARSE_APP_NAME="${PIGPARSE_APP_NAME:-PigParse}"
PIGPARSE_YES="${PIGPARSE_YES:-0}"

BUNDLE_PATH="$HOME/Applications/${PIGPARSE_APP_NAME}.app"

say()  { printf '\n\033[1;34m==>\033[0m %s\n' "$*"; }
warn() { printf '\n\033[1;33m!!!\033[0m %s\n' "$*" >&2; }
die()  { printf '\n\033[1;31mxxx\033[0m %s\n' "$*" >&2; exit 1; }
step() { printf '    %s\n' "$*"; }

if [[ "$(uname -s)" != "Darwin" ]]; then
    die "This uninstaller only runs on macOS."
fi

# Strip trailing slashes first, so "$HOME/.wine/" cannot slip past the guards below.
while [[ "$PIGPARSE_PREFIX" == */ ]] && [[ "$PIGPARSE_PREFIX" != "/" ]]; do
    PIGPARSE_PREFIX="${PIGPARSE_PREFIX%/}"
done

# Refuse to touch obviously dangerous paths.
case "$PIGPARSE_PREFIX" in
    /|""|"$HOME")
        die "Refusing to remove '$PIGPARSE_PREFIX'. That is not a Wine prefix."
        ;;
esac
if [[ "$PIGPARSE_PREFIX" == "$HOME/.wine" ]]; then
    die "Refusing to remove ~/.wine. This uninstaller only handles the dedicated PigParse prefix."
fi

say "PigParse macOS uninstaller"
step "Wine prefix:  ${PIGPARSE_PREFIX}"
step "App bundle:   ${BUNDLE_PATH}"
printf '\n'
warn "This deletes your PigParse settings, triggers, saved sessions, and log parser state."
step "It does NOT remove Homebrew, wine-stable, or winetricks."
step "It does NOT touch ~/.wine or any CrossOver bottle."
printf '\n'

PREFIX_EXISTS=0
BUNDLE_EXISTS=0
[[ -d "$PIGPARSE_PREFIX" ]] && PREFIX_EXISTS=1
[[ -e "$BUNDLE_PATH"     ]] && BUNDLE_EXISTS=1

if [[ $PREFIX_EXISTS -eq 0 ]] && [[ $BUNDLE_EXISTS -eq 0 ]]; then
    step "Nothing to remove. Done."
    exit 0
fi

if [[ "$PIGPARSE_YES" != "1" ]]; then
    # Fail closed. With no terminal to prompt on (piped from curl, run from a
    # build step) we must refuse rather than delete unattended.
    if [[ ! -t 0 ]]; then
        die "No terminal to confirm on. Rerun in Terminal, or set PIGPARSE_YES=1 to confirm deletion."
    fi
    printf 'Delete these? [y/N] '
    read -r reply
    case "$reply" in
        y|Y|yes|YES) ;;
        *) die "Aborted by user." ;;
    esac
fi

if [[ $PREFIX_EXISTS -eq 1 ]]; then
    say "Removing ${PIGPARSE_PREFIX}"
    rm -rf "$PIGPARSE_PREFIX"
fi

if [[ $BUNDLE_EXISTS -eq 1 ]]; then
    say "Removing ${BUNDLE_PATH}"
    rm -rf "$BUNDLE_PATH"
fi

say "Done."
