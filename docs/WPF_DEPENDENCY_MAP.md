# FemVoice Studio — WPF Dependency Map (WPF Baseline)

Audit date: 2026-06-16 · Read-only · grep-backed. — CONFIRMED unless noted.

Purpose: catalogue every meaningful WPF/Windows coupling so the Avalonia port can be planned safely. Grouped per the requested taxonomy. The headline finding: **the domain core is almost entirely UI-free**; coupling is concentrated in the UI shell, three theming/icon helpers, audio capture (NAudio), file dialogs, message boxes, and the dispatcher access pattern in three view-models.

## Coupling at a glance — CONFIRMED

| Coupling | Where (grep counts) |
| --- | --- |
| `System.Windows` import in domain `Services/` | 3 files only: `AnalysisChartTheme.cs`, `IconProvider.cs`, `ThemeManager.cs` |
| `Dispatcher` in `Services/`/`Models/`/`Subsystems/` | **0** |
| `Application.Current.Dispatcher.Invoke/BeginInvoke` | `MainViewModel`, `ExerciseDetailViewModel`, `SmartCoachViewModel`, + several Views |
| `Application.Current.TryFindResource(...) as Brush` | `MainViewModel`, `ExerciseDetailViewModel`, `SmartCoachViewModel` |
| `Microsoft.Win32.SaveFileDialog` | `ReportExportViewModel.cs` |
| `Microsoft.Win32.OpenFileDialog` | `SettingsWindow.xaml.cs` |
| `Microsoft.Win32.Registry` (theme) | `ThemeManager.cs` |
| `MessageBox.Show` | MainWindow ×18, SettingsWindow ×26, ResonanceWindow ×9, AnalyzerWindow ×6, ExerciseWindow ×4, SmartCoachDetailView ×2, App ×2 |
| OxyPlot.Wpf | chart VMs + AnalysisWindow/MainWindow/ResonanceWindow + AnalysisChartTheme |
| NAudio capture | AudioCaptureService, AudioAnalysisEngine, AnalyzerWindow (WaveFileWriter), Subsystems/Audio |
| `ObservableCollection` in subsystems | `Subsystems/Progression/*` (soft; `System.ObjectModel`) |

---

## Group A — Safe to keep WPF-only (rewritten per-platform, not shared)

These are the UI shell. They will be re-authored as Avalonia AXAML; nothing to "extract".

| File/path | Current responsibility | WPF dependency | Why it matters | Avalonia impact | Recommended action |
| --- | --- | --- | --- | --- | --- |
| `Views/*.xaml` (27) + thin code-behind | Windows/UserControls layout | XAML, WPF controls | UI definition | Rewrite as `.axaml` | Replace (rewrite) |
| `App.xaml` | Merged ResourceDictionaries (LightTheme + Icons) | WPF `Application.Resources` | App resource root | Rewrite as Avalonia `App.axaml` | Replace |
| `Themes/LightTheme.xaml`, `DarkTheme.xaml` | Color/brush palette + control styles (ComboBox, Button states, Chart*, Analyzer*) | `ResourceDictionary`, `SolidColorBrush`, `Style` | Theme system | Convert to Avalonia `Styles`/`ResourceDictionary`; pack URIs → `avares://` | Replace |
| `Resources/Icons.xaml` | 10 `DrawingImage` vector icons | WPF `GeometryDrawing`/`DynamicResource` | Iconography | Convert to Avalonia drawings/`PathIcon` | Replace |
| `App.xaml.cs` startup/splash + `DispatcherHelper.DoEvents` | App lifecycle, programmatic splash `Window`, `DispatcherFrame`/`PushFrame` pump | WPF `Application`, `Window`, `BitmapImage`, `DispatcherFrame` | Startup flow | `DispatcherFrame.PushFrame` has **no** Avalonia equivalent — rework startup | Replace |
| `RelayCommand.cs` (`CommandManager.RequerySuggested`) | Hand-rolled `ICommand` | WPF `CommandManager` (WPF-only) | Command requery | Use CommunityToolkit `RelayCommand` / manual `NotifyCanExecuteChanged` | Replace |

## Group B — Must extract to shared core (pure, no WPF — move to a platform-agnostic library)

These have **no** UI framework dependency and are the reusable heart of the app.

