# FemVoice — WPF → Avalonia feature-parity status (honest)

Date: 2026-07-18. Answers: "are ALL the WPF functions in Avalonia yet?" — **No.** UI *structure* for the main
screens exists (and the daily dashboard loop is functionally real), but most **professional/clinical behaviour** is
still display-only scaffold or not ported. Legend: ✅ real · 🟡 partial · 🟦 scaffold (structure only, synthetic/no
data) · ❌ not ported.

## Screen / feature map (WPF reference → Avalonia)

| WPF window/feature | Avalonia | Notes |
| --- | --- | --- |
| MainWindow — start/stop, live pitch chart, comfort zone, stability/health, difficulty | 🟡 Dashboard | **Real live mic → pitch/stability/health/RESONANCE + chart** (Linux). Real resonance via the Core `ResonanceProxyEngine` (PR #69), shown live + saved to `TrainingSession.ResonanceScore`. Simplified feedback string — full FeedbackConsistencyGuard / FemVoiceScore / VocalHealthSupervisor / Hydration NOT wired. |
| ExerciseWindow — list, filters/search, guidance, live feedback, hold, subjective report, save | 🟡 Exercise Guide + Runtime | List (15) + filters/search + guidance + hold/target UI real. **Exercise runtime still uses target-tuned SYNTHETIC audio (not the real mic); no subjective report, no save→progression, no persistence.** |
| SettingsWindow — theme, language, audio, voice-goal, backup/restore/clear, accessibility, privacy | 🟡 Settings | Theme/language/reduce-motion persist + apply (Avalonia-local). **No DB settings, mic calibration, backup/restore/clear, voice-goal profile, or privacy export/delete.** |
| AnalysisWindow — OxyPlot resonance/pitch/prosody/health over real session data | 🟡 Analysis | **REAL** pitch/score/**resonance/prosody** trends + summary stats from the DB (PR #56, #70, #72). Resonance (`ResonanceProxyEngine`) + prosody (pitch-variation std-dev) both flow end-to-end (dashboard DSP → persisted → Analysis trends). OxyPlot styling deferred; health-dimension trend still placeholder. |
| ReportExportWindow — 4 report types × 3 formats (QuestPDF/CSV) | 🟡 Reports + panels | **REAL** progress-summary preview + CSV/text export (PR #57, #64), AND **real PDF/CSV/JSON export of all assembled reports — Coach, Clinician (Outcome), and Timeline** — via the Core `ExportWriter` (QuestPDF headless) from those panels (PR #68, #73). 3 of the 4 report types have real Avalonia panels+export (Clinical needs notes/audit — deferred); a single unified 4×3 export window still deferred. |
| Diagnostics / SupportPackage / backup / RC-0 / research anonymization | 🟦 Diagnostics | Card hub only. **No support package, backup/restore, RC-0 export.** |
| SmartCoachDashboard/Detail — engine-backed daily recommendation, streak, health | 🟡 SmartCoach | **REAL** engine: `SmartCoachEngine.GenerateDailyRecommendation` on the real DB (PR #53). |
| ProgressionDashboard/Window — level, progress bar, FemVoice score, parameter rows | 🟡 Progression | **REAL** level + FemVoice score + `ProgressionService` summary on the real DB (PR #54). Per-dimension rings deferred. |
| ResonanceWindow · ResonanceContrastDemoWindow | 🟡 Resonans | **REAL** dedicated resonance screen (PR #75): live real-time resonance meter (Core `ResonanceProxyEngine`) + the non-scored resonance-contrast awareness demo. Nav item. AnalyzerWindow still ❌ (see below). |
| AnalyzerWindow | ❌ | Real-time spectrum/formant analyzer — not yet ported. |
| StatisticsWindow | 🟡 Statistikk | **REAL** stats (total/streak/days/time/avg pitch/consistency/score) from the DB (PR #58). |
| CalendarWindow | 🟡 Kalender | **REAL** training-day history (last 90 days) from the DB (PR #59). DayDetails view deferred. |
| ClinicianDashboard · CoachDashboard · CaseReviewWindow | 🟡 Veileder + Kliniker | **REAL** Coach panel (PR #66) + **Clinician outcome panel** (PR #67): both assemble a real `OutcomeProfile` from saved sessions via the frozen Core pipeline (read-only) — Coach shows focus/recommendations/development; Clinician shows the `OutcomeReport` overview (composite score, recovery, goal progress, top exercises). Both opened from the Reports page; both degrade to a truthful "not enough data" state. **CaseReview** still deferred; per-dimension resonance/intonation fill in once sessions record them. |
| ManualOverrideWindow (safety clamp) | 🟡 Manuell overstyring | **REAL safety clamp** (PR #74): runs the FROZEN two-stage `ManualOverrideEngine` clamp verbatim and shows ONLY the clamped outcome — the raw intent is never echoed (safety invariant preserved; smoke asserts an aggressive request is pulled below baseline). Nav item. Display-only: the persist/apply step (ManualOverridesStore) is deferred pending explicit clinical sign-off. No safety logic changed. |
| MicrophoneCalibrationWindow | 🟡 Mikrofonkalibrering | **REAL** mic check: device list + live RMS level meter + signal-detected indicator on the audio abstraction (real backend in production, synthetic in tests); own capture backend, stopped on navigate-away (PR #63). Full clinical calibration profile (noise-gate/SNR/clipping thresholds → frozen DSP) intentionally NOT computed/saved — deferred. |
| FirstTimeSetupWindow | 🟡 Førstegangsoppsett | **REAL** onboarding: welcome + language + theme, persisted to the Avalonia-local prefs file and applied live; records a completed flag so it shows once (PR #62). Voice-goal-style / training-frequency (clinical-adjacent profile, no Avalonia consumer yet) deferred to a profile slice. |

## Persistence (updated 2026-07-18)
✅ **Real DB wired** — the real Core `DatabaseService` (SQLite, the SAME store WPF uses,
`<MyDocuments>/FemVoiceStudio/femvoice.db`) is in the Avalonia DI (PR #52). The dashboard **saves real
`TrainingSession`s** on Stop (PR #55), so the engines fill with real data. (An earlier Avalonia-local JSON store
remains only as the headless/no-DB fallback for tests.)

## Cross-cutting Core systems — status
**Wired (read-only, real):** `DatabaseService` (SQLite), `SmartCoachEngine`, `ProgressionService` +
`LevelClassificationSystem`, session save/read, real Analysis/Statistics/Calendar aggregates.
**Still NOT wired:** `ReportAssembler` full 4×3 export (needs OutcomeProfile/notes/audit + file dialogs + QuestPDF),
`SessionAnalyticsStore` (per-dimension resonance/intonation rings), `MasteryEvaluator`/`RecoveryScorer`/
`VocalHealthSupervisor` (deeper feedback), research anonymization + RC-0 diagnostics + support package, mic
calibration, subjective-report → progression, exercise-runtime real mic.

## Honest bottom line
- **Platform + shell + all screens' visual structure:** done (4 heads build; desktop runs; **APK builds**).
- **Real-data functional parity:** the **daily loop is real** (dashboard live mic → real sessions saved to the DB)
  and **6 professional screens are engine/DB-backed with real data** (SmartCoach, Progression, Analysis, Reports
  preview, Statistics, Calendar).
- **Remaining:** full Reports export, exercise-runtime real mic, Diagnostics real, per-dimension Analysis, and the
  not-yet-ported screens (Resonance, Clinician/Coach/CaseReview, ManualOverride safety clamp, MicCalibration,
  FirstTimeSetup). These are more clinical-adjacent/complex — ported carefully, one at a time, no clinical change.
- **Cannot be verified on this box:** RUNNING the Android APK (device/emulator) or the macOS/Windows builds (those
  OS hosts). The builds all pass on Linux.
