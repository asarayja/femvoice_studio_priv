# Avalonia Exercise Runtime Integration — Placeholders & Deferred Wiring

Date: 2026-06-16. Real vs. placeholder/deferred in this slice.

## Real (read-only, no behaviour change)
- **Exercise → profile mapping**: `ExerciseProfileMap` (Avalonia-only) maps catalog Id 1–15 → `ExerciseProfileType`, copied verbatim from the `ExerciseDataService` SQLite seed. All 15 mapped (0 fallback).
- **Target profile metadata**: `ExerciseProfileFactory.CreateProfile(type)` → `ExerciseTargetProfile` (pure). Surfaced read-only: target pitch, target resonance range, `RequiredHoldSeconds`, `StabilityThreshold`, which signals the profile uses, and localized purpose/focus/safety/common-mistakes text.
- **Hold target**: the runtime now uses the profile's `RequiredHoldSeconds` as the **display-only** hold target (falls back to 5 s if no profile/zero).

## Placeholder / deferred (by design)
| Item | Status | Notes |
| --- | --- | --- |
| Exercise→ProfileType mapping location | Avalonia-only mapper (`ExerciseProfileMap`) | Deliberately not in `FemVoice.Core`, and the shared catalog/`ExerciseDataService` are not modified or read at runtime (no DB dependency). Source of truth cited in the file. |
| Hold / progress | **Display-only** | Uses `RequiredHoldSeconds` as the *target*, but the accumulation is the in-VM derived hold — NOT `ExerciseIntelligenceCoordinator`'s clinical hold/safety state (still deferred). |
| Localization of profile text | `LocalizationService.Instance[key]` with documented fallback | If a key is null/missing, a readable Norwegian fallback is shown. No RESX/semantics change. |
| Resonance target display | Shows the profile's `TargetResonanceMin/Max` (a normalized 0–1 score for some profiles, e.g. ResonanceHumming 0–1) | Display of the profile's own values; not converted to Hz. Documented. |
| `ExerciseIntelligenceCoordinator` | Not wired | Real hold/`ExerciseLiveState`/`IsSafetyLocked` deferred (verify read-only first). |
| Audio | Dedicated synthetic capture | No real mic; Windows would inject real `IAudioCaptureService` (deferred). |
| Session persistence / SmartCoach / progression / mastery / adaptive-difficulty | Not wired | Out of scope. |
| Voice Health / Recovery / ProgressionSafetyGate decisions | Not wired/enforced | Frozen clinical gates not invoked or faked. |
| `FeedbackConsistencyGuard` / FeedbackPipeline | Not wired | Runtime status text is a simple non-clinical derivation. |
| Theme/localization parity, compiled bindings | Skeleton / reflection bindings | Later slices. |

## Safety note
No clinical scoring, SmartCoach, Voice Health gate, recovery, safety priority, progression, reports, persistence, localization semantics, exercise definitions, or target profiles were changed. The integration reads `ExerciseTargetProfile` via the pure `ExerciseProfileFactory` and renders it; it makes no clinical decision and enforces no gate.
