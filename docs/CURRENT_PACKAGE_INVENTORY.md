# FemVoice Studio — Current Package Inventory (WPF Baseline)

Audit date: 2026-06-16 · Read-only · Source: the two `.csproj` files. — CONFIRMED unless noted.

> Versions are exactly as declared inline in the project files (no central package management). The transitive dependency graph was **not** resolved (no offline NuGet restore available); "direct vs transitive" is based on the `PackageReference` entries only — PARTIAL where noted. `Avalonia-compatible` describes whether an equivalent works in an Avalonia/cross-platform app, not that the exact package is reused unchanged.

## 1. Declared packages

### `FemVoiceStudio` (main app)

| Package | Version |
| --- | --- |
| CommunityToolkit.Mvvm | 8.2.2 |
| QuestPDF | 2026.5.0 |
| Microsoft.Data.Sqlite | 8.0.0 |
| Microsoft.Extensions.DependencyInjection | 8.0.0 |
| Microsoft.NET.Test.Sdk | 17.8.0 |
| NAudio | 2.2.1 |
| OxyPlot.Wpf | 2.1.2 |
| xunit | 2.6.2 |
| xunit.runner.visualstudio | 2.5.4 |

> ⚠️ **Concern:** `Microsoft.NET.Test.Sdk`, `xunit`, and `xunit.runner.visualstudio` are referenced by the **production app** project (not just the test project). Combined with 4 test files under `FemVoiceStudio/Tests/`, test code + frameworks ship inside the WinExe. Recommendation (documented only): move these to the test project exclusively.

### `FemVoiceStudio.Tests`

| Package | Version |
| --- | --- |
| Microsoft.NET.Test.Sdk | 17.8.0 |
| xunit | 2.6.2 |
| xunit.runner.visualstudio | 2.5.4 |

(Plus `ProjectReference` → `FemVoiceStudio`.)

## 2. Per-package detail

### CommunityToolkit.Mvvm — 8.2.2
- **Used by project:** FemVoiceStudio
- **Purpose:** MVVM primitives — `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]` source generators.
- **Category:** MVVM framework
- **WPF-specific:** No · **Windows-specific:** No
- **Avalonia-compatible:** Yes (the recommended MVVM stack for Avalonia)
- **Porting risk:** Low
- **Suggested action:** **Keep**

### QuestPDF — 2026.5.0
- **Used by project:** FemVoiceStudio (`Services/ExportWriter.cs`)
- **Purpose:** PDF report generation (text/table layout). Community license set once in `static ExportWriter()`.
- **Category:** Reporting / PDF
- **WPF-specific:** No · **Windows-specific:** No (cross-platform; no System.Drawing/System.Windows usage — verified). Charts are **not** embedded in PDFs.
- **Avalonia-compatible:** Yes (framework-agnostic)
- **Porting risk:** Low
- **Suggested action:** **Keep** (consider keeping report assembly/writing in a shared `FemVoice.Reports` project)

### Microsoft.Data.Sqlite — 8.0.0
- **Used by project:** FemVoiceStudio (`DatabaseService`, `ExerciseDataService`, all `Sqlite*` stores)
- **Purpose:** SQLite access for the single shared `femvoice.db`.
- **Category:** Persistence / database
- **WPF-specific:** No · **Windows-specific:** No (cross-platform native binary)
- **Avalonia-compatible:** Yes
- **Porting risk:** Low
- **Suggested action:** **Keep** (the data layer has no UI coupling)

### Microsoft.Extensions.DependencyInjection — 8.0.0
- **Used by project:** FemVoiceStudio (`App.xaml.cs.ConfigureServices`)
- **Purpose:** DI container / composition root.
- **Category:** DI / infrastructure
- **WPF-specific:** No · **Windows-specific:** No
- **Avalonia-compatible:** Yes (standard for Avalonia too)
- **Porting risk:** Low
- **Suggested action:** **Keep** (composition root will be re-authored in the Avalonia head, but the abstraction stays)

### NAudio — 2.2.1
- **Used by project:** FemVoiceStudio (`Audio/AudioCaptureService.cs`, `AudioAnalysisEngine.cs`, `ResonanceProxyEngine.cs` (FFT only), `Subsystems/Audio`, `AnalyzerWindow` WaveFileWriter)
- **Purpose:** Microphone capture (WASAPI + WaveIn fallback), device enumeration (MMDevice), FFT (`NAudio.Dsp`).
- **Category:** Audio
- **WPF-specific:** No · **Windows-specific:** **Yes** — WASAPI/WaveIn/MMDevice capture is Windows-only.
- **Avalonia-compatible:** Partial. NAudio's **FFT/`Complex`** math (used by `ResonanceProxyEngine`/`AudioAnalysisEngine`) is portable; NAudio **capture/device** APIs are Windows-only.
- **Porting risk:** **High** (the single biggest cross-platform blocker)
- **Suggested action:** **Keep behind abstraction** for Windows; **Replace later for Linux/macOS**. Introduce an `IAudioCaptureService`/`FemVoice.Audio.Abstractions` boundary; keep NAudio in a `FemVoice.Audio.Windows` implementation. For cross-platform, evaluate a portable capture backend (e.g. PortAudio/OpenAL/Miniaudio bindings) and a portable FFT.

