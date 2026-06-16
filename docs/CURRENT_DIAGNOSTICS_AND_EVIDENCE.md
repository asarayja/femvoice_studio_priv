# FemVoice Studio — Current Diagnostics & Evidence (WPF Baseline)

Audit date: 2026-06-16 · Read-only. — CONFIRMED unless noted. This subsystem must be preserved during the port.

---

## 1. What "RC-0" is — CONFIRMED

RC-0 is the **release-candidate-zero validation profile** (`DiagnosticsNaming.ValidationProfile = "RC0"`). The RC-0 *evidence pipeline* is a developer-only, **never-throws** diagnostics subsystem that proves with on-disk artifacts that an exercise session's audio → pitch → resonance → graph → persistence → reports chain actually ran and what it produced. It is **not** a product feature, adds no UI surface, and never influences any Safety/Health/Recovery decision.

### Session classification — CONFIRMED

`Rc0EvidenceExporter.Export` writes a `SessionEvidence` record + an `AudioCaptureDiagnosticsSnapshot`; `ResolveResult` classifies **PASS / WARNING / FAIL / BLOCKED**:

- **BLOCKED** — `DataAvailableCount <= 0` or `PitchDetectorCalledCount <= 0`.
- **FAIL** — hard audio failure (CAPTURE_STOPS, DEVICE_SELECTION_ERROR, WINDOWS_OR_DRIVER_LEVEL_ISSUE), any `Errors`, no persistence/analytics, or zero pitch samples.
- **WARNING** — soft warnings or a non-UNKNOWN failure classification.
- **PASS** — otherwise.

## 2. Startup bootstrap — CONFIRMED

`Rc0StartupBootstrap.Run()` is called from `App.OnStartup` **before** DI (`App.xaml.cs:30`, ahead of `ConfigureServices`/`BuildServiceProvider`). It:

1. Always writes two `Rc0RuntimeLog` lines: an `AppStartup` banner (version/OS/process path) and a `Paths` line exposing resolved Settings/RuntimeLog/EvidenceRoot/Documents/LocalAppData paths (surfacing OneDrive Known-Folder-Move redirection).
2. Returns early unless `DebugSettingsService.Instance.EnableRc0Diagnostics` is true.
3. If enabled, writes baseline files (each in its own try): `STARTUP_SENTINEL.txt`, `RUNTIME_LOG.txt`, `ERRORS_ONLY.txt`, `EVIDENCE.json` (CaptureStatus `STARTUP_ONLY`), `AUDIO_PIPELINE_DIAGNOSTIC_REPORT.md`, RC0-aliased copies, a Documents-mirror sentinel, and a legacy `RC0_*` copy.
4. Wraps everything in try/catch → `Rc0WriteFailureSink`. **Never throws** (a startup crash still leaves baseline evidence).

