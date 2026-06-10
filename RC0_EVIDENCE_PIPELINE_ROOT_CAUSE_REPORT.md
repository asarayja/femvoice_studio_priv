# RC-0 Evidence Pipeline — Root Cause Report

Date: 2026-06-10
Investigation: 28-agent workflow (9 parallel investigators, synthesis, 18 adversarial
verifiers — all 6 load-bearing claims confirmed 3/3), followed by implementation and a
4-dimension adversarial review of the fix.

## TL;DR

**Real sessions never produced real evidence because the evidence pipeline's final
stage was never wired, and the "evidence" files you were reading were static
placeholders that a unit test actively re-stamped as BLOCKED on every `dotnet test`
run.** Nothing was failing at runtime — the export simply never executed, and the
flag you set (`EnableRc0Diagnostics`) did not exist in the code and was set in a
file the app never reads.

## Root causes (all verified with file:line evidence, none refuted)

1. **`Rc0EvidenceExporter.Export()` had zero call sites** in app and test code
   (`Services/Rc0EvidenceExporter.cs`). No session-end path ever invoked it, so no
   real session could produce an evidence package. *(PRIMARY)*

2. **The four repo-root RC0 files were causally disconnected from runtime.**
   `RC0_VERIFICATION_REPORT.md` + `RC0_VERIFICATION_EVIDENCE.json` were regenerated
   into the repo root by `Rc0SystemVerificationReportTests` from a hardcoded
   `CreateBlockedBaseline()` (fixed timestamp 2026-06-10T00:00:00Z) — the test even
   asserted `Result == "BLOCKED"`. `RC0_EVIDENCE.json` +
   `RC0_AUDIO_PIPELINE_DIAGNOSTIC_REPORT.md` were hand-committed placeholders
   containing values the exporter is structurally incapable of emitting
   (`SessionId: null` from a non-nullable `int`; `CaptureStatus: "NOT_RUN"` which no
   code path produces). All four were born in commit `6bf30df` (2026-06-10), the same
   commit that added the RC0 services. *(PRIMARY, co-equal)*

3. **`EnableRc0Diagnostics` was a double no-op:** the string appeared in zero files
   in the repo (no code read it), and the file you edited
   (`FemVoiceStudio/settings.json` in the repo) is never read at runtime — the app
   reads **`%USERPROFILE%\Documents\FemVoiceStudio\settings.json`**
   (`DebugSettingsService.cs`, `ThemeManager.cs`, `FirstTimeSetupService.cs`).
   Additionally, the settings models had no `[JsonExtensionData]`, so a hand-added
   unknown key was silently stripped on the next lossy save round-trip (theme change,
   first-time setup).

4. **The runtime traces that DID exist landed where nobody looks** —
   `Rc0RuntimeLog` writes (ungated) to
   **`%LOCALAPPDATA%\FemVoiceStudio\RC0_Runtime\RC0_RUNTIME_<timestamp>.txt`** — and
   only for builds containing commit `6bf30df` (2026-06-10 13:01). The `publish/`
   build on disk is from Jun 7 and `bin/Debug` from Jun 8: **sessions run on those
   binaries write no RC0 output at all, which alone fully explains an empty
   RC0_Runtime folder.**

5. **Even with Export wired, every exercise session would have reported
   BLOCKED/FAIL:** the exercise pipeline (`ExerciseWindow` drives
   `PitchDetectionService` directly) had no pitch/resonance/graph counters
   (`ResolveResult` returns BLOCKED when `PitchDetectorCalledCount <= 0`), and a
   clean stop was misclassified as `CAPTURE_STOPS` (`_isRecording` was set false
   *before* the diagnostics snapshot in `AudioCaptureService.StopRecording`), which
   `ResolveResult` maps to FAIL.

6. **Systemic silent-failure design** meant none of the above could announce itself:
   bare `catch { }` in `Rc0RuntimeLog.Write`, ~30 catch handlers logging only via
   `Debug.WriteLine` (compiled out of Release builds), no global exception handlers,
   no `OnExit`, and a misleading "No RC0 runtime log was available" message on a
   failed `File.Copy`.

