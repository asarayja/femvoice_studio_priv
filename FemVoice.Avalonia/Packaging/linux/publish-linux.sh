#!/usr/bin/env bash
# Publish FemVoice.Avalonia for a Linux RID (framework-dependent by default).
# Behavior-neutral packaging helper: no root, no system packages, no codesign, no install.
# Usage: ./publish-linux.sh [rid]   (default: linux-x64; override e.g. linux-arm64)
set -euo pipefail

RID="${1:-linux-x64}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"   # .../Packaging/linux -> repo root
PROJECT="$REPO_ROOT/FemVoice.Avalonia/FemVoice.Avalonia.csproj"
OUT="$REPO_ROOT/artifacts/publish/$RID"

# Framework-dependent by default (a matching .NET runtime is required on the target). For a self-contained
# build instead, see FemVoice.Avalonia/Packaging/README.md (do NOT enable trimming — reflection bindings).
echo "Publishing FemVoice.Avalonia (rid=$RID, framework-dependent) -> $OUT"
dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained false -o "$OUT"
echo "Publish output: $OUT"
