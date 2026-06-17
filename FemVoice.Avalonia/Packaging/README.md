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

## Templates (inert; future bundling)
- `macos/Info.plist` — macOS `.app` bundle metadata (incl. a static `NSMicrophoneUsageDescription` readiness
  string; no real capture exists yet).
- `linux/femvoice-studio.desktop` — Linux desktop entry.

A future packaging slice would wire these into a bundling step (e.g. produce a `.app` on macOS, an AppImage/
tarball on Linux). That step is deferred; this slice only establishes readiness + verifies publish works.

## Verified (this slice, on Linux)
Framework-dependent publish for `linux-x64` and `osx-x64` completed and produced a valid apphost plus the
expected managed DLLs (`FemVoice.Core`, `FemVoice.Audio.Abstractions`, `Avalonia`, `Tmds.DBus.Protocol` — and
NOT `FemVoice.Audio.Windows`). The published `linux-x64` app runs `--smoke` OK via the shared .NET runtime
(`dotnet FemVoice.Avalonia.dll --smoke`). Note: a standalone framework-dependent apphost additionally needs a
system/registered .NET runtime; on a user-local-SDK box, launch via `dotnet <app>.dll` or publish
self-contained. See `docs/AVALONIA_DESKTOP_PACKAGING_READINESS_GATE_RESULTS.md`.
