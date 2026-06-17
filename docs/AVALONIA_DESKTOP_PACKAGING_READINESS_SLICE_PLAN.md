# Avalonia macOS/Linux Packaging Readiness — Slice Plan

Date: 2026-06-17 · Branch: `avalonia-desktop-packaging-readiness-slice` (off `main` @ `a2b077d`, incl. PR #1–#15).

> **Status: IMPLEMENTED (Linux-verified, headless).** Packaging/build-infrastructure only; behavior-neutral.
> No clinical/domain change · no WPF change · no Android/iOS heads · no real mic · no persistence · no
> report/support-package/export · no backup/restore · no SmartCoach/progression · no safety-gate · no
> Voice-Health/recovery. Linux/macOS readiness only. See `_SLICE_REPORT.md` / `_GATE_RESULTS.md`.

## 1. Goal
Prepare behavior-neutral cross-platform desktop packaging metadata for Linux/macOS and verify that publish
works — without changing runtime behaviour, adding real mic capture, persistence, or mobile heads.

## 2. Scope (implemented)
- **Edit** `FemVoice.Avalonia.csproj` — add a behavior-neutral packaging PropertyGroup:
  `RuntimeIdentifiers` = `linux-x64;linux-arm64;osx-x64;osx-arm64` (**plural** — enables RID publish/restore but
  does NOT force a RID on the default `dotnet build`/`dotnet run`); `UseAppHost=true`; **`PublishTrimmed=false`**
  (reflection bindings would break under trimming); app metadata (`Product`, `AssemblyTitle`, `Version`,
  `InformationalVersion`). No Windows target; `SelfContained` left unset (chosen at publish time).
- **New** `FemVoice.Avalonia/Packaging/macos/Info.plist` — **inert** macOS `.app` bundle template (NOT wired into
  the build), incl. a static `NSMicrophoneUsageDescription` readiness string (no real capture exists).
- **New** `FemVoice.Avalonia/Packaging/linux/femvoice-studio.desktop` — **inert** Linux desktop-entry template.
- **New** `FemVoice.Avalonia/Packaging/README.md` — documented publish commands + readiness notes.
- **Edit** `Program.cs` — `--packaging-smoke` (read-only metadata inspection).

## 3. Why behavior-neutral
`RuntimeIdentifiers` is plural so default build/run stay portable and unchanged (verified: 0-warning build, all
prior smokes green). The templates are plain files under `Packaging/`, not referenced by any MSBuild item, so
they don't affect build/publish output. Trimming is explicitly disabled. No new runtime dependency; refs stay
`FemVoice.Core` + `FemVoice.Audio.Abstractions`; `Tmds.DBus.Protocol` stays pinned 0.21.3.

## 4. Forbidden / not done
No real cross-platform audio capture (no PulseAudio/ALSA/PipeWire/CoreAudio/AVAudioEngine code), no mobile heads
(no Android/iOS/Maui/Xamarin projects), no runtime permission requests in code, no persistence, no file dialogs,
no export/package generation that writes outside build output, no WPF project/package change, no clinical change.

## 5. Smoke (`--packaging-smoke`)
Read-only: locate the csproj from the build output (`AppContext.BaseDirectory` up 3) and assert the 4 RIDs +
`RuntimeIdentifiers`, the Tmds 0.21.3 pin, `PublishTrimmed=false`, exactly 2 project refs (Core + Audio.Abstractions);
confirm the inert templates exist; and confirm at runtime the head references no `FemVoice.Audio.*` assembly
other than Abstractions. It embeds no forbidden token literals (verified via positive checks + the source leak guard).

## 6. Publish verification (optional, run this slice)
Framework-dependent publish for `linux-x64` and `osx-x64` succeeded; the published `linux-x64` app runs `--smoke`
OK via the shared runtime (`dotnet FemVoice.Avalonia.dll --smoke`). The published output contains Core +
Audio.Abstractions + Avalonia + Tmds.DBus.Protocol and NOT FemVoice.Audio.Windows. (No expensive RID matrix / no
self-contained or trimmed publish run.)

## 7. Gate
`dotnet build` (0 warnings) · all 15 smokes OK · `dotnet list --vulnerable` clean · `FemVoice.Tests.Portable`
baseline (1570/1580; 1569 acceptable due to the known ComfortZone flake) · leak guard: packaging slice files
introduce zero forbidden tokens (docs/negation comments aside) · Windows CI via PR.
