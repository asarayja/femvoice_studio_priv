# Avalonia Runtime Chart + Live Feedback — Placeholders & Deferred Wiring

Date: 2026-06-16. Real vs. placeholder/deferred in this slice.

## Real (read-only, no behaviour change)
- **Pitch trace**: real stabilized pitch from the shared DSP (`PitchDetectionService` → `LiveMetricsService`
  → `PitchTraceStabilizer`), mapped to px and shown as a converter-free `ItemsControl`/`Rectangle` trace.
- **Axis range**: real, from the pure portable `PitchChartAxisRangeCalculator` over the exercise target band
  (fixed per session).
- **Target band + current-pitch marker**: real px positions from the exercise's `TargetPitchMin/Max` and the
  measured pitch, in the same px space as the trace.
- **Hold visuals**: derived hold = the existing pitch-band `HoldProgressPercent`; coordinator hold = the
  existing display-only `CoordinatorReadout` value.

## Placeholder / deferred (by design)
| Item | Status | Notes |
| --- | --- | --- |
| Charting engine | **Converter-free native Avalonia** (Canvas + ItemsControl + Rectangle) | No OxyPlot. `OxyPlot.Avalonia` deferred (Avalonia 11.2.1 compat unvalidated + WPF-brush theme abstraction). Leak guard forbids any OxyPlot. |
| Chart fidelity | **Lightweight scaffold** | No true time axis/ticks, no zoom/pan bounds, no WPF chart styling parity. Y-axis = max/min Hz labels only. |
| Axis behaviour | **Fixed per session** | Range computed once from the target band so the band stays put and the trace scrolls; not a dynamic auto-ranging axis. |
| Live feedback | **Local display-only `DeriveLiveFeedback`** | Mirrors the dashboard's local pattern; NOT `FeedbackConsistencyGuard`/`FeedbackPipeline`/SmartCoach (deferred; frozen). |
| Feedback severity | **Text label only** | No colour value-converter (the slice is converter-free); severity is descriptive text with no clinical meaning. |
| Coordinator safety-lock feedback branch | **Display-only** | Surfaces the coordinator's `IsSafetyLocked` as text if ever set; with the safe health placeholder it does not trigger. Never enforced. |
| `ComfortZoneController` | **Not wired** | DB-coupled/clinical; the target band comes from `ExerciseTargetProfile`. |
| Audio | **Dedicated synthetic capture** | No real mic; Windows would inject the real `IAudioCaptureService` (deferred). |
| Persistence / SmartCoach / progression / mastery / adaptive-difficulty | **Not wired** | Out of scope. |
| Voice Health / Recovery / ProgressionSafetyGate decisions | **Not wired/enforced** | Frozen clinical gates not invoked or faked. |
| Coordinator hold value | **Reads 0 % for resonance exercises** | Resonance fed as a neutral placeholder vs the 0.50–0.85 target (documented in the coordinator slice); both holds shown. |

## Safety note
No clinical scoring, SmartCoach, Voice-Health gate, recovery, safety priority, progression, reports,
persistence, localization semantics, exercise definitions, or target profiles were changed. The chart renders
the synthetic pitch stream and the target band; the feedback is a local descriptive derivation. It makes no
clinical decision and enforces no gate or safety-lock. The Safety > Health > Recovery > Comfort >
Voice-Development > Reporting hierarchy is unaffected.
