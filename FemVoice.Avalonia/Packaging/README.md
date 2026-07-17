# FemVoice.Avalonia — Desktop Packaging Readiness

Behavior-neutral packaging metadata for the **Linux/macOS** Avalonia desktop preview. Nothing here changes
runtime behaviour, adds real microphone capture, persists data, or starts Android/iOS work. The `Info.plist`
and `.desktop` files are **inert templates** for a future bundling step — they are NOT wired into the default
build/publish.

## Supported runtime identifiers (csproj `RuntimeIdentifiers`)
```
linux-x64 ; linux-arm64 ; osx-x64 ; osx-arm64 ; win-x64 ; win-arm64
```
`RuntimeIdentifiers` is plural, so `dotnet build` / `dotnet run` stay portable and unchanged; only
`dotnet publish -r <rid>` targets a specific platform. The **Windows** RIDs make the Avalonia head the
cross-platform Windows path — a `win-x64` publish produces `FemVoice.Avalonia.exe` (`Avalonia.Win32`), and it
cross-compiles from Linux. The WPF app (`FemVoiceStudio`) remains the frozen Windows *reference* baseline.

Windows publish:
```
dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj -c Release -r win-x64  --self-contained false -o out/win-x64
dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj -c Release -r win-arm64 --self-contained false -o out/win-arm64
```
> Follow-up: set `<OutputType>WinExe</OutputType>` (with console reattach for the `--*-smoke` paths) so the
> Windows GUI launch does not open a console window. The current `Exe` output builds a console-subsystem `.exe`.

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
- `/opt/femvoice-studio` — published app files
- `/usr/bin/femvoice-studio` — launcher **script** that runs the managed DLL via `dotnet` (see below)
- `/usr/share/applications/femvoice-studio.desktop` — desktop entry (`Exec=femvoice-studio`)
- `/usr/share/doc/femvoice-studio/copyright` — machine-readable copyright (from `linux/debian-copyright`)
- `/usr/share/doc/femvoice-studio/README.Debian` — short install/runtime note (from `linux/README.Debian`)
- `DEBIAN/control` — `Maintainer: A hansen <rassyhansen@gmail.com>`, `Homepage`, framework-dependent `Description`

It fails with a clear message if `dpkg-deb` is unavailable, and uses `dpkg-deb --root-owner-group` so package
files are owned by `root:root` without needing root/`fakeroot`. The `.deb` is framework-dependent (it does
**not** bundle or install .NET) — a compatible .NET 10 desktop runtime must already be present on the target.

### Installed launch path & runtime requirement (root cause of the "flash then vanish" bug)
The first `.deb` made `/usr/bin/femvoice-studio` simply `exec` the published **apphost**
(`/opt/femvoice-studio/FemVoice.Avalonia`). A framework-dependent apphost only resolves a **system-registered**
runtime (`DOTNET_ROOT`, `/etc/dotnet/install_location_*`, or `/usr/share/dotnet`). On machines where .NET is
installed elsewhere (e.g. a user-local install on `PATH`), the apphost prints *"You must install .NET to run
this application"* and exits **131** — the window appears briefly in the taskbar/dock and then disappears.

The launcher is now a small `bash` script instead:
```bash
#!/usr/bin/env bash
set -euo pipefail
APP_DIR="/opt/femvoice-studio"
APP_DLL="$APP_DIR/FemVoice.Avalonia.dll"
if ! command -v dotnet >/dev/null 2>&1; then
  echo "FemVoice Studio requires the .NET runtime to be installed." >&2
  echo "Install the matching .NET desktop/runtime package, then run femvoice-studio again." >&2
  exit 127
fi
cd "$APP_DIR"
exec dotnet "$APP_DLL" "$@"
```
This resolves the runtime from `PATH` (so it works wherever `dotnet` is reachable) and, when `dotnet` is missing,
prints a clear message and exits `127` instead of flashing and dying. The launcher uses no `sudo`, installs
nothing, and writes no user/system state.

### Install & run
```
sudo apt install ./artifacts/packages/deb/femvoice-studio_0.1.0_amd64.deb   # install (requires .NET runtime on PATH)
femvoice-studio --smoke      # headless self-check
femvoice-studio              # launch the desktop preview
cat /usr/share/doc/femvoice-studio/copyright
```
If `dotnet` is not installed, install a compatible .NET 10 desktop runtime first (this package is
framework-dependent by design and does not bundle .NET). A self-contained `.deb` that bundles the runtime is a
**separate, deferred** packaging slice.

## Templates
- `macos/Info.plist` — **inert** macOS `.app` bundle metadata (incl. a static `NSMicrophoneUsageDescription`
  readiness string; no real capture exists yet). Not wired into any build step yet.
