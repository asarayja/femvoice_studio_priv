# FemVoice Studio — Shared Project Extraction Plan (Step 3)

Date: 2026-06-16 · Owner: Agent 2 (Solution Architecture). Grounded in an adversarially-verified per-file dependency-closure analysis (17-agent workflow: 8 closure + 8 refute + 1 synthesis). Sources: `docs/AVALONIA_PORT_READINESS_NOTES.md`, `WPF_DEPENDENCY_MAP.md`, `CURRENT_PROJECT_STRUCTURE.md`.

> ⚠️ **This is a plan, not an executed move.** No code has been relocated. The repo cannot build/test here (Linux, no .NET SDK, WPF is Windows-only — see `AVALONIA_BASELINE_TEST_RESULTS.md`), and the work order requires "tests green after each phase." The move must be executed on a Windows host with the .NET 10 SDK + Desktop workload, one batch per commit, verifying the build/test gate each time. Every file verdict below is grep-confirmed against the code.

## 0. Headline conclusions (verified)

1. **The named Phase-1 set is NOT a closed `net10.0` set.** Taken literally it pulls in 10 clean companions and trips **3 blockers** + **1 RESX hazard** (details in §4–§5).
2. **The safe, immediately-compilable first slice is `Models/**` + `ILocalizationService` (Option B0)**, extendable to a **pure scoring core (Option B1)** with no preparatory refactor.
3. **The `Rc0RuntimeLog → ThemeManager.SettingsPath` WPF-leak hypothesis is FALSE** (6 independent confirmations). `Services/Rc0RuntimeLog.cs` imports only `System` + `System.IO` and depends only on the clean `DiagnosticsNaming` + `Rc0WriteFailureSink`. The edge runs the other way (`ThemeManager` *consumes* the sink). So the diagnostics/logging core is cleanly extractable; only `Rc0StartupBootstrap.cs`, `SupportPackageService.cs`, `PrivacyConsentPolicy.cs` are ThemeManager-coupled and stay in the WPF app.

## 1. Target project topology & dependency direction

```
            ┌───────────────────────────────────────────────┐
            │ FemVoice.Wpf (net10.0-windows, UseWPF)          │  existing head (reference baseline)
            │ FemVoice.Avalonia (net10.0)                     │  new head (later phases)
            └───────────────┬───────────────────────────────┘
                            │ depends on ▼ (heads reference shared; shared never references heads)
   ┌─────────────────────────────────────────────────────────────────────┐
   │ FemVoice.Core           (net10.0)  domain: scoring, smartcoach,        │
   │                                     health, recovery, progression,     │
   │                                     analytics, feedback, Models/**,     │
   │                                     Data + SQLite stores                │
   │ FemVoice.Audio.Abstractions (net10.0)  IAudioCaptureService + DSP       │
   │ FemVoice.Reports        (net10.0)  ReportAssembler + ExportWriter+QuestPDF│
   │ FemVoice.Localization   (net10.0)  LocalizationService + RESX           │
   │ FemVoice.Diagnostics    (net10.0)  Rc0RuntimeLog/Sink/Naming + evidence │
   └─────────────────────────────────────────────────────────────────────┘
            ▲                                   ▲
            │ Windows-only impl ────────────────┘
   ┌──────────────────────────────────┐
   │ FemVoice.Audio.Windows (net10.0-windows)  NAudio capture impl of        │
   │                                            IAudioCaptureService          │
   └──────────────────────────────────┘
   Tests: FemVoice.Tests (net10.0, portable) + FemVoice.Tests.Wpf (net10.0-windows)
```

**Rule:** shared projects target plain `net10.0` and reference **only** BCL + cross-platform NuGet (Microsoft.Data.Sqlite, QuestPDF, CommunityToolkit.Mvvm, NAudio-for-FFT-math). Heads + `FemVoice.Audio.Windows` target `net10.0-windows`. Shared never references a head.

