# FemVoice Studio — Current Project Structure (WPF Baseline)

Audit date: 2026-06-16 · Read-only · Source of truth: code + `.slnx`/`.csproj`.

## 1. Solution

`FemVoiceStudio.slnx` (XML solution format) references two projects:

```
FemVoiceStudio.slnx
├── FemVoiceStudio/FemVoiceStudio.csproj        (WinExe, net10.0-windows, UseWPF)
└── FemVoiceStudio.Tests/FemVoiceStudio.Tests.csproj  (xUnit, net10.0-windows)
```

No build props/targets files, no central package management. — CONFIRMED

## 2. Main project folder map — CONFIRMED

```
FemVoiceStudio/
├── App.xaml / App.xaml.cs        ← startup, DI composition root, splash, RC-0 bootstrap, global exception logging
├── AssemblyInfo.cs
├── Audio/                        ← capture + DSP (NAudio + pure-DSP analyzers)   [22 files]
├── Converters/                   ← WPF IValueConverters + localization MarkupExtensions  [8 files]
├── Data/                         ← SQLite data layer + interfaces + migrations/001
├── Docs/                         ← engineering checklists & RESX plans (markdown)
├── Infra/DependencyInjection.cs  ← LEGACY/DEAD DI (AddFemVoiceStudio — never called)
├── Models/                       ← domain DTOs/records/enums (UI-free)            [~70 files]
│   └── VoiceLoad/
├── Resources/                    ← RESX strings (20 locales + base), Icons.xaml, DatabaseSchema.sql (dormant)
├── Services/                     ← domain/business logic (scoring, coach, health, progression, reports, diagnostics)  [~130 files]
│   ├── FeedbackRuleEngine/
│   ├── Interfaces/
│   ├── Progression/
│   ├── SmartCoachModule/
│   └── VoiceLoad/
├── Subsystems/                   ← LEGACY/DEAD parallel architecture (never referenced externally)
│   ├── Analysis/  Audio/  Data/  Progression/  SmartCoach/
├── Tests/                        ← 4 xUnit test files compiled INTO the app (concern — see audit §5C)
├── Themes/                       ← LightTheme.xaml, DarkTheme.xaml (ResourceDictionary)
├── ViewModels/                   ← MVVM view-models (mostly CommunityToolkit ObservableObject)  [17 files]
└── Views/                        ← Windows + UserControls (.xaml + .xaml.cs)       [27 xaml]
```

## 3. Layering (intended vs. actual) — CONFIRMED

```
        ┌──────────────────────────────────────────────┐
        │  WPF UI shell  (Views, ViewModels, Converters, │  Windows-only
        │  Themes, App.xaml, ThemeManager)               │
        └───────────────┬────────────────────────────────┘
                        │ DI (App.xaml.cs.ConfigureServices)
        ┌───────────────▼────────────────────────────────┐
        │  Domain core  (Services/, Models/, FeedbackPipe)│  UI-free C# (portable)
        │  scoring · smartcoach · health · recovery ·     │
        │  progression · analytics · reports · diagnostics│
        └───────────────┬────────────────────────────────┘
        ┌───────────────▼────────────────────────────────┐
        │  Audio (Audio/)  → DSP analyzers are pure;      │  capture = NAudio/Windows
        │  capture (AudioCaptureService/AudioAnalysisEngine)│
        └───────────────┬────────────────────────────────┘
        ┌───────────────▼────────────────────────────────┐
        │  Data (Data/ + SQLite stores in Services/)      │  UI-free; shared femvoice.db
        └──────────────────────────────────────────────────┘
```

Verified UI-leak points in the lower layers (the only ones): `Services/AnalysisChartTheme.cs`, `Services/IconProvider.cs`, `Services/ThemeManager.cs` import `System.Windows`; `Services/ExportWriter.cs` references OxyPlot (comments only — no chart embedding); `Subsystems/Progression/*` use `ObservableCollection` (soft, `System.ObjectModel`). No `Dispatcher` anywhere in Services/Models/Subsystems. — CONFIRMED via grep.

## 4. Composition root — CONFIRMED

All DI is in `App.xaml.cs.ConfigureServices` (~270 lines). Highlights:

