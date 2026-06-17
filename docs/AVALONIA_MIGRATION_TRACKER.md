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
| 8i | Exercise runtime lifecycle UI | **SLICE DONE (Linux-verified, PR open)** — explicit **inactive → Start → active → Stop → session-ended** lifecycle (`RuntimePhase`); runtime **no longer auto-starts** (explicit Start; matches WPF ExerciseWindow); display-only recommended-duration, elapsed, session-ended summary + not-saved note; live content gated by `IsRunning`; `--runtime-lifecycle-smoke` (+ 6 existing runtime smokes updated to explicit BeginCommand). Synthetic/display-only; no persistence/SmartCoach/safety/Voice-Health/recovery/mic; no clinical/WPF change | Agent 9 | real session persistence + clinical runtime still deferred |
| 8j | Analysis / resonance charts scaffold | **SLICE DONE (Linux-verified, PR open)** — display-only Analysis page reachable from the shell (Analyse nav item now **implemented**); `AnalysisViewModel` (static, no services/commands, not IDisposable) + `AnalysisView` with 4 **converter-free** mini bar-charts over SYNTHETIC in-memory sample data (Pitch trend/Resonance/Stability/Formant) + static summary placeholders + disabled import/export; `--analysis-scaffold-smoke`. **No OxyPlot**, no DB/SessionAnalyticsStore/history reads-writes, no report export, no clinical/SmartCoach/Voice-Health/recovery; no clinical/WPF change | Agent 7+10 | real analysis-engine parity (OxyPlot.Avalonia or richer native charts) + real session-history data deferred |
| 8k | Reports / professional workflow scaffold | **SLICE DONE (Linux-verified, PR open)** — display-only Reports page reachable from the shell (Rapporter nav item now **implemented**); `ReportsViewModel` (static, no services/commands, not IDisposable) + `ReportsView` with 8 static placeholder cards (preview/progress/history/Klinikerpanel/Veilederpanel/Saksgjennomgang/kalender/Eksport) + disabled global & per-card actions; `--reports-scaffold-smoke`. **No report generation/export/file-dialogs**, no DB/history/SessionAnalyticsStore, no clinical/SmartCoach/Voice-Health/recovery/diagnostics; no clinical/WPF change. Norwegian labels avoid forbidden tokens | Agent 11 | real report generation/export + clinician/coach/case-review behaviour deferred |
| 8l | Diagnostics / export / backup read-only scaffold | **SLICE DONE (Linux-verified, PR open)** — display-only Diagnostics page reachable from the shell (Diagnostikk nav item now **implemented**); `DiagnosticsViewModel` (static, no services/commands, not IDisposable) + `DiagnosticsView` with 8 static placeholder cards (Systemstatus/App-diagnostikk/Støttepakke/Sikkerhetskopi/Gjenoppretting/Dataeksport/Forskning-anonymisering/Feilsøking) + disabled global & per-card actions; `--diagnostics-scaffold-smoke`. **No SupportPackageService/support-package, no export/file-dialogs, no backup/restore, no DB/history, no RC-0/research-anonymization change**; no clinical/WPF change. Norwegian labels avoid forbidden tokens | Agent 12 | real diagnostics/support-package/export/backup-restore behaviour deferred |
| 8m | macOS/Linux packaging readiness | **SLICE DONE — MERGED (PR #16 base `8919c7b`; PR #17 `.deb`+launch fix `901d682`)** — behavior-neutral csproj `RuntimeIdentifiers` (linux-x64;linux-arm64;osx-x64;osx-arm64, **plural** — default build/run unchanged), `UseAppHost`, `PublishTrimmed=false` (reflection bindings), app metadata; **inert** `Packaging/macos/Info.plist` + `Packaging/linux/*.desktop` templates (not wired into build) + `Packaging/README.md`; `--packaging-smoke` (read-only metadata inspection). FDD publish for linux-x64/osx-x64 verified; published linux-x64 runs `--smoke` via shared runtime. **Follow-up adds Debian/Ubuntu `.deb` readiness**: behavior-neutral helper scripts `publish-linux.sh`/`package-deb.sh`/`publish-macos.sh` (no root, no install, no `dpkg` maintainer hooks, no mic perms; output under gitignored `artifacts/`), `.desktop` `Exec=femvoice-studio`, extended `--packaging-smoke`; real `.deb` built + inspected (not installed). **PR #17 launch fix**: root-caused the installed "flash-then-vanish" to the FDD **apphost** finding no system-registered runtime (exit 131) on a user-local .NET box; `/usr/bin/femvoice-studio` is now a `bash` launcher running `dotnet /opt/femvoice-studio/FemVoice.Avalonia.dll` with a `dotnet`-present check (clear msg + exit 127 if missing); added Debian author metadata (`Maintainer: A hansen <rassyhansen@gmail.com>`, `Homepage`) + machine-readable `/usr/share/doc/femvoice-studio/copyright` (`License: Proprietary`; no LICENSE in repo so no OSS license invented) + `README.Debian`; GUI launch verified alive via the packaged launcher. No runtime/clinical/WPF change, no mobile heads, no real mic/persistence | Agent 11 | self-contained `.deb` (bundling .NET) + macOS `.app`/`.dmg` + signing/notarization + real cross-platform capture deferred |
| 8n | Packaged theme/resource verification | **SLICE DONE — MERGED (PR #18 `d04e823`)** — diagnostics-only. Investigated the "`.deb` app looks unstyled/without colors" report. **Conclusion: NO packaging resource loss** — theme/resources resolve **identically** in source-run, published output, and `.deb`-shipped bits. Facts: `ShellTheme.axaml` (14 `Shell*` brushes, Dark+Light) is **embedded** in `FemVoice.Avalonia.dll` via `avares://`; FluentTheme ships (`Avalonia.Themes.Fluent.dll`) + Skia natives; zero loose `.axaml`; all 14 `{DynamicResource Shell*}` keys used by views are defined in both variants (zero dangling). Plain look = **deferred design polish** + Light variant selected (dark-first shell, `RequestedThemeVariant="Default"` follows session). New read-only `--packaged-theme-smoke` (16th smoke; no display): asserts FluentTheme registered + all view-referenced keys resolve Dark+Light + reports variant; runs from source AND published DLL (parity proof). Signing/notarization unblocked from a resource standpoint (NOT started). No UI redesign, no clinical/WPF change | Agent 11 | visual design polish + signing/notarization deferred |
| 8o | Cross-platform visual baseline / dark theme parity | **SLICE DONE — MERGED (PR #19 `d6947b2`)** — visual styling/layout only. **Dark-first** (`App.axaml RequestedThemeVariant="Dark"`, Avalonia head only) → FluentTheme renders dark, removing the light-gray button wall. `ShellTheme.axaml` expanded to **28 `Shell*` brushes** (Dark+Light): surfaces (window/header/status/panel/card/border), text, semantic palette (accent=purple, primary/secondary/success/warning/danger), chart/chip. `App.axaml` control styles: `Button.primary` (purple), `.secondary` (blue), `.nav`+`.nav.deferred`, `Border.card`, `Border.chip`. All views re-themed (hardcoded hex → brushes; `.card` surfaces; purple Start/badges; chart keys); scaffold views upgraded to `.card`. Bindings/Norwegian text unchanged. New read-only **`--visual-baseline-smoke`** (17th smoke; dark-first + palette + deferred-stay-deferred + Settings-inert + source theme-usage; headless-skip-safe); `--packaged-theme-smoke` key set expanded to 28. **Crash fix**: the 3 platform-initializing smokes intermittently SIGSEGV'd on exit (~1/12) in the NVIDIA GL `atexit` teardown after passing — now `_exit()` after the result (Linux-only) bypasses it (90 runs, 0 crashes). **Follow-up polish (same PR #19)**: Exercise Guide rows are clickable `Button.guideCard` (whole card → existing `OpenExerciseCommand`, keyboard-focusable, hover/pressed/cursor; chevron affordance retained); Dashboard chart upgraded to the shared converter-free `RuntimeChartDisplay` geometry (windowed axis via portable `PitchChartAxisRangeCalculator`, green comfort band, grid lines, current-pitch marker, y-axis Hz labels, scaled trace, centered empty-state) — display-only `DashboardChart`+`PitchTracePx`, data/pitch/target-profiles unchanged; new read-only `--visual-interaction-chart-smoke` (18th); no OxyPlot/charting dep. **Follow-up 2 (same PR #19) — WPF exercise-layout parity**: inspected frozen `FemVoiceStudio/Views/ExerciseWindow.xaml` → **WPF has NO pitch graph** on the exercise/session screen (uses metric bars + 2-col grid). So the Avalonia **Runtime view pitch chart was removed** (VM chart data model RETAINED → `--runtime-chart-feedback-smoke` unaffected) and Detail+Runtime re-laid-out as **two-column grids** (session timer/Start-Stop visible without long scrolling); Dashboard chart + Guide clickable cards kept. New read-only `--exercise-layout-parity-smoke` (19th; fixed an async-`Stop()` stop-race flake). **Follow-up 3 (same PR #19) — exercise flow + focus-aware wording**: WPF `ExerciseWindow` opens the exercise on ONE page (Detail+session together; first Start starts directly — no double-start), so the Avalonia guide now opens the exercise page (runtime view) **directly** and the redundant `ExerciseDetailViewModel`/`ExerciseDetailView` were **deleted** (one page, one Start, Back→guide; runtime VM enriched with pre-start Purpose/Steps); wording is now **focus-aware** from `GoalCategory` (FocusSummary + `Fokus:` label; pitch shown prominently only for Pitch/Combined, demoted to secondary technical detail for resonance/breath/intonation). New read-only `--exercise-flow-parity-smoke` (20th); ~12 existing smokes updated to the direct-open flow. **Follow-up 4 (same PR #19) — Exercise Guide list parity**: WPF list shows Name · Goal chip + Difficulty • Duration · Frequency chip · trimmed Description · per-exercise session count ("N økter") + chevron + a top "today's progress" card, and NO target-pitch (Hz). So the Avalonia list **removed target-pitch + verbose labels**, matched the WPF row fields, and **added display-only progress/session counts** (`0 min · 0 økter` — no persistence/analytics/DB, no invented numbers, labelled by a ProgressNote); `--exercise-flow-parity-smoke` extended with list-parity checks. Dashboard chart + Guide cards kept. No clinical/domain/WPF change, no real mic/persistence, no theme persistence, signing/notarization NOT started | Agent 11 | pixel-perfect parity + real theme switching/persistence + signing/notarization deferred |
| 8p | Desktop package signing / notarization readiness | **SLICE DONE — MERGED (PR #20 `27e5041`)** — readiness/docs/tooling only; NO real signing/notarization, NO secrets/certs/Apple account required or committed; unsigned local `.deb`/macOS publish unchanged. New `Packaging/linux/SIGNING.md` + `signing-readiness.sh` (apt-repo/detached/checksum options; CI key injection; never-commit list) and `Packaging/macos/NOTARIZATION.md` + `notarization-readiness.sh` (Developer ID cert, hardened runtime, codesign, notarytool, staple; CI secret injection). Both scripts are POSIX `sh` with `--check`/`--dry-run`/`--help` (exit 0 without secrets even when `gpg`/`dpkg-sig`/`codesign`/`xcrun` absent → reported optional/future-only; env-var **values never printed**; unknown opt → exit 2). Signing is NOT wired into `package-deb.sh`/`publish-macos.sh`. New read-only `--signing-readiness-smoke` (21st; verifies docs+scripts+flags+hides-values+unsigned-flows-intact+signing-not-mandatory+no-secrets+env-documented; source-tree inspection, skips→0 from published DLL). Gate: build 0/0, 21/21 smokes, vuln clean, Tmds 0.21.3, refs Core+Abstractions only, leak guard clean, secret-safety clean, portable 1570/1580. No clinical/domain/WPF change | Agent 11 | real `.deb` GPG/apt signing + macOS codesign/notarytool/staple (credentialed CI release job) deferred |
| 8q | macOS `.app` bundle / `.dmg` packaging readiness | **SLICE DONE — MERGED (PR #21 `8a820a7`)** — unsigned packaging/readiness only; NO real codesign/notarytool/staple, NO Apple credentials/secrets required or committed; Linux `.deb` + unsigned macOS publish unchanged. New `Packaging/macos/package-app.sh` (builds an UNSIGNED `FemVoice Studio.app` from the publish output, consuming `Info.plist`; pure file assembly, runs on any OS — verified on Linux: apphost + 46 libs in `Contents/MacOS/`), `package-dmg.sh` (`.dmg` via `hdiutil` on macOS; graceful skip→exit 0 off macOS), and `Packaging/macos/README.md`; `NOTARIZATION.md`/`Packaging/README.md` updated. Both scripts POSIX `sh` (dash-clean) with `--check`/`--dry-run`/`--help` → exit 0, unknown → exit 2, no secrets/values, write only under gitignored `artifacts/dist/<rid>/`. New read-only `--macos-packaging-readiness-smoke` (22nd; verifies docs+scripts+flags+hdiutil-handling+Info.plist-use+no-codesign/notarytool-invocation+unsigned-flows-intact+no-secrets; source-tree inspection, skips→0 from published DLL). Gate: build 0/0, 22/22 smokes, vuln clean, Tmds 0.21.3, refs Core+Abstractions only, leak guard clean, secret-safety clean, portable 1570/1580. No clinical/domain/WPF change | Agent 11 | real macOS codesign/notarytool/staple (credentialed CI) + production icon/branding deferred |
| 8r | macOS app icon / `.icns` readiness | **SLICE DONE — MERGED (PR #22 `9dc1a65`)** — icon readiness only; NO production icon/branding committed or invented, NO signing/notarization, NO secrets; existing Linux `.deb` + macOS publish/`.app`/`.dmg` flows unchanged; icon NOT required for packaging. `Info.plist` wires `CFBundleIconFile = AppIcon` (safe when absent → macOS uses generic icon, no error); `package-app.sh` copies `Packaging/macos/AppIcon.icns` into `Contents/Resources/` **only if present** (graceful deferred note otherwise; never fabricates an icon — no `iconutil`/`sips`). New docs-only `AppIcon.icns.README.md` (expected path + how to make a real `.icns` later); `macos/README.md` + `Packaging/README.md` updated. New read-only `--macos-icon-readiness-smoke` (23rd; verifies path-documented + CFBundleIconFile=AppIcon + conditional-bundle + graceful-when-absent + no-fabrication + existing-readiness-intact + no-secrets; reports `icns-committed=false` deferred, not gated; source-tree inspection, skips→0 from published DLL). Both icon paths verified on Linux (throwaway `.icns` bundled; absent → graceful, removed before commit). Gate: build 0/0, 23/23 smokes, vuln clean, Tmds 0.21.3, refs Core+Abstractions only, leak guard clean, secret+branding safety clean (no `.icns`/image committed), portable 1570/1580. No clinical/domain/WPF change | Agent 11 | real production `AppIcon.icns` (branding) + macOS codesign/notarytool/staple (credentialed CI) deferred |
| 8s | Exercise Guide category filter + search parity | **SLICE DONE — MERGED (PR #23 `77f3ed3`)** — display/UI-only, behavior-neutral; NO persistence/analytics/DB/session writes, NO clinical/exercise-definition/target-profile change, WPF untouched. WPF source-of-truth (`ExerciseWindow.xaml` + `ExerciseListViewModel`): 6 category chips (Alle/Pitch/Resonance/Intonation/Breathing/Practice) + VM-level `SearchText` matching Name OR Description (case-insensitive), combined (`matchesCategory && matchesSearch`); no prominent search TextBox in the window; list is DB-backed (not ported). Avalonia: `ExerciseGuideViewModel` gains `CategoryChips` (`"Alle"` + distinct exercise **Goal** labels — the clean WPF category axis; freeform catalog `Category` is not it), `SearchText`/`SelectedCategory`/`FilteredExercises`/`SelectCategoryCommand`/`HasResults`/`IsEmpty`; `ApplyFilter()` mirrors WPF over the in-memory cards; `Exercises`/`Categories`/`Count`/progress placeholders unchanged. New `CategoryChipViewModel` (Label + observable IsSelected → converter-free `selected` class). `ExerciseGuideView.axaml`: search `TextBox` + horizontal chip row above the list, list bound to `FilteredExercises`, empty-state TextBlock; rows/no-target-Hz/row-click/Dagens-fremgang/dark-baseline preserved. `App.axaml`: `Button.chipFilter` (+`.selected`) dark pill styles. New `--exercise-guide-filter-search-smoke` (24th; chips incl. Alle, default-all, category subset, one-chip-selected, search by name/desc, combined, empty state, clears, opens exercise, no target-Hz). Gate: build 0/0, 24/24 smokes, vuln clean, Tmds 0.21.3, refs Core+Abstractions only, leak guard clean (incl. SessionAnalyticsStore), portable 1570/1580; packaging 10/10 published smokes + deb + macOS intact. Manual Linux visual hold for screenshots | Agent 11 | (parity feature; saved-search/recommendations remain WPF-DB-backed, not ported) |
| 8t | SmartCoach + Progression deferred UI scaffold parity | **SLICE DONE (PR open, not merged)** — UI scaffold only; display-only, synthetic, clearly DEFERRED; NO SmartCoach/progression behavior, NO engines/scoring/safety-gate/analytics/persistence/microphone/clinical decisions; WPF untouched. WPF source-of-truth: `SmartCoachDashboardView` (today's-focus card + streak/sessions/health tiles, engine-backed) and `ProgressionDashboard` (level badge + progress bar + FemVoice-score + Resonance/Pitch/Intonation parameter rows, engine-backed) — reproduced VISUAL structure only with synthetic "—". New `SmartCoachScaffoldViewModel` + `ProgressionScaffoldViewModel` (sealed, **no services, parameterless ctor, not IDisposable**) + `SmartCoachScaffoldView`/`ProgressionScaffoldView` (cards/chips/tiles, dark baseline, disabled "Kommer senere" actions, empty disabled progress bars). `ShellViewModel`: Progresjon/SmartCoach nav still `IsImplemented=false` (deferred) but route to retained scaffold singletons (ShowProgression/ShowSmartCoach) instead of the bare `DeferredSurfaceViewModel`; nav-title + disposal-exclusion updated; counts unchanged (9/6/3). `MainWindow.axaml` +2 DataTemplates. New `--smartcoach-progression-ui-scaffold-smoke` (25th; nav opens inert scaffolds, no-service-deps via reflection, deferred+disabled+synthetic, 3 "—" params); `--shell-smoke` extended (Mikrofon still generic placeholder; scaffolds inert). Gate: build 0/0, 25/25 smokes, vuln clean, Tmds 0.21.3, refs Core+Abstractions only, leak guard clean (no engine/safety-gate/recovery/analytics refs — a forbidden token in a runtime SafetyNote string was caught + reworded), portable 1570/1580; packaging 11/11 published smokes + deb + macOS intact. Manual Linux visual hold for screenshots | Agent 11 | real SmartCoach/progression behavior (engine-backed) remains a future approved slice |
| 9 | SmartCoach & progression UI | TODO (display scaffold done in 8t; real engine-backed behavior deferred) | Agent 10 | Daily rec matches WPF on same data |
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
