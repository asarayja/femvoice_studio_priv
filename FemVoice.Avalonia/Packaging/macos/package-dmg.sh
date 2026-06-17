#!/usr/bin/env sh
# FemVoice Studio — build an UNSIGNED .dmg from the unsigned .app (readiness).
# Creates the .dmg ONLY when hdiutil is available (macOS). On Linux / where hdiutil is absent, it does NOT
# fail: --check/--dry-run exit 0, and build mode reports the macOS-only limitation and exits 0. Performs NO
# codesign/notarize, requires NO secrets. POSIX sh. Writes only under artifacts/dist/<rid>/. See README.md.
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../../.." && pwd)

usage() {
  cat <<'USAGE'
Usage: package-dmg.sh [osx-x64 | osx-arm64 | --check | --dry-run | --help]

  (rid)       Build "FemVoice Studio.dmg" from the unsigned .app for the RID (default osx-x64) — ONLY when
              hdiutil is available (macOS). Off macOS it reports the limitation and exits 0 (no failure).
  --check     Validate readiness (app present?, hdiutil available?). No build. Exit 0.
  --dry-run   Print the planned hdiutil steps without building. Exit 0.
  --help      Show this help. Exit 0.

Readiness only — UNSIGNED; performs NO codesign/notarize; requires NO secrets. Build the .app first with
package-app.sh. Output: artifacts/dist/<rid>/FemVoice Studio.dmg (gitignored). Real signing is a future slice.
USAGE
}

have() { if command -v "$1" >/dev/null 2>&1; then echo "available"; else echo "absent (macOS-only — future)"; fi; }

RID=osx-x64
MODE=build
case "${1:-}" in
  --help|-h) usage; exit 0 ;;
  --check)   MODE=check ;;
  --dry-run) MODE=dry-run ;;
  ""|osx-x64|osx-arm64) RID="${1:-osx-x64}"; MODE=build ;;
  *) echo "Unknown option/RID: $1" >&2; usage >&2; exit 2 ;;
esac

APP="$REPO_ROOT/artifacts/dist/$RID/FemVoice Studio.app"
DMG="$REPO_ROOT/artifacts/dist/$RID/FemVoice Studio.dmg"

echo "[macos-dmg] unsigned .dmg readiness — mode=$MODE rid=$RID (no signing, no secrets)"
echo "  optional tool: hdiutil=$(have hdiutil)  (DMG creation is macOS-only)"

if [ "$MODE" = check ]; then
  if [ -d "$APP" ]; then echo "  .app input: present ($APP)"; else echo "  .app input: not built yet (run: package-app.sh $RID)"; fi
  echo "[macos-dmg] OK — check passed (no DMG built, no signing, no secrets)."
  exit 0
fi

if [ "$MODE" = dry-run ]; then
  echo "  planned steps (NOT executed):"
  echo "    1. package-app.sh $RID  (ensure the unsigned .app exists)"
  echo "    2. hdiutil create -volname 'FemVoice Studio' -srcfolder '<.app>' -ov -format UDZO '<.dmg>'"
  echo "    NB: requires hdiutil (macOS). Real codesign/notarize/staple are a future credentialed slice — never here."
  echo "[macos-dmg] OK — dry-run (nothing built or signed)."
  exit 0
fi

# build mode
if ! command -v hdiutil >/dev/null 2>&1; then
  echo "  hdiutil not available (macOS-only) — skipping real DMG creation (readiness only)."
  echo "  Run this on macOS after package-app.sh to produce the .dmg. Unsigned .app packaging is unaffected."
  echo "[macos-dmg] OK — skipped (no hdiutil; readiness only)."
  exit 0
fi
if [ ! -d "$APP" ]; then echo "  .app missing — run package-app.sh $RID first: $APP" >&2; exit 1; fi
hdiutil create -volname "FemVoice Studio" -srcfolder "$APP" -ov -format UDZO "$DMG"
echo "  built UNSIGNED dmg: $DMG"
echo "[macos-dmg] OK — unsigned .dmg ready."
exit 0
