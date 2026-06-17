# FemVoice.Avalonia — Desktop Packaging Readiness

Behavior-neutral packaging metadata for the **Linux/macOS** Avalonia desktop preview. Nothing here changes
runtime behaviour, adds real microphone capture, persists data, or starts Android/iOS work. The `Info.plist`
and `.desktop` files are **inert templates** for a future bundling step — they are NOT wired into the default
build/publish.

## Supported runtime identifiers (csproj `RuntimeIdentifiers`)
```
linux-x64 ; linux-arm64 ; osx-x64 ; osx-arm64
```
`RuntimeIdentifiers` is plural, so `dotnet build` / `dotnet run` stay portable and unchanged; only
`dotnet publish -r <rid>` targets a specific platform.

## Publish commands (documented; run as needed)
Framework-dependent (smallest; needs a matching .NET 10 runtime on the target):
```
dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj -c Release -r linux-x64  --self-contained false -o out/linux-x64
dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj -c Release -r osx-x64    --self-contained false -o out/osx-x64
dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj -c Release -r osx-arm64  --self-contained false -o out/osx-arm64
dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj -c Release -r linux-arm64 --self-contained false -o out/linux-arm64
```
Self-contained (bundles the runtime; no prerequisite on the target). **Do not enable trimming** — the head
uses reflection bindings (`AvaloniaUseCompiledBindingsByDefault=false`):
```
dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj -c Release -r linux-x64 --self-contained true -p:PublishTrimmed=false -o out/linux-x64-sc
```

## Helper scripts (convenience wrappers; behavior-neutral)
These wrap the commands above. They are **framework-dependent by default** (`--self-contained false`), require
no root, install nothing, run no `dpkg` maintainer hook scripts, and request no microphone permissions. Output
goes under `artifacts/` (gitignored).

| Script | Purpose | Default RID (override) | Output |
| --- | --- | --- | --- |
| `linux/publish-linux.sh [rid]` | Publish for a Linux RID | `linux-x64` (`linux-arm64`) | `artifacts/publish/<rid>` |
| `macos/publish-macos.sh [rid]` | Publish for a macOS RID | `osx-arm64` (`osx-x64`) | `artifacts/publish/<rid>` |
| `linux/package-deb.sh [rid]` | Build a Debian/Ubuntu `.deb` | `linux-x64` (`linux-arm64`) | `artifacts/packages/deb/femvoice-studio_<ver>_<arch>.deb` |

`package-deb.sh` (re)publishes first for determinism, then lays out a minimal package:
`/opt/femvoice-studio` (app files), `/usr/bin/femvoice-studio` (thin launcher → the framework-dependent apphost),
`/usr/share/applications/femvoice-studio.desktop`, and a `DEBIAN/control` with safe metadata. It fails with a
clear message if `dpkg-deb` is unavailable, and uses `dpkg-deb --root-owner-group` so package files are owned
by `root:root` without needing root/`fakeroot`. The `.deb` is framework-dependent (it does **not** bundle or
install .NET) — a matching .NET 10 runtime must already be present on the target.

## Templates (inert; future bundling)
- `macos/Info.plist` — macOS `.app` bundle metadata (incl. a static `NSMicrophoneUsageDescription` readiness
  string; no real capture exists yet).
- `linux/femvoice-studio.desktop` — Linux desktop entry.

A future packaging slice would wire these into a bundling step (e.g. produce a `.app` on macOS, an AppImage/
tarball on Linux). That step is deferred; this slice only establishes readiness + verifies publish works.

## Verified (on Linux)
Framework-dependent publish for `linux-x64` and `osx-x64` completed and produced a valid apphost plus the
expected managed DLLs (`FemVoice.Core`, `FemVoice.Audio.Abstractions`, `Avalonia`, `Tmds.DBus.Protocol` — and
NOT `FemVoice.Audio.Windows`). The published `linux-x64` app runs `--smoke` OK via the shared .NET runtime
(`dotnet FemVoice.Avalonia.dll --smoke`). Note: a standalone framework-dependent apphost additionally needs a
system/registered .NET runtime; on a user-local-SDK box, launch via `dotnet <app>.dll` or publish
self-contained.

The helper scripts were exercised end-to-end: `publish-linux.sh linux-x64` + `publish-macos.sh osx-x64`
published successfully, and `package-deb.sh linux-x64` produced `femvoice-studio_0.1.0_amd64.deb` with the
expected layout (`/opt/femvoice-studio/FemVoice.Avalonia`, `/usr/bin/femvoice-studio`,
`/usr/share/applications/femvoice-studio.desktop`) and `root:root` ownership. The `.deb` was **built but not
installed**. See `docs/AVALONIA_DESKTOP_PACKAGING_READINESS_GATE_RESULTS.md`.
