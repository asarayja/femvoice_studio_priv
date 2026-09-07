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
# Version comes from the SINGLE source of truth: <Version> in FemVoice.Avalonia.csproj. It used to be
# hardcoded here ("0.1.0"), which meant every release needed a manual edit or a rename after the fact — a
# silent way to ship a package whose filename and control version disagree with the app's own About screen.
# Override with VERSION=x.y.z only for a deliberate one-off.
VERSION="${VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$REPO_ROOT/FemVoice.Avalonia/FemVoice.Avalonia.csproj" | head -1 | tr -d '\r')}"
if [ -z "$VERSION" ]; then
  echo "ERROR: could not read <Version> from FemVoice.Avalonia.csproj" >&2
  exit 4
fi
PKG="femvoice-studio"

PUBLISH_DIR="$REPO_ROOT/artifacts/publish/$RID"
WORK="$REPO_ROOT/artifacts/package-work/$RID"
OUT_DIR="$REPO_ROOT/artifacts/packages/deb"

# Always (re)publish for a deterministic package. The publish directory is cleared first: `dotnet publish -o`
# does NOT empty its output folder, so a previous SELF-CONTAINED publish into the same path would leave the whole
# .NET runtime behind and this framework-dependent package would silently ship ~28 MB of runtime it never declares.
echo "Publishing before packaging ..."
rm -rf "$PUBLISH_DIR"
"$SCRIPT_DIR/publish-linux.sh" "$RID"

# Minimal Debian package layout under artifacts/package-work/<rid>.
rm -rf "$WORK"
mkdir -p "$WORK/opt/femvoice-studio" "$WORK/usr/bin" "$WORK/usr/share/applications" \
         "$WORK/usr/share/doc/femvoice-studio" "$WORK/DEBIAN" "$OUT_DIR"

# App files under /opt/femvoice-studio
cp -r "$PUBLISH_DIR/." "$WORK/opt/femvoice-studio/"

# Launcher under /usr/bin/femvoice-studio.
# A small wrapper that runs the managed DLL via `dotnet` instead of relying on the
# framework-dependent apphost. The apphost only resolves a SYSTEM-REGISTERED runtime
# (DOTNET_ROOT / /etc/dotnet / /usr/share/dotnet); when .NET is installed elsewhere
# (e.g. a user-local install on PATH) the apphost prints "You must install .NET" and
# exits, which looks like the window flashing and vanishing. Going through `dotnet`
# resolves the runtime from PATH and gives a clear message if it is missing.
cat > "$WORK/usr/bin/femvoice-studio" <<'LAUNCHER'
#!/usr/bin/env bash
set -euo pipefail

APP_DIR="/opt/femvoice-studio"
APP_DLL="$APP_DIR/FemVoice.Studio.dll"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "FemVoice Studio requires the .NET runtime to be installed." >&2
  echo "Install the matching .NET desktop/runtime package, then run femvoice-studio again." >&2
  exit 127
fi

cd "$APP_DIR"
exec dotnet "$APP_DLL" "$@"
LAUNCHER
chmod 0755 "$WORK/usr/bin/femvoice-studio"

# Desktop entry under /usr/share/applications/femvoice-studio.desktop
cp "$SCRIPT_DIR/femvoice-studio.desktop" "$WORK/usr/share/applications/femvoice-studio.desktop"

# Icon under /usr/share/icons/hicolor/256x256/apps/femvoice-studio.png — this is what the .desktop entry's
# "Icon=femvoice-studio" resolves to. Packaged by the script (it used to be copied in manually afterwards, which
# meant a rebuild that forgot the step shipped a menu entry with a generic placeholder icon).
ICON_SRC="$REPO_ROOT/FemVoice.Avalonia.UI/Assets/logo.png"
if [ ! -f "$ICON_SRC" ]; then
  echo "ERROR: icon not found at $ICON_SRC" >&2
  exit 5
fi
install -Dm644 "$ICON_SRC" "$WORK/usr/share/icons/hicolor/256x256/apps/femvoice-studio.png"

# DEBIAN/control (safe metadata; NO Debian maintainer hook scripts in this slice).
cat > "$WORK/DEBIAN/control" <<CONTROL
Package: femvoice-studio
Version: $VERSION
Section: sound
Priority: optional
Architecture: $DEB_ARCH
Maintainer: A hansen <rassyhansen@gmail.com>
Homepage: https://github.com/asarayja/femvoice_studio_priv
Description: FemVoice Studio Avalonia desktop preview (framework-dependent)
 Display-only cross-platform desktop preview of FemVoice Studio. This is a
 framework-dependent build: a compatible .NET 10 desktop runtime must already be
 installed on the system (this package does NOT bundle or install .NET). It does
 NOT request microphone permissions and does not enable real capture/persistence.
CONTROL

# Machine-readable copyright + a short README.Debian under /usr/share/doc/femvoice-studio.
cp "$SCRIPT_DIR/debian-copyright" "$WORK/usr/share/doc/femvoice-studio/copyright"
cp "$SCRIPT_DIR/README.Debian"    "$WORK/usr/share/doc/femvoice-studio/README.Debian"
chmod 0644 "$WORK/usr/share/doc/femvoice-studio/copyright" "$WORK/usr/share/doc/femvoice-studio/README.Debian"

DEB="$OUT_DIR/femvoice-studio_${VERSION}_${DEB_ARCH}.deb"
# --root-owner-group sets root:root ownership inside the package without needing root/fakeroot.
dpkg-deb --root-owner-group --build "$WORK" "$DEB"
echo "Built .deb: $DEB"