- **Data:** `DatabaseService` (singleton, schema init guarded once), inline `Sqlite*` repositories/stores all pointed at `~/Documents/FemVoiceStudio/femvoice.db`.
- **Audio/score engines (singletons):** `ResonanceProxyEngine`, `FemVoiceScoreEngine`, `ComfortZoneController`, `ExerciseIntelligenceCoordinator`.
- **Health/recovery:** `VocalHealthBaselineProvider` (+ options factories), `VocalHealthSupervisor`, `HydrationAdvisor`, `RecoveryScorer`, `RecoveryIntelligenceService`, `ProgressionSafetyGate`, `MasteryEvaluator`.
- **Feedback:** `FeedbackConsistencyGuard`, `FeedbackPipeline`, and the mappers (`ProgressionFeedbackMapper`, `SmartCoachFeedbackMapper`, `InlineCoachFeedbackMapper`, `VocalHealthFeedbackMapper`, `HydrationFeedbackMapper`) — all defined inside `FeedbackPipeline.cs`.
- **Coach/analytics:** `SmartCoachEngine` (large factory with ~14 collaborators), `LearningPathProfileBuilder`, `ComplexityEngine`, `ExerciseEffectivenessEngine`, `TrendEngineService`, `VoicePatternDetector`, `LongitudinalInsightEngine`, `RecommendationExplanationEngine`, `SmartCoachMemoryStore`, `VoiceKnowledgeGraphBuilder`.
- **Professional/Research:** five SQLite stores (`OutcomeProfileStore`, `ManualOverridesStore`, `ClinicalNotesStore`, `AuditTrailStore`, `CaseReviewsStore`) + `OutcomeProfileBuilder`, `ManualOverrideEngine`, `ReportAssembler`, `ResearchAnonymizer`, `ResearchAggregator`, `CaseReviewAssembler`, `ExportWriter`, `ParticipantTokenProvider`.
- **ViewModels registered in DI:** only `SmartCoachViewModel` and `ExerciseDetailViewModel` (Transient). All other VMs (Clinician/Coach/Report/Override/CaseReview/Main/etc.) self-resolve via the static `App.Services` locator.

## 5. MVVM stacks (two coexist) — CONFIRMED

| Stack | Used by | Notes |
| --- | --- | --- |
| CommunityToolkit.Mvvm `ObservableObject` + `[ObservableProperty]`/`[RelayCommand]` | Most VMs (MainViewModel, SmartCoachViewModel, AnalysisPageViewModel, dashboards, etc.) | Source-generated; CommunityToolkit is cross-platform (Avalonia-friendly). |
| Hand-rolled `ViewModelBase` + `RelayCommand` (`ICommand`, `CommandManager.RequerySuggested`) | Largely unused; `ExerciseDetailViewModel` implements `INotifyPropertyChanged` directly | `RelayCommand` uses WPF-only `CommandManager`. `ViewModelBase`/`SubsystemViewModelBase` are effectively dead. |

Recommendation (documented only): standardize on CommunityToolkit for the port.

## 6. Legacy / dead code inventory — CONFIRMED

| Item | Evidence |
| --- | --- |
| `Subsystems/**` | No external references; active app uses `App.ConfigureServices`. |
| `Infra/DependencyInjection.cs` (`AddFemVoiceStudio`) | Never called. |
| `ViewModels/ViewModelBase.cs`, `SubsystemViewModelBase` | No VM derives from them. |
| `Audio/AudioAnalysisEngine_new.cs` | Compiled but only `using System;`. |
| `Audio/RealtimeAnalysisEngine.cs`, `Audio/AsyncAudioPipeline.cs` | No production instantiation found (PARTIAL). |
| `*.cs.old` / `*.cs.old2` (multiple) | Not compiled; backup artifacts. |
| `Services/VoiceHealthService.cs`, `Services/HealthStatus.cs` | Appear orphaned from the main Safety/Health/Recovery gate flow (NEEDS REVIEW). |

## 7. External (non-code) repository content

The repo root and `work-documents/`, `dokumentasjon/`, `FemVoiceStudio/Docs/` hold extensive markdown (clinical, sprint, RC-0, tester-pack docs), plus build assets (`logo.ico/png`, `install.ps1`, `generate_resources.py`) and a `femvoice_priv_mirror.git` mirror. These are planning/reference material, not authoritative about implemented behaviour (per the existing overview's own caveat).
