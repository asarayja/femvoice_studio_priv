# Avalonia Runtime Chart + Live Feedback — Slice Plan (PREP ONLY)

Date: 2026-06-16 · Branch: `avalonia-runtime-chart-feedback-slice` (off `main` @ `a8df6ec`, incl. PR #1–#7).

> **Status: PLANNING ONLY — no chart/feedback code in this prompt.** This branch currently equals `main`
> plus this plan doc. The slice adds a **display-only, non-clinical** visual feedback layer to the
> Exercise Runtime screen.

## 1. Current merged baseline
`main` (`a8df6ec`): portable core, Audio.Abstractions/Windows, Avalonia shell + dashboard, Exercise Guide +
Detail, Exercise Runtime scaffold, Runtime target-profile integration, and **Exercise Coordinator Readout**
(VM-local parameterless `ExerciseIntelligenceCoordinator`, display-only). 6 smokes green; build 0 warnings;
no vulnerable packages; leak guard clean (2 documentary comments); Avalonia refs only `FemVoice.Core` +
`FemVoice.Audio.Abstractions`; portable 1570/1580.

## 2. Current runtime / coordinator behaviour (today)
`ExerciseRuntimeViewModel`/`ExerciseRuntimeView`: dedicated `SyntheticAudioCaptureService` (aimed at the
target-band midpoint) → shared DSP (`PitchDetectionService`/`PitchTraceStabilizer`/`LiveMetricsService`,
read-only) → `CurrentPitch`, pitch-vs-band `PitchStatus`, **display-only** derived hold (`HoldSeconds`/
`HoldProgressPercent`, target = profile `RequiredHoldSeconds`) and `ElapsedSeconds`; a read-only "Mål-profil"
panel; a display-only "Koordinator-readout" panel (coordinator hold vs derived hold, non-enforced safety-lock
text). Feedback today is the single text field `RuntimeStatusMessage`, derived locally from in/under/over-band
+ hold conditions. **No chart, no pitch-sample trace, no visual band/marker.**

## 3. Existing chart / feedback assets (inspected)
- **Avalonia dashboard pitch trace** (`Views/DashboardView.axaml` 58–80): a **converter-free placeholder** —
  an `ItemsControl` over `MainDashboardViewModel.PitchSamples` (`ObservableCollection<double>`, capped at
  `MaxTracePoints`), each value a bottom-aligned `Rectangle` of `Height = pitch (Hz)`. No converter, no
  third-party chart dep, builds reliably (see `docs/AVALONIA_CHART_PORT_NOTES.md`). **Proven, reusable.**
- **`MainDashboardViewModel.DeriveFeedback(voiced, stability, health, pitch)`** (line 157): a **local,
  display-only** feedback-string derivation from `StabilityState`/`HealthState`/pitch — explicitly NOT a
  `FeedbackConsistencyGuard` invocation (comment lines 19–21). **The safe feedback pattern to mirror.**
- **`PitchChartAxisRangeCalculator`** (`FemVoice.Core/Services/`, **portable**, unit-tested in
  `FemVoice.Tests.Portable`): pure Y-axis range math. Reusable for chart scaling.
- **`PitchChartViewModel`** (`FemVoiceStudio/Views/`): **WPF-coupled** (builds an OxyPlot `PlotModel`); not
  directly portable. Logic is largely portable but tied to `OxyPlot.Wpf`.
- **`docs/AVALONIA_CHART_PORT_NOTES.md`**: documents why OxyPlot.Avalonia was deferred and the eventual path.

## 4. OxyPlot.Avalonia availability
**NOT available / not referenced.** The only OxyPlot reference in the repo is `OxyPlot.Wpf 2.1.2` in the
WPF app (`FemVoiceStudio.csproj`). `FemVoice.Avalonia` has **no** OxyPlot package. Per
`AVALONIA_CHART_PORT_NOTES.md`, `OxyPlot.Avalonia`'s compatibility with **Avalonia 11.2.1** is unvalidated
(OxyPlot 2.x host-control/binding changes), and `AnalysisChartTheme` reads WPF brushes
(`System.Windows.Media` + `Application.Current.TryFindResource`) that must be abstracted before reuse.
Adding it now would introduce a new dependency + version-compat risk + a fresh vulnerability-scan surface.
**Recommendation: do NOT add OxyPlot.Avalonia in this slice** — use the converter-free native-Avalonia
approach; keep OxyPlot.Avalonia for a dedicated future "chart parity" slice.

## 5. Proposed chart data model
Reuse the dashboard pattern in the runtime VM (display-only):
- `ObservableCollection<double> PitchSamples` on `ExerciseRuntimeViewModel`, fed the **stabilized** pitch each
  frame (the value already computed), capped at a `MaxTracePoints` constant; cleared on Begin/Stop.
- Optionally a small immutable sample record later (`(double pitch, bool inBand)`) if per-sample colouring is
  wanted; start with `double` (matches the dashboard, zero risk).
- Y-scaling via `PitchChartAxisRangeCalculator` (portable) so the trace and the band overlay share one range.
- No persistence; no new types in `FemVoice.Core` required (the calculator already exists there).

## 6. Target-band rendering approach (or placeholder)
- **Preferred (safe):** a native-Avalonia overlay — a `Panel`/`Grid` with a semi-transparent `Rectangle`
  spanning the exercise's `TargetPitchMin…TargetPitchMax` (from `EnhancedExercise`/`ExerciseTargetProfile`,
  already on the VM) mapped into the same pixel range as the trace, plus a horizontal "current pitch" marker
  line. All converter-free (computed positions exposed as VM doubles), display-only.
