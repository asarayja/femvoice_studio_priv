# Avalonia Port — Baseline Build & Test Results (Step 1)

Date: 2026-06-16 · Author: Agent 1 (Build & Baseline Guardian) role.

## ⛔ Baseline build/test could NOT be executed in this environment — BLOCKED

This is reported honestly per the work-order rule "Do not hide failing tests." The baseline `dotnet build` / `dotnet test` were **not run** because the current environment cannot build the product, for two independent reasons:

1. **No .NET SDK is installed.** `command -v dotnet` returns nothing on this machine.
   ```
   $ command -v dotnet   → (not found)
   ```
2. **The product is Windows-only WPF.** `FemVoiceStudio.csproj` targets `net10.0-windows` with `<UseWPF>true</UseWPF>`, and `FemVoiceStudio.Tests.csproj` targets `net10.0-windows` and references the WPF app. WPF requires `Microsoft.NET.Sdk.WindowsDesktop`, which is **Windows-only**. Even with a .NET 10 SDK installed, this solution **cannot build or test on Linux/macOS**.

### Environment observed
| Item | Value |
| --- | --- |
| OS | Linux (Ubuntu, kernel 7.0.0-22-generic), x86_64 |
| .NET SDK | **Not installed** |
| Solution | `FemVoiceStudio.slnx` (net10.0-windows app + net10.0-windows xUnit tests) |
| Git | branch `main`, last commit `e9e0091 docs`; working tree has the new `docs/` set + the `Funksjonsoversikt` edit from the audit |

### Consequence for the port work order
- **Step 1 (baseline verification): BLOCKED here.** It must be performed on a Windows machine (or Windows CI) with the .NET 10 SDK + WPF workload.
- **Step 4 (first extraction "run tests" gate): cannot be satisfied here.** Because no build/test is possible, performing the destructive code move now would be unverifiable and would violate the "keep tests green after each phase" rule. The extraction is therefore **planned and fully specified** (see `docs/SHARED_PROJECT_EXTRACTION_PLAN.md`) but **not executed** until a build environment exists. See `docs/PHASE_1_EXTRACTION_REPORT.md` §Gate.

> **This is not a code failure.** Nothing indicates the WPF baseline is broken; it simply cannot be compiled in this Linux/no-SDK environment. Treat the baseline as "unverified-here", not "failing".

## How to capture the real baseline (on Windows)

Run on a Windows host with .NET 10 SDK + Desktop workload, from the repo root:

```powershell
dotnet --info                 # record SDK + workloads
dotnet build  FemVoiceStudio.slnx -c Debug   2>&1 | tee build-baseline.log
dotnet test   FemVoiceStudio.slnx -c Debug   2>&1 | tee test-baseline.log
```

Then paste the build/test summary tables into this file (replace this section) so subsequent phases have a real green/known baseline to diff against.

---

## Test-suite classification (static analysis — CONFIRMED from the audit)

Even without running them, the ~130 xUnit tests in `FemVoiceStudio.Tests/` (plus 4 stray files in `FemVoiceStudio/Tests/`) classify as follows for the port. All currently target `net10.0-windows` (Windows-bound). The goal is to migrate the **portable** majority onto a cross-platform `net10.0` test project (`FemVoice.Tests`) that runs on Linux/macOS CI, while a Windows-only test head (`FemVoice.Tests.Wpf`) keeps the UI-coupled tests.

### A. Portable core tests → target `FemVoice.Tests` (net10.0, runnable on Linux/CI)
Domain/logic tests with no WPF/Application.Current/Brush dependency, e.g.:
`FemVoiceScoreEngineTests`, `FemVoiceScoreTests`, `ClinicalSessionScoreTests`, `VoiceIntelligenceScorerTests`, `LevelClassificationSystemTests`, `SmartCoach*Tests` (most), `ExerciseRecommendationEngineTests`, `ExerciseEffectivenessEngineTests`, `LearningPathProfileTests`, `VocalHealthSupervisorTests`, `HydrationAdvisorTests`, `RecoveryScorerTests`, `RecoveryIntelligenceServiceTests`, `ProgressionOrchestratorTests`, `ProgressionAuthorityTests`, `MasteryEvaluatorTests`, `TrendEngineServiceTests`, `LongitudinalInsightEngineTests`, `VoicePatternDetectorTests`, `FeedbackConsistencyGuardTests`, `FeedbackPriorityMatrixTests`, `FeedbackSignalPolicyTests`, `InlineCoachPolicyTests`, `MainScreenFeedbackMapperTests`, `PitchDetectionServiceTests`, `FormantDetectionServiceTests`, `ResonanceProxyEngineTests`, `VocalWeightAnalyzerTests`, `PitchTraceStabilizerTests`, `PitchTargetZonePolicyTests`, `PitchChartAxisRangeCalculatorTests`, `MicrophoneCalibrationServiceTests`, `ReportAssemblerTests`, `ExportWriterTests`, `ReportLocalizationTests`, `ReportVerificationTrackerTests`, `ResearchAnonymizerTests`, `ResearchAggregatorTests`, `ResearchNoPiiTests`, `PrivacyConsentReadinessTests`, `AuditTrailStoreTests`, `AuditCompletenessTests`, `PilotReadinessCheckerTests`, `Rc0EvidenceExporterSignalClassificationTests`, `DiagnosticsNamingTests`, exercise-catalog/profile tests, store tests, etc.

