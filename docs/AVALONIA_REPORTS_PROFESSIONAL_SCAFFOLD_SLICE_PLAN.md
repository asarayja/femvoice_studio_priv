# Avalonia Reports / Professional Workflow Scaffold — Slice Plan

Date: 2026-06-17 · Branch: `avalonia-reports-professional-scaffold-slice` (off `main` @ `e552d2e`, incl. PR #1–#13).

> **Status: IMPLEMENTED (Linux-verified, headless).** Display-only reports/professional scaffold; static/in-memory
> placeholder data only. No clinical/domain change · no WPF change · no Android/iOS · no real mic · no
> persistence · no session-history reads/writes · no report generation · no export/file dialogs · no
> SmartCoach/progression · no safety-gate · no Voice-Health/recovery · no diagnostics/RC-0/research change.
> See `_SLICE_REPORT.md` / `_GATE_RESULTS.md`.

## 1. Goal
Represent the WPF Reports/Professional surfaces in Avalonia as a reachable, display-only scaffold built on the
existing shell + theme + localization foundations. The Reports nav item becomes implemented; other missing
surfaces stay deferred placeholders.

## 2. Scope (implemented)
- **New** `ViewModels/ReportsViewModel.cs` — a purely static page (no services, no commands, not IDisposable, no
  timers/subscriptions/capture, no DB/history reads, no file dialogs). 8 static `ReportsCard`s (Report preview,
  Progress summary, Session history, Klinikerpanel, Veilederpanel, Saksgjennomgang, Calendar/history, Eksport),
  each with a deferred/sample status; `AllActionsDeferred => true`.
- **New** `Views/ReportsView.axaml` (+ `.axaml.cs`) — converter-free cards via `ItemsControl`; **disabled** global
  actions ("Eksporter (senere)", "Åpne rapport (senere)") + a disabled per-card action (all `IsEnabled=False`, no
  command, no file dialog). Shell theme brushes.
- **Edit** `ViewModels/ShellViewModel.cs` — retained inert `_reports` singleton + `ShowReports` command; "Rapporter"
  nav item now **implemented**; destination label (via `Shell_Nav_Reports`) + disposal guard updated.
- **Edit** `MainWindow.axaml` — `DataTemplate` for `ReportsViewModel`.
- **Edit** `Program.cs` — `--reports-scaffold-smoke`; updated `--shell-smoke` (5 implemented / 4 deferred).

## 3. Safety / token discipline
Norwegian labels are used for the professional cards (Klinikerpanel / Veilederpanel / Saksgjennomgang /
Eksporter) so the source naturally avoids the forbidden English tokens (`ClinicianDashboard`/`CoachDashboard`/
`CaseReview`/`Export`...). The page title uses a scaffold-specific key (`Reports_ScaffoldTitle`) to avoid any
pre-existing clinical RESX collision (lesson from the Analysis slice). No report generation, `ReportExport`/
`ExportWriter`, file dialogs, PDF/Docx, database/`SessionAnalyticsStore`/`ExerciseSessionRecorder`, clinical
recomputation, SmartCoach/progression, or Voice-Health/recovery.

## 4. Lifecycle
Reports is a retained singleton (not disposed; not IDisposable), starting no work. The shell's transient-page
disposal is preserved and exercised: navigating to Reports from a running runtime disposes the runtime (stops
synthetic capture; no orphan frames; no duplicate runtime).

## 5. Smoke (`--reports-scaffold-smoke`)
Headless: Reports nav item exists and is implemented; navigating switches `CurrentPage` to `ReportsViewModel`;
the VM is inert (not IDisposable via reflection; no `IRelayCommand`); ≥6 placeholder cards all deferred
(`AllActionsDeferred`); and navigating to Reports from a running runtime disposes it (`IsRunning==false`, trace
stops growing → no orphaned capture).

## 6. Pre-existing file-dialog note
`IFileDialogService` / `AvaloniaFileDialogService` (null-returning placeholders) and their DI registration
(`Program.cs:50`) are **pre-existing** platform-abstraction code (from the Phase-2 abstractions), NOT introduced
by this slice and NOT used by the Reports scaffold. This slice opens no file dialogs.

## 7. Gate
`dotnet build` (0 warnings) · all 13 smokes OK · `dotnet list --vulnerable` clean · `FemVoice.Tests.Portable`
baseline (1570/1580; 1569 acceptable due to the known ComfortZone flake) · leak guard: the Reports slice files
introduce zero forbidden references (only the pre-existing file-dialog placeholder matches remain) · Windows CI via PR.
