# Windows Baseline + Audio.Windows — Gate Report

Date: 2026-06-16 · Branch: `linux-portable-core` · Draft PR open · **NOT merged; `main` untouched (`e9e0091`).**

This gate: opened a draft PR, ran the **real WPF build/test on a Windows runner via GitHub Actions** (the verification that cannot run on the Linux dev host), fixed the mechanical extraction issues it surfaced, and confirmed Linux/Avalonia regression. The only remaining item is the manual Windows microphone smoke (CI cannot exercise a real mic).

## 1. Draft PR URL
**https://github.com/asarayja/femvoice_studio_priv/pull/1** — "Draft: Extract portable core and add Avalonia/Linux bootstrap". Labels: `migration`, `avalonia`, `needs-windows-verification`, `do-not-merge`. Base `main`, head `linux-portable-core`. Repo is public; owned by `asarayja`.

## 2. Final commit tested
Windows CI verified commit **`710ed53`** (run https://github.com/asarayja/femvoice_studio_priv/actions/runs/27618290291, conclusion: success). Subsequent docs-only commits follow on the same review branch.

Commit batches (reviewable): `bc89c8a` core extraction · `92ca285` portable tests · `d391844` Avalonia shell + platform abstractions · `949bd9b` audit/migration docs + gate script · `7abf648` Audio.Windows adapter · `07a542c`/`107f7f4` gate docs · `5b7ad49` Windows CI workflow · `173b5e9` test-support link fix · `a07ba5c` InternalsVisibleTo restore · `42133e0` split-trx CI · `710ed53` ResonanceContrastDemo path fix.

## 3. Windows CI build result
**✅ GREEN.** On `windows-latest`, .NET SDK **10.0.301**: `dotnet restore` + `dotnet build FemVoiceStudio.slnx -c Debug` succeed. The WPF app (`net10.0-windows` + WPF), `FemVoice.Core`, `FemVoice.Audio.Abstractions`, `FemVoice.Audio.Windows`, and both test projects all compile against the extracted shared core.

## 4. Windows test result
Per-project trx (nothing filtered/hidden; full artifacts uploaded):
- **Portable** (`FemVoice.Tests.Portable`, net10.0): **1580 total, 1570 passed, 10 failed.**
- **Windows-only** (`FemVoiceStudio.Tests`, net10.0-windows): **301 total, 297 passed, 4 failed.**
- **Combined: 1881 total, 1867 passed, 14 failed, 0 errors.** All 14 failures are **pre-existing** (see §9), not port regressions. Details in `WINDOWS_BASELINE_TEST_RESULTS.md`.

## 5. Safety invariant test result
**✅ GREEN on Windows.** `SafetyOverrideInvariantTests`, `SafetyPriorityEngineTests`, `ManualOverrideClampTests`, `FeedbackPriorityMatrixTests`, `FeedbackConsistencyGuardTests`, `ProgressionSafetyGateTests`, `RecoveryAwareTargetZoneTests`, `ReportAssemblerTests`, `ExportWriterTests`, `ResearchNoPiiTests`, SmartCoach*, Recovery*, FemVoiceScore* — all pass (none in the failed set).

## 6. Windows-only test result
**297/301 pass.** The 4 failures are all `ThemeNoteButtonStyleTests` (`NoteRadioButtonStyle_ExistsAndCoversAllStates`/`UsesThemeBrushes`, Dark/Light) — **pre-existing**: the theme XAML they assert on is byte-identical to `origin/main`, so the result equals the pre-port baseline. Frozen theme XAML and the assertions are unchanged.

## 7. Manual mic smoke result
**⛔ PENDING — requires a Windows host with a real microphone.** CI cannot exercise capture. The adapter is build-verified on Linux + Windows; the runtime mic checklist is in `AUDIO_WINDOWS_ADAPTER_NOTES.md`. This is the one open verification item.

## 8. Linux regression result (after the Windows fixes)
**✅ GREEN.** Avalonia + portable core build on Linux; headless smoke OK; Avalonia references **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions` (no `FemVoice.Audio.Windows`/NAudio-capture/`System.Windows`/`Microsoft.Win32`/`OxyPlot.Wpf` leak). Portable tests 1570/1580 (10 pre-existing; occasional +1 `ComfortZoneControllerTests` timing flake). See `AVALONIA_LINUX_GATE_RESULTS.md`.

## 9. Known issues (all pre-existing — git-proven — or environmental)
- **14 pre-existing test failures**, none caused by the migration:
  - 9× `NewLanguageResourcesTests` (`Report_RecommendationHighFatigueFormat` placeholders `{0} {0} {1:F1}` vs neutral `{0} {1:F1}`; present even in English).
  - 1× `ExerciseGuideEncodingTests.ResourceFiles_NoMojibake_All12Resx` (asserts 12 resx; repo has 21 — stale since the 9-language expansion).
  - 4× `ThemeNoteButtonStyleTests` (theme XAML byte-identical to `main`).
- 1 intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` timing flake (~1 run in 4).
- Transitive `NU1903` advisory (`Tmds.DBus.Protocol` via Avalonia's Linux backend); Node 20 action-deprecation notice.
- Docs `AVALONIA_LINUX_FIRST_UI_SLICE_REPORT.md` / `AVALONIA_MAIN_DASHBOARD_SLICE_REPORT.md` / `AVALONIA_MAIN_DASHBOARD_GATE_RESULTS.md` referenced by the prompt **do not exist** (no dashboard slice built — only a bootstrap shell). Reported, not invented.

## 10. Behaviour changes: **NO**
No clinical/scoring/SmartCoach/health/recovery/safety-gate/progression/report/localization-semantics/diagnostics/analytics/persistence/exercise-catalog behaviour changed. Changes were: code relocations (namespaces preserved), two behaviour-neutral type extractions, additive DI wiring + the audio adapter (pure delegation), three **mechanical** test/assembly compat fixes (TestDatabaseService link, `InternalsVisibleTo` restore, ResonanceContrastDemo path), the CI workflow, commits/PR, and docs. No assertions or product resources were modified.

## 11. Recommendation: **KEEP BLOCKED (continue review) — one item from merge-ready**
All automated gates are green: WPF builds on Windows, the safety-invariant + Windows-only + portable suites pass except the 14 documented pre-existing failures, Linux/Avalonia regression is clean, and no behaviour changed. **The single remaining gate item is the manual Windows microphone smoke** for `NAudioCaptureService` (needs a human + a Windows mic — I cannot perform it). Per the work order ("do not merge unless the report explicitly says merge-ready"), this is **not merge-ready yet**; it becomes merge-ready once the mic smoke is recorded in `AUDIO_WINDOWS_ADAPTER_NOTES.md`. Optionally, the team may also fix the 14 pre-existing failures as a separate, approved change before/after merge.

## Stop condition
Stopping after this report. Do not merge until merge-ready (manual mic smoke recorded). Do not start Avalonia dashboard parity until this gate is reviewed.
