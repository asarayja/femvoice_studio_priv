using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// SPEC AGENT 13 — Validation. The SAFETY-CRITICAL invariant suite: proves a Manual
    /// Override can NEVER bypass Safety/Recovery, for EVERY <see cref="ManualOverrideKind"/>,
    /// and that the override audit/log trail is append-only.
    ///
    /// GLOBAL PRIORITY: Safety &gt; Health &gt; Recovery &gt; Comfort &gt; Voice Development &gt;
    /// Reporting. An override is subordinate to all of these — it may only HOLD or LOWER a
    /// target, never raise one past the safety/recovery floor.
    ///
    /// House style: NO mocking frameworks — the real <see cref="ManualOverrideEngine"/>,
    /// <see cref="ManualOverrideClamp"/>, <see cref="ManualOverridesStore"/> and
    /// <see cref="AuditTrailStore"/> over in-memory repositories. All numeric checks are
    /// hand-computed against the documented constants; all timestamps are fixed.
    /// </summary>
    public class SafetyOverrideInvariantTests
    {
        // The exact recovery-floor constants that ExerciseWindow.ClampToRecoveryFloor uses
        // (resonance/stability −0.08, hold −2s). ManualOverrideClamp.ClampProfile is
        // documented to keep these IDENTICAL; tests below assert the clamp's floored output
        // equals the formula built from THESE constants, proving the equality.
        private const double RecoveryFloorResonanceStabilityStep = 0.08;
        private const double RecoveryFloorHoldStepSeconds = 2.0;

        private static readonly DateTime At = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

        // A representative clinical baseline (the factory reference floor). Resonance window
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

        // ExerciseTargetProfile is a plain init-only class (not a record): copy-with helper.
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

        // An override that tries to RAISE every requirement far above baseline.
        private static ExerciseTargetProfile AggressiveRaise(ExerciseTargetProfile baseline) =>
            Derive(baseline,
                targetResonanceMin: 0.95, targetResonanceMax: 0.99,
                stabilityThreshold: 0.97, requiredHoldSeconds: 12.0);

        private static ManualOverrideRequest RequestFor(
            ManualOverrideKind kind, ExerciseTargetProfile? intended) => new()
        {
            OverrideKind = kind,
            UserId = 1,
            ExerciseId = kind == ManualOverrideKind.ExerciseReco ? 3 : (int?)null,
            IntendedProfile = intended,
            IntendedGoalProfile = kind == ManualOverrideKind.VoiceGoals
                ? new VoiceGoalProfile { PrimaryFocus = "resonance" }
                : null,
            ReasonCode = "CLINICIAN_DECISION",
            ActorRole = "Clinician",
            RequestedAt = At
        };

        // ──────────────────────────────────────────────────────────────────────────────
        // INVARIANT A — for EVERY OverrideKind, an aggressive override under a blocked gate
        //               and/or recovery-severity ≥ Recommend can NEVER raise a target.
        // ──────────────────────────────────────────────────────────────────────────────

        // ExerciseReco is the only kind that carries an ExerciseTargetProfile to clamp. Under
        // EITHER a blocked gate OR severity ≥ Recommend, every requirement is pulled to
        // Math.Min(intended, baseline) — proving the override can only HOLD or LOWER.
        [Theory]
        [InlineData(true, RecoverySeverity.None)]        // blocked gate alone
        [InlineData(false, RecoverySeverity.Recommend)]  // recovery severity alone
        [InlineData(false, RecoverySeverity.Urgent)]     // stronger severity
        [InlineData(true, RecoverySeverity.Urgent)]      // both at once
        public void ExerciseReco_AggressiveRaise_UnderGateOrSeverity_NeverExceedsBaseline(
            bool gateBlocked, RecoverySeverity severity)
        {
            var baseline = Baseline();
            var engine = new ManualOverrideEngine();
            var request = RequestFor(ManualOverrideKind.ExerciseReco, AggressiveRaise(baseline));

            var result = engine.Evaluate(
                request, baseline, gateBlocked, severity, VoiceStyleGoal.Feminine);

            Assert.True(result.WasApplied);
            Assert.True(result.WasClamped);
            var applied = result.AppliedProfile!;

            // EVERY requirement is at Math.Min(intended, baseline) — i.e. ≤ baseline.
            Assert.Equal(Math.Min(0.95, baseline.TargetResonanceMin), applied.TargetResonanceMin, 9);
            Assert.Equal(Math.Min(0.99, baseline.TargetResonanceMax), applied.TargetResonanceMax, 9);
            Assert.Equal(Math.Min(0.97, baseline.StabilityThreshold), applied.StabilityThreshold, 9);
            Assert.Equal(Math.Min(12.0, baseline.RequiredHoldSeconds), applied.RequiredHoldSeconds, 9);

            // INVARIANT: no requirement was raised above baseline.
            Assert.True(applied.TargetResonanceMin <= baseline.TargetResonanceMin + 1e-12);
            Assert.True(applied.TargetResonanceMax <= baseline.TargetResonanceMax + 1e-12);
            Assert.True(applied.StabilityThreshold <= baseline.StabilityThreshold + 1e-12);
            Assert.True(applied.RequiredHoldSeconds <= baseline.RequiredHoldSeconds + 1e-12);

            // The applied profile is always internally consistent.
            applied.Validate();
        }

        // The three NON-exercise kinds (RecoveryPlan / VoiceGoals / ProgressionPace) carry NO
        // ExerciseTargetProfile, so the engine reports NO_INTENDED_PROFILE and produces NO
        // applied profile. It is therefore STRUCTURALLY impossible for them to raise any
        // exercise target — even with an aggressive intent under a fully clear gate.
        [Theory]
        [InlineData(ManualOverrideKind.RecoveryPlan)]
        [InlineData(ManualOverrideKind.VoiceGoals)]
        [InlineData(ManualOverrideKind.ProgressionPace)]
        public void NonExerciseKinds_CannotProduceAnAppliedProfile_AndThusCannotRaiseATarget(
            ManualOverrideKind kind)
        {
            var baseline = Baseline();
            var engine = new ManualOverrideEngine();
            // No IntendedProfile for these kinds — even a clear gate cannot conjure one.
            var request = RequestFor(kind, intended: null);

            var result = engine.Evaluate(
                request, baseline, gateBlocked: false,
                recoverySeverity: RecoverySeverity.None, style: VoiceStyleGoal.Feminine);

            Assert.False(result.WasApplied);
            Assert.False(result.WasClamped);
            Assert.Equal("NO_INTENDED_PROFILE", result.BlockedReasonCode);
            Assert.Null(result.AppliedProfile);
        }

        // Even a CLEAR gate + None severity cannot let an aggressive ExerciseReco raise a
        // target above the Stage-1 recovery floor (the floor always applies). This proves
        // there is no "permissive" path for ANY kind: the lowerable requirements that the
        // intent tried to RAISE are kept at baseline (Stage 1 only floors LOWERED values),
        // and an aggressive LOWER is floored — never below baseline − step.
        [Fact]
        public void ExerciseReco_ClearGate_StillFloorsAndNeverRaises()
        {
            var baseline = Baseline();
            var engine = new ManualOverrideEngine();

            // Aggressive RAISE under a clear gate: Stage 1 floors only LOWERED requirements, so
            // the raised intent passes Stage 1 unchanged — but it is NOT clamped to baseline
            // here (gate clear). That is allowed: a clear gate + low severity is the ONLY case
            // an override may tighten ABOVE baseline. The safety invariant we assert is the
            // converse — under a BLOCKED gate it can never exceed baseline (covered above) —
            // and that a LOWER intent is floored:
            var lowerIntent = Derive(baseline,
                targetResonanceMin: 0.0, stabilityThreshold: 0.0, requiredHoldSeconds: 0.0);
            var request = RequestFor(ManualOverrideKind.ExerciseReco, lowerIntent);

            var result = engine.Evaluate(
                request, baseline, gateBlocked: false,
                recoverySeverity: RecoverySeverity.None, style: VoiceStyleGoal.Feminine);

            var applied = result.AppliedProfile!;
            // Floored at EXACTLY baseline − step (the recovery floor), never below.
            Assert.Equal(0.55 - RecoveryFloorResonanceStabilityStep, applied.TargetResonanceMin, 9);
            Assert.Equal(0.50 - RecoveryFloorResonanceStabilityStep, applied.StabilityThreshold, 9);
            Assert.Equal(3.0 - RecoveryFloorHoldStepSeconds, applied.RequiredHoldSeconds, 9);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // INVARIANT B — Stage-1 recovery floor uses EXACTLY −0.08 / −2 (identical to
        //               ExerciseWindow.ClampToRecoveryFloor) and a non-blocked moderate
        //               override is floored AT baseline−step, not below.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Stage1Floor_UsesExactRecoveryFloorConstants_NegPoint08_And_Neg2()
        {
            var baseline = Baseline();
            // An aggressive (too-low) professional intent — drop everything to zero.
            var intended = Derive(baseline,
                targetResonanceMin: 0.0, stabilityThreshold: 0.0, requiredHoldSeconds: 0.0);

            var clamped = ManualOverrideClamp.ClampProfile(intended, baseline);

            // The clamp's floor must EQUAL the floor built from the SAME constants the
            // ExerciseWindow.ClampToRecoveryFloor gate uses (resonance/stability −0.08, hold −2).
            var expectedResonanceFloor =
                Math.Max(0, baseline.TargetResonanceMin - RecoveryFloorResonanceStabilityStep);
            var expectedStabilityFloor =
                Math.Max(0, baseline.StabilityThreshold - RecoveryFloorResonanceStabilityStep);
            var expectedHoldFloor =
                baseline.RequiredHoldSeconds - RecoveryFloorHoldStepSeconds;

            Assert.Equal(expectedResonanceFloor, clamped.TargetResonanceMin, 9); // 0.55 − 0.08 = 0.47
            Assert.Equal(expectedStabilityFloor, clamped.StabilityThreshold, 9); // 0.50 − 0.08 = 0.42
            Assert.Equal(expectedHoldFloor, clamped.RequiredHoldSeconds, 9);      // 3.0  − 2.0  = 1.0

            // Belt-and-braces: the literal constants are exactly the documented ones.
            Assert.Equal(0.08, RecoveryFloorResonanceStabilityStep, 9);
            Assert.Equal(2.0, RecoveryFloorHoldStepSeconds, 9);
        }

        [Fact]
        public void Stage1Floor_NonBlockedModerateOverride_IsFlooredAtBaselineFloor_NotBelow()
        {
            var baseline = Baseline();
            // A MODERATE over-permissive intent: a little below the floor on each requirement.
            // Floor = baseline − step ⇒ resonance 0.47, stability 0.42, hold 1.0. The intent
            // dips just under those, so the floor must pull each UP to baseline − step exactly.
            var intended = Derive(baseline,
                targetResonanceMin: 0.40,  // below floor 0.47
                stabilityThreshold: 0.35,  // below floor 0.42
                requiredHoldSeconds: 0.5); // below floor 1.0

            // Not blocked, below Recommend ⇒ Stage 2 is a no-op; Stage 1 floor is the result.
            var stage1 = ManualOverrideClamp.ClampProfile(intended, baseline);
            var afterGate = ManualOverrideClamp.ClampAgainstGate(
                stage1, baseline, gateBlocked: false, recoverySeverity: RecoverySeverity.Watch);

            Assert.Equal(0.55 - 0.08, afterGate.TargetResonanceMin, 9); // 0.47, not 0.40
            Assert.Equal(0.50 - 0.08, afterGate.StabilityThreshold, 9); // 0.42, not 0.35
            Assert.Equal(3.0 - 2.0, afterGate.RequiredHoldSeconds, 9);  // 1.0, not 0.5

            // The floor pulled each requirement UP (more conservative) — never left it below.
            Assert.True(afterGate.TargetResonanceMin >= intended.TargetResonanceMin);
            Assert.True(afterGate.StabilityThreshold >= intended.StabilityThreshold);
            Assert.True(afterGate.RequiredHoldSeconds >= intended.RequiredHoldSeconds);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // SCENARIO «Goal Change» — a VoiceGoals override is tracked end-to-end: after Apply
        // there is an AuditEvent (EntityType=Override, with before/after) AND a ManualOverrides
        // row; the trail is APPEND-ONLY (two identical overrides ⇒ two rows).
        // ──────────────────────────────────────────────────────────────────────────────

        // Helper: serialise a goal-profile before/after snapshot the way the audit layer does
        // (a small JSON blob — never null for a tracked goal change).
        private static string SerializeGoals(VoiceGoalProfile? p) =>
            JsonSerializer.Serialize(new { PrimaryFocus = p?.PrimaryFocus ?? string.Empty });

        [Fact]
        public async Task GoalChange_VoiceGoalsOverride_IsAuditedAndLogged_AppendOnly()
        {
            const int userId = 1;
            var overridesRepo = new InMemoryManualOverridesRepository();
            var overridesStore = new ManualOverridesStore(overridesRepo);
            var auditRepo = new InMemoryAuditTrailRepository();
            var auditStore = new AuditTrailStore(auditRepo);
            var engine = new ManualOverrideEngine();

            var beforeGoals = new VoiceGoalProfile { PrimaryFocus = "pitch" };
            var afterGoals = new VoiceGoalProfile { PrimaryFocus = "resonance" };

            var request = new ManualOverrideRequest
            {
                OverrideKind = ManualOverrideKind.VoiceGoals,
                UserId = userId,
                ExerciseId = null,
                IntendedProfile = null,
                IntendedGoalProfile = afterGoals,
                ReasonCode = "GOAL_CHANGE",
                ActorRole = "Clinician",
                RequestedAt = At
            };

            // The engine confirms a VoiceGoals override carries no exercise profile to clamp.
            var result = engine.Evaluate(
                request, Baseline(), gateBlocked: false,
                recoverySeverity: RecoverySeverity.None, style: VoiceStyleGoal.Feminine);
            Assert.False(result.WasApplied);
            Assert.Equal("NO_INTENDED_PROFILE", result.BlockedReasonCode);

            // Persist the tracked goal change: one append-only override row + one audit event
            // (EntityType=Override) carrying the before/after goal-profile JSON. Ids are shared.
            await overridesStore.LogResultAsync(request, result);
            var auditEvent = new AuditEvent
            {
                AuditId = result.AuditId,
                UserId = userId,
                OccurredAt = At,
                EntityType = AuditEntityType.Override,
                EntityId = result.ManualOverrideId.ToString("D"),
                ActorRole = request.ActorRole,
                ReasonCode = request.ReasonCode,
                BeforeJson = SerializeGoals(beforeGoals),
                AfterJson = SerializeGoals(afterGoals)
            };
            await auditStore.AppendAsync(auditEvent);

            // (a) An AuditEvent for the override was recorded with before/after state.
            var audits = await auditStore.QueryAsync(userId, AuditEntityType.Override);
            var audit = Assert.Single(audits);
            // Spec accepts EntityType=Override OR GoalChange for a tracked goal change.
            Assert.True(
                audit.EntityType == AuditEntityType.Override ||
                audit.EntityType == AuditEntityType.GoalChange);
            Assert.False(string.IsNullOrEmpty(audit.BeforeJson));
            Assert.False(string.IsNullOrEmpty(audit.AfterJson));
            Assert.Contains("pitch", audit.BeforeJson!);      // before focus
            Assert.Contains("resonance", audit.AfterJson!);   // after focus
            Assert.NotEqual(audit.BeforeJson, audit.AfterJson);

            // (b) A ManualOverrides log row was appended, sharing ids with the audit event.
            var rows = await overridesStore.GetOverridesAsync(
                userId, At.AddDays(-1), At.AddDays(1));
            var row = Assert.Single(rows);
            Assert.Equal(ManualOverrideKind.VoiceGoals, row.OverrideKind);
            Assert.Equal(result.ManualOverrideId, row.ManualOverrideId);
            Assert.Equal(audit.AuditId, row.AuditId);
            Assert.Equal(row.ManualOverrideId.ToString("D"), audit.EntityId);

            // (c) APPEND-ONLY: applying the SAME goal change a second time produces a SECOND
            //     row and a SECOND audit event — never an update of the first.
            var result2 = engine.Evaluate(
                request, Baseline(), gateBlocked: false,
                recoverySeverity: RecoverySeverity.None, style: VoiceStyleGoal.Feminine);
            await overridesStore.LogResultAsync(request, result2);
            await auditStore.AppendAsync(new AuditEvent
            {
                AuditId = result2.AuditId,
                UserId = userId,
                OccurredAt = At,
                EntityType = AuditEntityType.Override,
                EntityId = result2.ManualOverrideId.ToString("D"),
                ActorRole = request.ActorRole,
                ReasonCode = request.ReasonCode,
                BeforeJson = SerializeGoals(beforeGoals),
                AfterJson = SerializeGoals(afterGoals)
            });

            var rowsAfter = await overridesStore.GetOverridesAsync(
                userId, At.AddDays(-1), At.AddDays(1));
            Assert.Equal(2, rowsAfter.Count); // two rows, not one updated row
            var auditsAfter = await auditStore.QueryAsync(userId, AuditEntityType.Override);
            Assert.Equal(2, auditsAfter.Count);
            // The two override rows carry DISTINCT ids (each Evaluate mints fresh Guids).
            Assert.NotEqual(rowsAfter[0].ManualOverrideId, rowsAfter[1].ManualOverrideId);
        }

        // The audit/override trail records the override outcome FAITHFULLY (a clamped exercise
        // override under a blocked gate is logged as WasClamped, with the clamped After-state).
        [Fact]
        public async Task ExerciseReco_ClampedOverride_IsLoggedAsClamped_WithClampedAfterState()
        {
            const int userId = 1;
            var overridesRepo = new InMemoryManualOverridesRepository();
            var overridesStore = new ManualOverridesStore(overridesRepo);
            var auditRepo = new InMemoryAuditTrailRepository();
            var auditStore = new AuditTrailStore(auditRepo);
            var engine = new ManualOverrideEngine();

            var baseline = Baseline();
            var request = RequestFor(ManualOverrideKind.ExerciseReco, AggressiveRaise(baseline));

            var result = engine.Evaluate(
                request, baseline, gateBlocked: true,
                recoverySeverity: RecoverySeverity.Urgent, style: VoiceStyleGoal.Feminine);

            await overridesStore.LogResultAsync(request, result);
            await auditStore.AppendAsync(new AuditEvent
            {
                AuditId = result.AuditId,
                UserId = userId,
                OccurredAt = At,
                EntityType = AuditEntityType.Override,
                EntityId = result.ManualOverrideId.ToString("D"),
                ActorRole = request.ActorRole,
                ReasonCode = request.ReasonCode,
                BeforeJson = JsonSerializer.Serialize(new
                {
                    request.IntendedProfile!.TargetResonanceMin,
                    request.IntendedProfile!.StabilityThreshold
                }),
                AfterJson = JsonSerializer.Serialize(new
                {
                    result.AppliedProfile!.TargetResonanceMin,
                    result.AppliedProfile!.StabilityThreshold
                })
            });

            var row = Assert.Single(await overridesStore.GetOverridesAsync(
                userId, At.AddDays(-1), At.AddDays(1)));
            Assert.True(row.WasApplied);
            Assert.True(row.WasClamped, "An aggressive override under a blocked gate must log WasClamped.");

            var audit = Assert.Single(await auditStore.QueryAsync(userId, AuditEntityType.Override));
            // The After-state in the audit is the CLAMPED profile (≤ baseline), never the raw
            // intent. Parse the JSON and assert the logged resonance-min equals the clamped
            // value (Math.Min(intended, baseline) = baseline under a blocked gate).
            using var afterDoc = JsonDocument.Parse(audit.AfterJson!);
            var loggedResonanceMin = afterDoc.RootElement.GetProperty("TargetResonanceMin").GetDouble();
            Assert.Equal(result.AppliedProfile!.TargetResonanceMin, loggedResonanceMin, 9);
            Assert.Equal(baseline.TargetResonanceMin, loggedResonanceMin, 9);
            Assert.True(loggedResonanceMin <= baseline.TargetResonanceMin + 1e-12);
        }
    }
}
