using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Agent 7 (Manual Override — SAFETY-CRITICAL) — proves the two-stage clamp NEVER lets
    /// a professional override be less conservative than the safety/recovery gate floor.
    ///
    /// House style: no mocking frameworks — real classes only. All numeric checks are hand
    /// computed against the documented constants (resonance/stability −0.08, hold −2s).
    /// </summary>
    public class ManualOverrideClampTests
    {
        // A representative baseline (the factory reference floor). Resonance window
        // [0.55, 0.90], stability 0.50, hold 3s, no pitch bounds.
        private static ExerciseTargetProfile Baseline() => new()
        {
            UsesResonance = true,
            UsesStability = true,
            TargetResonanceMin = 0.55,
            TargetResonanceMax = 0.90,
            StabilityThreshold = 0.50,
            RequiredHoldSeconds = 3.0
        };

        // ExerciseTargetProfile is a plain init-only class (NOT a record), so `with` is
        // unavailable — this copy-with-overrides helper stands in for it in the tests.
        private static ExerciseTargetProfile Derive(
            ExerciseTargetProfile b,
            double? targetResonanceMin = null,
            double? targetResonanceMax = null,
            double? stabilityThreshold = null,
            double? requiredHoldSeconds = null,
            double? minPitch = null,
            double? maxPitch = null) => new()
        {
            UsesResonance = b.UsesResonance,
            UsesPitch = b.UsesPitch,
            UsesStability = b.UsesStability,
            UsesIntensity = b.UsesIntensity,
            ClinicalPurposeKey = b.ClinicalPurposeKey,
            PhysicalFocusKey = b.PhysicalFocusKey,
            CommonMistakesKey = b.CommonMistakesKey,
            SafetyInfoKey = b.SafetyInfoKey,
            FeedbackModeKey = b.FeedbackModeKey,
            ThresholdStrategyKey = b.ThresholdStrategyKey,
            IndicatorPackageSummaryKey = b.IndicatorPackageSummaryKey,
            MinPitch = minPitch ?? b.MinPitch,
            MaxPitch = maxPitch ?? b.MaxPitch,
            TargetResonanceMin = targetResonanceMin ?? b.TargetResonanceMin,
            TargetResonanceMax = targetResonanceMax ?? b.TargetResonanceMax,
            StabilityThreshold = stabilityThreshold ?? b.StabilityThreshold,
            RequiredHoldSeconds = requiredHoldSeconds ?? b.RequiredHoldSeconds
        };

        // ── 1. Stage 1: an aggressive (too-low) intent is floored at baseline − step ──
        [Fact]
        public void ClampProfile_AggressiveLowIntent_FlooredAtRecoveryFloor()
        {
            var baseline = Baseline();
            // Professional tries to drop everything to the bottom.
            var intended = Derive(baseline,
                targetResonanceMin: 0.0, stabilityThreshold: 0.0, requiredHoldSeconds: 0.0);

            var clamped = ManualOverrideClamp.ClampProfile(intended, baseline);

            // Floor = max(0, baseline − 0.08) for resonance/stability; baseline − 2 for hold.
            Assert.Equal(0.55 - 0.08, clamped.TargetResonanceMin, 9); // 0.47
            Assert.Equal(0.50 - 0.08, clamped.StabilityThreshold, 9); // 0.42
            Assert.Equal(3.0 - 2.0, clamped.RequiredHoldSeconds, 9);  // 1.0
        }

        // ── 2. Stage 1: a MORE conservative intent is kept (override may tighten) ─────
        [Fact]
        public void ClampProfile_MoreConservativeIntent_IsKept()
        {
            var baseline = Baseline();
            // Above the floor in the conservative direction — should pass through untouched.
            var intended = Derive(baseline,
                targetResonanceMin: 0.70, stabilityThreshold: 0.65, requiredHoldSeconds: 5.0);

            var clamped = ManualOverrideClamp.ClampProfile(intended, baseline);

            Assert.Equal(0.70, clamped.TargetResonanceMin, 9);
            Assert.Equal(0.65, clamped.StabilityThreshold, 9);
            Assert.Equal(5.0, clamped.RequiredHoldSeconds, 9);
        }

        // ── 3. Stage 1: floor never goes negative (max(0, ...)) ──────────────────────
        [Fact]
        public void ClampProfile_LowBaseline_FloorClampedAtZero()
        {
            var baseline = Derive(Baseline(),
                targetResonanceMin: 0.05,  // baseline − 0.08 would be negative
                stabilityThreshold: 0.04,
                requiredHoldSeconds: 1.0);  // baseline − 2 would be negative
            var intended = Derive(baseline,
                targetResonanceMin: 0.0, stabilityThreshold: 0.0, requiredHoldSeconds: 0.0);

            var clamped = ManualOverrideClamp.ClampProfile(intended, baseline);

            Assert.Equal(0.0, clamped.TargetResonanceMin, 9);
            Assert.Equal(0.0, clamped.StabilityThreshold, 9);
            // Hold floor = baseline − 2 = −1, but intended is 0 ⇒ max(0, −1) = 0.
            Assert.Equal(0.0, clamped.RequiredHoldSeconds, 9);
        }

        // ── 4. Stage 2: when the gate is BLOCKED, intended can NEVER raise a target ───
        [Fact]
        public void ClampAgainstGate_GateBlocked_ForcesMinAgainstBaseline()
        {
            var baseline = Baseline();
            // A professional tries to RAISE every requirement above baseline.
            var intended = Derive(baseline,
                targetResonanceMin: 0.80,   // higher than baseline 0.55
                targetResonanceMax: 0.99,   // wider than baseline 0.90
                stabilityThreshold: 0.90,   // higher than baseline 0.50
                requiredHoldSeconds: 9.0);  // longer than baseline 3.0

            var clamped = ManualOverrideClamp.ClampAgainstGate(
                intended, baseline, gateBlocked: true, recoverySeverity: RecoverySeverity.None);

            // Every requirement pulled to Math.Min(intended, baseline) ⇒ baseline values.
            Assert.Equal(baseline.TargetResonanceMin, clamped.TargetResonanceMin, 9);
            Assert.Equal(baseline.StabilityThreshold, clamped.StabilityThreshold, 9);
            Assert.Equal(baseline.RequiredHoldSeconds, clamped.RequiredHoldSeconds, 9);
            // Resonance window ceiling shrunk toward baseline too (never wider).
            Assert.Equal(baseline.TargetResonanceMax, clamped.TargetResonanceMax, 9);

            // INVARIANT: no clamped requirement exceeds baseline.
            Assert.True(clamped.TargetResonanceMin <= baseline.TargetResonanceMin);
            Assert.True(clamped.StabilityThreshold <= baseline.StabilityThreshold);
            Assert.True(clamped.RequiredHoldSeconds <= baseline.RequiredHoldSeconds);
            Assert.True(clamped.TargetResonanceMax <= baseline.TargetResonanceMax);
        }

        // ── 5. Stage 2: severity ≥ Recommend triggers the same Math.Min clamp ────────
        [Fact]
        public void ClampAgainstGate_RecoverySeverityRecommend_ForcesMin()
        {
            var baseline = Baseline();
            var intended = Derive(baseline, requiredHoldSeconds: 9.0, stabilityThreshold: 0.95);

            var clamped = ManualOverrideClamp.ClampAgainstGate(
                intended, baseline, gateBlocked: false, recoverySeverity: RecoverySeverity.Recommend);

            Assert.Equal(baseline.RequiredHoldSeconds, clamped.RequiredHoldSeconds, 9);
            Assert.Equal(baseline.StabilityThreshold, clamped.StabilityThreshold, 9);
        }

        // ── 6. Stage 2: below Recommend AND not blocked ⇒ intended returned unchanged ─
        [Fact]
        public void ClampAgainstGate_NotBlockedAndBelowRecommend_ReturnsIntended()
        {
            var baseline = Baseline();
            var intended = Derive(baseline, requiredHoldSeconds: 9.0, stabilityThreshold: 0.95);

            var watch = ManualOverrideClamp.ClampAgainstGate(
                intended, baseline, gateBlocked: false, recoverySeverity: RecoverySeverity.Watch);
            var none = ManualOverrideClamp.ClampAgainstGate(
                intended, baseline, gateBlocked: false, recoverySeverity: RecoverySeverity.None);

            // Watch (=1) is below Recommend (=2) ⇒ no Stage-2 clamp.
            Assert.Equal(9.0, watch.RequiredHoldSeconds, 9);
            Assert.Equal(0.95, watch.StabilityThreshold, 9);
            Assert.Equal(9.0, none.RequiredHoldSeconds, 9);
        }

        // ── 7. Stage 2: blocked gate shrinks the pitch comfort-zone toward baseline ──
        [Fact]
        public void ClampAgainstGate_GateBlocked_ShrinksPitchComfortZone()
        {
            var baseline = new ExerciseTargetProfile
            {
                UsesPitch = true,
                UsesStability = true,
                MinPitch = 165.0,
                MaxPitch = 255.0,
                TargetResonanceMin = 0.30,
                TargetResonanceMax = 1.00,
                StabilityThreshold = 0.45,
                RequiredHoldSeconds = 3.0
            };
            // Professional tries to WIDEN the comfort zone (lower min, higher max).
            var intended = Derive(baseline, minPitch: 120.0, maxPitch: 320.0);

            var clamped = ManualOverrideClamp.ClampAgainstGate(
                intended, baseline, gateBlocked: true, recoverySeverity: RecoverySeverity.None);

            // Min raised toward baseline, max lowered toward baseline ⇒ baseline window.
            Assert.Equal(165.0, clamped.MinPitch!.Value, 9);
            Assert.Equal(255.0, clamped.MaxPitch!.Value, 9);
            // INVARIANT: the clamped window is never wider than baseline.
            Assert.True(clamped.MinPitch.Value >= baseline.MinPitch.Value);
            Assert.True(clamped.MaxPitch.Value <= baseline.MaxPitch.Value);
        }

        // ── 8. Full pipeline via engine: aggressive RAISE under blocked gate ⇒ baseline
        [Fact]
        public void Engine_AggressiveRaise_UnderBlockedGate_NeverExceedsBaseline()
        {
            var baseline = Baseline();
            var intended = Derive(baseline,
                targetResonanceMin: 0.85, targetResonanceMax: 0.99,
                stabilityThreshold: 0.95, requiredHoldSeconds: 12.0);
            var engine = new ManualOverrideEngine();
            var request = new ManualOverrideRequest
            {
                OverrideKind = ManualOverrideKind.ExerciseReco,
                UserId = 1,
                ExerciseId = 3,
                IntendedProfile = intended,
                ReasonCode = "TEST",
                ActorRole = "Clinician",
                RequestedAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc)
            };

            var result = engine.Evaluate(
                request, baseline, gateBlocked: true,
                recoverySeverity: RecoverySeverity.Urgent, style: VoiceStyleGoal.Feminine);

            Assert.True(result.WasApplied);
            Assert.True(result.WasClamped);
            var applied = result.AppliedProfile!;
            // PROOF: no requirement was raised above baseline despite an aggressive intent.
            Assert.True(applied.TargetResonanceMin <= baseline.TargetResonanceMin);
            Assert.True(applied.StabilityThreshold <= baseline.StabilityThreshold);
            Assert.True(applied.RequiredHoldSeconds <= baseline.RequiredHoldSeconds);
            Assert.True(applied.TargetResonanceMax <= baseline.TargetResonanceMax);
        }

        // ── 9. Engine: aggressive LOWER (clear gate) ⇒ floored, never below floor ─────
        [Fact]
        public void Engine_AggressiveLower_ClearGate_FlooredAtRecoveryFloor()
        {
            var baseline = Baseline();
            var intended = Derive(baseline,
                targetResonanceMin: 0.0, stabilityThreshold: 0.0, requiredHoldSeconds: 0.0);
            var engine = new ManualOverrideEngine();
            var request = new ManualOverrideRequest
            {
                OverrideKind = ManualOverrideKind.ExerciseReco,
                UserId = 1,
                ExerciseId = 3,
                IntendedProfile = intended,
                RequestedAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc)
            };

            var result = engine.Evaluate(
                request, baseline, gateBlocked: false,
                recoverySeverity: RecoverySeverity.None, style: VoiceStyleGoal.Feminine);

            var applied = result.AppliedProfile!;
            // Stage-1 recovery floor: never below baseline − step.
            Assert.Equal(0.55 - 0.08, applied.TargetResonanceMin, 9);
            Assert.Equal(0.50 - 0.08, applied.StabilityThreshold, 9);
            Assert.Equal(3.0 - 2.0, applied.RequiredHoldSeconds, 9);
            Assert.True(result.WasClamped);
        }

        // ── 10. Engine: style multiplier scales resonance down for DarkFeminine ──────
        [Fact]
        public void Engine_DarkFeminineStyle_ScalesResonanceDown()
        {
            var baseline = new ExerciseTargetProfile
            {
                UsesResonance = true,
                UsesStability = true,
                TargetResonanceMin = 0.20,
                TargetResonanceMax = 1.00,
                StabilityThreshold = 0.40,
                RequiredHoldSeconds = 3.0
            };
            // Intent sits above the floor so Stage 1 keeps it; clear gate so Stage 2 is a
            // no-op — isolating the style multiplier effect on resonance.
            var intended = Derive(baseline, targetResonanceMin: 0.60, targetResonanceMax: 0.80);
            var engine = new ManualOverrideEngine();
            var request = new ManualOverrideRequest
            {
                OverrideKind = ManualOverrideKind.ExerciseReco,
                UserId = 1,
                ExerciseId = 1,
                IntendedProfile = intended,
                RequestedAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc)
            };

            var result = engine.Evaluate(
                request, baseline, gateBlocked: false,
                recoverySeverity: RecoverySeverity.None, style: VoiceStyleGoal.DarkFeminine);

            var applied = result.AppliedProfile!;
            // DarkFeminine multiplier 0.5: intended min 0.60 → 0.30, max 0.80 → 0.40.
            // Stage-1 floor = max(0, 0.20 − 0.08) = 0.12, so 0.30 is kept.
            Assert.Equal(0.30, applied.TargetResonanceMin, 9);
            Assert.Equal(0.40, applied.TargetResonanceMax, 9);
        }

        // ── 11. Engine: non-exercise override (no profile) is reported as not applied ─
        [Fact]
        public void Engine_NoIntendedProfile_ReturnsBlockedReason()
        {
            var baseline = Baseline();
            var engine = new ManualOverrideEngine();
            var request = new ManualOverrideRequest
            {
                OverrideKind = ManualOverrideKind.VoiceGoals,
                UserId = 1,
                IntendedProfile = null,
                RequestedAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc)
            };

            var result = engine.Evaluate(
                request, baseline, gateBlocked: false,
                recoverySeverity: RecoverySeverity.None, style: VoiceStyleGoal.Feminine);

            Assert.False(result.WasApplied);
            Assert.False(result.WasClamped);
            Assert.Equal("NO_INTENDED_PROFILE", result.BlockedReasonCode);
            Assert.Null(result.AppliedProfile);
        }

        // ── 12. Engine: applied profile is always internally valid ───────────────────
        [Fact]
        public void Engine_AppliedProfile_AlwaysValidates()
        {
            var baseline = Baseline();
            var intended = Derive(baseline,
                targetResonanceMin: 0.95, targetResonanceMax: 0.96,
                stabilityThreshold: 0.99, requiredHoldSeconds: 8.0);
            var engine = new ManualOverrideEngine();
            var request = new ManualOverrideRequest
            {
                OverrideKind = ManualOverrideKind.ExerciseReco,
                UserId = 1,
                ExerciseId = 2,
                IntendedProfile = intended,
                RequestedAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc)
            };

            var result = engine.Evaluate(
                request, baseline, gateBlocked: true,
                recoverySeverity: RecoverySeverity.Recommend, style: VoiceStyleGoal.Feminine);

            // Validate() did not throw inside Evaluate; the applied profile is consistent.
            var applied = result.AppliedProfile!;
            Assert.True(applied.TargetResonanceMax >= applied.TargetResonanceMin);
            applied.Validate(); // explicit re-assert
        }
    }
}
