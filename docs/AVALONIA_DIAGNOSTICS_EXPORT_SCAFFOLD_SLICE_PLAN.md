# Avalonia Diagnostics / Export / Backup Read-only Scaffold — Slice Plan

Date: 2026-06-17 · Branch: `avalonia-diagnostics-export-scaffold-slice` (off `main` @ `f0ad68c`, incl. PR #1–#14).

> **Status: IMPLEMENTED (Linux-verified, headless).** Display-only diagnostics/export/backup scaffold; static/
> in-memory placeholder data only. No clinical/domain change · no WPF change · no Android/iOS · no real mic ·
> no persistence · no session-history reads/writes · no report generation · no support-package generation ·
> no export/file dialogs · no backup/restore · no diagnostics/RC-0/research-anonymization change · no
> SmartCoach/progression · no safety-gate · no Voice-Health/recovery. See `_SLICE_REPORT.md` / `_GATE_RESULTS.md`.

## 1. Goal
Represent the WPF diagnostics/export/backup surfaces in Avalonia as a reachable, display-only, read-only
scaffold built on the existing shell + theme + localization foundations. The Diagnostics nav item becomes
implemented; the remaining missing surfaces stay deferred placeholders.

## 2. Scope (implemented)
- **New** `ViewModels/DiagnosticsViewModel.cs` — a purely static page (no services, no commands, not IDisposable,
  no timers/subscriptions/capture, no DB/history reads, no file dialogs/folders). 8 static `DiagnosticsCard`s
  (Systemstatus, App-diagnostikk, Støttepakke, Sikkerhetskopi, Gjenoppretting, Dataeksport, Forskning/
  anonymisering, Feilsøking), each deferred/sample; `AllActionsDeferred => true`.
- **New** `Views/DiagnosticsView.axaml` (+ `.axaml.cs`) — converter-free cards via `ItemsControl`; **disabled**
  global actions (Generer støttepakke / Eksporter diagnostikk / Lag sikkerhetskopi / Gjenopprett / Åpne
  diagnostikkmappe) + a disabled per-card action — all `IsEnabled=False`, no command, no file dialog. Shell theme brushes.
- **Edit** `ViewModels/ShellViewModel.cs` — retained inert `_diagnostics` singleton + `ShowDiagnostics` command;
  "Diagnostikk" nav item now **implemented**; destination label (via `Shell_Nav_Diagnostics`) + disposal guard updated.
- **Edit** `MainWindow.axaml` — `DataTemplate` for `DiagnosticsViewModel`.
- **Edit** `Program.cs` — `--diagnostics-scaffold-smoke`; updated `--shell-smoke` (6 implemented / 3 deferred).

## 3. Safety / token discipline
Norwegian labels/keys are used throughout (Støttepakke / Sikkerhetskopi / Gjenoppretting / Eksport / Forskning /
anonymisering) so the source naturally avoids the forbidden English tokens (`Backup`/`Restore`/`Research`/
`Anonymization`/`Export`/`SupportPackageService`/`DiagnosticsService`/`Zip`/`PDF`/`Docx`/`Save`/`Persist`). The
page title uses a scaffold-specific key (`Diag_ScaffoldTitle`). No `SupportPackageService`, file dialogs, open
folder/file APIs, support-package/export/backup/restore creation, database/`SessionAnalyticsStore` reads-writes,
RC-0 evidence change, research-anonymization change, clinical recomputation, SmartCoach/progression, or Voice-Health/recovery.

## 4. Lifecycle
Diagnostics is a retained singleton (not disposed; not IDisposable), starting no work. The shell's transient-page
disposal is preserved and exercised: navigating to Diagnostics from a running runtime disposes the runtime
(stops synthetic capture; no orphan frames; no duplicate runtime).

## 5. Smoke (`--diagnostics-scaffold-smoke`)
Headless: Diagnostics nav item exists and is implemented; navigating switches `CurrentPage` to
`DiagnosticsViewModel`; the VM is inert (not IDisposable via reflection; no `IRelayCommand`); ≥6 placeholder
cards all deferred (`AllActionsDeferred`); navigating to Diagnostics from a running runtime disposes it
(`IsRunning==false`, trace stops growing → no orphaned capture).

## 6. Pre-existing file-dialog note
`IFileDialogService` / `AvaloniaFileDialogService` (null-returning placeholders) and their DI registration
(`Program.cs`) are **pre-existing** platform-abstraction code (Phase-2 abstractions), NOT introduced by this
slice and NOT used by the Diagnostics scaffold. This slice opens no file dialogs/folders.

## 7. Gate
`dotnet build` (0 warnings) · all 14 smokes OK · `dotnet list --vulnerable` clean · `FemVoice.Tests.Portable`
baseline (1570/1580; 1569 acceptable due to the known ComfortZone flake) · leak guard: the Diagnostics slice
files introduce zero forbidden references (only the pre-existing file-dialog placeholder remains) · Windows CI via PR.
