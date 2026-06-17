#!/usr/bin/env sh
# FemVoice Studio — macOS codesign/notarization READINESS check (dry-run / config-check only).
# Performs NO real codesigning or notarization and requires NO Apple account/secrets. It validates the
# readiness surface (docs + optional env vars + tools) for a FUTURE real-signing slice, and confirms the
# existing UNSIGNED macOS publish flow is intact. Safe to run on any OS without credentials. See NOTARIZATION.md.
# POSIX sh. Never prints env-var VALUES. Never signs. Never runs notarytool. Never writes credentials.
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

usage() {
  cat <<'USAGE'
Usage: notarization-readiness.sh [--check | --dry-run | --help]

  --check     Verify codesign/notarization readiness docs/config surface (no secrets). Exit 0.
  --dry-run   Print the planned FUTURE codesign + notarytool steps without doing anything. Exit 0.
  --help      Show this help. Exit 0.

Readiness only — performs NO signing/notarization, requires NO Apple account/secrets, never commits credentials.
Unsigned local macOS publish remains fully supported (publish-macos.sh).

Optional env vars (documented only; NOT required for local publish; values are never printed):
  APPLE_TEAM_ID                    Apple Developer Team ID
  APPLE_DEVELOPER_ID_APPLICATION   "Developer ID Application: ..." certificate name
  APPLE_NOTARY_PROFILE             notarytool keychain profile name
  APPLE_ID                         Apple ID for notarization (if not using a profile)
  APPLE_APP_SPECIFIC_PASSWORD      app-specific password for notarization (secret)
  FEMVOICE_MACOS_SIGNING_IDENTITY  identity passed to codesign --sign
  FEMVOICE_MACOS_ENTITLEMENTS      path to an entitlements plist, if added later
USAGE
}

# Report whether an optional env var is SET — never prints the value.
report_var() {
  eval "_v=\${$1:-}"
  if [ -n "${_v:-}" ]; then echo "  $1: set (value hidden)"; else echo "  $1: not set (optional)"; fi
}

have() { if command -v "$1" >/dev/null 2>&1; then echo "available"; else echo "absent (optional — future only)"; fi; }

MODE=check
case "${1:-}" in
  --help|-h) usage; exit 0 ;;
  --dry-run) MODE=dry-run ;;
  --check|"") MODE=check ;;
  *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
esac

echo "[macos-notarize] macOS codesign/notarization readiness — mode=$MODE (no secrets, no signing)"

if [ -f "$SCRIPT_DIR/NOTARIZATION.md" ]; then echo "  docs: NOTARIZATION.md present"; else echo "  docs: NOTARIZATION.md MISSING" >&2; exit 1; fi
if [ -f "$SCRIPT_DIR/publish-macos.sh" ]; then echo "  unsigned flow: publish-macos.sh present (unsigned publish supported)"; else echo "  publish-macos.sh MISSING" >&2; exit 1; fi
if [ -f "$SCRIPT_DIR/Info.plist" ]; then echo "  bundle template: Info.plist present (inert; future .app bundling)"; fi

echo "  optional env vars (presence only — values never printed):"
report_var APPLE_TEAM_ID
report_var APPLE_DEVELOPER_ID_APPLICATION
report_var APPLE_NOTARY_PROFILE
report_var APPLE_ID
report_var APPLE_APP_SPECIFIC_PASSWORD
report_var FEMVOICE_MACOS_SIGNING_IDENTITY
report_var FEMVOICE_MACOS_ENTITLEMENTS

echo "  optional tools: codesign=$(have codesign) xcrun=$(have xcrun)"

if [ "$MODE" = dry-run ]; then
  echo "  planned FUTURE steps (NOT executed; real signing/notarization only in a future credentialed release job):"
  echo "    1. build a .app bundle (consuming Info.plist) — separate deferred slice"
  echo "    2. codesign --force --options runtime --timestamp --sign <identity> [--entitlements <file>] <App>.app"
  echo "    3. xcrun notarytool submit <App>.zip --keychain-profile <profile> --wait"
  echo "    4. xcrun stapler staple <App>.app"
  echo "    Credentials come from CI secret storage into an ephemeral keychain; never committed, never run locally by default."
fi

echo "[macos-notarize] OK — readiness check passed (no signing/notarization performed, no secrets required)."
exit 0