`App.xaml.cs` also registers global exception logging (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` → `Rc0RuntimeLog.Write`).

## 3. File locations — CONFIRMED (`DiagnosticsNaming.cs`)

| Artifact | Path |
| --- | --- |
| Primary evidence root | `%LOCALAPPDATA%\FemVoiceStudio\Diagnostics` (per-session folders `EVIDENCE_yyyy-MM-dd_HHmmss`) — chosen because LOCALAPPDATA is never OneDrive-redirected nor subject to Controlled Folder Access |
| Documents mirror (visibility) | `%USERPROFILE%\Documents\FemVoiceStudio\Diagnostics` |
| Runtime log | `%LOCALAPPDATA%\FemVoiceStudio\RuntimeDiagnostics\RUNTIME_<timestamp>.txt` |
| Legacy RC-0 aliases (when `EnableRc0CompatibilityExport`) | `…\FemVoiceStudio\RC0_Evidence` in both LOCALAPPDATA and Documents, `RC0_`-prefixed names |
| Write-failure sink | `RC0_LOGGING_FAILURE.txt` in first writable of `Documents\FemVoiceStudio\logs`, `%LOCALAPPDATA%\FemVoiceStudio\logs`, `AppContext.BaseDirectory\logs` |

> **Doc drift (OUTDATED):** the older `RC0_EVIDENCE_PIPELINE_ROOT_CAUSE_REPORT.md` describes `…\RC0_Evidence`/`…\RC0_Runtime` roots. The current code uses neutral `Diagnostics`/`RuntimeDiagnostics` roots, keeping `RC0_Evidence` only as a legacy compatibility mirror.

These paths use `%LOCALAPPDATA%`/`%USERPROFILE%\Documents` — **Windows-specific path shapes**; on other OSes `LocalApplicationData`/`MyDocuments` resolve elsewhere (relevant only if cross-platform diagnostics are ever targeted).

## 4. Support package — CONFIRMED (`SupportPackageService.cs`)

Produces a single **`.zip`**: `Documents\FemVoiceStudio\SupportPackages\FemVoiceStudio_SupportPackage_<yyyyMMdd_HHmmss>.zip` (format v1).

- **Includes (latest of each, if present):** `EVIDENCE.json`, `VERIFICATION_REPORT.md`, `AUDIO_PIPELINE_DIAGNOSTIC_REPORT.md`, `ERRORS_ONLY.txt`, `SCREENSHOT_CHECKLIST.md`, `SESSION_SUMMARY.md`, latest `RUNTIME_*.txt`, plus generated `app-version.json`, `system-summary.json` (OS / 64-bit / CPU count only), `privacy-summary.json` (`PrivacyConsentPolicy.Snapshot()`), `settings-summary.json` (non-sensitive only), and `manifest.json` (lists included + excluded-sensitive files).
- **Excluded for privacy by default:** professional free-text notes, clinical note bodies, personal notes, identifiable research exports, secrets, raw private user text. Settings summary strips any key containing secret/token/password/apikey (`PrivacyConsentPolicy.IsSensitiveSettingsKey`). Professional free text is included **only** if `SupportPackageOptions.IncludeProfessionalFreeText` is explicitly set (adds a warning JSON).
- All failures route to `Rc0WriteFailureSink` and return a safe failure message.

## 5. Research anonymization & participant token — CONFIRMED

- `ResearchAnonymizer.cs` — pure transform `RawResearchRow` → `ResearchParticipantRow`. PII removed per row: integer `UserId` → opaque participant token; `Timestamp` → truncated to UTC calendar day (time-of-day discarded); microphone `DeviceName` → dropped (only `SignalToNoiseDb` + `HasCalibration` survive); all free text (`SubjectiveNote`, `ClinicalNoteBody`) → dropped. The output graph has **no field** able to carry name/device/raw note/clock time — PII-free by construction.
- `ResearchAggregator.cs` — group-level means/shares; flags `HasSufficientCohort = false` below `MinimumCohortSize = 5` with a `VolumeCaveatNote` (the N=1 caveat).
- `ParticipantTokenProvider.cs` — opaque per-install UUID (`Guid.NewGuid("D")`), minted on first run, stored in `%LOCALAPPDATA%\FemVoiceStudio\Research\participant-token.json` — deliberately **outside** `femvoice.db` so it can't be joined to the integer UserId. Corrupt file → re-mint. Directory + UUID factory injectable for tests.

## 6. Audit trail — CONFIRMED

`AuditEvent` is immutable and **strictly append-only** (INSERT-only, no UNIQUE on AuditId → duplicate writes produce two rows). Records role labels (never raw usernames), machine reason codes, and before/after JSON. Backed by `AuditTrailStore`/`SqliteAuditTrailRepository` (WAL mode) in `femvoice.db`.

## 7. Pilot readiness — CONFIRMED

`PilotReadinessChecker.cs` — read-only harness, 6 guarded checks, never gates anything (descriptive): (1) all five professional stores constructible/schema-ready via write-read round-trip; (2) audit trail append-only; (3) anonymizer output PII-free; (4) export non-empty for JSON/CSV/PDF; (5) outcome tracking round-trips by id and latest-for-user; (6) safety-override invariant (override under a blocked gate never raises a target above baseline). Uses fixed probe `UserId = -987654`; `now` injected. `IsPilotReady` = all pass.

## 8. Coupling — CONFIRMED (minimal)

Grep across all diagnostics/research files: **no** `System.Windows`/`Dispatcher`/`Application.Current`/media types. They use `System.IO`/`System.Text.Json`/`System.IO.Compression`. **Caveat:** several reference `ThemeManager.SettingsPath`, and `ThemeManager.cs` itself imports `System.Windows`/`Application.Current` — a minor static dependency chain to flag (the diagnostics logic itself is UI-free).

## 9. RC-0 docs context — CONFIRMED

- `RC0_EVIDENCE_PIPELINE_ROOT_CAUSE_REPORT.md` (2026-06-10) documents that the gate was previously *fake*: `Rc0EvidenceExporter.Export()` had no call sites; the four repo-root `RC0_*` files were static placeholders; `RC0_VERIFICATION_*` were re-stamped `BLOCKED` on every `dotnet test`; `EnableRc0Diagnostics` read from the wrong settings file; logs landed in unviewed locations; silent `catch{}` hid all of it. The current code addresses these (real LOCALAPPDATA paths, the bootstrap, the write-failure sink).
- `dokumentasjon/rc0-templates/` holds canonical output shapes: `RC0_EVIDENCE.json`, `RC0_VERIFICATION_REPORT.md`, `RC0_AUDIO_PIPELINE_DIAGNOSTIC_REPORT.md`, `RC0_VERIFICATION_EVIDENCE.json` + a README explaining the placeholders.
- `RC0_HYDRATION_REVIEW.md` enforces RC-0 *scope discipline* (no new subsystems/dashboards/migrations under the gate) and flags a hydration anti-repeat singleton-state leak.

## 10. Avalonia portability verdict — CONFIRMED

Entirely portable C# (file I/O + JSON + zip + crypto). The only things to revisit for a cross-platform port are the **Windows path shapes** and the incidental dependency on `ThemeManager.SettingsPath`. Recommendation: place diagnostics/evidence/research in a shared `FemVoice.Diagnostics` module, abstract the settings-path source so it does not pull in the WPF `ThemeManager`. **Do not remove or weaken any diagnostics/evidence behaviour during the port.**
