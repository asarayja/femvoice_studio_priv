using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// SAFETY-CRITICAL two-stage clamp for manual professional overrides (Sprint E,
    /// Agent 7). PURE and static so its invariants are trivially unit-testable and so the
    /// formula can be reasoned about in isolation.
    ///
    /// ── THE INVARIANT ────────────────────────────────────────────────────────────────
    /// An override may NEVER be LESS conservative than the safety/recovery gate floor.
    /// Two stages enforce this:
    ///
    ///   STAGE 1 (recovery floor): re-implements the SAME floor as
    ///   <c>ExerciseWindow.ClampToRecoveryFloor</c> — a requirement may be lowered at most
    ///   one recovery step below the factory baseline (resonance-min / stability −0.08,
    ///   hold −2s). The constants are kept IDENTICAL to that gate (−0.08 / −2) by design.
    ///   This means even a permissive professional intent cannot drop a target below the
    ///   clinical floor.
    ///
    ///   STAGE 2 (gate clamp): when the ProgressionSafetyGate has BLOCKED, OR the recovery
    ///   forecast severity is at/above <see cref="RecoverySeverity.Recommend"/>, every
    ///   requirement is additionally pulled to <c>Math.Min(intended, baseline)</c>. Under a
    ///   blocked gate an override can therefore only HOLD or LOWER a target — it can never
    ///   raise one. (Resonance-MAX and the pitch comfort-zone are shrunk TOWARD baseline so
    ///   the acceptable window never widens beyond what the unblocked baseline allows.)
    ///
    /// Stage 1 always runs; Stage 2 runs on top of it when blocked/severe. The result is
    /// then <see cref="ExerciseTargetProfile.Validate"/>-d before it can be persisted.
    /// This class writes NOTHING and reads NOTHING — it only computes a more-conservative
    /// profile. The hard gates remain the only authority that can stop training.
    /// </summary>
    public static class ManualOverrideClamp
    {
        // Kept IDENTICAL to ExerciseWindow.ClampToRecoveryFloor — one recovery step.
        private const double ResonanceStabilityFloorStep = 0.08;
        private const double HoldFloorStepSeconds = 2.0;

        /// <summary>
        /// STAGE 1 — recovery floor. Returns a profile whose lowerable requirements are
        /// floored at one recovery step below <paramref name="baseline"/>: resonance-min
        /// and stability at <c>max(0, baseline − 0.08)</c>, hold at <c>baseline − 2s</c>.
        /// A professional intent that is already more conservative than the floor is kept;
        /// an intent more permissive than the floor is pulled UP to the floor. Mirrors the
        /// recovery-floor clamp in <c>ExerciseWindow</c> (constants intentionally identical).
        /// </summary>
        public static ExerciseTargetProfile ClampProfile(
            ExerciseTargetProfile intended,
            ExerciseTargetProfile baseline)
        {
            ArgumentNullException.ThrowIfNull(intended);
            ArgumentNullException.ThrowIfNull(baseline);

            return new ExerciseTargetProfile
            {
                UsesResonance = intended.UsesResonance,
                UsesPitch = intended.UsesPitch,
                UsesStability = intended.UsesStability,
                UsesIntensity = intended.UsesIntensity,
                ClinicalPurposeKey = intended.ClinicalPurposeKey,
                PhysicalFocusKey = intended.PhysicalFocusKey,
                CommonMistakesKey = intended.CommonMistakesKey,
                SafetyInfoKey = intended.SafetyInfoKey,
                FeedbackModeKey = intended.FeedbackModeKey,
                ThresholdStrategyKey = intended.ThresholdStrategyKey,
                IndicatorPackageSummaryKey = intended.IndicatorPackageSummaryKey,
                MinPitch = intended.MinPitch,
                MaxPitch = intended.MaxPitch,
                TargetResonanceMax = intended.TargetResonanceMax,
                TargetResonanceMin = Math.Max(intended.TargetResonanceMin,
                    Math.Max(0, baseline.TargetResonanceMin - ResonanceStabilityFloorStep)),
                StabilityThreshold = Math.Max(intended.StabilityThreshold,
                    Math.Max(0, baseline.StabilityThreshold - ResonanceStabilityFloorStep)),
                RequiredHoldSeconds = Math.Max(intended.RequiredHoldSeconds,
                    baseline.RequiredHoldSeconds - HoldFloorStepSeconds)
            };
        }

        /// <summary>
        /// STAGE 2 — gate clamp. When <paramref name="gateBlocked"/> is true OR
        /// <paramref name="recoverySeverity"/> is at/above <see cref="RecoverySeverity.Recommend"/>,
        /// EVERY requirement is pulled to <c>Math.Min(intended, baseline)</c> so the
        /// override can only hold or LOWER a target, never raise it. The acceptable
        /// resonance window is narrowed toward baseline (min pulled toward baseline, max
        /// pulled DOWN toward baseline) and the pitch comfort-zone is shrunk toward
        /// baseline (min raised toward baseline, max lowered toward baseline) so the window
        /// never widens beyond the unblocked baseline. When neither condition holds,
        /// <paramref name="intended"/> is returned unchanged (Stage 1 already applied).
        /// </summary>
        public static ExerciseTargetProfile ClampAgainstGate(
            ExerciseTargetProfile intended,
            ExerciseTargetProfile baseline,
            bool gateBlocked,
            RecoverySeverity recoverySeverity)
        {
            ArgumentNullException.ThrowIfNull(intended);
            ArgumentNullException.ThrowIfNull(baseline);

            if (!gateBlocked && recoverySeverity < RecoverySeverity.Recommend)
                return intended;

            // Acceptable resonance ceiling: shrink the window toward baseline (never wider).
            var clampedMax = Math.Min(intended.TargetResonanceMax, baseline.TargetResonanceMax);
            // Requirement floor: only ever HELD or LOWERED vs baseline. Also kept ≤ the
            // (lowered) ceiling so the result is always internally consistent — pulling the
            // floor DOWN to the ceiling is the safe direction (more permissive minimum), so
            // the override still never raises a target.
            var clampedMin = Math.Min(
                Math.Min(intended.TargetResonanceMin, baseline.TargetResonanceMin), clampedMax);

            return new ExerciseTargetProfile
            {
                UsesResonance = intended.UsesResonance,
                UsesPitch = intended.UsesPitch,
                UsesStability = intended.UsesStability,
                UsesIntensity = intended.UsesIntensity,
                ClinicalPurposeKey = intended.ClinicalPurposeKey,
                PhysicalFocusKey = intended.PhysicalFocusKey,
                CommonMistakesKey = intended.CommonMistakesKey,
                SafetyInfoKey = intended.SafetyInfoKey,
                FeedbackModeKey = intended.FeedbackModeKey,
                ThresholdStrategyKey = intended.ThresholdStrategyKey,
                IndicatorPackageSummaryKey = intended.IndicatorPackageSummaryKey,

                // Requirements: an override may only HOLD or LOWER vs baseline.
                TargetResonanceMin = clampedMin,
                TargetResonanceMax = clampedMax,
                StabilityThreshold = Math.Min(intended.StabilityThreshold, baseline.StabilityThreshold),
                RequiredHoldSeconds = Math.Min(intended.RequiredHoldSeconds, baseline.RequiredHoldSeconds),

                // Pitch comfort-zone: shrink toward baseline (min up, max down) so the safe
                // window never widens. Null baseline bounds leave the intended bound as-is.
                MinPitch = ShrinkPitchMinTowardBaseline(intended.MinPitch, baseline.MinPitch),
                MaxPitch = ShrinkPitchMaxTowardBaseline(intended.MaxPitch, baseline.MaxPitch)
            };
        }

        /// <summary>Pitch lower bound shrunk toward baseline: <c>Max(intended, baseline)</c>
        /// (a higher floor is the more conservative, narrower comfort zone). Falls back to
        /// whichever bound is present when one is null.</summary>
        private static double? ShrinkPitchMinTowardBaseline(double? intendedMin, double? baselineMin)
        {
            if (!intendedMin.HasValue) return baselineMin;
            if (!baselineMin.HasValue) return intendedMin;
            return Math.Max(intendedMin.Value, baselineMin.Value);
        }

        /// <summary>Pitch upper bound shrunk toward baseline: <c>Min(intended, baseline)</c>
        /// (a lower ceiling is the more conservative, narrower comfort zone). Falls back to
        /// whichever bound is present when one is null.</summary>
        private static double? ShrinkPitchMaxTowardBaseline(double? intendedMax, double? baselineMax)
        {
            if (!intendedMax.HasValue) return baselineMax;
            if (!baselineMax.HasValue) return intendedMax;
            return Math.Min(intendedMax.Value, baselineMax.Value);
        }
    }

    /// <summary>
    /// Applies a <see cref="ManualOverrideRequest"/> under the SAFETY-CRITICAL ordering
    /// (Sprint E, Agent 7). The order is NEVER reversed:
    ///
    ///   (1) evaluate the gate signals — ProgressionSafetyGate blocked? recovery forecast
    ///       severity? (these are the authorities the override is subordinate to);
    ///   (2) take the professional's intended profile;
    ///   (3) apply Stage 1 (recovery floor) then Stage 2 (gate clamp) BEFORE anything is
    ///       persisted.
    ///
    /// The engine never reaches a hard gate (IsLowRecovery / VocalHealthSupervisor Lock):
    /// it only ever writes a MORE-conservative profile. The gate signals are supplied by
    /// the caller (already evaluated against the persisted history) so this engine stays
    /// pure and store-free; the only side effect is the append-only log handed in.
    ///
    /// Persistence is delegated to <see cref="ManualOverridesStore"/> by the caller — this
    /// engine returns the computed result and the clamped profile.
    /// </summary>
    public sealed class ManualOverrideEngine
    {
        // Style-aware resonance multiplier — a darker/androgynous goal must not be pushed
        // toward bright resonance, so its intended resonance targets are scaled down before
        // clamping. Mirrors the direction used across the recommendation/scoring layers
        // (DarkFeminine stronger than Androgynous).
        private const double DarkFeminineResonanceMultiplier = 0.5;
        private const double AndrogynousResonanceMultiplier = 0.75;
        private const double NeutralResonanceMultiplier = 1.0;

        /// <summary>
        /// Evaluates an override request. The <paramref name="baseline"/> is the factory
        /// profile for the targeted exercise (the clinical reference floor).
        /// <paramref name="gateBlocked"/> and <paramref name="recoverySeverity"/> are the
        /// already-evaluated Safety/Recovery signals. <paramref name="style"/> selects the
        /// resonance multiplier.
        ///
        /// Returns a <see cref="ManualOverrideResult"/> carrying the clamped, validated
        /// profile (when the override is an exercise-profile change) or a non-null
        /// <see cref="ManualOverrideResult.BlockedReasonCode"/> when there is nothing to
        /// clamp. This method NEVER persists — the caller logs the result via
        /// <see cref="ManualOverridesStore"/>.
        /// </summary>
        public ManualOverrideResult Evaluate(
            ManualOverrideRequest request,
            ExerciseTargetProfile baseline,
            bool gateBlocked,
            RecoverySeverity recoverySeverity,
            VoiceStyleGoal style)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(baseline);

            // Only an exercise-profile override carries a profile to clamp. Other kinds
            // (recovery plan / voice goals / pace) have no ExerciseTargetProfile to make
            // more conservative here, so we report them as not-applied with a reason code.
            if (request.IntendedProfile is null)
            {
                return new ManualOverrideResult
                {
                    WasApplied = false,
                    WasClamped = false,
                    BlockedReasonCode = "NO_INTENDED_PROFILE",
                    AppliedProfile = null
                };
            }

            // (2) take the professional's intended profile, style-scaling the resonance
            //     targets so a darker/androgynous goal is never pushed toward bright
            //     resonance even before the conservative clamp runs.
            var intended = ApplyStyleResonanceMultiplier(request.IntendedProfile, style);

            // (3) Stage 1 (recovery floor) → Stage 2 (gate clamp), in that fixed order.
            var stage1 = ManualOverrideClamp.ClampProfile(intended, baseline);
            var clamped = ManualOverrideClamp.ClampAgainstGate(stage1, baseline, gateBlocked, recoverySeverity);

            // The persisted profile must always be internally consistent.
            clamped.Validate();

            var wasClamped = !ProfilesEquivalent(intended, clamped);

            return new ManualOverrideResult
            {
                WasApplied = true,
                WasClamped = wasClamped,
                BlockedReasonCode = null,
                AppliedProfile = clamped
            };
        }

        /// <summary>
        /// Scales the intended resonance targets by the style multiplier so a darker /
        /// androgynous goal is never pushed toward bright resonance. Only resonance fields
        /// are affected; stability/hold/pitch are untouched.
        /// </summary>
        private static ExerciseTargetProfile ApplyStyleResonanceMultiplier(
            ExerciseTargetProfile intended, VoiceStyleGoal style)
        {
            var multiplier = ResonanceMultiplierFor(style);
            if (multiplier >= NeutralResonanceMultiplier) return intended;

            return new ExerciseTargetProfile
            {
                UsesResonance = intended.UsesResonance,
                UsesPitch = intended.UsesPitch,
                UsesStability = intended.UsesStability,
                UsesIntensity = intended.UsesIntensity,
                ClinicalPurposeKey = intended.ClinicalPurposeKey,
                PhysicalFocusKey = intended.PhysicalFocusKey,
                CommonMistakesKey = intended.CommonMistakesKey,
                SafetyInfoKey = intended.SafetyInfoKey,
                FeedbackModeKey = intended.FeedbackModeKey,
                ThresholdStrategyKey = intended.ThresholdStrategyKey,
                IndicatorPackageSummaryKey = intended.IndicatorPackageSummaryKey,
                MinPitch = intended.MinPitch,
                MaxPitch = intended.MaxPitch,
                StabilityThreshold = intended.StabilityThreshold,
                RequiredHoldSeconds = intended.RequiredHoldSeconds,
                TargetResonanceMin = intended.TargetResonanceMin * multiplier,
                TargetResonanceMax = intended.TargetResonanceMax * multiplier
            };
        }

        private static double ResonanceMultiplierFor(VoiceStyleGoal style) => style switch
        {
            VoiceStyleGoal.DarkFeminine => DarkFeminineResonanceMultiplier,
            VoiceStyleGoal.Androgynous => AndrogynousResonanceMultiplier,
            _ => NeutralResonanceMultiplier
        };

        /// <summary>
        /// True when two profiles carry identical clamp-relevant requirement values (the
        /// only fields the two-stage clamp can change). Used to flag WasClamped.
        /// </summary>
        private static bool ProfilesEquivalent(ExerciseTargetProfile a, ExerciseTargetProfile b)
        {
            const double epsilon = 1e-9;
            return Math.Abs(a.TargetResonanceMin - b.TargetResonanceMin) < epsilon
                && Math.Abs(a.TargetResonanceMax - b.TargetResonanceMax) < epsilon
                && Math.Abs(a.StabilityThreshold - b.StabilityThreshold) < epsilon
                && Math.Abs(a.RequiredHoldSeconds - b.RequiredHoldSeconds) < epsilon
                && NullableDoubleEquals(a.MinPitch, b.MinPitch)
                && NullableDoubleEquals(a.MaxPitch, b.MaxPitch);
        }

        private static bool NullableDoubleEquals(double? x, double? y)
        {
            if (!x.HasValue || !y.HasValue) return !x.HasValue && !y.HasValue;
            return Math.Abs(x.Value - y.Value) < 1e-9;
        }
    }
}
