#!/usr/bin/env sh
# FemVoice Studio — build an UNSIGNED macOS .app bundle from the published bits (readiness).
# Performs NO codesign/notarize/staple and requires NO Apple credentials/secrets. It assembles a standard
# .app layout suitable for a FUTURE signing/notarization slice. Safe to run on any OS (pure file operations).
# POSIX sh. Writes only under artifacts/dist/<rid>/. Never signs. See README.md / NOTARIZATION.md.
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../../.." && pwd)

usage() {
  cat <<'USAGE'
Usage: package-app.sh [osx-x64 | osx-arm64 | --check | --dry-run | --help]

  (rid)       Build the unsigned "FemVoice Studio.app" for the RID (default osx-x64). Publishes first.
  --check     Validate the readiness surface (Info.plist, publish helper, tools). No build. Exit 0.
  --dry-run   Print the planned bundle steps without building. Exit 0.
  --help      Show this help. Exit 0.

Readiness only — builds an UNSIGNED .app; performs NO codesign/notarize/staple; requires NO secrets.
Output: artifacts/dist/<rid>/FemVoice Studio.app  (gitignored). Real signing is a future credentialed slice.
USAGE
}

have() { if command -v "$1" >/dev/null 2>&1; then echo "available"; else echo "absent (optional — future only)"; fi; }

RID=osx-x64
MODE=build
case "${1:-}" in
  --help|-h) usage; exit 0 ;;
  --check)   MODE=check ;;
  --dry-run) MODE=dry-run ;;
  ""|osx-x64|osx-arm64) RID="${1:-osx-x64}"; MODE=build ;;
  *) echo "Unknown option/RID: $1" >&2; usage >&2; exit 2 ;;
esac

PUBLISH_DIR="$REPO_ROOT/artifacts/publish/$RID"
APP="$REPO_ROOT/artifacts/dist/$RID/FemVoice Studio.app"
PLIST="$SCRIPT_DIR/Info.plist"

echo "[macos-app] unsigned .app readiness — mode=$MODE rid=$RID (no codesign, no notarize, no secrets)"
if [ -f "$PLIST" ]; then echo "  Info.plist: present (CFBundleExecutable=FemVoice.Avalonia)"; else echo "  Info.plist MISSING" >&2; exit 1; fi
if [ -f "$SCRIPT_DIR/publish-macos.sh" ]; then echo "  publish helper: publish-macos.sh present"; else echo "  publish-macos.sh MISSING" >&2; exit 1; fi
echo "  optional tools: codesign=$(have codesign) (this script never signs)"

if [ "$MODE" = check ]; then
  if [ -d "$PUBLISH_DIR" ]; then echo "  publish output: present ($PUBLISH_DIR)"; else echo "  publish output: not yet published (run: publish-macos.sh $RID)"; fi
  echo "[macos-app] OK — check passed (no bundle built, no signing, no secrets)."
  exit 0
fi

if [ "$MODE" = dry-run ]; then
  echo "  planned steps (NOT executed):"
  echo "    1. publish-macos.sh $RID            (framework-dependent, unsigned)"
  echo "    2. mkdir -p '<dist>/FemVoice Studio.app/Contents/MacOS' (+ /Contents/Resources)"
  echo "    3. cp Info.plist -> Contents/Info.plist"
  echo "    4. cp -R publish/$RID/. -> Contents/MacOS/"
  echo "    5. chmod +x Contents/MacOS/FemVoice.Avalonia  (apphost; NOT codesigned)"
  echo "    NB: real codesign/notarize/staple happen ONLY in a future credentialed slice — never here."
  echo "[macos-app] OK — dry-run (nothing built or signed)."
  exit 0
fi

# build mode — publish first for a deterministic bundle, then assemble the unsigned .app.
echo "  publishing first for a deterministic bundle ..."
"$SCRIPT_DIR/publish-macos.sh" "$RID" >/dev/null
if [ ! -d "$PUBLISH_DIR" ]; then echo "  publish output missing: $PUBLISH_DIR" >&2; exit 1; fi
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$PLIST" "$APP/Contents/Info.plist"
cp -R "$PUBLISH_DIR/." "$APP/Contents/MacOS/"
if [ -f "$APP/Contents/MacOS/FemVoice.Avalonia" ]; then chmod +x "$APP/Contents/MacOS/FemVoice.Avalonia"; fi
echo "  built UNSIGNED bundle: $APP"
echo "  (no codesign performed — sign/notarize in a future credentialed slice; see NOTARIZATION.md)"
echo "[macos-app] OK — unsigned .app bundle ready."
exit 0
