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
CSPROJ="$REPO_ROOT/FemVoice.Avalonia/FemVoice.Avalonia.csproj"
# The apphost file name comes from <AssemblyName>FemVoice.Studio</AssemblyName>, NOT from the project
# name. This script used to chmod "FemVoice.Avalonia" and the Info.plist used to name it as
# CFBundleExecutable — both left over from before the rename, which meant macOS could not find the
# executable and the bundle would not launch. Both are corrected and verified below.
APPHOST="FemVoice.Studio"
# Optional app icon (readiness): Info.plist references CFBundleIconFile=AppIcon -> Contents/Resources/AppIcon.icns.
# This file is intentionally NOT committed (no production icon/branding invented). If a real AppIcon.icns is dropped
# here later, it is bundled automatically; if absent, macOS uses the generic icon. See AppIcon.icns.README.md.
ICON="$SCRIPT_DIR/AppIcon.icns"

echo "[macos-app] unsigned .app readiness — mode=$MODE rid=$RID (no codesign, no notarize, no secrets)"
if [ -f "$PLIST" ]; then echo "  Info.plist: present (CFBundleExecutable=$APPHOST, CFBundleIconFile=AppIcon)"; else echo "  Info.plist MISSING" >&2; exit 1; fi
if [ -f "$SCRIPT_DIR/publish-macos.sh" ]; then echo "  publish helper: publish-macos.sh present"; else echo "  publish-macos.sh MISSING" >&2; exit 1; fi
if [ -f "$ICON" ]; then echo "  app icon: AppIcon.icns present (will be bundled)"; else echo "  app icon: AppIcon.icns absent — system default (production icon deferred; see AppIcon.icns.README.md)"; fi
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
  echo "    5. if AppIcon.icns present: cp -> Contents/Resources/AppIcon.icns (else skip — system default)"
  echo "    6. chmod +x Contents/MacOS/FemVoice.Studio  (apphost; NOT codesigned)"
  echo "    7. verify CFBundleExecutable names a real file in Contents/MacOS (else it cannot launch)"
  echo "    NB: real codesign/notarize/staple happen ONLY in a future credentialed slice — never here."
  echo "[macos-app] OK — dry-run (nothing built or signed)."
  exit 0
fi

# build mode — publish first for a deterministic bundle, then assemble the unsigned .app.
echo "  publishing first for a deterministic bundle ..."
SELF_CONTAINED="${SELF_CONTAINED:-false}" "$SCRIPT_DIR/publish-macos.sh" "$RID" >/dev/null
if [ ! -d "$PUBLISH_DIR" ]; then echo "  publish output missing: $PUBLISH_DIR" >&2; exit 1; fi
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# Bundle version comes from <Version> in the csproj (the single source of truth the About screen also
# uses). The template carries a __VERSION__ placeholder; a bundle is never emitted with it unresolved.
VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$CSPROJ" | head -1 | tr -d '\r')
if [ -z "$VERSION" ]; then echo "  ERROR: could not read <Version> from $CSPROJ" >&2; exit 1; fi
sed "s/__VERSION__/$VERSION/g" "$PLIST" > "$APP/Contents/Info.plist"
if grep -q '__VERSION__' "$APP/Contents/Info.plist"; then
  echo "  ERROR: Info.plist still contains the __VERSION__ placeholder after substitution" >&2; exit 1
fi
echo "  version: $VERSION (from FemVoice.Avalonia.csproj)"

cp -R "$PUBLISH_DIR/." "$APP/Contents/MacOS/"

# Strip the executable bit from managed assemblies.
#
# `dotnet publish` marks its output +x and `cp -R` preserves it, so ~210 managed .dll files land in
# Contents/MacOS marked executable. codesign treats every executable file inside a bundle as NESTED CODE
# that must carry its own signature, and a managed assembly is a PE32 file that never can — so sealing the
# bundle fails outright:
#
#   FemVoice Studio.app: code object is not signed at all
#   In subcomponent: .../Contents/MacOS/System.Web.dll
#
# The .NET runtime loads these assemblies itself; nothing ever execs them, so the bit is meaningless here.
# Clearing it lets codesign seal them as ordinary resources. The real executables — the apphost, createdump
# and the .dylib files — keep theirs.
find "$APP/Contents/MacOS" -type f -name '*.dll' -exec chmod a-x {} +

# The bundle is unlaunchable if CFBundleExecutable does not name a real file in Contents/MacOS.
# Verify it explicitly rather than discovering it on the test Mac.
if [ ! -f "$APP/Contents/MacOS/$APPHOST" ]; then
  echo "  ERROR: apphost '$APPHOST' not found in the bundle — CFBundleExecutable would not resolve." >&2
  echo "         Contents/MacOS holds:" >&2
  ls "$APP/Contents/MacOS" | head -20 >&2
  exit 1
fi
chmod +x "$APP/Contents/MacOS/$APPHOST"
PLIST_EXE=$(sed -n 's:.*<key>CFBundleExecutable</key>[[:space:]]*<string>\(.*\)</string>.*:\1:p' "$APP/Contents/Info.plist" | head -1)
if [ "$PLIST_EXE" != "$APPHOST" ]; then
  echo "  ERROR: CFBundleExecutable is '$PLIST_EXE' but the apphost is '$APPHOST' — bundle would not launch." >&2
  exit 1
fi
echo "  apphost: $APPHOST (matches CFBundleExecutable)"
# Bundle the app icon ONLY if a real AppIcon.icns has been provided; never fail when it is absent.
if [ -f "$ICON" ]; then
  cp "$ICON" "$APP/Contents/Resources/AppIcon.icns"
  echo "  icon: bundled AppIcon.icns -> Contents/Resources/"
else
  echo "  icon: AppIcon.icns absent — bundle uses the system default (production icon deferred; not an error)"
fi
echo "  built UNSIGNED bundle: $APP"
echo "  (no codesign performed — sign/notarize in a future credentialed slice; see NOTARIZATION.md)"
echo "[macos-app] OK — unsigned .app bundle ready."
exit 0
