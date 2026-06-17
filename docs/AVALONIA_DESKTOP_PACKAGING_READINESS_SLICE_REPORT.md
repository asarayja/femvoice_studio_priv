# Avalonia macOS/Linux Packaging Readiness — Slice Report

Date: 2026-06-17 · Branch: `avalonia-desktop-packaging-readiness-slice` (off `main` @ `a2b077d`).

> **Packaging/build infrastructure only; behavior-neutral.** No clinical/domain behaviour changed · no WPF
> behaviour changed · no Android/iOS heads started · no real mic · no persistence · no report/support-package/
> export functionality · no backup/restore · no SmartCoach/progression · no safety-gate enforcement · no
> Voice-Health/recovery decisions · Linux/macOS readiness only, behavior-neutral.

## 1. What this slice does
Establishes behavior-neutral Linux/macOS desktop packaging readiness for the Avalonia head: declares supported
runtime identifiers, sets safe publish metadata (untrimmed, app version/title), adds inert macOS/Linux packaging
templates, documents the publish commands, adds a read-only `--packaging-smoke`, and verifies that publish works.

## 2. Files changed
- **Edit** `FemVoice.Avalonia/FemVoice.Avalonia.csproj` — packaging PropertyGroup: `RuntimeIdentifiers`
  (`linux-x64;linux-arm64;osx-x64;osx-arm64`, plural), `UseAppHost=true`, `PublishTrimmed=false`, app metadata.
- **New** `FemVoice.Avalonia/Packaging/macos/Info.plist` — inert macOS bundle template (static `NSMicrophoneUsageDescription`).
- **New** `FemVoice.Avalonia/Packaging/linux/femvoice-studio.desktop` — inert Linux desktop-entry template.
- **New** `FemVoice.Avalonia/Packaging/README.md` — documented publish commands + readiness/verification notes.
- **Edit** `FemVoice.Avalonia/Program.cs` — `--packaging-smoke` (read-only).
- **Docs** this report + `_SLICE_PLAN.md` + `_GATE_RESULTS.md` + tracker.

No files under `FemVoiceStudio/`, `FemVoice.Core/`, or `FemVoice.Audio.Windows/`. No `.github/workflows/` change.

## 3. Behavior-neutrality (verified)
`RuntimeIdentifiers` is plural, so `dotnet build` / `dotnet run` stay portable and unchanged — confirmed by the
0-warning build and all 15 smokes passing. The `Info.plist` / `.desktop` are plain files under `Packaging/`, not
referenced by any MSBuild item, so they don't alter build/publish output. Trimming is explicitly disabled
(`PublishTrimmed=false`) because the head uses reflection bindings. No new runtime dependency; refs unchanged
(Core + Audio.Abstractions); `Tmds.DBus.Protocol` pinned 0.21.3.

## 4. Forbidden / not done
No real cross-platform audio capture (no PulseAudio/ALSA/PipeWire/CoreAudio/AVAudioEngine/NAudio code), no mobile
heads (Android/iOS/Maui/Xamarin), no code-side runtime permission requests, no persistence, no file dialogs, no
export/package creation outside build output, no WPF project/package change, no clinical/domain change.

## 5. Publish verification (run this slice, Linux host)
- `dotnet publish -r linux-x64 --self-contained false` → succeeded; produced a valid apphost + managed DLLs.
- `dotnet publish -r osx-x64 --self-contained false` → succeeded; produced a valid apphost + managed DLLs.
- Published `linux-x64` output runs `--smoke` OK via the shared runtime (`dotnet FemVoice.Avalonia.dll --smoke`).
- Published output contains `FemVoice.Core`, `FemVoice.Audio.Abstractions`, `Avalonia`, `Tmds.DBus.Protocol`, and
  NOT `FemVoice.Audio.Windows`. (Standalone FDD apphost needs a system/registered runtime; on a user-local-SDK box
  launch via `dotnet <app>.dll` or publish self-contained — a host-resolution detail, not a publish defect.)

## 6. Verification (see `_GATE_RESULTS.md`)
Build 0 warnings · all 15 smokes OK (incl. `--packaging-smoke`) · no vulnerable packages · leak guard: packaging
slice's new source/project/template files introduce zero forbidden tokens (docs/negation comments aside) · refs
only Core + Audio.Abstractions · Tmds 0.21.3 · portable 1570/1580 · Windows CI = pending PR.

## 7. Behaviour changes
**None to clinical/domain behaviour. WPF untouched.** Packaging metadata + inert templates only; default
build/run behaviour is unchanged.
