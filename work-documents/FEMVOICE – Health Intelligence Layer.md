FEMVOICE – HEALTH INTELLIGENCE LAYER
Unified Production Checklist (LOCKED v1.0)

(After UX Refinement & Pedagogical Feedback is complete)

🧱 1) Core Responsibilities — Health Brain

✅ Dedicated component: VocalHealthSupervisor
✅ Subscribes to ExerciseLiveState
✅ Consumes only normalized numeric metrics + safety booleans
✅ Zero UI dependency
✅ No use of PerformanceQuality or MasteryLevel
✅ Publishes HealthSafetyState:

Normal → Caution → Restrict → Lock

✅ Owns internal intelligence policies:

• TrendEngine
• StrainDetectionPolicy
• FatigueDetectionPolicy
• PausePolicy
• RecoveryPolicy
• HydrationAdvisor

⚠ Control principle:

HealthSafetyState is evaluated first
Pause & Hydration are supportive actions — never primary control

📈 2) TrendEngine (foundation of all health intelligence)

✅ Exponential Moving Average per metric
✅ Spike protection
✅ Noise filtering
✅ Separate windows:

• Microtrend (5–15s) → strain
• Mesotrend (30–120s) → fatigue

✅ Baseline-relative evaluation per user

👉 All decisions depend on TrendEngine — no raw-frame logic allowed.

🩺 3) StrainDetectionPolicy (acute load)

✅ Rising micro-variance
✅ Sudden hold collapse
✅ Repeated comfort-zone breaches
✅ Unstable response at same effort

Strain behavior:

• reacts fast
• can trigger Restrict directly
• always prioritized over fatigue

🧠 4) FatigueDetectionPolicy (slow accumulation)

✅ Stability drift downward
✅ Sustained resonance decay
✅ Increasing effort for same output
✅ Reduced recovery effect
✅ Mesotrend-based escalation

⚠ Never tied to UX Quality levels.

⏸ 5) PausePolicy (neuromuscular rest)

✅ Triggered by fatigue
✅ Reinforced by repeated strain
✅ Temporarily freezes progression
✅ Independent from hydration

👉 Pause = Caution-level intervention

💧 6) HydrationAdvisor (physiology support)

✅ Resonance brightness drift
✅ Stability variance increase
✅ Accumulated vocal load model
✅ Baseline-relative analysis
✅ Coupled to fatigue trends
✅ Never time-based
✅ Never escalates state
✅ Calm supportive messaging
✅ Independent test coverage

👉 Hydration assists recovery — never controls session flow.

⛔ 7) Restrict & Lock State Machine

✅ Restrict on sustained strain
✅ Restrict on strain + fatigue combination
✅ Lock only on:

• repeated Restrict cycles
• failed recovery after Pause
• persistent safety breaches

⚠ Low performance quality alone is never health risk.

🌱 8) RecoveryPolicy (mandatory)

✅ Gradual de-escalation
✅ Longer recovery after Restrict
✅ Stable window required after Lock
✅ No instant reset
✅ Trend-confirmed improvement

📣 9) SmartCoach Integration

✅ HealthSafetyState → severity mapping
✅ Supportive tone
✅ No numeric exposure
✅ No blame language
✅ Health overrides standard coaching

Priority chain:

Health → SmartCoach → Coaching

🧪 10) Testing Matrix
TrendEngine

✅ Ignores spikes
✅ Detects drift
✅ Filters noise

Policies

✅ Strain vs fatigue separation
✅ Pause vs hydration separation
✅ Escalation correctness
✅ Recovery flow

Edge cases

✅ Acute overload
✅ Slow fatigue
✅ Pause + improvement
✅ Pause + worsening

📐 11) Architecture Laws

✅ Event-driven
✅ No polling
✅ No timers
✅ UI-agnostic
✅ Trend-based logic only
✅ Policy-driven decisions
✅ UX layer fully isolated

🎯 Mandatory Evaluation Order

Update TrendEngine

Evaluate Strain

Evaluate Fatigue

Evaluate Pause need

Evaluate Hydration advice

Evaluate Restrict/Lock

Emit HealthSafetyState + recommendations

🧠 Golden Rule

RAW SIGNAL → TREND → RISK → POLICY

Never:

UX QUALITY → HEALTH

✅ FINAL STATUS

With this unified Health Intelligence Layer:

🏥 Clinically correct
📈 Real-time stable
🛡 Actively injury-preventive
🧠 Med-tech grade intelligence
🚀 Ready for personalization & AI

Status 2026-05-28:

✅ Implementert `VocalHealthSupervisor`
✅ Implementert `VocalHealthTrendEngine`
✅ Implementert `HealthSafetyState`: Normal → Caution → Restrict → Lock
✅ Registrert i DI
✅ Exercise live-state mates inn i supervisoren under aktive økter
✅ Supervisor journalfører StrainPeriod, PauseRecommended og HydrationSuggested til `SessionAnalyticsStore`
✅ Session summary inkluderer pause- og hydreringsteller
✅ Tester dekker spike-filter, noise-filter, akutt strain, slow fatigue, hydration vs pause, restrict/lock, recovery og events

Status 2026-05-29:

✅ Implementert `VocalHealthFeedbackMapper`
✅ Registrert health-feedback mapper i DI
✅ HealthSafetyState/strain/fatigue/pause/hydration mappes til `FeedbackCandidate`
✅ Health-feedback går gjennom `FeedbackPipeline` og `FeedbackConsistencyGuard`
✅ Godkjente health-feedbackmeldinger vises via lokaliserte ressursnøkler
✅ Lagt til `VoiceHealthFeedback_*` keys i alle språkfiler
✅ Norsk og engelsk har egne tekster; øvrige språk bruker engelsk fallback for å unngå rå keys
✅ Health-feedback vises bare ved state-endring eller throttlet health-event, slik at DB/UI ikke spammes
✅ Implementert `HydrationAdvisor` som egen fysiologisk støtte-modul
✅ HydrationAdvisor bruker resonansdrift, stabilitetsvarians og akkumulert vokal belastning
✅ HydrationAdvisor er sample-/trendbasert, ikke time-basert, og eskalerer aldri helsetilstand
✅ HydrationAdvisor journalfører eksplisitte HydrationSuggested-events og sender feedback via `FeedbackPipeline`
✅ Eldre `VoiceHealthMonitor`-events forenes via `VocalHealthLegacyBridge`
✅ `ExerciseIntelligenceCoordinator` bruker nå `VocalHealthDecision`-mapping for legacy warning/critical/lockout
✅ Tester dekker legacy warning, critical og lockout mapping
✅ Implementert `VocalHealthBaselineProvider`
✅ `VocalHealthSupervisor` og `HydrationAdvisor` bruker persistent per-user baseline fra SmartCoachBaseline/recent sessions
✅ Tester dekker persistent baseline, fallback til nylige økter og option-generering

Gjenstår i denne modulen:

✅ Ingen åpne punkter i dette dokumentet per 2026-05-29