## Which agents found what

| Agent | Classification |
|---|---|
| 1 Runtime paths | `ONLY_PLACEHOLDER_FILES_FOUND`, `FILES_WRITTEN_TO_UNEXPECTED_LOCATION` |
| 2 Settings load | `WRONG_SETTINGS_FILE`, `DEBUG_FLAGS_ONLY_APPLY_TO_CSV_NOT_RC0`, `ENABLE_RC0_DIAGNOSTICS_FALSE_AT_RUNTIME`, `SETTINGS_OVERWRITTEN` |
| 3 Bootstrap | `BOOTSTRAP_NOT_CALLED`, `BOOTSTRAP_ONLY_IN_REPO_TESTS`, `BOOTSTRAP_FAILS_SILENTLY` |
| 4 DI & lifetime | `TEST_HARNESS_ONLY`, `SERVICE_NOT_REGISTERED` (static classes, no DI), `MULTIPLE_INSTANCES_WRITING_DIFFERENT_PATHS` |
| 5 Session lifecycle | `SESSION_SAVED_BUT_EVIDENCE_NOT_EXPORTED`, `EXERCISE_WINDOW_BYPASSES_RC0_LOGGING` |
| 6 Audio pipeline | `INSTRUMENTATION_GAPS` (exercise counters missing, 6/10 failure codes never assigned, no watchdog, clean stop → CAPTURE_STOPS) |
| 7 Score & graph | `SCORE_TOO_FEW_VALID_SAMPLES`/`SCORE_LOW_REAL_INPUT` (the ~8% is the real clinical formula on near-empty data, **not** a hardcoded fallback), `GRAPH_FILTERS_OUT_OF_RANGE_PITCH` (PitchTraceStabilizer rejects <60/>340 Hz), `DASHBOARD_PIPELINE_DIFFERENT_FROM_EXERCISE_PIPELINE` |
| 8 Write failures | `NO_WRITE_ATTEMPT_MADE` (primary), `SILENT_EXCEPTION`, plus OneDrive/CFA risk paths |
| 9 External research | OneDrive Known Folder Move redirects `Documents`; Windows mic privacy for desktop apps; Controlled Folder Access silently blocks Documents writes; `Debug.WriteLine` is `[Conditional("DEBUG")]`; WaveInEvent DataAvailable stalls |

Note on the score/graph question: the ~8% score is **`ClinicalSessionScore` running
honestly on a session with almost no accepted voice data** — the fix therefore adds
`ScoreSource` evidence (`CLINICAL_SESSION_SCORE` vs `NO_VERIFIED_VOICE_DATA`) instead
of touching the formula. The front-page graph stops because the stabilizer/range
filters reject the incoming pitch — the fix adds skip-reason counters
(`FrontPageGraph` log area) instead of changing the filters.

## What was changed (all observation-only; no clinical scoring, thresholds, or exercise logic touched)

1. **Defused the placeholder poisoning** — `Rc0SystemVerificationReportTests` now
   writes to a temp directory; the four placeholder files moved to
   `dokumentasjon/rc0-templates/` with a README.
2. **`Services/Rc0WriteFailureSink.cs` (new)** — on any RC0/settings write failure,
   appends one line to `RC0_LOGGING_FAILURE.txt` trying
   `Documents\FemVoiceStudio\logs` → `%LOCALAPPDATA%\FemVoiceStudio\logs` →
   `AppContext.BaseDirectory\logs`; `FirstWriteError` is embedded in
   `RC0_ERRORS_ONLY.txt`. Wired into every previously-mute catch in the chain.
3. **`EnableRc0Diagnostics` is now real** — added to `DebugSettings`, loaded/saved by
   `DebugSettingsService`, with `[JsonExtensionData]` on `AppSettings`/`DebugSettings`
   and a shared `AppSettingsJson.Options` (string enums) across all three settings
   writers so hand-edits survive round-trips.
