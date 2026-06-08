using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Agent 12 (Pilot Study Readiness) — verifies the READ-ONLY
    /// <see cref="PilotReadinessChecker"/> harness.
    ///
    /// House style: no mocking frameworks — real classes + in-memory fakes
    /// (InMemoryXxxRepository) wrapped in the real store facades. A few deliberately
    /// broken in-memory repositories drive single checks to failure so the report's
    /// per-check bool + blocker list can be asserted precisely.
    ///
    /// Determinism: a fixed <c>Now</c> is always passed; nothing depends on wall-clock time.
    /// </summary>
    public class PilotReadinessCheckerTests
    {
        private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // ── Context builders ────────────────────────────────────────────────────────

        /// <summary>A fully-healthy context built entirely from in-memory fakes.</summary>
        private static PilotReadinessContext HealthyContext() => new(
            outcomeProfiles: new OutcomeProfileStore(new InMemoryOutcomeProfileRepository()),
            auditTrail: new AuditTrailStore(new InMemoryAuditTrailRepository()),
            manualOverrides: new ManualOverridesStore(new InMemoryManualOverridesRepository()),
            clinicalNotes: new ClinicalNotesStore(new InMemoryClinicalNotesRepository()),
            caseReviews: new CaseReviewsStore(new InMemoryCaseReviewsRepository()));

        // ── 1. All systems healthy ⇒ IsPilotReady true, no blockers ─────────────────
        [Fact]
        public async Task CheckAsync_AllSystemsHealthy_IsPilotReadyTrue()
        {
            var checker = new PilotReadinessChecker();

            var report = await checker.CheckAsync(HealthyContext(), Now);

            Assert.True(report.IsPilotReady);
            Assert.Empty(report.Blockers);

            // Every individual check passed.
            Assert.True(report.StoresReady);
            Assert.True(report.AuditAppendOnly);
            Assert.True(report.AnonymizerClean);
            Assert.True(report.ExportNonEmpty);
            Assert.True(report.OutcomeTrackingWorks);
            Assert.True(report.OverrideInvariantHolds);
        }

        // ── 2. Exactly six checks are run, all named uniquely ───────────────────────
        [Fact]
        public async Task CheckAsync_RunsSixUniquelyNamedChecks()
        {
            var checker = new PilotReadinessChecker();

            var report = await checker.CheckAsync(HealthyContext(), Now);

            Assert.Equal(6, report.Checks.Count);
            Assert.Equal(6, report.Checks.Select(c => c.Name).Distinct().Count());
        }

        // ── 3. Broken store read ⇒ not ready + a StoresConstructible blocker ────────
        [Fact]
        public async Task CheckAsync_StoreReadAlwaysNull_BlocksOnStoresConstructible()
        {
            var checker = new PilotReadinessChecker();
            // CaseReviews write succeeds but GetById always returns null → round-trip fails.
            var context = new PilotReadinessContext(
                outcomeProfiles: new OutcomeProfileStore(new InMemoryOutcomeProfileRepository()),
                auditTrail: new AuditTrailStore(new InMemoryAuditTrailRepository()),
                manualOverrides: new ManualOverridesStore(new InMemoryManualOverridesRepository()),
                clinicalNotes: new ClinicalNotesStore(new InMemoryClinicalNotesRepository()),
                caseReviews: new CaseReviewsStore(new ReadReturnsNullCaseReviewsRepository()));

            var report = await checker.CheckAsync(context, Now);

            Assert.False(report.IsPilotReady);
            Assert.False(report.StoresReady);
            Assert.Contains(report.Blockers, b => b.StartsWith("StoresConstructible:", StringComparison.Ordinal));

            // The independent checks remain green — only the store check failed.
            Assert.True(report.AuditAppendOnly);
            Assert.True(report.AnonymizerClean);
            Assert.True(report.ExportNonEmpty);
            Assert.True(report.OverrideInvariantHolds);
        }

        // ── 4. Audit that DEDUPLICATES ⇒ append-only blocker ────────────────────────
        [Fact]
        public async Task CheckAsync_AuditDeduplicates_BlocksOnAuditAppendOnly()
        {
            var checker = new PilotReadinessChecker();
            // This repository updates-on-duplicate-AuditId instead of appending → 1 row.
            var context = new PilotReadinessContext(
                outcomeProfiles: new OutcomeProfileStore(new InMemoryOutcomeProfileRepository()),
                auditTrail: new AuditTrailStore(new DeduplicatingAuditRepository()),
                manualOverrides: new ManualOverridesStore(new InMemoryManualOverridesRepository()),
                clinicalNotes: new ClinicalNotesStore(new InMemoryClinicalNotesRepository()),
                caseReviews: new CaseReviewsStore(new InMemoryCaseReviewsRepository()));

            var report = await checker.CheckAsync(context, Now);

            Assert.False(report.IsPilotReady);
            Assert.False(report.AuditAppendOnly);
            Assert.Contains(report.Blockers, b => b.StartsWith("AuditAppendOnly:", StringComparison.Ordinal));
        }

        // ── 5. Outcome reads return null ⇒ outcome-tracking blocker ─────────────────
        [Fact]
        public async Task CheckAsync_OutcomeReadReturnsNull_BlocksOnOutcomeTracking()
        {
            var checker = new PilotReadinessChecker();
            var context = new PilotReadinessContext(
                outcomeProfiles: new OutcomeProfileStore(new ReadReturnsNullOutcomeRepository()),
                auditTrail: new AuditTrailStore(new InMemoryAuditTrailRepository()),
                manualOverrides: new ManualOverridesStore(new InMemoryManualOverridesRepository()),
                clinicalNotes: new ClinicalNotesStore(new InMemoryClinicalNotesRepository()),
                caseReviews: new CaseReviewsStore(new InMemoryCaseReviewsRepository()));

            var report = await checker.CheckAsync(context, Now);

            Assert.False(report.IsPilotReady);
            Assert.False(report.OutcomeTrackingWorks);
            // The store round-trip ALSO reads outcome back, so it blocks too — both surface.
            Assert.Contains(report.Blockers, b => b.StartsWith("OutcomeTracking:", StringComparison.Ordinal));
        }

        // ── 6. A throwing store is caught and surfaced as a blocker (never bubbles) ──
        [Fact]
        public async Task CheckAsync_ThrowingStore_IsCaughtAndReportedNotThrown()
        {
            var checker = new PilotReadinessChecker();
            var context = new PilotReadinessContext(
                outcomeProfiles: new OutcomeProfileStore(new InMemoryOutcomeProfileRepository()),
                auditTrail: new AuditTrailStore(new InMemoryAuditTrailRepository()),
                manualOverrides: new ManualOverridesStore(new InMemoryManualOverridesRepository()),
                clinicalNotes: new ClinicalNotesStore(new ThrowingClinicalNotesRepository()),
                caseReviews: new CaseReviewsStore(new InMemoryCaseReviewsRepository()));

            // Must NOT throw — the harness catches and records the failure.
            var report = await checker.CheckAsync(context, Now);

            Assert.False(report.IsPilotReady);
            Assert.False(report.StoresReady);
            Assert.Contains(report.Blockers, b => b.Contains("Threw", StringComparison.Ordinal));
        }

        // ── 7. Empty report (no checks) is NOT pilot-ready ──────────────────────────
        [Fact]
        public void Report_WithNoChecks_IsNotPilotReady()
        {
            var report = new PilotReadinessReport();
            Assert.False(report.IsPilotReady);
            Assert.Empty(report.Blockers);
        }

        // ── 8. Blockers carry the check name + detail for every failed check ────────
        [Fact]
        public void Report_Blockers_ProjectFailedChecksWithDetail()
        {
            var report = new PilotReadinessReport
            {
                Checks = new[]
                {
                    new PilotReadinessCheck("A", true, "ok"),
                    new PilotReadinessCheck("B", false, "broke"),
                    new PilotReadinessCheck("C", false, "also broke"),
                }
            };

            Assert.False(report.IsPilotReady);
            Assert.Equal(2, report.Blockers.Count);
            Assert.Contains("B: broke", report.Blockers);
            Assert.Contains("C: also broke", report.Blockers);
        }

        // ── 9. Null context throws (programmer error, not a check failure) ──────────
        [Fact]
        public async Task CheckAsync_NullContext_Throws()
        {
            var checker = new PilotReadinessChecker();
            await Assert.ThrowsAsync<ArgumentNullException>(() => checker.CheckAsync(null!, Now));
        }

        // ── Broken in-memory repository fakes (real classes, no mocking framework) ──

        /// <summary>CaseReviews repo whose reads always return null (write is a no-op success).</summary>
        private sealed class ReadReturnsNullCaseReviewsRepository : ICaseReviewsRepository
        {
            public Task SaveAsync(CaseReview review, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<IReadOnlyList<CaseReview>> GetByUserAsync(int userId, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<CaseReview>>(Array.Empty<CaseReview>());

            public Task<IReadOnlyList<CaseReview>> GetByUserAndTypeAsync(
                int userId, ReviewType reviewType, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<CaseReview>>(Array.Empty<CaseReview>());

            public Task<CaseReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default)
                => Task.FromResult<CaseReview?>(null);
        }

        /// <summary>Audit repo that UPDATES on duplicate AuditId instead of appending.</summary>
        private sealed class DeduplicatingAuditRepository : IAuditTrailRepository
        {
            private readonly Dictionary<Guid, AuditEvent> _byId = new();

            public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
            {
                _byId[auditEvent.AuditId] = auditEvent; // dedup — the append-only violation.
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<AuditEvent>> QueryAsync(
                int userId, AuditEntityType? entityType = null,
                DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
            {
                var result = _byId.Values
                    .Where(e => e.UserId == userId)
                    .Where(e => entityType == null || e.EntityType == entityType)
                    .ToList();
                return Task.FromResult<IReadOnlyList<AuditEvent>>(result);
            }
        }

        /// <summary>Outcome repo whose reads always return null (write is a no-op success).</summary>
        private sealed class ReadReturnsNullOutcomeRepository : IOutcomeProfileRepository
        {
            public Task SaveSnapshotAsync(Guid outcomeId, OutcomeProfile profile, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<OutcomeProfile?> GetSnapshotAsync(Guid outcomeId, CancellationToken cancellationToken = default)
                => Task.FromResult<OutcomeProfile?>(null);

            public Task<OutcomeProfile?> GetLatestForUserAsync(int userId, CancellationToken cancellationToken = default)
                => Task.FromResult<OutcomeProfile?>(null);
        }

        /// <summary>Clinical-notes repo that throws on save (simulates a broken/locked store).</summary>
        private sealed class ThrowingClinicalNotesRepository : IClinicalNotesRepository
        {
            public Task SaveNoteAsync(ClinicalNote note, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("simulated store failure");

            public Task<IReadOnlyList<ClinicalNote>> GetNotesAsync(
                int userId, ClinicalNoteType noteType, DateTime from, DateTime to,
                CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<ClinicalNote>>(Array.Empty<ClinicalNote>());
        }
    }
}
