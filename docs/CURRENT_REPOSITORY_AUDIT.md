# FemVoice Studio — Current Repository Audit (WPF Baseline)

Audit date: 2026-06-16
Scope: Read-only audit of the finished WPF reference implementation prior to an Avalonia UI port.
Source of truth: the code, `.csproj`/`.slnx`, package references, and tests. Where documentation conflicted with code, code wins.

> This document describes **what exists today**. It is not an Avalonia plan and proposes no behavioural change. Companion documents: [`CURRENT_PROJECT_STRUCTURE.md`](CURRENT_PROJECT_STRUCTURE.md), [`CURRENT_PACKAGE_INVENTORY.md`](CURRENT_PACKAGE_INVENTORY.md), [`WPF_DEPENDENCY_MAP.md`](WPF_DEPENDENCY_MAP.md), [`CURRENT_FEATURE_MATRIX.md`](CURRENT_FEATURE_MATRIX.md), [`CURRENT_RUNTIME_WORKFLOWS.md`](CURRENT_RUNTIME_WORKFLOWS.md), [`CURRENT_REPORTS_AND_LOCALIZATION.md`](CURRENT_REPORTS_AND_LOCALIZATION.md), [`CURRENT_AUDIO_PIPELINE.md`](CURRENT_AUDIO_PIPELINE.md), [`CURRENT_DIAGNOSTICS_AND_EVIDENCE.md`](CURRENT_DIAGNOSTICS_AND_EVIDENCE.md), [`AVALONIA_PORT_READINESS_NOTES.md`](AVALONIA_PORT_READINESS_NOTES.md), [`AUDIT_SUMMARY_FOR_AVALONIA_PLANNING.md`](AUDIT_SUMMARY_FOR_AVALONIA_PLANNING.md).

## Confidence labels

Every claim in this doc set is tagged:

- **CONFIRMED** — read directly from code/project files.
- **PARTIAL** — inferred from strong but incomplete evidence (e.g. grep without full call-graph).
- **UNKNOWN / NEEDS REVIEW** — could not be confirmed from the repository; flagged, not invented.
- **OUTDATED** — existing documentation contradicts the current code.

## 1. Solution & project topology — CONFIRMED

- Solution file: **`FemVoiceStudio.slnx`** (new XML solution format, not `.sln`). Contains exactly two projects:
  - `FemVoiceStudio/FemVoiceStudio.csproj` — the application.
  - `FemVoiceStudio.Tests/FemVoiceStudio.Tests.csproj` — the xUnit test project.
- **No** `Directory.Build.props`, `Directory.Packages.props`, or central package management. All package versions are declared inline per-project.

### Main project (`FemVoiceStudio.csproj`) — CONFIRMED

| Property | Value |
| --- | --- |
| SDK | `Microsoft.NET.Sdk` |
| `OutputType` | `WinExe` |
| `TargetFramework` | `net10.0-windows` |
| `UseWPF` | `true` |
| `Nullable` | `enable` |
| `ImplicitUsings` | `enable` |
| `ApplicationIcon` | `..\logo.ico` |
| `GenerateProgramFile` | `false` |

Startup object: the WPF `App` class (`FemVoiceStudio/App.xaml` + `App.xaml.cs`). There is no `Program.cs`/explicit `Main`.

### Test project (`FemVoiceStudio.Tests.csproj`) — CONFIRMED

- `TargetFramework` = `net10.0-windows`, `IsTestProject=true`, `ProjectReference` → main app.
- Contains a **commented-out** `<ItemGroup>` that *would* exclude `ExerciseFeedbackEngineTests.cs`, `SmartCoachDecisionTests.cs`, `SafetyLockTests.cs`, `TestDatabaseService.cs` ("tests with pre-existing issues"). The block is inactive, so those compile today (note `ExerciseFeedbackEngineTests.cs` is not actually present in the folder).

## 2. Codebase size — CONFIRMED

