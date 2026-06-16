# Avalonia Dashboard — Feedback Wiring

Date: 2026-06-16.

## What is wired (this slice)
`MainDashboardViewModel.CurrentFeedbackMessage` is a **simple, safe descriptive derivation** of the live state produced by shared services:
- not voiced → "Ingen stemme oppdaget — prøv å snakke jevnt."
- health Warning/Danger → "Ta en pause og slapp av i stemmen."
- pitch below comfort zone → "Litt under komfortsonen — løft tonen forsiktig."
- pitch above comfort zone → "Litt over komfortsonen — slipp tonen litt ned."
- otherwise → stable: "Fin, stabil tone i komfortsonen." / developing: "Hold tonen jevn i komfortsonen."

Inputs are read-only outputs of `PitchDetectionService` / `LiveMetricsService` / `PitchTargetZonePolicy`. **This is a UI display string, not a change to any feedback contract.**

## What is NOT wired (deferred — by design)
The real **`FeedbackPipeline` + `FeedbackConsistencyGuard`** path (and its mappers: SmartCoach / InlineCoach / Progression / Hydration / VocalHealth) is **not** routed in this slice. That pipeline enforces the canonical priority + suppression + rate-limiting (`Safety > Health > Recovery > Comfort > Voice Development > Reporting`) over multiple message sources, and needs:
- a live `ExerciseLiveState` stream (exercise/session context),
- `VocalHealthSupervisor` / `RecoveryIntelligenceService` decisions,
- `SmartCoachEngine` / `HydrationAdvisor` message sources,
- the guard's `FeedbackGuardContext` (per-channel rate state).

Wiring this safely belongs to the exercise/session slices, where the `ExerciseLiveState` source exists. **The frozen `FeedbackConsistencyGuard` behaviour is untouched** — this slice simply does not invoke it yet.

## Recommended next step (feedback slice)
Introduce an Avalonia-safe adapter that feeds dashboard/exercise live-state into `FeedbackPipeline.Submit(...)` and renders the **approved** `FeedbackDecision` (respecting suppression/priority) instead of the placeholder derivation — with tests asserting the priority/suppression invariants still hold (the existing `FeedbackPriorityMatrixTests`/`FeedbackConsistencyGuardTests` already cover the engine and run green in `FemVoice.Tests.Portable`).