4. **`Services/Rc0StartupBootstrap.cs` (new)**, called first in `App.OnStartup`:
   always logs `AppStartup`/`Paths`/`Settings` lines (the Paths line exposes OneDrive
   redirection); when the flag is on, creates
   `%LOCALAPPDATA%\FemVoiceStudio\RC0_Evidence\` with `RC0_STARTUP_SENTINEL.txt`,
   `RC0_RUNTIME_LOG.txt`, `RC0_ERRORS_ONLY.txt`, `RC0_EVIDENCE.json`
   (`StartupCompleted: true`, `CaptureStatus: "STARTUP_ONLY"`) and
   `RC0_AUDIO_PIPELINE_DIAGNOSTIC_REPORT.md`, plus a sentinel copy to Documents.
   `App` also got `OnExit` logging and global
   Dispatcher/AppDomain/UnobservedTask exception logging (non-behavior-changing).
5. **`AudioCaptureService`**: clean stop no longer stamped `CAPTURE_STOPS`
   (snapshot taken before stop + `_stopRequested`); `DEVICE_SELECTION_ERROR` and
   `WINDOWS_OR_DRIVER_LEVEL_ISSUE` now assigned at their natural sites; terminal log
   line in `HandleRecordingStopped` (including the previously invisible
   "stopped unexpectedly without exception" case); independent 2s watchdog timer that
   logs when `DataAvailable` stalls while recording; `PipelineLabel` tag
   (`Exercise`/`FrontPage`/`Calibration`) on all log areas.
6. **`ExerciseWindow`**: RC-0 counters around the existing `DetectPitch` call
   (called/accepted/rejected), `ResonanceSamplesCount`, `GraphUpdateCount`,
   `ScoreSource`; lifecycle log lines (selected/started/stopped/aborted/saved/score);
   per-second `ExerciseFeed` line (gated on the flag) showing rms, voiced, and
   engine-vs-fallback resonance source; **`Rc0EvidenceExporter.Export()` is now
   called at session end** with the audio snapshot taken *before* the capture is
   disposed.
7. **Dashboard**: `MainViewModel` logs init/start/stop failures, `SessionSaved`, and
   exports a monitor evidence package at stop (Test B); `MainWindow` counts graph
   renders and skip reasons (`notRecording` / `noPitch` / `stabilizerRejected` /
   `duplicate`) with a 1 Hz `FrontPageGraph` log line.
8. **Persistence/calibration/report hooks**: `ExerciseDataService.CompleteSession`,
   `DatabaseService.SaveTrainingSession`, the three swallowed persist-catches in
   `ExerciseSessionRecorder`, `ResonanceWindow` save-catch, calibration completion,
   and report generation all log to the runtime log now.
9. **`Rc0EvidenceExporter`**: evidence root moved to
   `%LOCALAPPDATA%\FemVoiceStudio\RC0_Evidence` (immune to OneDrive KFM and
   Controlled Folder Access) with a best-effort mirror copy to
   `Documents\FemVoiceStudio\RC0_Evidence`; honest error message on a failed log
   copy; `Export` wrapped so it can never break session end.

Both projects build green with `-p:EnableWindowsTargeting=true`. Tests cannot run on
this Linux machine (no WindowsDesktop runtime) — Windows-side validation required.

## Adversarial review of the fix (30 agents, 4 dimensions, every finding double-verified)

A second multi-agent workflow reviewed the diff (threading, clinical purity, RC-0
acceptance coverage, regressions). The clinical-purity dimension confirmed the change
is **observation-only**: clinical scoring, thresholds, exercise logic, audio
processing and settings semantics are untouched, and no new throw path reaches
capture/exercise flow. 13 confirmed findings (none refuted by the skeptic pass) were
fixed before delivery:

- **Quiet-stop false FAIL (critical)**: the stop-time snapshot classifies the *last*
  audio frame, so being silent when clicking Stop yielded
  `SILENCE_GATE`/`SIGNAL_LEVEL_COLLAPSES` → FAIL. `ResolveResult` now fails only on
  hard classifications (`CAPTURE_STOPS`, `DEVICE_SELECTION_ERROR`,
  `WINDOWS_OR_DRIVER_LEVEL_ISSUE`); soft last-frame states map to WARNING.
- **Unguarded `DebugSettingsService.Instance` (critical)**: the singleton ctor could
  throw into audio callbacks via the new gating checks — the ctor now never throws
  (failures go to the sink).
- **PASS was unreachable**: both export sites hardcoded `Warnings`, and any warning
  capped the result at WARNING. Fixed-text explanations moved to a new `Notes` field.
- **Counters read after awaits**: a session restarted mid-`OnStopClick` reset the
  counters before export read them — all counters/score-source/start-time are now
  snapshotted before the first await (same fix in `MainViewModel.StopRecording`).
- **Watchdog missed "zero callbacks ever"**: stall is now measured from
  `StartRecording` when no `DataAvailable` has arrived — the canonical
  mic-privacy-blocked case.
- **Hand-edited settings fragility**: settings JSON parsing is now case-insensitive.
- Minor hardening: evidence folder name collision (same-second exports) gets a
  suffix; runtime-log copies happen under the write lock (`Rc0RuntimeLog.TryCopyTo`);
  bootstrap writes each baseline file in its own try; global exception-handler
  lambdas are try-wrapped; the front-page graph logs a final counter line at the
  recording→stopped transition (Test B "graph stop reason"); test temp-dir cleanup
  can no longer mask the test result.

Known accepted trade-off from review: settings.json written by this build serializes
`Theme` as a string; **older builds** of the app would fail to parse it and reset the
file (forward-only compatibility).

## Actual paths (resolved for Windows)

| What | Path |
|---|---|
| Settings the app READS | `%USERPROFILE%\Documents\FemVoiceStudio\settings.json` (⚠ may be `...\OneDrive\Documents\...` with Known Folder Move) |
| Runtime log | `%LOCALAPPDATA%\FemVoiceStudio\RC0_Runtime\RC0_RUNTIME_<launch-timestamp>.txt` (one file per app launch) |
| Evidence root (new) | `%LOCALAPPDATA%\FemVoiceStudio\RC0_Evidence\` (+ mirror in `Documents\FemVoiceStudio\RC0_Evidence\`) |
| Per-session evidence | `...\RC0_Evidence\RC0_EVIDENCE_<yyyy-MM-dd_HHmmss>\` (7 files incl. `RC0_EVIDENCE.json`) |
| Write-failure fallback | `RC0_LOGGING_FAILURE.txt` in `Documents\FemVoiceStudio\logs` → `%LOCALAPPDATA%\FemVoiceStudio\logs` → app folder `\logs` |
| Repo `FemVoiceStudio/settings.json` | **Never read at runtime** — now marked as a template/decoy |

## Before / after evidence JSON

Before (static placeholder, committed to git — now in `dokumentasjon/rc0-templates/`):

```json
{ "SessionId": null, "ExerciseId": null, "AudioStarted": false,
  "DataAvailableCount": 0, "CaptureStatus": "NOT_RUN",
  "ScoreSource": "NOT_RUN", "FailureClassification": "UNKNOWN",
  "Result": "BLOCKED" }
