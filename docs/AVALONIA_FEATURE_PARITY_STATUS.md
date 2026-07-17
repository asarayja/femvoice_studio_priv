# FemVoice — WPF → Avalonia feature-parity status (honest)

Date: 2026-07-18. Answers: "are ALL the WPF functions in Avalonia yet?" — **No.** UI *structure* for the main
screens exists (and the daily dashboard loop is functionally real), but most **professional/clinical behaviour** is
still display-only scaffold or not ported. Legend: ✅ real · 🟡 partial · 🟦 scaffold (structure only, synthetic/no
data) · ❌ not ported.

## Screen / feature map (WPF reference → Avalonia)

| WPF window/feature | Avalonia | Notes |
| --- | --- | --- |
| MainWindow — start/stop, live pitch chart, comfort zone, stability/health, difficulty | 🟡 Dashboard | **Real live mic → pitch/stability/health + chart** (Linux). Simplified feedback string — full FeedbackConsistencyGuard / FemVoiceScore / VocalHealthSupervisor / Hydration NOT wired. |
| ExerciseWindow — list, filters/search, guidance, live feedback, hold, subjective report, save | 🟡 Exercise Guide + Runtime | List (15) + filters/search + guidance + hold/target UI real. **Exercise runtime still uses target-tuned SYNTHETIC audio (not the real mic); no subjective report, no save→progression, no persistence.** |
| SettingsWindow — theme, language, audio, voice-goal, backup/restore/clear, accessibility, privacy | 🟡 Settings | Theme/language/reduce-motion persist + apply (Avalonia-local). **No DB settings, mic calibration, backup/restore/clear, voice-goal profile, or privacy export/delete.** |
| AnalysisWindow — OxyPlot resonance/pitch/prosody/health over real session data | 🟡 Analysis | **REAL** pitch/score trends + summary stats from the DB (PR #56). OxyPlot + per-dimension resonance/intonation rings (WPF sources from `SessionAnalyticsStore`) deferred. |
| ReportExportWindow — 4 report types × 3 formats (QuestPDF/CSV) | 🟡 Reports | **REAL** progress-summary report preview from the DB (PR #57). Full 4×3 generation + export (OutcomeProfile/notes/audit assembler + file dialogs) deferred. |
| Diagnostics / SupportPackage / backup / RC-0 / research anonymization | 🟦 Diagnostics | Card hub only. **No support package, backup/restore, RC-0 export.** |
| SmartCoachDashboard/Detail — engine-backed daily recommendation, streak, health | 🟡 SmartCoach | **REAL** engine: `SmartCoachEngine.GenerateDailyRecommendation` on the real DB (PR #53). |
| ProgressionDashboard/Window — level, progress bar, FemVoice score, parameter rows | 🟡 Progression | **REAL** level + FemVoice score + `ProgressionService` summary on the real DB (PR #54). Per-dimension rings deferred. |
| ResonanceWindow · ResonanceContrastDemoWindow · AnalyzerWindow | ❌ | No dedicated Avalonia screen (resonance appears only as a synthetic mini-chart in the Analysis scaffold). |
| CalendarWindow · DayDetailsWindow · StatisticsWindow | ❌ | Represented only as a deferred "kalender/historikk" card in Reports. |
| ClinicianDashboard · CoachDashboard · CaseReviewWindow | ❌ | Deferred cards in Reports; no real panels. |
| ManualOverrideWindow (safety clamp) | ❌ | Not ported. |
| MicrophoneCalibrationWindow | ❌ | Deferred card in Settings/Diagnostics. |
| FirstTimeSetupWindow | ❌ | Deferred card in Settings. |

## Persistence (updated 2026-07-18)
🟡 **Partial** — Avalonia-LOCAL, display-only: UI prefs (theme/language/reduce-motion) + **session history**
(dashboard + exercise sessions log to `<ApplicationData>/FemVoiceAvalonia/`, shown in "Siste økter (lokalt)"). This
is deliberately NOT the WPF SQLite DB and feeds no clinical/progression engine — a safe foundation, not full DB parity.

## Cross-cutting Core systems NOT wired into Avalonia yet
SQLite `DatabaseService` (full/clinical persistence), `SmartCoach*`, `ProgressionOrchestrator`, `MasteryEvaluator`,
`RecoveryScorer`, `VocalHealthSupervisor`, report assembler/export, research anonymization, RC-0 diagnostics, mic
calibration, `SessionAnalyticsStore`, subjective-report → progression. (The Avalonia UI deliberately references
**only** `FemVoice.Core` + `FemVoice.Audio.Abstractions` and touches no DB/engine — enforced by the packaging
leak-guard smoke.)

## Honest bottom line
- **Platform + shell + all screens' visual structure:** done (4 heads build; desktop runs; APK builds).
- **Functional parity:** roughly the **daily-use loop is real** (dashboard live mic, exercise browsing, local
  settings); the **professional/clinical half is scaffold or missing** (analysis-with-data, reports, SmartCoach/
  progression engines, persistence, calibration, clinician/coach/case tools, manual-override safety clamp).
- Each of those is a **future slice**, and most are **clinical-adjacent** (touch frozen engines / DB / safety), so
  they are approval-gated and must be ported one careful screen at a time — not changing the frozen clinical logic,
  only presenting/wiring it read-only where safe.