### OxyPlot.Wpf — 2.1.2
- **Used by project:** FemVoiceStudio (`PitchChartViewModel`, `ResonanceChartViewModel`, `AnalysisPageViewModel`, `MainViewModel`; hosted in `AnalysisWindow`/`MainWindow`/`ResonanceWindow`; bridged by `AnalysisChartTheme`)
- **Purpose:** Real-time/interactive UI charts (pitch trace, formant scatter, analysis dashboards). **Not** used for PDF/report charts.
- **Category:** Charting (UI)
- **WPF-specific:** **Yes** (`OxyPlot.Wpf` is the WPF binding; `PlotView` is a WPF control)
- **Windows-specific:** No (OxyPlot core is portable)
- **Avalonia-compatible:** Yes via **`OxyPlot.Avalonia`** — `PlotModel`-building code (the bulk) ports as-is; only the host control + the brush-reading half of `AnalysisChartTheme` change.
- **Porting risk:** Medium
- **Suggested action:** **Replace for Avalonia** (swap `OxyPlot.Wpf` → `OxyPlot.Avalonia`; abstract the theme-brush lookup)

### Microsoft.NET.Test.Sdk — 17.8.0
- **Used by:** FemVoiceStudio.Tests **and (incorrectly) FemVoiceStudio**
- **Purpose:** Test host/SDK.
- **Category:** Test infrastructure
- **WPF-specific:** No · **Windows-specific:** No (but test project TFM is `net10.0-windows`, so tests are Windows-bound)
- **Avalonia-compatible:** Yes
- **Porting risk:** Low
- **Suggested action:** **Keep** in the test project; **Remove if unused** from the main app project.

### xunit — 2.6.2
- **Used by:** FemVoiceStudio.Tests **and (incorrectly) FemVoiceStudio**
- **Purpose:** Unit test framework.
- **Category:** Test framework
- **WPF-specific:** No · **Windows-specific:** No
- **Avalonia-compatible:** Yes
- **Porting risk:** Low
- **Suggested action:** **Keep** in test project; **Remove if unused** from the app project.

### xunit.runner.visualstudio — 2.5.4
- **Used by:** FemVoiceStudio.Tests **and (incorrectly) FemVoiceStudio**
- **Purpose:** VS/`dotnet test` runner adapter.
- **Category:** Test runner
- **WPF-specific:** No · **Windows-specific:** No
- **Avalonia-compatible:** Yes
- **Porting risk:** Low
- **Suggested action:** **Keep** in test project; **Remove if unused** from the app project.

## 3. Implicit / framework dependencies (not NuGet) — CONFIRMED

| Dependency | Source | Notes for Avalonia |
| --- | --- | --- |
| WPF (`UseWPF=true`) | SDK | The entire UI shell; replaced by Avalonia. |
| `System.Windows.*` (Media, Threading, Input, Data, Markup) | WPF | Used by Views/VMs/Converters/ThemeManager/AnalysisChartTheme. Replace per Avalonia equivalents. |
| `Microsoft.Win32` (`SaveFileDialog`/`OpenFileDialog`, `Registry`) | Windows | File dialogs → Avalonia `StorageProvider`; registry theme read → Avalonia `PlatformSettings`. |
| `System.Resources.ResourceManager` + RESX | BCL | Localization core; framework-neutral. Keep. (XAML `LocExtension`/`LocConverter` are WPF MarkupExtensions — replace.) |
| `System.Text.Json`, `System.IO.Compression`, `System.Security.Cryptography` | BCL | Backups, support package, calibration profile hashing. Portable. Keep. |
| `Environment.SpecialFolder.MyDocuments` / `LocalApplicationData` | BCL | Resolve to different locations cross-platform — note for paths. |

## 4. Category summary & risk

| Category | Package(s) | Cross-platform risk |
| --- | --- | --- |
| **Audio** | NAudio 2.2.1 | **High** — Windows-only capture; the main blocker |
| **Charting** | OxyPlot.Wpf 2.1.2 | Medium — `OxyPlot.Avalonia` exists |
| **Reporting/PDF** | QuestPDF 2026.5.0 | Low — already portable |
| **Persistence** | Microsoft.Data.Sqlite 8.0.0 | Low |
| **Localization** | RESX/ResourceManager (BCL) | Low (core) / Medium (XAML markup extensions) |
| **DI** | Microsoft.Extensions.DependencyInjection 8.0.0 | Low |
| **MVVM** | CommunityToolkit.Mvvm 8.2.2 | Low |
| **Logging/diagnostics** | None (custom `Rc0RuntimeLog` over `System.IO`) | Low |
| **Tests** | xUnit 2.6.2 + runner + Test.Sdk 17.8.0 | Low (but currently leaking into app project) |

There is **no third-party logging package** (Serilog/NLog/etc.); diagnostics are custom file writers. There is **no ORM** (raw ADO.NET via Microsoft.Data.Sqlite). There is **no separate localization package** beyond the BCL ResourceManager.