- Main project: **263 `.cs` files**, **27 `.xaml` files** (18 Windows + ~5 UserControls + App/Themes/Resources/Icons).
- Test project: **~130 test `.cs` files** (xUnit).
- Plus 4 stray test files inside the **main** project at `FemVoiceStudio/Tests/` (see §5 concern C).

## 3. Key technology choices — CONFIRMED

| Concern | Technology |
| --- | --- |
| UI framework | WPF (XAML + code-behind), .NET 10, Windows-only |
| MVVM | Two stacks coexist: CommunityToolkit.Mvvm 8.2.2 (`ObservableObject`/source-gen) **and** a hand-rolled `ViewModelBase`/`RelayCommand` |
| DI | `Microsoft.Extensions.DependencyInjection` 8.0.0, wired entirely in `App.xaml.cs.ConfigureServices` |
| Audio capture | NAudio 2.2.1 (WASAPI + WaveIn), Windows-only |
| Charts (UI) | OxyPlot.Wpf 2.1.2 |
| PDF reports | QuestPDF 2026.5.0 (Community license) |
| Persistence | SQLite via Microsoft.Data.Sqlite 8.0.0; single shared `femvoice.db` |
| Localization | `System.Resources.ResourceManager` + RESX; neutral language is **Norwegian** |
| Tests | xUnit 2.6.2 |

## 4. Architecture in one paragraph — CONFIRMED

A single Windows WPF executable. `App.OnStartup` runs an RC-0 evidence bootstrap (before DI), registers global exception logging, builds the DI container, initializes theme/debug, optionally shows first-time setup, then opens `MainWindow`. The **domain core** (Services/, Models/, Audio DSP, Data/) is overwhelmingly UI-framework-free C# (verified: only 3 service files import `System.Windows`). UI concerns (Views, ViewModels, Converters, Themes) and hardware/audio capture (NAudio) are the Windows-coupled layers. The product enforces a clinical priority hierarchy **Safety > Health > Recovery > Comfort > Voice Development > Reporting**, machine-enforced primarily by the `FeedbackPriority` enum + `FeedbackConsistencyGuard` and by the progression safety gates.

## 5. Material findings & concerns (code is source of truth)

### A. Dead / legacy parallel architecture — CONFIRMED
`Subsystems/*` (Audio/Analysis/Data/Progression/SmartCoach interfaces+impls), `Infra/DependencyInjection.cs` (`AddFemVoiceStudio`), and `ViewModels/ViewModelBase.cs` / `SubsystemViewModelBase` are **never referenced** outside their own folders (grep returned zero external hits). The live wiring is `App.xaml.cs.ConfigureServices`; `AddFemVoiceStudio` is never called. Treat these as legacy. Recommendation: exclude from the port (document as dead code; deletion is a separate cleanup pass, not part of this audit).

### B. Stale documentation references — CONFIRMED / OUTDATED
The existing feature overview (`work-documents/FemVoice Funksjonsoversikt.md`) referenced several files that **do not exist** or live elsewhere. These are corrected in the updated overview and fully enumerated in [`CURRENT_REPORTS_AND_LOCALIZATION.md`](CURRENT_REPORTS_AND_LOCALIZATION.md) and the overview itself. Highlights:
- `Views/ExerciseSummaryView.xaml`/`ViewModel`, `Views/LiveFeedbackView.xaml`/`ViewModel`, `Services/CoachMessageGenerator.cs`, `Services/CoachMessageFormatter.cs`, `Services/SmartCoachExerciseAdapter.cs`, `VoiceHealthModule`, `VocalHealthLegacyBridge`, `AudioAnalysisEngine part2.cs` — **DO NOT EXIST**.
- The five "feedback mapper" files (`SmartCoachFeedbackMapper`, `ProgressionFeedbackMapper`, `HydrationFeedbackMapper`, `VocalHealthFeedbackMapper`, `InlineCoachFeedbackMapper`) are real classes but **all live inside `Services/FeedbackPipeline.cs`** (plus `MainScreenFeedbackMapper`), not standalone files.
- `ExerciseEffectivenessProvider` is a fictional name for `Services/ExerciseEffectivenessEngine.cs`.

