# FemVoice Studio — Avalonia Migration Tracker

Owner: Agent 0 (Orchestrator / Release Captain). Created 2026-06-16. Single source of truth for port progress.
Reference docs: `docs/AUDIT_SUMMARY_FOR_AVALONIA_PLANNING.md`, `AVALONIA_PORT_READINESS_NOTES.md`, `WPF_DEPENDENCY_MAP.md`, `CURRENT_*`, `work-documents/FemVoice Funksjonsoversikt.md`.

## Prime directives (do not violate)

- **Target now:** Avalonia **desktop** app with **Windows feature parity** vs the frozen WPF baseline. **No Android yet.**
- **No** new features, no redesign, no clinical-behaviour change.
- Behaviour-frozen systems (the 20-item list in the work order): clinical scoring, FemVoice score, SmartCoach, VocalHealthSupervisor, RecoveryScorer, RecoveryIntelligenceService, ProgressionSafetyGate, FeedbackConsistencyGuard, FeedbackPriority hierarchy, ProgressionOrchestrator, MasteryEvaluator, ComfortZoneController, AdaptiveDifficultyService, SQLite schema/stores, session analytics, report contents, research anonymization, RC-0 diagnostics, localization semantics, 15-exercise catalog + target profiles.
- Governing hierarchy **Safety > Health > Recovery > Comfort > Voice Development > Reporting** — nothing may weaken safety/health/recovery.
- **Tests green after every phase.** Small reviewable changes only. No broad rewrites unless explicitly assigned.

## Environment reality (read this first)

⛔ This workstation is **Linux with no .NET SDK**, and the product is **WPF/`net10.0-windows` (Windows-only)**. **Nothing in this repo can build or test here.** Phases that move/compile code require a **Windows host with .NET 10 SDK + Desktop (WPF) workload**, or Windows CI. See `docs/AVALONIA_BASELINE_TEST_RESULTS.md`. Phases that are pure documentation/analysis are doable anywhere.

## Status legend
`TODO` · `IN PROGRESS` · `BLOCKED` · `DONE` · `N/A`

## Phase status board

