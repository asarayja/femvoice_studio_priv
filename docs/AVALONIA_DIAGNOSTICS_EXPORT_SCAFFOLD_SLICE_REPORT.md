# Avalonia Diagnostics / Export / Backup Read-only Scaffold — Slice Report

Date: 2026-06-17 · Branch: `avalonia-diagnostics-export-scaffold-slice` (off `main` @ `f0ad68c`).

> **Display-only diagnostics/export/backup scaffold; static/in-memory placeholder data only.** No clinical/
> domain behaviour changed · no WPF behaviour changed · no Android/iOS · no real mic · no persistence · no
> session-history reads/writes · no report generation · no support-package generation · no export/file dialogs ·
> no backup/restore · no diagnostics/RC-0/research-anonymization changes · no SmartCoach/progression · no
> safety-gate enforcement · no Voice-Health/recovery decisions.

## 1. What this slice does
Adds a reachable, display-only Diagnostics/Export/Backup page to the Avalonia shell. Diagnostics is now an
implemented nav destination; the remaining missing WPF surfaces stay deferred placeholders. The page shows 8
static placeholder cards mirroring the WPF diagnostics/export/backup surfaces, every action disabled/deferred,
with clear "deferred / not exported / not saved" messaging.

## 2. Files changed
- **New** `FemVoice.Avalonia/ViewModels/DiagnosticsViewModel.cs` — static page (no services/commands; not IDisposable);
  `DiagnosticsCard`; 8 cards (Systemstatus, App-diagnostikk, Støttepakke, Sikkerhetskopi, Gjenoppretting,
  Dataeksport, Forskning/anonymisering, Feilsøking); `AllActionsDeferred => true`.
- **New** `FemVoice.Avalonia/Views/DiagnosticsView.axaml` (+ `.axaml.cs`) — converter-free cards; disabled global +
  per-card actions (no command, no file dialog); shell theme brushes.
- **Edit** `FemVoice.Avalonia/ViewModels/ShellViewModel.cs` — inert `_diagnostics` singleton + `ShowDiagnostics`;
  "Diagnostikk" nav item implemented; destination label + disposal guard updated.
- **Edit** `FemVoice.Avalonia/MainWindow.axaml` — `DataTemplate` for `DiagnosticsViewModel`.
- **Edit** `FemVoice.Avalonia/Program.cs` — `--diagnostics-scaffold-smoke`; updated `--shell-smoke` (6 implemented / 3 deferred).
- **Docs** this report + `_SLICE_PLAN.md` + `_GATE_RESULTS.md` + tracker.

No files under `FemVoiceStudio/`, `FemVoice.Core/`, or `FemVoice.Audio.Windows/`. `SupportPackageService` is not moved or referenced.

## 3. Inertness (verified)
`DiagnosticsViewModel` holds no services, exposes no commands (no `IRelayCommand`), is not `IDisposable`, starts
no work, opens no file dialogs/folders, and reads/writes nothing. Every `DiagnosticsCard.IsEnabled` is `false`;
the view's buttons are `IsEnabled=False` with no command. `--diagnostics-scaffold-smoke` asserts this behaviorally
(reflection: no-IDisposable / no-IRelayCommand; ≥6 cards all deferred).

## 4. Display-only / no forbidden behaviour
No `SupportPackageService`, file dialogs, open folder/file APIs, support-package/export/backup/restore creation,
PDF/Docx/Zip, database/`SessionAnalyticsStore`/`IDatabaseService`/`ExerciseSessionRecorder`, report export,
clinical recomputation/FemVoice-score change, SmartCoach/progression, Voice-Health/recovery, RC-0 evidence
change, or research-anonymization change. Norwegian labels keep the source free of the forbidden English tokens;
the page title uses a scaffold-specific key (`Diag_ScaffoldTitle`).

## 5. Lifecycle safety
Diagnostics is a retained singleton (not disposed; not IDisposable). The shell's transient-page disposal is
preserved: navigating to Diagnostics from a running runtime disposes the runtime (stops synthetic capture; trace
stops growing → no orphaned capture; no duplicate runtime). Verified by `--diagnostics-scaffold-smoke`.

## 6. Verification (see `_GATE_RESULTS.md`)
Build 0 warnings · all 14 smokes OK (incl. `--diagnostics-scaffold-smoke`) · no vulnerable packages · leak guard:
Diagnostics slice files introduce zero forbidden references (only the pre-existing file-dialog placeholder
matches) · refs only Core + Audio.Abstractions · Tmds 0.21.3 · portable 1570/1580 (1569 known flake) · Windows CI = pending PR.

## 7. Behaviour changes
**None to clinical/domain behaviour. WPF untouched.** All additions are display-only Diagnostics scaffold over static placeholders.
