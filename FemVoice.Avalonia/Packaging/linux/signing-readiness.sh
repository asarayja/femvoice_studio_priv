#!/usr/bin/env sh
# FemVoice Studio — Linux .deb signing READINESS check (dry-run / config-check only).
# Performs NO real signing and requires NO secrets. It validates that the signing-readiness surface (docs +
# optional env vars + tools) is in place for a FUTURE real-signing slice, and confirms the existing UNSIGNED
# .deb flow is intact. Safe to run locally and in CI without any credentials. See SIGNING.md.
# POSIX sh. Never prints env-var VALUES. Never signs. Never writes keys.
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

usage() {
  cat <<'USAGE'
Usage: signing-readiness.sh [--check | --dry-run | --help]

  --check     Verify signing-readiness docs/config surface (no secrets). Exit 0.
  --dry-run   Print the planned FUTURE .deb signing steps without doing anything. Exit 0.
  --help      Show this help. Exit 0.

Readiness only — performs NO signing, requires NO secrets, never commits keys.
Unsigned local .deb builds remain fully supported (package-deb.sh).

Optional env vars (documented only; NOT required for local builds; values are never printed):
  FEMVOICE_DEB_SIGNING_KEY_ID    GPG key id a future release pipeline would sign with
  FEMVOICE_DEB_SIGNING_KEYRING   path to the keyring a future pipeline would import the key into
  FEMVOICE_RELEASE_CHECKSUMS     set to 1 to request SHA256SUMS generation in a future pipeline
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

echo "[deb-signing] Linux .deb signing readiness — mode=$MODE (no secrets, no signing)"

if [ -f "$SCRIPT_DIR/SIGNING.md" ]; then echo "  docs: SIGNING.md present"; else echo "  docs: SIGNING.md MISSING" >&2; exit 1; fi
if [ -f "$SCRIPT_DIR/package-deb.sh" ]; then echo "  unsigned flow: package-deb.sh present (unsigned .deb supported)"; else echo "  package-deb.sh MISSING" >&2; exit 1; fi

echo "  optional env vars (presence only — values never printed):"
report_var FEMVOICE_DEB_SIGNING_KEY_ID
report_var FEMVOICE_DEB_SIGNING_KEYRING
report_var FEMVOICE_RELEASE_CHECKSUMS

echo "  optional tools: gpg=$(have gpg) dpkg-sig=$(have dpkg-sig) sha256sum=$(have sha256sum)"

if [ "$MODE" = dry-run ]; then
  echo "  planned FUTURE steps (NOT executed; real signing only in a future credentialed release job):"
  echo "    1. sha256sum artifacts/packages/deb/*.deb > artifacts/packages/deb/SHA256SUMS"
  echo "    2. (apt-repo option) sign the repository Release/InRelease with the repo GPG key"
  echo "    3. (detached option) dpkg-sig --sign builder <pkg>.deb  OR  gpg --detach-sign <pkg>.deb"
  echo "    Keys come from CI secret storage into an ephemeral keyring; never committed, never run locally by default."
fi

echo "[deb-signing] OK — readiness check passed (no signing performed, no secrets required)."
exit 0