- `linux/femvoice-studio.desktop` — Linux desktop entry. **Installed by `package-deb.sh`** into
  `/usr/share/applications/`; the default `dotnet build/publish` does not consume it.
- `linux/debian-copyright` — machine-readable copyright, installed as `/usr/share/doc/femvoice-studio/copyright`.
- `linux/README.Debian` — short runtime/launch note, installed as `/usr/share/doc/femvoice-studio/README.Debian`.

macOS `.app`/`.dmg` bundling is now available (unsigned, readiness): `macos/package-app.sh <rid>` assembles
`artifacts/dist/<rid>/FemVoice Studio.app` from the publish output (consuming `Info.plist`; runs on any OS, no
signing), and `macos/package-dmg.sh <rid>` builds a `.dmg` when `hdiutil` is available (macOS) or skips gracefully
otherwise. Both support `--check`/`--dry-run`/`--help` and require no secrets. See `macos/README.md`. The `.app` is
**icon-ready**: `Info.plist` wires `CFBundleIconFile = AppIcon` and `package-app.sh` bundles `macos/AppIcon.icns`
into `Contents/Resources/` only if present (absent today — production icon deferred, see
`macos/AppIcon.icns.README.md`; covered by `--macos-icon-readiness-smoke`). A self-contained `.deb` and real
signing/notarization remain **deferred**.

## Signing / notarization readiness (future — no real signing today)
Local packages are **unsigned** and that flow is fully supported. Signing/notarization is **deferred**; only
readiness (docs + dry-run/check scripts) is in place. No secrets, certificates, GPG keys, or Apple account are
required or committed, and signing is **not** wired into `package-deb.sh`/`publish-macos.sh`.
- Linux `.deb`: see `linux/SIGNING.md`; check with `linux/signing-readiness.sh --check` (or `--dry-run`/`--help`).
- macOS codesign/notarize: see `macos/NOTARIZATION.md`; check with `macos/notarization-readiness.sh --check`.

Both scripts are POSIX `sh`, exit `0` in `--check`/`--dry-run` **without secrets** (even when `gpg`/`dpkg-sig`/
`codesign`/`xcrun` are absent — reported as optional, future-only), and **never print env-var values**. Optional
env vars are documented only (e.g. `FEMVOICE_DEB_SIGNING_KEY_ID`, `APPLE_NOTARY_PROFILE`) — never required for
local builds, never committed. The read-only `--signing-readiness-smoke` covers the readiness surface (docs +
scripts + dry-run/check/help flags + unsigned-flow-intact + signing-not-wired-into-build + no-secrets); see
`docs/AVALONIA_SIGNING_NOTARIZATION_READINESS_REPORT.md`.

## Verified (on Linux)
Framework-dependent publish for `linux-x64` and `osx-x64` completed and produced a valid apphost plus the
expected managed DLLs (`FemVoice.Core`, `FemVoice.Audio.Abstractions`, `Avalonia`, `Tmds.DBus.Protocol` — and
NOT `FemVoice.Audio.Windows`). The published `linux-x64` app runs `--smoke` OK via the shared .NET runtime
(`dotnet FemVoice.Avalonia.dll --smoke`). Note: a standalone framework-dependent apphost additionally needs a
system/registered .NET runtime; on a user-local-SDK box, launch via `dotnet <app>.dll` or publish
self-contained.

The helper scripts were exercised end-to-end: `publish-linux.sh linux-x64` + `publish-macos.sh osx-x64`
published successfully, and `package-deb.sh linux-x64` produced `femvoice-studio_0.1.0_amd64.deb` with the
expected layout (`/opt/femvoice-studio/FemVoice.Avalonia.dll`, `/usr/bin/femvoice-studio`,
`/usr/share/applications/femvoice-studio.desktop`, `/usr/share/doc/femvoice-studio/{copyright,README.Debian}`),
`root:root` ownership, and `Maintainer: A hansen <rassyhansen@gmail.com>`.

The new launcher was verified directly (the exact script the `.deb` installs): with `dotnet` **on PATH** it runs
`--smoke` OK and launches the GUI (window stays up); with `dotnet` **absent** it prints the clear message and
exits `127`. A real GUI launch via the launcher stayed alive (no apphost ".NET not found"/exit-131 flash). The
old (apphost) launcher was confirmed to fail with exit `131` on this user-local-runtime box, reproducing the
reported "flash then vanish". See `docs/AVALONIA_DESKTOP_PACKAGING_READINESS_GATE_RESULTS.md` and
`docs/AVALONIA_DESKTOP_PACKAGING_READINESS_SLICE_REPORT.md`.
