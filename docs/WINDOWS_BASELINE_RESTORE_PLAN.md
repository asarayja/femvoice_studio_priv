# Windows Baseline Restore Plan (Agent 0 / Gate Captain)

Date: 2026-06-16 · Branch: `linux-portable-core`.

## Current branch
`linux-portable-core` (off `main` @ `e9e0091`). 6 commits added (see `COMMIT_BATCH_REVIEW_NOTES.md`). Working tree clean. Not pushed.

## Uncommitted files
None — all work is committed in reviewable batches.

## Planned commit batches
Done (batches 1–6). See `COMMIT_BATCH_REVIEW_NOTES.md`.

## Windows build requirements (to run the real baseline — Agent 2)
- Windows host (or Windows CI).
- .NET 10 SDK (≥ 10.0.301) **with the Desktop/WPF workload** (`dotnet workload install` is not needed for the SDK MSI, which bundles the Windows Desktop targeting/runtime packs).
- Commands:
  ```powershell
  dotnet --info
  dotnet restore FemVoiceStudio.slnx
  dotnet build   FemVoiceStudio.slnx -c Debug
  dotnet test    FemVoiceStudio.slnx -c Debug
  ```
- Solution updated: `FemVoiceStudio.slnx` now lists `FemVoice.Core`, `FemVoice.Audio.Abstractions`, `FemVoice.Audio.Windows`, `FemVoice.Tests.Portable`, `FemVoice.Avalonia`, `FemVoiceStudio`, `FemVoiceStudio.Tests`.

## Known Linux limitations (why the Windows steps can't run here)
- This workstation is **Linux** with a user-local SDK at `~/.dotnet` and **no WPF workload** (WPF is Windows-only). `FemVoiceStudio` (`UseWPF`) and `FemVoiceStudio.Tests` **cannot build on Linux**.
- Therefore Agent 2 (real WPF build + full test run) is **PENDING — must run on Windows**. Results are recorded as pending in `WINDOWS_BASELINE_TEST_RESULTS.md`; **no Windows results are invented**.
- What WAS verified on Linux: `FemVoice.Core`, `FemVoice.Audio.Abstractions`, `FemVoice.Audio.Windows` (via `EnableWindowsTargeting`), `FemVoice.Avalonia` all build; `FemVoice.Tests.Portable` runs 1570/1580 (10 pre-existing failures). See `LINUX_PORTABLE_GATE_RESULTS.md` / `AVALONIA_LINUX_GATE_RESULTS.md`.

## Risks
- WPF build against the shared core is **unverified on Linux**. Mitigations applied: namespaces preserved (no call-site changes), RESX RootNamespace preserved, ProjectReferences added, no WPF use of Core internals (verified). Residual risk is WPF-workload/packaging specifics only.
- The `App.xaml.cs` DI registration + csproj reference edits are unverified (Windows build will confirm).

## Rollback plan
- All changes are on branch `linux-portable-core`; `main` is untouched. `git checkout main` restores the exact prior baseline.
- Each batch is an independent commit → `git revert <hash>` per batch.
- The AudioCaptureService move and the two type extractions are pure relocations (revertible without logic loss).

## Tracker status
See `AVALONIA_MIGRATION_TRACKER.md`: Phase 0 = baseline (Windows-pending), Phase 1 extraction = done+Linux-verified/WPF-pending, Phase 2 abstractions = interfaces added, Phase 3 Audio.Windows = adapter added + Linux-compile-verified, manual Windows mic test pending.