| Phase | Title | Status | Owner | Gate to next |
| --- | --- | --- | --- | --- |
| 0 | Safety snapshot & build verification | **DONE** — Windows CI green (build + 1867/1881 tests; 14 pre-existing) | Agent 1/2 | ✓ `WINDOWS_BASELINE_TEST_RESULTS.md` (PR #1 run 27618290291) |
| 1 | Project split & shared core extraction | **DONE** — WPF builds against shared core on Windows (CI verified); 3 mechanical fixes applied | Agent 2 | ✓ |
| 2 | Platform abstraction interfaces | **DONE (interfaces in FemVoice.Core/Platform; Avalonia impls)** | Agent 3 | — |
| 3 | Windows audio backend behind abstraction | **DONE (FemVoice.Audio.Windows + NAudioCaptureService; Linux-compile-verified) / manual Windows mic test PENDING** | Agent 4 | Manual Windows mic smoke (`AUDIO_WINDOWS_ADAPTER_NOTES.md`) |
| 4 | Avalonia shell bootstrap | **DONE (boots headless; DI resolves on Linux)** | Agent 5 | — |
| 5 | Theme & localization port | TODO | Agent 6 | Light/dark + runtime language switch work |
| 6 | Navigation & main dashboard | **MERGED to main (PR #3)** — `MainDashboardViewModel` + dashboard; start/stop + live pitch/stability/health from synthetic audio; top-nav shell added in Phase 8 | Agent 8 | Chart/feedback/theme parity in later slices |
| 7 | Audio, pitch chart, live feedback | TODO | Agent 7 + 8 | Pitch/comfort-zone parity |
| 8 | Exercise guide & detail | **MERGED to main (PR #4)** — Guide list (15) + Detail + shell nav; read-only over `VoiceFeminizationExerciseService`; `--exercise-smoke` | Agent 9 | — |
| 8b | Exercise runtime scaffold | **MERGED to main (PR #5)** — Detail→Runtime nav; `ExerciseRuntimeViewModel`/`View`; synthetic pitch vs target band; display-only hold/elapsed; `--exercise-runtime-smoke` | Agent 9 | — |
| 8c | Exercise runtime target-profile integration | **MERGED to main (PR #6, `51565d9`)** — read-only `ExerciseTargetProfile`/`IndicatorPackage` panel; Id→ProfileType map (15/15, 0 fallback); `RequiredHoldSeconds` as display-only hold target; `--exercise-runtime-integration-smoke` | Agent 9 | — |
| 8d | Exercise coordinator readout | **MERGED to main (PR #7, `a8df6ec`)** — VM-local **parameterless** `ExerciseIntelligenceCoordinator` driven **read-only**; synthetic-derived `UpdateMetrics`; "Koordinator-readout" panel (hold/progress, status, **display-only** safety-lock) vs derived hold; `--exercise-coordinator-smoke`. No persistence/gate/SmartCoach/enforcement | Agent 9 | — |
| 8e | Runtime chart + live feedback | **MERGED to main (PR #8, `47e9a72`)** — **converter-free** native-Avalonia pitch chart (Canvas band + ItemsControl trace + current-pitch marker, fixed axis via portable `PitchChartAxisRangeCalculator`); local **display-only** live-feedback readout; derived vs coordinator hold bars; `--runtime-chart-feedback-smoke`. No OxyPlot/FeedbackConsistencyGuard/ComfortZoneController, no persistence/clinical change | Agent 9 | — |
| 8f | Desktop shell + navigation/layout parity | **MERGED to main (PR #9, `fe3b43c`)** — window chrome (min-size + CenterScreen + resizable); header + left nav rail (`ShellNavItem`: 2 implemented + 7 **deferred** static placeholders via `DeferredSurfaceViewModel`) + central content + static right info sidebar + **display-only** bottom status strip; preserved transient-page disposal; `--shell-smoke`. Converter-free; no clinical/WPF change | Agent 8 | — |
| 8g | Theme + localization adapter foundation | **MERGED to main (PR #10, `49dcb0e`)** — Avalonia-only `Themes/ShellTheme.axaml` (Dark/Light `ThemeDictionaries`, 14 named shell brushes) merged in `App.axaml`; `MainWindow` shell colours via `{DynamicResource}`; safe **read-only** localization adapter (`Localized.Get` + reactive `LocalizedValue` + `{loc:Tr}` `TrExtension`) for shell/nav/status/deferred labels (fallback = current text); `--theme-loc-smoke`. **Localization semantics preserved** (no RESX/key/culture/SetLanguage change); no WPF theme-manager/LocExtension/LocConverter; no clinical/WPF change | Agent 8 + 3 | full 176-key theme parity + inner-view theming + `Shell_*` RESX keys + runtime language switch deferred |
| 8h | Settings / preferences UI scaffold | **SLICE DONE (Linux-verified, PR open)** — display-only Settings page reachable from the shell (Settings nav item now **implemented**); `SettingsViewModel` (static, no services/commands, not IDisposable) + `SettingsView` with 8 cards (General/Appearance/Language/Audio/Exercise prefs/Data-backup/Privacy/About) via read-only `Localized.Get`; every control **disabled/deferred/inert**; `--settings-smoke`. No persistence/SetLanguage/theme-switch/profile-write/backup/restore/SQLite; no clinical/WPF change | Agent 12 | real Settings behaviour (persistence, language/theme switch, profile/backup) deferred to later slices |
| 9 | SmartCoach & progression UI | TODO | Agent 10 | Daily rec matches WPF on same data |
| 10 | Analysis, resonance, chart windows | TODO | Agent 7 + 10 | Resonance/analysis chart parity |
| 11 | Reports, professional tools, dialogs | TODO | Agent 11 | 4×3 export parity; override clamp intact |
| 12 | Settings, backup/restore, diagnostics | TODO | Agent 12 | RC-0 writes; backup/restore parity |
| 13 | Parity testing & cleanup | TODO | Agent 1 + 13 | Full parity matrix green; cleanup logged |

---

## Phase detail

### Phase 0 — Safety snapshot & build verification — BLOCKED (env)
- **Goal:** Establish a known-green baseline (build + test) and freeze it for diffing.
- **Scope:** `dotnet build`/`dotnet test` of `FemVoiceStudio.slnx`; record results + SDK/workloads; classify tests.
- **Out of scope:** Any code change.
- **Agents:** Agent 1.
- **Files likely touched:** `docs/AVALONIA_BASELINE_TEST_RESULTS.md` (docs only).
- **Tests required:** Whole suite (record pass/fail).
- **Acceptance:** Baseline documented; safety-invariant tests confirmed green.
- **Rollback:** N/A (read-only).
- **Status note:** Cannot run here (no SDK + WPF-on-Linux). Test classification done; real run must occur on Windows. See baseline doc.

### Phase 1 — Project split & shared core extraction — PLANNED
- **Goal:** Create UI-free shared projects and move only confirmed-portable code, zero behaviour change.
- **Scope:** New projects per `docs/SHARED_PROJECT_EXTRACTION_PLAN.md` (`FemVoice.Core` + optional `FemVoice.Reports`/`FemVoice.Localization`/`FemVoice.Diagnostics`/`FemVoice.Audio.Abstractions`); `git mv` of closed sets; update references.
- **Out of scope:** Any logic edit; moving WPF-coupled files; moving dead `Subsystems/**`/`Infra/**`.
- **Agents:** Agent 2 (lead), Agent 1 (test gate).
- **Files likely touched:** `Models/**`, pure scoring/feedback/health/recovery/progression/analytics services, `Data/**`, stores, audio DSP — exact list in the extraction plan; new `.csproj` files; `FemVoiceStudio.slnx`.
- **Tests required:** Full suite after each batch; the extracted portable subset should also build/run as `FemVoice.Tests` (net10.0).
- **Acceptance:** Everything compiles on Windows; all tests green; no namespace meaning lost; safety-invariant tests green (ideally now also on Linux/CI).
- **Rollback:** Per-batch git revert; each batch is an independent commit. Branch off `main`.
- **Status note:** Move is **specified but not performed** (no build env to verify the green gate). Execute on Windows.

### Phase 2 — Platform abstraction interfaces — TODO
- **Goal:** Introduce framework-neutral interfaces so shareable view-models/logic don't touch WPF.
- **Scope:** `IUiDispatcher`, `IDialogService`, `IFileDialogService`, `ISystemThemeProvider`, `IThemeResourceProvider`, `IAudioCaptureService` (+ DTOs) in `FemVoice.Core/Platform` / `FemVoice.Audio.Abstractions`; WPF implementations of each in the existing WPF head; replace direct WPF calls in *shareable* VMs only.
- **Out of scope:** Avalonia implementations; UI rewrite.
- **Agents:** Agent 3.
- **Files likely touched:** new interface files; `MainViewModel`/`ExerciseDetailViewModel`/`SmartCoachViewModel` (dispatcher/brush call sites), `ReportExportViewModel` (SaveFileDialog), `SettingsWindow.xaml.cs` (OpenFileDialog), `ThemeManager.cs` (registry).
- **Tests required:** Full suite; add interface-contract tests.
- **Acceptance:** Interfaces compile; WPF app still builds and behaves identically; no behaviour change.
- **Rollback:** Interfaces are additive; revert call-site swaps individually.

### Phase 3 — Windows audio backend behind abstraction — TODO
- **Goal:** Put NAudio capture behind `IAudioCaptureService` without changing audio behaviour.
- **Scope:** `FemVoice.Audio.Windows` implementing `IAudioCaptureService` (wrap `AudioCaptureService`/`AudioAnalysisEngine` capture); DSP already in shared core (Phase 1); UI flows resolve capture via DI.
- **Out of scope:** Linux/macOS capture; DSP changes; scoring/strain/resonance changes.
- **Agents:** Agent 4.
- **Files likely touched:** `Audio/AudioCaptureService.cs`, `AudioAnalyzerService.cs`, `AudioAnalysisEngine.cs` (capture half), `ResonanceWindow.xaml.cs` capture construction; new abstraction impl; `docs/AUDIO_PORT_DECISIONS.md`.
- **Tests required:** `AudioCaptureServiceTests`, `AudioSafetyTests`, DSP tests; manual mic smoke on Windows.
- **Acceptance:** Sample rates, buffers, noise gate, high-pass, watchdog, device-lost, calibration, stabilization, resonance proxy all preserved; behaviour unchanged.
- **Rollback:** Keep direct construction path until abstraction proven; revert DI swap.

### Phase 4 — Avalonia shell bootstrap — TODO
- **Goal:** Minimal bootable `FemVoice.Avalonia` resolving shared services via DI.
- **Scope:** `App.axaml(.cs)`, `MainWindow.axaml(.cs)`, DI wiring (`Microsoft.Extensions.DependencyInjection`), Avalonia implementations of the Phase-2 interfaces.
- **Out of scope:** Full UI parity; porting all views.
- **Agents:** Agent 5.
- **Files likely touched:** new `FemVoice.Avalonia/**`.
- **Tests required:** App boots; DI container builds; smoke test.
- **Acceptance:** Avalonia app starts; DI resolves shared services; WPF app still builds; no domain change.
- **Rollback:** New project; deletable.

### Phase 5 — Theme & localization port — TODO
- **Goal:** Reimplement theme + localization markup for Avalonia, preserving semantics.
- **Scope:** Convert `LightTheme`/`DarkTheme`/`Icons` to Avalonia styles/resources; `pack://` → `avares://`; registry theme → Avalonia `PlatformSettings`; reimplement `LocExtension`/`LocConverter` for Avalonia; keep `LocalizationService` semantics (Norwegian neutral, `PropertyChanged("Item[]")`, key fallback, `language.txt`).
- **Out of scope:** Changing RESX text meaning (document the `String.pt-BR`/`Strings_en`/missing-Designer issues; fix naming only if separately approved).
- **Agents:** Agent 6.
- **Files likely touched:** `FemVoice.Avalonia` themes/styles; localization markup; `FemVoice.Localization` (if RESX moved).
- **Tests required:** `ReportLocalizationTests`, `NewLanguageResourcesTests`, `LocalizationAccessibilityRobustnessTests`, `ProfessionalResxPolicyTests`; new Avalonia theme/lang tests.
- **Acceptance:** Light/dark + runtime language switch work; Norwegian fallback preserved; no translation content changed.
- **Rollback:** Theme/markup isolated in Avalonia head.

### Phase 6 — Navigation & main dashboard — TODO
- **Goal:** Avalonia main shell with start/stop session, pitch chart, comfort zone, stability/health indicators, feedback, exercise text, navigation, professional-tools row, difficulty selector.
- **Scope:** Port `MainWindow` + the shareable parts of `MainViewModel` (via abstractions).
- **Out of scope:** Redesign; changing session start/stop logic.
- **Agents:** Agent 8 (+ Agent 7 for chart).
- **Tests required:** VM logic tests; manual parity vs WPF.
- **Acceptance:** Start/stop works; pitch + comfort-zone + health/recovery feedback appear; nav opens ported screens or safe placeholders.
- **Rollback:** Avalonia head only.

### Phase 7 — Audio, pitch chart, live feedback — TODO
- **Goal:** OxyPlot.Avalonia pitch chart + live feedback parity.
- **Scope:** `OxyPlot.Wpf`→`OxyPlot.Avalonia`; port `PitchChartViewModel`; replace `AnalysisChartTheme` brush-reading with injected colors.
- **Agents:** Agent 7 + 8.
- **Tests required:** `PitchChartAxisRangeCalculatorTests`; chart bounds/zoom parity.
- **Acceptance:** Pitch graph + comfort zone + bounds/zoom limits + empty state match WPF; no chart embedded in PDFs (unchanged).
- **Rollback:** Per-view.

### Phase 8 — Exercise guide & detail — TODO
- **Goal:** Port `ExerciseWindow`/`ExerciseListViewModel`/`ExerciseDetailViewModel` and the exercise session flow.
- **Scope:** Filters, detail, guidance text, live feedback, hold progress, subjective report, stop/save.
- **Out of scope:** Catalog/profile/orchestrator/coordinator/recorder logic (frozen).
- **Agents:** Agent 9.
- **Tests required:** `ExerciseCatalogCoverageTests`, `ExerciseDetailViewModelTests`, `GuidanceCompletenessTests`, `ExerciseProfileFactoryTests`.
- **Acceptance:** 15 exercises visible; localized text loads; hold progress updates; safety lock/freeze preserved; subjective report feeds progression.
- **Rollback:** Per-view.

### Phase 9 — SmartCoach & progression UI — TODO
- **Goal:** Port SmartCoach detail + progression dashboard (+ calendar/statistics/analytics if in scope) without engine changes.
- **Agents:** Agent 10.
- **Tests required:** `SmartCoach*Tests`, `ProgressionOrchestratorTests`, recovery/trend tests.
- **Acceptance:** Daily rec matches WPF on same data; recovery/strain still restrict; dashboards read same persisted data; analytics never gate safety.
- **Rollback:** Per-view.

### Phase 10 — Analysis, resonance, chart windows — TODO
- **Goal:** Port `AnalysisWindow`/`ResonanceWindow` + `AnalysisPageViewModel`/`ResonanceChartViewModel`.
- **Agents:** Agent 7 + 10.
- **Tests required:** `ResonanceProxyEngineTests`, `SpectrogramResonanceMapperTests`, chart tests.
- **Acceptance:** Resonance scatter/timeline + analysis charts keep same data ranges/bounds.
- **Rollback:** Per-view.

### Phase 11 — Reports, professional tools, dialogs — TODO
- **Goal:** Port report export + clinician/coach/override/case-review UIs; preserve report content.
- **Scope:** Swap `SaveFileDialog`→`IFileDialogService`/Avalonia `StorageProvider`, `MessageBox`→`IDialogService`.
- **Agents:** Agent 11.
- **Tests required:** `ReportAssemblerTests`, `ExportWriterTests`, `ReportExportViewModelTests`, `ManualOverrideClampTests`, `AuditTrailStoreTests`.
- **Acceptance:** Same 4 types × 3 formats; QuestPDF/RFC4180/sanitizer intact; override can't weaken safety/recovery; audit append-only; report text unchanged.
- **Rollback:** Per-view.

### Phase 12 — Settings, backup/restore, diagnostics — TODO
- **Goal:** Port settings + backup/restore; preserve RC-0 diagnostics/support package/research anonymization.
- **Scope:** Abstract WPF static path chain (esp. `ThemeManager.SettingsPath`); keep never-throws + privacy filtering.
- **Agents:** Agent 12.
- **Tests required:** `LocalBackupServiceTests`, `SupportPackage`/`ResearchNoPiiTests`, `Rc0*Tests`, `PilotReadinessCheckerTests`.
- **Acceptance:** RC-0 startup evidence writes; support package exports privacy-filtered zip; research PII-free; diagnostics never crash startup; paths documented.
- **Rollback:** Per-view; diagnostics behaviour must not regress.

### Phase 13 — Parity testing & cleanup — TODO
- **Goal:** Verify full Windows feature parity; perform approved cleanup.
- **Scope:** Parity matrix vs WPF; then cleanup per `docs/AVALONIA_CLEANUP_LOG.md` (dead `Subsystems/**`, `Infra/**`, `*.cs.old`, test pkgs in app, `Tests/**` in app, dormant SQL, etc.).
- **Agents:** Agent 1 (parity) + Agent 13 (cleanup, only after Orchestrator approval).
- **Tests required:** Full suite both heads; parity checklist.
- **Acceptance:** Parity matrix green; cleanup logged; no behaviour change from cleanup.
- **Rollback:** Cleanup commits isolated and reversible.

---

## Anti-mistake checklist (Agent 0 enforces)
- [ ] No Android work started.
- [ ] No dead `Subsystems/**` / `Infra/DependencyInjection.cs` ported.
- [ ] No clinical/domain logic changed during file moves.
- [ ] SQLite schema unchanged.
- [ ] SmartCoach / safety / recovery logic unchanged.
- [ ] Report content unchanged.
- [ ] Localization resource *meaning* unchanged.
- [ ] RC-0 diagnostics retained.
- [ ] Cleanup not mixed with UI port without tests.
- [ ] Each agent change carries the required report block (Files changed / Behavior changed / Tests run / Risks / Follow-up).

## Open cross-phase questions (carry from audit §8)
1. Is `AudioAnalysisEngine` capture ever started in prod; are `RealtimeAnalysisEngine`/`AsyncAudioPipeline`/`AudioAnalysisEngine_new.cs` dead?
2. Are `DatabaseSchema.sql` / migration 001 executed or dormant (migration has invalid SQLite)?
3. Are `VoiceHealthService`/`HealthStatus` live or orphaned?
4. Cross-platform DB/diagnostics path strategy (`MyDocuments`/`LocalApplicationData`).
5. Backups omit WAL/SHM sidecars — fix before port?
6. Which languages officially ship (pt-BR mis-wired)?
7. Delete dead architecture before or after port?
