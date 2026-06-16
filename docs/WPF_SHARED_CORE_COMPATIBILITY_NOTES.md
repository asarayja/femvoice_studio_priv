# WPF ↔ Shared-Core Compatibility Notes (Agent 3)

Date: 2026-06-16. **Static review only — the WPF app cannot be built on this Linux host; all items below are verified by inspection/grep, and must be confirmed by the Windows build (Agent 2).**

Principle followed: only **mechanical** compatibility changes from the extraction. No behaviour, no UI rewrite, no clinical/domain logic change, no dead-architecture revival.

## Mechanical changes made (so WPF compiles against the shared projects)

| File | Change | Behavior changed | Risk |
| --- | --- | --- | --- |
| `FemVoiceStudio/FemVoiceStudio.csproj` | Added `<ProjectReference>` to `FemVoice.Core`, `FemVoice.Audio.Abstractions`, `FemVoice.Audio.Windows`; removed the `Resources\Strings.resx` `<EmbeddedResource>` block (RESX moved to Core) | No | Low — RESX manifest base name preserved via Core `RootNamespace=FemVoiceStudio` |
| `FemVoiceStudio/Services/ThemeManager.cs` | Removed `AppTheme`/`AppSettings`/`DebugSettings`/`AppSettingsJson` type defs (moved to `FemVoice.Core/Services/SettingsModels.cs`, same namespace) | No | Low — same namespace, consumed via Core ref |
| `FemVoiceStudio/Subsystems/Analysis/IAnalysisSubsystem.cs` | Removed `ResonanceCategory` enum (moved to `FemVoice.Core/Models/ResonanceCategory.cs`, same namespace) | No | Low — dead file; enum consumed via Core ref |
| `FemVoiceStudio/App.xaml.cs` | Added `using FemVoiceStudio.Audio.Abstractions; using FemVoiceStudio.Audio.Windows;` and `services.AddSingleton<IAudioCaptureService, NAudioCaptureService>();` | No (additive DI registration) | Low |
| `FemVoiceStudio/Audio/AudioCaptureService.cs` | Moved to `FemVoice.Audio.Windows/` (namespace `FemVoiceStudio.Audio` unchanged) | No | Low — WPF call sites resolve via the new project ref |

Tests run to support these: portable suite (1570/1580) + Audio.Windows/Core/Abstractions/Avalonia builds, all on Linux. WPF build itself: **pending Windows**.

## Verified compatibility facts (grep/inspection)
- **No WPF use of Core internals.** Core defines only 3 internal methods (`AddEdge`, `AddNode`, `EmitFormantsForTesting`); none are referenced by the WPF app. So **no `InternalsVisibleTo("FemVoiceStudio")` is required** on Core. (Core already grants internals to `FemVoiceStudio.Tests` + `FemVoice.Tests.Portable` for `EmitFormantsForTesting`.)
- **Namespaces preserved** for every moved type (`FemVoiceStudio.Models/.Services/.Audio/.Data`, `FemVoiceStudio.Subsystems.Analysis.ResonanceCategory`, `FemVoiceStudio.Audio.AudioCaptureService`) → no WPF `using`/call-site edits needed.
- **RESX**: `FemVoice.Core` `RootNamespace=FemVoiceStudio` keeps the manifest name `FemVoiceStudio.Resources.Strings`; runtime smoke confirms `Common_Yes → "Ja"`. 19 culture satellites build.
- **Capture orchestration stays in WPF** (`AudioAnalysisEngine`, `AudioAnalyzerService`, `RealtimeAnalysisEngine`, `AsyncAudioPipeline`, `AudioAnalysisEngine_new`) and references `AudioCaptureService` via the `FemVoice.Audio.Windows` project ref.

## Dead architecture — NOT ported / NOT revived (as required)
`Subsystems/**`, `Infra/DependencyInjection.cs`, `ViewModelBase`/`SubsystemViewModelBase`, `*.cs.old`/`*.cs.old2`, `AudioAnalysisEngine_new.cs` remain in the WPF project untouched (not moved into shared projects). The only edit to a dead file was removing the relocated `ResonanceCategory` enum from `IAnalysisSubsystem.cs` (consumed by live Models).

## Residual unknowns (Windows build will confirm)
1. WPF/`UseWPF` build + packaging specifics (workload) — unverifiable on Linux.
2. The `App.xaml.cs` DI edit + csproj refs compile cleanly — predicted yes (no internal access, namespaces preserved).
3. The 30 Windows-only tests compile/pass against the moved types — predicted yes.

If any mechanical break surfaces on Windows, record it here with File / Reason / Behavior-changed / Tests-run / Risk.
