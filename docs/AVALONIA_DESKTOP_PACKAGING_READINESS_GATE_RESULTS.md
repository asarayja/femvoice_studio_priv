# Avalonia macOS/Linux Packaging Readiness — Gate Results

Date: 2026-06-17 · Branch: `avalonia-desktop-packaging-readiness-slice` · Host: Linux (.NET SDK 10, user-local `~/.dotnet`).

All commands from the repo root with `DOTNET_ROOT=$HOME/.dotnet`, `PATH=$HOME/.dotnet:$PATH`.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` (default, no `-r`) → **Build succeeded. 0 Warning(s) 0 Error(s).**
(Adding plural `RuntimeIdentifiers` did not disturb the default portable build/run.)

## Smokes (`dotnet run --project FemVoice.Avalonia --no-build -- --<smoke>`)
| Smoke | Result |
| --- | --- |
| `--smoke` … `--diagnostics-scaffold-smoke` (14 prior) | **all OK** |
| `--packaging-smoke` | **OK** |

### `--packaging-smoke` output
```
[pkg] csproj: found=True RIDs(linux-x64;linux-arm64;osx-x64;osx-arm64)=True Tmds-pin-0.21.3=True no-trim=True
[pkg] project refs: count=2 core+abstractions-only=True
[pkg] templates: macos/Info.plist=True linux/.desktop=True
[pkg] runtime refs: Core=True Abstractions=True no-other-FemVoice.Audio=True
[pkg] Packaging readiness smoke OK
```
Read-only verification: the csproj declares the 4 Linux/macOS RIDs + `RuntimeIdentifiers`, the Tmds 0.21.3 pin,
`PublishTrimmed=false`, and exactly 2 project refs (Core + Audio.Abstractions); the inert packaging templates
exist; and the running head references no `FemVoice.Audio.*` assembly other than Abstractions.

## Optional publish verification (run, Linux host)
```
dotnet publish … -r linux-x64 --self-contained false   -> succeeded (apphost 78 KB + managed DLLs)
dotnet publish … -r osx-x64   --self-contained false   -> succeeded (apphost 89 KB + managed DLLs)
dotnet /tmp/femvoice-publish-linux-x64/FemVoice.Avalonia.dll --smoke  -> [smoke] OK (exit 0)
```
Published `linux-x64` output contains `FemVoice.Avalonia.dll`, `FemVoice.Core.dll`,
`FemVoice.Audio.Abstractions.dll`, `Avalonia.dll`, `Tmds.DBus.Protocol.dll` — and NOT `FemVoice.Audio.Windows`.
(Standalone FDD apphost needs a system/registered .NET runtime; on this user-local-SDK box it is launched via
`dotnet <app>.dll`. No self-contained/trimmed publish and no full RID matrix were run.)

## Vulnerability scan
`dotnet list … --vulnerable --include-transitive` → **no vulnerable packages.** `Tmds.DBus.Protocol` pinned **0.21.3**.

## Project references
`FemVoice.Avalonia` references only **`FemVoice.Core`** + **`FemVoice.Audio.Abstractions`**.

## Leak guard
Base forbidden list → **CLEAN (zero real references)** (only the two pre-existing negation comments match & are excluded).

Packaging-specific list (`Android`, `iOS`, `Xamarin`, `Maui`/`MAUI`, `UIKit`, `AVAudioEngine`, `CoreAudio`,
`PulseAudio`, `ALSA`, `PipeWire`, `NAudio`, `Wasapi`, `WaveIn`, `SQLite`, `IDatabaseService`,
`ExerciseSessionRecorder`, `Save`, `Persist`, `SmartCoachEngine`, `ProgressionSafetyGate`, `VoiceHealth`,
`VocalHealthSupervisor`, `RecoveryScorer`, `RecoveryIntelligenceService`, `MicrophoneCalibration`, `OxyPlot`):
- **The slice's new source/project/template files** (`FemVoice.Avalonia.csproj` packaging additions,
  `Packaging/macos/Info.plist`, `Packaging/linux/femvoice-studio.desktop`) and the new `--packaging-smoke`
  Program.cs additions → **introduce ZERO forbidden tokens** (verified via `git diff` of the additions).
- Acceptable, non-introduced matches: `Packaging/README.md` is **documentation** mentioning target platforms in
  negations ("no … Android/iOS work") — explicitly allowed. `Program.cs` `notSaved` (the `Save` substring) is the
  **pre-existing** runtime-lifecycle smoke variable, not added by this slice and not a real `Save` API.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **Passed: 1570, Failed: 10, Total: 1580** (known baseline; 1569/1580 is the
documented intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake variant). No new failures.

## Windows CI
Pending PR (`windows-wpf-verification.yml`). Avalonia-only csproj/metadata + smoke; WPF build unaffected.

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** Packaging metadata + inert templates only; default build/run unchanged.

---

# Follow-up: Debian/Ubuntu `.deb` packaging readiness

Date: 2026-06-17 · Branch: `avalonia-desktop-packaging-deb-helpers-slice` · Host: Linux (.NET SDK 10, user-local `~/.dotnet`).

**State note:** PR #16 (the base packaging-readiness slice) was already **merged** into `main` per an explicit
merge instruction, so this `.deb` correction is delivered as a **follow-up PR off `main`** rather than amended
onto the merged PR.

## What this follow-up adds
Linux packaging now includes **`.deb` readiness**, not only a raw publish helper. New, behavior-neutral helper
scripts (framework-dependent by default, no root, no install, no `dpkg` maintainer hook scripts, no mic perms):
- `FemVoice.Avalonia/Packaging/linux/publish-linux.sh [rid]` — publish for a Linux RID → `artifacts/publish/<rid>`.
- `FemVoice.Avalonia/Packaging/linux/package-deb.sh [rid]` — build a `.deb` → `artifacts/packages/deb/`.
- `FemVoice.Avalonia/Packaging/macos/publish-macos.sh [rid]` — publish for a macOS RID → `artifacts/publish/<rid>`.
- `Packaging/linux/femvoice-studio.desktop` `Exec=` → `femvoice-studio` (matches the `/usr/bin` launcher).
- `--packaging-smoke` extended to verify the helpers (existence, csproj reference, `--self-contained false`
  default, `artifacts/publish` output, `dpkg-deb`, `artifacts/packages/deb`, `/opt/femvoice-studio`,
  `/usr/share/applications/femvoice-studio.desktop`, no `sudo`, no maintainer hook scripts, `Exec=femvoice-studio`).
- `/artifacts/` added to root `.gitignore` (publish/`.deb`/package-work output is never committed).

## Gate (follow-up branch)
- **Build** (all 3 portable projects, default no-`-r`): **Build succeeded, 0 Error(s).**
- **Smokes** (`--smoke` … `--packaging-smoke`): **15/15 OK** (extended `--packaging-smoke` prints all helper/`.deb` checks `True`).
- **Portable tests**: **Passed 1570, Failed 10, Total 1580** — known localization-data baseline; no new failures.
- **Vulnerable packages**: none (`dotnet list package --vulnerable --include-transitive`). `Tmds.DBus.Protocol` resolved `0.21.3` == requested.
- **Base leak guard** (non-comment forbidden tokens in `FemVoice.Avalonia`): **clean**.
- **Packaging leak guard** (platform-impl tokens): **clean** (no non-comment hits; doc/comment negations like
  "no microphone permissions" / "NOT `FemVoice.Audio.Windows`" are allowed metadata).

## Practical verification (scripts actually run)
- `publish-linux.sh linux-x64` → published; `dotnet artifacts/publish/linux-x64/FemVoice.Avalonia.dll --smoke` → **OK**.
- `package-deb.sh linux-x64` → built **`femvoice-studio_0.1.0_amd64.deb`** (~15 MB) under `artifacts/packages/deb/`.
  - `dpkg-deb -I`: Package `femvoice-studio`, Version `0.1.0`, Section `sound`, Architecture `amd64`, framework-dependent (no .NET install, no mic perms).
  - `dpkg-deb -c`: `/opt/femvoice-studio/FemVoice.Avalonia`, `/usr/bin/femvoice-studio`, `/usr/share/applications/femvoice-studio.desktop`; ownership **`root:root`** (via `--root-owner-group`, no root/`fakeroot`).
  - The `.deb` was **built but NOT installed** (per slice scope).
- `publish-macos.sh osx-x64` → published successfully.

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** Packaging helper scripts + inert template + smoke checks only; default build/run unchanged.

---

# Follow-up 2: installed Linux launch fix + Debian author/license metadata

Date: 2026-06-17 · Branch: `avalonia-desktop-packaging-deb-helpers-slice` (PR #17) · Host: Linux (.NET 10 user-local `~/.dotnet`).

## Investigation — root cause of "starts briefly then disappears"
Reproduced on the test box (the `.deb` was installed during manual testing):
- `which femvoice-studio` → `/usr/bin/femvoice-studio`; the installed launcher did `exec /opt/femvoice-studio/FemVoice.Avalonia` (the framework-dependent **apphost**).
- No system-registered .NET runtime exists (`command -v dotnet` → none; no `/usr/share/dotnet`, `/usr/lib/dotnet`, `/etc/dotnet`); .NET is installed user-locally at `~/.dotnet`.
- Running the apphost in a clean (GUI-like) env → `You must install .NET to run this application. … .NET location: Not found` → **exit 131**. The launcher (which only execs the apphost) reproduced the same exit 131. The GUI window appears briefly then vanishes.
- `dotnet /opt/femvoice-studio/FemVoice.Avalonia.dll --smoke` with the user-local runtime → **smoke OK, exit 0**.

**Root cause:** a framework-dependent **apphost** only resolves a *system-registered* runtime; with .NET installed elsewhere (user-local on PATH) it cannot find a runtime and exits 131. Not a missing native dep, not a bad `.desktop`, not a missing executable bit, not an Avalonia crash.

## Fix
- `/usr/bin/femvoice-studio` is now a small `bash` launcher: checks `command -v dotnet` (clear message + exit 127 if missing), `cd /opt/femvoice-studio`, `exec dotnet /opt/femvoice-studio/FemVoice.Avalonia.dll "$@"`. No sudo, no install, no state writes.
- `.desktop` keeps `Exec=femvoice-studio` (now resolves the script launcher).
- Package stays framework-dependent (no .NET bundled/installed).

## Debian metadata / license
- `DEBIAN/control`: `Maintainer: A hansen <rassyhansen@gmail.com>`, `Homepage: https://github.com/asarayja/femvoice_studio_priv`, framework-dependent `Description`. (Homepage included because this is an explicitly private/proprietary preview package, consistent with the copyright `Source`; it would be omitted for public distribution.)
- New `/usr/share/doc/femvoice-studio/copyright` (machine-readable, `License: Proprietary` — no LICENSE file in the repo, so no OSS license is invented) and `/usr/share/doc/femvoice-studio/README.Debian`.
- Templates: `Packaging/linux/debian-copyright`, `Packaging/linux/README.Debian`.

