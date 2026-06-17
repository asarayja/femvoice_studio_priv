#!/usr/bin/env bash
# Build a minimal Debian/Ubuntu .deb from the Linux publish output (framework-dependent).
# Behavior-neutral: no privilege escalation, no install step, no Debian maintainer hook scripts,
# no microphone permissions, no modification of user/system state. Publishes first for determinism.
# Usage: ./package-deb.sh [rid]   (default: linux-x64; override: linux-arm64)
set -euo pipefail

RID="${1:-linux-x64}"
case "$RID" in
  linux-x64)   DEB_ARCH="amd64" ;;
  linux-arm64) DEB_ARCH="arm64" ;;
  *) echo "ERROR: unsupported RID '$RID' (expected linux-x64 or linux-arm64)" >&2; exit 2 ;;
esac

if ! command -v dpkg-deb >/dev/null 2>&1; then
  echo "ERROR: dpkg-deb not found. Install dpkg (Debian/Ubuntu) to build a .deb package." >&2
  exit 3
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"   # .../Packaging/linux -> repo root
VERSION="0.1.0"
PKG="femvoice-studio"

PUBLISH_DIR="$REPO_ROOT/artifacts/publish/$RID"
WORK="$REPO_ROOT/artifacts/package-work/$RID"
OUT_DIR="$REPO_ROOT/artifacts/packages/deb"

# Always (re)publish for a deterministic package.
echo "Publishing before packaging ..."
"$SCRIPT_DIR/publish-linux.sh" "$RID"

# Minimal Debian package layout under artifacts/package-work/<rid>.
rm -rf "$WORK"
mkdir -p "$WORK/opt/femvoice-studio" "$WORK/usr/bin" "$WORK/usr/share/applications" "$WORK/DEBIAN" "$OUT_DIR"

# App files under /opt/femvoice-studio
cp -r "$PUBLISH_DIR/." "$WORK/opt/femvoice-studio/"

# Launcher under /usr/bin/femvoice-studio (runs the framework-dependent apphost).
cat > "$WORK/usr/bin/femvoice-studio" <<'LAUNCHER'
#!/bin/sh
# Requires a matching .NET runtime installed on the system (this is a framework-dependent build).
exec /opt/femvoice-studio/FemVoice.Avalonia "$@"
LAUNCHER
chmod 0755 "$WORK/usr/bin/femvoice-studio"

# Desktop entry under /usr/share/applications/femvoice-studio.desktop
cp "$SCRIPT_DIR/femvoice-studio.desktop" "$WORK/usr/share/applications/femvoice-studio.desktop"

# DEBIAN/control (safe metadata; NO maintainer scripts in this slice).
cat > "$WORK/DEBIAN/control" <<CONTROL
Package: femvoice-studio
Version: $VERSION
Section: sound
Priority: optional
Architecture: $DEB_ARCH
Maintainer: FemVoice Studio
Description: FemVoice Studio Avalonia desktop preview
 Display-only cross-platform desktop preview. This is a framework-dependent build:
 a matching .NET 10 runtime must already be installed on the system. This package does
 NOT install .NET and does NOT request microphone permissions.
CONTROL

DEB="$OUT_DIR/femvoice-studio_${VERSION}_${DEB_ARCH}.deb"
# --root-owner-group sets root:root ownership inside the package without needing root/fakeroot.
dpkg-deb --root-owner-group --build "$WORK" "$DEB"
echo "Built .deb: $DEB"