```

After (written per real session by `Rc0EvidenceExporter.Export` at `OnStopClick`):

```json
{ "SessionId": 17, "ExerciseId": 3, "ExerciseName": "Grunnleggende humming",
  "AudioStarted": true, "DataAvailableCount": 412, "PitchSamplesCount": 87,
  "CaptureStatus": "UNKNOWN_OR_OK", "ScoreSource": "CLINICAL_SESSION_SCORE",
  "GraphUpdateCount": 95, "FailureClassification": "UNKNOWN",
  "Notes": ["PitchDetectorCalledCount counts 100ms-throttled analysis frames ..."],
  "Result": "PASS" }
```

(Values illustrative; every field now has a real producer.)

## Lifecycle events now connected

App startup ✔ (bootstrap) · Dashboard monitor start/stop ✔ · Calibration complete ✔
· Exercise selected ✔ · Start Økt ✔ (SessionId+ExerciseId) · Audio capture start ✔
· DataAvailable health 1 Hz ✔ + watchdog ✔ · Pitch detected/rejected ✔ (both
pipelines) · Graph updates ✔ (both pipelines) · Exercise ended ✔ · Session saved ✔
(both DB paths) · Score + ScoreSource ✔ · Evidence exported ✔ · Reports generated ✔
· App shutdown ✔ (OnExit) · Unhandled exceptions ✔

Remaining gaps (known, deliberate):
- **Calibration START** is not logged (only completion); capture init within the
  calibration window is logged via the `Calibration` pipeline tag.
- **PersistenceReadBack** is reported `false` — no read-back verification is
  performed (would require a new DB read at session end).
- **Exercise pitch counters count 100 ms-throttled analysis frames (~10/s)**, not raw
  audio callbacks (~43/s) — do not compare 1:1 with the dashboard's per-callback
  counters. Noted in the evidence Warnings.
- The four `ReportsGenerated` booleans in session evidence stay `false`; report
  generation is logged separately when actually run from the report UI.

## Windows-side actions required (do these in order)

1. **Rebuild/republish from the current code.** The `publish/` (Jun 7) and
   `bin/Debug` (Jun 8) binaries predate ALL RC0 instrumentation — any session run on
   them writes nothing, with no further bug to find.
2. Edit **`%USERPROFILE%\Documents\FemVoiceStudio\settings.json`** (NOT the repo
   file) and set:
   ```json
   "Debug": { "EnablePitchDebug": true, "EnableAnalyzerDebug": true, "EnableRc0Diagnostics": true }
   ```
   If `Documents` is OneDrive-redirected, the file is under `OneDrive\Documents\...`.
3. **Test A** — start the app, click nothing. Check
   `%LOCALAPPDATA%\FemVoiceStudio\RC0_Evidence\` for the five startup files and that
   `RC0_STARTUP_SENTINEL.txt` shows `EnableRc0Diagnostics=true` and the real paths.
4. **Test B** — front page, speak 20 s, stop. Check the newest
   `RC0_RUNTIME_*.txt` in `%LOCALAPPDATA%\FemVoiceStudio\RC0_Runtime\` for
   `FrontPagePitchMonitor`, `AudioCaptureHealth/FrontPage`, `PitchPipeline`,
   `FrontPageGraph` lines, then a `SessionSaved` + `EvidenceExported` line.
5. **Test C** — run *Grunnleggende humming* 10 s, stop and save. Open the newest
   `RC0_EVIDENCE_*` folder: `RC0_EVIDENCE.json` must show real
   SessionId/ExerciseId/Name, `AudioStarted: true`, `DataAvailableCount > 0`, and
   `ScoreSource` set.
6. If any file is missing: look for **`RC0_LOGGING_FAILURE.txt`** in the three
   fallback locations; if `StartRecording OK` appears but `DataAvailableCount` stays
   0, check *Windows Settings → Privacy → Microphone → Let desktop apps access your
   microphone*; also check Controlled Folder Access if Documents writes fail.
7. Housekeeping (optional): the repo still tracks `femvoice_priv_mirror.git/` (a bare
   mirror accidentally committed in `bd32aeb`) and a stray 0-byte
   `FemVoiceStudio/Audio/test_write.txt` from `6bf30df`.

## Next RC-0 action

Run Tests A→B→C on a fresh Windows build. **Do not resume audio algorithm or
threshold tuning until Test C produces an evidence folder with real values** — then
use `FailureClassification`, `ScoreSource`, `ExerciseFeed` and `FrontPageGraph`
evidence to classify the ~8% score and graph-stop symptoms before changing anything
clinical.
