#!/usr/bin/env bash
# Publish FemVoice.Avalonia for a macOS RID (framework-dependent by default).
# Behavior-neutral packaging helper: no codesign, no notarize, no .dmg, no runtime mic permission,
# no Xcode-specific tooling, no root, no system packages, no install.
# Usage: ./publish-macos.sh [rid]   (default: osx-arm64; override e.g. osx-x64)
set -euo pipefail

RID="${1:-osx-arm64}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"   # .../Packaging/macos -> repo root
PROJECT="$REPO_ROOT/FemVoice.Avalonia/FemVoice.Avalonia.csproj"
OUT="$REPO_ROOT/artifacts/publish/$RID"

# Framework-dependent by default (a matching .NET runtime is required on the target). No codesign/notarize/.dmg.
echo "Publishing FemVoice.Avalonia (rid=$RID, framework-dependent) -> $OUT"
dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained false -o "$OUT"
echo "Publish output: $OUT"
