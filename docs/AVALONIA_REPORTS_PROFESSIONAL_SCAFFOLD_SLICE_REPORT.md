# Avalonia Reports / Professional Workflow Scaffold — Slice Report

Date: 2026-06-17 · Branch: `avalonia-reports-professional-scaffold-slice` (off `main` @ `e552d2e`).

> **Display-only reports/professional workflow scaffold; static/in-memory placeholder data only.** No
> clinical/domain behaviour changed · no WPF behaviour changed · no Android/iOS · no real mic · no
> persistence · no session-history reads/writes · no report generation · no export/file dialogs · no
> SmartCoach/progression · no safety-gate enforcement · no Voice-Health/recovery · no diagnostics/RC-0/research change.

## 1. What this slice does
Adds a reachable, display-only Reports/Professional page to the Avalonia shell. Reports is now an implemented
nav destination; other missing WPF surfaces remain deferred placeholders. The page shows 8 static placeholder
cards mirroring the WPF reports/professional surfaces, every action disabled/deferred, with clear "deferred /
not exported / not saved" messaging.

## 2. Files changed
- **New** `FemVoice.Avalonia/ViewModels/ReportsViewModel.cs` — static page (no services/commands; not IDisposable);
  `ReportsCard`; 8 cards (preview, progress summary, session history, Klinikerpanel, Veilederpanel, Saksgjennomgang,
  calendar/history, Eksport); `AllActionsDeferred => true`.
- **New** `FemVoice.Avalonia/Views/ReportsView.axaml` (+ `.axaml.cs`) — converter-free cards; disabled global +
  per-card actions (no command, no file dialog); shell theme brushes.
- **Edit** `FemVoice.Avalonia/ViewModels/ShellViewModel.cs` — inert `_reports` singleton + `ShowReports`; "Rapporter"
  nav item implemented; destination label + disposal guard updated.
- **Edit** `FemVoice.Avalonia/MainWindow.axaml` — `DataTemplate` for `ReportsViewModel`.
- **Edit** `FemVoice.Avalonia/Program.cs` — `--reports-scaffold-smoke`; updated `--shell-smoke` (5 implemented / 4 deferred).
- **Docs** this report + `_SLICE_PLAN.md` + `_GATE_RESULTS.md` + tracker.

No files under `FemVoiceStudio/`, `FemVoice.Core/`, or `FemVoice.Audio.Windows/`.

## 3. Inertness (verified)
`ReportsViewModel` holds no services, exposes no commands (no `IRelayCommand`), is not `IDisposable`, starts no
work, opens no file dialogs, and reads/writes nothing. Every `ReportsCard.IsEnabled` is `false`; the view's
buttons are `IsEnabled=False` with no command. `--reports-scaffold-smoke` asserts this behaviorally (reflection:
no-IDisposable / no-IRelayCommand; ≥6 cards all deferred).

## 4. Display-only / no forbidden behaviour
No report generation, `ReportExport`/`ExportWriter`, file dialogs, PDF/Docx, database/`SessionAnalyticsStore`/
`IDatabaseService`/`ExerciseSessionRecorder`, clinical score recalculation/FemVoice-score change, SmartCoach/
progression, Voice-Health/recovery, or diagnostics/RC-0/research change. Norwegian labels (Klinikerpanel/
Veilederpanel/Saksgjennomgang/Eksporter) keep the source free of the forbidden English tokens; the page title
uses a scaffold-specific key (`Reports_ScaffoldTitle`).

## 5. Lifecycle safety
Reports is a retained singleton (not disposed; not IDisposable). The shell's transient-page disposal is
preserved: navigating to Reports from a running runtime disposes the runtime (stops synthetic capture; trace
stops growing → no orphaned capture; no duplicate runtime). Verified by `--reports-scaffold-smoke`.

## 6. Verification (see `_GATE_RESULTS.md`)
Build 0 warnings · all 13 smokes OK (incl. `--reports-scaffold-smoke`) · no vulnerable packages · leak guard:
Reports slice files introduce zero forbidden references (only pre-existing file-dialog placeholders match) ·
refs only Core + Audio.Abstractions · Tmds 0.21.3 · portable 1570/1580 (1569 known flake) · Windows CI = pending PR.

## 7. Behaviour changes
**None to clinical/domain behaviour. WPF untouched.** All additions are display-only Reports scaffold over static placeholders.
