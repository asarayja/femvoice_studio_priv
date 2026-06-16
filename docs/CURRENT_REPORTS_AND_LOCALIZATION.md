# FemVoice Studio — Current Reports & Localization (WPF Baseline)

Audit date: 2026-06-16 · Read-only. — CONFIRMED unless noted. Describes current behaviour; proposes no content change.

---

## Part A — Reports / Export

### Report types — CONFIRMED (exactly 4)

Defined as records in `Models/ProfessionalReports.cs`: **`ClinicalReport`, `CoachReport`, `OutcomeReport`, `TimelineReport`**. `Services/ReportAssembler.cs` has one builder per type; `Services/ReportVerificationTracker.cs` tracks the same four; `ViewModels/ReportExportViewModel.cs` maps index 0–3 → Clinical/Coach/Outcome/Timeline.

### Export formats — CONFIRMED (exactly 3)

`enum ExportFormat { Pdf, Csv, Json }` in `Services/ExportWriter.cs`; `Write()` dispatches to `WriteJson`/`WriteCsv`/`WritePdf`.

- **JSON:** `System.Text.Json`, indented, `JsonStringEnumConverter`, UTF-8.
- **CSV:** UTF-8 per report type; cell escaping is **RFC 4180** (`EscapeCsvCell` quotes fields containing `, " \r \n` and doubles embedded quotes); every cell is run through `ReportTextSanitizer.Clean` first.
- **PDF:** **QuestPDF 2026.5.0**, Community license set once in `static ExportWriter()` (the only license call in the repo). A4, text/table layout via the QuestPDF fluent API, one builder per report type + a generic fallback.

### Charts in PDF — CONFIRMED: NOT embedded

PDF output is text/table only. The "OxyPlot" mentions in `ExportWriter.cs` are **comments** ("charts require a UI thread and are deferred"). OxyPlot is used for live UI charts only, never in the report/export path.

### Text localization & sanitization — CONFIRMED

PDF/CSV text resolves through `LocalizationService.Instance` (helpers `T()`/`Tf()`), then `ReportTextSanitizer.Clean` strips control/format chars + BOM/replacement chars, NFC-normalizes, preserves `\r\n\t`/ZWJ/ZWNJ. `ReportAssembler` localizes statuses, reason codes, dimensions, exercise names with graceful key-fallback. `ClinicalLanguagePolicy` is a separate static rule engine (CI/test guard scanning RESX for shaming/pressure/diagnosis copy) — **not** invoked in the runtime export path.

### Coupling — CONFIRMED

`ExportWriter`/`ReportAssembler`/`ReportTextSanitizer` have **no** `System.Drawing`/`System.Windows` (QuestPDF usage is cross-platform-clean). The **only** WPF coupling in the report area is the VM: `ReportExportViewModel` uses `Microsoft.Win32.SaveFileDialog` and `App.Services` (it offers a test ctor + `FileSavePathOverride` seam). → Abstract behind `IFileDialogService` for Avalonia.

### Related professional/research assembly

`OutcomeProfileBuilder` (reads SmartCoach/Effectiveness/Longitudinal engines, mutates nothing → assembles an `OutcomeProfile`), `CaseReviewAssembler`, `ResearchAnonymizer`/`ResearchAggregator` (see diagnostics doc) are all pure and portable.

---

## Part B — Localization

### Mechanism — CONFIRMED (framework-neutral core)

`Services/LocalizationService.cs` uses `System.Resources.ResourceManager` + `System.Globalization.CultureInfo` only. Lookup: `_resourceManager.GetString(key, _currentCulture) ?? key`. `SetLanguage` sets `Thread.CurrentThread.CurrentUICulture/CurrentCulture` and raises `PropertyChanged("Item[]")` to refresh bindings. **Default language is `nb` (Norwegian).** Preference persisted to `%LocalAppData%/FemVoiceStudio/language.txt`. The only framework touch is `INotifyPropertyChanged` (binding interface, not the WPF assembly). `ILocalizationService` is framework-neutral.

### XAML wiring — CONFIRMED (WPF-coupled — replace for Avalonia)

- `Converters/LocConverter.cs` — `MarkupExtension, IValueConverter` (WPF `System.Windows.Data`/`Markup`), delegates to `LocalizationService.Instance[key]`.
- `Converters/LocalizationExtensions.cs` — `LocExtension : MarkupExtension` (`{loc:Loc Key}`) creates a WPF `Binding` so culture switches propagate live; plus a static `Loc` helper for code-behind.

These two are the localization items that must be re-implemented for Avalonia (Avalonia markup extensions / binding); the `LocalizationService` core ports as-is.

### RESX inventory — CONFIRMED

Under `FemVoiceStudio/Resources/`:

- **Base/neutral:** `Strings.resx` — **Norwegian** (e.g. `Common_Yes` = "Ja"), ~1673 keys. This is the ResourceManager base.
- **Well-formed culture satellites (`Strings.<culture>.resx`), 18 cultures:** `ar`, `cs-CZ`, `da-DK`, `de-DE`, `el-GR`, `en`, `es-ES`, `fi-FI`, `fr-FR`, `hr-HR`, `hu-HU`, `it-IT`, `nl-NL`, `pl-PL`, `ro-RO`, `sv-SE`, `tr-TR`, `uk-UA`.

So effectively: Norwegian (neutral) + 18 satellites ≈ **19 languages loadable**.

### Naming anomalies — CONFIRMED / OUTDATED (flag, do not change)

| File | Problem | Effect |
| --- | --- | --- |
| `Strings_en.resx` (underscore) | Not a valid culture satellite name | Treated as a separate base resource `Strings_en`; orphan/duplicate — no code loads it. Content differs from `Strings.en.resx`. |
| `String.pt-BR.resx` (singular "String") | Wrong base name (`String`, not `Strings`) | `pt-BR` is **not** picked up by `ResourceManager("…Strings", pt-BR)`; Brazilian Portuguese is **effectively absent**. Also has a quality bug (`Common_No`="No"). |
| `Strings.resx.old` | Backup artifact | Not a build input. |

> Per the hard rules, **no localization resource was modified**. These are documented as recommendations: rename `String.pt-BR.resx` → `Strings.pt-BR.resx` to actually enable pt-BR, and remove/merge `Strings_en.resx`.

### Generator config anomaly — NEEDS REVIEW

The csproj declares `ResXFileCodeGenerator` → `Strings.Designer.cs` for `Strings.resx`, but **`Resources/Strings.Designer.cs` does not exist**. The strongly-typed accessor is absent; all access goes through `LocalizationService`/`Loc`. The generator declaration is effectively dead.

### Exercise/guidance text

`Services/ExerciseTextService.cs` and `Services/ExerciseGuideTextLocalizer.cs` provide localized exercise/guide text layered on the same RESX system (internals not deep-read — PARTIAL).

---

## Part C — What must be preserved during the port

- The **4 report types** and **3 export formats** and their exact assembled content.
- QuestPDF Community-license setup and the no-chart-in-PDF behaviour.
- RFC 4180 CSV escaping + `ReportTextSanitizer` cleaning + `ClinicalLanguagePolicy` CI guard.
- `LocalizationService` semantics: Norwegian neutral base, runtime culture switch via `PropertyChanged("Item[]")`, key-fallback, `language.txt` persistence.
- All 19 effectively-loadable languages and their keys (resources are not to be edited except the documented naming-fix recommendations).