| File/path | Current responsibility | WPF dependency | Why it matters | Avalonia impact | Recommended action |
| --- | --- | --- | --- | --- | --- |
| `Services/**` (scoring, smartcoach, health, recovery, progression, analytics, feedback, reports, diagnostics) | All domain/business logic | **None** (except the 3 theming helpers below) | This is the product's behaviour — must be preserved bit-for-bit | Reuse as-is | Extract to shared core (`FemVoice.Core`/etc.) |
| `Models/**` | Domain DTOs/records/enums | None | Shared contracts | Reuse as-is | Extract to shared core |
| `Data/**` + `Sqlite*` stores | SQLite persistence | None | Persistence | Reuse as-is | Extract to shared core (`FemVoice.Core`/persistence) |
| `Audio/` DSP analyzers (PitchDetectionService, AdaptivePitchDetector, FormantDetectionService, VoiceActivityDetector, VocalWeightAnalyzer, VoiceStrainDetector, SpeechRateAnalyzer, VoiceMetricsCalculator, ResonansScoringService, ResonanceProxyEngine DSP, PitchTraceStabilizer, PitchTargetZonePolicy, ZoneConfiguration, LiveMetricsService, SpectrogramResonanceMapper, MicrophoneCalibrationService/Profile, AudioCaptureDiagnostics) | Pure DSP/logic | None (NAudio only for `Complex`/FFT math, which is cross-platform) | Pitch/resonance/health math | Reuse as-is | Extract to shared core / `FemVoice.Audio.Abstractions` |
| `ViewModels/PitchChartViewModel.cs`, `ResonanceChartViewModel.cs`, `AnalysisPageViewModel.cs` | Build OxyPlot `PlotModel`s | OxyPlot only (no `System.Windows`) | Chart construction | Pair with `OxyPlot.Avalonia` | Extract (shared) + swap package |
| `Converters/**` logic | Value conversion | WPF `IValueConverter` base interface | Trivial to re-target | Re-implement against Avalonia `IValueConverter` (logic copies over) | Extract logic / re-shell |
| `...BrushKey` string properties throughout VMs | Theme resource keys (strings) | None | Already pure | Reuse | Extract (keep strings, drop the WPF `Brush` resolution) |

## Group C — Must abstract behind interface

Coupling embedded in otherwise-shareable code. Introduce small abstractions so the shared VM/logic can run under Avalonia.

| File/path | Current responsibility | WPF dependency | Why it matters | Avalonia impact | Recommended action |
| --- | --- | --- | --- | --- | --- |
| `MainViewModel`, `ExerciseDetailViewModel`, `SmartCoachViewModel` (dispatcher calls) | Marshal audio-thread results to UI | `Application.Current.Dispatcher.Invoke/BeginInvoke`, `DispatcherTimer` | Real-time UI updates from background threads | Avalonia has `Dispatcher.UIThread`/`DispatcherTimer` (mechanically portable) | Abstract behind `IUiDispatcher` (testable + portable) |
| `ExerciseDetailViewModel`, `SmartCoachViewModel`, `MainViewModel` (brush props) | Expose `Brush?` resolved from theme | `Application.Current.TryFindResource(...) as Brush` | Couples VM to WPF resource lookup | Keys are already pure strings | Abstract behind `IThemeResourceProvider`; bind by key in XAML instead |
| `ReportExportViewModel.cs` | Save report file | `Microsoft.Win32.SaveFileDialog` | File save UX | Use Avalonia `IStorageProvider` | Abstract behind `IFileDialogService` (already has a `FileSavePathOverride` test seam — good model) |
| `SettingsWindow.xaml.cs` | Backup import | `Microsoft.Win32.OpenFileDialog` | File open UX | Avalonia `IStorageProvider` | Abstract behind `IFileDialogService` |
| 7 files using `MessageBox.Show` | Confirmations/errors | WPF `MessageBox` | No built-in Avalonia MessageBox | Use MessageBox.Avalonia or custom dialog | Abstract behind `IDialogService` |
| `Services/ThemeManager.cs` (`IsSystemLightTheme`) | Read Windows theme | `Microsoft.Win32.Registry` | OS theme detection | Avalonia `PlatformSettings`/theme variants | Abstract behind `ISystemThemeProvider` |
| `Services/AnalysisChartTheme.cs` (brush-reading half) | Map theme brushes → OxyColor | `System.Windows.Media` + `Application.Current.TryFindResource` | Charts react to theme | Inject colors instead of reading WPF resources | Abstract color source; keep OxyColor mapping shared |