## Gate (this follow-up)
- **Build**: 0 warnings / 0 errors.
- **Smokes**: 15/15 OK (extended `--packaging-smoke` prints `launcher: …=True` and `metadata: …=True`).
- **Portable tests**: 1569/1580 — 10 known localization-data failures + the known intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake; no new failures (packaging-only change).
- **Vulnerable packages**: none; `Tmds.DBus.Protocol` 0.21.3.
- **Leak guards**: base + packaging clean; new packaging files (`.sh`, `debian-copyright`, `README.Debian`, `.desktop`) clean of forbidden runtime tokens.

## Practical verification
- `publish-linux.sh linux-x64` → published; published `--smoke` OK. `publish-macos.sh osx-x64` → OK.
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb` (clean, root:root). `dpkg-deb -I` shows the new Maintainer/Homepage/Description; `dpkg-deb -c` shows `/usr/bin/femvoice-studio`, `.desktop`, `copyright`, `README.Debian`, and `/opt/.../FemVoice.Avalonia.dll`. `copyright` present.
- **Launcher (the exact packaged script):** `dotnet` absent → clear message + exit 127; `dotnet` present → `--smoke` OK (exit 0); **real GUI launch stayed alive the full 10s** (no exit-131 flash).
- **Install note:** the box's `sudo` is not available non-interactively, so the new `.deb` was **not re-installed**; the fix is verified by running the exact packaged launcher script + by `.deb` inspection. Re-`apt install` to confirm the installed GUI (the launcher script is what gets placed at `/usr/bin/femvoice-studio`).

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** Launcher/metadata/doc + smoke checks only; default build/run unchanged.
