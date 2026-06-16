# Avalonia Runtime Chart + Live Feedback — Slice Report

Date: 2026-06-16 · Branch: `avalonia-runtime-chart-feedback-slice` (off `main` @ `a8df6ec`, incl. PR #1–#7).

> **Status: IMPLEMENTED (Linux-verified, headless).** Adds a **converter-free, display-only** pitch chart +
> live feedback layer to the Avalonia Exercise Runtime screen. No OxyPlot, no `FeedbackConsistencyGuard`, no
> `ComfortZoneController`, no persistence, no clinical/domain change. See `_GATE_RESULTS.md` / `_PLACEHOLDERS.md`.

## 1. What this slice does
Promotes the runtime from text-only feedback to **visual** feedback: a recent pitch-trace chart with a target
band and a current-pitch marker, a local live-feedback readout, and visual hold bars (derived vs coordinator)
with a comparison line. Everything is display-only and driven by the existing synthetic runtime stream.

## 2. Chart implementation approach (converter-free, no OxyPlot)
- **Fixed pixel coordinate space.** A constant `ChartHeightPx = 200`; all chart values are "px from the chart
  bottom". A fixed per-session axis range is computed once at Begin from the target band via the **portable,
  pure** `PitchChartAxisRangeCalculator.Calculate([], TargetPitchMin, TargetPitchMax)` (no WPF). Keeping the
  range fixed means the target band stays put while the trace scrolls under it.
- **Trace:** `RuntimePitchSamples` (`ObservableCollection<double>` of px heights) appended on the UI thread
  (capped at `MaxTracePoints = 120`, front-removed), rendered exactly like the dashboard placeholder — an
  `ItemsControl` of bottom-aligned `Rectangle`s with `Height={Binding}` (the bound item *is* the px height).
- **Target band + current-pitch marker:** an Avalonia `Canvas` with the band and marker positioned by the
  `Canvas.Bottom` / `Height` attached properties bound directly to VM px doubles (`TargetBandBottomPx`,
  `TargetBandHeightPx`, `CurrentPitchMarkerPx`). Widths bind to `{Binding #ChartCanvas.Bounds.Width}`
  (element-name binding). **No value converters** anywhere — matching the dashboard's deliberate convention.
- Scalar chart state is an immutable `RuntimeChartDisplay` snapshot rebuilt each frame; the trace collection
  lives on the VM so it can be appended incrementally without per-frame rebuilds.

## 3. Why OxyPlot was deferred
`OxyPlot.Avalonia` is **not referenced** in the repo (only `OxyPlot.Wpf` in the WPF app) and its compatibility
with Avalonia 11.2.1 is unvalidated; `AnalysisChartTheme` also reads WPF brushes that must be abstracted first
(see `AVALONIA_CHART_PORT_NOTES.md`). Adding it would mean a new dependency + version risk + a fresh
vulnerability surface. A converter-free native chart meets this slice's display-only goal with zero new deps;
OxyPlot parity is left to a dedicated future chart-parity slice. **The leak guard forbids any OxyPlot.**

## 4. Why FeedbackConsistencyGuard was deferred
`FeedbackConsistencyGuard` is pure in-memory (no DB) but is one of the **behaviour-frozen** systems and needs
a non-trivial adapter (`FeedbackCandidate` + `FeedbackGuardContext` + `BeginSession` lifecycle). The dashboard
deliberately derives feedback locally instead. This slice mirrors that: a local `DeriveLiveFeedback` returns a
display-only message + severity from safe conditions. `ComfortZoneController` (DB-coupled/clinical) is likewise
not used — the target band comes from `ExerciseTargetProfile`. **The leak guard forbids both.**

## 5. Data model used
- **New** `RuntimeChartDisplay` (immutable): `ChartHeightPx`, `ChartMinPitch`, `ChartMaxPitch`,
  `TargetPitchMin/Max`, `TargetBandBottomPx/TopPx/HeightPx`, `CurrentPitch`, `CurrentPitchMarkerPx`, `HasVoice`,
  `ChartStatusText`, plus the static `ToPx(hz,min,max,heightPx)` mapper and `Empty(...)`/`From(...)` builders.
- **VM additions:** `RuntimePitchSamples` (`ObservableCollection<double>` px heights), `RuntimeChart`
  (`RuntimeChartDisplay`), `LiveFeedbackMessage`, `LiveFeedbackSeverity`, and computed `DerivedHoldVisualPercent`
  / `CoordinatorHoldVisualPercent` / `HoldComparisonText` (with change-forwarding).

## 6. Target-band rendering approach
Implemented as the **preferred** band overlay (a translucent rectangle at the band's px position) + a labelled
Y-axis (max/min Hz) and a legend line ("gul linje = nåværende pitch · grønt felt = målområde"). The
labelled-guide-line fallback from the plan was not needed — the Canvas/`Canvas.Bottom` approach is stable.

## 7. Feedback rules (local, display-only)
`DeriveLiveFeedback(voiced, pitch, liveState)`:
- coordinator safety-lock (display-only) → "Koordinator varsler lås — kun visning, ikke håndhevet" / "Lås (visning)"
- no voice / pitch ≤ 0 → "Ingen stabil stemme registrert" / "Ingen stemme"
- below band → "Litt under målområdet" / "Juster"
- above band → "Litt over målområdet" / "Juster"
- in band → "Innenfor målområdet" / "I mål"
Severity is a short **text** label (no colour-converter; no clinical meaning). No `FeedbackConsistencyGuard`,
no SmartCoach, no gate. The coordinator-lock branch is display-only and (with the safe health placeholder) does
not trigger in practice; it is included only so the readout would surface it as text if ever set.

## 8. Coordinator / derived hold visualization
Two `ProgressBar`s: derived hold (`DerivedHoldVisualPercent` = the existing pitch-band `HoldProgressPercent`)
and coordinator hold (`CoordinatorHoldVisualPercent` from the display-only `CoordinatorReadout`), plus a
`HoldComparisonText`. As documented in the coordinator slice, the coordinator hold reads 0 % for resonance
exercises under the neutral resonance placeholder, while the derived hold accumulates — both are shown.

## 9. Display-only limitations
Lightweight Avalonia scaffold, not WPF/OxyPlot parity: no true time axis/ticks, no zoom/pan, no styling parity.
Synthetic audio only (no real mic). Feedback is local/non-clinical. No persistence, SmartCoach, progression,
gate, Voice-Health, or recovery. Frozen systems untouched.

## 10. Files changed
- **New** `FemVoice.Avalonia/ViewModels/RuntimeChartDisplay.cs`.
- **Edit** `FemVoice.Avalonia/ViewModels/ExerciseRuntimeViewModel.cs` — chart range (fixed, via
  `PitchChartAxisRangeCalculator`), `RuntimePitchSamples`, `RuntimeChart`, `LiveFeedbackMessage/Severity`,
  computed hold visuals + change-forwarding, per-frame chart/feedback update, Begin reset; Stop also clears
  the trace + resets the chart/feedback (no frozen live-looking marker after Stop — review polish).
- **Edit** `FemVoice.Avalonia/Views/ExerciseRuntimeView.axaml` — Canvas chart (band + trace + marker),
  live-feedback panel, derived+coordinator hold bars + comparison.
- **Edit** `FemVoice.Avalonia/Program.cs` — `--runtime-chart-feedback-smoke`.
- **Docs** this report + `_GATE_RESULTS.md` + `_PLACEHOLDERS.md` + tracker + plan-doc status.

## 11. Verification (see `_GATE_RESULTS.md`)
Build 0 warnings · all 7 smokes OK (incl. `--runtime-chart-feedback-smoke`) · no vulnerable packages · leak
guard clean (zero real references; only negation comments) · portable 1570/1580 · Windows CI = pending PR.

## 12. Behaviour changes
**None to clinical/domain behaviour.** WPF untouched. No scoring/SmartCoach/Voice-Health/recovery/gate/
progression/persistence/report/localization-semantics/exercise-definition/target-profile change.