## Group D — Must replace for Avalonia

Components with no clean abstraction — replaced outright.

| File/path | Current responsibility | WPF dependency | Avalonia impact | Recommended action |
| --- | --- | --- | --- | --- |
| `OxyPlot.Wpf` package + `PlotView` hosts (`AnalysisWindow`, `MainWindow`, `ResonanceWindow`) | Chart hosting | WPF `PlotView` | Swap to `OxyPlot.Avalonia` `PlotView` | Replace |
| `ThemeManager.LoadThemeResourceDictionary` | Runtime theme swap | `Application.Current.Resources.MergedDictionaries` mutation + `pack://` URIs | Avalonia uses `ThemeVariant`/`RequestedThemeVariant` | Replace |
| `Audio/AudioCaptureService.cs`, `Audio/AudioAnalysisEngine.cs` (capture), `AnalyzerWindow` `WaveFileWriter`, `Subsystems/Audio` device enumeration | Mic capture / device enum / WAV write | NAudio WASAPI/WaveIn/MMDevice (Windows-only) | Needs cross-platform capture backend (Linux/macOS) | Replace later for Linux/macOS (keep NAudio impl for Windows behind abstraction) |
| `ExerciseWindow`/`AnalyzerWindow` Storyboard animations + Canvas `PathGeometry` rendering | Hold-arc animation, spectrogram canvas | WPF animations/`DrawingContext` | Avalonia animations/`DrawingContext` differ | Replace |
| `CreateSplash` (App.xaml.cs) | Programmatic splash | WPF `Window`/`BitmapImage` | Rework startup | Replace |

## Group E — Needs investigation / cleanup before port

| Item | Concern | Action |
| --- | --- | --- |
| `Subsystems/**`, `Infra/DependencyInjection.cs`, `ViewModelBase`/`SubsystemViewModelBase` | Dead parallel architecture (no external refs) | Decide delete vs. ignore; do **not** port |
| `Audio/RealtimeAnalysisEngine.cs`, `Audio/AsyncAudioPipeline.cs`, `Audio/AudioAnalysisEngine_new.cs` | Appear unused/dead (PARTIAL) | Confirm via full call graph before relying on them |
| `*.cs.old` / `_new` artifacts | Clutter; `_new.cs` is compiled but empty | Confirm exclusion; cleanup pass |
| `Services/VoiceHealthService.cs`, `Services/HealthStatus.cs` | Orphaned from main gate flow (NEEDS REVIEW) | Verify before treating as live behaviour |
| Two MVVM stacks | Inconsistency | Standardize on CommunityToolkit |
| Test project + main app both `net10.0-windows` and both reference xUnit | Tests are Windows-bound; tests leak into app | Retarget once shared core is split; remove test pkgs from app |
| 10 WPF-coupled test files (theme/icon/resource/Brush/Application.Current) | Cannot run on non-Windows | Keep WPF UI tests in a Windows-only test head; move pure tests to a portable test project |

---

## Dispatcher / threading inventory — CONFIRMED

| Location | Use |
| --- | --- |
| `App.xaml.cs` | `Dispatcher.BeginInvoke` (splash close); `DispatcherHelper` (`DispatcherFrame`/`PushFrame`) |
| `MainViewModel` | `DispatcherTimer` UI refresh + ~8 `Dispatcher.Invoke/BeginInvoke` marshalling audio results |
| `ExerciseDetailViewModel` | `Dispatcher.CheckAccess()` + `BeginInvoke` for live-state/coach messages |
| `SmartCoachViewModel` | ~6 `Dispatcher.Invoke` around async loads |
| `AnalyzerWindow` | 50 ms render `DispatcherTimer` + `Dispatcher.Invoke` (FFT) |
| `ExerciseWindow` | session timer + `Dispatcher.BeginInvoke` |
| `MainWindow` | chart render timer + `Dispatcher.Invoke` |
| `MicrophoneCalibrationWindow`, `ResonanceWindow` | `Dispatcher.Invoke` progress |

All are pure thread-marshalling — portable to `Dispatcher.UIThread`, but the embedded `Application.Current.Dispatcher` access should move behind `IUiDispatcher`.

Audio engines that already use `SynchronizationContext.Post` (`AudioAnalysisEngine`, `ResonanceProxyEngine`) are Avalonia-friendly as-is.