> Caveat: these are runnable on Linux **only after** the code they exercise is moved into a cross-platform shared library and any transitive WPF/`Rc0RuntimeLog→ThemeManager` coupling is broken (see extraction plan). Until then they remain Windows-bound by transitive reference.

### B. Windows/WPF-only tests → keep in `FemVoice.Tests.Wpf` (net10.0-windows)
~10 files reference WPF/UI types (Brush/Color/Application.Current/ResourceDictionary/FrameworkElement/Dispatcher):
`AnalysisChartThemeTests`, `IconRenderingTests`, `ProgressionIconThemeTests`, `ExerciseSessionRecorderTests`, `ThemeButtonStyleTests`, `SmartCoachStressSensitiveTests`, `StressSensitiveExperienceTests`, `ThemeComboBoxStyleTests`, `ThemeNoteButtonStyleTests`, `ThemeResourceCoverageTests`. Also the layout/modality tests (`WindowModalBehaviorTests`, `SettingsWindowLayoutTests`) and viewmodel tests that touch theme brushes.

### C. Avalonia UI tests to CREATE (later phases)
New tests validating Avalonia theme variants, localization markup, OxyPlot.Avalonia chart hosting, dialog/file-dialog/dispatcher abstractions, navigation, and view parity. None exist yet.

### D. Audio hardware / manual tests
`AudioCaptureServiceTests`, `AudioSafetyTests` exercise capture behaviour; capture is hardware/Windows-bound (NAudio). Keep Windows-only; the cross-platform capture backend (out of scope now) will need its own harness + manual mic validation.

### E. RC-0 diagnostics tests (keep green; behaviour-frozen)
`Rc0EvidenceExporterSignalClassificationTests`, `Rc0SystemVerificationReportTests`, `DiagnosticsNamingTests`. Note `Rc0SystemVerificationReportTests` historically re-stamped the repo-root `RC0_VERIFICATION_*` files on every `dotnet test` (see `RC0_EVIDENCE_PIPELINE_ROOT_CAUSE_REPORT.md`) — watch for side effects.

---

## Safety-invariant tests — must remain GREEN after every phase

These are the guardrails proving the frozen clinical hierarchy is intact. **Do not change their assertions** unless an assertion is provably WPF-only and is being moved to the WPF test head (and even then, preserve the assertion's meaning):

- `SafetyOverrideInvariantTests`
- `SafetyPriorityEngineTests`
- `ManualOverrideClampTests`
- `FeedbackPriorityMatrixTests`
- `FeedbackConsistencyGuardTests`
- `ProgressionSafetyGateTests`
- `RecoveryAwareTargetZoneTests`

Acceptance for each port phase includes: "these tests still pass on the Windows baseline." Since they appear to be UI-free domain tests, they are also prime candidates to run in the cross-platform `FemVoice.Tests` project once the domain core is extracted — giving Linux/CI coverage of the safety invariants.

## Pre-existing test risks noted in the csproj
- The test `.csproj` has a **commented-out** exclusion block for `ExerciseFeedbackEngineTests.cs`, `SmartCoachDecisionTests.cs`, `SafetyLockTests.cs`, `TestDatabaseService.cs` ("tests with pre-existing issues"). These currently compile; confirm they pass on the real baseline (`ExerciseFeedbackEngineTests.cs` is not present in the folder).
- 4 test files in `FemVoiceStudio/Tests/` are compiled into the **main app** and `FemVoiceScoreTests.cs` is duplicated by name — flagged for the cleanup agent.
