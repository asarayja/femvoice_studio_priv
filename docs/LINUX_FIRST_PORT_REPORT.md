# Linux-First Portable Core — Final Report

Date: 2026-06-16 · Branch: `linux-portable-core` · SDK: .NET 10.0.301 (Ubuntu 26.04, linux-x64).

This phase made the portable parts of FemVoice Studio **build and test on Linux**, kept WPF as the frozen Windows reference, and stood up a minimal Avalonia shell — all without changing product behaviour.

## 1. What builds on Linux ✅
- `FemVoice.Audio.Abstractions` (net10.0) — 0 warn / 0 err.
- `FemVoice.Core` (net10.0) — 0 warn / 0 err. Holds the entire UI-free domain: Models (53), Services (112: scoring, SmartCoach, VocalHealth/recovery/safety gates, progression, analytics, feedback pipeline + 6 mappers, ReportAssembler/ExportWriter/QuestPDF, LocalizationService, RC-0 diagnostics, research anonymization, SQLite stores), Data (6 + migrations), Audio DSP (13), and the RESX (19 culture satellites generated, neutral Norwegian).
- `FemVoice.Tests.Portable` (net10.0) — 0 warn / 0 err.
- `FemVoice.Avalonia` (net10.0) — 0 err (2 transitive-advisory warnings).
- Reproduce: `bash scripts/linux-portable-gate.sh`.

## 2. What does NOT build on Linux, and why
- `FemVoiceStudio` (the WPF app, `net10.0-windows` + `UseWPF`) and `FemVoiceStudio.Tests` (`net10.0-windows`). WPF requires the Windows Desktop workload (Windows-only); there is no Linux WPF. This is by design ("do not build the WPF app on Linux") — it stays the frozen Windows reference and must be built/tested on Windows. Its csproj was updated (ProjectReference to Core + Abstractions; removed the moved RESX block) but those WPF-side edits are **unverified on Linux**.

## 3. Portable projects created
`FemVoice.Core`, `FemVoice.Audio.Abstractions`, `FemVoice.Tests.Portable`, `FemVoice.Avalonia` (all net10.0). The prompt's suggested separate `Reports`/`Localization`/`Diagnostics`/`Audio.Dsp` assemblies were folded into `FemVoice.Core` to avoid circular references; splitting later is mechanical. See `LINUX_PORTABLE_EXTRACTION_PLAN.md` and `LINUX_SDK_AND_TFM_DECISION.md`.

## 4. Tests moved to the portable test project
**101 of 131** test files → `FemVoice.Tests.Portable` (runs on Linux). Includes all prioritized safety/clinical suites — all green: SafetyOverrideInvariant, SafetyPriorityEngine, ManualOverrideClamp, FeedbackPriorityMatrix, FeedbackConsistencyGuard, ProgressionSafetyGate, RecoveryAwareTargetZone, RecoveryScorer, RecoveryIntelligenceService, FemVoiceScore(+Engine), ClinicalSessionScore, VoiceIntelligenceScorer, SmartCoach*, ReportAssembler, ExportWriter, ResearchAnonymizer/Aggregator/NoPii, MicrophoneCalibration, DSP. (Only **file-location paths** were updated where tests read moved RESX/source — no assertions changed.)

## 5. Tests still Windows-only
**30 of 131** kept in `FemVoiceStudio.Tests` (net10.0-windows): WPF/ViewModel/theme-XAML tests and tests referencing WPF-stay services (ThemeManager, IconProvider, AnalysisChartTheme, FeedbackService, LocalBackupService, SupportPackageService, PrivacyConsentPolicy) or NAudio capture. Full list in `LINUX_TEST_CLASSIFICATION.md`. Run these on Windows CI.

## 6. Audio abstraction status
`IAudioCaptureService` + DTOs created in `FemVoice.Audio.Abstractions`, with `NoopAudioCaptureService` and `SyntheticAudioCaptureService` (Linux/test/bootstrap). DSP moved into Core unchanged. **NAudio capture stays in the WPF app**; the Windows `NAudioAudioCaptureService : IAudioCaptureService` adapter and the `FemVoice.Audio.Windows` project are a Windows-side follow-up (cannot build/verify on Linux). Cross-platform real capture is out of scope. See `LINUX_AUDIO_BOUNDARY_NOTES.md`.

