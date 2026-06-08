using System;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Agent 7 (Manual Override UI + audit wiring) — SAFETY-CRITICAL integration tests.
    ///
    /// Proves end-to-end, through the real <see cref="ManualOverrideViewModel"/> + real
    /// <see cref="ManualOverrideEngine"/> + in-memory stores, that:
    ///   • an aggressive professional override under a BLOCKED gate is CLAMPED — it never
    ///     raises a requirement past the factory baseline;
    ///   • the VM exposes WasClamped / WasBlocked / BlockedReasonCode reflecting the clamp;
    ///   • the outcome is persisted as BOTH a ManualOverrides log row AND an AuditEvent
    ///     (EntityType=Override, with Before/After JSON) AND a MANUAL_OVERRIDE health event.
    ///
    /// House style: no mocking frameworks — real classes + the in-memory repositories.
    /// </summary>
    public class ManualOverrideIntegrationTests
    {
        private const int UserId = 1;

        // The factory baseline the engine clamps against (ResonanceHumming):
        // resonance [0.50, 0.85], stability 0.45, hold 3s.
        private static readonly ExerciseProfileType ProfileType = ExerciseProfileType.ResonanceHumming;

        private static (ManualOverrideViewModel vm,
                        ManualOverridesStore overridesStore,
                        AuditTrailStore auditStore,
                        SessionAnalyticsStore analyticsStore,
                        InMemoryManualOverridesRepository overridesRepo,
                        InMemoryAuditTrailRepository auditRepo)
            BuildViewModel(bool? forcedGateBlocked, RecoverySeverity? forcedSeverity = null,
                           ProgressionSafetyGate? liveGate = null,
                           SessionAnalyticsStore? sharedAnalytics = null)
        {
            var overridesRepo = new InMemoryManualOverridesRepository();
            var overridesStore = new ManualOverridesStore(overridesRepo);

            var auditRepo = new InMemoryAuditTrailRepository();
            var auditStore = new AuditTrailStore(auditRepo);

            var analyticsStore = sharedAnalytics
                ?? new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());

            var vm = new ManualOverrideViewModel(
                engine: new ManualOverrideEngine(),
                overridesStore: overridesStore,
                auditStore: auditStore,
                analyticsStore: analyticsStore,
                profileFactory: new ExerciseProfileFactory(),
                safetyGate: liveGate,
                recoveryService: null,
                forcedGateBlocked: forcedGateBlocked,
                forcedRecoverySeverity: forcedSeverity);

            vm.UserId = UserId;
            vm.SelectedKind = ManualOverrideKind.ExerciseReco;
            vm.BaselineProfileType = ProfileType;
            vm.ExerciseId = 1;
            return (vm, overridesStore, auditStore, analyticsStore, overridesRepo, auditRepo);
        }

        // An override that tries to RAISE every requirement well past the baseline.
        private static void SetAggressiveIntent(ManualOverrideViewModel vm)
        {
            vm.IntendedResonanceMin = 0.99;        // baseline 0.50
            vm.IntendedResonanceMax = 1.00;        // baseline 0.85
            vm.IntendedStabilityThreshold = 0.99;  // baseline 0.45
            vm.IntendedRequiredHoldSeconds = 30.0; // baseline 3.0
            vm.StyleGoal = VoiceStyleGoal.Feminine;
            vm.ReasonCode = "CLINICIAN_DECISION";
        }

        // ── 1. Blocked gate ⇒ aggressive override is CLAMPED, never raised, and logged ──
        [Fact]
        public async Task Apply_AggressiveOverrideUnderBlockedGate_IsClampedAndLogged()
        {
            var (vm, _, _, _, overridesRepo, auditRepo) = BuildViewModel(forcedGateBlocked: true);
            SetAggressiveIntent(vm);

            var baseline = ExerciseTargetProfile.CreateResonanceHumming();

            await vm.ApplyCommand.ExecuteAsync(null);

            // (a) The clamp engaged and the override WAS applied (it produced a profile).
            Assert.True(vm.HasResult);
            Assert.True(vm.WasApplied);
            Assert.True(vm.WasClamped, "An aggressive override under a blocked gate must be clamped.");
            Assert.False(vm.WasBlocked);
            Assert.Null(vm.BlockedReasonCode);
            Assert.True(vm.GateWasBlocked);

            // (b) INVARIANT — the clamped (presented) result never raises a target past
            //     baseline. Under a blocked gate every requirement is held at/below baseline.
            Assert.True(vm.AppliedResonanceMin <= baseline.TargetResonanceMin + 1e-9);
            Assert.True(vm.AppliedResonanceMax <= baseline.TargetResonanceMax + 1e-9);
            Assert.True(vm.AppliedStabilityThreshold <= baseline.StabilityThreshold + 1e-9);
            Assert.True(vm.AppliedRequiredHoldSeconds <= baseline.RequiredHoldSeconds + 1e-9);

            // (c) A ManualOverrides log row was appended (append-only), reflecting WasClamped.
            var rows = await overridesRepo.GetOverridesAsync(
                UserId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            var row = Assert.Single(rows);
            Assert.True(row.WasApplied);
            Assert.True(row.WasClamped);
            Assert.Equal(ManualOverrideKind.ExerciseReco, row.OverrideKind);

            // (d) An AuditEvent (EntityType=Override) was appended with Before/After JSON.
            var audits = await auditRepo.QueryAsync(UserId, AuditEntityType.Override);
            var audit = Assert.Single(audits);
            Assert.Equal(AuditEntityType.Override, audit.EntityType);
            Assert.False(string.IsNullOrEmpty(audit.BeforeJson));
            Assert.False(string.IsNullOrEmpty(audit.AfterJson));
            // The log row and the audit event share identifiers (mapped from the result).
            Assert.Equal(row.AuditId, audit.AuditId);
            Assert.Equal(row.ManualOverrideId.ToString("D"), audit.EntityId);

            // (e) The VM reports the outcome as logged.
            Assert.True(vm.WasLogged);
        }

        // ── 2. A MANUAL_OVERRIDE health event is written (non-safety-signal type) ───────
        [Fact]
        public async Task Apply_WritesManualOverrideHealthEvent_WithoutPollutingSafetySignals()
        {
            var analytics = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var (vm, _, _, _, _, _) = BuildViewModel(forcedGateBlocked: true, sharedAnalytics: analytics);
            SetAggressiveIntent(vm);

            await vm.ApplyCommand.ExecuteAsync(null);

            var events = await analytics.GetHealthEventsAsync(
                DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), UserId);

            var manualOverrideEvent = Assert.Single(
                events.Where(e => e.ReasonCode == ManualOverrideViewModel.ManualOverrideReasonCode));
            Assert.Equal(ManualOverrideViewModel.ManualOverrideReasonCode, manualOverrideEvent.ReasonCode);

            // CRITICAL: the health event must NOT be a gate-counted safety/recovery signal,
            // so a journaled override can never itself trigger a safety block.
            Assert.NotEqual(HealthAnalyticsEventType.SafetyFreeze, manualOverrideEvent.EventType);
            Assert.NotEqual(HealthAnalyticsEventType.StrainPeriod, manualOverrideEvent.EventType);
            Assert.NotEqual(HealthAnalyticsEventType.PauseRecommended, manualOverrideEvent.EventType);
            Assert.NotEqual(HealthAnalyticsEventType.HydrationSuggested, manualOverrideEvent.EventType);
            Assert.NotEqual(HealthAnalyticsEventType.ComfortZoneBreach, manualOverrideEvent.EventType);
        }

        // ── 3. End-to-end with the REAL ProgressionSafetyGate reading persisted events ──
        [Fact]
        public async Task Apply_WithLiveBlockedSafetyGate_ClampsAgainstBaseline()
        {
            var analytics = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());

            // Seed two SafetyFreeze events in the last 7 days ⇒ ProgressionSafetyGate blocks
            // (REPEATED_SAFETY_LOCKS). The override must then be clamped against baseline.
            var now = DateTime.UtcNow;
            for (var i = 0; i < 2; i++)
            {
                await analytics.RecordHealthEventAsync(new HealthAnalyticsEvent
                {
                    SessionId = 100 + i,
                    UserId = UserId,
                    EventType = HealthAnalyticsEventType.SafetyFreeze,
                    OccurredAt = now.AddDays(-1 - i),
                    Severity = 1.0,
                    ReasonCode = "SAFETY_LOCK"
                });
            }

            var liveGate = new ProgressionSafetyGate(analytics);
            // forcedGateBlocked: null ⇒ the VM consults the real gate over the seeded history.
            var (vm, _, _, _, overridesRepo, auditRepo) =
                BuildViewModel(forcedGateBlocked: null, liveGate: liveGate, sharedAnalytics: analytics);
            SetAggressiveIntent(vm);

            var baseline = ExerciseTargetProfile.CreateResonanceHumming();

            await vm.ApplyCommand.ExecuteAsync(null);

            Assert.True(vm.GateWasBlocked, "Two recent SafetyFreeze events must block the gate.");
            Assert.True(vm.WasClamped);
            Assert.True(vm.AppliedResonanceMin <= baseline.TargetResonanceMin + 1e-9);
            Assert.True(vm.AppliedStabilityThreshold <= baseline.StabilityThreshold + 1e-9);
            Assert.True(vm.AppliedRequiredHoldSeconds <= baseline.RequiredHoldSeconds + 1e-9);

            // Override row + audit event both produced.
            var rows = await overridesRepo.GetOverridesAsync(
                UserId, now.AddDays(-1), now.AddDays(1));
            Assert.Single(rows);
            var audits = await auditRepo.QueryAsync(UserId, AuditEntityType.Override);
            Assert.Single(audits);
        }

        // ── 4. Unblocked + low severity ⇒ a conservative intent passes through unclamped ─
        [Fact]
        public async Task Apply_ConservativeIntentUnblocked_IsNotClampedButStillLogged()
        {
            var (vm, _, _, _, overridesRepo, auditRepo) =
                BuildViewModel(forcedGateBlocked: false, forcedSeverity: RecoverySeverity.None);

            // A MORE-conservative intent (tighter than baseline) — the engine should keep it.
            vm.IntendedResonanceMin = 0.70;        // > baseline 0.50 (conservative)
            vm.IntendedResonanceMax = 0.85;        // == baseline ceiling
            vm.IntendedStabilityThreshold = 0.65;  // > baseline 0.45 (conservative)
            vm.IntendedRequiredHoldSeconds = 5.0;  // > baseline 3.0 (conservative)
            vm.StyleGoal = VoiceStyleGoal.Feminine;

            await vm.ApplyCommand.ExecuteAsync(null);

            Assert.True(vm.WasApplied);
            Assert.False(vm.WasClamped);
            Assert.False(vm.GateWasBlocked);

            // Conservative values are preserved on the applied result.
            Assert.Equal(0.70, vm.AppliedResonanceMin, 9);
            Assert.Equal(0.65, vm.AppliedStabilityThreshold, 9);
            Assert.Equal(5.0, vm.AppliedRequiredHoldSeconds, 9);

            // Still fully audited/logged.
            Assert.Single(await overridesRepo.GetOverridesAsync(
                UserId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));
            Assert.Single(await auditRepo.QueryAsync(UserId, AuditEntityType.Override));
            Assert.True(vm.WasLogged);
        }
    }
}