> **Pragmatic first step (per the work order's "smaller first step if safer"):** you may collapse `FemVoice.Reports`/`FemVoice.Localization`/`FemVoice.Diagnostics`/`FemVoice.Audio.Abstractions` into **`FemVoice.Core`** initially (they are all `net10.0` and UI-free) and split later. The audit shows the split is low-risk either way. The batches below name the eventual target but can all land in `FemVoice.Core` first.

## 2. Target framework strategy

| Project | TFM | Why |
| --- | --- | --- |
| `FemVoice.Core` (+ Reports/Localization/Diagnostics/Audio.Abstractions if merged) | `net10.0` | UI-free; runnable + testable on Linux/macOS CI |
| `FemVoice.Audio.Windows` | `net10.0-windows` | NAudio WASAPI/WaveIn/MMDevice capture |
| `FemVoice.Wpf` | `net10.0-windows`, `UseWPF=true` | existing reference head |
| `FemVoice.Avalonia` | `net10.0` | Avalonia desktop head |
| `FemVoice.Tests` | `net10.0` | portable domain tests |
| `FemVoice.Tests.Wpf` | `net10.0-windows` | theme/icon/viewmodel-brush/UI tests |

## 3. 🚨 RESX / RootNamespace hazard (must handle in Batch 1) — NEEDS REVIEW

`LocalizationService.cs:56` and `LevelClassificationSystem.cs:182` resolve resources with the **hardcoded base name** `"FemVoiceStudio.Resources.Strings"` via `new ResourceManager("FemVoiceStudio.Resources.Strings", <assembly>)`. The current csproj has **no** `<RootNamespace>`/`<AssemblyName>` override and the `<EmbeddedResource Update="Resources\Strings.resx">` has **no** `<LogicalName>`, so the manifest name defaults to `FemVoiceStudio.Resources.Strings`.

**If the RESX moves to an assembly whose root namespace is not `FemVoiceStudio`, `GetString` silently returns nothing (all UI text disappears) — it will still compile.** Mitigations (pick one, before/with moving RESX):
1. Set the shared lib's `<RootNamespace>FemVoiceStudio</RootNamespace>` and move `Resources/Strings*.resx` into it under a `Resources/` folder → manifest name stays `FemVoiceStudio.Resources.Strings`. **(Recommended, lowest churn.)**
2. Add `<LogicalName>FemVoiceStudio.Resources.Strings</LogicalName>` to each embedded resx.
3. Route `LevelClassificationSystem`'s lookup through `ILocalizationService` instead of a private `ResourceManager`.

Also (documented in localization audit, do not silently rewrite translations): the mis-named `Resources/Strings_en.resx` (underscore) and `Resources/String.pt-BR.resx` (singular "String") don't resolve as satellites and should be renamed (`Strings.en` already exists; `Strings.pt-BR`) — but only with approval, as a separate change.

## 4. Verified per-group closure summary

Counts are files confirmed clean to move to a `net10.0` lib (grep-verified for `System.Windows*`, `Dispatcher`, `Application.Current`, `Microsoft.Win32`, `OxyPlot`, `ObservableCollection`, `Presentation*`, `WindowsBase`, plus the `Rc0RuntimeLog`/`ThemeManager` tangle).

| Group | Clean | Verdict | Blockers / required companions |
| --- | --- | --- | --- |
| `Models/**` | 52 | confirmed | None. Fully self-contained (7 internal cross-namespace usings). `ScoreSnapshot.cs` OxyPlot hit is a comment. |
| Pure scoring services | 5 | amended | `ClinicalSessionScore` blocked by `ExerciseSessionOutcome`; `LevelClassificationSystem` needs concrete `DatabaseService` via `[Obsolete]` ctor (→ Rc0RuntimeLog chain) + RESX hazard |
| Feedback pipeline/guard/rule-engine | 8 | confirmed | `FeedbackPipeline` blocked by `SmartCoachMessage`; `FeedbackService` blocked by WPF `Loc` (LocalizationExtensions) |
| Localization core + interfaces | 3 | amended | `ISettingsService` blocked by `AppSettings`/**`AppTheme` enum** living in WPF-coupled `ThemeManager.cs` |
| Diagnostics/logging (critical) | 7 | confirmed | `Rc0StartupBootstrap`, `SupportPackageService`, `PrivacyConsentPolicy` are ThemeManager-coupled (stay in app). Logger itself is clean. |
| SmartCoach/health/recovery/progression/analytics | 35 | confirmed | `ExerciseSessionRecorder` (Rc0 log), `ExerciseIntelligenceCoordinator` (NAudio via ResonanceProxyEngine) — handle in their own batches |
| Data/persistence | 15 | amended | Missing companions: `Rc0RuntimeLog` + `DiagnosticsNaming` + `Rc0WriteFailureSink` (all clean) referenced by `DatabaseService.cs:1074`, `ExerciseDataService.cs:193` |
| Audio DSP analyzers | 18 | amended | Missing transitive companions: `DiagnosticsNaming` + `Rc0WriteFailureSink` (via Rc0RuntimeLog). Only `ResonanceProxyEngine` uses NAudio — math-only (`NAudio.Dsp`; `using NAudio.Wave` is unused). |

## 5. The 3 blockers + required prep refactors (do these BEFORE the affected batch)

All three are **tiny, behaviour-preserving** code extractions — moving a type to its own file or deleting an already-`[Obsolete]` ctor. None change clinical logic. They must be done on the Windows build env with tests green.

1. **`ExerciseSessionOutcome`** — declared inside `Services/ExerciseSessionRecorder.cs:15` (host calls `Rc0RuntimeLog.Write` at 578/697/723/881). Required by `ClinicalSessionScore.cs:23`.
   → Extract the `ExerciseSessionOutcome` record into its own clean file (ideally `Models/ExerciseSessionOutcome.cs`). Do **not** move `ExerciseSessionRecorder` into Phase-1.

2. **`SmartCoachMessage`** — declared inside `Data/DatabaseService.cs:3075`. Required by `FeedbackPipeline.cs:49/67/106`.
   → Extract `SmartCoachMessage` into its own clean Models file. Then drop `using FemVoiceStudio.Data;` from `FeedbackPipeline.cs`.

3. **Concrete `DatabaseService` in `LevelClassificationSystem.cs:118`** — an `[Obsolete]` ctor takes the concrete `Data.DatabaseService`.
   → **Recommended:** delete the obsolete ctor so the class depends only on `IDatabaseService` (`Data/IDatabaseService.cs`, clean). This removes the need to drag the persistence + logging chain into the scoring batch. (Alternative: accept `DatabaseService` + `Rc0RuntimeLog` + `DiagnosticsNaming` + `Rc0WriteFailureSink` as companions — all clean, but heavier.)

Plus the **RESX hazard** (§3) before `LevelClassificationSystem`/RESX move, and extracting the **`AppTheme` enum** out of `ThemeManager.cs` when `ISettingsService`/`AppSettings` move (Batch for settings/localization).

## 6. Recommended ordered extraction sequence

| Batch | Moves | Target | Prereq | Risk |
| --- | --- | --- | --- | --- |
| **1** | `Models/**` (52) + `Services/Interfaces/ILocalizationService.cs` + `Resources/Strings*.resx` | FemVoice.Core (+ Localization) | §3 RESX/RootNamespace fix | **Zero** (Option B0) |
| **2** | `FemVoiceScore.cs`, `FemVoiceScoreSnapshot.cs`, `FemVoiceScoreEngine.cs`, `RecoveryScorer.cs`, `VoiceIntelligenceScorer.cs`, `Audio/VocalWeightAnalyzer.cs`, `Data/IScoreRepository.cs`, `IUserRepository.cs`, `IDatabaseService.cs` | FemVoice.Core | Batch 1 | **Low** (Option B1 — pure scoring core, no DB body, no feedback) |
| **prep** | Extract `ExerciseSessionOutcome` + `SmartCoachMessage`; delete `[Obsolete]` ctor in `LevelClassificationSystem` | — | — | Low (mechanical) |
| **3** | `FeedbackConsistencyGuard.cs` + `FeedbackPipeline.cs` | FemVoice.Core | prep (SmartCoachMessage) | Low |
| **4** | `ClinicalSessionScore.cs` + `LevelClassificationSystem.cs` | FemVoice.Core | prep + §3 RESX | Low–Med |
| **5** | `Rc0RuntimeLog.cs`, `Rc0WriteFailureSink.cs`, `DiagnosticsNaming.cs`, `SafeFailureMessages.cs`, `ParticipantTokenProvider.cs`, `ResearchAnonymizer.cs`, `Rc0EvidenceExporter.cs` (+ `Models/ResearchDataset.cs`, `Audio/AudioCaptureDiagnostics.cs`) | FemVoice.Diagnostics | — | Low (logger is clean) |
| **6** | `DatabaseService.cs` + 15 verified-clean Data files + Rc0 companions | FemVoice.Core (persistence) | Batch 5 | Med |
| **7** | 18 audio DSP analyzers (+ `DiagnosticsNaming`/`Rc0WriteFailureSink` already moved) | FemVoice.Audio.Abstractions | Batch 5; confirm NAudio net10.0 | Med |
| **8** | SmartCoach/health/recovery/progression/analytics (35 clean) | FemVoice.Core | Batches 2–7 | Med |
| **9** | `ISettingsService`/`AppSettings` + extract `AppTheme` enum from `ThemeManager.cs` | FemVoice.Localization/Core | — | Med |

`Rc0StartupBootstrap.cs`, `SupportPackageService.cs`, `PrivacyConsentPolicy.cs` (ThemeManager-coupled), and all of `Views/**`, `Themes/**`, `ThemeManager.cs`, `IconProvider.cs`, `AnalysisChartTheme.cs`, `Converters/**`, `Subsystems/**`, `Infra/**` **stay in the WPF head** (the last two are dead — do not port).

## 7. Ready-to-use project files (drop in on Windows)

`FemVoice.Core/FemVoice.Core.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- CRITICAL: preserve the RESX manifest base name so ResourceManager("FemVoiceStudio.Resources.Strings") keeps resolving -->
    <RootNamespace>FemVoiceStudio</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
    <!-- add QuestPDF here only if Reports merged into Core -->
  </ItemGroup>
  <!-- when RESX moves here: -->
  <ItemGroup>
    <EmbeddedResource Update="Resources\Strings.resx">
      <Generator>ResXFileCodeGenerator</Generator>
    </EmbeddedResource>
  </ItemGroup>
</Project>
```

`FemVoice.Tests/FemVoice.Tests.csproj` (portable, runs on Linux/CI):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\FemVoice.Core\FemVoice.Core.csproj" />
  </ItemGroup>
</Project>
```

`FemVoiceStudio.slnx` add the new projects:
```xml
<Solution>
  <Project Path="FemVoiceStudio/FemVoiceStudio.csproj" />
  <Project Path="FemVoice.Core/FemVoice.Core.csproj" />
  <Project Path="FemVoice.Tests/FemVoice.Tests.csproj" />
  <Project Path="FemVoiceStudio.Tests/FemVoiceStudio.Tests.csproj" />
</Solution>
```

## 8. Mechanical move procedure (per batch, on Windows)

1. Branch: `git checkout -b avalonia/phase1-extraction` (off `main`).
2. `git mv FemVoiceStudio/Models/Foo.cs FemVoice.Core/Models/Foo.cs` (preserve folder layout; **keep namespaces stable** — `namespace FemVoiceStudio.Models` stays, since RootNamespace=FemVoiceStudio).
3. Add `<ProjectReference>` from `FemVoiceStudio` (WPF) and `FemVoice.Tests` to `FemVoice.Core`.
4. Because SDK globbing auto-includes `**/*.cs`, moving files out of `FemVoiceStudio/` removes them from the WPF project automatically — no `<Compile Remove>` needed.
5. `dotnet build FemVoiceStudio.slnx` → fix references → `dotnet test`.
6. Commit the batch with the agent report block (Files changed / Behavior changed: **no** / Tests run / Risks / Follow-up).
7. Repeat per batch; never mix batches.

**Namespace stability:** keep `FemVoiceStudio.*` namespaces in the moved files so no call sites change. Only the *assembly* changes; the namespace does not. This minimizes diff and avoids touching the frozen logic.

## 9. NEEDS REVIEW before/while extracting
- **RESX base name** (§3) — silent runtime breakage risk. Highest priority.
- **Duplicate type declarations** between `Services`/`Models` and dead `Subsystems.*`: `FemVoiceScoreInput`/`FemVoiceScoreResult` (FemVoiceScore.cs vs IProgressionSubsystem.cs), `VoiceMetrics` (Models vs IAnalysisSubsystem.cs), `TrainingLevel`/`LevelClassificationResult` (LevelClassificationSystem.cs vs IProgressionSubsystem.cs). No Phase-1 file imports `Subsystems.*`, so **no ambiguity today** — but if a later batch moved the Subsystems variants into the same assembly you'd get CS0101. Since `Subsystems/**` is dead and not being ported, this is avoided — but confirm.
- **NAudio net10.0 compatibility** for the audio DSP batch (`ResonanceProxyEngine` uses `NAudio.Dsp` FFT). NAudio 2.2.1 targets netstandard2.0 so it should load on `net10.0`; verify on build.
- **`[Obsolete]` ctor deletion** in `LevelClassificationSystem` — confirm the team accepts removing the obsolete `DatabaseService` ctor (recommended) vs. importing the persistence chain.
- Confirm the new lib's compile glob excludes `*.cs.old`/`.cs.old2` backups (it will by extension; verify no broad `<Compile Include>`).
