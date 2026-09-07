#!/usr/bin/env bash
# Publish FemVoice.Avalonia for a macOS RID. No codesign, no notarize, no .dmg, no root.
# Usage: ./publish-macos.sh [rid]            (default: osx-arm64; override e.g. osx-x64)
#        SELF_CONTAINED=true ./publish-macos.sh osx-arm64
#
# SELF_CONTAINED=true bundles the .NET runtime into the output so the target Mac needs NOTHING
# installed. That is what the CI workflow uses: a framework-dependent build refuses to start on a
# clean Mac with only a "You must install .NET" message, which is indistinguishable from a crash to
# anyone trying to test the app. Trimming stays OFF (the head uses reflection bindings).
set -euo pipefail

RID="${1:-osx-arm64}"
SELF_CONTAINED="${SELF_CONTAINED:-false}"

case "$RID" in
  osx-x64|osx-arm64) ;;
  *) echo "ERROR: unsupported RID '$RID' (expected osx-x64 or osx-arm64)" >&2; exit 2 ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"   # .../Packaging/macos -> repo root
PROJECT="$REPO_ROOT/FemVoice.Avalonia/FemVoice.Avalonia.csproj"
OUT="$REPO_ROOT/artifacts/publish/$RID"

echo "Publishing FemVoice.Avalonia (rid=$RID, self-contained=$SELF_CONTAINED) -> $OUT"
# The output directory is cleared first: `dotnet publish -o` does NOT empty it, so switching between a
# self-contained and a framework-dependent publish into the same path leaves the previous runtime behind
# and the bundle silently carries files it does not declare. (Same defect was found in the .deb path.)
rm -rf "$OUT"
dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained "$SELF_CONTAINED" \
  -p:PublishTrimmed=false -o "$OUT"
echo "Publish output: $OUT"
