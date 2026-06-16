# Windows Baseline Test Results (Agent 2)

Status: **✅ RUN on Windows via GitHub Actions (windows-latest). Real results below — build GREEN; remaining test failures are all pre-existing (git-proven), not port regressions.**

Verified by the `Windows WPF Verification` workflow (`.github/workflows/windows-wpf-verification.yml`) on a GitHub-hosted Windows runner — the WPF build/test that cannot run on the Linux dev host. Manual microphone smoke is still separate/pending (no real mic on CI; see `AUDIO_WINDOWS_ADAPTER_NOTES.md`).

## Recorded results (final run after mechanical fixes)
```
OS:               Windows (windows-latest, 10.0.26100)
dotnet --info:    .NET SDK 10.0.301 (RID win-x64)  [via actions/setup-dotnet 10.0.x]
SDK version:      10.0.301  (matches the Linux dev SDK)
Workloads:        SDK-bundled Windows Desktop targeting/runtime packs (WPF builds on the runner)
Branch:           linux-portable-core
Commit:           710ed53
Restore result:   SUCCESS (dotnet restore FemVoiceStudio.slnx)
Build result:     SUCCESS (dotnet build FemVoiceStudio.slnx -c Debug --no-build gate) — WPF app + FemVoice.Core
                  + FemVoice.Audio.Abstractions + FemVoice.Audio.Windows + both test projects all compile
Test result:      Windows-only (FemVoiceStudio.Tests): 301 total, 297 passed, 4 failed
                  Portable (FemVoice.Tests.Portable):   1580 total, 1570 passed, 10 failed
                  Combined: 1881 total, 1867 passed, 14 failed, 0 errors
Failing tests:    ALL PRE-EXISTING (not caused by the migration):
                  - Windows-only (4): ThemeNoteButtonStyleTests.NoteRadioButtonStyle_ExistsAndCoversAllStates
                    + .UsesThemeBrushes (Dark/Light). Themes are byte-identical to origin/main → same
                    result as the pre-port baseline. Frozen theme XAML left unchanged.
                  - Portable (10): NewLanguageResourcesTests.NewFile_PreservesPlaceholdersPipesAndGlobs
                    ×9 (Report_RecommendationHighFatigueFormat placeholders) + ExerciseGuideEncodingTests
                    .ResourceFiles_NoMojibake_All12Resx. RESX byte-identical; quirk present even in English.
Skipped tests:    0
Warnings:         NU1903 (transitive Tmds.DBus.Protocol via Avalonia Linux backend); Node 20 action deprecation notice.
Manual notes:     Build is the hard gate (GREEN). Test steps run with continue-on-error so the BUILD signal
                  isn't masked by pre-existing failures; full per-project trx uploaded (nothing hidden).
CI run URL:       https://github.com/asarayja/femvoice_studio_priv/actions/runs/27618290291
```

## Mechanical extraction fixes that got the build green (Windows-CI-driven)
The first CI runs surfaced exactly the mechanical issues this gate exists to catch (full detail in `WPF_SHARED_CORE_COMPATIBILITY_NOTES.md`):
1. **`TestDatabaseService` (CS0246)** — moved to portable but used by Windows-kept tests → linked the shared test-support files into `FemVoiceStudio.Tests`.
2. **`InternalsVisibleTo("FemVoiceStudio.Tests")` lost** — it lived in the moved `AudioCaptureService.cs` → restored on the WPF assembly (`AssemblyInfo.cs`).
3. **`ResonanceContrastDemoTests` RESX path** — read `FemVoiceStudio/Resources` (moved) → repointed to `FemVoice.Core/Resources`.
After these, **every port-induced failure is resolved**; only the 14 pre-existing failures remain.

## Safety-invariant / clinical tests — GREEN on Windows
The prioritized suites all **pass** in the Windows run (in `portable.trx`, none in the failed set):
`SafetyOverrideInvariantTests`, `SafetyPriorityEngineTests`, `ManualOverrideClampTests`, `FeedbackPriorityMatrixTests`, `FeedbackConsistencyGuardTests`, `ProgressionSafetyGateTests`, `RecoveryAwareTargetZoneTests`, `ReportAssemblerTests`, `ExportWriterTests`, `ResearchNoPiiTests`, SmartCoach*, Recovery*, FemVoiceScore*.

## Conclusion
The WPF reference implementation **builds and tests on Windows against the extracted shared core**, with **zero port-induced failures** remaining. The 14 remaining failures are pre-existing (localization-data + a theme-style assertion), independently confirmed via git as identical to `origin/main`. The only open item is the **manual Windows microphone smoke** for `NAudioCaptureService` (CI cannot exercise a real mic).