## 7. Avalonia bootstrap status
Minimal `FemVoice.Avalonia` head builds; headless `--smoke` proves shared `FemVoice.Core` services resolve via DI on Linux (localization returns "Ja", scoring type resolves, capture behind the abstraction). **No product views ported** — it is a bootstrap shell only. See `AVALONIA_LINUX_BOOTSTRAP_STATUS.md`.

## 8. Behaviour changes: **NO**
No clinical scoring, FemVoice score, SmartCoach, Voice Health, recovery, safety gates, progression, mastery, comfort-zone, exercise catalog, SQLite schema/stores, analytics, report content, research anonymization, RC-0 diagnostics, or localization **semantics** were changed. Changes were: (a) `git mv` relocations (namespaces preserved), (b) two behaviour-neutral type extractions (`AppSettings`/`AppTheme`/`DebugSettings`/`AppSettingsJson` and the `ResonanceCategory` enum) into Core in their original namespaces, (c) an additive `InternalsVisibleTo`, (d) new projects/abstractions/Avalonia shell, (e) test **file-location path** updates (no assertion changes), (f) csproj/slnx wiring. RESX content is byte-identical (moved, not edited).

## 9. Failing tests
**10 of 1580** portable test cases fail — **all pre-existing, none caused by the port** (proven via git `HEAD`: identical test code + byte-identical RESX; the placeholder quirk appears even in English):
- 9× `NewLanguageResourcesTests.NewFile_PreservesPlaceholdersPipesAndGlobs` — key `Report_RecommendationHighFatigueFormat` placeholders `{0} {0} {1:F1}` vs neutral `{0} {1:F1}`.
- 1× `ExerciseGuideEncodingTests.ResourceFiles_NoMojibake_All12Resx` — asserts 12 resx; repo has 21 (stale since the 9-language expansion).
Not fixed here (hard rule: don't change localization resources/assertions). Recommended as a separate, approved product fix.

Additionally, **1 pre-existing intermittent flake** (`ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate`) appears ~1 run in 4 (failure counts across 9 runs: 10,10,11,10,10,11,10,10,10) — a timing-sensitive event test under xUnit parallel load; `ComfortZoneController` was moved verbatim, so it is not a port regression. **Stable baseline: 1570 pass / 10 fail; 0 regressions.**

## 10. Recommended next phase
1. **On Windows:** restore the real baseline (`dotnet build`/`test` of `FemVoiceStudio.slnx`), confirm the WPF app still compiles against `FemVoice.Core` + `FemVoice.Audio.Abstractions`, and run the 30 Windows-only tests green. This closes the only unverified gap.
2. **Create `FemVoice.Audio.Windows`** with a `NAudioAudioCaptureService : IAudioCaptureService` adapter; inject it in the Windows composition root.
3. **Then begin Avalonia UI parity** (per `AVALONIA_MIGRATION_TRACKER.md`): theme port → localization markup → navigation/main dashboard → pitch chart (OxyPlot.Avalonia) → exercise/SmartCoach/reports/settings. Build Avalonia tests as views land.
4. **Optional cleanup (separate, approved):** fix the 10 pre-existing localization-data issues; split `Reports`/`Localization`/`Diagnostics` out of Core; retire dead `Subsystems/**`+`Infra/**`; remove test packages from the WPF app project.

> Per the work order, do not continue to full UI port until the Windows baseline (step 1) is confirmed. The Linux portable foundation is now green and reproducible via `scripts/linux-portable-gate.sh`.

## Artifacts produced this phase
Docs: `LINUX_SDK_AND_TFM_DECISION.md`, `LINUX_TEST_CLASSIFICATION.md`, `LINUX_PORTABLE_EXTRACTION_PLAN.md`, `LINUX_AUDIO_BOUNDARY_NOTES.md`, `LINUX_PORTABLE_GATE_RESULTS.md`, `AVALONIA_LINUX_BOOTSTRAP_STATUS.md`, `LINUX_FIRST_PORT_REPORT.md`. Script: `scripts/linux-portable-gate.sh`. Projects: `FemVoice.Core`, `FemVoice.Audio.Abstractions`, `FemVoice.Tests.Portable`, `FemVoice.Avalonia`. All on branch `linux-portable-core` (not committed — awaiting your review).