### C. Test code compiled into the production executable — CONFIRMED (concern)
`FemVoiceStudio/Tests/` (4 files: `DirectionAnalyzerTests.cs`, `FemVoiceScoreTests.cs`, `LevelClassificationSystemTests.cs`, `VoiceProfileExtensionsTests.cs`) is inside the **main** WinExe project, which uses default SDK globbing and **directly references xUnit + Microsoft.NET.Test.Sdk**. So xUnit-attributed test classes ship inside the consumer executable, and `FemVoiceScoreTests.cs` is duplicated by name in both the app and the test project. Recommendation (documented, not applied): exclude `Tests/**` from the main csproj and remove test packages from the app project.

### D. RESX naming inconsistencies — CONFIRMED
Three malformed resource files in `FemVoiceStudio/Resources/`:
- `Strings_en.resx` (underscore) — not a valid culture satellite; effectively orphaned.
- `String.pt-BR.resx` (singular "String") — wrong base name, so `pt-BR` is **not** wired into the ResourceManager; pt-BR is effectively absent.
- `Strings.resx.old` — backup artifact.
Also: the csproj declares a `ResXFileCodeGenerator` → `Strings.Designer.cs`, but **`Strings.Designer.cs` does not exist**; all access goes through `LocalizationService`. See [`CURRENT_REPORTS_AND_LOCALIZATION.md`](CURRENT_REPORTS_AND_LOCALIZATION.md).

### E. Dormant SQL artifacts — NEEDS REVIEW
`Resources/DatabaseSchema.sql` (16 tables) and `Data/migrations/001_exercise_feedback_system.sql` (5 tables + 2 views) do **not** appear to be executed at runtime — the live schema is built from an inline batch in `DatabaseService.CreateSchema` plus per-store `CREATE TABLE IF NOT EXISTS`. The migration file additionally contains invalid SQLite (`ALTER TABLE ... ADD COLUMN IF NOT EXISTS`). Treat the `.sql` files as documentation/dormant until proven otherwise.

### F. Backup artifacts (`*.cs.old`, `_new`) — CONFIRMED
Numerous `*.cs.old`/`.old2` files exist (e.g. `App.xaml.cs.old`, `Data/DatabaseService.cs.old`, `ViewModels/MainViewModel.cs.old`, `Strings.resx.old`). These are not compiled (SDK globs `.cs`, not `.cs.old`). `Audio/AudioAnalysisEngine_new.cs` **is** compiled but contains only `using System;` (dead stub). Cleanup candidates; out of scope for this audit.

## 6. What is verified vs. what still needs review

| Area | Status |
| --- | --- |
| Project/solution structure, TFM, output type | CONFIRMED |
| Package list & versions | CONFIRMED (transitive graph not resolved offline — see package inventory) |
| WPF coupling map | CONFIRMED (grep-backed) |
| Domain core is UI-free | CONFIRMED (only 3 service files touch `System.Windows`) |
| Audio capture is NAudio/Windows-only | CONFIRMED |
| Report types (4) & formats (3) | CONFIRMED |
| Localization languages effectively loadable | PARTIAL (≈19 of 20 nominal; pt-BR dead) |
| Whether `AudioAnalysisEngine` capture is ever started in production | PARTIAL |
| `AsyncAudioPipeline` / `RealtimeAnalysisEngine` usage in production | PARTIAL (appear unused) |
| DatabaseSchema.sql / migration 001 execution | NEEDS REVIEW (appear dormant) |
| `.cs.old`/`_new` compilation exclusion | NEEDS REVIEW (globbing-based reasoning) |

## 7. Hard constraints reaffirmed

No clinical scoring, SmartCoach, Voice Health, persistence, analytics, diagnostics, report content, localization resources, or exercise definitions were modified by this audit. All proposed changes are recorded as recommendations only.