- **Fallback placeholder:** if precise band mapping proves fiddly within Avalonia layout, render the band as
  labelled min/max guide lines (like the dashboard's comfort-zone numbers) and clearly mark it
  "visuell veiledning (omtrentlig)". Document whichever is shipped.

## 7. Proposed feedback readout approach
- **Mirror the dashboard's local `DeriveFeedback`**: a display-only runtime feedback string derived from safe
  conditions already available (voiced/no-voice, under/in/over band, hold progress, hold complete). The
  runtime already does a lightweight version in `RuntimeStatusMessage`; this slice promotes it to a dedicated
  feedback readout (and may add a short secondary line, e.g. distance-to-band or hold-remaining), still local
  and non-clinical.
- No `SmartCoach`, no `FeedbackPipeline`, no `FeedbackConsistencyGuard` invocation (see §8).

## 8. FeedbackConsistencyGuard safety assessment
`FeedbackConsistencyGuard` (`FemVoice.Core/Services/FeedbackConsistencyGuard.cs`) is **pure in-memory**: its
ctor takes only `Func<DateTime>? clock`, `TimeSpan? minimumInterval`, `int escalationThreshold` — **no DB,
no recorder, no persistence** (verified). `Submit(FeedbackCandidate, FeedbackGuardContext?) → FeedbackDecision`
is a deterministic decision; `BeginSession()` manages in-memory session state; it raises
Approved/Suppressed/Escalated events. So it *could* be driven read-only with a synthetic `FeedbackCandidate`.
**However:** (a) it is one of the **behaviour-frozen systems** (must not change its contract/behaviour);
(b) feeding it correctly needs a non-trivial adapter to build `FeedbackCandidate` (priority, reason codes,
timing) + `FeedbackGuardContext` + manage `BeginSession` per runtime session; (c) the dashboard deliberately
does **not** use it and instead derives feedback locally. **Recommendation: DEFER** — use the local
derivation (§7). If a later slice wants real guard routing, do it as its own slice with a dedicated
synthetic-adapter + read-only verification, never wired to persistence.

## 9. Services safe to use now (read-only / display-only)
- **Shared DSP** (`PitchDetectionService`, `PitchTraceStabilizer`, `LiveMetricsService`) — already used; read-only.
- **`PitchChartAxisRangeCalculator`** (Core, portable, pure) — for chart Y-range.
- **`EnhancedExercise` / `ExerciseTargetProfile`** (already on the VM) — for the target band + hold target.
- **`ExerciseCoordinatorReadoutDisplay`** (already on the VM) — coordinator hold value for the visual compare.
- **Dashboard patterns** (`PitchSamples` trace, `DeriveFeedback`) — copy the approach into the runtime VM/view.

## 10. Services deferred / not used
- **OxyPlot.Avalonia** — deferred (compat + new-dependency + theme-brush abstraction; §4).
- **`FeedbackConsistencyGuard` / `FeedbackPipeline` / `SmartCoachFeedbackMapper` / `InlineCoachFeedbackMapper` /
  `ProgressionFeedbackMapper`** — deferred (frozen; needs adapter; §8).
- **`ComfortZoneController`** — deferred: ctor + `InitializeAsync(userId)` / `UpdateZoneAsync` /
  `RecordStrainIncidentAsync` are DB-coupled and clinical/adaptive (frozen). The target band from
  `ExerciseTargetProfile` already covers the visual need.
- **`SmartCoachEngine`, progression, mastery, adaptive-difficulty, `VocalHealthSupervisor`, recovery,
  `ProgressionSafetyGate`, `ExerciseSessionRecorder`, `SubjectiveReport`** — not used (persistence/clinical).
- **Real microphone capture / Android** — not used.

## 11. Smoke test design (`--runtime-chart-feedback-smoke`)
Headless (no display): build the runtime VM for exercise #1; run synthetic frames ~700 ms; assert:
`PitchSamples.Count > 0` and capped at `MaxTracePoints`; computed band-overlay/marker VM values are sane
(min < max, marker within the pixel range when in-band); derived feedback string is non-empty and changes
sensibly with band status; the existing derived hold + coordinator readout still populate; Begin clears and
re-fills `PitchSamples` (re-Begin safe); Stop clears the trace; shell nav detail→runtime→back works and
nav-away disposal still holds. Print concise CI lines (sample count, band lo/hi, marker, feedback).

## 12. Leak guard requirements
`FemVoice.Avalonia` keeps referencing only `FemVoice.Core` + `FemVoice.Audio.Abstractions`; **no**
`System.Windows`, `Microsoft.Win32`, `MessageBox`, `OxyPlot.Wpf` (or any OxyPlot), `FemVoice.Audio.Windows`,
NAudio capture, `WaveInEvent`, `WasapiCapture`, `ThemeManager`, `LocExtension`, `LocConverter`. Keep
`Tmds.DBus.Protocol` pinned **0.21.3**. If OxyPlot.Avalonia is ever added it must pass the vuln scan and not
pull WPF; this slice avoids it.

## 13. Build / test gate
`dotnet build FemVoice.Avalonia` (0 warnings) · all smokes incl. the new one OK · `dotnet list --vulnerable`
clean · `FemVoice.Tests.Portable` baseline (1570/1580) · leak guard clean · Windows CI green via PR.

## 14. Risks
- **Pixel mapping**: mapping Hz→pixels for the band/marker within Avalonia layout can be finicky; mitigate by
  computing positions in the VM (testable) and/or shipping the labelled-guide-line fallback (§6).
- **Trace performance**: cap `PitchSamples` (e.g. ~120 points) and remove from the front, exactly as the
  dashboard does, to avoid unbounded growth.
- **Scope creep toward OxyPlot / FeedbackConsistencyGuard** — explicitly deferred; keep the slice converter-free
  and local-derivation only.
- **Threading**: build sample/feedback updates off the capture thread then marshal via `IUiDispatcher` (the
  runtime VM already follows this).
- **Frozen-system temptation**: do not "improve" feedback by routing through the guard/SmartCoach.

## 15. Explicit non-goals
No clinical/domain behaviour change; no scoring/SmartCoach/Voice-Health/recovery/safety-gate/progression/
mastery/adaptive-difficulty; no persistence/reports/subjective-report; no `ComfortZoneController` or
`FeedbackConsistencyGuard` wiring; no OxyPlot; no real mic; no Android; no full WPF chart parity; no change to
exercise definitions or target profiles; no localization-semantics change.

## 16. Recommendation
**Implement chart + feedback together in one slice, using the converter-free native-Avalonia chart** (reuse
the proven dashboard `PitchSamples`/`Rectangle` trace + a computed target-band overlay and current-pitch
marker, scaled via the portable `PitchChartAxisRangeCalculator`) **and a local, display-only feedback readout**
(mirror the dashboard's `DeriveFeedback`, promoting the runtime's existing local status text). They share the
same synthetic stream and are naturally cohesive, both small and low-risk. **Defer OxyPlot.Avalonia** to a
dedicated future chart-parity slice (it carries the real risk: Avalonia 11.2.1 package compat + WPF-brush
theme abstraction) and **defer `FeedbackConsistencyGuard`** (frozen; needs an adapter; unnecessary given the
proven local pattern). Add `--runtime-chart-feedback-smoke`. If implementation reveals band/marker pixel
mapping is unstable, ship the labelled-guide-line fallback for the band and keep the trace + feedback — do not
block the slice on perfect band rendering.
