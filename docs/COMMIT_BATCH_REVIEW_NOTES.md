# Commit Batch Review Notes (Agent 1)

Date: 2026-06-16 · Branch: `linux-portable-core` (off `main` @ `e9e0091`). **Committed, not pushed.**

The previously-uncommitted portable-core work was committed in 6 reviewable batches. Working tree is clean. `bin/obj` are git-ignored (verified via `git check-ignore`); no build artifacts committed. Whitespace checked with `git diff --cached --check`.

| # | Commit | Subject | Scope |
| --- | --- | --- | --- |
| 1 | `bc89c8a` | Extract portable FemVoice.Core + FemVoice.Audio.Abstractions from WPF app | 221 files: code moves (Models/Services/Audio-DSP/Data/RESX → Core, as renames), Core csproj/AssemblyInfo, SettingsModels + ResonanceCategory extractions, Audio.Abstractions, ThemeManager/IAnalysisSubsystem edits, WPF csproj ref |
| 2 | `92ca285` | Add FemVoice.Tests.Portable (net10.0) and move portable tests | 101 test files moved (renames) + path-location fixes; 30 stay in FemVoiceStudio.Tests |
| 3 | `d391844` | Add minimal Avalonia Linux shell + platform abstraction interfaces | FemVoice.Avalonia/** + FemVoice.Core/Platform |
| 4 | `949bd9b` | Add audit + migration docs, Linux portable gate script; update solution and feature overview | docs/**, scripts/linux-portable-gate.sh, slnx, Funksjonsoversikt |
| 5 | `7abf648` | Add FemVoice.Audio.Windows: NAudio capture behind IAudioCaptureService | AudioCaptureService moved (rename) into Audio.Windows + NAudioCaptureService adapter; WPF ref + DI registration; slnx |
| 6 | (this docs batch) | Windows/audio gate docs | docs for this gate (see below) |

## Grouping decision
The extraction (batch 1) is one logical, interdependent change (moving the closed dependency graph + wiring references); it is **not** internally separable into "project structure" vs "core extraction" without leaving non-compiling intermediate states. It is committed as a single reviewable unit with clean rename detection (most files show 0 content change). Tests, Avalonia, docs, and the audio adapter are cleanly separable and were split out.

## Notes
- Renames are detected (e.g. moved files show `| 0` in `--stat`); reviewers can use `git log --follow` / `git show --find-renames`.
- No squashing of unrelated changes.
- Not pushed (no remote push requested).
